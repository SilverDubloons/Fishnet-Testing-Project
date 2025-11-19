using UnityEngine;
using Steamworks.Data;
using Steamworks;
using System.Threading.Tasks;

public class FriendInteractionPanel : MonoBehaviour
{
	[SerializeField] private GameObject visibilityObject;
	[SerializeField] private ButtonPlus joinButton;
	[SerializeField] private ButtonPlus inviteButton;
	
	private Friend selectedFriend;
	
    public static FriendInteractionPanel instance;
	
	void Awake()
	{
		instance = this;
	}
	
	void Start()
	{
		visibilityObject.SetActive(false);
	}
	
	public void OpenPanel(Friend friend)
	{
		visibilityObject.SetActive(true);
		selectedFriend = friend;
		// if friend in this game lobby not searching join is active
		Friend.FriendGameInfo? gameInfo = friend.GameInfo;
		if(friend.IsPlayingThisGame && gameInfo.HasValue && gameInfo.Value.Lobby.HasValue && gameInfo.Value.Lobby.Value.MemberCount < gameInfo.Value.Lobby.Value.MaxMembers)
		{
			joinButton.ChangeButtonEnabled(true);
		}
		else
		{
			joinButton.ChangeButtonEnabled(false);
		}
		// if in lobby and not searching invite is active
		
		// chat always active
	}
	
	public async void Click_Join()
	{
		try
		{
			await NetworkInterface.instance.JoinSteamLobbyFromFriend(selectedFriend);
			visibilityObject.SetActive(false);
		}
		catch (System.Exception e)
		{
			Logger.instance.Log($"Error joining friend's lobby: {e.Message}");
        }
    }
	
	public void Click_Invite()
	{
		selectedFriend.InviteToGame("Come on in big guy");
        visibilityObject.SetActive(false);
	}
	
	public void Click_Chat()
	{
		SteamFriends.OpenUserOverlay(selectedFriend.Id, "chat");
		visibilityObject.SetActive(false);
	}
	
	
}
