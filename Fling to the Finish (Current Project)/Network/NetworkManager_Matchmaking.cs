using ExitGames.Client.Photon;
using Fling.Saves;
using Menus;
using Photon.Pun;
using Photon.Realtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public partial class NetworkManager
{
    private static readonly List<string> REGIONS_ARRAY = new List<string>()
    {
        "asia", "au", "cae", "eu", "in", "jp", "ru", "rue", "za", "sa", "kr", "tr", "us", "usw"
    };

    private static readonly int[][] REGION_DISTANCES_MATRIX = new int[][]
    {
        new int[] { -1, 3, 12, 9, 0, 2, 5, 4, 6, 10, 1, 7, 11, 8 },
        new int[] { 0, -1, 8, 12, 4, 2, 11, 3, 9, 6, 1, 10, 7, 5 },
        new int[] { 9, 8, -1, 2, 12, 7, 4, 11, 6, 3, 10, 5, 0, 1 },
        new int[] {8,9,5,-1,4,12,1,10,2,3,11,0,6,7 },
        new int[] {0,3,12,9,-1,2,5,4,6,10,1,7,11,8 },
        new int[] {2,3,7,11,5,-1,8,1,10,12,0,9,6,4 },
        new int[] {4,8,10,1,2,7,-1,5,3,9,6,0,11,12 },
        new int[] {2,3,7,11,5,1,8,-1,10,12,0,9,6,4 },
        new int[] {4,8,7,2,3,11,1,10,-1,5,9,0,6,12 },
        new int[] {8,9,2,4,7,10,6,12,0,-1,11,5,1,3 },
        new int[] {2,3,7,11,5,0,8,1,10,12,-1,9,6,4 },
        new int[] {4,9,10,1,2,8,0,7,3,5,6,-1,11,12 },
        new int[] {11,5,0,3,12,4,8,7,10,2,6,9,-1,1 },
        new int[] {7,5,1,9,8,2,10,6,12,3,4,11,0,-1 }
    };

    private string baseRegion = "";
    private int currentMatchmakingRegionIndex = -1;
    private int currentMatchmakingRegionConnectAttempt = 0;
    private bool lastMatchmakingRegionConnectSuccess = false;

    public MenuData.PlayType CurrentMatchmakingType => MenuData.MainMenuData.PlayType;
    public bool IsLookingForBothTypesOfMatchmaking { get; private set; } = false;

    private const int MAX_MATCHMAKING_REGION_CONNECT_ATTEMPTS = 3;

    private int currentMatchmakingCreateRoomAttempt = 0;
    private bool shouldCreateMatchmakingRoom = false;
    private bool shouldRestartRegionMatchSearchAfterSomeTime = false;
    private const int MAX_MATCHMAKING_CREATE_ROOM_ATTEMPTS = 3;

    private bool disconnectOnConnectedWhenMatchmakingStopped = false;
    private Coroutine restartRegionSearchLoopCoroutine = null;

    [SerializeField] private float restartRegionSearchForMatchTimer = 30f;

    #region Matchmaking specific properties
    //private List<List<string>> matchmakingQueueTeamClientIDs;

    /// <summary>
    /// Local variable, not synced online.
    /// The global MMQ index at which this party's first team indices start
    /// </summary>
    //public int StartingTeamIndexForMyParty { get; private set; }

    private bool shouldSearchInOtherRegions = false;
    //private bool shouldCorrectTeamIDs = false;

    private string currentlySearchingForMMInRegion = "";
    #endregion

    #region Create/Join Room
    /// <summary>
    /// Creates a matchmaking room
    /// </summary>
    public void CreateMatchmakingRoom()
    {
        Debug.Log("Creating a new matchmaking room...");

        int menuState = (int)MenuState.NONE;
        if (MetaManager.Instance.IsInMainMenu && MenuManager.Instance != null)
        {
            menuState = (int)MenuManager.Instance.CurrentMenuState;
        }
        int playType = (int)MenuData.MainMenuData.PlayType;

        RoomOptions roomOptions = new RoomOptions();
        roomOptions.CustomRoomProperties = new Hashtable { 
            { MATCHMAKING_FILTER_TEAM_NUMBER, 0 }, 
            { ROOM_PROPERTY_MENU_STATE, menuState },
            { ROOM_PROPERTY_PLAY_TYPE, playType },
            { ROOM_PROPERTY_IS_MATCHMAKING_ROOM, true }
        };
        roomOptions.CustomRoomPropertiesForLobby = new[] { MATCHMAKING_FILTER_TEAM_NUMBER, ROOM_PROPERTY_PLAY_TYPE };
        roomOptions.PublishUserId = true;
        roomOptions.IsVisible = true;
        roomOptions.IsOpen = true;
        roomOptions.MaxPlayers = CurrentMatchmakingType == MenuData.PlayType.Campaign? MAX_CLIENTS_CAMPAIGN : MAX_CLIENTS;
        roomOptions.EmptyRoomTtl = 0;
        roomOptions.PlayerTtl = 0;
        roomOptions.CleanupCacheOnLeave = false;

        string rmName = GenerateNewRoomCode();

        bool didCreateRoomRequestGoThrough = PhotonNetwork.CreateRoom(rmName, roomOptions, MATCHMAKING_SQL_LOBBY, PartyUserIds.ToArray());
        if (!didCreateRoomRequestGoThrough)
        {
            JoinOrCreateRoomFailed(ErrorCodes.PHOTON_ERROR, "");
        }
    }

    /// <summary>
    /// Joins a random available matchmaking room according to current criteria
    /// </summary>
    private void JoinRandomMatchmakingRoom()
    {
        Debug.Log("Now looking for a random room...");
        Debug.Log("ROOMS: " + PhotonNetwork.CountOfRooms);
        int possibleExtraTeams = MAX_TEAMS_ALLOWED - 0; // these many teams can be already present in the matchmaking room we're finding
        string sqlFilter = ROOM_PROPERTY_PLAY_TYPE + " = " + (int)CurrentMatchmakingType;
        byte maxClients = MAX_CLIENTS_CAMPAIGN;
        if (CurrentMatchmakingType == MenuData.PlayType.Race)
        {
            // in a race we still want to keep the CurrentMatchmakingType as a search filter, so we use "AND" to append this extra filter
            sqlFilter += " AND " + MATCHMAKING_FILTER_TEAM_NUMBER + " <= " + possibleExtraTeams;
            maxClients = MAX_CLIENTS;
        }
        if (IsLookingForBothTypesOfMatchmaking)
        {
            // when finding BOTH types of matchmaking, we get rid of the matchmaking type filter
            sqlFilter = MATCHMAKING_FILTER_TEAM_NUMBER + " <= " + possibleExtraTeams;
            maxClients = MAX_CLIENTS;
        }
        PhotonNetwork.JoinRandomRoom(null, maxClients, MatchmakingMode.FillRoom, MATCHMAKING_SQL_LOBBY, sqlFilter, PartyUserIds.ToArray());
    }

    /// <summary>
    /// Readjusts the current room to be open for the type of matchmaking
    /// 1. If the room is private, makes it public
    /// 2. Adjusts the max number of players allowed per <see cref="CurrentMatchmakingType"/>
    /// 3. Sets the custom room property for lobby for the matchmaking type to be <see cref="CurrentMatchmakingType"/>
    /// </summary>
    private void ReadjustCurrentRoomForMatchmaking()
    {
        RoomOptions roomOptions = new RoomOptions();

        int menuState = (int)MenuState.NONE;
        if (MetaManager.Instance.IsInMainMenu && MenuManager.Instance != null)
        {
            menuState = (int)MenuManager.Instance.CurrentMenuState;
        }
        int playType = (int)MenuData.MainMenuData.PlayType;

        roomOptions.CustomRoomProperties = new Hashtable {
            { MATCHMAKING_FILTER_TEAM_NUMBER, 0 },
            { ROOM_PROPERTY_MENU_STATE, menuState },
            { ROOM_PROPERTY_PLAY_TYPE, playType },
            { ROOM_PROPERTY_IS_MATCHMAKING_ROOM, true }
        };
        roomOptions.CustomRoomPropertiesForLobby = new[] { MATCHMAKING_FILTER_TEAM_NUMBER, ROOM_PROPERTY_PLAY_TYPE };

        RoomType = RoomType.Matchmake;

        PhotonNetwork.CurrentRoom.IsVisible = true;
        PhotonNetwork.CurrentRoom.MaxPlayers = CurrentMatchmakingType == MenuData.PlayType.Campaign ? MAX_CLIENTS_CAMPAIGN : MAX_CLIENTS;
        PhotonNetwork.CurrentRoom.SetCustomProperties(roomOptions.CustomRoomProperties);
        PhotonNetwork.CurrentRoom.SetPropertiesListedInLobby(roomOptions.CustomRoomPropertiesForLobby);
    }
    #endregion

    #region Matchmaking Specific Functions
    /// <summary>
    /// Starts the matchmaking process
    /// </summary>
    public void StartMatchmaking()
    {
        NumberOfTeamsInLocalParty = NumberOfTeams;

        if (PhotonNetwork.OfflineMode)
        {
            if (!SaveManager.Instance.IsUsingChineseServers())
            {
                shouldSearchInOtherRegions = true;
                //shouldCorrectTeamIDs = true;
            }
            else
            {
                shouldSearchInOtherRegions = false;
                //shouldCorrectTeamIDs = false;
            }
            ConnectThenMatchmake();
        }
        else if (PhotonNetwork.IsMasterClient)
        {
            // Before leaving room, tell everyone else to start looking for this client!
            if (!SaveManager.Instance.IsUsingChineseServers())
            {
                shouldSearchInOtherRegions = NumberOfClients == 1;
                //shouldCorrectTeamIDs = shouldSearchInOtherRegions;
            }
            else
            {
                shouldSearchInOtherRegions = false;
                //shouldCorrectTeamIDs = false;
            }

            startedMatchmakingFromOnlineParty = true;
            IsMatchmaking = true;
            IsPartyLeader = true;
            partyLeaderID = PhotonNetwork.LocalPlayer.UserId;

            if (NumberOfClients == 1)
            {
                // this is the only client, can disconnect and search
                PhotonNetwork.LeaveRoom();
            }
            else
            {
                ConvertPrivateRoomToMatchmakingRoom();
                Matchmaking_StartedMatchmakingViaPrivateRoom(PhotonNetwork.LocalPlayer.UserId);
            }
        }
    }

    public void StopMatchmaking(Action onDisconnectedCallback = null)
    {
        IsMatchmaking = false;
        IsLookingForBothTypesOfMatchmaking = false;

        if (isConnecting)
        {
            disconnectOnConnectedWhenMatchmakingStopped = true;
            this.onDisconnectedCallback = onDisconnectedCallback;
        }
        else if (isConnected)
        {
            Disconnect(onDisconnectedCallback);
        }
        else
        {
            onDisconnectedCallback?.Invoke();
        }
    }

    /// <summary>
    /// Converts the current private room to a visible matchmaking room
    /// </summary>
    private void ConvertPrivateRoomToMatchmakingRoom()
    {
        ReadjustCurrentRoomForMatchmaking();
    }

    public void SwitchToSearchForBothMatchmaking()
    {
        if (IsLookingForBothTypesOfMatchmaking)
        {
            return; // already looking for both
        }

        IsLookingForBothTypesOfMatchmaking = true;

        if (PhotonNetwork.CurrentRoom != null && PhotonNetwork.IsMasterClient)  // are we the host?
        {
            // switch current room to become a both matchmaking room
            ReadjustCurrentRoomForMatchmaking();
        }
    }

    private IEnumerator LookForMyLeader(string leaderID)
    {
        startedMatchmakingFromOnlineParty = true;
        IsMatchmaking = true;
        IsLookingForLeader = true;
        partyLeaderID = leaderID;

        while (isConnecting)
        {
            yield return null;
        }

        while (IsLookingForLeader)
        {
            PhotonNetwork.FindFriends(new string[] { leaderID });
            yield return new WaitForSeconds(2f);
            Debug.Log("<color=yellow>Still looking for friend...</color>");
        }

        Debug.Log("<color=green>Friend found!</color>");
    }

    public override void OnFriendListUpdate(List<FriendInfo> friendList)
    {
        base.OnFriendListUpdate(friendList);

        FriendInfo leader;
        for (int i = 0; i < friendList.Count; i++)
        {
            if (friendList[i].UserId.Equals(partyLeaderID))
            {
                leader = friendList[i];
                if (leader.IsInRoom)    // leader has to be in a room
                {
                    ConnectToLeaderRoom(leader);
                }
                break;
            }
        }
    }

    private void ConnectToLeaderRoom(FriendInfo leader)
    {
        if (!IsLookingForLeader)
        {
            return; // already joined the leader
        }

        IsLookingForLeader = false;
        bool didJoinRequestGoThrough = PhotonNetwork.JoinRoom(leader.Room);
        if (!didJoinRequestGoThrough)
        {
            JoinOrCreateRoomFailed?.Invoke(ErrorCodes.PHOTON_ERROR, "Custom Photon Error");
        }
    }

    public event Action<string> OnStartedMatchmakingInRegion;
    private void OnConnectedToMaster_ForMatchmaking()
    {
        if (shouldSearchInOtherRegions)
        {
            currentlySearchingForMMInRegion = PhotonNetwork.CloudRegion;
            if (string.IsNullOrEmpty(baseRegion))
            {
                baseRegion = currentlySearchingForMMInRegion;
                currentMatchmakingRegionIndex = -1;
            }

            lastMatchmakingRegionConnectSuccess = true;
            currentMatchmakingRegionConnectAttempt = 0;
        }
        if (!startedMatchmakingFromOnlineParty)     // started matchmaking while offline
        {
            Matchmaking_FillValuesRequiredInOnlineMode();
        }
        if (!IsLookingForLeader)
        {
            JoinRandomMatchmakingRoom();
        }
        else
        {
            Debug.Log("Starting search for lobby leader...");
        }

        OnStartedMatchmakingInRegion?.Invoke(currentlySearchingForMMInRegion);
    }
    #endregion

    #region Matchmaking Specific Cached Events
    /// <summary>
    /// When player starts matchmaking from an offline lobby, use this to fill required values.
    /// This is not a RaiseEvent function!
    /// </summary>
    private void Matchmaking_FillValuesRequiredInOnlineMode()
    {
        string playerId = PhotonNetwork.LocalPlayer.UserId;

        if (PartyUserIds == null) // came from offline
        {
            PartyUserIds = new List<string>();
        }

        if (!PartyUserIds.Contains(playerId))
        {
            PartyUserIds.Add(playerId);
        }

        roomCode = PhotonNetwork.LocalPlayer.UserId;    // as a failsafe for when it is needed in the if (partyCode == roomCode) check
        partyLeaderID = playerId;
        startedMatchmakingFromOnlineParty = true;
    }

    private void Matchmaking_StartedMatchmakingViaPrivateRoom(string leaderID)
    {
        if (!PhotonNetwork.OfflineMode)
        {
            RaiseEventOptions reo = new RaiseEventOptions { Receivers = ReceiverGroup.Others, CachingOption = EventCaching.AddToRoomCache };
            SendOptions so = new SendOptions { Reliability = true };
            PhotonNetwork.RaiseEvent(MATCHMAKING_PRIVATE_TO_MATCHMAKING_EVCODE, new object[] { leaderID }, reo, so);
        }
    }

    private void Matchmaking_StartLookingForLeaderEvent(EventData eventData)
    {
        if (eventData.Code == MATCHMAKING_PRIVATE_TO_MATCHMAKING_EVCODE)
        {
            object[] content = (object[])eventData.CustomData;
            string leaderID = (string)content[0];

            startedMatchmakingFromOnlineParty = true;
            IsMatchmaking = true;
            IsLookingForLeader = false;
            partyLeaderID = leaderID;
            RoomType = RoomType.Matchmake;
        }
    }

    private void Matchmaking_AddNumberOfTeams(string partyCode, int numberOfTeams)
    {
        return;

        if (!PhotonNetwork.OfflineMode)
        {
            RaiseEventOptions reo = new RaiseEventOptions { Receivers = ReceiverGroup.All, CachingOption = EventCaching.AddToRoomCache };
            SendOptions so = new SendOptions { Reliability = true };
            object[] content = new object[2 + TeamClientIDs.Count];
            content[0] = partyCode;
            content[1] = numberOfTeams;

            // Add TeamClientIDs, one team array after the other because we can't send multi-dimensional arrays or lists through this
            for (int i = 0; i < TeamClientIDs.Count; i++)
            {
                content[i + 2] = TeamClientIDs[i].ToArray();
                Debug.Log(i + 2 + " , " + ((string[])content[i + 2])[0] + " , " + ((string[])content[i + 2])[1]);
            }
            PhotonNetwork.RaiseEvent(MATCHMAKING_ADD_NUM_OF_TEAMS_EVCODE, content, reo, so);
        }
    }

    public delegate void Matchmaking_NumberOfTeamsUpdatedDelegate();
    public event Matchmaking_NumberOfTeamsUpdatedDelegate Matchmaking_NumberOfTeamsUpdated;

    private void Matchmaking_AddNumberOfTeamsEvent(EventData eventData)
    {
        if (eventData.Code == MATCHMAKING_ADD_NUM_OF_TEAMS_EVCODE)
        {
            object[] content = (object[])eventData.CustomData;
            string partyCode = (string)content[0];
            int numToAdd = (int)content[1];
            List<List<string>> teamClientIDs = new List<List<string>>();
            for (int i = 2; i < content.Length; i++)
            {
                teamClientIDs.Add(new List<string>((string[])content[i]));
            }
            //matchmakingQueueTeamClientIDs.AddRange(teamClientIDs);

            if (!string.IsNullOrEmpty(partyCode) && partyCode != roomCode)
            {
                NumberOfTeams += numToAdd;

                if (PhotonNetwork.IsMasterClient)
                {
                    if (NumberOfTeams >= MAX_TEAMS_ALLOWED)
                    {
                        HideRoom();
                        CheckIfAllMatchmakingPlayersHaveJoined();
                    }

                    Hashtable customProps = new Hashtable();
                    customProps.Add(MATCHMAKING_FILTER_TEAM_NUMBER, NumberOfTeams);
                    PhotonNetwork.CurrentRoom.SetCustomProperties(customProps);
                }

                if (Matchmaking_NumberOfTeamsUpdated != null)
                {
                    Matchmaking_NumberOfTeamsUpdated();
                }
            }

            if (partyCode == roomCode)
            {
                //StartingTeamIndexForMyParty = matchmakingQueueTeamClientIDs.IndexOf(teamClientIDs[0]);
            }
        }
    }

    public event Action OnAllExpectedMatchmakingPlayersJoined;
    /// <summary>
    /// Checks whether all players in a matchmaking lobby have joined the room and a matchmaking level can be loaded.
    /// For private parties of 2+ clients, this ensures that not only the host of that private lobby, but the others also join.
    /// </summary>
    public void CheckIfAllMatchmakingPlayersHaveJoined()
    {
        int totalCurrentClientsInRoom = PhotonNetwork.CurrentRoom.PlayerCount;
        int totalUniqueExpectedClients = 2;

        if (CurrentMatchmakingType == MenuData.PlayType.Campaign)
        {
            totalUniqueExpectedClients = MAX_CLIENTS_CAMPAIGN;
        }
        else
        {
            totalUniqueExpectedClients = MAX_CLIENTS;

            //if (NumberOfTeams < MAX_TEAMS_ALLOWED)
            //{
            //    return; // not all teams formed yet
            //}

            //List<string> clients = new List<string>();
            //for (int i = 0; i < matchmakingQueueTeamClientIDs.Count; i++)
            //{
            //    string clientId = matchmakingQueueTeamClientIDs[i][0];
            //    if (!string.IsNullOrEmpty(clientId) && !clientId.Contains(DISCONNECTED_TEAM_CLIENT_ID) && !clients.Contains(clientId))
            //    {
            //        clients.Add(clientId);
            //    }

            //    clientId = matchmakingQueueTeamClientIDs[i][1];
            //    if (!string.IsNullOrEmpty(clientId) && !clientId.Contains(DISCONNECTED_TEAM_CLIENT_ID) && !clients.Contains(clientId))
            //    {
            //        clients.Add(clientId);
            //    }
            //}

            //totalUniqueExpectedClients = clients.Count;
        }


        if (totalUniqueExpectedClients == totalCurrentClientsInRoom)
        {
            CloseRoom();
            OnAllExpectedMatchmakingPlayersJoined?.Invoke();
        }
    }

    public void Matchmaking_MatchStarted()
    {
        if (restartRegionSearchLoopCoroutine != null)
        {
            StopCoroutine(restartRegionSearchLoopCoroutine);
            restartRegionSearchLoopCoroutine = null;
        }
    }

    private void TournamentRaceFinished(bool finished)
    {
        int maxLvlIdx = MetaManager.Instance.allPlayableLevels.Count - 1;
        int curLvlIdx = MetaManager.Instance.allPlayableLevels.IndexOf(MetaManager.Instance.CurrentLevel);
        List<string> clients = new List<string>(LevelsNotPlayedByEachClient.Keys.ToList());
        foreach (string clientId in clients)
        {
            if (LevelsNotPlayedByEachClient[clientId].Contains(curLvlIdx))
            {
                LevelsNotPlayedByEachClient[clientId].Remove(curLvlIdx);
            }
        }
        
        UpdateHighestPlayedRaceLevelIndex();
    }

    public void Matchmaking_JoinedRoom()
    {
        if (shouldRestartRegionMatchSearchAfterSomeTime)
        {
            // if this is a solo client that can loop and search for players in other regions --> start a 30 second timer to start searching for players again
            if (restartRegionSearchLoopCoroutine != null)
            {
                StopCoroutine(restartRegionSearchLoopCoroutine);
            }
            shouldRestartRegionMatchSearchAfterSomeTime = false;

            restartRegionSearchLoopCoroutine = StartCoroutine(Matchmaking_RestartRegionSearchForMatch());
        }
    }

    public void SetTeamClientIDsFromMatchmakingQueue()
    {
        if (!PhotonNetwork.OfflineMode)
        {
            RaiseEventOptions reo = new RaiseEventOptions { Receivers = ReceiverGroup.All };
            SendOptions so = new SendOptions { Reliability = true };
            PhotonNetwork.RaiseEvent(MATCHMAKING_SET_TEAM_CLIENT_IDS_EVCODE, new object[] { }, reo, so);
        }
    }

    private void SetTeamClientIDsFromMatchmakingQueueEvent(EventData eventData)
    {
        if (eventData.Code == MATCHMAKING_SET_TEAM_CLIENT_IDS_EVCODE)
        {
            //TeamClientIDs = matchmakingQueueTeamClientIDs;

            for (int i = 0; i < TeamClientIDs.Count; i++)
            {
                Debug.Log("[ " + (TeamClientIDs[i][0] == "" ? "null" : TeamClientIDs[i][0]) + " , " + (TeamClientIDs[i][1] == "" ? "null" : TeamClientIDs[i][1]) + " ]");
            }
        }
    }

    private void Matchmaking_CorrectPlayerIDs()
    {
        TeamClientIDs.ForEach((x) => CheckAndCorrect(x));

        void CheckAndCorrect(List<string> teamClientID)
        {
            if (teamClientID[0] == partyLeaderID)
            {
                teamClientID[0] = PhotonNetwork.LocalPlayer.UserId;
            }
            if (teamClientID[1] == partyLeaderID)
            {
                teamClientID[1] = PhotonNetwork.LocalPlayer.UserId;
            }
        }

        //shouldCorrectTeamIDs = false;
    }

    private IEnumerator Matchmaking_RestartRegionSearchForMatch()
    {
        yield return new WaitForSeconds(restartRegionSearchForMatchTimer);

        if (MetaManager.Instance.IsInPlayableLevel)
        {
            restartRegionSearchLoopCoroutine = null;
            yield break;
        }
        if (!PhotonNetwork.OfflineMode && PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.PlayerCount == 1) // still only 1 client?
        {
            shouldSearchInOtherRegions = true;
            currentMatchmakingRegionConnectAttempt = 0;
            currentMatchmakingRegionIndex = -1;
            lastMatchmakingRegionConnectSuccess = false;
            PhotonNetwork.Disconnect();
        }

        restartRegionSearchLoopCoroutine = null;
    }

    private void Matchmaking_PlayerEnteredRoom(Player other)
    {
        if (restartRegionSearchLoopCoroutine != null)
        {
            StopCoroutine(restartRegionSearchLoopCoroutine);
            restartRegionSearchLoopCoroutine = null;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            CheckIfAllMatchmakingPlayersHaveJoined();
        }
    }
    #endregion
}