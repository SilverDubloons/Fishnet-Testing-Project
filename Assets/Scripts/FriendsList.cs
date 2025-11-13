using UnityEngine;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using System.Collections.Generic;
using System;
using FishyFacepunch;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;

public class FriendsList : MonoBehaviour
{
    [SerializeField] private RectTransform contentRt;
	[SerializeField] private GameObject friendPrefab;
	[SerializeField] private GameObject visibilityObject;
	[SerializeField] private float timeBetweenUpdates = 10f;
	[SerializeField] private UnityEngine.Color color_Offline;
	[SerializeField] private UnityEngine.Color color_OutOfGameOnline;
	[SerializeField] private UnityEngine.Color color_InGameOnline;
	[SerializeField] private UnityEngine.Color color_OutOfGameAway;
	[SerializeField] private UnityEngine.Color color_InGameAway;
	
	public Sprite noAvatarSprite;
	
	private List<FriendUI> friendUIs = new List<FriendUI>();
	private Dictionary<Friend, FriendUI> friends = new Dictionary<Friend, FriendUI>();
	private Dictionary<AppId, string> gameNameCache = new Dictionary<AppId, string>();
	public static FriendsList instance;
	private float timeOfNextUpdate;
	
	void Awake()
	{
		instance = this;
	}
	
	void Update()
	{
		if(Time.time > timeOfNextUpdate)
		{
			timeOfNextUpdate = Time.time + timeBetweenUpdates;
			if(visibilityObject.activeInHierarchy)
			{
				UpdateFriendsList();
			}
		}
	}
	
	private void UpdateFriendsList()
	{
		foreach (var friend in SteamFriends.GetFriends())
		{
			// Logger.instance.Log( $"Id: {friend.Id} Name: {friend.Name} IsOnline: {friend.IsOnline} SteamLevel: {friend.SteamLevel}" );
			if(friends.ContainsKey(friend))
			{
				friends[friend].UpdateFriendUI();
			}
			else
			{
				GameObject newFriendUIGO = Instantiate(friendPrefab, contentRt);
				FriendUI newFriendUI = newFriendUIGO.GetComponent<FriendUI>();
				friends.Add(friend, newFriendUI);
				friendUIs.Add(newFriendUI);
				newFriendUI.SetupFriendUI(friend);
			}
		}
		friendUIs.Sort((x, y) =>
		{
			return x.CompareTo(y);
		});
		for(int i = 0; i < friendUIs.Count; i++)
		{
			friendUIs[i].rt.anchoredPosition = new Vector2(3f, -3f -47f * i);
		}
		contentRt.sizeDelta = new Vector2(contentRt.sizeDelta.x, 47f * friendUIs.Count + 3f);
	}
	
	public static int GetStatePriority(FriendState state)
	{
		switch(state)
		{
			case FriendState.Offline:
				return 0;
			case FriendState.Online:
				return 5;
			case FriendState.Busy:
				return 1;
			case FriendState.Away:
				return 3;
			case FriendState.Snooze:
				return 2;
			case FriendState.LookingToTrade:
				return 4;
			case FriendState.LookingToPlay:
				return 6;
			case FriendState.Invisible:
				return 0;
			case FriendState.Max:
				return 0;
			default:
				return 0;
		}
	}
	
	public static async Task<Image?> GetAvatar(SteamId steamId)
	{
		try
		{
			// Get Avatar using await
			return await SteamFriends.GetLargeAvatarAsync(steamId);
		}
		catch(Exception exception)
		{
			// If something goes wrong, log it
			Logger.instance.Log($"FriendUI GetAvatar() exception: {exception}");
			return null;
		}
	}
	
	public static Texture2D Covert(Image image)
	{
		// Create a new Texture2D
		var avatar = new Texture2D( (int)image.Width, (int)image.Height, TextureFormat.ARGB32, false );
		
		// Set filter type, or else its really blury
		avatar.filterMode = FilterMode.Trilinear;

		// Flip image
		for ( int x = 0; x < image.Width; x++ )
		{
			for ( int y = 0; y < image.Height; y++ )
			{
				var p = image.GetPixel( x, y );
				avatar.SetPixel( x, (int)image.Height - y, new UnityEngine.Color( p.r / 255.0f, p.g / 255.0f, p.b / 255.0f, p.a / 255.0f ) );
			}
		}
		
		avatar.Apply();
		return avatar;
	}
	
	public static string GetStateString(FriendState state)
	{
		switch(state)
		{
			case FriendState.Offline:
				return "Offline";
			case FriendState.Online:
				return "Online";
			case FriendState.Busy:
				return "Busy";
			case FriendState.Away:
				return "Away";
			case FriendState.Snooze:
				return "Snooze";
			case FriendState.LookingToTrade:
				return "Looking to Trade";
			case FriendState.LookingToPlay:
				return "Looking to Play";
			case FriendState.Invisible:
				return "Offline";
			case FriendState.Max:
				return "Max";
			default:
				return "ERROR";
		}
	}
	
	public UnityEngine.Color GetStateColor(FriendState state, bool inGame)
	{
		switch(state)
		{
			case FriendState.Offline:
				return color_Offline;
			case FriendState.Online:
				if(inGame)
				{
					return color_InGameOnline;
				}
				else
				{
					return color_OutOfGameOnline;
				}
			case FriendState.Busy:
				if(inGame)
				{
					return color_InGameOnline;
				}
				else
				{
					return color_OutOfGameOnline;
				}
			case FriendState.Away:
				if(inGame)
				{
					return color_InGameAway;
				}
				else
				{
					return color_OutOfGameAway;
				}
			case FriendState.Snooze:
				if(inGame)
				{
					return color_InGameAway;
				}
				else
				{
					return color_OutOfGameAway;
				}
			case FriendState.LookingToTrade:
				if(inGame)
				{
					return color_InGameOnline;
				}
				else
				{
					return color_OutOfGameOnline;
				}
			case FriendState.LookingToPlay:
				if(inGame)
				{
					return color_InGameOnline;
				}
				else
				{
					return color_OutOfGameOnline;
				}
			case FriendState.Invisible:
				return color_Offline;
			case FriendState.Max:
				return color_Offline;
			default:
				return UnityEngine.Color.red;
		}
	}
	
	public async Task<string> GetGameNameFromAppIDAsync(AppId appId)
	{
		if ((uint)appId == 0)
		{
			return string.Empty;
		}

		if (gameNameCache.ContainsKey(appId))
		{
			return gameNameCache[appId];
		}

		string url = $"https://store.steampowered.com/api/appdetails?appids={appId}";
		using (UnityWebRequest request = UnityWebRequest.Get(url))
		{
			await request.SendWebRequest();

			if (request.result == UnityWebRequest.Result.Success)
			{
				string jsonResponse = request.downloadHandler.text;

				try
				{
					var parsedData = JObject.Parse(jsonResponse);
					if (parsedData[appId.ToString()] != null)
					{
						var appData = parsedData[appId.ToString()];
						if (appData["success"] != null && appData["success"].Value<bool>())
						{
							string gameName = appData["data"]["name"].ToString();
							// gameNameCache[appId] = gameName;
							gameNameCache.Add(appId, gameName);
							return gameName;
						}
					}
				}
				catch (Exception ex)
				{
					Logger.instance.Error($"Failed to parse game name for AppID: {appId}. Error: {ex.Message}");
				}
			}

			Logger.instance.Error($"Failed to fetch game name for AppID: {appId}");
			return string.Empty;
		}
	}
}
