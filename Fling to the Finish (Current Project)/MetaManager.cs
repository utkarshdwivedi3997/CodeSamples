#if UNITY_ANDROID || UNITY_IOS || UNITY_TIZEN || UNITY_TVOS || UNITY_WEBGL || UNITY_WSA || UNITY_PS4 || UNITY_WII || UNITY_XBOXONE || UNITY_SWITCH
#define DISABLESTEAMWORKS
# endif

using ExitGames.Client.Photon;
using Fling.Achievements;
using Fling.DLC;
using Fling.GameModes;
using Fling.Levels;
using Fling.Localization;
using Fling.Saves;
using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Realtime;
using Menus;

#if !DISABLESTEAMWORKS
using Steamworks;
#endif

public class MetaManager : Photon.Pun.MonoBehaviourPunCallbacks {

    /// <summary>
    /// Public static instance of MetaManager
    /// </summary>
    public static MetaManager Instance { get; set; }

    public bool LoadingNewScene { get; private set; } = false;

    //[TextArea]
    //public string GameVersion;

    [SerializeField] private GameVersion gameVersion;
    public GameVersion GameVersion => gameVersion;

    public bool Initialized { get; private set; }

    /// <summary>
    /// The current platform (Steam, Switch, etc.)
    /// </summary>
    public Platform CurrentPlatform { get; private set; }

    #region URLs
    private const string SIGN_UP_URL = "https://t.co/PzVm5H62yr";                       
    private const string TWITTER_URL = "https://twitter.com/SplitSideGames";
    private const string FACEBOOK_URL = "https://www.facebook.com/SplitSideGames/";
    private const string YOUTUBE_URL = "https://www.youtube.com/channel/UCf9sFb93w10epTmr-k4CJnQ";
    private const string DISCORD_JOIN_URL = "https://discord.gg/DCe9cfk";
    private const string SPLITSIDE_WEBSITE_URL = "https://www.splitsidegames.com/";
    private const string KICKSTARTER_PAGE_URL = "https://www.splitsidegames.com/kickstarter";
    private const string STEAM_STORE_PAGE_URL = "https://store.steampowered.com/app/1054430/Fling_to_the_Finish/";
    #endregion

    #region LevelsAndWorlds
    public List<LevelScriptableObject> AllLevels { get; private set; }
    /// <summary>
    /// List of all levels that are playable (in build index. locked levels are also playable, unless they are demo locked)
    /// </summary>
    public List<LevelScriptableObject> allPlayableLevels { get; private set; }
    [SerializeField]
    private List<WorldScriptableObject> allWorlds;
    /// <summary>
    /// List of all World scriptable objects
    /// </summary>
    public List<WorldScriptableObject> AllWorlds
    {
        get { return allWorlds; }
    }

    /// <summary>
    /// Dictionary of all PLAYABLE worlds (excludes <see cref="WorldType.MISCELLANEOUS"/>)
    /// </summary>
    public Dictionary<WorldType, WorldScriptableObject> AllWorldsDictionary { get; private set; }

    [Header("Miscellaneous levels")]
    [SerializeField] private LevelScriptableObject mainMenuScriptableObject;
    public LevelScriptableObject MainMenuScriptableObject { get { return mainMenuScriptableObject; } }
    [SerializeField] private LevelScriptableObject creditsRollScriptableObject;
    public LevelScriptableObject CreditsRollScriptableObject { get { return creditsRollScriptableObject; } }

    /// <summary>
    /// The currently loaded game level
    /// </summary>
    public LevelScriptableObject CurrentLevel { get; private set; }
    /// <summary>
    /// The level before the currently loaded game level
    /// </summary>
    public LevelScriptableObject PreviousLevel { get; private set; }
    /// <summary>
    /// The level after the currently loaded game level
    /// </summary>
    public LevelScriptableObject NextLevel { get; private set; }

    public bool isInTutorialLevel { get; private set; }

    /// <summary>
    /// Returns whether the player is currently in a playable level (any level that isn't the main menu or credits etc.)
    /// </summary>
    public bool IsInPlayableLevel { get; private set; }

    /// <summary>
    /// Returns whether the current loaded scene is main menu or not
    /// </summary>
    public bool IsInMainMenu { get { return (SceneManager.GetActiveScene().name == MainMenuScriptableObject.SceneName); } }
    #endregion

    #region GameModes
    [Header("Game Modes")]
    [SerializeField]
    private List<GameModeScriptableObject> AllGameModes;
    /// <summary>
    /// Dictionary of [ key = GameMode type, value = GameModeScriptableObject] for all game modes
    /// </summary>
    public Dictionary<GameMode, GameModeScriptableObject> AllGameModesDictionary { get; private set; }

    /// <summary>
    /// The current game mode
    /// </summary>
    public GameMode CurrentGameMode { get; private set; } = GameMode.Race;
    /// <summary>
    /// The GameModeScriptableObject for the CurrentGameMode
    /// </summary>
    public GameModeScriptableObject CurrentGameModeObject { get; private set; }

    /// <summary>
    /// Timer properties for the current game mode in the current level
    /// </summary>
    public RaceTimerPropertiesScriptableObject CurrentLevelGameModeTimerProperties { get; private set; }
    #endregion

    #region Miscellaneous
    [Header("Miscellaneous")]
    [SerializeField]
    private LoadingScreenControl loadingScreenControl;

    [SerializeField] private PopUpNotification notificationControl;
    /// <summary>
    /// The pop up notification manager
    /// </summary>
    public PopUpNotification NotificationControl { get { return notificationControl; } }

    [SerializeField] private PrivacyPolicy privacyPolicy;
    public PrivacyPolicy PrivacyPolicy => privacyPolicy;

    [SerializeField] private OnlineInformationNotifier onlineConnectivityNotifier;
    [SerializeField] private GenericCanvasFader canvasGroupFader;
    public GenericCanvasFader CanvasGroupFader => canvasGroupFader;

    [SerializeField] private CursorHandler cursorHandler;
    public CursorHandler CursorHandler => cursorHandler;

    public bool IsChineseBuild { get; private set; } = false;
    #endregion

    #region Editor Overrides
    [Header("Editor Play Overrides")]
    [SerializeField]
    private bool skipPreRaceTimer = false;
    /// <summary>
    /// Should use the start timer or no?
    /// </summary>
    public bool SkipPreRaceTimer { get { return skipPreRaceTimer; } }

    [SerializeField] private bool skipPreRaceCutscene = true;
    /// <summary>
    /// Should display the pre race cutscene or no?
    /// </summary>
    public bool SkipPreRaceCutscene { get { return skipPreRaceCutscene; } }

    [SerializeField] private GameMode gameModeInEditorPlay = GameMode.Race;
    /// <summary>
    /// GameMode to start a game from the editor play button in the level itself
    /// </summary>
    public GameMode GameModeInEditorPlay { get { return gameModeInEditorPlay; } }

    [SerializeField]
    private bool useSteam = false;
    /// <summary>
    /// Is Steam being used or not?
    /// </summary>
    public bool UseSteam { get { return useSteam; } }

    [SerializeField] private Regions forceRegion = Regions.NONE;
    /// <summary>
    /// If a specific region should be connected to for testing in that region
    /// </summary>
    public Regions ForceRegion { get { return forceRegion; } set { forceRegion = value; } }
    #endregion

    void Awake()
    {
        // Singleton instance
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }

        Initialized = false;
        LoadingNewScene = false;
        PhotonNetwork.AutomaticallySyncScene = false;
        if (!PhotonNetwork.IsConnected) {
            PhotonNetwork.OfflineMode = true;        // by default, start offline
        }
    }

    IEnumerator Start()
    {
        // Change PhotonView number as to not be destroyed ever again
        // photonView.ViewID = 999;
        CurrentPlatform = Platform.Steam;
        // Initialize leaderboard manager
        LeaderboardManager.Init(CurrentPlatform);
        AchievementsManager.Instance.Init(CurrentPlatform);
        DLCManager.Instance.Init(CurrentPlatform);

        // Initialize analytics manager
        AnalyticsManager.Init();
        onlineConnectivityNotifier.Init();

        // Handle event subscription
        SceneManager.sceneLoaded += OnNewSceneLoad;

        PhotonNetwork.NetworkingClient.EventReceived += LoadGameLevelUsingLevelNameEvent;
        PhotonNetwork.NetworkingClient.EventReceived += LoadGameLevelUsingSceneIndexEvent;
        PhotonNetwork.NetworkingClient.EventReceived += LoadMainMenuEvent;
#if !DISABLESTEAMWORKS
        SteamScript.Instance.OnGameJoinRequested += TryJoinRoom;
#endif

        // Make the AllWorldsDictionary and AllLevels list
        AllWorldsDictionary = new Dictionary<WorldType, WorldScriptableObject>();
        AllLevels = new List<LevelScriptableObject>();
        allPlayableLevels = new List<LevelScriptableObject>();

        foreach(WorldScriptableObject thisWorld in allWorlds)
        {
            if (thisWorld.World != WorldType.MISCELLANEOUS)
            {
                AllWorldsDictionary.Add(thisWorld.World, thisWorld);
            }

            foreach (LevelScriptableObject level in thisWorld.Levels)
            {
                level.InitValues();
                AllLevels.Add(level);

                if ((!DemoManager.Instance.IsDemo || !DemoManager.Instance.IsLevelDemoLocked(level)) && level.World != WorldType.MISCELLANEOUS)
                {
                    allPlayableLevels.Add(level);
                }
            }
        }

        // Make the all game modes list
        AllGameModesDictionary = new Dictionary<GameMode, GameModeScriptableObject>();
        foreach (GameModeScriptableObject mode in AllGameModes)
        {
            AllGameModesDictionary.Add(mode.Mode, mode);
        }

        if (CurrentLevel == null)
        {
            if (SceneManager.GetActiveScene().name != MainMenuScriptableObject.SceneName &&
                SceneManager.GetActiveScene().name != CreditsRollScriptableObject.SceneName)
            {
                Debug.Log("<color=green>PLAY MODE ENTERED FROM LEVEL IN EDITOR. Setting desired properties.</color>");

                CurrentLevel = GetCurrentLevel(SceneManager.GetActiveScene());
                //NextLevel = GetLevelAfter(CurrentLevel);
                NextLevel = GetDemoUnlockedLevelAfter(CurrentLevel);        // get the next level that is not permalocked in demos. Since demo permalocked levels are pretty much non-existent in the demo, we should get the next level by making sure it's not locked in demos
                PreviousLevel = GetLevelBefore(CurrentLevel);

                MenuData.LevelSelectData.GameMode = gameModeInEditorPlay;
                if (gameModeInEditorPlay != GameMode.Race && gameModeInEditorPlay != GameMode.NONE)
                {
                    MenuData.MainMenuData.PlayType = MenuData.PlayType.Campaign;
                }
                else
                {
                    MenuData.MainMenuData.PlayType = MenuData.PlayType.Race;
                }
                UpdateGameModeRelatedProperties(gameModeInEditorPlay);

                OnNewPlayableLevelLoaded?.Invoke(CurrentLevel, CurrentGameMode);
                IsInPlayableLevel = true;
            }
            else
            {
                IsInPlayableLevel = false;
            }
        }

        if (CurrentLevel != null)
        {
            isInTutorialLevel = CurrentLevel.SaveName.Equals("DesertTutorial") ? true : false;
        }
        else
        {
            isInTutorialLevel = false;
        }

        // Wait until localization manager is ready
        while (!LocalizationManager.Instance.IsReady)
        {
            yield return null;
        }

        while (!SteamScript.Initialized)
        {
            yield return null;
        }

        NetworkManager.Instance.OnDisconnectedFromNetwork += OnDisconnectedFromNetwork;
        Initialized = true;
    }

    public static event System.Action<LevelScriptableObject, GameMode> OnNewPlayableLevelLoaded;
    void OnNewSceneLoad(Scene scene, LoadSceneMode loadSceneMode)
    {
        // photonView.ViewID = 999;
        LoadingNewScene = false;
        IsInPlayableLevel = false;
        isInTutorialLevel = false;
        PhotonNetwork.IsMessageQueueRunning = true;

        NetworkManager.Instance.ClientLoaded();     // tell the network manager that this player has finished loading
        CurrentLevel = GetCurrentLevel(scene);

        if (SceneManager.GetActiveScene().name == MainMenuScriptableObject.SceneName ||
            SceneManager.GetActiveScene().name == creditsRollScriptableObject.SceneName)
        {
            return;
        }


        if (CurrentLevel == null)
        {
            Debug.LogWarning("<color = orange>Something's not right. Are you sure the current scene is in the All Worlds list?</color>");
            isInTutorialLevel = false;
            IsInPlayableLevel = false;
        }
        else
        {
            NextLevel = GetDemoUnlockedLevelAfter(CurrentLevel);
            PreviousLevel = GetLevelBefore(CurrentLevel);
            isInTutorialLevel = CurrentLevel.SaveName.Equals("DesertTutorial") ? true : false;
            IsInPlayableLevel = true;
        }

        UpdateGameModeRelatedProperties(MenuData.LevelSelectData.GameMode);

        if (CurrentLevel != CreditsRollScriptableObject)
        {
            OnNewPlayableLevelLoaded?.Invoke(CurrentLevel, CurrentGameMode);
        }
    }


    /* ============= Scene loading code ============= */
    #region SCENE_LOAD_UNLOAD
    /// <summary>
    /// Event that's fired when a new level load starts
    /// </summary>
    public System.Action OnNewLevelLoadStarted;

    /// <summary>
    /// Loads a game level. Fades the screen to black as a transition.
    /// </summary>
    /// <param name="level">Index of level to load</param>
    public void LoadGameLevel(int level)
    {
        CurrentLevel = null;

        if (PhotonNetwork.OfflineMode)
        {
            LoadGameLevelUsingSceneIndex(level);
        }
        else
        {
            Photon.Realtime.RaiseEventOptions reo = new Photon.Realtime.RaiseEventOptions { Receivers = Photon.Realtime.ReceiverGroup.All };
            SendOptions so = new SendOptions { Reliability = true };

            // KEEP IN MIND: We don't use the NetworkManager.Instance to get the event code because it's a const - it will NEVER change
            PhotonNetwork.RaiseEvent(NetworkManager.LOAD_LEVEL_USING_SCENE_INDEX_EVCODE, new object[] { level }, reo, so);
        }
    }

    private void LoadGameLevelUsingSceneIndex(int level)
    {
        StartCoroutine(LoadGameLevelCoroutine(level));
    }

    private void LoadGameLevelUsingSceneIndexEvent(EventData eventData)
    {
        if (eventData.Code == NetworkManager.LOAD_LEVEL_USING_SCENE_INDEX_EVCODE)
        {
            object[] content = (object[])eventData.CustomData;
            int level = (int)content[0];

            LoadGameLevelUsingSceneIndex(level);
        }
    }

    /// <summary>
    /// Loads a game level. Fades the screen to black as a transition.
    /// </summary>
    /// <param name="level">Name of level to load</param>
    public void LoadGameLevel(string level)
    {
        CurrentLevel = null;

        if (PhotonNetwork.OfflineMode)
        {
            LoadGameLevelUsingLevelName(level);
        }
        else
        {
            Photon.Realtime.RaiseEventOptions reo = new Photon.Realtime.RaiseEventOptions { Receivers = Photon.Realtime.ReceiverGroup.All };
            SendOptions so = new SendOptions { Reliability = true };

            // KEEP IN MIND: We don't use the NetworkManager.Instance to get the event code because it's a const - it will NEVER change
            PhotonNetwork.RaiseEvent(NetworkManager.LOAD_LEVEL_USING_SCENE_NAME_EVCODE, new object[] { level }, reo, so);
        }
    }

    /// <summary>
    /// Loads a game level. Fades the screen to black as a transition.
    /// </summary>
    /// <param name="level">LevelScriptableObject level to load</param>
    public void LoadGameLevel(LevelScriptableObject level, bool evenIfLocked = false)
    {
        if (evenIfLocked || SaveManager.Instance.IsLevelUnlocked(level, MenuData.MainMenuData.PlayType))
        {
            CurrentLevel = level;
            if (PhotonNetwork.OfflineMode)
            {
                LoadGameLevelUsingLevelName(level.SceneName);
            }
            else
            {
                Photon.Realtime.RaiseEventOptions reo = new Photon.Realtime.RaiseEventOptions { Receivers = Photon.Realtime.ReceiverGroup.All };
                SendOptions so = new SendOptions { Reliability = true };

                // KEEP IN MIND: We don't use the NetworkManager.Instance to get the event code because it's a const - it will NEVER change
                PhotonNetwork.RaiseEvent(NetworkManager.LOAD_LEVEL_USING_SCENE_NAME_EVCODE, new object[] { level.SceneName }, reo, so);
            }
        }
        else
        {
            Debug.Log("Level Not Unlocked");
        }
    }

    private void LoadGameLevelUsingLevelName(string level)
    {
        StartCoroutine(LoadGameLevelCoroutine(level));
    }

    private void LoadGameLevelUsingLevelNameEvent(EventData eventData)
    {
        if (eventData.Code == NetworkManager.LOAD_LEVEL_USING_SCENE_NAME_EVCODE)
        {
            object[] content = (object[])eventData.CustomData;
            string level = (string)content[0];

            LoadGameLevelUsingLevelName(level);
        }
    }

    /// <summary>
    /// Coroutine to be called by the public LoadGameLevel function.
    /// DO NOT call this from outside this script. Ever.
    /// </summary>
    /// <param name="level">Index of level to load</param>
    private IEnumerator LoadGameLevelCoroutine(int level)
    {
        if (!LoadingNewScene)
        {
            bool isOnline = !PhotonNetwork.OfflineMode;
            NetworkManager.Instance.LoadingNewLevel();      // tell NetworkManager.cs that a new level has started loading

            if (OnNewLevelLoadStarted != null)
            {
                OnNewLevelLoadStarted();
            }
            loadingScreenControl.StartLoading();

            // fix time
            Time.timeScale = 1f;
            Time.fixedDeltaTime = Time.timeScale * 0.02f;

            LoadingNewScene = true;
            //float fadeTime = GetComponent<Fade>().BeginFade(1);
            yield return new WaitForSeconds(loadingScreenControl.StartLoadTime);

            AsyncOperation operation = SceneManager.LoadSceneAsync(level);

            while (!operation.isDone)
            {
                float progress = operation.progress;

                yield return null;
            }

            if (isOnline)
            {
                while (!NetworkManager.Instance.AllClientsLoaded)
                {
                    yield return null;
                }

                if (IsInPlayableLevel)
                {
                    yield return WaitUntilOnlineRaceCanStart();
                }
            }

            loadingScreenControl.EndLoading();
        }
    }

    /// <summary>
    /// Coroutine to be called by the public LoadGameLevel function.
    /// DO NOT call this from outside this script. Ever.
    /// </summary>
    /// <param name="level">Name of scene to load</param>
    private IEnumerator LoadGameLevelCoroutine(string level)
    {
        if (!LoadingNewScene)
        {
            bool isOnline = !PhotonNetwork.OfflineMode;
            NetworkManager.Instance.LoadingNewLevel();      // tell NetworkManager.cs that a new level has started loading

            if (OnNewLevelLoadStarted != null)
            {
                OnNewLevelLoadStarted();
            }

            loadingScreenControl.StartLoading();

            // fix time
            Time.timeScale = 1f;
            Time.fixedDeltaTime = Time.timeScale * 0.02f;

            LoadingNewScene = true;
            //float fadeTime = GetComponent<Fade>().BeginFade(1);
            yield return new WaitForSeconds(loadingScreenControl.StartLoadTime);

            AsyncOperation operation = SceneManager.LoadSceneAsync(level);

            PhotonNetwork.IsMessageQueueRunning = false;
            while (!operation.isDone)
            {
                float progress = operation.progress;

                yield return null;
            }
            PhotonNetwork.IsMessageQueueRunning = true;

            if (isOnline)
            {
                while (!NetworkManager.Instance.AllClientsLoaded)
                {
                    yield return null;
                }

                if (IsInPlayableLevel)
                {
                    yield return WaitUntilOnlineRaceCanStart();
                }
            }

            loadingScreenControl.EndLoading();
        }
    }

    private IEnumerator WaitUntilOnlineRaceCanStart()
    {
        while (RaceManager.Instance == null)
        {
            yield return null;
        }

        while (!RaceManager.Instance.HasOnlineRaceSyncedTimerBeenHit)
        {
            yield return null;
        }
    }

    /* ============= Helpers for loading special levels ============= */

    /// <summary>
    /// Restarts the current level.
    /// </summary>
    public void Restart()
    {
        if (CurrentLevel != null)
        {
            LoadGameLevel(CurrentLevel);
            //StartCoroutine(LoadGameLevelCoroutine(CurrentLevel.SceneName));
        }
        else
        {
            Debug.LogWarning("Current Level is null in MetaManager. Cannot restart!");
        }
    }

    /// <summary>
    /// Loads the main menu.
    /// </summary>
    public void LoadMainMenu()
    {
        if (RaceManager.Instance != null) {
            if (RaceManager.Instance.IsPaused) {
                RaceManager.Instance.TogglePauseMenu();
            }
        }

        if (PhotonNetwork.OfflineMode) {
            NetworkManager.Instance.Disconnect();
        }

        MenuData.LobbyScreenData.ResetLobbyData();

        LoadGameLevel(mainMenuScriptableObject);
    }

    /// <summary>
    /// Loads the main menu to a certain state.
    /// </summary>
    public void LoadMainMenu(Menus.MenuState menuState)
    {
        if (PhotonNetwork.OfflineMode)
        {
            LoadMainMenuLogic((int)menuState);
        }
        else
        {
            Photon.Realtime.RaiseEventOptions reo = new Photon.Realtime.RaiseEventOptions { Receivers = Photon.Realtime.ReceiverGroup.All };
            SendOptions so = new SendOptions { Reliability = true };

            // KEEP IN MIND: We don't use the NetworkManager.Instance to get the event code because it's a const - it will NEVER change
            PhotonNetwork.RaiseEvent(NetworkManager.LOAD_MAIN_MENU_EVCODE, new object[] { (int)menuState }, reo, so);
        }
    }

    public void LoadMainMenuLogic(int menuState)
    {
        Menus.MenuState menuStateEnum = (Menus.MenuState)menuState;
        if (menuStateEnum < Menus.MenuState.LOBBY_SCREEN)
        {
            MenuData.LobbyScreenData.ResetLobbyData();
        }

        StartCoroutine(MainMenuStateCoroutine(menuStateEnum));
    }

    private void LoadMainMenuEvent(EventData eventData)
    {
        if (eventData.Code == NetworkManager.LOAD_MAIN_MENU_EVCODE)
        {
            object[] content = (object[])eventData.CustomData;
            int menuState = (int)content[0];

            LoadMainMenuLogic(menuState);
        }
    }

    /// <summary>
    /// Loads the unplayable credits scene
    /// </summary>
    public void LoadCreditsRoll()
    {
        LoadGameLevel(creditsRollScriptableObject);
    }

    /// <summary>
    /// Waits for main menu to load in couroutine, then sets the main menu to a certain state.
    /// </summary>
    private IEnumerator MainMenuStateCoroutine(Menus.MenuState menuState) {
        yield return StartCoroutine(LoadGameLevelCoroutine(mainMenuScriptableObject.SceneName));

        //if (PhotonNetwork.OfflineMode)
        //{
            Menus.MenuManager.Instance.MoveToMenuState(menuState);
        //}
        //else
        //{
        //    Menus.MenuManager.Instance.GetComponent<PhotonView>().RPC("MoveToMenuState", RpcTarget.All, (int)menuState);
        //}

        yield return 0;
    }

    /// <summary>
    /// Loads the next unlocked level
    /// </summary>
    public void LoadNextLevel()
    {
        LevelScriptableObject nextLevel;

        nextLevel = NextLevel;

        if (!SaveManager.Instance.IsLevelUnlocked(nextLevel, MenuData.MainMenuData.PlayType))
        {
            // Is the level locked?
            // Get the next unlocked level
            nextLevel = GetUnlockedLevelAfter(nextLevel);
        }

        LoadGameLevel(nextLevel);
    }

    /// <summary>
    /// Loads a random level that has been unlocked
    /// </summary>
    public void LoadRandomLevel(bool loadEvenIfLocked = false, bool loadTutorial = false)
    {
        Random.InitState(System.DateTime.Now.Millisecond);
        LevelScriptableObject level = GetRandomLevel(loadTutorial);
        LoadGameLevel(level, loadEvenIfLocked);
    }

    public void LoadNextTournamentRace()
    {
        int highestPlayedIndex = SaveManager.Instance.LeastLevelNotPlayedIndex;
        if (!PhotonNetwork.OfflineMode)
        {
            highestPlayedIndex = NetworkManager.Instance.LeastCommonLevelNotPlayedInCurrentRoom;
        }

        int levelToPlay = highestPlayedIndex;
        if (levelToPlay >= allPlayableLevels.Count) // all players have played all levels
        {
            // load any random level
            LoadRandomLevel(true);
        }
        else
        {
            LevelScriptableObject level = allPlayableLevels[levelToPlay];
            LoadGameLevel(level, true);
        }
    }

    /// <summary>
    /// Gets a random level.
    /// </summary>
    /// <param name="canBeTutorial">Should tutorial be returned as a random level or not?</param>
    /// <returns></returns>
    private LevelScriptableObject GetRandomLevel(bool canBeTutorial)
    {
        int startIdx = 0;
        if (!canBeTutorial)
        {
            startIdx = 1;
        }
        
        int allLevelCount = allPlayableLevels.Count;
        LevelScriptableObject level = null;
        while (level==null || level.World == WorldType.MISCELLANEOUS || level == CurrentLevel)
        {
            level = allPlayableLevels[Random.Range(startIdx, allLevelCount)];
        }

        return level;
    }
    #endregion

    #region INVITES
    private void TryJoinRoom(string rmCode)
    {
        if (IsInMainMenu)
        {
            // handled by lobby screen!
            return;
        }

        if (!PhotonNetwork.OfflineMode)
        {
            NetworkManager.Instance.Disconnect(() => TryJoinRoom(rmCode));
        }
        else
        {
            MenuData.MainMenuData.InviteRoomCode = rmCode;
            LoadMainMenu();
        }
    }
    #endregion

    #region ONLINE
    private void OnDisconnectedFromNetwork(DisconnectCause cause)
    {
        if (cause == DisconnectCause.DisconnectByClientLogic)
        {
            return; // if disconnected by player then we don't want to show disconnected message!
        }
        if (IsInMainMenu)
        {
            if (MenuManager.Is3DMenuState(MenuManager.Instance.CurrentMenuState))
            {
                // handled by 3d menus
                return;
            }

            MenuData.LobbyScreenData.ResetLobbyData();
        }

        NotificationControl.ShowErrorMessage(ErrorCodes.DISCONNECTED, LoadMainMenu, null, extraDisconnectCause: cause);
    }
    #endregion
    #region LOAD_EXTERNAL_LINKS
    /// <summary>
    /// Opens a browser with the sign up form
    /// </summary>
    public void OpenSignUp()
    {
        Application.OpenURL(SIGN_UP_URL);
    }

    /// <summary>
    /// Opens a browser with the Twitter link
    /// </summary>
    public void OpenTwitter()
    {
        Application.OpenURL(TWITTER_URL);
    }

    /// <summary>
    /// Opens a browser with the Facebook link
    /// </summary>
    public void OpenFacebook()
    {
        Application.OpenURL(FACEBOOK_URL);
    }

    /// <summary>
    /// Opens a browser with the YouTube link
    /// </summary>
    public void OpenYouTube()
    {
        Application.OpenURL(YOUTUBE_URL);
    }

    /// <summary>
    /// Opens a browser with the Discord link
    /// </summary>
    public void OpenDiscordJoin()
    {
        Application.OpenURL(DISCORD_JOIN_URL);
    }

    /// <summary>
    /// Opens a browser with the Kickstarter link
    /// </summary>
    public void OpenKickstarterPage()
    {
        Application.OpenURL(KICKSTARTER_PAGE_URL);
    }

    /// <summary>
    /// Opens a browser with the SplitSide website
    /// </summary>
    public void OpenWebsite()
    {
        Application.OpenURL(SPLITSIDE_WEBSITE_URL);
    }

    public void OpenStorePage()
    {
        Application.OpenURL(STEAM_STORE_PAGE_URL);
    }
#endregion

#region LEVEL_SCRIPTABLEOBJECT_CODE
    private LevelScriptableObject GetCurrentLevel(Scene scene)
    {
        foreach (LevelScriptableObject level in AllLevels)
        {
            if (scene.name == level.SceneName) // Found current level!
            {
                return level;
            }
        }
        return null;
    }

    /// <summary>
    /// Returns the next unlocked level AFTER the passed level
    /// </summary>
    /// <param name="level"></param>
    public LevelScriptableObject GetUnlockedLevelAfter(LevelScriptableObject level)
    {
        LevelScriptableObject nextLevel = GetLevelAfter(level);

        if (!SaveManager.Instance.IsLevelUnlocked(nextLevel, MenuData.MainMenuData.PlayType))
        {
            return GetUnlockedLevelAfter(nextLevel);
        }

        else return nextLevel;
    }

    /// <summary>
    /// Returns the next level (locked or unlocked) which is not permalocked by the demo
    /// </summary>
    /// <param name="level"></param>
    /// <returns></returns>
    public LevelScriptableObject GetDemoUnlockedLevelAfter(LevelScriptableObject level)
    {
        LevelScriptableObject nextLevel = GetLevelAfter(level);

        // If this is a demo (if not, this doesn't matter), and if the level is permalocked by the demo
        if (DemoManager.Instance.IsDemo && DemoManager.Instance.IsLevelDemoLocked(nextLevel))
        {
            // Get the next level that is not demolocked
            return GetDemoUnlockedLevelAfter(nextLevel);
        }

        else return nextLevel;
    }

    /// <summary>
    /// Get the LevelScriptableObject after the provided level
    /// </summary>
    /// <param name="level"></param>
    /// <returns>LevelScriptableObject levelAfter</returns>
    public LevelScriptableObject GetLevelAfter(LevelScriptableObject level)
    {
        LevelScriptableObject nextLevel;
        List<LevelScriptableObject> thisLevelList = AllWorldsDictionary[level.World].Levels;
        if (thisLevelList.Contains(level))
        {
            int thisLevelIndex = thisLevelList.IndexOf(level);
            int nextLevelIndex = thisLevelIndex + 1;

            if (nextLevelIndex >= thisLevelList.Count)  // no next level in this world
            {
                // Start first level of next world
                nextLevelIndex = 0;

                int thisWorldIndex = AllWorlds.IndexOf(AllWorldsDictionary[level.World]);
                int nextWorldIndex = thisWorldIndex + 1;

                if (nextWorldIndex >= AllWorldsDictionary.Count)    // no next world
                {
                    Debug.Log("Has no next world, returning FIRST level");
                    // Debug.Log(level.Name);

                    //nextLevel = creditsRollScriptableObject;
                    nextWorldIndex = 0;
                    nextLevel = AllWorlds[nextWorldIndex].Levels[nextLevelIndex];
                }
                else
                {
                    nextLevel = AllWorlds[nextWorldIndex].Levels[nextLevelIndex];
                }
            }
            else
            {
                nextLevel = thisLevelList[nextLevelIndex];
            }

            return nextLevel;
        }
        else
        {
            Debug.Log("Error!! Returning TUTORIAL level");
            nextLevel = AllWorlds[0].Levels[0];
            return nextLevel;
        }
    }

    /// <summary>
    /// Get the LevelScriptableObject before the provided level
    /// </summary>
    /// <param name="level"></param>
    /// <returns>LevelScriptableObject levelBefore</returns>
    public LevelScriptableObject GetLevelBefore(LevelScriptableObject level)
    {
        LevelScriptableObject prevLevel;
        List<LevelScriptableObject> thisLevelList = AllWorldsDictionary[level.World].Levels;
        if (thisLevelList.Contains(level))
        {
            int thisLevelIndex = thisLevelList.IndexOf(level);
            int prevLevelIndex = thisLevelIndex - 1;

            if (prevLevelIndex < 0)        // previous level is from the world before the current world
            {
                int thisWorldIndex = AllWorlds.IndexOf(AllWorldsDictionary[level.World]);
                int prevWorldIndex = thisWorldIndex - 1;

                if (prevWorldIndex < 0)
                {
                    Debug.Log("Has no previous world, returning original level");
                    prevLevel = level;
                }
                else
                {
                    prevLevelIndex = AllWorlds[prevWorldIndex].Levels.Count - 1;
                    prevLevel = AllWorlds[prevWorldIndex].Levels[prevLevelIndex];
                }
            }
            else
            {
                prevLevel = thisLevelList[prevLevelIndex];
            }
        }
        else
        {
            Debug.Log("No current level in MetaManager, returning current level");
            prevLevel = level;
        }

        return prevLevel;
    }

    #endregion

    #region GAMEMODE_RELATED_PROPERTIES
    private void UpdateGameModeRelatedProperties(GameMode mode)
    {
        CurrentGameMode = mode;

        if (CurrentGameMode != GameMode.NONE)
        {
            if (AllGameModesDictionary.ContainsKey(CurrentGameMode))
            {
                CurrentGameModeObject = AllGameModesDictionary[CurrentGameMode];
            }

            if (CurrentLevel.GameModeTimerProperties != null)
            {
                if (CurrentLevel.GameModeTimerProperties.ContainsKey(CurrentGameMode))
                {
                    CurrentLevelGameModeTimerProperties = CurrentLevel.GameModeTimerProperties[CurrentGameMode];
                }
                else
                {
                    Debug.LogError("Game mode timer properties not set for " + CurrentGameMode + " game mode in " + CurrentLevel.Name + "'s scriptable object");
                }
            }
            else
            {
                Debug.LogError("Game mode timer properties array not set in " + CurrentLevel.Name + "'s scriptable object");
            }
        }
    }
    #endregion

    public void ConvertToChineseBuild()
    {
        IsChineseBuild = true;
    }

    public void ConvertToROWBuild()
    {
        IsChineseBuild = false;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnNewSceneLoad;

        PhotonNetwork.NetworkingClient.EventReceived -= LoadGameLevelUsingLevelNameEvent;
        PhotonNetwork.NetworkingClient.EventReceived -= LoadGameLevelUsingSceneIndexEvent;
        PhotonNetwork.NetworkingClient.EventReceived -= LoadMainMenuEvent;

#if !DISABLESTEAMWORKS
        if (SteamScript.Instance != null)
        {
            SteamScript.Instance.OnGameJoinRequested -= TryJoinRoom;
        }
#endif

        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnDisconnectedFromNetwork -= OnDisconnectedFromNetwork;
        }
    }
}
