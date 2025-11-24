using FishyFacepunch;
using Steamworks;
using Steamworks.Data;
using System;
using System.Threading.Tasks;

public static class SteamLobbyExtensions
{
    public static async Task RefreshAsync(this Lobby lobby)
    {
        TaskCompletionSource<bool> resultWaiter = new();

        void handler(Lobby changedLobby)
        {
            if (changedLobby.Id == lobby.Id)
                resultWaiter.TrySetResult(true);
        }

        SteamMatchmaking.OnLobbyDataChanged += handler;

        lobby.Refresh();

        await resultWaiter.Task;

        SteamMatchmaking.OnLobbyDataChanged -= handler;
    }
}