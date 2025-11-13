using UnityEngine;
using Steamworks;
using Steamworks.Data;
using System.Threading.Tasks;
using System;

public class LobbyMember : MonoBehaviour
{
    public RectTransform rt;
	[SerializeField] private UnityEngine.UI.Image avatarImage;
	[SerializeField] private Label nameLabel;
	
	public Friend friend;
	
	public async void SetupLobbyMember(Friend newFriend)
	{
		
	}
}
