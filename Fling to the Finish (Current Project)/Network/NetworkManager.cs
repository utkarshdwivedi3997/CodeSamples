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
using Hashtable = ExitGames.Client.Photon.Hashtable;
using Fling.Levels;
using Fling.GameModes;
using System;
using Menus;
using TMPro;
using UnityEngine.UI;
using Fling.Saves;
using Fling.Progression;

#if !DISABLESTEAMWORKS
using Steamworks;
using Fling.Seasons;
#endif

/// <summary>
/// The main manager for the networked portion of Fling to the Finish
/// </summary>
public partial class NetworkManager : MonoBehaviourPunCallbacks {

    public static NetworkManager Instance { get; private set; }

    #region Properties
    private bool isConnecting = false;                              // is the client attempting to connect to a network?
    private bool isConnected = false;
    public const byte MAX_CLIENTS = 8;                              // Max number of players allowed to join a room
    public const byte MAX_CLIENTS_CAMPAIGN = 2;                     // Max number of players allowed to join a CAMPAIGN room
    public const int MAX_TEAMS_ALLOWED = 4;                         // max number of teams allowed in a party
    public const int ROOM_CODE_LENGTH = 5;                          // length of room codes
    public const string DISCONNECTED_TEAM_CLIENT_ID = "DISCONNECTED";
    private int clientsLoaded = 0;                                  // number of players loaded in a level
    private Dictionary<string, bool> clientLoaded = new Dictionary<string, bool>();
    private Dictionary<string, Player> playerDictionary = new Dictionary<string, Player>();

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
    public int NumberOfClients { get { return (PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.PlayerCount : 1); } }

    /// <summary>
    /// Total number of teams across all clients in the current room, including single screen clients (1 team) and split screen clients (2 teams)
    /// </summary>
    public int NumberOfTeams { get; private set; }

    public List<int> indicesOfTeamsThatLeftForCleanUp;
    public List<int> indicesOfLocalPartyTeamsThatLeftForCleanup;
    private int numberOfDisconnectedTeams = 0;

    /// <summary>
    /// Indices of the highest level that is beaten per player (client ID) that is connected in the current room.
    /// </summary>
    public Dictionary<string, List<int>> LevelsNotPlayedByEachClient { get; private set; } = new Dictionary<string, List<int>>();

    /// <summary>
    /// Index of the least common level that is not beaten by at least one player in the current room
    /// </summary>
    public int LeastCommonLevelNotPlayedInCurrentRoom { get; private set; } = -1;

    #region CALLBACK_CODES
    // 0 <= Event code < 200 (Photon PUN2 itself uses the remaining 56 bytes)

    private const byte LOADING_NEW_LEVEL_EVCODE = 0;
    private const byte CLIENT_LOADED_EVCODE = 1;
    private const byte SET_NUM_OF_TEAMS_EVCODE = 2;
    private const byte ADD_NUM_OF_TEAMS_EVCODE = 3;
    private const byte SET_TEAM_NUMBERS_CLIENTID_EVCODE = 4;
    private const byte REMOVE_TEAM_NUMBERS_CLIENTID_EVCODE = 5;
    public const byte LOAD_LEVEL_USING_SCENE_INDEX_EVCODE = 6;
    public const byte LOAD_LEVEL_USING_SCENE_NAME_EVCODE = 7;
    public const byte LOAD_MAIN_MENU_EVCODE = 8;
    public const byte MATCHMAKING_PRIVATE_TO_MATCHMAKING_EVCODE = 9;
    public const byte MATCHMAKING_ADD_NUM_OF_TEAMS_EVCODE = 10;
    public const byte MATCHMAKING_SET_TEAM_CLIENT_IDS_EVCODE = 11;
    public const byte VOICE_CHAT_DICT_ADD_USER_EVCODE = 12;
    public const byte VOICE_CHAT_DICT_REMOVE_USER_EVCODE = 13;
    public const byte INTEREST_GROUP_SUBSCRIPTION_CHANGE_EVCODE = 14;
    public const byte UPDATE_HIGHEST_RACE_LEVEL_PLAYED_INDEX_EVCODE = 15;
    #endregion

    #region SQL_ROOM_OPTIONS_PROPERTIES
    public static readonly TypedLobby MATCHMAKING_SQL_LOBBY = new TypedLobby("matchmakingLobby", LobbyType.SqlLobby);

    // MATCHMAKING specific SQL Filter properties can only be C0-C9
    public const string MATCHMAKING_FILTER_TEAM_NUMBER = "C0";
    public const string ROOM_PROPERTY_PLAY_TYPE = "C1";
    public const string ROOM_PROPERTY_MENU_STATE = "MS";
    public const string ROOM_PROPERTY_IS_MATCHMAKING_ROOM = "MM";
    #endregion

    #region CUSTOM_PLAYER_PROPERTIES
    public const string PLAYER_PLATFORM_ID_PROPERTY = "PID";
    public const string PLAYER_CUSTOM_SHAREABLE_PROPS = "PCSP";
    public const string PLAYER_PING_PROPERTY = "PING";
    public const string PLAYER_CHOSEN_EVENT_TEAM = "PEVT";
    private const float PING_UPDATE_INTERVAL = 10f;
    public const int PING_THRESHOLD_POOR = 300;
    public const int PING_THRESHOLD_TERRIBLE = 450;
    #endregion

    #region INTEREST_GROUPS

    public enum InterestGroup : byte
    {
        InterestGroup1 = 1,     // start with 1. 0 is used by photon internally
        InterestGroup2,
        InterestGroup3,
        InterestGroup4,
        InterestGroup5,
        InterestGroup6,
        InterestGroup7,
        InterestGroup8,
        InterestGroup9,
        InterestGroup10,
        InterestGroup11,
        InterestGroup12,
        InterestGroup13,
        InterestGroup14,
        InterestGroup15,
        InterestGroup16,
        InterestGroup17,
        InterestGroup18,
        InterestGroup19,
        InterestGroup20
    }

    private Dictionary<InterestGroup, int> clientsSubscribedToEventGroups;
    private Dictionary<InterestGroup, bool> isClientSendingDataForInterestGroups;
    private Dictionary<InterestGroup, bool> isClientSubscribedToInterestGroup;
    #endregion

    #region NETWORK_REGIONS
    private readonly Dictionary<string, char> REGION_LETTER = new Dictionary<string, char>
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
        { "cn", 'M' },       // The Chinese Mainland servers have additional setup: https://doc.photonengine.com/en-us/pun/current/connection-and-authentication/regions#using_the_chinese_mainland_region
        { "tr", 'N' },
        { "za", 'O' },
    };

    private const string REGION_CHINA = "cn";
    private const string SERVER_CHINA = "ns.photonengine.cn";
    private const string PUN_APP_ID_CHINA = "2dd29dc7-2665-4813-9c4f-8ce0aaba68a8";
    private const string PUN_APP_ID_GLOBAL = "eb4dd32a-93eb-4278-a081-1de7fa95b7ae";

    private readonly Dictionary<MenuData.PlayType, char> PLAYTYPE_LETTER = new Dictionary<MenuData.PlayType, char>
    {
        {MenuData.PlayType.Campaign, 'C' },
        {MenuData.PlayType.Race, 'R' }
    };
    #endregion

    #region Party specific properties
    /// <summary>
    /// User IDs of players in the player's party
    /// </summary>
    public List<string> PartyUserIds { get; private set; }

    public List<int> LocalTeamIndices { get; set; }                 // Team indices IN PARTY. This is different from team indices in matchmaking queue.

    /// <summary>
    /// Is this client in the matchmaking queue right now?
    /// </summary>
    public bool IsMatchmaking { get; private set; }

    /// <summary>
    /// Is this client matchmaking and looking for their lobby leader?
    /// </summary>
    public bool IsLookingForLeader { get; private set; }
    private bool startedMatchmakingFromOnlineParty;
    private string partyLeaderID;
    /// <summary>
    /// Is this client the leader for their party?
    /// </summary>
    public bool IsPartyLeader { get; private set; }

    private string roomCode = "";

    public GameObject photonVoiceViewPrefab;
    private Dictionary<string, GameObject> photonVoiceViewDictionary = new Dictionary<string, GameObject>();

    public int NumberOfTeamsInLocalParty { get; private set; }
    #endregion

    #region Other connectivity related properties
    public RoomType RoomType { get; private set; } = RoomType.Offline;

    /// <summary>
    /// Dictionary of <clientId , FlingFriend>
    /// Each connected client Id will give a FlingFriend struct with details about that Friend (Friend via Steam, Switch, rando, etc.)
    /// </summary>
    public Dictionary<string, FlingFriend> FriendByClientId { get; private set; }

    private Dictionary<string, FriendListUI> friendUIToBeUpdated = new Dictionary<string, FriendListUI>();

    private Dictionary<string, Action<FlingFriend>> friendInformationUpdatedCallbackDict = new Dictionary<string, Action<FlingFriend>>(); 
    #endregion
    #endregion

    #region Awake and Start
    void Awake() {
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
        PhotonNetwork.NetworkingClient.EventReceived += InterestGroupSubscriptionChangedEvent;
        PhotonNetwork.NetworkingClient.EventReceived += UpdateHighestPlayedRaceLevelOnAllClientsEvent;

        MenuManager.OnMenuStateChanged += UpdateCurrentMenuStateRoomProperty;
        MenuData.MainMenuData.OnPlayTypeChanged += UpdateCurrentPlayTypeRoomProperty;
        ProgressionManager.OnPlayerLevelChanged += UpdatePlayerShareableProperties;
        BannersManager.OnEquippedBannerSettingsChanged += UpdatePlayerShareableProperties;
    }

    private void Start()
    {
        if (MetaManager.Instance != null)
        {
            MetaManager.Instance.OnNewLevelLoadStarted += OnNewLevelLoadStarted;
        }

        MetaManager.OnNewPlayableLevelLoaded += OnPlayableLevelLoaded;

        LocalTeamIndices = new List<int> { -1, -1 };
        indicesOfTeamsThatLeftForCleanUp = new List<int>();
        indicesOfLocalPartyTeamsThatLeftForCleanup = new List<int>();
        numberOfDisconnectedTeams = 0;

        roomCode = "";

        isConnecting = false;
        isConnected = false;
        IsMatchmaking = false;
        IsLookingForBothTypesOfMatchmaking = false;
        startedMatchmakingFromOnlineParty = false;
        IsLookingForLeader = false;
        partyLeaderID = "";
        //StartingTeamIndexForMyParty = 0;
        RoomType = RoomType.Offline;

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
        if (DevScript.Instance.AllowInGameDebugPanel || DevScript.Instance.DevMode)
        {
            if (MetaManager.Instance.ForceRegion != Regions.NONE)
            {
                region = MetaManager.Instance.ForceRegion.ToString().ToLower();
            }
        }

        // Set the game version
        PhotonNetwork.PhotonServerSettings.AppSettings.AppVersion = MetaManager.Instance.GameVersion.ToString();

        if (SaveManager.Instance.IsUsingChineseServers())
        {
            // If this build is working in China, use the local Chinese server and restrict to the Chinese region
            UseChinesePhotonSettings();
            region = REGION_CHINA;
        }
        else
        {
            UseGlobalPhotonSettings();
            if (region == REGION_CHINA)
            {
                JoinOrCreateRoomFailed(ErrorCodes.DISCONNECTED, "");
                return;
            }
        }

        isConnecting = true;
        PhotonNetwork.OfflineMode = false;

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

        PhotonNetwork.GameVersion = MetaManager.Instance.GameVersion.ToString();       // Set photon network game version. This has to be done after calling ConnectUsingSettings() for some reason :/. 
                                                                            // Also, yes there are two different things: PhotonNetwork.GameVersion and PhotonNetwork.PhotonServerSettings.AppSettings.AppVersion set above
    }

    private System.Action onDisconnectedCallback = null;
    /// <summary>
    /// Disconnect from Master Server
    /// </summary>
    public void Disconnect(System.Action onDisconnectedCallback = null)
    {
        if (isConnected)
        {
            this.onDisconnectedCallback = onDisconnectedCallback;
            Debug.Log("Disconnecting...");
            PhotonNetwork.Disconnect();
        }

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
        else if (rmCode.Length == ROOM_CODE_LENGTH)
        {
            string region = REGION_LETTER.FirstOrDefault(x => x.Value == rmCode[0]).Key;

            //MenuData.PlayType playType = PLAYTYPE_LETTER.FirstOrDefault(x => x.Value == rmCode[1]).Key;
            //MenuData.MainMenuData.PlayType = playType;  // override the playtype to be the same as the one from the connected client

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

    private void ConnectThenMatchmake()
    {
        if (!isConnecting)
        {
            IsMatchmaking = true;
            IsPartyLeader = true;
            startedMatchmakingFromOnlineParty = false;
            Connect();
        }
    }

    /// <summary>
    /// Creates a new room
    /// </summary>
    public void CreateRoom()
    {
        string rmCode = GenerateNewRoomCode();
        Debug.Log("Room code is " + rmCode);

        RoomOptions ro = new RoomOptions();

        int menuState = (int)MenuState.NONE;
        if (MetaManager.Instance.IsInMainMenu && MenuManager.Instance != null)
        {
            menuState = (int)MenuManager.Instance.CurrentMenuState;
        }
        int playType = (int)MenuData.MainMenuData.PlayType;

        ro.CustomRoomProperties = new Hashtable {
            { ROOM_PROPERTY_MENU_STATE, menuState },
            { ROOM_PROPERTY_PLAY_TYPE, playType },
            { ROOM_PROPERTY_IS_MATCHMAKING_ROOM, false }
        };
        ro.MaxPlayers = MAX_CLIENTS;
        ro.IsOpen = true;
        ro.IsVisible = false;
        ro.EmptyRoomTtl = 0;
        ro.PlayerTtl = 0;
        ro.PublishUserId = true;
        ro.CleanupCacheOnLeave = false;

        bool didCreateRoomRequestGoThrough = PhotonNetwork.CreateRoom(rmCode, ro, MATCHMAKING_SQL_LOBBY);
        if (!didCreateRoomRequestGoThrough)
        {
            JoinOrCreateRoomFailed(ErrorCodes.PHOTON_ERROR, "");
        }
    }

    /// <summary>
    /// Joins an existing room with a specified name
    /// </summary>
    /// <param name="roomName"></param>
    public void JoinRoom(string roomName)
    {
        // Need to reset this for the future
        PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = null;

        if (roomName.Length != ROOM_CODE_LENGTH)
        {
            Debug.Log("<color=red>Please provide a proper room name!</color>");
        }
        else
        {
            bool didJoinRequestGoThrough = PhotonNetwork.JoinRoom(roomName);
            if (!didJoinRequestGoThrough)
            {
                JoinOrCreateRoomFailed?.Invoke(ErrorCodes.PHOTON_ERROR, "Custom Photon Error");
            }
        }
    }

    public void HideRoom()
    {
        if (!PhotonNetwork.OfflineMode)
        {
            PhotonNetwork.CurrentRoom.IsVisible = false;
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
            PhotonNetwork.CurrentRoom.IsVisible = false;

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

    private void UpdateCurrentMenuStateRoomProperty(MenuState state)
    {
        if (PhotonNetwork.OfflineMode || !PhotonNetwork.IsMasterClient)
        {
            return;
        }

        int menuState = (int)state;
        if (MetaManager.Instance.IsInMainMenu && MenuManager.Instance != null)
        {
            menuState = (int)MenuManager.Instance.CurrentMenuState;
        }

        Hashtable changeRoomProps;
        changeRoomProps = new Hashtable {
            { ROOM_PROPERTY_MENU_STATE, menuState },
        };

        PhotonNetwork.CurrentRoom.SetCustomProperties(changeRoomProps);
    }

    private void UpdateCurrentPlayTypeRoomProperty(MenuData.PlayType playType)
    {
        if (PhotonNetwork.OfflineMode || !PhotonNetwork.IsMasterClient)
        {
            return;
        }

        int playTypeInt = (int)playType;

        Hashtable changeRoomProps;
        changeRoomProps = new Hashtable {
            { ROOM_PROPERTY_PLAY_TYPE, playTypeInt }
        };

        PhotonNetwork.CurrentRoom.SetCustomProperties(changeRoomProps);
    }

    /// <summary>
    /// Cleans up all Raise Event cache as well as buffered RPCs from this client
    /// </summary>
    public void ClearLocalPlayerBufferCache()
    {
        if (!PhotonNetwork.OfflineMode)
        {
            //RaiseEventOptions reo = new RaiseEventOptions { Receivers = ReceiverGroup.All, CachingOption = EventCaching.RemoveFromRoomCache };
            //SendOptions so = SendOptions.SendReliable;
            //PhotonNetwork.RaiseEvent(REMOVE_TEAM_NUMBERS_CLIENTID_EVCODE, new object[] { }, reo, so);
            //PhotonNetwork.RaiseEvent(SET_TEAM_NUMBERS_CLIENTID_EVCODE, new object[] { }, reo, so);
            //PhotonNetwork.RaiseEvent(UPDATE_STEAM_PERSONA_EVCODE, new object[] { }, reo, so);
            //PhotonNetwork.RemoveRPCs(PhotonNetwork.LocalPlayer);    // remove ALL RPCs for this player!      

            PhotonNetwork.OpRemoveCompleteCacheOfPlayer(PhotonNetwork.LocalPlayer.ActorNumber);
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

            //RaiseEventOptions reo = new RaiseEventOptions { Receivers = ReceiverGroup.All };
            //SendOptions so = new SendOptions { Reliability = true };
            //PhotonNetwork.RaiseEvent(LOADING_NEW_LEVEL_EVCODE, new object[] { }, reo, so);

            AllClientsLoaded = false;
            clientLoaded = new Dictionary<string, bool>();
            var players = PhotonNetwork.CurrentRoom.Players.Values;
            foreach (Player player in players)
            {
                if (!clientLoaded.ContainsKey(player.UserId))
                {
                    clientLoaded.Add(player.UserId, false);
                }
            }
            clientsLoaded = 0;
            // also clean up existing cache in the room!
            ClearLocalPlayerBufferCache();
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
            clientLoaded = new Dictionary<string, bool>();
            var players = PhotonNetwork.CurrentRoom.Players.Values;
            foreach (Player player in players)
            {
                if (!clientLoaded.ContainsKey(player.UserId))
                {
                    clientLoaded.Add(player.UserId, false);
                }
            }
            clientsLoaded = 0;
        }
    }

    //private void AdjustStartingTeamIndex()
    //{
    //    if (StartingTeamIndexForMyParty == 0)
    //    {
    //        return; // no need to adjust
    //    }

    //    int i = StartingTeamIndexForMyParty - 1;
    //    int numDisconnectedTeamsBeforeMyIndex = 0;
    //    for (; i >= 0; i--)
    //    {
    //        if (TeamClientIDs[i][0] != "" || TeamClientIDs[i][1] != "")
    //        {
    //            // not empty, don't count this team as it is still connected
    //            continue;
    //        }
    //        numDisconnectedTeamsBeforeMyIndex++;
    //    }

    //    StartingTeamIndexForMyParty -= numDisconnectedTeamsBeforeMyIndex;
    //}
    public void ClearEmptyTeamClientIDs()
    {
        ClearEmptyTeamClientIDsHelper(TeamClientIDs);

        void ClearEmptyTeamClientIDsHelper(List<List<string>> teamClientIDs)
        {
            if (teamClientIDs == null)
            {
                return;
            }
            teamClientIDs.RemoveAll(x => (x[0] == "" && x[1] == ""));       // remove all elements where both client IDs are null
        }

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

    public void PurgeDisconnectedTeamClientIDs()
    {
        PurgeDisconnectedClientIDsHelper(TeamClientIDs);

        void PurgeDisconnectedClientIDsHelper(List<List<string>> teamClientIDs)
        {
            if (teamClientIDs == null)
            {
                return;
            }

            teamClientIDs.ForEach(x =>
            {
                if (!string.IsNullOrEmpty(x[0]) && !string.IsNullOrEmpty(x[1]))
                {
                    // if this is a shared team --> BOTH clients have to be disconnected to be purged!
                    if (x[0].Contains(DISCONNECTED_TEAM_CLIENT_ID) && x[1].Contains(DISCONNECTED_TEAM_CLIENT_ID))
                    {
                        x[0] = "";
                        x[1] = "";
                    }
                }
                else
                {
                    // single client
                    if (x[0].Contains(DISCONNECTED_TEAM_CLIENT_ID))
                    {
                        x[0] = "";
                    }
                    if (x[1].Contains(DISCONNECTED_TEAM_CLIENT_ID))
                    {
                        x[1] = "";
                    }
                }
            });
        }
    }

    public void ResetTeamClientIDs()
    {
        TeamClientIDs = new List<List<string>>();
        for (int i = 0; i < MAX_TEAMS_ALLOWED; i++)
        {
            TeamClientIDs.Add(new List<string>());
            TeamClientIDs[i].Add("");
            TeamClientIDs[i].Add("");
        }

        //matchmakingQueueTeamClientIDs = new List<List<string>>();
        //StartingTeamIndexForMyParty = 0;
    }

    private void ResetFriendsList()
    {
        FriendByClientId = new Dictionary<string, FlingFriend>();
    }

    private void UpdatePlatformPersonaOnOtherClients()
    {
#if !DISABLESTEAMWORKS
        UpdateSteamPersonaOnOtherClients();
#endif
    }

    private void UpdateOtherClientPlatformPersona(Player targetPlayer, string platformId)
    {
#if !DISABLESTEAMWORKS
        UpdateOtherClientSteamPersona(targetPlayer, platformId);
#endif
    }

    /// <summary>
    /// Call this after joining a room. If that room has players, then this will update their platform (Steam/Switch etc.)
    /// personas if they are set and available via custom player properties
    /// </summary>
    private void UpdateAllExistingPlayerPersonasIfAvailable()
    {
        var players = PhotonNetwork.CurrentRoom.Players;
        foreach (KeyValuePair<int, Player> player in players)
        {
            if (player.Value.UserId == PhotonNetwork.LocalPlayer.UserId)
            {
                continue;
            }

            if (player.Value.CustomProperties.ContainsKey(PLAYER_PLATFORM_ID_PROPERTY))
            {
                UpdateOtherClientPlatformPersona(player.Value, (string)player.Value.CustomProperties[PLAYER_PLATFORM_ID_PROPERTY]);
            }

            if (FriendByClientId.ContainsKey(player.Value.UserId))
            {
                FlingFriend friend = FriendByClientId[player.Value.UserId];
                bool change = false;

                if (player.Value.CustomProperties.ContainsKey(PLAYER_CUSTOM_SHAREABLE_PROPS))
                {
                    friend.UpdateShareableCustomProperties((string)player.Value.CustomProperties[PLAYER_CUSTOM_SHAREABLE_PROPS]);
                    change = true;
                }
                if (player.Value.CustomProperties.ContainsKey(PLAYER_CHOSEN_EVENT_TEAM))
                {
                    int evtTeamInt = (int)player.Value.CustomProperties[PLAYER_CHOSEN_EVENT_TEAM];
                    friend.ChosenEventTeam = (SeasonalTeam)evtTeamInt;
                    change = true;
                }

                if (change)
                {
                    FriendByClientId[player.Value.UserId] = friend;
                }
            }
        }
    }
    #region Steam System Functions
#if !DISABLESTEAMWORKS

    private void UpdateSteamPersonaOnOtherClients()
    {
        CSteamID self = SteamUser.GetSteamID();
        string longSelf = self.m_SteamID.ToString();

        if (!PhotonNetwork.OfflineMode)
        {
            Hashtable customProps = new Hashtable { { PLAYER_PLATFORM_ID_PROPERTY, longSelf } };
            PhotonNetwork.SetPlayerCustomProperties(customProps);
        }
    }

    private void UpdateOtherClientSteamPersona(Player targetPlayer, string steamIdStr)
    {
        ulong ulongSteamId = ulong.Parse(steamIdStr);
        string clientId = targetPlayer.UserId;

        CSteamID SteamId = new CSteamID(ulongSteamId);
        SteamScript.Instance.RegisterToPerformActionWhenFlingFriendInformationIsAvailable(SteamId, (FlingFriend friend) => UpdateFlingFriendInformation(friend, clientId));
    }
#endif

    private void UpdateFlingFriendInformation(FlingFriend friend, string clientId)
    {
        if (!FriendByClientId.ContainsKey(clientId))
        {
            friend.clientId = clientId;

            string props = GetClientCustomDetails(clientId);
            if (!string.IsNullOrEmpty(props))
            {
                friend.UpdateShareableCustomProperties(props);
            }

            FriendByClientId.Add(clientId, friend);
        }

        UpdateFriendPlatformUI(clientId);
        FireFriendInformationAvailableCallback(clientId);
    }

    /// <summary>
    /// Registers the given text to update to the given client ID's corresponding PLATFORM (Steam/Switch) UI whenever it is available
    /// </summary>
    /// <param name="clientID"></param>
    /// <param name="playerNameText"></param>
    public void RegisterToUpdateFriendPlatformUIAvailable(string clientID, TextMeshProUGUI playerNameText = null, Image playerAvatar = null)
    {
        if (FriendByClientId.ContainsKey(clientID))
        {
            if (playerNameText != null)
            {
                playerNameText.text = FriendByClientId[clientID].Name;
            }
            if (playerAvatar != null)
            {
                playerAvatar.sprite = FriendByClientId[clientID].Avatar;
            }
        }
        else if (!friendUIToBeUpdated.ContainsKey(clientID))
        {
            friendUIToBeUpdated.Add(clientID, new FriendListUI(playerNameText, playerAvatar));
        }
    }

    /// <summary>
    /// Unregisters the given client's UI to update according to <see cref="RegisterToUpdateFriendPlatformUIAvailable(string, TextMeshProUGUI, Image)"/>
    /// </summary>
    /// <param name="clientID"></param>
    public void UnregisterToUpdateFriendPlatformUsernameWhenAvailable(string clientID)
    {
        if (friendUIToBeUpdated.ContainsKey(clientID))
        {
            friendUIToBeUpdated.Remove(clientID);
        }
    }

    private void UpdateFriendPlatformUI(string clientId)
    {
        if (friendUIToBeUpdated.ContainsKey(clientId) && FriendByClientId.ContainsKey(clientId))
        {
            FriendListUI ui = friendUIToBeUpdated[clientId];
            FlingFriend friend = FriendByClientId[clientId];
            ui.UpdateUI(friend);
            friendUIToBeUpdated.Remove(clientId);
        }
    }

    /// <summary>
    /// Registers a given callback to fire when user platform information for the given client ID is available
    /// </summary>
    /// <param name="clientID"></param>
    /// <param name="callback"></param>
    public void RegisterToGetFriendPlatformInformationWhenAvailable(string clientID, Action<FlingFriend> callback)
    {
        if (FriendByClientId.ContainsKey(clientID))
        {
            callback?.Invoke(FriendByClientId[clientID]);
        }
        else if (friendInformationUpdatedCallbackDict.ContainsKey(clientID))
        {
            friendInformationUpdatedCallbackDict[clientID] += callback;
        }
        else
        {
            friendInformationUpdatedCallbackDict.Add(clientID, callback);
        }
    }

    /// <summary>
    /// Unregisters a given callback to fire when user platform information for the given client ID is available
    /// </summary>
    /// <param name="clientID"></param>
    /// <param name="callback"></param>
    public void UnregisterToGetFriendPlatformInformationWhenAvailable(string clientID, Action<FlingFriend> callback)
    {
        if (friendInformationUpdatedCallbackDict.ContainsKey(clientID))
        {
            friendInformationUpdatedCallbackDict[clientID] -= callback;

            if (friendInformationUpdatedCallbackDict[clientID] == null)
            {
                friendInformationUpdatedCallbackDict.Remove(clientID);
            }
            else
            {
                var list = friendInformationUpdatedCallbackDict[clientID].GetInvocationList();
                if (list == null || list.Length <= 0)
                {
                    friendInformationUpdatedCallbackDict.Remove(clientID);
                }
            }
        }
    }

    private void FireFriendInformationAvailableCallback(string clientId)
    {
        if (friendInformationUpdatedCallbackDict.ContainsKey(clientId) && FriendByClientId.ContainsKey(clientId))
        {
            friendInformationUpdatedCallbackDict[clientId]?.Invoke(FriendByClientId[clientId]);
            friendInformationUpdatedCallbackDict[clientId] = null;
            friendInformationUpdatedCallbackDict.Remove(clientId);
        }
    }
    #endregion

    /// <summary>
    /// Returns the ping of a client
    /// </summary>
    /// <param name="clientId"></param>
    /// <returns></returns>
    public int GetClientPing(string clientId)
    {
        if (clientId == PhotonNetwork.LocalPlayer.UserId)
        {
            return PhotonNetwork.GetPing();
        }
        else if (PhotonNetwork.InRoom && playerDictionary.ContainsKey(clientId))
        {
            Player player = playerDictionary[clientId];
            if (player != null)
            {
                if (player.CustomProperties.ContainsKey(PLAYER_PING_PROPERTY))
                {
                    int ping = (int)player.CustomProperties[PLAYER_PING_PROPERTY];
                    return ping;
                }
            }
        }

        return 0;
    }

    /// <summary>
    /// Gets the clients' player level, equipped banner and sticker string
    /// </summary>
    /// <param name="clientId"></param>
    /// <returns></returns>
    private string GetClientCustomDetails(string clientId)
    {
        if (PhotonNetwork.InRoom && playerDictionary.ContainsKey(clientId))
        {
            Player player = playerDictionary[clientId];
            if (player != null)
            {
                if (player.CustomProperties.ContainsKey(PLAYER_CUSTOM_SHAREABLE_PROPS))
                {
                    string props = (string)player.CustomProperties[PLAYER_CUSTOM_SHAREABLE_PROPS];
                    return props;
                }
            }
        }

        return "";
    }

    /// <summary>
    /// Gets the client's chosen Seasonal Team for the community event
    /// </summary>
    /// <param name="clientId"></param>
    /// <returns></returns>
    public SeasonalTeam GetClientChosenEventTeam(string clientId)
    {
        if (PhotonNetwork.InRoom && playerDictionary.ContainsKey(clientId))
        {
            Player player = playerDictionary[clientId];
            if (player != null)
            {
                if (player.CustomProperties.ContainsKey(PLAYER_CHOSEN_EVENT_TEAM))
                {
                    int evtTeamInt = (int)player.CustomProperties[PLAYER_CHOSEN_EVENT_TEAM];
                    SeasonalTeam chosenTeam = (SeasonalTeam)evtTeamInt;
                    return chosenTeam;
                }
            }
        }

        return SeasonalTeam.None;
    }
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
            PhotonNetwork.RaiseEvent(CLIENT_LOADED_EVCODE, new object[] { PhotonNetwork.LocalPlayer.UserId }, reo, so);
        }
    }

    public static event System.Action OnAllClientsLoaded;
    /// <summary>
    /// The actual logic for the function for ClientLoaded()
    /// </summary>
    private void ClientLoadedEvent(EventData eventData)
    {
        if (eventData.Code == CLIENT_LOADED_EVCODE)
        {
            clientsLoaded++;
            object[] content = (object[])eventData.CustomData;
            string clientId = (string)content[0];
            if (clientLoaded.ContainsKey(clientId))
            {
                clientLoaded[clientId] = true;
            }
            CheckIfAllClientsHaveLoaded();
        }
    }

    private void CheckIfAllClientsHaveLoaded()
    {
        clientsLoaded = clientLoaded.Count(x => x.Value == true);
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

            OnAllClientsLoaded?.Invoke();
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
            NumberOfTeams = numToSet;
            NumberOfTeamsInLocalParty = numToSet;
        }
        else
        {
            NumberOfTeams = numToSet;
            NumberOfTeamsInLocalParty = numToSet;
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
            NumberOfTeamsInLocalParty = numOfTeams;
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
            object[] content = (object[])eventData.CustomData;
            int teamNumber = (int)content[0];
            string clientId = (string)content[1];
            int playerNum = (int)content[2];

            SetTeamNumbersClientIdLogic(teamNumber, clientId, playerNum);
        }
    }

    public void SetTeamNumbersClientIdLogic(int teamNumber, string clientId, int playerNum)
    {
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
            object[] content = (object[])eventData.CustomData;
            int teamNumber = (int)content[0];
            string clientId = (string)content[1];
            int playerNum = (int)content[2];

            RemoveTeamNumbersClientIdLogic(teamNumber, clientId, playerNum);
        }
    }

    public void RemoveTeamNumbersClientIdLogic(int teamNumber, string clientId, int playerNum)
    {
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

        Debug.Log("<color=red>Team 0 " + TeamClientIDs[0][0] + "," + TeamClientIDs[0][1] + "</color>");
        Debug.Log("<color=red>Team 1 " + TeamClientIDs[1][0] + "," + TeamClientIDs[1][1] + "</color>");
        Debug.Log("<color=red>Team 2 " + TeamClientIDs[2][0] + "," + TeamClientIDs[2][1] + "</color>");
        Debug.Log("<color=red>Team 3 " + TeamClientIDs[3][0] + "," + TeamClientIDs[3][1] + "</color>");
        //Debug.Log(teamClientIds[0].ToString());\
    }
    private void UpdateHighestPlayedRaceLevelOnAllClients()
    {
        if (!PhotonNetwork.OfflineMode)
        {
            // Stuff to do on all clients
            // photonView.RPC("RemoveTeamNumbersClientIdEvent", RpcTarget.AllBuffered, teamNumber, clientId, playerNum);

            RaiseEventOptions reo = new RaiseEventOptions { Receivers = ReceiverGroup.All, CachingOption = EventCaching.AddToRoomCache };
            SendOptions so = new SendOptions { Reliability = true };
            PhotonNetwork.RaiseEvent(UPDATE_HIGHEST_RACE_LEVEL_PLAYED_INDEX_EVCODE, new object[] { SaveManager.Instance.LevelsNotPlayedInAnyModeString, PhotonNetwork.LocalPlayer.UserId }, reo, so);
        }
    }

    private void UpdateHighestPlayedRaceLevelOnAllClientsEvent(EventData eventData)
    {
        if (eventData.Code == UPDATE_HIGHEST_RACE_LEVEL_PLAYED_INDEX_EVCODE)
        {
            object[] content = (object[])eventData.CustomData;
            string levelsPlayedString = (string)content[0];
            string clientId = (string)content[1];

            List<int> levels = new List<int>();
            if (!string.IsNullOrEmpty(levelsPlayedString))
            {
                string[] split = levelsPlayedString.Split(',');
                foreach (string levelStr in split)
                {
                    if (int.TryParse(levelStr, out int levelIdx))
                    {
                        levels.Add(levelIdx);
                    }
                }
            }

            if (LevelsNotPlayedByEachClient.ContainsKey(clientId))
            {
                LevelsNotPlayedByEachClient[clientId] = levels;
            }
            else
            {
                LevelsNotPlayedByEachClient.Add(clientId, levels);
            }

            UpdateHighestPlayedRaceLevelIndex();
        }
    }

    private void UpdateHighestPlayedRaceLevelIndex()
    {
        int leastLevelIdx = MetaManager.Instance.allPlayableLevels.Count;
        List<string> clients = LevelsNotPlayedByEachClient.Keys.ToList();
        foreach (string client in clients)
        {
            if (LevelsNotPlayedByEachClient[client].Count > 0)
            {
                int leastLevelNotPlayed = LevelsNotPlayedByEachClient[client][0];
                if (leastLevelIdx > leastLevelNotPlayed)
                {
                    leastLevelIdx = leastLevelNotPlayed;
                }
            }
        }
        LeastCommonLevelNotPlayedInCurrentRoom = leastLevelIdx;
    }
    #endregion

    #region Interest Group Helpers
    /// <summary>
    /// Subscribes to receive messages from the given interest group
    /// </summary>
    /// <param name="interestGroupToSubscribeTo"></param>
    public void SubscribeToInterestGroup(InterestGroup interestGroupToSubscribeTo)
    {
        PhotonNetwork.SetInterestGroups((byte)interestGroupToSubscribeTo, true);
        NotifyOthersOfInterestGroupSubscriptionChange(interestGroupToSubscribeTo, true);
        isClientSubscribedToInterestGroup[interestGroupToSubscribeTo] = true;
    }

    /// <summary>
    /// Unsubscribes to stop receiving messages from the given interest group
    /// </summary>
    /// <param name="interestGroupToUnsubscribeFrom"></param>
    public void UnsubscribeFromInterestGroup(InterestGroup interestGroupToUnsubscribeFrom)
    {
        PhotonNetwork.SetInterestGroups((byte)interestGroupToUnsubscribeFrom, false);
        NotifyOthersOfInterestGroupSubscriptionChange(interestGroupToUnsubscribeFrom, false);
        isClientSubscribedToInterestGroup[interestGroupToUnsubscribeFrom] = false;
    }

    public void SetToOnlyListenToDefaultInterestGroup()
    {
        InterestGroup[] values = (InterestGroup[])Enum.GetValues(typeof(InterestGroup));
        foreach (var value in values)
        {
            PhotonNetwork.SetInterestGroups((byte)value, false);
        }
        //PhotonNetwork.SetInterestGroups(new byte[0], null);
    }

    public void SetToOnlySendMessagesToDefaultInterestGroup()
    {
        InterestGroup[] values = (InterestGroup[])Enum.GetValues(typeof(InterestGroup));
        foreach (var value in values)
        {
            PhotonNetwork.SetSendingEnabled((byte)value, false);
        }
        //PhotonNetwork.SetSendingEnabled(new byte[0], null);
    }

    /// <summary>
    /// Starts sending messages to a given interest group
    /// </summary>
    /// <param name="group"></param>
    public void StartSendingDataToInterestGroup(InterestGroup group)
    {
        PhotonNetwork.SetSendingEnabled((byte)group, true);
        isClientSendingDataForInterestGroups[group] = true;
    }

    /// <summary>
    /// Stops sending messages to a given interest group
    /// </summary>
    /// <param name="group"></param>
    public void StopSendingDataToInterestGroup(InterestGroup group)
    {
        PhotonNetwork.SetSendingEnabled((byte)group, false);
        isClientSendingDataForInterestGroups[group] = false;
    }

    /// <summary>
    /// Notifies other clients that this client has started or stopped receiving messages from a given interest group
    /// </summary>
    /// <param name="interestGroup"></param>
    /// <param name="subscribed"></param>
    private void NotifyOthersOfInterestGroupSubscriptionChange(InterestGroup interestGroup, bool subscribed)
    {
        if (!PhotonNetwork.OfflineMode)
        {
            RaiseEventOptions reo = new RaiseEventOptions { Receivers = ReceiverGroup.All };
            SendOptions so = new SendOptions { Reliability = true };
            PhotonNetwork.RaiseEvent(INTEREST_GROUP_SUBSCRIPTION_CHANGE_EVCODE, new object[] { (byte)interestGroup, subscribed }, reo, so);
        }
    }

    /// <summary>
    /// Event received to get notified that another client has started or stopped receiving messages from a given interest group
    /// </summary>
    /// <param name="eventData"></param>
    private void InterestGroupSubscriptionChangedEvent(EventData eventData)
    {
        if (eventData.Code == INTEREST_GROUP_SUBSCRIPTION_CHANGE_EVCODE)
        {
            object[] content = (object[])eventData.CustomData;
            byte intGroup = (byte)content[0];
            bool subscribed = (bool)content[1];

            InterestGroup group = (InterestGroup)intGroup;

            if (subscribed)
            {
                clientsSubscribedToEventGroups[group]++;

                if (!isClientSendingDataForInterestGroups[group])
                {
                    StartSendingDataToInterestGroup(group);
                }
            }
            else
            {
                clientsSubscribedToEventGroups[group]--;
                if (clientsSubscribedToEventGroups[group] <= 0)
                {
                    clientsSubscribedToEventGroups[group] = 0;

                    if (isClientSendingDataForInterestGroups[group])
                    {
                        StopSendingDataToInterestGroup(group);
                    }
                }
            }
        }
    }

    private void ResetEventGroupSubscriptionDict()
    {
        clientsSubscribedToEventGroups = new Dictionary<InterestGroup, int>();
        isClientSendingDataForInterestGroups = new Dictionary<InterestGroup, bool>();
        isClientSubscribedToInterestGroup = new Dictionary<InterestGroup, bool>();

        foreach (InterestGroup intGrp in Enum.GetValues(typeof(InterestGroup)))
        {
            clientsSubscribedToEventGroups.Add(intGrp, 0);
            isClientSendingDataForInterestGroups.Add(intGrp, false);
            isClientSubscribedToInterestGroup.Add(intGrp, false);
        }
    }

    /// <summary>
    /// Returns whether this client is listening to incoming data in the specified interest group or not.
    /// </summary>
    /// <param name="group"></param>
    /// <returns></returns>
    public bool IsSubscribedToInterestGroup(InterestGroup group)
    {
        if (isClientSubscribedToInterestGroup.ContainsKey(group))
        {
            return isClientSubscribedToInterestGroup[group];
        }
        
        return false;
    }
    #endregion

    #region PUN Callbacks
    public override void OnConnectedToMaster()
    {
        // we don't want to do anything if we are not attempting to join a room. 
        // this case where isConnecting is false is typically when you lost or quit the game, when this level is loaded, OnConnectedToMaster will be called, in that case
        // we don't want to do anything.
        Debug.Log("OnConnectedToMaster");

        if (isConnecting)
        {
            Debug.Log("OnConnectedToMaster: This client is connected to the server.");

            isConnecting = false;
            isConnected = true;
            // Debug.Log("Server: " + PhotonNetwork.ServerAddress);
            // Debug.Log("Region: " + PhotonNetwork.CloudRegion);

            if (IsMatchmaking)
            {
                OnConnectedToMaster_ForMatchmaking();
            }
            else if (disconnectOnConnectedWhenMatchmakingStopped)
            {
                Disconnect(onDisconnectedCallback);
            }
            else
            {
                roomCode = "";
                ResetTeamClientIDs();
                ResetFriendsList();
            }
        }
        else if (!shouldSearchInOtherRegions)
        {
            roomCode = "";
        }
    }

    /// <summary>
    /// Use this sparingly! For almost all purposes, <see cref="JoinedRoom"/> will work.
    /// </summary>
    public event Action CreatedRoom;
    public event Action<string> OnCreatedMatchmakingRoom;
    public override void OnCreatedRoom()
    {
        base.OnCreatedRoom();

        Debug.Log("<color=green>OnCreatedRoom success.</color>");
        PhotonNetwork.CurrentRoom.MaxPlayers = MAX_CLIENTS;   // set a limit to how many clients can join this room
        //roomCode = PhotonNetwork.CurrentRoom.Name;
        //roomCodeText.text = "Room code: " + roomCode;
        if (IsMatchmaking)
        {
            OnCreatedMatchmakingRoom?.Invoke(PhotonNetwork.CloudRegion);
        }
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        base.OnCreateRoomFailed(returnCode, message);

        Debug.Log("<color=red>OnCreateRoomFailed</color> with message " + message);

        // Room with the same name already exists! Call create room again
        if (returnCode == ErrorCode.GameIdAlreadyExists)
        {
            if (IsMatchmaking)
            {
                CreateMatchmakingRoom();
            }
            else
            {
                CreateRoom();
            }
        }
        else
        {
            JoinOrCreateRoomFailed?.Invoke(returnCode, message);
        }
    }

    public delegate void OnJoinedRoomDelegate(string roomName);
    public event OnJoinedRoomDelegate JoinedRoom;
    public override void OnJoinedRoom()
    {
        Debug.Log("<Color=Green>OnJoinedRoom</Color> with " + Photon.Pun.PhotonNetwork.CurrentRoom.PlayerCount + " Player(s)");

        playerDictionary = new Dictionary<string, Player>();
        foreach (Player player in PhotonNetwork.CurrentRoom.Players.Values)
        {
            playerDictionary.Add(player.UserId, player);
        }

        UpdatePlayerShareableProperties();
        BeginUpdatingPing();
        UpdatePlatformPersonaOnOtherClients();
        UpdateHighestPlayedRaceLevelOnAllClients();
        UpdateChosenEventTeamOnOtherClients();

        shouldCreateMatchmakingRoom = false;

        Hashtable ht = PhotonNetwork.CurrentRoom.CustomProperties;
        bool isMm = (bool)ht[ROOM_PROPERTY_IS_MATCHMAKING_ROOM];

        if (isMm)
        {
            IsMatchmaking = true;
            partyLeaderID = PhotonNetwork.MasterClient.UserId;
            IsLookingForLeader = false;
        }

        // If isn't matchmaking
        if (!IsMatchmaking)
        {
            PartyUserIds = new List<string>();
            foreach (KeyValuePair<int, Player> player in PhotonNetwork.CurrentRoom.Players)
            {
                PartyUserIds.Add(player.Value.UserId);
            }
            roomCode = PhotonNetwork.CurrentRoom.Name;
        }
        // Else
        else
        {
            Matchmaking_JoinedRoom();
        }

        shouldSearchInOtherRegions = false;
        RoomType = IsMatchmaking ? RoomType.Matchmake : RoomType.Private;

        UpdateAllExistingPlayerPersonasIfAvailable();
         
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

    public delegate void JoinOrCreateRoomFailedDelegate(short returnCode, string message);
    public event JoinOrCreateRoomFailedDelegate JoinOrCreateRoomFailed;
    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        base.OnJoinRoomFailed(returnCode, message);

        Debug.Log("<color=red>OnJoinRoomFailed</color> with message " + message);

        //if (returnCode == ErrorCode.GameDoesNotExist)
        //{
        if (JoinOrCreateRoomFailed != null) JoinOrCreateRoomFailed(returnCode, message);
        Disconnect();
        //}
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        if (IsMatchmaking)
        {
            if (shouldSearchInOtherRegions)
            {
                Debug.Log("MM: JoinRandomFailed and will now search in the next region");
                Disconnect();
            }
            else
            {
                Debug.Log("MM: JoinRandomFailed and will now create a room");
                CreateMatchmakingRoom();
            }
        }
        else
        {
            OnJoinRoomFailed(returnCode, message);
        }
    }

    public static event Action LeftRoom;
    public override void OnLeftRoom()
    {
        base.OnLeftRoom();

        playerDictionary.Clear();

        if (IsMatchmaking)
        {
            isConnecting = true;
            //if (!IsLookingForLeader)
            //{
            //    Debug.Log("Left party to start matchmaking.");
            //}
            //else
            //{
            //    Debug.Log("Left party to find leader from matchmaking.");
            //}
        }

        LeftRoom?.Invoke();
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
        RoomType = RoomType.Offline;
        playerDictionary.Clear();

        StopUpdatingPing();

        if (IsMatchmaking && (shouldSearchInOtherRegions || shouldCreateMatchmakingRoom) && cause != DisconnectCause.Exception && cause != DisconnectCause.ExceptionOnConnect)
        {
            if (shouldSearchInOtherRegions)
            {
                OnDisconnected_ConnectToNextRegionForMatchmaking();
            }
            else if (currentMatchmakingCreateRoomAttempt < MAX_MATCHMAKING_CREATE_ROOM_ATTEMPTS)
            {
                OnDisconnected_CreateMatchmakingRoom();
            }
        }
        else
        {
            IsMatchmaking = false;
            IsLookingForBothTypesOfMatchmaking = false;
            startedMatchmakingFromOnlineParty = false;
            IsLookingForLeader = false;
            PartyUserIds = null;
            //StartingTeamIndexForMyParty = 0;
            IsPartyLeader = false;
            roomCode = "";
            numberOfDisconnectedTeams = 0;
            indicesOfTeamsThatLeftForCleanUp.Clear();
            indicesOfLocalPartyTeamsThatLeftForCleanup.Clear();
            shouldSearchInOtherRegions = false;
            //shouldCorrectTeamIDs = false;
            baseRegion = "";
            currentMatchmakingRegionIndex = -1;
            currentMatchmakingRegionConnectAttempt = 0;
            lastMatchmakingRegionConnectSuccess = false;
            shouldCreateMatchmakingRoom = false;
            shouldRestartRegionMatchSearchAfterSomeTime = false;
            currentMatchmakingCreateRoomAttempt = 0;
            disconnectOnConnectedWhenMatchmakingStopped = false;
            LevelsNotPlayedByEachClient.Clear();
            LeastCommonLevelNotPlayedInCurrentRoom = -1;
            NumberOfTeams = 0;
            MenuData.LobbyScreenData.Online.roomName = null;
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

            if (onDisconnectedCallback != null)
            {
                onDisconnectedCallback();
                onDisconnectedCallback = null;
            }
        }
    }

    private void OnDisconnected_ConnectToNextRegionForMatchmaking()
    {
        Debug.Log("MM: Disconnected from region " + currentlySearchingForMMInRegion.ToUpper());
        currentMatchmakingRegionConnectAttempt++;

        if (lastMatchmakingRegionConnectSuccess || currentMatchmakingRegionConnectAttempt >= MAX_MATCHMAKING_REGION_CONNECT_ATTEMPTS)
        {
            currentMatchmakingRegionIndex++;
        }

        lastMatchmakingRegionConnectSuccess = false;
        int idxOfBaseRegion = REGIONS_ARRAY.IndexOf(baseRegion);
        int[] distances = REGION_DISTANCES_MATRIX[idxOfBaseRegion];

        if (currentMatchmakingRegionIndex < distances.Length - 1)
        {
            int idxOfNextRegion = Array.IndexOf(distances, currentMatchmakingRegionIndex);
            string nextRegion = REGIONS_ARRAY[idxOfNextRegion];

            if (REGION_LETTER.ContainsKey(nextRegion))
            {
                Debug.Log("MM: Now connecting to next region to look for rooms");
                Connect(nextRegion);
            }
            else
            {
                OnDisconnected_ConnectToNextRegionForMatchmaking();
            }
        }
        else
        {
            // Stop looking for rooms in other regions and just create a room
            Debug.Log("MM: Cycled through all available regions and didn't find any available rooms. Will create a region in the best region.");
            shouldSearchInOtherRegions = false;
            shouldCreateMatchmakingRoom = true;
            shouldRestartRegionMatchSearchAfterSomeTime = true;
            currentMatchmakingCreateRoomAttempt = 1;
            Connect();
        }
    }

    private void OnDisconnected_CreateMatchmakingRoom()
    {
        currentMatchmakingCreateRoomAttempt++;
        if (currentMatchmakingCreateRoomAttempt >= MAX_MATCHMAKING_CREATE_ROOM_ATTEMPTS)
        {
            shouldCreateMatchmakingRoom = false;
            shouldRestartRegionMatchSearchAfterSomeTime = false;
        }
        Connect();
    }

    public delegate void OnClientEnteredRoomDelegate(Photon.Realtime.Player other);
    public event OnClientEnteredRoomDelegate OnClientEnteredRoom;
    public override void OnPlayerEnteredRoom(Photon.Realtime.Player other)
    {
        Debug.Log("OnPlayerEnteredRoom() " + other.NickName); // not seen if you're the player connecting
        if (PhotonNetwork.CurrentRoom.PlayerCount >= MAX_CLIENTS && PhotonNetwork.IsMasterClient)
        {
            Debug.Log("Max players in room. Closing the room.");
            CloseRoom();
        }

        if (!playerDictionary.ContainsKey(other.UserId))
        {
            playerDictionary.Add(other.UserId, other);
        }

        if (!IsMatchmaking)
        {
            PartyUserIds.Add(other.UserId);
        }
        else
        {
            Matchmaking_PlayerEnteredRoom(other);
        }

        if (OnClientEnteredRoom != null)
        {
            OnClientEnteredRoom(other);
        }
    }

    public delegate void OnClientLeftRoomDelegate(string clientID, Player otherPlayer);
    /// <summary>
    /// Event that fires when another player leaves the room
    /// </summary>
    public event OnClientLeftRoomDelegate OnClientLeftRoom;

    public delegate void OnTeamsLeftRoomDelegate(List<int> indicesOfTeamsThatLeft, List<int> sharedTeamIndices);
    /// <summary>
    /// Event that fires when a client leaves the room.
    /// Similar to OnClientLeftRoom, but this also passes a list of team indices for teams that left
    /// </summary>
    public event OnTeamsLeftRoomDelegate OnTeamsLeftRoom;
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);
        PhotonNetwork.OpRemoveCompleteCacheOfPlayer(otherPlayer.ActorNumber);

        string clientID = otherPlayer.UserId;

        if (clientLoaded.ContainsKey(clientID))
        {
            clientLoaded.Remove(clientID);
        }

        if (playerDictionary.ContainsKey(clientID))
        {
            playerDictionary.Remove(clientID);
        }

        if (!MetaManager.Instance.IsInMainMenu)
        {
            // not in main menu
            FindAllTeamsThatLeftRoom(clientID);
        }
        else if (MenuManager.Instance != null && MenuManager.Instance.CurrentMenuState > MenuState.LOBBY_SCREEN_3D)
        {
            FindAllTeamsThatLeftRoom(clientID);
        }

        if (LevelsNotPlayedByEachClient.ContainsKey(clientID))
        {
            LevelsNotPlayedByEachClient.Remove(clientID);
            UpdateHighestPlayedRaceLevelIndex();
        }

        if (OnClientLeftRoom != null)
        {
            OnClientLeftRoom(clientID, otherPlayer);
        }

        if (FriendByClientId.ContainsKey(clientID))
        {
            FriendByClientId.Remove(clientID);
        }

        UnregisterToUpdateFriendPlatformUsernameWhenAvailable(clientID);
    }

    public override void OnPlayerPropertiesUpdate(Player target, Hashtable changedProps)
    {
        if (target.UserId == PhotonNetwork.LocalPlayer.UserId)
        {
            return;
        }

        if (changedProps.ContainsKey(PLAYER_PLATFORM_ID_PROPERTY))
        {
            UpdateOtherClientPlatformPersona(target, (string)changedProps[PLAYER_PLATFORM_ID_PROPERTY]);
        }
        if (FriendByClientId.ContainsKey(target.UserId))
        {
            FlingFriend friend = FriendByClientId[target.UserId];
            bool changed = false;

            if (changedProps.ContainsKey(PLAYER_CUSTOM_SHAREABLE_PROPS))
            {
                friend.UpdateShareableCustomProperties((string)changedProps[PLAYER_CUSTOM_SHAREABLE_PROPS]);
                changed = true;
            }

            if (changedProps.ContainsKey(PLAYER_CHOSEN_EVENT_TEAM))
            {
                int evtTeamInt = (int)changedProps[PLAYER_CHOSEN_EVENT_TEAM];
                friend.ChosenEventTeam = (SeasonalTeam)evtTeamInt;
                changed = true;
            }

            if (changed)
            {
                FriendByClientId[target.UserId] = friend;
            }
        }

        base.OnPlayerPropertiesUpdate(target, changedProps);
    }

    private void FindAllTeamsThatLeftRoom(string clientID)
    {
        // I was using LINQ earlier, but it's apparently not as performant in Unity, so I'm not using it :(
        List<int> indicesOfTeamsThatLeft = new List<int>(); //TeamClientIDs.FindAllIndexOfConditionMet(x => (x[0] == clientID || x[1] == clientID));
        List<int> sharedTeamIndices = new List<int>();

        bool didThisClientShareATeamWithDisconnectedClient = false;

        string disconnectedClientId = DISCONNECTED_TEAM_CLIENT_ID + "|" + clientID;

        List<List<string>> teamClientIds = TeamClientIDs;

        for (int i = 0; i < teamClientIds.Count; i++)
        {
            // If team isn't shared by 2 clients
            if (teamClientIds[i][0] == clientID && (string.IsNullOrEmpty(teamClientIds[i][1]) || teamClientIds[i][1] == clientID))
            {
                // an entire team disconnected
                numberOfDisconnectedTeams++;
                indicesOfTeamsThatLeft.Add(i);
                indicesOfTeamsThatLeftForCleanUp.Add(i);

                if (RoomType == RoomType.Private || PartyUserIds.Contains(clientID))
                {
                    indicesOfLocalPartyTeamsThatLeftForCleanup.Add(i);
                }
            }

            // If team IS shared by 2 clients
            if (teamClientIds[i][0] != teamClientIds[i][1] &&
                (teamClientIds[i][0] == clientID && !string.IsNullOrEmpty(teamClientIds[i][1])) ||
                (teamClientIds[i][1] == clientID && !string.IsNullOrEmpty(teamClientIds[i][0])))
            {
                sharedTeamIndices.Add(i);

                if ((teamClientIds[i][0] == clientID && teamClientIds[i][1].Contains(DISCONNECTED_TEAM_CLIENT_ID)) ||
                    (teamClientIds[i][1] == clientID && teamClientIds[i][0].Contains(DISCONNECTED_TEAM_CLIENT_ID)))
                {
                    // if both clients from this team have disconnected, that means this team has disconnected entirely
                    numberOfDisconnectedTeams++;
                    indicesOfTeamsThatLeft.Add(i);
                    indicesOfTeamsThatLeftForCleanUp.Add(i);

                    if (RoomType == RoomType.Private || PartyUserIds.Contains(clientID))
                    {
                        indicesOfLocalPartyTeamsThatLeftForCleanup.Add(i);
                    }
                }

                if (teamClientIds[i].Contains(PhotonNetwork.LocalPlayer.UserId))
                {
                    didThisClientShareATeamWithDisconnectedClient = true;
                }
            }

            if (teamClientIds[i][0] == clientID)
            {
                teamClientIds[i][0] = disconnectedClientId;
            }

            if (teamClientIds[i][1] == clientID)
            {
                teamClientIds[i][1] = disconnectedClientId;
            }
        }

        for (int i = 0; i < teamClientIds.Count; i++)
        {
            Debug.Log("[ " + (teamClientIds[i][0] == "" ? "null" : teamClientIds[i][0]) + " , " + (teamClientIds[i][1] == "" ? "null" : teamClientIds[i][1]) + " ]");
        }

        if (didThisClientShareATeamWithDisconnectedClient)
        {
            if (MenuData.MainMenuData.PlayType == MenuData.PlayType.Campaign || 
                !TournamentManager.IsPlayingTournament() ||
                !TournamentManager.Instance.IsTournamentOver)
            {
                Disconnect(() => MetaManager.Instance.NotificationControl.ShowOKNotification("pop partner disconnected", MetaManager.Instance.LoadMainMenu, null));
            }
        }
        else
        {
            OnTeamsLeftRoom?.Invoke(indicesOfTeamsThatLeft, sharedTeamIndices);
            if (!MetaManager.Instance.IsInPlayableLevel || !AllClientsLoaded)
            {
                // if not in a race, OR in a race where all clients have no loaded yet, clean up disconnected players immediately!
                CleanUpDisconnectedPlayers();
                if (!AllClientsLoaded)
                {
                    CheckIfAllClientsHaveLoaded();
                }
            }
        }
    }
    private void VoiceChatDictRemoveUserEvent(EventData eventData) {
        if (eventData.Code == VOICE_CHAT_DICT_REMOVE_USER_EVCODE) {
            object[] content = (object[])eventData.CustomData;
            string playerUserId = (string)content[0];

            if (photonVoiceViewDictionary.ContainsKey(playerUserId)) {

                if (photonVoiceViewDictionary[playerUserId] != null) {
                    Destroy(photonVoiceViewDictionary[playerUserId]);
                }
                photonVoiceViewDictionary.Remove(playerUserId);
            }
        }
    }

    private void CleanUpDisconnectedPlayers()
    {
        if (indicesOfTeamsThatLeftForCleanUp == null)
        {
            return;
        }

        for (int i = 0; i < LocalTeamIndices.Count; i++)
        {
            int amountToSubtract = 0;
            for (int j = 0; j < indicesOfTeamsThatLeftForCleanUp.Count; j++)
            {
                if (LocalTeamIndices[i] > indicesOfTeamsThatLeftForCleanUp[j])
                {
                    amountToSubtract++;
                }
            }
            LocalTeamIndices[i] -= amountToSubtract;
        }

        bool isInMatchmakingScreen = MenuManager.Instance != null && MenuManager.Instance.CurrentMenuState == MenuState.MATCHMAKING;

        if (!isInMatchmakingScreen)
        {
            MenuData.LobbyScreenData.OnTeamsDisconnectedCleanup(indicesOfTeamsThatLeftForCleanUp);
            if (TournamentManager.IsPlayingTournament())
            {
                TournamentManager.Instance.AdjustPoints(indicesOfTeamsThatLeftForCleanUp);
            }
        }
        indicesOfTeamsThatLeftForCleanUp.Clear();
        indicesOfLocalPartyTeamsThatLeftForCleanup.Clear();

        PurgeDisconnectedTeamClientIDs();
        ClearEmptyTeamClientIDs();

        NumberOfTeams -= numberOfDisconnectedTeams;
        numberOfDisconnectedTeams = 0;
        Debug.Log("num teams = " + NumberOfTeams);
        if (NumberOfTeams < 0)
        {
            NumberOfTeams = 0;
            Debug.LogWarning("The number of teams went below 0.");
        }
    }

    private void OnPlayableLevelLoaded(LevelScriptableObject level, GameMode gameMode)
    {
        SetToOnlyListenToDefaultInterestGroup();
        SetToOnlySendMessagesToDefaultInterestGroup();
        ResetEventGroupSubscriptionDict();
        SubscribeToRaceEvents();
    }

    private void OnNewLevelLoadStarted()
    {
        if (PhotonNetwork.OfflineMode)
        {
            return;
        }

        if (MetaManager.Instance.IsInMainMenu && MenuManager.Instance.CurrentMenuState == MenuState.LOBBY_SCREEN_3D)
        {
            // no need for this before moving past the lobby screen!
            return;
        }

        CleanUpDisconnectedPlayers();
        UnsubscribeFromRaceEvents();
    }

    private void SubscribeToRaceEvents()
    {
        if (!PhotonNetwork.OfflineMode && TournamentManager.IsPlayingTournament())
        {
            if (RaceManager.Instance != null)
            {
                RaceManager.Instance.OnRaceFinish += TournamentRaceFinished;
            }
        }
    }

    private void UnsubscribeFromRaceEvents()
    {
        if (RaceManager.Instance != null)
        {
            RaceManager.Instance.OnRaceFinish -= TournamentRaceFinished;
        }
    }
    #endregion

    #region Internal Helper Functions
    /// <summary>
    /// Generates a new random room code
    /// The room code is a 4 letter long string
    /// The first letter denotes the region
    /// The second letter denotes the PlayType for lobby (Campaign/Race)
    /// The last 3 letters denote randomness for unique room names in that region
    /// </summary>
    /// <returns></returns>
    private string GenerateNewRoomCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        int lengthOfRandomCharacters = ROOM_CODE_LENGTH - 1; // -1 for region letter and another -1 for race/campaign
        string code = new string(Enumerable.Repeat(chars, lengthOfRandomCharacters)
          .Select(s => s[UnityEngine.Random.Range(0, s.Length)]).ToArray());

        // Add region char and playtype char to string
        code = REGION_LETTER[PhotonNetwork.CloudRegion].ToString() + code;

        return code;
    }

    public void UseChinesePhotonSettings()
    {
        PhotonNetwork.PhotonServerSettings.AppSettings.UseNameServer = true;
        PhotonNetwork.PhotonServerSettings.AppSettings.Server = SERVER_CHINA;
        PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = REGION_CHINA;
        PhotonNetwork.PhotonServerSettings.AppSettings.AppIdRealtime = PUN_APP_ID_CHINA;
        PhotonNetwork.PhotonServerSettings.AppSettings.AppIdChat = PUN_APP_ID_CHINA;
    }

    public void UseGlobalPhotonSettings()
    {
        PhotonNetwork.PhotonServerSettings.AppSettings.Server = "";
        PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = "";
        PhotonNetwork.PhotonServerSettings.AppSettings.AppIdRealtime = PUN_APP_ID_GLOBAL;
        PhotonNetwork.PhotonServerSettings.AppSettings.AppIdChat = PUN_APP_ID_GLOBAL;
    }

    private void UpdatePlayerShareableProperties()
    {
        if (!PhotonNetwork.OfflineMode)
        {
            string props = ProgressionManager.Instance.GetShareablePlayerCustomizationString();
            Hashtable customProps = new Hashtable { { PLAYER_CUSTOM_SHAREABLE_PROPS, props } };
            PhotonNetwork.SetPlayerCustomProperties(customProps);
        }
    }

    private void UpdateChosenEventTeamOnOtherClients()
    {
        if (TeamBasedSeasonalEventManager.IsEventOngoing && TeamBasedSeasonalEventManager.Instance.HasInitialized
            && TeamBasedSeasonalEventManager.Instance.HasChosenTeam)
        {
            if (!PhotonNetwork.OfflineMode)
            {
                Hashtable customProps = new Hashtable { { PLAYER_CHOSEN_EVENT_TEAM, (int)TeamBasedSeasonalEventManager.Instance.ChosenTeam } };
                PhotonNetwork.SetPlayerCustomProperties(customProps);
            }
        }
    }

    /// <summary>
    /// Starts updating the current player's ping to the custom room properties every <see cref="PING_UPDATE_INTERVAL"/> seconds
    /// </summary>
    private void BeginUpdatingPing()
    {
        StopUpdatingPing();
        InvokeRepeating(nameof(UpdatePingToServer), 0, PING_UPDATE_INTERVAL);
    }

    /// <summary>
    /// Stops updating the current player's ping to the custom room properties
    /// </summary>
    private void StopUpdatingPing()
    {
        if (IsInvoking(nameof(UpdatePingToServer)))
        {
            CancelInvoke(nameof(UpdatePingToServer));
        }
    }

    /// <summary>
    /// Updates the current player's ping to the custom room properties
    /// </summary>
    private void UpdatePingToServer()
    {
        if (!PhotonNetwork.OfflineMode)
        {
            Hashtable customProps = new Hashtable { { PLAYER_PING_PROPERTY, PhotonNetwork.GetPing()} };
            PhotonNetwork.SetPlayerCustomProperties(customProps);
        }
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
        PhotonNetwork.NetworkingClient.EventReceived -= InterestGroupSubscriptionChangedEvent;
        PhotonNetwork.NetworkingClient.EventReceived -= UpdateHighestPlayedRaceLevelOnAllClientsEvent;

        MenuManager.OnMenuStateChanged -= UpdateCurrentMenuStateRoomProperty;
        MenuData.MainMenuData.OnPlayTypeChanged -= UpdateCurrentPlayTypeRoomProperty;
        ProgressionManager.OnPlayerLevelChanged -= UpdatePlayerShareableProperties;
        BannersManager.OnEquippedBannerSettingsChanged -= UpdatePlayerShareableProperties;

        if (MetaManager.Instance != null)
        {
            MetaManager.Instance.OnNewLevelLoadStarted -= OnNewLevelLoadStarted;
        }
        MetaManager.OnNewPlayableLevelLoaded -= OnPlayableLevelLoaded;

        if (RaceManager.Instance != null)
        {
            RaceManager.Instance.OnRaceFinish -= TournamentRaceFinished;
        }
    }

    private struct FriendListUI
    {
        private Image avatar;
        private TextMeshProUGUI playerName;
        public FriendListUI(TextMeshProUGUI playerName, Image playerImage)
        {
            this.avatar = playerImage;
            this.playerName = playerName;
        }

        public void UpdateUI(FlingFriend friend)
        {
            if (playerName != null)
            {
                playerName.text = friend.Name;
            }

            if (avatar != null)
            {
                avatar.sprite = friend.Avatar;
            }
        }
    }
}

public enum RoomType
{
    Offline,
    Private,
    Matchmake
}

public enum Regions
{
    NONE,
    ASIA,
    AU,
    CAE,
    EU,
    IN,
    JP,
    RU,
    RUE,
    ZA,
    SA,
    KR,
    TR,
    US,
    USW,
    CN
}