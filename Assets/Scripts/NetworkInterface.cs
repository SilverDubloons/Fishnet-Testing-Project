using FishNet.Managing;
using FishNet.Managing.Transporting;
using FishNet.Transporting;
using FishyFacepunch;
using Steamworks;
using Steamworks.Data;
using System;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using static CachedLobbyData;
using System.Collections.Generic;

// Unity version 6000.2.9f1
// FishNetworking version 4.6.16R.Prerelease.01
// FishyFacepunch version 4.1.0
// Facepunch version 2.4.1

public class NetworkInterface : MonoBehaviour
{
	[SerializeField] private FishyFacepunch.FishyFacepunch fishyFacepunch;
	[SerializeField] private NetworkManager networkManager;
	public int lobbySize = 4;
	public uint appId;
    public CachedLobbyData cachedPartyLobbyData;
	public bool DefaultLobbiesToInvisible = false;

    private bool eventsSubscribed = false;
	// private bool searchingForMatch = false;

	private LocalConnectionState serverState = LocalConnectionState.Stopped;
	private LocalConnectionState clientState = LocalConnectionState.Stopped;
	private Lobby? partyLobby = null;
	private Lobby? matchmakingLobby = null;
	// private List<Friend> friendsQueuedWith = new();

	// public FishyFacepunch fishyFacepunch;
	public static NetworkInterface instance;

	private void Awake()
	{
		instance = this;
	}
	private void Start()
	{
		networkManager.ServerManager.OnServerConnectionState += OnServerConnectionStateChanged;
		networkManager.ClientManager.OnClientConnectionState += OnClientConnectionStateChanged;
		SubscribeToSteamEvents();
		StartAppropriateLobby();
	}
	private async void StartAppropriateLobby()
	{
		try
		{
			bool joinedFromSteamCommandLine = await JoinLobbyFromSteamCommandLine();
			if (!joinedFromSteamCommandLine)
			{
				bool joinedFromCommandLine = await JoinLobbyFromCommandLine();
			}
			SetFriendsCanJoin();
		}
		catch (Exception e)
		{
			Logger.instance.Error($"Could not start lobby. Exception: {e.Message}");
		}
	}
	private void Update()
	{
		SteamClient.RunCallbacks();
	}
	private void OnDestroy()
	{
		if (networkManager != null)
		{
			networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionStateChanged;
			networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionStateChanged;
		}
        LeaveCurrentPartyLobby();
        LeaveCurrentMatchmakingLobby();
        UnsubscribeFromSteamEvents();
		SetFriendsCanJoin();
#if UNITY_EDITOR
		SetRichPresence("gamestatus", "AtMainMenu");
		SetRichPresence("steam_display", "#StatusWithoutHealth");
#endif
	}
	private void OnApplicationQuit()
	{
        LeaveCurrentPartyLobby();
        LeaveCurrentMatchmakingLobby();
        UnsubscribeFromSteamEvents();
		SetFriendsCanJoin();
#if UNITY_EDITOR
        SetRichPresence("gamestatus", "AtMainMenu");
        SetRichPresence("steam_display", "#StatusWithoutHealth");
#endif
	}
	private async Task<bool> JoinLobbyFromCommandLine()
	{
		string lobbyIdStr = LocalInterface.GetCommandLineArgument("+connect_lobby");
		Logger.instance.Log($"JoinLobbyFromCommandLine lobbyIdStr: {lobbyIdStr}", 10);
		if (string.IsNullOrEmpty(lobbyIdStr))
		{
			return false;
		}
		return await JoinSteamPartyLobbyFromId((SteamId)ulong.Parse(lobbyIdStr));
	}
	private async Task<bool> JoinLobbyFromSteamCommandLine()
	{
		string steamCommandLine = SteamApps.CommandLine;
		Logger.instance.Log($"JoinLobbyFromSteamCommandLine steamCommandLine: \"{steamCommandLine}\"", 10);
		if (string.IsNullOrEmpty(steamCommandLine))
		{
			return false;
		}
		try
		{
			return await JoinSteamPartyLobbyFromId((SteamId)ulong.Parse(steamCommandLine));
		}
		catch (Exception e)
		{
			Logger.instance.Warning($"No valid Steam lobby ID found in command line: \"{steamCommandLine}\" Exception: {e.Message}");
			return false;
		}
	}
	private void SubscribeToSteamEvents()
	{
		if (eventsSubscribed)
		{
			return;
		}

		SteamMatchmaking.OnLobbyMemberJoined += OnLobbyMemberJoined;
		SteamMatchmaking.OnLobbyMemberLeave += OnLobbyMemberLeave;
        SteamMatchmaking.OnLobbyMemberKicked += OnLobbyMemberKicked;
        SteamMatchmaking.OnLobbyDataChanged += OnLobbyDataChanged;
		SteamMatchmaking.OnLobbyMemberDataChanged += OnLobbyMemberDataChanged;
        SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
        SteamMatchmaking.OnChatMessage += OnChatMessage;

        SteamFriends.OnGameRichPresenceJoinRequested += OnGameRichPresenceJoinRequested;
		SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequested;
        

		eventsSubscribed = true;
		Logger.instance.Log("Steam events subscribed", 10);
	}
	private void UnsubscribeFromSteamEvents()
	{
		if (!eventsSubscribed)
		{
			return;
		}

		SteamMatchmaking.OnLobbyMemberJoined -= OnLobbyMemberJoined;
		SteamMatchmaking.OnLobbyMemberLeave -= OnLobbyMemberLeave;
		SteamMatchmaking.OnLobbyMemberKicked -= OnLobbyMemberKicked;
        SteamMatchmaking.OnLobbyDataChanged -= OnLobbyDataChanged;
        SteamMatchmaking.OnLobbyMemberDataChanged -= OnLobbyMemberDataChanged;
        SteamMatchmaking.OnLobbyEntered -= OnLobbyEntered;
		SteamMatchmaking.OnChatMessage -= OnChatMessage;

        SteamFriends.OnGameRichPresenceJoinRequested -= OnGameRichPresenceJoinRequested;
        SteamFriends.OnGameLobbyJoinRequested -= OnGameLobbyJoinRequested;
        

        eventsSubscribed = false;
		Logger.instance.Log("Steam events unsubscribed", 10);
	}
	public void StartServer()
	{
		Logger.instance.Log($"Starting server connection serverState: {serverState}", 100);
        if (networkManager == null)
		{
			Logger.instance.Error("NetworkInterface StartServer no networkManager");
			return;
		}

		if (serverState == LocalConnectionState.Stopped)
		{
			networkManager.ServerManager.StartConnection();
		}
		else
		{
			// networkManager.ServerManager.StopConnection(true);
		}
	}
	public void StartClient(string clientAddress)
	{
        Logger.instance.Log($"Starting client connection to address: {clientAddress} clientState: {clientState}", 100);
        if (networkManager == null)
		{
			Logger.instance.Error("NetworkInterface StartClient no networkManager");
			return;
		}
		if (clientState == LocalConnectionState.Stopped)
		{
            fishyFacepunch.SetClientAddress(clientAddress);
			networkManager.ClientManager.StartConnection();
		}
		else
		{
			// networkManager.ClientManager.StopConnection();
		}
	}

    private void OnServerConnectionStateChanged(ServerConnectionStateArgs scsa)
	{
		serverState = scsa.ConnectionState;
		Logger.instance.Log($"OnServerConnectionStateChanged {serverState}", 100);
		if (serverState == LocalConnectionState.Started)
		{
			/*if(!partyLobby.HasValue || !partyLobby.Value.Id.IsValid)
			{
				Logger.instance.Error("Party lobby is not valid when trying to start client after server start");
                return;
			}
			if (partyLobby.Value.Owner.Id != SteamClient.SteamId)
			{
				return;
			}*/
			if (!matchmakingLobby.HasValue || !matchmakingLobby.Value.Id.IsValid)
			{
				// Logger.instance.Error("Matchmaking lobby is not valid when trying to start client after server start");
				if(partyLobby.HasValue && partyLobby.Value.Id.IsValid && partyLobby.Value.Owner.Id == SteamClient.SteamId && string.IsNullOrEmpty(partyLobby.Value.GetData(LobbyKeys.HostSteamId)))
				{
					Logger.instance.Log("No matchmaking lobby, assuming all players were in the same party lobby");
					StartClient(SteamClient.SteamId.ToString());
                    partyLobby.Value.SetData(LobbyKeys.LobbyState, LobbyState.GameStarting.ToString());
					partyLobby.Value.SetData(LobbyKeys.HostSteamId, SteamClient.SteamId.ToString());
                }
				return;
            }
			if (!matchmakingLobby.Value.IsOwnedBy(SteamClient.SteamId))
			{
				return;
			}
            Logger.instance.Log($"Game is ready to start! clientState: {clientState}");
            StartClient(SteamClient.SteamId.ToString());
            // Logger.instance.Log($"StartClient called! clientState: {clientState}");

            // this will change to matchmaking lobby when implemented
			matchmakingLobby.Value.SetData(LobbyKeys.HostSteamId, SteamClient.SteamId.ToString());
            partyLobby.Value.SetData(LobbyKeys.LobbyState, LobbyState.GameStarting.ToString());
		}
    }
	private void OnClientConnectionStateChanged(ClientConnectionStateArgs ccsa)
	{
		clientState = ccsa.ConnectionState;
		Logger.instance.Log($"OnClientConnectionStateChanged {clientState}", 100);
	}
	private void OnLobbyMemberJoined(Lobby lobby, Friend friend)
	{
		if (partyLobby.HasValue && partyLobby.Value.Id == lobby.Id)
		{
            Logger.instance.Log($"Player {friend.Name} joined the party lobby! Lobby ID: {lobby.Id}, Member count: {lobby.MemberCount}", 60);
            LobbyUI.instance.LobbyMemberJoined(friend, lobby);
            cachedPartyLobbyData.OnLobbyMemberJoined(friend, lobby);
            LobbyUI.instance.LobbyUpdated(lobby);
			return;
        }
        else if (matchmakingLobby.HasValue && matchmakingLobby.Value.Id == lobby.Id)
        {
            Logger.instance.Log($"Player {friend.Name} joined the matchmaking lobby! Lobby ID: {lobby.Id}, Member count: {lobby.MemberCount}", 60);
            if (matchmakingLobby.Value.Owner.Id == SteamClient.SteamId && matchmakingLobby.Value.MemberCount == matchmakingLobby.Value.MaxMembers)
			{
                // matchmakingLobby.Value.SetData(LobbyKeys.HostSteamId, SteamClient.SteamId.ToString());
                StartServer();
            }
            return;
        }
        Logger.instance.Warning($"OnLobbyMemberJoined for a lobby that is not the party or matchmaking lobby. partyLobby.Value.Id: {partyLobby.Value.Id}, matchmakingLobby: {matchmakingLobby.Value.Id}, changed LobbyId: {lobby.Id}");
        
    }
	private void OnLobbyMemberLeave(Lobby lobby, Friend friend)
	{
        if (partyLobby.HasValue && partyLobby.Value.Id == lobby.Id)
        {
            Logger.instance.Log($"Player {friend.Name} left the party lobby!, Member count: {lobby.MemberCount}", 60);
            LobbyUI.instance.LobbyMemberLeft(friend);
            cachedPartyLobbyData.OnLobbyMemberLeave(lobby, friend);
			LobbyUI.instance.SetReadyState(false);
            LobbyUI.instance.LobbyUpdated(lobby);
            return;
        }
        else if (matchmakingLobby.HasValue && matchmakingLobby.Value.Id == lobby.Id)
        {
            Logger.instance.Log($"Player {friend.Name} left the matchmaking lobby!, Member count: {lobby.MemberCount}", 60);
            return;
        }
        Logger.instance.Warning($"OnLobbyMemberLeave for a lobby that is not the party or matchmaking lobby. currentLobby.Value.Id: {partyLobby.Value.Id}, matchmakingLobby: {matchmakingLobby.Value.Id}, changed LobbyId: {lobby.Id}");

    }
	private void OnLobbyMemberKicked(Lobby lobby, Friend kickedFriend, Friend friendWhoKicked)
	{
		Logger.instance.Log($"Player {kickedFriend.Name} was kicked from the lobby by {friendWhoKicked.Name}! Lobby ID: {lobby.Id}, Member count: {lobby.MemberCount}");
        // only do this if it doesn't also trigger onlobbymemberleave
        OnLobbyMemberLeave(lobby, kickedFriend);
    }
	private void OnLobbyDataChanged(Lobby lobby)
	{
		Logger.instance.Log($"OnLobbyDataChanged {lobby.Id}", 5);
		if(partyLobby.HasValue && partyLobby.Value.Id == lobby.Id)
		{
            Logger.instance.Log("partyLobby", 5);
            LobbyChanges lobbyChanges = cachedPartyLobbyData.GetLobbyChanges(lobby);
            if (!lobbyChanges.Any)
            {
                Logger.instance.Log("no changes", 5);
                return;
            }
            Logger.instance.Log($"LobbyOwnerChanged: {lobbyChanges.ownerChange.LobbyOwnerChanged} LobbyTypeChanged: {lobbyChanges.typeChange.LobbyTypeChanged} MatchmakingStateChanged: {lobbyChanges.matchmakingChange.MatchmakingStateChanged} LobbyStateChanged: {lobbyChanges.stateChange.LobbyStateChanged} HostIdChanged: {lobbyChanges.hostIdChange.HostIdChanged}", 5);
            if (lobbyChanges.ownerChange.LobbyOwnerChanged)
            {
                Logger.instance.Log($"Lobby owner changed from {lobbyChanges.ownerChange.OldOwner} to {lobbyChanges.ownerChange.NewOwner}", 10);
            }
            if (lobbyChanges.typeChange.LobbyTypeChanged)
            {
                Logger.instance.Log($"Lobby type changed from {lobbyChanges.typeChange.OldType} to {lobbyChanges.typeChange.NewType}", 10);
                SetFriendsCanJoin();
            }
			if (lobbyChanges.matchmakingChange.MatchmakingStateChanged)
			{
				Logger.instance.Log($"Matchmaking state changed from {lobbyChanges.matchmakingChange.OldState} to {lobbyChanges.matchmakingChange.NewState}", 10);
                if (string.IsNullOrEmpty(lobbyChanges.matchmakingChange.NewState))
				{
					LeaveCurrentMatchmakingLobby();
				}
				else
				{
					_ = JoinSteamMatchmakingLobbyFromId((SteamId)ulong.Parse(lobbyChanges.matchmakingChange.NewState));
                }
			}
            if (lobbyChanges.stateChange.LobbyStateChanged)
            {
                Logger.instance.Log($"Lobby state changed from {lobbyChanges.stateChange.OldState} to {lobbyChanges.stateChange.NewState}", 10);
                SetFriendsCanJoin();
                /*if (lobbyChanges.stateChange.NewState == LobbyState.Join)
                {
                    string lobbyStateString = lobby.GetData(LobbyKeys.LobbyState);
                    string rawId = lobbyStateString[LobbyState.Join.ToString().Length..];
                    if (ulong.TryParse(rawId, out ulong targetId))
                    {
                        _ = JoinSteamPartyLobbyFromId(targetId);
                        return;
                    }
                    Logger.instance.Error($"Lobby state set to join but lobby state format was incorrect. rawId: {rawId} targetId: {targetId}");
                }
                else if (lobbyChanges.stateChange.NewState == LobbyState.GameStarting)
                {
                    StartClient(lobby.GetData(LobbyKeys.HostSteamId));
                }*/
            }
            if (lobbyChanges.hostIdChange.HostIdChanged)
			{
				Logger.instance.Log($"Host SteamID changed from {lobbyChanges.hostIdChange.OldHostId} to {lobbyChanges.hostIdChange.NewHostId}", 10);
                if (partyLobby.Value.Owner.Id != SteamClient.SteamId && clientState == LocalConnectionState.Stopped)
				{
					string hostSteamId = partyLobby.Value.GetData(LobbyKeys.HostSteamId);
					if (!string.IsNullOrEmpty(hostSteamId))
					{
						StartClient(hostSteamId);
					}
				}
			}
            LobbyUI.instance.LobbyUpdated(lobby);
			return;
        }
        else if (matchmakingLobby.HasValue && matchmakingLobby.Value.Id == lobby.Id)
		{
            Logger.instance.Log("matchmakingLobby", 5);
            if (matchmakingLobby.Value.Owner.Id != SteamClient.SteamId && clientState == LocalConnectionState.Stopped)
			{
				string hostSteamId = matchmakingLobby.Value.GetData(LobbyKeys.HostSteamId);
				if (!string.IsNullOrEmpty(hostSteamId))
				{ 
					StartClient(hostSteamId);
                }
			}
			return;
		}
        Logger.instance.Warning($"OnLobbyDataChanged for a lobby that is not the party or matchmaking lobby. partyLobby.Value.Id: {partyLobby.Value.Id}, matchmakingLobby: {matchmakingLobby.Value.Id}, changed LobbyId: {lobby.Id}");
    }

	private void OnLobbyMemberDataChanged(Lobby lobby, Friend friend)
	{
        if (partyLobby.HasValue && partyLobby.Value.Id == lobby.Id)
        {
            ReadyStateChange readyStateChange = cachedPartyLobbyData.GetReadyStateChange(lobby, friend);
            Logger.instance.Log($"OnLobbyMemberDataChanged friend: {friend.Name}, readyChanged: {readyStateChange.ReadyStateChanged} OldState: {readyStateChange.OldState} NewState: {readyStateChange.NewState}", 5);
            if (readyStateChange.ReadyStateChanged)
            {
                CheckForAllPlayersReady();
                LobbyUI.instance.UpdateLobbyMemberReadyStatus(friend, readyStateChange.NewState == ReadyState.Ready);
            }
            LobbyUI.instance.LobbyUpdated(lobby);
            return;
        }
		if (matchmakingLobby.HasValue && matchmakingLobby.Value.Id == lobby.Id)
		{
			return;
		}
        Logger.instance.Warning($"OnLobbyMemberDataChanged for a lobby that is not the party or matchmaking lobby. partyLobby: {partyLobby.Value.Id}, matchmakingLobby: {matchmakingLobby.Value.Id}, changed LobbyId: {lobby.Id}");

    }
	private void OnChatMessage(Lobby lobby, Friend friend, string message)
	{
        
	}
    private async void OnGameRichPresenceJoinRequested(Friend friend, string data)
	{
		Logger.instance.Log($"OnGameRichPresenceJoinRequested friend: {friend.Name}, data: {data}", 50);
		try
		{
			await JoinSteamPartyLobbyFromId((SteamId)ulong.Parse(data));
		}
		catch (Exception e)
		{
			Logger.instance.Warning($"Failed to join lobby from rich presence data: \"{data}\" exception: {e.Message}");
		}
	}

	private async void OnGameLobbyJoinRequested(Lobby lobby, SteamId steamId)
	{
		Logger.instance.Log($"OnGameLobbyJoinRequested steamId: {steamId}, lobby: {lobby.Id}", 50);
		try
		{
			await JoinSteamPartyLobby(lobby);
		}
		catch (Exception e)
		{
			Logger.instance.Warning($"Failed to join lobby from OnGameLobbyJoinRequested: \"{lobby.Id}\" exception: {e.Message}");
		}
	}
	private void OnLobbyEntered(Lobby lobby)
	{
        // might have to rethink how this works with party vs matchmaking lobbies
        Logger.instance.Log($"OnLobbyEntered lobby: {lobby.Id}", 100);
        cachedPartyLobbyData.SetupFromNewLobby(lobby);
		SetFriendsCanJoin();
        // LobbyUI.instance.LobbyUpdated(lobby);
        LobbyUI.instance.JoinLobby(lobby);
    }
    public async Task StartSteamPartyLobby()
	{
		LeaveCurrentPartyLobby();
		Lobby? result = await SteamMatchmaking.CreateLobbyAsync(lobbySize);
		if (result != null)
		{
			partyLobby = result.Value;
			if (DefaultLobbiesToInvisible)
			{
                partyLobby.Value.SetInvisible();
                partyLobby.Value.SetData(LobbyKeys.LobbyType, LobbyType.Invisible.ToString());
            }
			else
			{
                partyLobby.Value.SetFriendsOnly();
                partyLobby.Value.SetData(LobbyKeys.LobbyType, LobbyType.FriendsOnly.ToString());
            }
            //partyLobby.Value.SetData(LobbyKeys.HostSteamId, SteamClient.SteamId.ToString());
            partyLobby.Value.SetData(LobbyKeys.LobbyState, LobbyState.WaitingForReady.ToString());
            SetFriendsCanJoin();
			// Logger.instance.Log($"Created a new party lobby with Id: {partyLobby.Value.Id} and HostSteamID: {partyLobby.Value.GetData(LobbyKeys.HostSteamId)}");
			Logger.instance.Log($"Created a new party lobby with Id: {partyLobby.Value.Id}", 100);
		}
		else
		{
			Logger.instance.Error($"Failed to create lobby! result: {result}");
		}
	}
	public async Task StartSteamMatchmaking()
	{
		Logger.instance.Log("StartSteamMatchmaking", 10);
        LobbyQuery query = new();
        query.WithSlotsAvailable(partyLobby.Value.MemberCount).WithKeyValue(LobbyKeys.LobbyState, LobbyState.Matchmaking.ToString());
        Lobby[] lobbies = await query.RequestAsync();
        if (lobbies == null || lobbies.Length == 0)
        {
            Lobby? result = await SteamMatchmaking.CreateLobbyAsync(lobbySize);
            if (result != null && partyLobby.HasValue && partyLobby.Value.Id.IsValid)
            {
                matchmakingLobby = result.Value;
                // matchmakingLobby.Value.SetInvisible(); // defaults to invisible?
                matchmakingLobby.Value.SetData(LobbyKeys.LobbyState, LobbyState.Matchmaking.ToString());
                partyLobby.Value.SetData(LobbyKeys.MatchmakingLobby, matchmakingLobby.Value.Id.ToString());
                Logger.instance.Log($"Created a new matchmaking lobby with Id: {matchmakingLobby.Value.Id}", 100);
            }
            else
            {
                Logger.instance.Error($"Failed to create matchmaking lobby! result: {result}");
            }
            return;
        }
        Lobby targetLobby = lobbies[0];
        matchmakingLobby = targetLobby;
        SteamId matchmakingLobbyId = targetLobby.Id;
        partyLobby.Value.SetData(LobbyKeys.MatchmakingLobby, matchmakingLobbyId.ToString());
        Logger.instance.Log($"Joining matchmaking lobby: {matchmakingLobbyId} with {targetLobby.MemberCount}/{targetLobby.MaxMembers} players", 100);
        await JoinSteamPartyLobby(targetLobby);
        
	}

	public async Task<bool> JoinSteamPartyLobby(Lobby lobby)
	{
		Logger.instance.Log($"Joining lobby: {lobby.Id}", 100);
		LeaveCurrentPartyLobby();
		try
		{
			partyLobby = lobby;
			await partyLobby.Value.Join();
			SetPlayerReady(LobbyUI.instance.GetPlayerReady());
			Logger.instance.Log($"Joined party lobby: {partyLobby.Value.Id}", 100);
			return true;
		}
		catch (Exception e)
		{
			Logger.instance.Error($"Failed to join party lobby: {lobby.Id} Exception: {e.Message}");
			return false;
		}
	}

	public async Task<bool> JoinSteamPartyLobbyFromFriend(Friend friend, bool retry = false)
	{
		Logger.instance.Log($"Joining party lobby from friend: {friend.Name}", 100);
		try
		{
			Lobby? lobby = GetPartyLobbyFromFriend(friend);
			if (!lobby.HasValue)
			{
				if (retry)
				{
					Logger.instance.Log($"Retrying to get party lobby from friend: {friend.Name}", 100);
					int retries = 0;
					while (retries < 10 && !lobby.HasValue)
					{
						await Task.Delay(500);
						lobby = GetPartyLobbyFromFriend(friend);
						retries++;
					}
					if (!lobby.HasValue)
					{
						Logger.instance.Error($"Failed to get party lobby from friend on retry: {friend.Name}");
						return false;
					}
				}
				else
				{
					Logger.instance.Error($"Failed to get party lobby from friend: {friend.Name}");
					return false;
                }
                return false;
			}
			if (!LobbyCanBeJoinedByFriends(lobby.Value))
			{
				return false;
			}
			return await JoinSteamPartyLobby(lobby.Value);
		}
		catch (Exception e)
		{
			Logger.instance.Error($"Failed to join party lobby from friend: {friend.Name} Exception: {e.Message}");
			return false;
		}
	}
	public async Task<bool> JoinSteamPartyLobbyFromId(SteamId lobbyId)
	{
		Logger.instance.Log($"Joining party lobby from Id: {lobbyId}", 100);
		LeaveCurrentPartyLobby();
		try
		{
			partyLobby = await SteamMatchmaking.JoinLobbyAsync(lobbyId);
			// SetFriendsCanJoin();
			SetPlayerReady(LobbyUI.instance.GetPlayerReady());
			// LobbyUI.instance.JoinLobby(currentLobby);
			Logger.instance.Log($"Joined party lobby: {partyLobby.Value.Id}", 100);
			return true;
		}
		catch (Exception e)
		{
			Logger.instance.Error($"Failed to join party lobby from Id: {lobbyId} Exception: {e.Message}");
			return false;
		}
	}
	public async Task<bool> JoinSteamMatchmakingLobbyFromId(SteamId lobbyId)
	{
		Logger.instance.Log($"Joining matchmaking lobby from Id: {lobbyId}", 100);
		LeaveCurrentMatchmakingLobby();
		try
		{
			matchmakingLobby = await SteamMatchmaking.JoinLobbyAsync(lobbyId);
			Logger.instance.Log($"Joined matchmaking lobby: {matchmakingLobby.Value.Id}", 100);
			return true;
		}
		catch (Exception e)
		{
			Logger.instance.Error($"Failed to join matchmaking lobby from Id: {lobbyId} Exception: {e.Message}");
			return false;
        }
	}

    public void LeaveCurrentPartyLobby()
	{
        Logger.instance.Log("LeaveCurrentPartyLobby", 10);
        cachedPartyLobbyData.Reset();
        if (partyLobby.HasValue && partyLobby.Value.Id.IsValid)
		{
            partyLobby.Value.Leave();
            partyLobby = null;
			LobbyUI.instance.LeaveCurrentPartyLobby();
		}
	}
	public void LeaveCurrentMatchmakingLobby()
	{
        Logger.instance.Log("LeaveCurrentMatchmakingLobby", 10);
		if (partyLobby.HasValue && partyLobby.Value.Id.IsValid && partyLobby.Value.Owner.Id == SteamClient.SteamId)
		{ 
			// tell other party members we're leaving matchmaking
			partyLobby.Value.SetData(LobbyKeys.MatchmakingLobby, null);
        }
        if (matchmakingLobby.HasValue && matchmakingLobby.Value.Id.IsValid)
		{ 
			matchmakingLobby.Value.Leave();
			matchmakingLobby = null;
        }
    }
	private void SetRichPresence(string key, string val)
	{
		if (SteamFriends.GetRichPresence(key) != val)
		{
			SteamFriends.SetRichPresence(key, val);
			Logger.instance.Log($"SetRichPresence {key}: {val}", 20);
		}
	}

	public AppId GetAppId()
	{
		return (AppId)appId;
	}

	public async void SetFriendsCanJoin()
	{
		if (!partyLobby.HasValue || !partyLobby.Value.Id.IsValid)
		{
			await StartSteamPartyLobby();
			if (!partyLobby.HasValue || !partyLobby.Value.Id.IsValid)
			{
				return;
			}
		}
		if (!CurrentPartyLobbyCanBeJoinedByFriends())
		{
			SetRichPresence("connect", null);
            partyLobby.Value.SetInvisible();
            partyLobby.Value.SetData(LobbyKeys.LobbyType, LobbyType.Invisible.ToString());
            // currentLobby.Value.SetJoinable(false);
            // currentLobby.Value.SetData(LobbyKeys.JoinableState, JoinableState.NotJoinable.ToString());
            if (partyLobby.Value.GetData(LobbyKeys.LobbyState) == LobbyState.SearchingForGame.ToString())
			{
				SetRichPresence("gamestatus", "WaitingForMatch");
			}
			else
			{
                SetRichPresence("gamestatus", "AtMainMenu");
            }
			SetRichPresence("steam_display", "#StatusWithoutHealth");
			return;
		}
		SetRichPresence("connect", $"{partyLobby.Value.Id}");
        partyLobby.Value.SetFriendsOnly();
        partyLobby.Value.SetData(LobbyKeys.LobbyType, LobbyType.FriendsOnly.ToString());
        // currentLobby.Value.SetJoinable(true);
        // currentLobby.Value.SetData(LobbyKeys.JoinableState, JoinableState.Joinable.ToString());
        SetRichPresence("gamestatus", "Joinable");
        SetRichPresence("steam_display", "#StatusWithoutHealth");
    }

	public void SetPartyLobbyMemberData(string key, string val)
	{
		if (partyLobby.HasValue && partyLobby.Value.Id.IsValid)
		{
            partyLobby.Value.SetMemberData(key, val);
			Logger.instance.Log($"SetMemberData {key}: {val}", 20);
		}
	}

	public void SetPlayerReady(bool ready)
	{
		if (partyLobby.HasValue && partyLobby.Value.Id.IsValid)
		{
			SetPartyLobbyMemberData(LobbyKeys.Ready, ready ? ReadyState.Ready.ToString() : ReadyState.NotReady.ToString());
		}
	}

	public void CheckForAllPlayersReady()
	{
		if (!partyLobby.HasValue || !partyLobby.Value.IsOwnedBy(SteamClient.SteamId))
		{
			return;
		}
        if (partyLobby.Value.GetData(LobbyKeys.LobbyState) == LobbyState.GameStarting.ToString())
		{
			return;
		}
        Logger.instance.Log("CheckForAllPlayersReady", 10);
		Friend[] friendsInLobby = partyLobby.Value.Members.ToArray();
		for (int i = 0; i < friendsInLobby.Length; i++)
		{
            string readyStatus = partyLobby.Value.GetMemberData(friendsInLobby[i], LobbyKeys.Ready);
			if (!Enum.TryParse(readyStatus, out ReadyState readyState) || readyState != ReadyState.Ready)
			{
                if (partyLobby.Value.GetData(LobbyKeys.LobbyState) == LobbyState.SearchingForGame.ToString())
                {
                    Logger.instance.Log("Stopping searching, someone unreadied", 20);
					LeaveCurrentMatchmakingLobby();
                    partyLobby.Value.SetFriendsOnly();
					partyLobby.Value.SetData(LobbyKeys.MatchmakingLobby, null);
                    partyLobby.Value.SetData(LobbyKeys.LobbyType, LobbyType.FriendsOnly.ToString());
                    partyLobby.Value.SetData(LobbyKeys.LobbyState, LobbyState.WaitingForReady.ToString());
                }
                return;
			}
        }
        Logger.instance.Log("all players ready", 20);
        // All players are ready, start searching for a game if there's
        // less than lobbySize players, else start the game
        if (partyLobby.Value.MemberCount < lobbySize && partyLobby.Value.GetData(LobbyKeys.LobbyState) != LobbyState.SearchingForGame.ToString())
		{
            Logger.instance.Log("Starting searching for game", 30);
            partyLobby.Value.SetData(LobbyKeys.LobbyState, LobbyState.SearchingForGame.ToString());
			_ = StartSteamMatchmaking();
        }
		else if (partyLobby.Value.MemberCount == partyLobby.Value.MaxMembers)
		{
            StartServer();
			/*StartClient(SteamClient.SteamId.ToString());
			Logger.instance.Log($"Game is ready to start! clientState = {clientState}");
			currentLobby.Value.SetData(LobbyKeys.LobbyState, LobbyState.GameStarting.ToString());*/
		}
	}

	public bool LobbyCanBeJoinedByFriends(Lobby lobby)
	{
		string lobbyStateString = lobby.GetData(LobbyKeys.LobbyState);
        if (!Enum.TryParse(lobbyStateString, out LobbyState lobbyState))
		{
			return false;
		}
        if (lobbyState == LobbyState.SearchingForGame || lobbyState == LobbyState.GameStarting)
		{
			return false;
		}
        if (lobby.MemberCount >= lobby.MaxMembers)
		{
			return false;
		}
		string lobbyTypeString = lobby.GetData(LobbyKeys.LobbyType);
        if (!Enum.TryParse(lobbyTypeString, out LobbyType lobbyType))
		{
			return false;
		}
        if (lobbyType == LobbyType.Private || lobbyType == LobbyType.Invisible)
		{
			return false;
		}
        /*if (!Enum.TryParse(lobby.GetData(LobbyKeys.JoinableState), out JoinableState joinableState))
		{
			return false;
		}
        if (joinableState != JoinableState.Joinable)
		{
			return false;
        }*/
        return true;
	}


    public bool CurrentPartyLobbyCanBeJoinedByFriends()
	{
		if (!partyLobby.HasValue || !partyLobby.Value.Id.IsValid)
		{
			return false;
		}
		return LobbyCanBeJoinedByFriends(partyLobby.Value);
	}

	public Lobby? GetPartyLobbyFromFriend(Friend friend)
	{
		if (!friend.IsPlayingThisGame)
		{
			return null;
		}
		Friend.FriendGameInfo? gameInfo = friend.GameInfo;
		if (!gameInfo.HasValue || !gameInfo.Value.Lobby.HasValue)
		{
			return null;
		}
		return gameInfo.Value.Lobby;
	}

/*	private async Task SearchForSteamLobbyToJoin()
	{
        LobbyQuery query = new();
		query.WithSlotsAvailable(currentLobby.Value.MemberCount).WithKeyValue(LobbyKeys.LobbyState, LobbyState.SearchingForGame.ToString());
        Lobby[] lobbies = await query.RequestAsync();
		if (lobbies == null || lobbies.Length == 0)
		{
			Logger.instance.Log("No available lobbies found to join");
			return;
        }
        Lobby targetLobby = lobbies[0];
		SteamId targetLobbyId = targetLobby.Id;
        currentLobby.Value.SetData(LobbyKeys.LobbyState, $"{LobbyState.Join}{targetLobbyId}");
        Logger.instance.Log($"Joining lobby: {targetLobbyId} with {targetLobby.MemberCount}/{targetLobby.MaxMembers} players");
        await JoinSteamPartyLobby(targetLobby);
    }*/

	public string GetPartyLobbyIdStringForConnection()
	{
		if (!partyLobby.HasValue || !partyLobby.Value.Id.IsValid)
		{
			return null;
		}
		if (!CurrentPartyLobbyCanBeJoinedByFriends())
		{
			return null;
		}
        return partyLobby.Value.Id.ToString();
    }
	public async Task<Lobby?> GetLobbyFromFriendIfJoinable(Friend friend)
	{
		if (!friend.IsPlayingThisGame)
		{
			return null;
		}
        Friend.FriendGameInfo? gameInfo = friend.GameInfo;
		if (!gameInfo.HasValue || !gameInfo.Value.Lobby.HasValue || !gameInfo.Value.Lobby.Value.Id.IsValid)
		{
			return null;
		}
		Lobby lobby = gameInfo.Value.Lobby.Value;
		await lobby.RefreshAsync();
        if (!LobbyCanBeJoinedByFriends(lobby))
		{ 
			return null;
        }
        return lobby;
    }

	public string GetMatchmakingLobbyIdString()
	{
		if (!matchmakingLobby.HasValue || !matchmakingLobby.Value.Id.IsValid)
		{
			return "null";
		}
		return matchmakingLobby.Value.Id.ToString();
    }
}