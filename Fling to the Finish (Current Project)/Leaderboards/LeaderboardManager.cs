#if UNITY_ANDROID || UNITY_IOS || UNITY_TIZEN || UNITY_TVOS || UNITY_WEBGL || UNITY_WSA || UNITY_PS4 || UNITY_WII || UNITY_XBOXONE || UNITY_SWITCH
#define DISABLESTEAMWORKS
#endif

using Fling.GameModes;
using Fling.Levels;
using Fling.Saves;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Script that maintains leaderboard data for each level
/// </summary>
public static class LeaderboardManager
{
    public static bool HasInitialized { get; private set; }

    private static PlatformSpecificLeaderboardManager currentLeaderboardManager;
    public static Dictionary<LeaderboardType, bool> ValuesSetForCurrentLevel { get; private set; }
    private const string LEADERBOARD_NAME_SUFFIX = "_TIME";
    private static LeaderboardType currentLeaderboardType;
    
    /// <summary>
    /// The current level's leaderboard data
    /// WARNING: Might be null! Check if LeaderboardManager.ValuesSetForCurrentLevel is true before accessing this data!
    /// </summary>
    public static Dictionary<LeaderboardType, List<LeaderboardEntry>> CurrentLevelLeaderboardData { get; private set; }

    /// <summary>
    /// Called from MetaManager once at the start of the game session
    /// Initializes the current leaderboard platform (Steam, Switch, etc.)
    /// </summary>
    /// <param name="currentPlatform"></param>
    public static void Init(Platform currentPlatform)
    {
        ResetValuesSetDict();

        bool initSuccess = false;
        switch (currentPlatform)
        {
#if !DISABLESTEAMWORKS
            case Platform.Steam:
                if (MetaManager.Instance.UseSteam)
                {
                    currentLeaderboardManager = new SteamLeaderboardsManager();
                    initSuccess = true;
                }
                break;
#endif
        }

        if (!initSuccess)
        {
            return;
        }

        currentLeaderboardManager.Init();
        currentLeaderboardManager.OnDataInitializedForLevel += DataInitializedForLevel;
        currentLeaderboardManager.OnLeaderboardsDownloaded += LevelDataDownloaded;

        MetaManager.OnNewPlayableLevelLoaded += NewPlayableLevelStarted;
        MetaManager.Instance.OnNewLevelLoadStarted += NewLevelLoadStarted;

        HasInitialized = true;
    }

    private static void ResetValuesSetDict()
    {
        ValuesSetForCurrentLevel = new Dictionary<LeaderboardType, bool>();
        ValuesSetForCurrentLevel[LeaderboardType.Local] = false;
        ValuesSetForCurrentLevel[LeaderboardType.Global] = false;
        ValuesSetForCurrentLevel[LeaderboardType.GlobalAroundUser] = false;
        ValuesSetForCurrentLevel[LeaderboardType.Friends] = false;
        CurrentLevelLeaderboardData = new Dictionary<LeaderboardType, List<LeaderboardEntry>>();
    }

    /// <summary>
    /// Upload the given level and gamemode's time to the leaderboard stored on the cloud for Steam/Switch etc.
    /// </summary>
    /// <param name="level"></param>
    /// <param name="gameMode"></param>
    /// <param name="time"></param>
    public static void UploadLevelLeaderboardTime(LevelScriptableObject level, GameMode gameMode, int time)
    {
        if (!HasInitialized) return;
        if (!SaveManager.Instance.loadedSave.LeaderboardsEnabled)
        {
            return;
        }
        if (DevScript.Instance != null && (DevScript.Instance.DevMode || DevScript.Instance.AllowInGameDebugPanel))
        {
            Debug.Log("DEV MODE, not uploading leaderboard times!");
            return;
        }

        currentLeaderboardManager.UploadLeaderboardDataForLevel(level, gameMode, time);
    }

    /// <summary>
    /// Called when a new playable level is started in the game
    /// A playable level is any level that is not the main menu or credits
    /// </summary>
    /// <param name="level"></param>
    /// <param name="gameMode"></param>
    private static void NewPlayableLevelStarted(LevelScriptableObject level, GameMode gameMode)
    {
        if (!HasInitialized) return;
        if (SaveManager.Instance != null && SaveManager.Instance.HasInitialized && !SaveManager.Instance.loadedSave.LeaderboardsEnabled)
        {
            return;
        }
        ResetValuesSetDict();

        currentLeaderboardManager.InitDataForLevel(level, gameMode);

        RaceManager.Instance.OnRaceFinish += LevelRaceOver;
    }

    /// <summary>
    /// Called when a new level has started loading
    /// </summary>
    private static void NewLevelLoadStarted()
    {
        if (!SaveManager.Instance.loadedSave.LeaderboardsEnabled)
        {
            return;
        }
        if (RaceManager.Instance != null)
        {
            RaceManager.Instance.OnRaceFinish -= LevelRaceOver;
        }

        // Reset values here
        ResetValuesSetDict();
        CurrentLevelLeaderboardData = null;
    }

    /// <summary>
    /// Called on RaceManager.OnRaceFinish
    /// </summary>
    /// <param name="beaten"></param>
    private static void LevelRaceOver(bool beaten)
    {
        if (!SaveManager.Instance.loadedSave.LeaderboardsEnabled)
        {
            return;
        }
        if (beaten)
        {
            // Upload this level's time to the leaderboard ONLY if we beat this gamemode for this level
            UploadLevelLeaderboardTime(MetaManager.Instance.CurrentLevel, MetaManager.Instance.CurrentGameMode, (int)(RaceManager.Instance.RaceTimer.FinishTime * Utilities.MILLISECONDS_IN_SECOND));
        }
    }

    /// <summary>
    /// Called when the leaderboards have been initialized for the currently loaded level
    /// </summary>
    /// <param name="leaderboardName"></param>
    private static void DataInitializedForLevel(string leaderboardName)
    {
        if (!HasInitialized) return;
    }

    /// <summary>
    /// Called when the leaderboard scores have been downloaded for the currently loaded level
    /// </summary>
    /// <param name="levelName"></param>
    /// <param name="entries"></param>
    private static void LevelDataDownloaded(string levelName, List<LeaderboardEntry> entries)
    {
        if (!SaveManager.Instance.loadedSave.LeaderboardsEnabled)
        {
            return;
        }
        if (MetaManager.Instance.CurrentLevel.SaveName != levelName) return;

        ValuesSetForCurrentLevel[currentLeaderboardType] = true;
        CurrentLevelLeaderboardData[currentLeaderboardType] = entries;

        OnLeaderboardEntriesDownloaded?.Invoke(currentLeaderboardType);
    }

    public static event Action<LeaderboardType> OnLeaderboardEntriesDownloaded;
    /// <summary>
    /// Start a download request for the current level and gamemode in the desired LeaderboardType (Global, GlobalAroundUser, Friends).
    /// </summary>
    /// <param name="type"></param>
    public static void RequestDataForCurrentLevel(LeaderboardType type)
    {
        if (!SaveManager.Instance.loadedSave.LeaderboardsEnabled)
        {
            return;
        }
        if (!HasInitialized) return;

        currentLeaderboardType = type;
        currentLeaderboardManager.RequestLeaderboardDataForLevel(MetaManager.Instance.CurrentLevel, MetaManager.Instance.CurrentGameMode, type);
    }

    /// <summary>
    /// Returns the name of the current level from the leaderboard name
    /// </summary>
    /// <param name="leaderboardName"></param>
    /// <returns></returns>
    public static string GetLevelNameFromLeaderboardName(string leaderboardName)
    {
        int idx = leaderboardName.IndexOf("_");
        string levelName = leaderboardName.Substring(0, idx);
        return levelName;
    }

    /// <summary>
    /// Returns the name for the leaderboard given the current level and gamemode name
    /// </summary>
    /// <param name="level"></param>
    /// <param name="gameMode"></param>
    /// <returns></returns>
    public static string GetLeaderboardNameFromLevel(LevelScriptableObject level, GameMode gameMode)
    {
        string gameModeStr = gameMode.ToString();

        if (level.SaveName == "DesertTutorial")
        {
            gameModeStr = GameMode.Normal.ToString();
        }

        string leaderboardName = level.SaveName + "_" + gameModeStr + LEADERBOARD_NAME_SUFFIX;

        return leaderboardName;
    }
}