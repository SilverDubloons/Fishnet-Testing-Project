using UnityEngine;
using FishNet.Managing.Transporting;
using FishNet.Transporting;
using FishNet.Managing;
using FishyFacepunch;
using Steamworks;
using Steamworks.Data;
using TMPro;
using System.Threading.Tasks;

// Unity version 6000.2.9f1
// FishNetworking version 4.6.16R.Prerelease.01
// FishyFacepunch version 4.1.0
// Facepunch version 2.4.1

public class NetworkInterface : MonoBehaviour
{
	[SerializeField] private TMP_InputField hostSteamIDInputField;
	[SerializeField] private FishyFacepunch.FishyFacepunch fishyFacepunch;
	[SerializeField] private NetworkManager networkManager;
	
	private bool eventsSubscribed = false;
	private bool searchingForMatch = false;
	
	private LocalConnectionState serverState = LocalConnectionState.Stopped;
	private LocalConnectionState clientState = LocalConnectionState.Stopped;
	private Lobby? currentLobby = null;
	
	// public FishyFacepunch fishyFacepunch;
    public static NetworkInterface instance;
	
	void Awake()
	{
		instance = this;
	}
	
	void Start()
	{
		networkManager.ServerManager.OnServerConnectionState += OnServerConnectionStateChanged;
		networkManager.ClientManager.OnClientConnectionState += OnClientConnectionStateChanged;
	}
	
	void OnDestroy()
	{
		if(networkManager != null)
		{
			networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionStateChanged;
			networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionStateChanged;
		}
		UnsubscribeFromSteamEvents();
		LeaveCurrentLobby();
	}
	
	private void SubscribeToSteamEvents()
	{
		if(eventsSubscribed)
		{
			return;
		}
        
		SteamMatchmaking.OnLobbyMemberJoined += OnLobbyMemberJoined;
		// SteamMatchmaking.OnLobbyMemberLeave += OnLobbyMemberLeave;
		// SteamMatchmaking.OnLobbyDataChanged += OnLobbyDataChanged;
        
		eventsSubscribed = true;
		Logger.instance.Log("Steam events subscribed");
	}
	
	private void UnsubscribeFromSteamEvents()
	{
		if(!eventsSubscribed)
		{
			return;
		}
        
		SteamMatchmaking.OnLobbyMemberJoined -= OnLobbyMemberJoined;
		// SteamMatchmaking.OnLobbyMemberLeave -= OnLobbyMemberLeave;
		// SteamMatchmaking.OnLobbyDataChanged -= OnLobbyDataChanged;
        
		eventsSubscribed = false;
		Logger.instance.Log("Steam events unsubscribed");
	}
	
	private void OnApplicationQuit()
    {
        LeaveCurrentLobby();
		UnsubscribeFromSteamEvents();
    }
	
	public void StartServer()
	{
		if(networkManager == null)
		{
			Logger.instance.Error("NetworkInterface StartServer no networkManager");
			return;
		}

		if(serverState == LocalConnectionState.Stopped)
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
		if(networkManager == null)
		{
			Logger.instance.Error("NetworkInterface StartClient no networkManager");
			return;
		}

		if(clientState == LocalConnectionState.Stopped)
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
		if(serverState == LocalConnectionState.Started)
		{
			Logger.instance.Log($"LocalSteamID64={GetLocalSteamID64()}");
		}
	}
	
	private void OnClientConnectionStateChanged(ClientConnectionStateArgs ccsa)
	{
		clientState = ccsa.ConnectionState;
		Logger.instance.Log($"OnClientConnectionStateChanged {clientState}");
	}
	
	private void OnLobbyMemberJoined(Lobby lobby, Friend friend)
	{
		Logger.instance.Log($"Player {friend.Name} joined the lobby!");
		Logger.instance.Log($"Lobby ID: {lobby.Id}, Member count: {lobby.MemberCount}");
	}
	
	private async Task UpdateLobby()
	{
		if(currentLobby.HasValue && currentLobby.Value.Id.IsValid)
		{
			
		}
	}
	
	public ulong GetLocalSteamID64()
	{
		return fishyFacepunch.LocalUserSteamID;
	}
	
	public async Task<ulong> WaitForValidSteamID()
	{
		ulong id = 0;
		int retries = 0;
		while ((id = fishyFacepunch.LocalUserSteamID) == 0 && retries < 100)
		{
			await Task.Delay(25);
			retries++;
		}
		return id;
	}
	
	public void Click_StartServer()
	{
		StartServer();
	}
	
	public void Click_StartClient()
	{
		StartClient(hostSteamIDInputField.text);
	}
	
	public void Click_QuickMatch()
	{
		if(currentLobby.HasValue && currentLobby.Value.Id.IsValid)
		{
			LeaveCurrentLobby();
		}
		else
		{
			FindQuickMatch();
		}
	}
	
	public async void FindQuickMatch()
	{
		searchingForMatch = true;
		SubscribeToSteamEvents();
		var lobbyList = await SteamMatchmaking.LobbyList
			.WithSlotsAvailable(1)
			.RequestAsync();

		if (lobbyList == null || lobbyList.Length == 0)
		{
			// No open lobbies found, create one
			var result = await SteamMatchmaking.CreateLobbyAsync(2);
			if (result != null)
			{
				currentLobby = result.Value;
				StartServer();
				ulong steamId = await WaitForValidSteamID();
				string hostId =  steamId.ToString();
				Logger.instance.Log($"hostId = {hostId}");
				currentLobby.Value.SetData("HostID", hostId);
				currentLobby.Value.SetData("GameStarted", "False");
				
				while(serverState != LocalConnectionState.Started)
				{
					Logger.instance.Log("Waiting for server to start");
					await Task.Delay(25);
				}
				StartClient(hostId);
				Logger.instance.Log($"Created a new lobby with Id: {currentLobby.Value.Id} and HostID: {currentLobby.Value.GetData("HostID")}");
			}
			else
			{
				Logger.instance.Error("Failed to create lobby!");
			}
		}
		else
		{
			// Join the first available lobby
			currentLobby = lobbyList[0];
			await currentLobby.Value.Join();
			Logger.instance.Log($"Joined lobby: {currentLobby.Value.Id}");

			// Get host SteamID if stored
			string hostId = currentLobby.Value.GetData("HostID");
			Logger.instance.Log($"hostId: {hostId}");
			if (!string.IsNullOrEmpty(hostId))
			{
				StartClient(hostId);
			}
		}
	}
	
	public void LeaveCurrentLobby()
	{
		if(currentLobby.HasValue && currentLobby.Value.Id.IsValid)
		{
			currentLobby.Value.Leave();
			currentLobby = null;
			Logger.instance.Log("Left lobby");
		}
	}
}
