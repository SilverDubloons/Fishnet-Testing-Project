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
	
	public async void OpenPanel(Friend friend)
	{
		visibilityObject.SetActive(true);
		selectedFriend = friend;
        // if friend in this game lobby not searching join is active
        // if(friend.IsPlayingThisGame && gameInfo.HasValue && gameInfo.Value.Lobby.HasValue && gameInfo.Value.Lobby.Value.MemberCount < gameInfo.Value.Lobby.Value.MaxMembers)
        // if in lobby and not searching invite is active
        Logger.instance.Log($"Checking if can invite {friend.Name} to current lobby");
        if (NetworkInterface.instance.CurrentPartyLobbyCanBeJoinedByFriends())
        {
            inviteButton.ChangeButtonEnabled(true);
        }
        else
        {
            inviteButton.ChangeButtonEnabled(false);
        }
        joinButton.ChangeButtonEnabled(false);
        Logger.instance.Log($"Checking if can join friend {friend.Name}'s lobby");
        Lobby? lobby = await NetworkInterface.instance.GetLobbyFromFriendIfJoinable(friend);
		if(lobby.HasValue)
        {
			joinButton.ChangeButtonEnabled(true);
		}
		else
		{
			joinButton.ChangeButtonEnabled(false);
		}
		
		// chat always active
	}
	
	public async void Click_Join()
	{
		try
		{
			await NetworkInterface.instance.JoinSteamPartyLobbyFromFriend(selectedFriend);
			visibilityObject.SetActive(false);
		}
		catch (System.Exception e)
		{
			Logger.instance.Error($"Error joining friend's lobby: {e.Message}");
        }
    }
	
	public void Click_Invite()
	{

		string lobbyJoinString = NetworkInterface.instance.GetPartyLobbyIdStringForConnection();
		if (string.IsNullOrEmpty(lobbyJoinString))
		{ 
			Logger.instance.Warning("No current lobby to invite friend to, lobby is full, or unjoinable");
            visibilityObject.SetActive(false);
            return;
        }
        selectedFriend.InviteToGame(lobbyJoinString);
        visibilityObject.SetActive(false);
	}
	
	public void Click_Chat()
	{
		SteamFriends.OpenUserOverlay(selectedFriend.Id, "chat");
		visibilityObject.SetActive(false);
	}
	
	
}
