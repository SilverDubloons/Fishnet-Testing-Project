using Steamworks;
using Steamworks.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "CachedLobbyData", menuName = "Scriptable Objects/CachedLobbyData")]
public class CachedLobbyData : ScriptableObject
{
    [System.Serializable]
    public class LobbyMember
    {
        public Friend friend;
        public ReadyState readyState;

        public LobbyMember(Friend friend, ReadyState readyState)
        {
            this.friend = friend;
            this.readyState = readyState;
        }
    }

    [Header("Lobby Metadata")]
    public Friend lobbyOwner;
    public LobbyState lobbyState;
    public LobbyType lobbyType;
    public SteamId lobbyId;
    public bool log;
    public string matchmakingValue;
    public string hostId;

    [Header("Members")]
    [NonSerialized]
    public Dictionary<SteamId, LobbyMember> members = new();

    public static class LobbyKeys
    {
        // Member keys
        public const string Ready = "Ready";

        // Lobby keys
        public const string LobbyState = "LobbyState";
        public const string LobbyType = "LobbyType";
        public const string HostSteamId = "HostSteamId";
        public const string JoinableState = "JoinableState";
        public const string MatchmakingLobby = "MM";

        // Special chat keys
        public const string KickMember = "KickMember";
    }
    public void SetupFromNewLobby(Lobby lobby)
    {
        lobbyOwner = lobby.Owner;
        if (TryGetEnum(lobby, LobbyKeys.LobbyState, out LobbyState parsedState))
        {
            lobbyState = parsedState;
        }
        else
        {
            lobbyState = LobbyState.Unset;
        }
        if (TryGetEnum(lobby, LobbyKeys.LobbyType, out LobbyType parsedType))
        {
            lobbyType = parsedType;
        }
        else
        {
            lobbyType = LobbyType.Unset;
        }
        Friend[] friendsInLobby = lobby.Members.ToArray();
        foreach (var friend in friendsInLobby)
        {
            if (TryGetEnum(lobby, LobbyKeys.Ready, out ReadyState readyState))
            {
                members[friend.Id] = new LobbyMember(friend, readyState);
            }
            else
            {
                members[friend.Id] = new LobbyMember(friend, ReadyState.Unset);
            }
        }
        lobbyId = lobby.Id;
        matchmakingValue = null;
    }
    public LobbyOwnerChange GetLobbyOwnerChange(Lobby lobby)
    {
        Logger.instance.Log("GetLobbyOwnerChange", 10);
        if (lobby.Owner.Id == lobbyOwner.Id)
            return default;

        Friend oldOwner = lobbyOwner;
        lobbyOwner = lobby.Owner;
        return new LobbyOwnerChange(true, oldOwner, lobbyOwner);
    }
    public LobbyStateChange GetLobbyStateChange(Lobby lobby)
    {
        string stateString = lobby.GetData(LobbyKeys.LobbyState);
        Logger.instance.Log($"GetLobbyStateChange 0 lobbyState: {lobbyState} stateString: {stateString}", 10);
        LobbyState parsedState;
        if (stateString.Length >= LobbyState.Join.ToString().Length && stateString.StartsWith(LobbyState.Join.ToString()))
        {
            parsedState = LobbyState.Join;
        }
        else
        {
            if (!Enum.TryParse(stateString, out parsedState))
                return default;
        }
        Logger.instance.Log("GetLobbyStateChange 1", 10);

        if (parsedState == lobbyState)
            return default;

        Logger.instance.Log("GetLobbyStateChange 2", 10);

        LobbyState oldState = lobbyState;
        lobbyState = parsedState;
        return new LobbyStateChange(true, oldState, lobbyState);
    }
    public MatchmakingStateChange GetMatchmakingStateChange(Lobby lobby)
    {
        string mmString = lobby.GetData(LobbyKeys.MatchmakingLobby);
        Logger.instance.Log($"GetMatchmakingStateChange 0 matchmakingValue: {matchmakingValue} mmString: {mmString}", 10);
        if (mmString == matchmakingValue)
            return default;
        Logger.instance.Log("GetMatchmakingStateChange 1", 10);
        string oldValue = matchmakingValue;
        matchmakingValue = mmString;
        return new MatchmakingStateChange(true, oldValue, matchmakingValue);
    }
    public HostIdChange GetHostIdChange(Lobby lobby)
    {
        string hostIdString = lobby.GetData(LobbyKeys.HostSteamId);
        Logger.instance.Log($"GetHostIdChange 0 hostIdString: {hostIdString}", 10);
        if (string.IsNullOrEmpty(hostIdString) && string.IsNullOrEmpty(hostId))
            return default;
        if (hostId == hostIdString)
            return default;
        Logger.instance.Log("GetHostIdChange 1", 10);
        string oldHostId = hostId;
        hostId = hostIdString;
        return new HostIdChange(true, oldHostId, hostId);
    }
    public LobbyTypeChange GetLobbyTypeChange(Lobby lobby)
    {
        Logger.instance.Log($"GetLobbyTypeChange 0 lobbyType: {lobbyType} LobbyTypeData: {lobby.GetData(LobbyKeys.LobbyType)}", 10);
        if (!TryGetEnum(lobby, LobbyKeys.LobbyType, out LobbyType parsedType))
            return default;

        Logger.instance.Log("GetLobbyTypeChange 1", 10);

        if (parsedType == lobbyType)
            return default;

        Logger.instance.Log("GetLobbyTypeChange 2", 10);

        LobbyType oldType = lobbyType;
        lobbyType = parsedType;
        return new LobbyTypeChange(true, oldType, lobbyType);
    }
    public LobbyChanges GetLobbyChanges(Lobby lobby)
    {
        Logger.instance.Log("GetLobbyChanges", 20);
        return new LobbyChanges()
        {
            ownerChange = GetLobbyOwnerChange(lobby),
            stateChange = GetLobbyStateChange(lobby),
            typeChange = GetLobbyTypeChange(lobby),
            matchmakingChange = GetMatchmakingStateChange(lobby),
            hostIdChange = GetHostIdChange(lobby)
        };
    }
    public ReadyStateChange GetReadyStateChange(Lobby lobby, Friend friend)
    {
        string readyString = lobby.GetMemberData(friend, LobbyKeys.Ready);
        if (!Enum.TryParse(readyString, out ReadyState readyState))
            return default;

        if (members.TryGetValue(friend.Id, out LobbyMember member))
        {
            if (readyState != member.readyState)
            {
                ReadyState oldState = member.readyState;
                member.readyState = readyState;
                return new ReadyStateChange(true, oldState, member.readyState);
            }
        }
        Logger.instance.Error($"Detected Ready State Change for friend who is not in lobby. Friend: {friend.Name}, friendId = {friend.Id} lobby: {lobby.Id}");
        return default;
    }
    public void OnLobbyMemberJoined(Friend friend, Lobby lobby)
    {
        string readyString = lobby.GetMemberData(friend, LobbyKeys.Ready);
        if (Enum.TryParse(readyString, out ReadyState readyState))
        {
            members[friend.Id] = new LobbyMember(friend, readyState);
        }
        else
        {
            members[friend.Id] = new LobbyMember(friend, ReadyState.Unset);
        }
    }
    public void OnLobbyMemberLeave(Lobby lobby, Friend friend)
    {
        members.Remove(friend.Id);
    }
    public void Reset()
    {
        lobbyOwner = default;
        lobbyState = LobbyState.Unset;
        lobbyType = LobbyType.Unset;
        members.Clear();
    }
    private bool TryGetEnum<T>(Lobby lobby, string key, out T result) where T : struct
    {
        string value = lobby.GetData(key);
        return Enum.TryParse(value, out result);
    }
}
public readonly struct LobbyOwnerChange
{
    public bool LobbyOwnerChanged { get; }
    public Friend OldOwner { get; }
    public Friend NewOwner { get; }
    public LobbyOwnerChange(bool changed, Friend oldOwner, Friend newOwner)
    {
        LobbyOwnerChanged = changed;
        OldOwner = oldOwner;
        NewOwner = newOwner;
    }
}
public readonly struct LobbyStateChange
{
    public bool LobbyStateChanged { get; }
    public LobbyState OldState { get; }
    public LobbyState NewState { get; }
    public LobbyStateChange(bool changed, LobbyState oldState, LobbyState newState)
    {
        LobbyStateChanged = changed;
        OldState = oldState;
        NewState = newState;
    }
}
public readonly struct LobbyTypeChange
{
    public bool LobbyTypeChanged { get; }
    public LobbyType OldType { get; }
    public LobbyType NewType { get; }
    public LobbyTypeChange(bool changed, LobbyType oldType, LobbyType newType)
    {
        LobbyTypeChanged = changed;
        OldType = oldType;
        NewType = newType;
    }
}
public readonly struct ReadyStateChange
{
    public bool ReadyStateChanged { get; }
    public ReadyState OldState { get; }
    public ReadyState NewState { get; }
    public ReadyStateChange(bool changed, ReadyState oldState, ReadyState newState)
    {
        ReadyStateChanged = changed;
        OldState = oldState;
        NewState = newState;
    }
}
public readonly struct MatchmakingStateChange
{
    public bool MatchmakingStateChanged { get; }
    public string OldState { get; }
    public string NewState { get; }
    public MatchmakingStateChange(bool changed, string oldState, string newState)
    {
        MatchmakingStateChanged = changed;
        OldState = oldState;
        NewState = newState;
    }
}
public readonly struct HostIdChange
{
    public bool HostIdChanged { get; }
    public string OldHostId { get; }
    public string NewHostId { get; }
    public HostIdChange(bool changed, string oldHostId, string newHostId)
    {
        HostIdChanged = changed;
        OldHostId = oldHostId;
        NewHostId = newHostId;
    }
}
public struct LobbyChanges
{
    public LobbyOwnerChange ownerChange;
    public LobbyStateChange stateChange;
    public LobbyTypeChange typeChange;
    public MatchmakingStateChange matchmakingChange;
    public HostIdChange hostIdChange;
    public readonly bool Any => ownerChange.LobbyOwnerChanged || stateChange.LobbyStateChanged || typeChange.LobbyTypeChanged || matchmakingChange.MatchmakingStateChanged || hostIdChange.HostIdChanged;
}
public enum LobbyState { Unset, WaitingForReady, SearchingForGame, GameStarting, Join, Matchmaking }
public enum LobbyType { Unset, Public, Private, FriendsOnly, Invisible }
public enum ReadyState { Unset, Ready, NotReady }
public enum JoinableState { Unset, Joinable, NotJoinable }