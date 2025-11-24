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
    public CachedLobbyData cachedLobbyData;
	public bool DefaultLobbiesToInvisible = false;

    private bool eventsSubscribed = false;
	// private bool searchingForMatch = false;

	private LocalConnectionState serverState = LocalConnectionState.Stopped;
	private LocalConnectionState clientState = LocalConnectionState.Stopped;
	private Lobby? currentLobby = null;
	private List<Friend> friendsQueuedWith = new();

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
			Logger.instance.Log($"Could not start lobby. Exception: {e.Message}");
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
		UnsubscribeFromSteamEvents();
		LeaveCurrentLobby();
		SetFriendsCanJoin();
	}
	private void OnApplicationQuit()
	{
		LeaveCurrentLobby();
		UnsubscribeFromSteamEvents();
		SetFriendsCanJoin();
	}
	private async Task<bool> JoinLobbyFromCommandLine()
	{
		string lobbyIdStr = LocalInterface.GetCommandLineArgument("+connect_lobby");
		Logger.instance.Log($"lobbyIdStr: {lobbyIdStr}");
		if (string.IsNullOrEmpty(lobbyIdStr))
		{
			return false;
		}
		return await JoinSteamLobbyFromId((SteamId)ulong.Parse(lobbyIdStr));
	}
	private async Task<bool> JoinLobbyFromSteamCommandLine()
	{
		string steamCommandLine = SteamApps.CommandLine;
		Logger.instance.Log($"steamCommandLine: \"{steamCommandLine}\"");
		if (string.IsNullOrEmpty(steamCommandLine))
		{
			return false;
		}
		try
		{
			return await JoinSteamLobbyFromId((SteamId)ulong.Parse(steamCommandLine));
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
			networkManager.ServerManager.StopConnection(true);
		}
	}
	public void StartClient(string clientAddress)
	{
        Logger.instance.Log($"Starting client connection to address: {clientAddress} clientState: {clientState}");
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
			networkManager.ClientManager.StopConnection();
		}
	}

    private void OnServerConnectionStateChanged(ServerConnectionStateArgs scsa)
	{
		serverState = scsa.ConnectionState;
		Logger.instance.Log($"OnServerConnectionStateChanged {serverState}");
		if (serverState == LocalConnectionState.Started)
		{
			if(!currentLobby.HasValue || !currentLobby.Value.Id.IsValid)
			{
				Logger.instance.Error("Current lobby is not valid when trying to start client after server start");
                return;
			}
			if (currentLobby.Value.Owner.Id != SteamClient.SteamId)
			{
				return;
			}
            Logger.instance.Log($"Game is ready to start! clientState: {clientState}");
            // StartClient(SteamClient.SteamId.ToString());
			// Logger.instance.Log($"StartClient called! clientState: {clientState}");
            currentLobby.Value.SetData(LobbyKeys.LobbyState, LobbyState.GameStarting.ToString());
		}
    }
	private void OnClientConnectionStateChanged(ClientConnectionStateArgs ccsa)
	{
		clientState = ccsa.ConnectionState;
		Logger.instance.Log($"OnClientConnectionStateChanged {clientState}");
	}
	private void OnLobbyMemberJoined(Lobby lobby, Friend friend)
	{
        if (currentLobby.Value.Id != lobby.Id)
        {
            Logger.instance.Warning($"OnLobbyMemberJoined for a lobby that is not the current lobby. currentLobby.Value.Id: {currentLobby.Value.Id}, changed LobbyId: {lobby.Id}");
            return;
        }
        Logger.instance.Log($"Player {friend.Name} joined the lobby! Lobby ID: {lobby.Id}, Member count: {lobby.MemberCount}");
		LobbyUI.instance.LobbyMemberJoined(friend, lobby);
		cachedLobbyData.OnLobbyMemberJoined(friend, lobby);
        LobbyUI.instance.LobbyUpdated(lobby);
		if(Enum.TryParse(lobby.GetData(LobbyKeys.LobbyState), out LobbyState lobbyState) && lobbyState != LobbyState.SearchingForGame)
		{
			if (!friendsQueuedWith.Contains(friend))
			{
				friendsQueuedWith.Add(friend);
			}
        }
    }
	private void OnLobbyMemberLeave(Lobby lobby, Friend friend)
	{
        if (currentLobby.Value.Id != lobby.Id)
        {
            Logger.instance.Warning($"OnLobbyMemberLeave for a lobby that is not the current lobby. currentLobby.Value.Id: {currentLobby.Value.Id}, changed LobbyId: {lobby.Id}");
            return;
        }
        Logger.instance.Log($"Player {friend.Name} left the lobby! Lobby ID: {lobby.Id}, Member count: {lobby.MemberCount}");
		LobbyUI.instance.LobbyMemberLeft(friend);
		cachedLobbyData.OnLobbyMemberLeave(lobby, friend);
		if (Enum.TryParse(lobby.GetData(LobbyKeys.LobbyState), out LobbyState lobbyState) && lobbyState == LobbyState.SearchingForGame)
		{
			if (friendsQueuedWith.Contains(friend))
			{
				friendsQueuedWith.Remove(friend);
				lobby.SetMemberData(LobbyKeys.Ready, ReadyState.NotReady.ToString());
				if (lobby.Owner.Id == SteamClient.SteamId)
				{
					lobby.SetFriendsOnly();
					lobby.SetData(LobbyKeys.LobbyType, LobbyType.FriendsOnly.ToString());
					lobby.SetData(LobbyKeys.LobbyState, LobbyState.WaitingForReady.ToString());
					Friend[] friendsInLobby = lobby.Members.ToArray();
					for (int i = 0; i < friendsInLobby.Length; i++)
					{
						if (!friendsQueuedWith.Contains(friendsInLobby[i]))
						{
							lobby.SendChatString($"{LobbyKeys.KickMember}{friendsInLobby[i].Id}");
						}
					}
				}
			}
		}
        LobbyUI.instance.LobbyUpdated(lobby);
    }
	private void OnLobbyMemberKicked(Lobby lobby, Friend kickedFriend, Friend friendWhoKicked)
	{
		Logger.instance.Log($"Player {kickedFriend.Name} was kicked from the lobby by {friendWhoKicked.Name}! Lobby ID: {lobby.Id}, Member count: {lobby.MemberCount}");
        // only do this if it doesn't also trigger onlobbymemberleave
        OnLobbyMemberLeave(lobby, kickedFriend);
    }
	private void OnLobbyDataChanged(Lobby lobby)
	{
		// Logger.instance.Log($"OnLobbyDataChanged");
		// if(cachedLobbyData.lobbyId != lobby.Id)
		if(currentLobby.Value.Id != lobby.Id)
		{
			// Logger.instance.Log($"Lobby data changed for a lobby that is not the current cached lobby. cachedLobbyId: {cachedLobbyData.lobbyId}, changedLobbyId: {lobby.Id}");
			Logger.instance.Warning($"OnLobbyDataChanged for a lobby that is not the current lobby. currentLobby.Value.Id: {currentLobby.Value.Id}, changed LobbyId: {lobby.Id}");
			return;
        }
        LobbyChanges lobbyChanges = cachedLobbyData.GetLobbyChanges(lobby);
		if (!lobbyChanges.Any)
		{
            // Logger.instance.Log($"No relevant lobby changes");
            return;
		}
		if (lobbyChanges.ownerChange.LobbyOwnerChanged)
		{
			Logger.instance.Log($"Lobby owner changed from {lobbyChanges.ownerChange.OldOwner} to {lobbyChanges.ownerChange.NewOwner}");
        }
		if (lobbyChanges.typeChange.LobbyTypeChanged)
		{ 
			Logger.instance.Log($"Lobby type changed from {lobbyChanges.typeChange.OldType} to {lobbyChanges.typeChange.NewType}");
			SetFriendsCanJoin();
        }
		if (lobbyChanges.stateChange.LobbyStateChanged)
		{ 
			Logger.instance.Log($"Lobby state changed from {lobbyChanges.stateChange.OldState} to {lobbyChanges.stateChange.NewState}");
			SetFriendsCanJoin();
            if (lobbyChanges.stateChange.NewState == LobbyState.Join)
			{
				string lobbyStateString = lobby.GetData(LobbyKeys.LobbyState);
				string rawId = lobbyStateString[LobbyState.Join.ToString().Length..];
				if (ulong.TryParse(rawId, out ulong targetId))
				{
					_ = JoinSteamLobbyFromId(targetId);
					return;
				}
				Logger.instance.Error($"Lobby state set to join but lobby state format was incorrect. rawId: {rawId} targetId: {targetId}");
			}
			else if (lobbyChanges.stateChange.NewState == LobbyState.GameStarting)
			{
                StartClient(lobby.GetData(LobbyKeys.HostSteamId));
            }
        }
        LobbyUI.instance.LobbyUpdated(lobby);
    }

	private void OnLobbyMemberDataChanged(Lobby lobby, Friend friend)
	{
        if (currentLobby.Value.Id != lobby.Id)
        {
            Logger.instance.Warning($"OnLobbyMemberDataChanged for a lobby that is not the current lobby. currentLobby.Value.Id: {currentLobby.Value.Id}, changed LobbyId: {lobby.Id}");
            return;
        }
        ReadyStateChange readyStateChange = cachedLobbyData.GetReadyStateChange(lobby, friend);
        Logger.instance.Log($"OnLobbyMemberDataChanged friend: {friend.Name}, readyChanged: {readyStateChange.ReadyStateChanged} OldState: {readyStateChange.OldState} NewState: {readyStateChange.NewState}");
        if (readyStateChange.ReadyStateChanged)
		{
            CheckForAllPlayersReady();
            LobbyUI.instance.UpdateLobbyMemberReadyStatus(friend, readyStateChange.NewState == ReadyState.Ready);
        }
        LobbyUI.instance.LobbyUpdated(lobby);
    }
	private void OnChatMessage(Lobby lobby, Friend friend, string message)
	{
        if (currentLobby.Value.Id != lobby.Id)
        {
            Logger.instance.Warning($"OnChatMessage for a lobby that is not the current lobby. currentLobby.Value.Id: {currentLobby.Value.Id}, messaged LobbyId: {lobby.Id}");
            return;
        }
        Logger.instance.Log($"{friend.Name} sent chat message: {message}");
		if(message.Length >= LobbyKeys.KickMember.Length && message.StartsWith(LobbyKeys.KickMember))
		{
			string rawId = message[LobbyKeys.KickMember.Length..];
			if (ulong.TryParse(rawId, out ulong targetId))
			{
				if (targetId == SteamClient.SteamId)
				{
					Logger.instance.Log($"Kicking self from lobby as requested by {friend.Name}");
					_ = KickSelfFromLobby();
				}
				else
				{
					Logger.instance.Log($"Received kick request for non-self player Id: {targetId} from {friend.Name}");
				}
				return;
			}
			Logger.instance.Error($"KickMember message format incorrect. rawId: {rawId} targetId: {targetId}");
		}
	}
    private async void OnGameRichPresenceJoinRequested(Friend friend, string data)
	{
		Logger.instance.Log($"OnGameRichPresenceJoinRequested friend: {friend.Name}, data: {data}");
		try
		{
			await JoinSteamLobbyFromId((SteamId)ulong.Parse(data));
		}
		catch (Exception e)
		{
			Logger.instance.Log($"Failed to join lobby from rich presence data: \"{data}\" exception: {e.Message}");
		}
	}

	private async void OnGameLobbyJoinRequested(Lobby lobby, SteamId steamId)
	{
		Logger.instance.Log($"OnGameLobbyJoinRequested steamId: {steamId}, lobby: {lobby.Id}");
		try
		{
			await JoinSteamLobby(lobby);
		}
		catch (Exception e)
		{
			Logger.instance.Log($"Failed to join lobby from OnGameLobbyJoinRequested: \"{lobby.Id}\" exception: {e.Message}");
		}
	}
	private void OnLobbyEntered(Lobby lobby)
	{
        Logger.instance.Log($"OnLobbyEntered lobby: {lobby.Id}");
        cachedLobbyData.SetupFromNewLobby(lobby);
		SetFriendsCanJoin();
        // LobbyUI.instance.LobbyUpdated(lobby);
        LobbyUI.instance.JoinLobby(lobby);
    }
	public async Task KickSelfFromLobby()
	{
        LeaveCurrentLobby();
        friendsQueuedWith.Sort((a, b) => b.Id.Value.CompareTo(a.Id.Value));
        // If someone else has the highest ID, they should start a new lobby
        if (friendsQueuedWith.Count > 0 && friendsQueuedWith[0].Id != SteamClient.SteamId)
        {
			await JoinSteamLobbyFromFriend(friendsQueuedWith[0], true);
            return;
        }
        // Local player has the highest ID (or no friends queued with) — proceed
       await StartSteamLobby();
    }
    public async Task StartSteamLobby()
	{
		LeaveCurrentLobby();
		var result = await SteamMatchmaking.CreateLobbyAsync(lobbySize);
		if (result != null)
		{
			currentLobby = result.Value;
			if (DefaultLobbiesToInvisible)
			{
				currentLobby.Value.SetInvisible();
                currentLobby.Value.SetData(LobbyKeys.LobbyType, LobbyType.Invisible.ToString());
            }
			else
			{
                currentLobby.Value.SetFriendsOnly();
                currentLobby.Value.SetData(LobbyKeys.LobbyType, LobbyType.FriendsOnly.ToString());
            }
			currentLobby.Value.SetData(LobbyKeys.HostSteamId, SteamClient.SteamId.ToString());
            currentLobby.Value.SetData(LobbyKeys.LobbyState, LobbyState.WaitingForReady.ToString());
            /*currentLobby.Value.SetData(LobbyKeys.JoinableState, DefaultLobbiesToJoinable ? JoinableState.Joinable.ToString() : JoinableState.NotJoinable.ToString());*/
            SetFriendsCanJoin();
			Logger.instance.Log($"Created a new lobby with Id: {currentLobby.Value.Id} and HostSteamID: {currentLobby.Value.GetData(LobbyKeys.HostSteamId)}");
		}
		else
		{
			Logger.instance.Error($"Failed to create lobby! result: {result}");
		}
	}

	public async Task<bool> JoinSteamLobby(Lobby lobby)
	{
		Logger.instance.Log($"Joining lobby: {lobby.Id}");
		LeaveCurrentLobby();
		try
		{
			currentLobby = lobby;
			await currentLobby.Value.Join();
            // LobbyUI.instance.JoinLobby(lobby);
            // SetFriendsCanJoin();
			SetPlayerReady(LobbyUI.instance.GetPlayerReady());
			Logger.instance.Log($"Joined lobby: {currentLobby.Value.Id}");
			return true;
		}
		catch (Exception e)
		{
			Logger.instance.Error($"Failed to join lobby: {lobby.Id} Exception: {e.Message}");
			return false;
		}
	}

	public async Task<bool> JoinSteamLobbyFromFriend(Friend friend, bool retry = false)
	{
		Logger.instance.Log($"Joining lobby from friend: {friend.Name}");
		try
		{
			Lobby? lobby = GetLobbyFromFriend(friend);
			if (!lobby.HasValue)
			{
				if (retry)
				{
					Logger.instance.Log($"Retrying to get lobby from friend: {friend.Name}");
					int retries = 0;
					while (retries < 10 && !lobby.HasValue)
					{
						await Task.Delay(500);
						lobby = GetLobbyFromFriend(friend);
					}
					if (!lobby.HasValue)
					{
						Logger.instance.Error($"Failed to get lobby from friend on retry: {friend.Name}");
						return false;
					}
				}
				else
				{
					Logger.instance.Error($"Failed to get lobby from friend: {friend.Name}");
					return false;
                }
                return false;
			}
			if (!LobbyCanBeJoinedByFriends(lobby.Value))
			{
				return false;
			}
			return await JoinSteamLobby(lobby.Value);
		}
		catch (Exception e)
		{
			Logger.instance.Error($"Failed to join lobby from friend: {friend.Name} Exception: {e.Message}");
			return false;
		}
	}

	public async Task<bool> JoinSteamLobbyFromId(SteamId lobbyId)
	{
		Logger.instance.Log($"Joining lobby from Id: {lobbyId}");
		LeaveCurrentLobby();
		try
		{
			currentLobby = await SteamMatchmaking.JoinLobbyAsync(lobbyId);
			// SetFriendsCanJoin();
			SetPlayerReady(LobbyUI.instance.GetPlayerReady());
			// LobbyUI.instance.JoinLobby(currentLobby);
			Logger.instance.Log($"Joined lobby: {currentLobby.Value.Id}");
			return true;
		}
		catch (Exception e)
		{
			Logger.instance.Error($"Failed to join lobby from Id: {lobbyId} Exception: {e.Message}");
			return false;
		}
	}

	public void LeaveCurrentLobby()
	{
        Logger.instance.Log("LeaveCurrentLobby");
        cachedLobbyData.Reset();
        if (currentLobby.HasValue && currentLobby.Value.Id.IsValid)
		{
			currentLobby.Value.Leave();
			currentLobby = null;
			LobbyUI.instance.LeaveCurrentLobby();
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
		if (!currentLobby.HasValue || !currentLobby.Value.Id.IsValid)
		{
			await StartSteamLobby();
			if (!currentLobby.HasValue || !currentLobby.Value.Id.IsValid)
			{
				return;
			}
		}
		if (!CurrentLobbyCanBeJoinedByFriends())
		{
			SetRichPresence("connect", null);
            currentLobby.Value.SetInvisible();
			currentLobby.Value.SetData(LobbyKeys.LobbyType, LobbyType.Invisible.ToString());
            // currentLobby.Value.SetJoinable(false);
            // currentLobby.Value.SetData(LobbyKeys.JoinableState, JoinableState.NotJoinable.ToString());
            if (currentLobby.Value.GetData(LobbyKeys.LobbyState) == LobbyState.SearchingForGame.ToString())
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
		SetRichPresence("connect", $"{currentLobby.Value.Id}");
		currentLobby.Value.SetFriendsOnly();
		currentLobby.Value.SetData(LobbyKeys.LobbyType, LobbyType.FriendsOnly.ToString());
        // currentLobby.Value.SetJoinable(true);
        // currentLobby.Value.SetData(LobbyKeys.JoinableState, JoinableState.Joinable.ToString());
        SetRichPresence("gamestatus", "Joinable");
        SetRichPresence("steam_display", "#StatusWithoutHealth");
    }

	public void SetMemberData(string key, string val)
	{
		if (currentLobby.HasValue && currentLobby.Value.Id.IsValid)
		{
			currentLobby.Value.SetMemberData(key, val);
			Logger.instance.Log($"SetMemberData {key}: {val}");
		}
	}

	public void SetPlayerReady(bool ready)
	{
		if (currentLobby.HasValue && currentLobby.Value.Id.IsValid)
		{
			SetMemberData(LobbyKeys.Ready, ready ? ReadyState.Ready.ToString() : ReadyState.NotReady.ToString());
		}
	}

	public void CheckForAllPlayersReady()
	{
		if (!currentLobby.HasValue || !currentLobby.Value.IsOwnedBy(SteamClient.SteamId))
		{
			return;
		}
        if (currentLobby.Value.GetData(LobbyKeys.LobbyState) == LobbyState.GameStarting.ToString())
		{
			return;
		}
        Logger.instance.Log("CheckForAllPlayersReady");
		Friend[] friendsInLobby = currentLobby.Value.Members.ToArray();
		for (int i = 0; i < friendsInLobby.Length; i++)
		{
            string readyStatus = currentLobby.Value.GetMemberData(friendsInLobby[i], LobbyKeys.Ready);
			if (!Enum.TryParse(readyStatus, out ReadyState readyState) || readyState == ReadyState.NotReady)
			{
                if (currentLobby.Value.GetData(LobbyKeys.LobbyState) == LobbyState.SearchingForGame.ToString())
                {
                    Logger.instance.Log("Stopping searching, someone unreadied");
                    currentLobby.Value.SetFriendsOnly();
                    currentLobby.Value.SetData(LobbyKeys.LobbyType, LobbyType.FriendsOnly.ToString());
                    currentLobby.Value.SetData(LobbyKeys.LobbyState, LobbyState.WaitingForReady.ToString());
                }
                return;
			}
        }
        Logger.instance.Log("all players ready");
        // All players are ready, start searching for a game if there's
        // less than lobbySize players, else start the game
        if (currentLobby.Value.MemberCount < lobbySize && currentLobby.Value.GetData(LobbyKeys.LobbyState) != LobbyState.SearchingForGame.ToString())
		{
            Logger.instance.Log("Starting searching for game");
            currentLobby.Value.SetPublic();
			currentLobby.Value.SetData(LobbyKeys.LobbyType, LobbyType.Public.ToString());
			currentLobby.Value.SetData(LobbyKeys.LobbyState, LobbyState.SearchingForGame.ToString());
			_ = SearchForSteamLobbyToJoin();
		}
		else if (currentLobby.Value.MemberCount == currentLobby.Value.MaxMembers)
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


    public bool CurrentLobbyCanBeJoinedByFriends()
	{
		if (!currentLobby.HasValue || !currentLobby.Value.Id.IsValid)
		{
			return false;
		}
		return LobbyCanBeJoinedByFriends(currentLobby.Value);
	}

	public Lobby? GetLobbyFromFriend(Friend friend)
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

	private async Task SearchForSteamLobbyToJoin()
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
        await JoinSteamLobby(targetLobby);
    }

	public string GetCurrentLobbyIdStringForConnection()
	{
		if (!currentLobby.HasValue || !currentLobby.Value.Id.IsValid)
		{
			return null;
		}
		if (!CurrentLobbyCanBeJoinedByFriends())
		{
			return null;
		}
        return currentLobby.Value.Id.ToString();
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

	public string GetFriendsQueuedWithString()
	{
		string result = "";
		for (int i = 0; i < friendsQueuedWith.Count; i++)
		{
			result += friendsQueuedWith[i].Name;
			if (i < friendsQueuedWith.Count - 1)
			{
				result += ", ";
			}
		}
		return result;
    }
}