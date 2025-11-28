using Steamworks;
using Steamworks.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using static CachedLobbyData;

public class LobbyUI : MonoBehaviour
{
    [SerializeField] private GameObject visibilityObject;
	[SerializeField] private LobbyMember[] lobbyMembers;
	[SerializeField] private ButtonPlus leaveLobbyButton;
	[SerializeField] private UnityEngine.UI.Toggle readyToggle;
	[SerializeField] private Label lobbyDataLabel;
    public UnityEngine.Color playerReadyColor;
	public UnityEngine.Color playerNotReadyColor;
	
	public static LobbyUI instance;
	
	void Awake()
	{
		instance = this;
	}
	void Update()
	{
		if (Keyboard.current.qKey.wasPressedThisFrame)
		{
			Lobby? currentPartyLobby = NetworkInterface.instance.GetCurrentPartyLobby();
			if (currentPartyLobby.HasValue)
			{
				UpdateLobbyDataString(currentPartyLobby.Value);
			}
			else
			{
                lobbyDataLabel.ChangeText("No current party lobby");
			}
		}
		if (Keyboard.current.wKey.wasPressedThisFrame)
		{
			Lobby? currentMatchmakingLobby = NetworkInterface.instance.GetCurrentMatchmakingLobby();
			if (currentMatchmakingLobby.HasValue)
			{
				UpdateLobbyDataString(currentMatchmakingLobby.Value);
			}
			else
			{
                lobbyDataLabel.ChangeText("No current matchmaking lobby");
			}
		}
	}
	public void JoinLobby(Lobby? lobby)
	{
		if(lobby.HasValue && lobby.Value.Id.IsValid)
		{
			Friend[] friendsInLobby = lobby.Value.Members.ToArray();
			for(int i = 0; i < friendsInLobby.Length; i++)
			{
				LobbyMemberJoined(friendsInLobby[i], lobby.Value);
			}
		}
	}
	
	void Start()
	{
		SetupLocalLobbyMember();
		// SetCanLeaveLobby(false);
    }
	
	private async void SetupLocalLobbyMember()
	{
		Image? avatar = await FriendsList.GetAvatar(SteamClient.SteamId);
		if(avatar.HasValue)
		{
			Texture2D avatarTex = FriendsList.Covert(avatar.Value);
			lobbyMembers[0].avatarImage.sprite = Sprite.Create(avatarTex, new Rect(0, 0, avatarTex.width, avatarTex.height), new Vector2(0.5f, 0.5f), 100f);
		}
		else
		{
			lobbyMembers[0].avatarImage.sprite = FriendsList.instance.noAvatarSprite;
		}
		lobbyMembers[0].SetPlayerVisibility(true);
		lobbyMembers[0].nameLabel.ChangeText(SteamClient.Name);
		lobbyMembers[0].friend = new Friend(SteamClient.SteamId);
	}
	
	public void LobbyMemberJoined(Friend friend, Lobby lobby)
	{
		for(int i = 0; i < lobbyMembers.Length; i++)
		{   // this probably only needs to check lobbyMembers[0] for the player themselves,
            // but this can't hurt in case steam sends duplicate join events
            if (lobbyMembers[i].friend.HasValue && lobbyMembers[i].friend.Value.Id == friend.Id)
			{
				return;
			}
		}
		for(int i = 1; i < lobbyMembers.Length; i++)
		{
            if (lobbyMembers[i].IsOpen())
			{
				if (Enum.TryParse(lobby.GetMemberData(friend, LobbyKeys.Ready), out ReadyState readyState))
				{
					lobbyMembers[i].SetupLobbyMember(friend, readyState == ReadyState.Ready);
				}
				else
				{
					lobbyMembers[i].SetupLobbyMember(friend, false);
                }
                return;
            }
		}
		Logger.instance.Error("Lobby member joined with no slots open in UI");
	}
	public void LobbyMemberLeft(Friend friend)
	{
		bool moveRemainingMembersUp = false;
		for(int i = 1; i < lobbyMembers.Length; i++)
		{
			if(moveRemainingMembersUp)
			{
				if(lobbyMembers[i].friend.HasValue)
				{
					lobbyMembers[i - 1].CopyOtherLobbyMember(lobbyMembers[i]);
					lobbyMembers[i].Nullify();
				}
				else
				{
					return;
				}
			}
			if(lobbyMembers[i].friend.HasValue && lobbyMembers[i].friend.Value.Id == friend.Id)
			{
				lobbyMembers[i].Nullify();
				moveRemainingMembersUp = true;
			}
		}
	}
	
	public void LeaveCurrentPartyLobby()
	{
		for(int i = 1; i < lobbyMembers.Length; i++)
		{
			lobbyMembers[i].Nullify();
		}
		SetCanLeaveLobby(false);
    }

	public async void Click_LeaveLobby()
	{
		NetworkInterface.instance.LeaveCurrentPartyLobby();
		if (readyToggle.isOn)
		{
			readyToggle.isOn = false;
			ReadyStateChanged();
		}
		SetCanLeaveLobby(false);
        await NetworkInterface.instance.StartSteamPartyLobby();
    }
	public void SetCanLeaveLobby(bool canLeave)
	{
		if(leaveLobbyButton.buttonEnabled == canLeave)
		{
			return;
		}
		leaveLobbyButton.ChangeButtonEnabled(canLeave);
		// Logger.instance.Log($"SetCanLeaveLobby: {canLeave}");
    }

    public void SetCanChangeReadyState(bool canChangeReadyState)
    {
		if (readyToggle.interactable == canChangeReadyState)
		{
			return;
		}
        readyToggle.interactable= canChangeReadyState;
		// Logger.instance.Log($"SetCanChangeReadyState: {canChangeReadyState}");
    }
	public void SetReadyState(bool isReady)
	{
		if(readyToggle.isOn != isReady)
		{
			readyToggle.isOn = isReady;
		}
	}
	public void ReadyStateChanged()
	{
		NetworkInterface.instance.SetPlayerReady(readyToggle.isOn);
		Logger.instance.Log($"Player ready state changed to: {readyToggle.isOn}");
    }

	public void UpdateLobbyMemberReadyStatus(Friend friend, bool isReady)
	{
		// Logger.instance.Log($"UpdateLobbyMemberReadyStatus called for {friend.Name} to {isReady} lobbyMembers.Length: {lobbyMembers.Length}");
        for (int i = 0; i < lobbyMembers.Length; i++)
		{
			if(lobbyMembers[i].friend.HasValue && lobbyMembers[i].friend.Value.Id == friend.Id)
			{
				if(lobbyMembers[i].readyStatus == isReady)
				{
					return;
				}
				lobbyMembers[i].SetPlayerReady(isReady);
				Logger.instance.Log($"Updated ready status for {friend.Name} to {isReady}");
                return;
			}
		}
	}


    public void LobbyUpdated(Lobby lobby)
	{
		UpdateLobbyDataString(lobby);
        if ((Enum.TryParse(lobby.GetData(LobbyKeys.LobbyState), out LobbyState lobbyState) && lobbyState == LobbyState.GameStarting) || lobby.MemberCount == 1)
		{
            SetCanLeaveLobby(false);
        }
		else
		{
            SetCanLeaveLobby(true);
        }
		if (lobbyState == LobbyState.GameStarting)
		{
			SetCanChangeReadyState(false);
		}
		else
		{
            SetCanChangeReadyState(true);
        }
    }
    public bool GetPlayerReady()
    {
        return readyToggle.isOn;
    }
	private void UpdateLobbyDataString(Lobby lobby)
	{
        string lobbyDataString = "Lobby Data:\n";
        lobbyDataString += $"Id : {lobby.Id}\n";
        lobbyDataString += $"Owner : {lobby.Owner.Name}\n";
        foreach (var entry in lobby.Data)
        {
            lobbyDataString += $"{entry.Key} : {entry.Value}\n";
        }
        lobbyDataString += $"{lobby.MemberCount}/{lobby.MaxMembers}\n";
        Friend[] friendsInLobby = lobby.Members.ToArray();
        foreach (var friend in friendsInLobby)
        {
            string readyStatus = lobby.GetMemberData(friend, LobbyKeys.Ready);
            lobbyDataString += $"{friend.Name} : {readyStatus}\n";
        }
		lobbyDataString += "\nMMLobby:" + NetworkInterface.instance.GetMatchmakingLobbyIdString();
        lobbyDataLabel.ChangeText(lobbyDataString);
    }
}
