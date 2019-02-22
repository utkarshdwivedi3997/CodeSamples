using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Fling.Levels;
using Fling.Saves;
using Photon.Pun;

public class MetaManager : Photon.Pun.MonoBehaviourPunCallbacks {
    private bool loadingNewScene = false;
    private bool isConnecting = false;
    public const int MAX_PLAYERS = 20;

    [TextArea]
    public string GameVersion;
    private string signUpURL = "https://t.co/PzVm5H62yr";
    private string twitterURL = "https://twitter.com/SplitSideGames";
    private string facebookURL = "https://www.facebook.com/SplitSideGames/";
    private string youtubeURL = "https://www.youtube.com/channel/UCf9sFb93w10epTmr-k4CJnQ";
    private string splitsideGamesWebsiteURL = "https://www.splitsidegames.com/";

    /// <summary>
    /// Public static instance of MetaManager
    /// </summary>
    public static MetaManager Instance { get; set; }

    public Menus.MenuState_CharacterSelect.PlayerSelectData[] playerSelectData;
    public LevelAbilityMode levelAbilityMode;

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
    [SerializeField]
    private List<Character> allCharacters;
    /// <summary>
    /// List of all character scriptable objects
    /// </summary>
    public List<Character> AllCharacters
    {
        get { return allCharacters; }
    }

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

    [SerializeField]
    private LoadingScreenControl loadingScreenControl;

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

        DontDestroyOnLoad(gameObject);

        loadingNewScene = false;
        PhotonNetwork.AutomaticallySyncScene = true;
        if (!PhotonNetwork.IsConnected) {
            PhotonNetwork.OfflineMode = true;        // by default, start offline
        }
    }

    void Start()
    {
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

        SaveManager.Instance.allLevels = AllLevels;
        SaveManager.Instance.allCharacters = AllCharacters;

        if (DemoManager.Instance.isDemo)
        {
            SaveManager.Instance.defaultUnlockedCharacters = AllCharacters;
        }

        if (CurrentLevel == null)
        {
            Debug.LogWarning("Boio, I needed to enter this block of code. Good error catch hooo");
            if (SceneManager.GetActiveScene().buildIndex != 0) {
                CurrentLevel = GetCurrentLevel(SceneManager.GetActiveScene());
                NextLevel = GetLevelAfter(CurrentLevel);
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
        loadingNewScene = false;

        CurrentLevel = GetCurrentLevel(scene);

        if (CurrentLevel == null)
        {
            Debug.LogWarning("<color = orange>Something's not right. Are you sure the current scene is in the All Worlds list?</color>");
            isInTutorialLevel = false;
        }
        else
        {
            NextLevel = GetLevelAfter(CurrentLevel);
            PreviousLevel = GetLevelBefore(CurrentLevel);
            isInTutorialLevel = CurrentLevel.SaveName.Equals("DesertTutorial") ? true : false;
        }

        NetworkManager.Instance.PlayerLoaded();     // tell the network manager that this player has finished loading
    }


    /* ============= Scene loading code ============= */
    #region SCENE_LOAD_UNLOAD
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

    /*#region PUN Callbacks
    public override void OnConnectedToMaster()
    {
        // we don't want to do anything if we are not attempting to join a room. 
        // this case where isConnecting is false is typically when you lost or quit the game, when this level is loaded, OnConnectedToMaster will be called, in that case
        // we don't want to do anything.
        if (isConnecting)
        {
            Debug.Log("OnConnectedToMaster: Next -> try to Join Random Room");
            Debug.Log("PUN Basics Tutorial/Launcher: OnConnectedToMaster() was called by PUN. Now this client is connected and could join a room.\n Calling: PhotonNetwork.JoinRandomRoom(); Operation will fail if no room found");
    
            // #Critical: The first we try to do is to join a potential existing room. If there is, good, else, we'll be called back with OnJoinRandomFailed()
            Photon.Pun.PhotonNetwork.JoinRandomRoom();
        }
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.Log("<Color=Red>OnJoinRandomFailed</Color>: Next -> Create a new Room");
        Debug.Log("PUN Basics Tutorial/Launcher:OnJoinRandomFailed() was called by PUN. No random room available, so we create one.\nCalling: PhotonNetwork.CreateRoom");

        // #Critical: we failed to join a random room, maybe none exists or they are all full. No worries, we create a new room.
        Photon.Pun.PhotonNetwork.CreateRoom(null, new Photon.Realtime.RoomOptions { MaxPlayers = MAX_PLAYERS});
    }

    public override void OnDisconnected(Photon.Realtime.DisconnectCause cause)
    {
        Debug.Log("<Color=Red>OnDisconnected</Color> "+cause);
        Debug.LogError("PUN Basics Tutorial/Launcher:Disconnected");

        isConnecting = false;
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("<Color=Green>OnJoinedRoom</Color> with "+Photon.Pun.PhotonNetwork.CurrentRoom.PlayerCount+" Player(s)");
        Debug.Log("PUN Basics Tutorial/Launcher: OnJoinedRoom() called by PUN. Now this client is in a room.\nFrom here on, your game would be running.");
    
        // #Critical: We only load if we are the first player, else we rely on  PhotonNetwork.AutomaticallySyncScene to sync our instance scene.
        if (Photon.Pun.PhotonNetwork.CurrentRoom.PlayerCount == 1)
        {
            Debug.Log("We load " + CurrentLevel.SceneName);

            // #Critical
            // Load the Room Level. 
            // Photon.Pun.PhotonNetwork.LoadLevel("PunBasics-Room for 1");
            StartCoroutine(LoadGameLevelCoroutine(CurrentLevel.SceneName));

        }
    }

    public override void OnPlayerEnteredRoom(Photon.Realtime.Player other)
    {
        Debug.Log("OnPlayerEnteredRoom() " + other.NickName); // not seen if you're the player connecting

        // if (PhotonNetwork.IsMasterClient)
        // {
            // Debug.LogFormat("OnPlayerEnteredRoom IsMasterClient {0}", PhotonNetwork.IsMasterClient); // called before OnPlayerLeftRoom

            Restart();
        // }
    }
    #endregion*/

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

            loadingScreenControl.StartLoading();
            if (!PhotonNetwork.OfflineMode)
            {
                loadingScreenControl.photonView.RPC("StartLoading", RpcTarget.Others);
            }

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
            }

            loadingScreenControl.EndLoading();
            if (!PhotonNetwork.OfflineMode)
            {
                loadingScreenControl.photonView.RPC("EndLoading", RpcTarget.Others);
            }
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

        LoadGameLevel(0);
    }

    /// <summary>
    /// Loads the main menu to a certain state.
    /// </summary>
    public void LoadMainMenu(Menus.MenuState menuState)
    {
        if (RaceManager.Instance != null) {
            if (RaceManager.Instance.IsPaused) {
                RaceManager.Instance.TogglePauseMenu();
            }
        }

        StartCoroutine(MainMenuStateCoroutine(menuState));

        LoadGameLevel(0);
    }

    /// <summary>
    /// Waits for main menu to load in couroutine, then sets the main menu to a certain state.
    /// </summary>
    private IEnumerator MainMenuStateCoroutine(Menus.MenuState menuState) {
        Scene currentScene = SceneManager.GetActiveScene();
        int buildIndex = currentScene.buildIndex;

        while (buildIndex != 0) {
            currentScene = SceneManager.GetActiveScene();
            buildIndex = currentScene.buildIndex;
            yield return 0;
        }

        Menus.MenuManager.Instance.MoveToMenuState(menuState);

        yield return 0;
    }

    // /// <summary>
    // /// Loads the next level. If there is no next level, loads the main menu.
    // /// </summary>
    // public void LoadNextLevel()
    // {
    //     int buildIndex = SceneManager.GetActiveScene().buildIndex + 1;
    //     if (buildIndex < SceneManager.sceneCountInBuildSettings)
    //     {  
    //         if (NextLevel != null) {
    //             LoadGameLevel(NextLevel);
    //         }
    //         else {
    //             Debug.Log("NextLevel in MetaManager is null");
    //             LoadGameLevel(buildIndex);
    //         }
    //     }
    //     else
    //     {
    //         LoadMainMenu();
    //     }
    // }

    public void LoadNextLevel(LevelScriptableObject currentLevel = null) {
        LevelScriptableObject nextLevel;

        if (currentLevel == null) {
            nextLevel = NextLevel;
        }
        else {
            nextLevel = GetLevelAfter(currentLevel);
        }

        if (nextLevel == currentLevel || nextLevel == null) {
            LoadMainMenu();
        }
        
        // Debug.Log("big if block for " + nextLevel.SaveName);
        //if (SaveManager.Instance.loadedSave.LevelsUnlocked.Contains(nextLevel.SaveName)) {
            //Debug.Log("next level is unlocked");
        if (DemoManager.Instance.isDemo && !DemoManager.Instance.demoLockedLevels.Contains(nextLevel)) {
            // Debug.Log("trying to load next level");
            LoadGameLevel(nextLevel);
        }
        else if (DemoManager.Instance.isDemo && DemoManager.Instance.demoLockedLevels.Contains(nextLevel)) {
            // Debug.Log("trying to load level after next level");
            // LevelScriptableObject otherThing = GetLevelAfter(nextLevel);
            // LevelScriptableObject lastThing = GetLevelAfter(otherThing);
            // Debug.Log("check last " + lastThing);
            // Debug.Log("check last again " + lastThing.Name);
            LoadNextLevel(nextLevel);
        }
        else if (!DemoManager.Instance.isDemo) {
            LoadGameLevel(nextLevel);
        }
        //}
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
            int nextLevelIndex;
            if (thisLevelIndex >= AllWorldsDictionary[level.World].Levels.Count - 1)
            {
                nextLevelIndex = 0;

                int thisWorldIndex = AllWorlds.IndexOf(AllWorldsDictionary[level.World]);
                int nextWorldIndex = thisWorldIndex + 1;

                if (nextWorldIndex >= AllWorldsDictionary.Count)
                {
                    Debug.Log("Has no next world, returning original level");
                    // Debug.Log(level.Name);
                    return null;
                }
                else
                {
                    nextLevel = AllWorlds[nextWorldIndex].Levels[nextLevelIndex];
                }
            }
            else
            {
                nextLevelIndex = thisLevelIndex + 1;
                nextLevel = thisLevelList[nextLevelIndex];
            }

            return nextLevel;
        }
        else
        {
            Debug.Log("Has no next level, returning original level");
            return null;
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
                    prevLevel = AllWorlds[prevWorldIndex].Levels[prevWorldIndex];
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
