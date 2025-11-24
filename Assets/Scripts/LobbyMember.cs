using UnityEngine;
using Steamworks;
using Steamworks.Data;
using System.Threading.Tasks;
using System;

public class LobbyMember : MonoBehaviour
{
	[SerializeField] public RectTransform rt;
	[SerializeField] public UnityEngine.UI.Image avatarImage;
	[SerializeField] public Label nameLabel;
	[SerializeField] private UnityEngine.UI.Image backdropImage;
	[SerializeField] private GameObject inLobbyVisibilityObject;
	[SerializeField] private GameObject noPlayerVisibilityObject;
	
	public Friend? friend;
	public bool readyStatus = false;
	
	public async void SetupLobbyMember(Friend newFriend, bool isReady)
	{
		SetPlayerVisibility(true);
		friend = newFriend;
		nameLabel.ChangeText(newFriend.Name);
		Sprite avatarSprite = FriendsList.instance.GetFriendSprite(newFriend);
		if(avatarSprite != null)
		{
			avatarImage.sprite = avatarSprite;
		}
		else
		{
			Image? avatar = await FriendsList.GetAvatar(newFriend.Id);
			if(avatar.HasValue)
			{
				Texture2D avatarTex = FriendsList.Covert(avatar.Value);
				avatarImage.sprite = Sprite.Create(avatarTex, new Rect(0, 0, avatarTex.width, avatarTex.height), new Vector2(0.5f, 0.5f), 100f);
				Logger.instance.Log($"Got avatar for friend: {newFriend.Name}");
			}
			else
			{
				avatarImage.sprite = FriendsList.instance.noAvatarSprite;
				Logger.instance.Log($"Failed to get avatar for friend: {newFriend.Name} :(");
			}
		}
		SetPlayerReady(isReady);
    }
	
	public void SetPlayerVisibility(bool visible)
	{
		inLobbyVisibilityObject.SetActive(visible);
		noPlayerVisibilityObject.SetActive(!visible);
	}
	
	public void SetPlayerReady(bool ready)
	{
		readyStatus = ready;
		if(ready)
		{
			backdropImage.color = LobbyUI.instance.playerReadyColor;
		}
		else
		{
			backdropImage.color = LobbyUI.instance.playerNotReadyColor;
		}
	}
	
	public bool IsOpen()
	{
        if (inLobbyVisibilityObject.activeSelf)
		{
			return false;
		}
		if(noPlayerVisibilityObject.activeSelf)
		{
			return true;
		}
		Logger.instance.Error("Lobby member not open or occupied");
		return true;
	}
	
	public void CopyOtherLobbyMember(LobbyMember lobbyMemberToCopy)
	{
		if(lobbyMemberToCopy.friend.HasValue)
		{
			friend = lobbyMemberToCopy.friend.Value;
			avatarImage.sprite = lobbyMemberToCopy.avatarImage.sprite;
			nameLabel.ChangeText(lobbyMemberToCopy.nameLabel.GetText());
			SetPlayerReady(lobbyMemberToCopy.readyStatus);
			SetPlayerVisibility(true);
		}
		else
		{
			Nullify();
		}
	}

	public void Nullify()
	{
		SetPlayerVisibility(false);
		friend = null;
		readyStatus = false;
	}
}
