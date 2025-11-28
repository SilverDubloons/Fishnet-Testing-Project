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
	private LocalConnectionState serverState = LocalConnectionState.Stopped;
	private LocalConnectionState clientState = LocalConnectionState.Stopped;
	private Lobby? partyLobby = null;
	private Lobby? matchmakingLobby = null;
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
		Logger.instance.Log($"JoinLobbyFromCommandLine lobbyIdStr: {lobbyIdStr}");
		if (string.IsNullOrEmpty(lobbyIdStr))
		{
			return false;
		}
		return await JoinSteamPartyLobbyFromId((SteamId)ulong.Parse(lobbyIdStr));
	}
	private async Task<bool> JoinLobbyFromSteamCommandLine()
	{
		string steamCommandLine = SteamApps.CommandLine;
		Logger.instance.Log($"JoinLobbyFromSteamCommandLine steamCommandLine: \"{steamCommandLine}\"");
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
		Logger.instance.Log("Steam events subscribed");
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
		Logger.instance.Log("Steam events unsubscribed");
	}
	public void StartServer()
	{
		Logger.instance.Log($"Starting server connection serverState: {serverState}");
        if (networkManager == null)
		{
			Logger.instance.Error("StartServer no networkManager");
			return;
		}
		if (serverState == LocalConnectionState.Stopped)
		{
			networkManager.ServerManager.StartConnection();
		}
		else
		{
            Logger.instance.Error($"StartServer but serverState: {serverState}");
        }
	}
	public void StartClient(string clientAddress)
	{
        Logger.instance.Log($"Starting client connection to address: {clientAddress} clientState: {clientState}");
        if (networkManager == null)
		{
			Logger.instance.Error("StartClient no networkManager");
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
		Logger.instance.Log($"OnServerConnectionStateChanged {serverState}");
		if (serverState == LocalConnectionState.Started)
		{
			if (!matchmakingLobby.HasValue || !matchmakingLobby.Value.Id.IsValid)
			{
				if(partyLobby.HasValue && partyLobby.Value.Id.IsValid && partyLobby.Value.Owner.Id == SteamClient.SteamId && string.IsNullOrEmpty(partyLobby.Value.GetData(LobbyKeys.HostSteamId)) && clientState == LocalConnectionState.Stopped)
				{
					Logger.instance.Log("No matchmaking lobby, assuming all players were in the same party lobby");
					StartClient(SteamClient.SteamId.ToString());
                    partyLobby.Value.SetData(LobbyKeys.LobbyState, LobbyState.GameStarting.ToString());
					partyLobby.Value.SetData(LobbyKeys.HostSteamId, SteamClient.SteamId.ToString());
                }
				return;
            }
			if (!matchmakingLobby.Value.IsOwnedBy(SteamClient.SteamId) || clientState != LocalConnectionState.Stopped)
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
		Logger.instance.Log($"OnClientConnectionStateChanged {clientState}");
	}
	private void OnLobbyMemberJoined(Lobby lobby, Friend friend)
	{
		if (partyLobby.HasValue && partyLobby.Value.Id == lobby.Id)
		{
            Logger.instance.Log($"Player {friend.Name} joined the party lobby! Lobby ID: {lobby.Id}, Member count: {lobby.MemberCount}", 20, true);
            LobbyUI.instance.LobbyMemberJoined(friend, lobby);
            cachedPartyLobbyData.OnLobbyMemberJoined(friend, lobby);
            LobbyUI.instance.LobbyUpdated(lobby);
			return;
        }
        else if (matchmakingLobby.HasValue && matchmakingLobby.Value.Id == lobby.Id)
        {
            Logger.instance.Log($"Player {friend.Name} joined the matchmaking lobby! Lobby ID: {lobby.Id}, Member count: {lobby.MemberCount}", 20, true);
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
            Logger.instance.Log($"Player {friend.Name} left the party lobby!, Member count: {lobby.MemberCount}");
            LobbyUI.instance.LobbyMemberLeft(friend);
            cachedPartyLobbyData.OnLobbyMemberLeave(lobby, friend);
			LobbyUI.instance.SetReadyState(false);
            LobbyUI.instance.LobbyUpdated(lobby);
            return;
        }
        else if (matchmakingLobby.HasValue && matchmakingLobby.Value.Id == lobby.Id)
        {
            Logger.instance.Log($"Player {friend.Name} left the matchmaking lobby!, Member count: {lobby.MemberCount}");
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
		Logger.instance.Log($"OnLobbyDataChanged {lobby.Id}, 10");
		if(partyLobby.HasValue && partyLobby.Value.Id == lobby.Id)
		{
            Logger.instance.Log("partyLobby", 10);
            LobbyChanges lobbyChanges = cachedPartyLobbyData.GetLobbyChanges(lobby);
            if (!lobbyChanges.Any)
            {
                Logger.instance.Log("no changes", 10);
                return;
            }
            Logger.instance.Log($"LobbyOwnerChanged: {lobbyChanges.ownerChange.LobbyOwnerChanged} LobbyTypeChanged: {lobbyChanges.typeChange.LobbyTypeChanged} MatchmakingStateChanged: {lobbyChanges.matchmakingChange.MatchmakingStateChanged} LobbyStateChanged: {lobbyChanges.stateChange.LobbyStateChanged} HostIdChanged: {lobbyChanges.hostIdChange.HostIdChanged}", 5);
            if (lobbyChanges.ownerChange.LobbyOwnerChanged)
            {
                Logger.instance.Log($"Lobby owner changed from {lobbyChanges.ownerChange.OldOwner} to {lobbyChanges.ownerChange.NewOwner}", 30);
            }
            if (lobbyChanges.typeChange.LobbyTypeChanged)
            {
                Logger.instance.Log($"Lobby type changed from {lobbyChanges.typeChange.OldType} to {lobbyChanges.typeChange.NewType}", 20);
                SetFriendsCanJoin();
            }
			if (lobbyChanges.matchmakingChange.MatchmakingStateChanged)
			{
                Logger.instance.Log($"Matchmaking state changed from {lobbyChanges.matchmakingChange.OldState} to {lobbyChanges.matchmakingChange.NewState}", 100, true);
                if (partyLobby.Value.Owner.Id == SteamClient.SteamId)
				{
					Logger.instance.Log("Since we're the party lobby owner, ignoring matchmaking state change", 20, true);
				}
				else
				{
					if (string.IsNullOrEmpty(lobbyChanges.matchmakingChange.NewState))
					{
						LeaveCurrentMatchmakingLobby();
					}
					else
					{
						_ = JoinSteamMatchmakingLobbyFromId((SteamId)ulong.Parse(lobbyChanges.matchmakingChange.NewState));
					}
				}
			}
            if (lobbyChanges.stateChange.LobbyStateChanged)
            {
                Logger.instance.Log($"Lobby state changed from {lobbyChanges.stateChange.OldState} to {lobbyChanges.stateChange.NewState}", 20);
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
				Logger.instance.Log($"Host SteamID changed from {lobbyChanges.hostIdChange.OldHostId} to {lobbyChanges.hostIdChange.NewHostId}", 20);
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
            Logger.instance.Log("matchmakingLobby", 20);
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
            Logger.instance.Log($"OnLobbyMemberDataChanged friend: {friend.Name}, readyChanged: {readyStateChange.ReadyStateChanged} OldState: {readyStateChange.OldState} NewState: {readyStateChange.NewState}");
            if (readyStateChange.ReadyStateChanged)
            {
                LobbyUI.instance.UpdateLobbyMemberReadyStatus(friend, readyStateChange.NewState == ReadyState.Ready);
                CheckForAllPlayersReady();
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
		Logger.instance.Log($"OnGameRichPresenceJoinRequested friend: {friend.Name}, data: {data}");
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
		Logger.instance.Log($"OnGameLobbyJoinRequested steamId: {steamId}, lobby: {lobby.Id}");
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
		string lobbyStateString = lobby.GetData(LobbyKeys.LobbyState);
        Logger.instance.Log($"OnLobbyEntered lobby: {lobby.Id} state: {lobbyStateString}", 20, true );
		if (!Enum.TryParse(lobbyStateString, out LobbyState lobbyState) || lobbyState == LobbyState.Matchmaking)
		{
			return;
		}
	    cachedPartyLobbyData.SetupFromNewLobby(lobby);
		SetFriendsCanJoin();
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
			Logger.instance.Log($"Created a new party lobby with Id: {partyLobby.Value.Id}");
		}
		else
		{
			Logger.instance.Error($"Failed to create lobby! result: {result}");
		}
	}
	public async Task StartSteamMatchmaking()
	{
		if(matchmakingLobby.HasValue && matchmakingLobby.Value.Id.IsValid)
		{
			Logger.instance.Warning("StartSteamMatchmaking Already in a matchmaking lobby");
			return;
		}
		if (!partyLobby.HasValue || !partyLobby.Value.Id.IsValid)
		{
			Logger.instance.Error("StartSteamMatchmaking Cannot start matchmaking without a valid party lobby");
			return;
		}
        partyLobby.Value.SetData(LobbyKeys.LobbyState, LobbyState.SearchingForGame.ToString());
        LobbyQuery query = new();
        query.WithSlotsAvailable(partyLobby.Value.MemberCount).WithKeyValue(LobbyKeys.LobbyState, LobbyState.Matchmaking.ToString());
        Lobby[] lobbies = await query.RequestAsync();
        // Logger.instance.Log($"Found {lobbies.Length} lobbies", 20, true); // this fails but does not throw an error
        if (lobbies == null || lobbies.Length == 0)
        {
            Lobby? result = await SteamMatchmaking.CreateLobbyAsync(lobbySize);
            if (result != null && partyLobby.HasValue && partyLobby.Value.Id.IsValid)
            {
                matchmakingLobby = result.Value;
                // matchmakingLobby.Value.SetInvisible(); // defaults to invisible?
                matchmakingLobby.Value.SetData(LobbyKeys.LobbyState, LobbyState.Matchmaking.ToString());
                partyLobby.Value.SetData(LobbyKeys.MatchmakingLobby, matchmakingLobby.Value.Id.ToString());
                Logger.instance.Log($"Created a new matchmaking lobby with Id: {matchmakingLobby.Value.Id}", 30, true);
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
        Logger.instance.Log($"Joining matchmaking lobby: {matchmakingLobbyId} with {targetLobby.MemberCount}/{targetLobby.MaxMembers} players", 30, true);
        // await JoinSteamPartyLobby(targetLobby);
        await matchmakingLobby.Value.Join();
    }

	public async Task<bool> JoinSteamPartyLobby(Lobby lobby)
	{
		Logger.instance.Log($"Joining party lobby: {lobby.Id}");
		LeaveCurrentPartyLobby();
		try
		{
			partyLobby = lobby;
			await partyLobby.Value.Join();
			SetPlayerReady(LobbyUI.instance.GetPlayerReady());
			Logger.instance.Log($"Joined party lobby: {partyLobby.Value.Id}");
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
		Logger.instance.Log($"Joining party lobby from friend: {friend.Name}");
		try
		{
			Lobby? lobby = GetPartyLobbyFromFriend(friend);
			if (!lobby.HasValue)
			{
				if (retry)
				{
					Logger.instance.Log($"Retrying to get party lobby from friend: {friend.Name}");
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
		Logger.instance.Log($"Joining party lobby from Id: {lobbyId}");
		LeaveCurrentPartyLobby();
		try
		{
			partyLobby = await SteamMatchmaking.JoinLobbyAsync(lobbyId);
			// SetFriendsCanJoin();
			SetPlayerReady(LobbyUI.instance.GetPlayerReady());
			// LobbyUI.instance.JoinLobby(currentLobby);
			Logger.instance.Log($"Joined party lobby: {partyLobby.Value.Id}");
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
		Logger.instance.Log($"Joining matchmaking lobby from Id: {lobbyId}");
		LeaveCurrentMatchmakingLobby();
		try
		{
			matchmakingLobby = await SteamMatchmaking.JoinLobbyAsync(lobbyId);
			Logger.instance.Log($"Joined matchmaking lobby: {matchmakingLobby.Value.Id}");
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
        Logger.instance.Log("LeaveCurrentPartyLobby");
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
        Logger.instance.Log("LeaveCurrentMatchmakingLobby", 20, true);
		if (partyLobby.HasValue && partyLobby.Value.Id.IsValid && partyLobby.Value.Owner.Id == SteamClient.SteamId)
		{
            // tell other party members we're leaving matchmaking
            Logger.instance.Log("Setting matchmaking lobby to null", 20, true);
            partyLobby.Value.SetData(LobbyKeys.MatchmakingLobby, null);
        }
        if (matchmakingLobby.HasValue && matchmakingLobby.Value.Id.IsValid)
		{
            Logger.instance.Log("leaving matchmaking lobby and setting it to null", 20, true);
            matchmakingLobby.Value.Leave();
			matchmakingLobby = null;
        }
    }
	private void SetRichPresence(string key, string val)
	{
		if (SteamFriends.GetRichPresence(key) != val)
		{
			SteamFriends.SetRichPresence(key, val);
			Logger.instance.Log($"SetRichPresence {key}: {val}");
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
		if (CurrentPartyLobbyCanBeJoinedByFriends())
		{
            SetRichPresence("connect", $"{partyLobby.Value.Id}");
            partyLobby.Value.SetFriendsOnly();
            partyLobby.Value.SetData(LobbyKeys.LobbyType, LobbyType.FriendsOnly.ToString());
            SetRichPresence("gamestatus", "Joinable");
		}
		else
		{
            SetRichPresence("connect", null);
            partyLobby.Value.SetInvisible();
            partyLobby.Value.SetData(LobbyKeys.LobbyType, LobbyType.Invisible.ToString());
            if (partyLobby.Value.GetData(LobbyKeys.LobbyState) == LobbyState.SearchingForGame.ToString())
            {
                SetRichPresence("gamestatus", "WaitingForMatch");
            }
            else
            {
                SetRichPresence("gamestatus", "AtMainMenu");
            }
        }
        SetRichPresence("steam_display", "#StatusWithoutHealth");
    }

	public void SetPartyLobbyMemberData(string key, string val)
	{
		if (partyLobby.HasValue && partyLobby.Value.Id.IsValid)
		{
            partyLobby.Value.SetMemberData(key, val);
			Logger.instance.Log($"SetMemberData {key}: {val}");
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
        if (!Enum.TryParse(partyLobby.Value.GetData(LobbyKeys.LobbyState), out LobbyState lobbyState))
        {
            return;
        }
        if (lobbyState == LobbyState.GameStarting)
		{
			return;
		}
        Logger.instance.Log($"CheckForAllPlayersReady lobbyState: {lobbyState}");
		Friend[] friendsInLobby = partyLobby.Value.Members.ToArray();
		for (int i = 0; i < friendsInLobby.Length; i++)
		{
            string readyStatus = partyLobby.Value.GetMemberData(friendsInLobby[i], LobbyKeys.Ready);
			if (!Enum.TryParse(readyStatus, out ReadyState readyState) || readyState != ReadyState.Ready)
			{
                if (lobbyState == LobbyState.SearchingForGame)
                {
                    Logger.instance.Log("Stopping searching, someone unreadied", 20, true);
					LeaveCurrentMatchmakingLobby();
                    partyLobby.Value.SetFriendsOnly();
                    partyLobby.Value.SetData(LobbyKeys.LobbyType, LobbyType.FriendsOnly.ToString());
                    partyLobby.Value.SetData(LobbyKeys.LobbyState, LobbyState.WaitingForReady.ToString());
                }
                return;
			}
        }
        Logger.instance.Log("all players ready", 20, true);
		// All players are ready, start searching for a game if there's
		// less than lobbySize players, else start the game
		
        if (partyLobby.Value.MemberCount < partyLobby.Value.MaxMembers && lobbyState != LobbyState.SearchingForGame)
		{
            Logger.instance.Log("Starting searching for game", 20, true);
            
			_ = StartSteamMatchmaking();
        }
		else if (partyLobby.Value.MemberCount == partyLobby.Value.MaxMembers && serverState == LocalConnectionState.Stopped)
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
	public Lobby? GetCurrentPartyLobby()
	{
		return partyLobby;
    }
	public Lobby? GetCurrentMatchmakingLobby()
	{
		return matchmakingLobby;
    }
}