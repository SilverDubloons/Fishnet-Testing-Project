using UnityEngine;
using Steamworks;
using Steamworks.Data;
using System.Threading.Tasks;
using System;

public class FriendUI : MonoBehaviour
{
    // public SteamId steamId;
    
	public RectTransform rt;
	[SerializeField] private UnityEngine.UI.Image avatarImage;
	[SerializeField] private Label nameLabel;
	[SerializeField] private Label statusLabel;
	
	public FriendState state;
	public Friend friend;
	private string gameName = string.Empty;
	
	public async void SetupFriendUI(Friend newFriend)
	{
		this.friend = newFriend;
		state = friend.State;
		nameLabel.ChangeText(friend.Name);
		Friend.FriendGameInfo? gameInfo = friend.GameInfo;
		if(gameInfo.HasValue)
		{
			Steamworks.Data.GameId gameID = gameInfo.Value.GameID;
			uint appIDuint = gameID.AppId;
			AppId appId = (AppId)appIDuint;
			gameName = await FriendsList.instance.GetGameNameFromAppIDAsync(appId);
			statusLabel.ChangeText(gameName);
			statusLabel.ChangeColor(FriendsList.instance.GetStateColor(state, true));
			nameLabel.ChangeColor(FriendsList.instance.GetStateColor(state, true));
		}
		else
		{
			statusLabel.ChangeText(FriendsList.GetStateString(state));
			statusLabel.ChangeColor(FriendsList.instance.GetStateColor(state, false));
			nameLabel.ChangeColor(FriendsList.instance.GetStateColor(state, false));
			gameName = string.Empty;
		}
		
		Image? avatar = await FriendsList.GetAvatar(friend.Id);
		if(avatar.HasValue)
		{
			Texture2D avatarTex = FriendsList.Covert(avatar.Value);
			avatarImage.sprite = Sprite.Create(avatarTex, new Rect(0, 0, avatarTex.width, avatarTex.height), new Vector2(0.5f, 0.5f), 100f);
		}
		else
		{
			avatarImage.sprite = FriendsList.instance.noAvatarSprite;
		}
	}
	
	public async void UpdateFriendUI()
	{
		Friend.FriendGameInfo? gameInfo = friend.GameInfo;
		string updatedGameName = gameName;
		if(gameInfo.HasValue)
		{
			Steamworks.Data.GameId gameID = gameInfo.Value.GameID;
			uint appIDuint = gameID.AppId;
			AppId appId = (AppId)appIDuint;
			updatedGameName = await FriendsList.instance.GetGameNameFromAppIDAsync(appId);
		}
		else
		{
			updatedGameName = string.Empty;
		}
		if(state != friend.State || gameName != updatedGameName)
		{
			if(gameInfo.HasValue)
			{
				statusLabel.ChangeText(gameName);
				statusLabel.ChangeColor(FriendsList.instance.GetStateColor(state, true));
				nameLabel.ChangeColor(FriendsList.instance.GetStateColor(state, true));
			}
			else
			{
				statusLabel.ChangeText(FriendsList.GetStateString(state));
				statusLabel.ChangeColor(FriendsList.instance.GetStateColor(state, false));
				nameLabel.ChangeColor(FriendsList.instance.GetStateColor(state, false));
			}
		}
		gameName = updatedGameName;
	}
	
	public int CompareTo(FriendUI val)
	{
		if(friend.IsPlayingThisGame != val.friend.IsPlayingThisGame)
		{
			return val.friend.IsPlayingThisGame.CompareTo(friend.IsPlayingThisGame);
		}
		if(gameName == string.Empty && val.gameName != string.Empty)
		{
			return 1;
		}
		if(val.gameName == string.Empty && gameName != string.Empty)
		{
			return -1;
		}
		if(gameName != string.Empty && val.gameName != string.Empty)
		{
			if(state != val.state)
			{
				return FriendsList.GetStatePriority(val.state).CompareTo(FriendsList.GetStatePriority(state));
			}
			else
			{
				return val.gameName.CompareTo(gameName);
			}
		}
		if(FriendsList.GetStatePriority(val.state) != FriendsList.GetStatePriority(state))
		{
			return FriendsList.GetStatePriority(val.state).CompareTo(FriendsList.GetStatePriority(state));
		}
		return friend.Name.CompareTo(val.friend.Name);
	}
	
	public Sprite GetAvatarSprite()
	{
		return avatarImage.sprite;
	}
	
	public void Click_Friend()
	{
		FriendInteractionPanel.instance.OpenPanel(friend);
	}
}
