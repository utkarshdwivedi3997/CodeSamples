using Fling.GameModes;
using Fling.Levels;
using System;
using System.Collections.Generic;

public abstract class PlatformSpecificLeaderboardManager
{
    public abstract Platform Platform { get; }

    public abstract void Init();

    public abstract void InitDataForLevel(LevelScriptableObject level, GameMode gameMode);

    public event Action<string /* leaderboard name */> OnDataInitializedForLevel;
    protected void RaiseOnDataInitializedForLevel(string leaderboardName) => OnDataInitializedForLevel?.Invoke(leaderboardName);
    public abstract void RequestLeaderboardDataForLevel(LevelScriptableObject level, GameMode gameMode, LeaderboardType type);

    public event Action<string /*level name*/, List<LeaderboardEntry> /*level leaderboard scores*/> OnLeaderboardsDownloaded;
    protected void RaiseOnLeaderboardsDownloaded(string levelName, List<LeaderboardEntry> levelLeaderboardEntries) => OnLeaderboardsDownloaded?.Invoke(levelName, levelLeaderboardEntries);

    public abstract void UploadLeaderboardDataForLevel(LevelScriptableObject level, GameMode gameMode, int time);

    public event Action OnLeaderboardsUploaded;
    protected void RaiseOnLeaderboardsUploaded() => OnLeaderboardsUploaded?.Invoke();
}