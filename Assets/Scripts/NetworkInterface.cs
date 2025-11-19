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

// Unity version 6000.2.9f1
// FishNetworking version 4.6.16R.Prerelease.01
// FishyFacepunch version 4.1.0
// Facepunch version 2.4.1

public class NetworkInterface : MonoBehaviour
{
	[SerializeField] private TMP_InputField hostSteamIDInputField;
	[SerializeField] private FishyFacepunch.FishyFacepunch fishyFacepunch;
	[SerializeField] private NetworkManager networkManager;
	public int lobbySize = 4;
	public uint appId;

	private bool eventsSubscribed = false;
	// private bool searchingForMatch = false;

	private LocalConnectionState serverState = LocalConnectionState.Stopped;
	private LocalConnectionState clientState = LocalConnectionState.Stopped;
	private Lobby? currentLobby = null;

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
			SetFriendsCanJoin(true);
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
		SetFriendsCanJoin(false);
	}

	private void OnApplicationQuit()
	{
		LeaveCurrentLobby();
		UnsubscribeFromSteamEvents();
		SetFriendsCanJoin(false);
	}

	private string GetCommandLineArgument(string name)
	{
		var args = Environment.GetCommandLineArgs();
		for (int i = 0; i < args.Length; i++)
		{
			if (args[i] == name && args.Length > i + 1)
			{
				return args[i + 1];
			}
		}
		return null;
	}

	private async Task<bool> JoinLobbyFromCommandLine()
	{
		string lobbyIdStr = GetCommandLineArgument("+connect_lobby");
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
		SteamMatchmaking.OnLobbyDataChanged += OnLobbyDataChanged;
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
		SteamMatchmaking.OnLobbyDataChanged -= OnLobbyDataChanged;
		SteamFriends.OnGameRichPresenceJoinRequested -= OnGameRichPresenceJoinRequested;

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
	}

	private void OnClientConnectionStateChanged(ClientConnectionStateArgs ccsa)
	{
		clientState = ccsa.ConnectionState;
		Logger.instance.Log($"OnClientConnectionStateChanged {clientState}");
	}

	private void OnLobbyMemberJoined(Lobby lobby, Friend friend)
	{
		Logger.instance.Log($"Player {friend.Name} joined the lobby! Lobby ID: {lobby.Id}, Member count: {lobby.MemberCount}");
		LobbyUI.instance.LobbyMemberJoined(friend);
	}

	private void OnLobbyMemberLeave(Lobby lobby, Friend friend)
	{
		Logger.instance.Log($"Player {friend.Name} left the lobby! Lobby ID: {lobby.Id}, Member count: {lobby.MemberCount}");
		LobbyUI.instance.LobbyMemberLeft(friend);
	}

	private void OnLobbyDataChanged(Lobby lobby)
	{
		if (currentLobby.HasValue && currentLobby.Value.Id.IsValid)
		{
			try
			{
				Friend[] friendsInLobby = currentLobby.Value.Members.ToArray();
				for (int i = 0; i < friendsInLobby.Length; i++)
				{
					string readyStatus = currentLobby.Value.GetMemberData(friendsInLobby[i], "ready");
					if (!string.IsNullOrEmpty(readyStatus))
					{
						LobbyUI.instance.UpdateLobbyMemberReadyStatus(friendsInLobby[i]
						, bool.Parse(readyStatus));
					}
				}
				if (currentLobby.Value.GetData("LobbyState") != "Starting game")
				{
					CheckForAllPlayersReady();
				}
				if (!currentLobby.Value.IsOwnedBy(SteamClient.SteamId)
					&& currentLobby.Value.GetData("LobbyState") == "Starting game" && clientState == LocalConnectionState.Stopped)
				{
					SetFriendsCanJoin(false);
					StartClient(currentLobby.Value.GetData("HostSteamId"));
				}
				if (currentLobby.Value.MemberCount > 1 && currentLobby.Value.GetData("LobbyState") != "Starting game")
				{
					LobbyUI.instance.SetCanLeaveLobby(true);
					if (currentLobby.Value.MemberCount < lobbySize && currentLobby.Value.GetData("LobbyState") != "Searching for game")
					{
						SetFriendsCanJoin(true);
					}
				}
				else
				{
					LobbyUI.instance.SetCanLeaveLobby(false);
				}
				LobbyUI.instance.LobbyUpdated(currentLobby.Value);
			}
			catch (Exception e)
			{
				Logger.instance.Error($"Exception in OnLobbyDataChanged: {e.Message}");
			}
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

	public async Task StartSteamLobby()
	{
		var result = await SteamMatchmaking.CreateLobbyAsync(lobbySize);
		if (result != null)
		{
			currentLobby = result.Value;
			SetPlayerReady(false);
			currentLobby.Value.SetFriendsOnly();
			currentLobby.Value.SetData("LobbyType", "Friends Only");
			currentLobby.Value.SetData("HostSteamID", SteamClient.SteamId.ToString());
			currentLobby.Value.SetData("LobbyState", "Not Searching");
			Logger.instance.Log($"Created a new lobby with Id: {currentLobby.Value.Id} and HostSteamID: {currentLobby.Value.GetData("HostSteamID")}");
		}
		else
		{
			Logger.instance.Error($"Failed to create lobby! result: {result}");
		}
	}

	public async Task<bool> JoinSteamLobby(Lobby lobby)
	{
		try
		{
			Logger.instance.Log($"Joining lobby: {lobby.Id}");
			currentLobby = lobby;
			await currentLobby.Value.Join();
			LobbyUI.instance.JoinLobby(currentLobby);
			// Get host SteamID if stored
			string hostSteamId = currentLobby.Value.GetData("HostSteamID");
			Logger.instance.Log($"Joined lobby: {currentLobby.Value.Id} hostId: {hostSteamId}");
			if (string.IsNullOrEmpty(hostSteamId))
			{
				Logger.instance.Error($"Joined Steam lobby but hostSteamId is null or empty");
			}
			return true;
		}
		catch (Exception e)
		{
			Logger.instance.Error($"Failed to join lobby: {lobby.Id} Exception: {e.Message}");
			return false;
		}
	}

	public async Task<bool> JoinSteamLobbyFromFriend(Friend friend)
	{
		Logger.instance.Log($"Joining lobby from friend: {friend.Name}");
		try
		{
			Lobby? lobby = GetLobbyFromFriend(friend);
			if (!lobby.HasValue)
			{
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
		try
		{
			Logger.instance.Log($"Joining lobby from Id: {lobbyId}");
			currentLobby = await SteamMatchmaking.JoinLobbyAsync(lobbyId);
			LobbyUI.instance.JoinLobby(currentLobby);
			Logger.instance.Log($"Joined lobby: {currentLobby.Value.Id}");
			return true;
		}
		catch (Exception e)
		{
			Logger.instance.Error($"Failed to join lobby from Id: {lobbyId} Exception: {e.Message}");
			return false;
		}
	}

	public async void LeaveCurrentLobby()
	{
		if (currentLobby.HasValue && currentLobby.Value.Id.IsValid)
		{
			currentLobby.Value.Leave();
			currentLobby = null;
			LobbyUI.instance.LeaveCurrentLobby();
			Logger.instance.Log("Left lobby");
		}
		await StartSteamLobby();
	}

	private void SetRichPresence(string key, string val)
	{
		if (string.IsNullOrEmpty(SteamFriends.GetRichPresence(key)) || SteamFriends.GetRichPresence(key) != val)
		{
			SteamFriends.SetRichPresence(key, val);
			Logger.instance.Log($"SetRichPresence {key}: {val}");
		}
	}

	public AppId GetAppId()
	{
		return (AppId)appId;
	}

	public async void SetFriendsCanJoin(bool friendsCanJoin)
	{
		Logger.instance.Log($"SetFriendsCanJoin: {friendsCanJoin}");
		if (friendsCanJoin)
		{
			if (!currentLobby.HasValue || !currentLobby.Value.Id.IsValid)
			{
				await StartSteamLobby();
			}
			if (currentLobby.HasValue && currentLobby.Value.Id.IsValid && currentLobby.Value.MemberCount < currentLobby.Value.MaxMembers)
			{
				SetRichPresence("connect", $"{currentLobby.Value.Id}");
			}
		}
		else
		{
			SetRichPresence("connect", null);
		}
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
		Logger.instance.Log($"SetPlayerReady: {ready}");
		if (currentLobby.HasValue && currentLobby.Value.Id.IsValid)
		{
			SetMemberData("ready", ready.ToString());
			if (ready)
			{
				CheckForAllPlayersReady();
			}
			else
			{
				if (currentLobby.Value.GetData("LobbyState") != "Starting game")
				{
					currentLobby.Value.SetData("LobbyState", "Not Searching");
					currentLobby.Value.SetFriendsOnly();
					currentLobby.Value.SetData("LobbyType", "Friends Only");
				}
			}
		}
	}

	public void CheckForAllPlayersReady()
	{
		Logger.instance.Log("CheckForAllPlayersReady");
		if (!currentLobby.HasValue || !currentLobby.Value.IsOwnedBy(SteamClient.SteamId))
		{
			return;
		}
		Friend[] friendsInLobby = currentLobby.Value.Members.ToArray();
		for (int i = 0; i < friendsInLobby.Length; i++)
		{
			string readyStatus = currentLobby.Value.GetMemberData(friendsInLobby[i], "ready");
			if (!string.IsNullOrEmpty(readyStatus) && !bool.Parse(readyStatus))
			{
				return;
			}
		}
		// All players are ready, start searching for a game if there's
		// less than lobbySize players, else start the game
		if (currentLobby.Value.MemberCount < lobbySize && currentLobby.Value.GetData("LobbyState") != "Searching for game")
		{
			SetFriendsCanJoin(false);
			currentLobby.Value.SetPublic();
			currentLobby.Value.SetData("LobbyType", "Public");
			currentLobby.Value.SetData("LobbyState", "Searching for game");
			Logger.instance.Log("Searching for game");
		}
		else if (currentLobby.Value.MemberCount == lobbySize && currentLobby.Value.GetData("LobbyState") != "Starting game")
		{
			StartServer();
			StartClient(SteamClient.SteamId.ToString());
			Logger.instance.Log("Starting server. clientState = {clientState}");
			currentLobby.Value.SetData("LobbyState", "Starting game");
		}
	}

	public bool LobbyCanBeJoinedByFriends(Lobby lobby)
	{
		string lobbyState = lobby.GetData("LobbyState");
		if (lobbyState == "Searching for game" || lobbyState == "Starting game")
		{
			return false;
		}
		if (lobby.MemberCount >= lobby.MaxMembers)
		{
			return false;
		}
		string lobbyType = lobby.GetData("LobbyType");
		if (lobbyType == "Private")
		{
			return false;
		}
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
}