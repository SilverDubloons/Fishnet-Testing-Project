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

public class NetworkInterface : MonoBehaviour
{
	[SerializeField] private TMP_InputField hostSteamIDInputField;
	[SerializeField] private FishyFacepunch.FishyFacepunch fishyFacepunch;
	[SerializeField] private NetworkManager networkManager;
	
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
		if(networkManager == null)
		{
			return;
		}
		networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionStateChanged;
		networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionStateChanged;
		LeaveCurrentLobby();
	}
	
	private void OnApplicationQuit()
    {
        LeaveCurrentLobby();
    }
	
	public void StartServer()
	{
		if(networkManager == null)
		{
			Log.instance.DisplayError("NetworkInterface StartServer no networkManager");
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
			Log.instance.DisplayError("NetworkInterface StartClient no networkManager");
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
		Log.instance.DisplayLog($"OnServerConnectionStateChanged {serverState}");
		if(serverState == LocalConnectionState.Started)
		{
			Log.instance.DisplayLog($"SteamID64={GetLocalSteamID64()}");
		}
	}
	
	private void OnClientConnectionStateChanged(ClientConnectionStateArgs scsa)
	{
		clientState = scsa.ConnectionState;
		Log.instance.DisplayLog($"OnClientConnectionStateChanged {clientState}");
	}
	
	public ulong GetLocalSteamID64()
	{
		return fishyFacepunch.LocalUserSteamID;
	}
	
	public async Task<ulong> WaitForValidSteamID()
	{
		ulong id = 0;
		int retries = 0;
		while ((id = GetLocalSteamID64()) == 0 && retries < 100)
		{
			await Task.Delay(50);
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
		FindQuickMatch();
	}
	
	public async void FindQuickMatch()
	{
		var lobbyList = await SteamMatchmaking.LobbyList
			.WithSlotsAvailable(1)
			.RequestAsync();

		if (lobbyList == null || lobbyList.Length == 0)
		{
			// No open lobbies found, create one
			var result = await SteamMatchmaking.CreateLobbyAsync(4);
			if (result != null)
			{
				currentLobby = result.Value;
				StartServer();
				ulong steamId = await WaitForValidSteamID();
				string hostId =  steamId.ToString();
				Log.instance.DisplayLog($"hostId = {hostId}");
				currentLobby.Value.SetData("HostID", hostId);
				
				while(serverState != LocalConnectionState.Started)
				{
					Log.instance.DisplayLog("Waiting for server to start");
					await Task.Delay(25);
				}
				StartClient(hostId);
				Log.instance.DisplayLog($"Created a new lobby with Id: {currentLobby.Value.Id} and HostID: {currentLobby.Value.GetData("HostID")}");
			}
			else
			{
				Log.instance.DisplayError("Failed to create lobby!");
			}
		}
		else
		{
			// Join the first available lobby
			currentLobby = lobbyList[0];
			await currentLobby.Value.Join();
			Log.instance.DisplayLog($"Joined lobby: {currentLobby.Value.Id}");

			// Get host SteamID if stored
			string hostId = currentLobby.Value.GetData("HostID");
			Log.instance.DisplayLog($"hostId: {hostId}");
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
			Log.instance.DisplayLog("Left lobby");
		}
	}
}
