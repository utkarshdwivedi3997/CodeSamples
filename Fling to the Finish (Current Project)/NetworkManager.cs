#if UNITY_ANDROID || UNITY_IOS || UNITY_TIZEN || UNITY_TVOS || UNITY_WEBGL || UNITY_WSA || UNITY_PS4 || UNITY_WII || UNITY_XBOXONE || UNITY_SWITCH
#define DISABLESTEAMWORKS
# endif

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using System.Linq;                      // For generating random room codes
using UnityEngine.Events;

#if !DISABLESTEAMWORKS
using Steamworks;
#endif

/// <summary>
/// The main manager for the networked portion of Fling to the Finish
/// </summary>
public class NetworkManager : MonoBehaviourPunCallbacks {

    public static NetworkManager Instance { get; private set; }

#region Properties
    private bool isConnecting = false;                              // is the client attempting to connect to a network?
    private bool isConnected = false;
    public const int MAX_CLIENTS = 8;                              // Max number of players allowed to join a room

    private int clientsLoaded = 0;                                  // number of players loaded in a level

    /// <summary>
    /// Index 0 is team 1, contains a list of client ids for that team [["0", "1"], ["0",null], ["1",null], ["2",null]]
    /// </summary>
    public List<List<string>> TeamClientIDs { get; private set; }

    /// <summary> 
    /// Have all players loaded in the current level?
    /// </summary>
    public bool AllClientsLoaded { get; private set; }

    /// <summary>
    /// Number of clients in this room
    /// </summary>
    public int NumberOfClients { get { return PhotonNetwork.CurrentRoom.PlayerCount; } }

    /// <summary>
    /// Total number of teams across all clients in the current room, including single screen clients (1 team) and split screen clients (2 teams)
    /// </summary>
    public int NumberOfTeams { get; private set; }

    public List<int> indicesOfTeamsThatLeftForCleanUp;

#region CALLBACK_CODES
    // 0 <= Event code < 200 (Photon PUN2 itself uses the remaining 56 bytes)

    private const byte LOADING_NEW_LEVEL_EVCODE = 0;
    private const byte CLIENT_LOADED_EVCODE = 1;
    private const byte SET_NUM_OF_TEAMS_EVCODE = 2;
    private const byte ADD_NUM_OF_TEAMS_EVCODE = 3;
    private const byte SET_TEAM_NUMBERS_CLIENTID_EVCODE = 4;
    private const byte REMOVE_TEAM_NUMBERS_CLIENTID_EVCODE = 5;
    public const byte LOADING_SCREEN_START_EVCODE = 6;
    public const byte LOADING_SCREEN_END_EVCODE = 7;
    public const byte MATCHMAKING_LOOKING_FOR_LEADER_EVCODE = 8;
    public const byte MATCHMAKING_ADD_NUM_OF_TEAMS_EVCODE = 9;
    public const byte MATCHMAKING_SET_TEAM_CLIENT_IDS_EVCODE = 10;
    public const byte VOICE_CHAT_DICT_ADD_USER_EVCODE = 11;
    public const byte VOICE_CHAT_DICT_REMOVE_USER_EVCODE = 12;

#if !DISABLESTEAMWORKS
    public const byte UPDATE_STEAM_PERSONA_EVCODE = 13;
#endif

#endregion

    #region NETWORK_REGIONS
    private Dictionary<string, char> regionCorrespondingChars = new Dictionary<string, char>
    {
        { "asia", 'A'},
        { "au",'B' },
        { "cae", 'C' },
        { "eu", 'D' },
        { "in", 'E' },
        { "jp", 'F' },
        { "ru", 'G' },
        { "rue", 'H' },
        { "sa", 'I' },
        { "kr", 'J' },
        { "us", 'K' },
        { "usw", 'L' },
        // { "cn", 'M' },       // The Chinese Mainland servers have additional setup: https://doc.photonengine.com/en-us/pun/current/connection-and-authentication/regions#using_the_chinese_mainland_region
    };
#endregion

#region Party specific properties
    /// <summary>
    /// User IDs of players in the player's party
    /// </summary>
    public List<string> PartyUserIds;

    public List<int> LocalTeamIndices { get; set; }                 // Team indices IN PARTY. This is different from team indices in matchmaking queue.

    /// <summary>
    /// Is this client in the matchmaking queue right now?
    /// </summary>
    public bool IsMatchmaking { get; private set; }

    /// <summary>
    /// Is this client matchmaking and looking for their lobby leader?
    /// </summary>
    public bool IsLookingForLeader { get; private set; }
    
    /// <summary>
    /// Is this client the leader for their party?
    /// </summary>
    public bool IsPartyLeader { get; private set; }

    private string roomCode = "";

    public GameObject photonVoiceViewPrefab;
    private Dictionary<string, GameObject> photonVoiceViewDictionary = new Dictionary<string, GameObject>();
    #endregion

    #region Matchmaking specific properties
    private List<string> matchmakingQueueUserIDs;           // User IDs of ALL the users from all the parties in the matchmaking queue. This list updates in the order of joining of squad leaders, who then add all their party members to the list.
    private List<List<string>> matchmakingQueueTeamClientIDs;

    public List<int> TeamIndicesInMatchmakingQueue { get; set; }

    /// <summary>
    /// Local variable, not synced online.
    /// The global MMQ index at which this party's first team indices start
    /// </summary>
    public int StartingTeamIndexForMyParty { get; private set; }
#endregion

#region Other connectivity related properties
    /// <summary>
    /// The type of room connection we are using to get people to join lobbies
    /// </summary>
    public enum ConnectionType
    {
        /// <summary>
        /// Jackbox style legacy room codes
        /// </summary>
        RoomCodes,

        /// <summary>
        /// Invite friends using Steam/Switch friend system
        /// </summary>
        FriendInvites
    }

    [SerializeField]
    private ConnectionType connectUsing = ConnectionType.FriendInvites;
    public ConnectionType ConnectUsing
    {
        get { return connectUsing; }
    }

    /// <summary>
    /// Dictionary of <clientId , FlingFriend>
    /// Each connected client Id will give a FlingFriend struct with details about that Friend (Friend via Steam, Switch, rando, etc.)
    /// </summary>
    public Dictionary<string, FlingFriend> FriendByClientId { get; private set; }
#endregion
#endregion

#region Awake and Start
    void Awake () {
        // Singleton Instance
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
        }

        // Subscribe to events
        PhotonNetwork.NetworkingClient.EventReceived += LoadingNewLevelEvent;
        PhotonNetwork.NetworkingClient.EventReceived += ClientLoadedEvent;
        PhotonNetwork.NetworkingClient.EventReceived += AddNumberOfTeamsEvent;
        PhotonNetwork.NetworkingClient.EventReceived += SetNumberOfTeamsEvent;
        PhotonNetwork.NetworkingClient.EventReceived += SetTeamNumbersClientIdEvent;
        PhotonNetwork.NetworkingClient.EventReceived += RemoveTeamNumbersClientIdEvent;
        PhotonNetwork.NetworkingClient.EventReceived += Matchmaking_StartLookingForLeaderEvent;
        PhotonNetwork.NetworkingClient.EventReceived += Matchmaking_AddNumberOfTeamsEvent;
        PhotonNetwork.NetworkingClient.EventReceived += SetTeamClientIDsFromMatchmakingQueueEvent;
        PhotonNetwork.NetworkingClient.EventReceived += VoiceChatDictAddUserEvent;
        PhotonNetwork.NetworkingClient.EventReceived += VoiceChatDictRemoveUserEvent;
        PhotonNetwork.NetworkingClient.EventReceived += UpdateSteamPersonaOnOtherClientEvent;
    }

    private void Start()
    {
        LocalTeamIndices = new List<int> { -1, -1 };
        TeamIndicesInMatchmakingQueue = new List<int> { -1, -1 };
        indicesOfTeamsThatLeftForCleanUp = new List<int>();

        // PhotonNetwork.RaiseEvent(0, new object[] { }, new RaiseEventOptions { Receivers = ReceiverGroup.All, CachingOption = EventCaching. }, new ExitGames.Client.Photon.SendOptions { Reliability = true });
        roomCode = "";

        isConnecting = false;
        isConnected = false;
        IsMatchmaking = false;
        IsLookingForLeader = false;
        matchmakingQueueUserIDs = new List<string>();
        StartingTeamIndexForMyParty = 0;

        ResetTeamClientIDs();
        ResetFriendsList();
    }
#endregion

#region External Helper Functions
    /// <summary>
    /// Connect to Master Server
    /// </summary>
    public void Connect(string region = "")
    {
        isConnecting = true;
        Debug.Log("Connecting...");
        PhotonNetwork.OfflineMode = false;

        //ServerSettings.ResetBestRegionCodeInPreferences();
        
        // Set the game version
        PhotonNetwork.PhotonServerSettings.AppSettings.AppVersion = MetaManager.Instance.GameVersion;

        if (region == null || region.Equals(""))
        {
            ServerSettings.ResetBestRegionCodeInPreferences();
            PhotonNetwork.ConnectUsingSettings();
        }
        else
        {
            region = region.ToLower();

            PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = region;
            PhotonNetwork.ConnectUsingSettings();
        }

        PhotonNetwork.GameVersion = MetaManager.Instance.GameVersion;       // Set photon network game version. This has to be done after calling ConnectUsingSettings() for some reason :/. 
                                                                            // Also, yes there are two different things: PhotonNetwork.GameVersion and PhotonNetwork.PhotonServerSettings.AppSettings.AppVersion set above
    }

    /// <summary>
    /// Disconnect from Master Server
    /// </summary>
    public void Disconnect()
    {
        Debug.Log("Disconnecting...");
        PhotonNetwork.Disconnect();

        // PhotonNetwork.OfflineMode = true;
    }

    /// <summary>
    /// Connects and then creates a new room or joins an already existing one.
    /// If the rmCode string is empty, a new room is created, otherwise the rmCode is analaysed and the room with that room code is joined.
    /// </summary>
    /// <param name="rmCode"></param>
    public void ConnectThenCreateOrJoinRoom(string rmCode = "")
    {
        StartCoroutine(ConnectThenCreateOrJoinRoomCoroutine(rmCode));
    }

    private IEnumerator ConnectThenCreateOrJoinRoomCoroutine(string rmCode = "")
    {
        if (rmCode.Equals(""))
        {
            Connect();
        }
        else if (rmCode.Length == 4)
        {
            string region = regionCorrespondingChars.FirstOrDefault(x => x.Value == rmCode[0]).Key;
            Connect(region);
        }
        else
        {
            // Stop coroutine right here because the dumdums provided a wrong room code
            yield break;
        }

        while (!isConnected)
        {
            yield return null;
        }

        if (rmCode.Equals("")) CreateRoom();
        else JoinRoom(rmCode);
    }

    public void ConnectThenMatchmake()
    {
        if (!isConnecting)
        {
            StartCoroutine(ConnectThenMatchmakeCoroutine());
        }
    }

    private IEnumerator ConnectThenMatchmakeCoroutine()
    {
        IsMatchmaking = true;
        Connect();

        while (!isConnected)
        {
            yield return null;
        }

        StartMatchmaking();
    }

    /// <summary>
    /// Creates a new room
    /// </summary>
    public void CreateRoom()
    {
        string rmCode = GenerateNewRoomCode();
        Debug.Log("Room code is " + rmCode);

        RoomOptions ro = new RoomOptions();

        ro.MaxPlayers = (byte)MAX_CLIENTS;
        ro.IsOpen = true;
        ro.IsVisible = true;
        ro.EmptyRoomTtl = 0;
        ro.PlayerTtl = 0;
        ro.PublishUserId = true;
        ro.CleanupCacheOnLeave = false;

        //ro.CustomRoomProperties = new ExitGames.Client.Photon.Hashtable() { { "C0", "Hello" } };
        //ro.CustomRoomPropertiesForLobby = new string[] { "C0" };
        PhotonNetwork.CreateRoom(rmCode, ro, new TypedLobby("myLobby", LobbyType.SqlLobby));
    }

    /// <summary>
    /// Joins an existing room with a specified name
    /// </summary>
    /// <param name="roomName"></param>
    public void JoinRoom(string roomName)
    {
        // Need to reset this for the future
        PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = null;

        if (roomName.Length != 4)
        {
            Debug.Log("<color=red>Please provide a proper room name!</color>");
        }
        else
        {
            PhotonNetwork.JoinRoom(roomName);
        }
    }

    public UnityAction RoomClosed;
    /// <summary>
    /// Closes the current room
    /// </summary>
    public void CloseRoom()
    {
        if (!PhotonNetwork.OfflineMode)
        {
            PhotonNetwork.CurrentRoom.IsOpen = false;

            if (RoomClosed != null)
            {
                RoomClosed();
            }
        }
    }

    public UnityAction RoomOpened;
    /// <summary>
    /// Opens the current room
    /// </summary>
    public void OpenRoom()
    {
        if (!PhotonNetwork.OfflineMode)
        {
            PhotonNetwork.CurrentRoom.IsOpen = true;

            if (RoomOpened != null)
            {
                RoomOpened();
            }
        }
    }

    public void ClearCachesFromLobby()
    {
        if (!PhotonNetwork.OfflineMode)
        {
            RaiseEventOptions reo = new RaiseEventOptions { Receivers = ReceiverGroup.All, CachingOption = EventCaching.RemoveFromRoomCache };
            SendOptions so = new SendOptions { Reliability = true };
            PhotonNetwork.RaiseEvent(REMOVE_TEAM_NUMBERS_CLIENTID_EVCODE, new object[] { }, reo, so);
            PhotonNetwork.RaiseEvent(SET_TEAM_NUMBERS_CLIENTID_EVCODE, new object[] { }, reo, so);
            PhotonNetwork.RaiseEvent(UPDATE_STEAM_PERSONA_EVCODE, new object[] { }, reo, so);
        }
    }

    /// <summary>
    /// Public facing function to be called when loading a new level
    /// </summary>
    public void LoadingNewLevel()
    {
        if (!PhotonNetwork.OfflineMode)
        {
            // photonView.RPC("LoadingNewLevelEvent", RpcTarget.AllBufferedViaServer);

            RaiseEventOptions reo = new RaiseEventOptions { Receivers = ReceiverGroup.All };
            SendOptions so = new SendOptions { Reliability = true };
            PhotonNetwork.RaiseEvent(LOADING_NEW_LEVEL_EVCODE, new object[] { }, reo, so);
        }
    }

    /// <summary>
    /// The actual logic for the function LoadingNewLevel()
    /// </summary>
    private void LoadingNewLevelEvent(EventData eventData)
    {
        if (eventData.Code == LOADING_NEW_LEVEL_EVCODE)
        {
            AllClientsLoaded = false;
            clientsLoaded = 0;
        }
    }

    public void ClearEmptyTeamClientIDs()
    {
        TeamClientIDs.RemoveAll(x => (x[0] == "" && x[1] == ""));       // remove all elements where both client IDs are null

        // Move the nulls to the end, instead of removing them!
        //var nulls = TeamClientIDs.Where(x => x[0] == null);
        //var nonnulls = TeamClientIDs.Where(x => x[0] != null);
        //var result = nonnulls.Concat(nulls);
        //TeamClientIDs = result.ToList();

        //for (int i = 0; i < TeamClientIDs.Count; i++)
        //{
        //    Debug.Log("[ " + (TeamClientIDs[i][0] == null ? "null" : TeamClientIDs[i][0]) + " , " + (TeamClientIDs[i][1] == null ? "null" : TeamClientIDs[i][1]) + " ]");
        //}

        //Debug.Log(teamClientIds.Count);
    }

    public void ResetTeamClientIDs()
    {
        TeamClientIDs = new List<List<string>>();
        for (int i = 0; i < 8; i++)
        {
            TeamClientIDs.Add(new List<string>());
            TeamClientIDs[i].Add("");
            TeamClientIDs[i].Add("");
        }

        matchmakingQueueTeamClientIDs = new List<List<string>>();
        StartingTeamIndexForMyParty = 0;
    }

    private void ResetFriendsList()
    {
        FriendByClientId = new Dictionary<string, FlingFriend>();
    }

#region Matchmaking Specific Functions
    /// <summary>
    /// Starts the matchmaking process, ONLY applicable on Master Client
    /// </summary>
    public void StartMatchmaking()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        // Before leaving room, tell everyone else to start looking for this client!
        Matchmaking_StartLookingForLeader(PhotonNetwork.LocalPlayer.UserId);

        PhotonNetwork.LeaveRoom();
        IsMatchmaking = true;
    }

    public void CreateMatchmakingRoom()
    {
        Debug.Log("Creating a new matchmaking room...");
        PhotonNetwork.CreateRoom("", new RoomOptions() { MaxPlayers = 16, PublishUserId = true }, null, PartyUserIds.ToArray());
    }

    private IEnumerator LookForMyLeader(string leaderID)
    {
        IsMatchmaking = true;
        IsLookingForLeader = true;

        while (isConnecting)
        {
            yield return null;
        }

        while (!PhotonNetwork.FindFriends(new string[] { leaderID }))
        {
            yield return new WaitForSeconds(2f);
            Debug.Log("<color=yellow>Still looking for friend...</color>");
        }

        Debug.Log("<color=green>Friend found!</color>");
    }

    public override void OnFriendListUpdate(List<FriendInfo> friendList)
    {
        base.OnFriendListUpdate(friendList);

        StartCoroutine(ConnectToLeaderRoom(friendList[0]));
    }

    private IEnumerator ConnectToLeaderRoom(FriendInfo leader)
    {
        while (!leader.IsInRoom)
        {
            Debug.Log("Waiting for leader to join a matchmaking room...");
            yield return new WaitForSeconds(2f);
        }

        PhotonNetwork.JoinRoom(leader.Room);
    }

    #endregion

    #region Friend Invite System Functions
#if !DISABLESTEAMWORKS
    public void SendInviteToFriend(CSteamID ID)
    {
        SteamScript.Instance.SendInviteToFriend(ID);
    }

    private void UpdateSteamPersonaOnOtherClients()
    {
        CSteamID self = SteamUser.GetSteamID();
        string longSelf = self.m_SteamID.ToString();

        if (!PhotonNetwork.OfflineMode)
        {
            RaiseEventOptions reo = new RaiseEventOptions { Receivers = ReceiverGroup.All, CachingOption = EventCaching.AddToRoomCache };
            SendOptions so = new SendOptions { Reliability = true };
            PhotonNetwork.RaiseEvent(UPDATE_STEAM_PERSONA_EVCODE, new object[] { longSelf, PhotonNetwork.LocalPlayer.UserId }, reo, so);
        }
    }

    /// <summary>
    /// Event to update steam persona on other clients
    /// </summary>
    /// <param name="eventData"></param>
    private void UpdateSteamPersonaOnOtherClientEvent(EventData eventData)
    {
        if (eventData.Code == UPDATE_STEAM_PERSONA_EVCODE)
        {
            object[] content = (object[])eventData.CustomData;
            string steamId = (string)content[0];
            ulong ulongSteamId = ulong.Parse(steamId);
            string clientId = (string)content[1];

            CSteamID SteamId = new CSteamID(ulongSteamId);
            FlingFriend friend = SteamScript.Instance.GetFriendBySteamId(SteamId);

            if (!FriendByClientId.ContainsKey(clientId))
            {
                FriendByClientId.Add(clientId, friend);
            }
        }
    }
#endif

    #endregion
    #endregion

    #region RAISED_EVENTS
    /// <summary>
    /// Public facing function to be called when a level has finished loading on a client
    /// </summary>
    public void ClientLoaded()
    {
        if (!PhotonNetwork.OfflineMode)
        {
            // photonView.RPC("ClientLoadedEvent", RpcTarget.AllBufferedViaServer);

            RaiseEventOptions reo = new RaiseEventOptions { Receivers = ReceiverGroup.All };
            SendOptions so = new SendOptions { Reliability = true };
            PhotonNetwork.RaiseEvent(CLIENT_LOADED_EVCODE, new object[] { }, reo, so);
        }
    }

    /// <summary>
    /// The actual logic for the function for ClientLoaded()
    /// </summary>
    private void ClientLoadedEvent(EventData eventData)
    {
        if (eventData.Code == CLIENT_LOADED_EVCODE)
        {
            clientsLoaded++;
            if (clientsLoaded == PhotonNetwork.CurrentRoom.PlayerCount)
            {
                AllClientsLoaded = true;
                Debug.Log("All players loaded!");
                for (int i = 0; i < NumberOfTeams; i++)
                {
                    Debug.Log("[ " + (TeamClientIDs[i][0] == "" ? "null" : TeamClientIDs[i][0]) + " , " + (TeamClientIDs[i][1] == "" ? "null" : TeamClientIDs[i][1]) + " ]");
                    /*if (teamClientIds[i].Contains(PhotonNetwork.LocalPlayer.UserId))
                    {
                        TeamIndicesOrdered.Add(i);
                    }*/
                }
            }
        }
    }

    /// <summary>
    /// Add 1 or 2 teams to the total number of teams across all clients.
    /// This function calls an RPC call across all clients and adds the numToAdd to NumberOfTeams on all clients
    /// </summary>
    /// <param name="numToAdd"></param>
    public void AddNumberOfTeams(int numToAdd) {

        if (!PhotonNetwork.OfflineMode)
        {
            // Stuff to do on all clients
            //photonView.RPC("AddNumberOfTeamsEvent", RpcTarget.AllBufferedViaServer, numToAdd);

            RaiseEventOptions reo = new RaiseEventOptions { Receivers = ReceiverGroup.All };
            SendOptions so = new SendOptions { Reliability = true };
            PhotonNetwork.RaiseEvent(ADD_NUM_OF_TEAMS_EVCODE, new object[] { numToAdd }, reo, so);
        }
    }

    /// <summary>
    /// RPC for AddNumberOfTeams(), called on all clients
    /// </summary>
    /// <param name="numToAdd"></param>
    private void AddNumberOfTeamsEvent(EventData eventData) {
        if (eventData.Code == ADD_NUM_OF_TEAMS_EVCODE)
        {
            object[] content = (object[])eventData.CustomData;
            int numOfTeams = (int)content[0];

            NumberOfTeams += numOfTeams;
        }
    }

    /// <summary>
    /// Add 1 or 2 teams to the total number of teams across all clients.
    /// This function calls an RPC call across all clients and sets the NumberOfTeams to numToSet on all clients
    /// </summary>
    /// <param name="numToSet"></param>
    public void SetNumberOfTeams(int numToSet)
    {
        if (!PhotonNetwork.OfflineMode && PhotonNetwork.IsMasterClient)
        {        
            RaiseEventOptions reo = new RaiseEventOptions { Receivers = ReceiverGroup.All };
            SendOptions so = new SendOptions { Reliability = true };
            PhotonNetwork.RaiseEvent(SET_NUM_OF_TEAMS_EVCODE, new object[] { numToSet }, reo, so);
        }
        else
        {
            NumberOfTeams = numToSet;
        }
    }

    /// <summary>
    /// RPC for SetNumberOfTeams(), called on all clients
    /// </summary>
    /// <param name="numToSet"></param>
    private void SetNumberOfTeamsEvent(EventData eventData) {
        
        if (eventData.Code == SET_NUM_OF_TEAMS_EVCODE)
        {
            object[] content = (object[])eventData.CustomData;
            int numOfTeams = (int)content[0];

            NumberOfTeams = numOfTeams;
        }
    }

    public void SetTeamNumbersClientId(int teamNumber, string clientId, int playerNum) {
        if (!PhotonNetwork.OfflineMode)
        {            
            // Stuff to do on all clients
            // photonView.RPC("SetTeamNumbersClientIdEvent", RpcTarget.AllBuffered, teamNumber, clientId, playerNum);

            RaiseEventOptions reo = new RaiseEventOptions { Receivers = ReceiverGroup.All, CachingOption = EventCaching.AddToRoomCache };
            SendOptions so = new SendOptions { Reliability = true };
            PhotonNetwork.RaiseEvent(SET_TEAM_NUMBERS_CLIENTID_EVCODE, new object[] { teamNumber, clientId, playerNum }, reo, so);
        }
    }

    private void SetTeamNumbersClientIdEvent(EventData eventData) {

        if (eventData.Code == SET_TEAM_NUMBERS_CLIENTID_EVCODE)
        {
            Debug.Log("Setting teamClientIDs");
            object[] content = (object[]) eventData.CustomData;
            int teamNumber = (int)content[0];
            string clientId = (string)content[1];
            int playerNum = (int)content[2];

            //Debug.Log(teamNumber + " " + clientId + " " + playerNum);
            if (TeamClientIDs[teamNumber][playerNum] == "")
            {
                TeamClientIDs[teamNumber][playerNum] = clientId;

                // if (playerNum == 0) {
                //     possibleTeamsToCreate++;
                // }
            }
            else if (TeamClientIDs[teamNumber][1 - playerNum] == "")
            {
                TeamClientIDs[teamNumber][1 - playerNum] = TeamClientIDs[teamNumber][playerNum];
                TeamClientIDs[teamNumber][playerNum] = clientId;
            }
            //else if(teamClientIds[teamNumber][1] == null){
            //   teamClientIds[teamNumber][1] = clientId;
            //}
            else
            {
                Debug.LogWarning("somethings not right in controller RPC set");
            }
            //Debug.Log(teamClientIds[0].ToString());

            Debug.Log("<color=green>Team 0 " + TeamClientIDs[0][0] + "," + TeamClientIDs[0][1] + "</color>");
            Debug.Log("<color=green>Team 1 " + TeamClientIDs[1][0] + "," + TeamClientIDs[1][1] + "</color>");
            Debug.Log("<color=green>Team 2 " + TeamClientIDs[2][0] + "," + TeamClientIDs[2][1] + "</color>");
            Debug.Log("<color=green>Team 3 " + TeamClientIDs[3][0] + "," + TeamClientIDs[3][1] + "</color>");

        }
    }

    public void RemoveTeamNumbersClientId(int teamNumber, string clientId, int playerNum) {
        if (!PhotonNetwork.OfflineMode)
        {            
            // Stuff to do on all clients
            // photonView.RPC("RemoveTeamNumbersClientIdEvent", RpcTarget.AllBuffered, teamNumber, clientId, playerNum);

            RaiseEventOptions reo = new RaiseEventOptions { Receivers = ReceiverGroup.All, CachingOption = EventCaching.AddToRoomCache };
            SendOptions so = new SendOptions { Reliability = true };
            PhotonNetwork.RaiseEvent(REMOVE_TEAM_NUMBERS_CLIENTID_EVCODE, new object[] { teamNumber, clientId, playerNum }, reo, so);
        }
    }

    private void RemoveTeamNumbersClientIdEvent(EventData eventData) {
        if (eventData.Code == REMOVE_TEAM_NUMBERS_CLIENTID_EVCODE)
        {
            Debug.Log("Removing teamClientIDs");
            object[] content = (object[])eventData.CustomData;
            int teamNumber = (int)content[0];
            string clientId = (string)content[1];
            int playerNum = (int)content[2];

            if ((TeamClientIDs[teamNumber][playerNum] == clientId)) // && (teamClientIds[teamNumber][1] == clientId))
            {
                TeamClientIDs[teamNumber][playerNum] = "";

                /*if (playerNum == 0)
                {
                    TeamClientIDs[teamNumber][0] = TeamClientIDs[teamNumber][1];
                    TeamClientIDs[teamNumber][1] = null;
                }*/

            }
            //else if(teamClientIds[teamNumber][0] == clientId) {
            //    teamClientIds[teamNumber][0] = null;
            //}
            //else if(teamClientIds[teamNumber][1] == clientId){
            //    teamClientIds[teamNumber][1] = null;
            //}
            else
            {
                Debug.LogWarning("somethings not right in controller RPC REEEmove");
            }

            if (TeamClientIDs[teamNumber][0] == "" && TeamClientIDs[teamNumber][1] != "")
            {
                TeamClientIDs[teamNumber][0] = TeamClientIDs[teamNumber][1];
                TeamClientIDs[teamNumber][1] = "";
            }

            Debug.Log("<color=red>Team 0 " + TeamClientIDs[0][0] + "," + TeamClientIDs[0][1] + "</color>");
            Debug.Log("<color=red>Team 1 " + TeamClientIDs[1][0] + "," + TeamClientIDs[1][1] + "</color>");
            Debug.Log("<color=red>Team 2 " + TeamClientIDs[2][0] + "," + TeamClientIDs[2][1] + "</color>");
            Debug.Log("<color=red>Team 3 " + TeamClientIDs[3][0] + "," + TeamClientIDs[3][1] + "</color>");
            //Debug.Log(teamClientIds[0].ToString());\
        }
    }

#region Matchmaking Specific Cached Events
    private void Matchmaking_StartLookingForLeader(string leaderID)
    {
        if (!PhotonNetwork.OfflineMode)
        {
            RaiseEventOptions reo = new RaiseEventOptions { Receivers = ReceiverGroup.Others, CachingOption = EventCaching.AddToRoomCache };
            SendOptions so = new SendOptions { Reliability = true };
            PhotonNetwork.RaiseEvent(MATCHMAKING_LOOKING_FOR_LEADER_EVCODE, new object[] { leaderID }, reo, so);
        }
    }

    private void Matchmaking_StartLookingForLeaderEvent(EventData eventData)
    {
        if (eventData.Code == MATCHMAKING_LOOKING_FOR_LEADER_EVCODE)
        {
            object[] content = (object[])eventData.CustomData;
            string leaderID = (string)content[0];

            PhotonNetwork.LeaveRoom();
            isConnecting = true;

            StartCoroutine(LookForMyLeader(leaderID));
        }
    }

    private void Matchmaking_AddNumberOfTeams(string partyCode, int numberOfTeams)
    {
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
            if (partyCode != roomCode)
            {
                NumberOfTeams += numToAdd;

                if (PhotonNetwork.IsMasterClient)
                {
                    if (NumberOfTeams >= 8)
                    {
                        CloseRoom();
                    }
                }

                if (Matchmaking_NumberOfTeamsUpdated != null)
                {
                    Matchmaking_NumberOfTeamsUpdated();
                }
            }

            //matchmakingQueueUserIDs.AddRange(partyUserIDs);
            matchmakingQueueTeamClientIDs.AddRange(teamClientIDs);
            if (partyCode == roomCode)
            {
                StartingTeamIndexForMyParty = matchmakingQueueTeamClientIDs.IndexOf(teamClientIDs[0]);
            }
        }
    }

    [ContextMenu("Set TeamClientIDs")]
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
            TeamClientIDs = matchmakingQueueTeamClientIDs;

            for (int i = 0; i < TeamClientIDs.Count; i++)
            {
                Debug.Log("[ " + (TeamClientIDs[i][0] == "" ? "null" : TeamClientIDs[i][0]) + " , " + (TeamClientIDs[i][1] == "" ? "null" : TeamClientIDs[i][1]) + " ]");
            }
        }
    }

#endregion

#endregion

#region PUN Callbacks
    public override void OnConnectedToMaster()
    {
        // we don't want to do anything if we are not attempting to join a room. 
        // this case where isConnecting is false is typically when you lost or quit the game, when this level is loaded, OnConnectedToMaster will be called, in that case
        // we don't want to do anything.
        if (isConnecting)
        {
            Debug.Log("OnConnectedToMaster: This client is connected to the server.");

            isConnecting = false;
            isConnected = true;
            // Debug.Log("Server: " + PhotonNetwork.ServerAddress);
            // Debug.Log("Region: " + PhotonNetwork.CloudRegion);

            if (IsMatchmaking)
            {
                if (!IsLookingForLeader)
                {
                    Debug.Log("Now looking for a random room...");
                    PhotonNetwork.JoinRandomRoom(null, 16, MatchmakingMode.FillRoom, null, null, PartyUserIds.ToArray());
                }
                else
                {
                    Debug.Log("Starting search for lobby leader...");
                }
            }
            else
            {
                ResetTeamClientIDs();
                ResetFriendsList();
            }
        }
    }

    public override void OnCreatedRoom()
    {
        base.OnCreatedRoom();

        Debug.Log("<color=green>OnCreatedRoom success.</color>");
        //roomCode = PhotonNetwork.CurrentRoom.Name;
        //roomCodeText.text = "Room code: " + roomCode;
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        base.OnCreateRoomFailed(returnCode, message);

        Debug.Log("<color=red>OnCreateRoomFailed</color> with message " + message);

        // Room with the same name already exists! Call create room again
        if (returnCode == ErrorCode.GameIdAlreadyExists)
        {
            CreateRoom();
        }
    }

    public delegate void OnJoinedRoomDelegate(string roomName);
    public event OnJoinedRoomDelegate JoinedRoom;
    public override void OnJoinedRoom()
    {
        Debug.Log("<Color=Green>OnJoinedRoom</Color> with " + Photon.Pun.PhotonNetwork.CurrentRoom.PlayerCount + " Player(s)");

        // #Critical: We only load if we are the first player, else we rely on  PhotonNetwork.AutomaticallySyncScene to sync our instance scene.
        if (Photon.Pun.PhotonNetwork.CurrentRoom.PlayerCount == 1)
        {

        }

        UpdateSteamPersonaOnOtherClients();

        // If isn't matchmaking, then the player is in the lobby screen
        if (!IsMatchmaking)
        {
            roomCode = PhotonNetwork.CurrentRoom.Name;
            PartyUserIds = new List<string>();

            foreach (KeyValuePair<int, Player> player in PhotonNetwork.CurrentRoom.Players)
            {
                PartyUserIds.Add(player.Value.UserId);
            }

            if (PhotonNetwork.IsMasterClient)
            {
                IsPartyLeader = true;
            }
        }
        // Else, this is the matchmaking screen
        else
        {
            if (IsPartyLeader)
            {
                Matchmaking_AddNumberOfTeams(roomCode, NumberOfTeams);
            }
        }

        if (JoinedRoom != null)
        {
            JoinedRoom(PhotonNetwork.CurrentRoom.Name);
        }

        //GameObject thisPhotonVoiceView = PhotonNetwork.Instantiate(photonVoiceViewPrefab.name, Vector3.zero, Quaternion.identity);
        //int photonVoiceViewId = thisPhotonVoiceView.GetComponent<PhotonView>().ViewID;

        //RaiseEventOptions reo = new RaiseEventOptions { Receivers = ReceiverGroup.All, CachingOption = EventCaching.AddToRoomCache };
        //SendOptions so = new SendOptions { Reliability = true };
        //PhotonNetwork.RaiseEvent(VOICE_CHAT_DICT_ADD_USER_EVCODE, new object[] { PhotonNetwork.LocalPlayer.UserId, photonVoiceViewId }, reo, so);
    }

    private void VoiceChatDictAddUserEvent(EventData eventData) {
        if (eventData.Code == VOICE_CHAT_DICT_ADD_USER_EVCODE) {
            object[] content = (object[])eventData.CustomData;
            string playerUserId = (string)content[0];
            int thisPhotonVoiceViewId = (int)content[1];

            if (photonVoiceViewDictionary.ContainsKey(playerUserId)) {
                if (photonVoiceViewDictionary[playerUserId] != null) {
                    Destroy(photonVoiceViewDictionary[playerUserId]);
                    photonVoiceViewDictionary.Remove(playerUserId);
                }
            }

            GameObject thisPhotonVoiceView = PhotonView.Find(thisPhotonVoiceViewId).gameObject;
            photonVoiceViewDictionary.Add(playerUserId, thisPhotonVoiceView);
            DontDestroyOnLoad(photonVoiceViewDictionary[playerUserId]);
        }
    }

    public delegate void JoinRoomFailedDelegate(short returnCode, string message);
    public event JoinRoomFailedDelegate JoinRoomFailed;
    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        base.OnJoinRoomFailed(returnCode, message);

        Debug.Log("<color=red>OnJoinRoomFailed</color> with message " + message);

        //if (returnCode == ErrorCode.GameDoesNotExist)
        //{
            if (JoinRoomFailed != null) JoinRoomFailed(returnCode, message);
            Disconnect();
        //}
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.Log("<Color=Red>OnJoinRandomFailed</Color>: Next -> Create a new Room");

        if (IsMatchmaking)
        {
            CreateMatchmakingRoom();
        }
    }

    public override void OnLeftRoom()
    {
        base.OnLeftRoom();

        if (IsMatchmaking)
        {
            isConnecting = true;
            if (!IsLookingForLeader)
            {
                Debug.Log("Left party to start matchmaking.");
            }
            else
            {
                Debug.Log("Left party to find leader from matchmaking.");
            }
        }
    }

    public delegate void OnDisconnectedDelegate(DisconnectCause cause);
    public event OnDisconnectedDelegate OnDisconnectedFromNetwork;
    public override void OnDisconnected(Photon.Realtime.DisconnectCause cause)
    {
        Debug.Log("<Color=Red>OnDisconnected</Color> " + cause);
        // Debug.LogError("PUN Basics Tutorial/Launcher:Disconnected");

        isConnecting = false;
        isConnected = false;
        PhotonNetwork.OfflineMode = true;
        IsMatchmaking = false;
        IsLookingForLeader = false;
        PartyUserIds = null;
        matchmakingQueueUserIDs = new List<string>();
        StartingTeamIndexForMyParty = 0;

        //if (photonVoiceViewDictionary != null)
        //{
        //    foreach (string key in photonVoiceViewDictionary.Keys)
        //    {
        //        Destroy(photonVoiceViewDictionary[key]);
        //        photonVoiceViewDictionary.Remove(key);
        //    }
        //}


        if (OnDisconnectedFromNetwork != null)
        {
            OnDisconnectedFromNetwork(cause);
        }
    }

    public delegate void OnClientEnteredRoomDelegate(Photon.Realtime.Player other);
    public event OnClientEnteredRoomDelegate OnClientEnteredRoom;
    public override void OnPlayerEnteredRoom(Photon.Realtime.Player other)
    {
        Debug.Log("OnPlayerEnteredRoom() " + other.NickName); // not seen if you're the player connecting
        if (PhotonNetwork.CurrentRoom.PlayerCount == MAX_CLIENTS)
        {
            Debug.Log("Max players in room. Closing the room.");
            PhotonNetwork.CurrentRoom.IsVisible = true;
            PhotonNetwork.CurrentRoom.IsOpen = false;
        }

        if (!IsMatchmaking)
        {
            PartyUserIds.Add(other.UserId);
        }

        if (OnClientEnteredRoom != null)
        {
            OnClientEnteredRoom(other);
        }

        // if (PhotonNetwork.IsMasterClient)
        // {
        // Debug.LogFormat("OnPlayerEnteredRoom IsMasterClient {0}", PhotonNetwork.IsMasterClient); // called before OnPlayerLeftRoom

        //Restart();
        // }
    }

    public delegate void OnClientLeftRoomDelegate(string clientID, Player otherPlayer);
    /// <summary>
    /// Event that fires when another player leaves the room
    /// </summary>
    public event OnClientLeftRoomDelegate OnClientLeftRoom;

    public delegate void OnTeamsLeftRoomDelegate(Player player, List<int> indicesOfTeamsThatLeft, List<int> sharedTeamIndices);
    /// <summary>
    /// Event that fires when a client leaves the room.
    /// Similar to OnClientLeftRoom, but this also passes a list of team indices for teams that left
    /// </summary>
    public event OnTeamsLeftRoomDelegate OnTeamsLeftRoom;
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);

        string clientID = otherPlayer.UserId;

        if (FriendByClientId.ContainsKey(clientID))
        {
            FriendByClientId.Remove(clientID);
        }

        if (Menus.MenuManager.Instance != null && Menus.MenuManager.Instance.CurrentMenuState == Menus.MenuState.GAMEMODE_SELECT)      // TODO - Add appropriate player leave checks
            return;

        if (!MetaManager.Instance.IsInMainMenu || (Menus.MenuManager.Instance != null && Menus.MenuManager.Instance.CurrentMenuState != Menus.MenuState.LOBBY_SCREEN)) // don't do this in lobby screen since it works differently
        {
            // I was using LINQ earlier, but it's apparently not as performant in Unity, so I'm not using it :(
            List<int> indicesOfTeamsThatLeft = new List<int>(); //TeamClientIDs.FindAllIndexOfConditionMet(x => (x[0] == clientID || x[1] == clientID));
            int teamsFromClient = 0;
            List<int> sharedTeamIndices = new List<int>();

            for (int i = 0; i < TeamClientIDs.Count; i++)
            {
                // If team isn't shared
                if (TeamClientIDs[i][0] == clientID && (TeamClientIDs[i][1] == "" || TeamClientIDs[i][1] == clientID))
                {
                    teamsFromClient++;
                }

                if (TeamClientIDs[i][0] != TeamClientIDs[i][1] &&
                    (TeamClientIDs[i][0] == clientID && TeamClientIDs[i][1] != "") ||
                    (TeamClientIDs[i][1] == clientID && TeamClientIDs[i][0] != ""))
                {
                    sharedTeamIndices.Add(i);
                }

                bool firstTeamFromClient = false;
                if (TeamClientIDs[i][0] == clientID)
                {
                    firstTeamFromClient = true;
                    TeamClientIDs[i][0] = "";

                    indicesOfTeamsThatLeft.Add(i);
                    indicesOfTeamsThatLeftForCleanUp.Add(i);
                }
                if (TeamClientIDs[i][1] == clientID)
                {
                    TeamClientIDs[i][1] = "";

                    if (!firstTeamFromClient)
                    {
                        indicesOfTeamsThatLeft.Add(i);
                        indicesOfTeamsThatLeftForCleanUp.Add(i);
                    }
                }

                if (TeamClientIDs[i][0] == "" && TeamClientIDs[i][1] != "")
                {
                    TeamClientIDs[i][0] = TeamClientIDs[i][1];
                    TeamClientIDs[i][1] = "";
                }

            }
            // ClearEmptyTeamClientIDs();
            MenuData.RemoveAllIdentifyingKeysOfClient(clientID);

            //int teamsFromClient = TeamClientIDs.Count(x => ((x[0] == clientID) || (x[1] == clientID)));
            NumberOfTeams -= teamsFromClient;
            Debug.Log("num teams = " + NumberOfTeams);
            if (NumberOfTeams < 0)
            {
                NumberOfTeams = 0;
                Debug.LogWarning("The number of teams went below 0.");
            }
            //TeamClientIDs.ForEach(x => x.ForEach(y => y = y == clientID? null : y));

            for (int i = 0; i < TeamClientIDs.Count; i++)
            {
                Debug.Log("[ " + (TeamClientIDs[i][0] == "" ? "null" : TeamClientIDs[i][0]) + " , " + (TeamClientIDs[i][1] == "" ? "null" : TeamClientIDs[i][1]) + " ]");
            }

            if (indicesOfTeamsThatLeft.Count > 0)
            {
                if (OnTeamsLeftRoom != null)
                {
                    OnTeamsLeftRoom(otherPlayer, indicesOfTeamsThatLeft, sharedTeamIndices);
                }
            }

            // for (int i = 0; i < LocalTeamIndices.Count; i++) {
            //     int amountToSubtract = 0;
            //     for (int j = 0; j < indicesOfTeamsThatLeft.Count; j++) {
            //         if (LocalTeamIndices[i] > indicesOfTeamsThatLeft[j]) {
            //             amountToSubtract++;
            //         }
            //     }
            //     LocalTeamIndices[i] -= amountToSubtract;
            // }

            // ClearEmptyTeamClientIDs();
        }

        if (OnClientLeftRoom != null)
        {
            OnClientLeftRoom(clientID, otherPlayer);
        }

        //RaiseEventOptions reo = new RaiseEventOptions { Receivers = ReceiverGroup.All, CachingOption = EventCaching.AddToRoomCache };
        //SendOptions so = new SendOptions { Reliability = true };
        //PhotonNetwork.RaiseEvent(VOICE_CHAT_DICT_REMOVE_USER_EVCODE, new object[] { otherPlayer.UserId }, reo, so);
    }

    private void VoiceChatDictRemoveUserEvent(EventData eventData) {
        if (eventData.Code == VOICE_CHAT_DICT_REMOVE_USER_EVCODE) {
            object[] content = (object[])eventData.CustomData;
            string playerUserId = (string)content[0];

            if(photonVoiceViewDictionary.ContainsKey(playerUserId)) {

                if (photonVoiceViewDictionary[playerUserId] != null) {
                    Destroy(photonVoiceViewDictionary[playerUserId]);
                }
                photonVoiceViewDictionary.Remove(playerUserId);
            }
        }
    }

    public void OnRaceFinishNetworkCleanUp() {

        for (int i = 0; i < LocalTeamIndices.Count; i++) {
            int amountToSubtract = 0;
            for (int j = 0; j < indicesOfTeamsThatLeftForCleanUp.Count; j++) {
                if (LocalTeamIndices[i] > indicesOfTeamsThatLeftForCleanUp[j]) {
                    amountToSubtract++;
                }
            }
            LocalTeamIndices[i] -= amountToSubtract;
        }

        indicesOfTeamsThatLeftForCleanUp.Clear();

        ClearEmptyTeamClientIDs();
    }
#endregion

#region Internal Helper Functions
    /// <summary>
    /// Generates a new random room code
    /// </summary>
    /// <returns></returns>
    private string GenerateNewRoomCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        string code = new string(Enumerable.Repeat(chars, 3)
          .Select(s => s[Random.Range(0, s.Length)]).ToArray());

        // Add region char to string
        code = regionCorrespondingChars[PhotonNetwork.CloudRegion].ToString() + code;

        return code;
    }
#endregion

    private void OnDestroy()
    {
        // Unsubscribe from events

        PhotonNetwork.NetworkingClient.EventReceived -= LoadingNewLevelEvent;
        PhotonNetwork.NetworkingClient.EventReceived -= ClientLoadedEvent;
        PhotonNetwork.NetworkingClient.EventReceived -= AddNumberOfTeamsEvent;
        PhotonNetwork.NetworkingClient.EventReceived -= SetNumberOfTeamsEvent;
        PhotonNetwork.NetworkingClient.EventReceived -= SetTeamNumbersClientIdEvent;
        PhotonNetwork.NetworkingClient.EventReceived -= RemoveTeamNumbersClientIdEvent;
        PhotonNetwork.NetworkingClient.EventReceived -= Matchmaking_StartLookingForLeaderEvent;
        PhotonNetwork.NetworkingClient.EventReceived -= Matchmaking_AddNumberOfTeamsEvent;
        PhotonNetwork.NetworkingClient.EventReceived -= SetTeamClientIDsFromMatchmakingQueueEvent;
        PhotonNetwork.NetworkingClient.EventReceived -= VoiceChatDictAddUserEvent;
        PhotonNetwork.NetworkingClient.EventReceived -= VoiceChatDictRemoveUserEvent;
        PhotonNetwork.NetworkingClient.EventReceived -= UpdateSteamPersonaOnOtherClientEvent;
    }
}
