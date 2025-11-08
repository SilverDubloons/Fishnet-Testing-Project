using UnityEngine;
using FishNet.Managing.Transporting;
using FishNet.Transporting;
using FishNet.Managing;
using FishyFacepunch;
using TMPro;

public class NetworkInterface : MonoBehaviour
{
	[SerializeField] private TMP_InputField hostSteamIDInputField;
	[SerializeField] private FishyFacepunch.FishyFacepunch fishyFacepunch;
	
	private LocalConnectionState serverState = LocalConnectionState.Stopped;
	private LocalConnectionState clientState = LocalConnectionState.Stopped;
	
	public NetworkManager networkManager;
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
	}
	
	public void StartServer()
	{
		if(networkManager == null)
		{
			LocalInterface.instance.DisplayError("NetworkInterface StartServer no networkManager");
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
	
	public void StartClient()
	{
		if(networkManager == null)
		{
			LocalInterface.instance.DisplayError("NetworkInterface StartClient no networkManager");
			return;
		}

		if(clientState == LocalConnectionState.Stopped)
		{
			fishyFacepunch.SetClientAddress(hostSteamIDInputField.text);
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
		LocalInterface.instance.DisplayLog($"OnServerConnectionStateChanged {GetConnectionStateString(serverState)}");
		if(serverState == LocalConnectionState.Started)
		{
			LocalInterface.instance.DisplayLog($"SteamID64={GetSteamID64()}");
		}
	}
	
	private void OnClientConnectionStateChanged(ClientConnectionStateArgs scsa)
	{
		clientState = scsa.ConnectionState;
		LocalInterface.instance.DisplayLog($"OnClientConnectionStateChanged {GetConnectionStateString(clientState)}");
	}
	
	public string GetConnectionStateString(LocalConnectionState lcs)
	{
		switch(lcs)
		{
			case LocalConnectionState.Started:
				return "started";
			case LocalConnectionState.Starting:
				return "starting";
			case LocalConnectionState.Stopped:
				return "stopped";
			case LocalConnectionState.Stopping:
				return "stopping";
			default:
				return "UNKNOWN";
		}
	}
	
	public ulong GetSteamID64()
	{
		return fishyFacepunch.LocalUserSteamID;
	}
}
