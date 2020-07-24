#if UNITY_ANDROID || UNITY_IOS || UNITY_TIZEN || UNITY_TVOS || UNITY_WEBGL || UNITY_WSA || UNITY_PS4 || UNITY_WII || UNITY_XBOXONE || UNITY_SWITCH
#define DISABLESTEAMWORKS
# endif

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Fling.Levels;
using Fling.Saves;
using Photon.Pun;
using Fling.GameModes;

#if !DISABLESTEAMWORKS
using Steamworks;
#endif

public class MetaManager : Photon.Pun.MonoBehaviourPunCallbacks {

    /// <summary>
    /// Public static instance of MetaManager
    /// </summary>
    public static MetaManager Instance { get; set; }

    private bool loadingNewScene = false;
    private bool isConnecting = false;
    public const int MAX_PLAYERS = 20;

    [TextArea]
    public string GameVersion;
    private string signUpURL = "https://t.co/PzVm5H62yr";                       
    private string twitterURL = "https://twitter.com/SplitSideGames";
    private string facebookURL = "https://www.facebook.com/SplitSideGames/";
    private string youtubeURL = "https://www.youtube.com/channel/UCf9sFb93w10epTmr-k4CJnQ";
    private string discordJoinURL = "https://discord.gg/DCe9cfk";
    private string splitsideGamesWebsiteURL = "https://www.splitsidegames.com/";
    private string kickstarterPageURL = "https://www.splitsidegames.com/kickstarter";

#if !DISABLESTEAMWORKS
    [HideInInspector]
    public AppId_t BaseGameSteamAppID = (AppId_t)1054430;
#endif

    public List<LevelScriptableObject> AllLevels { get; private set; }
    [SerializeField]
    private List<WorldScriptableObject> allWorlds;
    /// <summary>
    /// List of all World scriptable objects
    /// </summary>
    public List<WorldScriptableObject> AllWorlds
    {
        get { return allWorlds; }
    }
    public Dictionary<WorldType, WorldScriptableObject> AllWorldsDictionary { get; private set; }

    [Header("Miscellaneous levels")]
    [SerializeField] private LevelScriptableObject mainMenuScriptableObject;
    public LevelScriptableObject MainMenuScriptableObject { get { return mainMenuScriptableObject; } }
    [SerializeField] private LevelScriptableObject creditsRollScriptableObject;
    public LevelScriptableObject CreditsRollScriptableObject { get { return creditsRollScriptableObject; } }

    [Header("Game Modes")]
    [SerializeField]
    private List<GameModeScriptableObject> AllGameModes;
    /// <summary>
    /// Dictionary of [ key = GameMode type, value = GameModeScriptableObject] for all game modes
    /// </summary>
    public Dictionary<GameMode, GameModeScriptableObject> AllGameModesDictionary { get; private set; }

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

    [SerializeField]
    private bool useSteam = false;
    /// <summary>
    /// Is Steam being used or not?
    /// </summary>
    public bool UseSteam { get { return useSteam; } }

    [SerializeField] 

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

    /// <summary>
    /// The current game mode
    /// </summary>
    public GameMode CurrentGameMode { get; private set; }
    /// <summary>
    /// The GameModeScriptableObject for the CurrentGameMode
    /// </summary>
    public GameModeScriptableObject CurrentGameModeObject { get; private set; }

    public bool isInTutorialLevel { get; private set; }

    [SerializeField]
    private LoadingScreenControl loadingScreenControl;

    [SerializeField] private PopUpNotification notificationControl;
    /// <summary>
    /// The pop up notification manager
    /// </summary>
    public PopUpNotification NotificationControl { get { return notificationControl; } }

    /// <summary>
    /// Returns whether the current loaded scene is main menu or not
    /// </summary>
    public bool IsInMainMenu { get { return (SceneManager.GetActiveScene().name == MainMenuScriptableObject.SceneName); } }


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

        loadingNewScene = false;
        PhotonNetwork.AutomaticallySyncScene = true;
        if (!PhotonNetwork.IsConnected) {
            PhotonNetwork.OfflineMode = true;        // by default, start offline
        }
    }

    void Start()
    {
        // Change PhotonView number as to not be destroyed ever again
        // photonView.ViewID = 999;

        // Handle event subscription
        SceneManager.sceneLoaded += OnNewSceneLoad;
        
        // Make the AllWorldsDictionary and AllLevels list
        AllWorldsDictionary = new Dictionary<WorldType, WorldScriptableObject>();
        AllLevels = new List<LevelScriptableObject>();

        foreach(WorldScriptableObject thisWorld in allWorlds)
        {
            AllWorldsDictionary.Add(thisWorld.World, thisWorld);

            foreach (LevelScriptableObject level in thisWorld.Levels)
            {
                AllLevels.Add(level);
            }
        }

        // Make the all game modes list
        AllGameModesDictionary = new Dictionary<GameMode, GameModeScriptableObject>();
        foreach (GameModeScriptableObject mode in AllGameModes)
        {
            AllGameModesDictionary.Add(mode.Mode, mode);
        }

        SaveManager.Instance.allLevels = AllLevels;

        if (CurrentLevel == null)
        {
            Debug.LogWarning("Boio, I needed to enter this block of code. Good error catch hooo");
            if (SceneManager.GetActiveScene().name != MainMenuScriptableObject.SceneName)
            {
                CurrentLevel = GetCurrentLevel(SceneManager.GetActiveScene());
                //NextLevel = GetLevelAfter(CurrentLevel);
                NextLevel = GetDemoUnlockedLevelAfter(CurrentLevel);        // get the next level that is not permalocked in demos. Since demo permalocked levels are pretty much non-existent in the demo, we should get the next level by making sure it's not locked in demos
                PreviousLevel = GetLevelBefore(CurrentLevel);
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
    }

    void OnNewSceneLoad(Scene scene, LoadSceneMode loadSceneMode)
    {
        // photonView.ViewID = 999;
        loadingNewScene = false;

        if (SceneManager.GetActiveScene().name == MainMenuScriptableObject.SceneName)
        {
            return;
        }

        CurrentLevel = GetCurrentLevel(scene);

        if (CurrentLevel == null)
        {
            Debug.LogWarning("<color = orange>Something's not right. Are you sure the current scene is in the All Worlds list?</color>");
            isInTutorialLevel = false;
        }
        else
        {
            NextLevel = GetDemoUnlockedLevelAfter(CurrentLevel);
            PreviousLevel = GetLevelBefore(CurrentLevel);
            isInTutorialLevel = CurrentLevel.SaveName.Equals("DesertTutorial") ? true : false;
        }

        CurrentGameMode = MenuData.LevelSelectData.GameMode;
        if (CurrentGameMode != GameMode.NONE)
        {
            if (AllGameModesDictionary.ContainsKey(CurrentGameMode))
            {
                CurrentGameModeObject = AllGameModesDictionary[CurrentGameMode];
            }
        }

        NetworkManager.Instance.ClientLoaded();     // tell the network manager that this player has finished loading
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
        StartCoroutine(LoadGameLevelCoroutine(level));
    }

    /// <summary>
    /// Loads a game level. Fades the screen to black as a transition.
    /// </summary>
    /// <param name="level">Name of level to load</param>
    public void LoadGameLevel(string level)
    {
        CurrentLevel = null;
        StartCoroutine(LoadGameLevelCoroutine(level));
    }

    /// <summary>
    /// Loads a game level. Fades the screen to black as a transition.
    /// </summary>
    /// <param name="level">LevelScriptableObject level to load</param>
    public void LoadGameLevel(LevelScriptableObject level)
    {
        if (SaveManager.Instance.IsLevelUnlocked(level))
        {
            isConnecting = true;
            CurrentLevel = level;
            //if (PhotonNetwork.OfflineMode) {
                StartCoroutine(LoadGameLevelCoroutine(level.SceneName));
            //}
            /*else {
                if (PhotonNetwork.IsConnected && PhotonNetwork.IsMasterClient) {
                    Debug.Log("Loading level!");
                    PhotonNetwork.AutomaticallySyncScene = true;    // sync scene
                    PhotonNetwork.LoadLevel(level.SceneName);
                    // #Critical we need at this point to attempt joining a Random Room. If it fails, we'll get notified in OnJoinRandomFailed() and we'll create one.
                    //Photon.Pun.PhotonNetwork.JoinRandomRoom();
                }
                else {

                    /*Debug.Log("Connecting..."); // LogFeedback("Connecting...");
                    
                    // #Critical, we must first and foremost connect to Photon Online Server.
                    Photon.Pun.PhotonNetwork.GameVersion = GameVersion;
                    Photon.Pun.PhotonNetwork.ConnectUsingSettings();
                }
            }*/
        }
        else
        {
            Debug.Log("Level Not Unlocked");
        }
    }

    /// <summary>
    /// Coroutine to be called by the public LoadGameLevel function.
    /// DO NOT call this from outside this script. Ever.
    /// </summary>
    /// <param name="level">Index of level to load</param>
    private IEnumerator LoadGameLevelCoroutine(int level)
    {
        /*if (!loadingNewScene)
        {

            // fix time
            Time.timeScale = 1f;
            Time.fixedDeltaTime = Time.timeScale * 0.02f;

            loadingNewScene = true;
            float fadeTime = GetComponent<Fade>().BeginFade(1);
            yield return new WaitForSeconds(fadeTime);
            SceneManager.LoadScene(level);
        }*/

        if (!loadingNewScene)
        {
            NetworkManager.Instance.LoadingNewLevel();      // tell NetworkManager.cs that a new level has started loading
            if (OnNewLevelLoadStarted != null)
            {
                OnNewLevelLoadStarted();
            }
            loadingScreenControl.StartLoading();

            // fix time
            Time.timeScale = 1f;
            Time.fixedDeltaTime = Time.timeScale * 0.02f;

            loadingNewScene = true;
            //float fadeTime = GetComponent<Fade>().BeginFade(1);
            yield return new WaitForSeconds(loadingScreenControl.StartLoadTime);

            if (PhotonNetwork.OfflineMode)
            {
                AsyncOperation operation = SceneManager.LoadSceneAsync(level);

                while (!operation.isDone)
                {
                    float progress = operation.progress;

                    yield return null;
                }
            }
            else
            {
                PhotonNetwork.AutomaticallySyncScene = true;
                if (PhotonNetwork.IsMasterClient) {
                    Debug.Log("Loading level");
                    PhotonNetwork.LoadLevel(level);
                }

                while (PhotonNetwork.LevelLoadingProgress < 1.0f)
                {
                    yield return null;
                }

                while (!NetworkManager.Instance.AllClientsLoaded)
                {
                    yield return null;
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
        if (!loadingNewScene)
        {
            NetworkManager.Instance.LoadingNewLevel();      // tell NetworkManager.cs that a new level has started loading

            if (OnNewLevelLoadStarted != null)
            {
                OnNewLevelLoadStarted();
            }

            loadingScreenControl.StartLoading();

            // fix time
            Time.timeScale = 1f;
            Time.fixedDeltaTime = Time.timeScale * 0.02f;

            loadingNewScene = true;
            //float fadeTime = GetComponent<Fade>().BeginFade(1);
            yield return new WaitForSeconds(loadingScreenControl.StartLoadTime);

            if (PhotonNetwork.OfflineMode) {
                AsyncOperation operation = SceneManager.LoadSceneAsync(level);

                while (!operation.isDone)
                {
                    float progress = operation.progress;

                    yield return null;
                }
            }
            else {
                PhotonNetwork.AutomaticallySyncScene = true;
                if (PhotonNetwork.IsMasterClient) {
                    Debug.Log("Loading level");
                    PhotonNetwork.LoadLevel(level);
                }

                while (PhotonNetwork.LevelLoadingProgress < 1.0f)
                {
                    yield return null;
                }

                while (!NetworkManager.Instance.AllClientsLoaded)
                {
                    yield return null;
                }
            }

            loadingScreenControl.EndLoading();

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
            // LoadGameLevel(CurrentLevel);
            StartCoroutine(LoadGameLevelCoroutine(CurrentLevel.SceneName));
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
        /*if (RaceManager.Instance != null) {
            if (RaceManager.Instance.IsPaused) {
                RaceManager.Instance.TogglePauseMenu();
            }
        }*/

        if (menuState < Menus.MenuState.LOBBY_SCREEN)
        {
            MenuData.LobbyScreenData.ResetLobbyData();
        }

        StartCoroutine(MainMenuStateCoroutine(menuState));

        //LoadGameLevel(0);
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
        /*Scene currentScene = SceneManager.GetActiveScene();
        int buildIndex = currentScene.buildIndex;

        while (buildIndex != 0) {
            currentScene = SceneManager.GetActiveScene();
            buildIndex = currentScene.buildIndex;
            yield return 0;
        }*/
        yield return StartCoroutine(LoadGameLevelCoroutine(mainMenuScriptableObject.SceneName));

        if (PhotonNetwork.OfflineMode)
        {
            Menus.MenuManager.Instance.MoveToMenuState(menuState);
        }
        else
        {
            Menus.MenuManager.Instance.GetComponent<PhotonView>().RPC("MoveToMenuState", RpcTarget.All, (int)menuState);
        }
        

        yield return 0;
    }

    /// <summary>
    /// Loads the next unlocked level
    /// </summary>
    public void LoadNextLevel()
    {
        LevelScriptableObject nextLevel;

        nextLevel = NextLevel;

        if (!SaveManager.Instance.IsLevelUnlocked(nextLevel))
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
    public void LoadRandomLevel()
    {
        Random.seed = System.DateTime.Now.Millisecond;
        LevelScriptableObject level = GetRandomLevel();
        LoadGameLevel(level);
    }

    private LevelScriptableObject GetRandomLevel()
    {
        LevelScriptableObject level = null;
        while (level==null || level.World == WorldType.MISCELLANEOUS || !SaveManager.Instance.IsLevelUnlocked(level))
        {
            if (level != null)
            {
                if (level.World == WorldType.MISCELLANEOUS)
                {
                    Debug.Log("Level: " + level.Name + " | Not a race track... finding another level");
                }
                else if (!SaveManager.Instance.IsLevelUnlocked(level))
                {
                    Debug.Log("Level: " + level.Name + " | Level is locked... finding another level");
                }
            }

            level = AllLevels[Random.Range(0, AllLevels.Count)];
        }

        return level;
    }
#endregion

#region LOAD_EXTERNAL_LINKS
    /// <summary>
    /// Opens a browser with the sign up form
    /// </summary>
    public void OpenSignUp()
    {
        Application.OpenURL(signUpURL);
    }

    /// <summary>
    /// Opens a browser with the Twitter link
    /// </summary>
    public void OpenTwitter()
    {
        Application.OpenURL(twitterURL);
    }

    /// <summary>
    /// Opens a browser with the Facebook link
    /// </summary>
    public void OpenFacebook()
    {
        Application.OpenURL(facebookURL);
    }

    /// <summary>
    /// Opens a browser with the YouTube link
    /// </summary>
    public void OpenYouTube()
    {
        Application.OpenURL(youtubeURL);
    }

    /// <summary>
    /// Opens a browser with the Discord link
    /// </summary>
    public void OpenDiscordJoin()
    {
        Application.OpenURL(discordJoinURL);
    }

    /// <summary>
    /// Opens a browser with the Kickstarter link
    /// </summary>
    public void OpenKickstarterPage()
    {
        Application.OpenURL(kickstarterPageURL);
    }

    /// <summary>
    /// Opens a browser with the SplitSide website
    /// </summary>
    public void OpenWebsite()
    {
        Application.OpenURL(splitsideGamesWebsiteURL);
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

        if (!SaveManager.Instance.IsLevelUnlocked(nextLevel))
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
        if (DemoManager.Instance.isDemo && DemoManager.Instance.IsLevelDemoLocked(nextLevel))
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

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnNewSceneLoad;
    }
}
