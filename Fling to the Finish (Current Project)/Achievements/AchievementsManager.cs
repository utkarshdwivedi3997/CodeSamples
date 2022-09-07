#if UNITY_ANDROID || UNITY_IOS || UNITY_TIZEN || UNITY_TVOS || UNITY_WEBGL || UNITY_WSA || UNITY_PS4 || UNITY_WII || UNITY_XBOXONE || UNITY_SWITCH
#define DISABLESTEAMWORKS
# endif

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fling.Achievements
{
    public partial class AchievementsManager : MonoBehaviour
    {
        public static AchievementsManager Instance { get; private set; }
        public bool HasInitialized { get; private set; }

        private IPlatformSpecificAchievementsManager currentAchievementsManager;

        [SerializeField] private List<Achievement> allAchievements;
        [SerializeField] private List<Stat> nonAchievementStats;

        private Dictionary<AchievementType, Achievement> achievements;
        private Dictionary<StatType, Stat> stats;
        private Dictionary<StatType, Stat> globalStats;
        private Dictionary<StatType, List<Achievement>> statAchievementsDictionary;

        private void Awake()
        {
            // Singleton
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(this);
            }
        }

        public void Init(Platform currentPlatform)
        {
            CreateStatsAndAchievementsDictionary();
            CreateStatToAchievementsDictionary();

            switch (currentPlatform)
            {
#if !DISABLESTEAMWORKS
                case Platform.Steam:
                    currentAchievementsManager = new SteamAchievementsManager();
                    break;
#endif
            }

            HasInitialized = currentAchievementsManager.Initialize();

            if (HasInitialized)
            {
                // download the stats for the first time
                DownloadStats();
            }
        }

        /// <summary>
        /// Creates a dictionary of [<see cref="StatType"/>, <see cref="Stat"/>] and [<see cref="AchievementType"/>, <see cref="Achievement"/>]
        /// </summary>
        private void CreateStatsAndAchievementsDictionary()
        {
            stats = new Dictionary<StatType, Stat>();
            globalStats = new Dictionary<StatType, Stat>();
            achievements = new Dictionary<AchievementType, Achievement>();

            foreach (Achievement achievement in allAchievements)
            {
                achievement.IsUnlocked = false;

                achievements.Add(achievement.AchievementType, achievement);

                if (!stats.ContainsKey(achievement.AssociatedStat.StatType))
                {
                    achievement.AssociatedStat.SetValue(0);
                    achievement.AssociatedStat.SetValue(0f);
                    stats.Add(achievement.AssociatedStat.StatType, achievement.AssociatedStat);
                }

                if (achievement.AssociatedStat.TrackGlobalValue)
                {
                    if (!globalStats.ContainsKey(achievement.AssociatedStat.StatType))
                    {
                        globalStats.Add(achievement.AssociatedStat.StatType, achievement.AssociatedStat);
                    }
                }
            }

            foreach (Stat stat in nonAchievementStats)
            {
                if (!stats.ContainsKey(stat.StatType))
                {
                    stats.Add(stat.StatType, stat);
                }

                if (stat.TrackGlobalValue)
                {
                    if (!globalStats.ContainsKey(stat.StatType))
                    {
                        globalStats.Add(stat.StatType, stat);
                    }
                }
            }
        }

        /// <summary>
        /// Creates a mapping of [<see cref="StatType"/>, list of <see cref="Achievement"/>s]
        /// </summary>
        private void CreateStatToAchievementsDictionary()
        {
            statAchievementsDictionary = new Dictionary<StatType, List<Achievement>>();

            foreach (Achievement achievement in achievements.Values)
            {
                StatType statType = achievement.AssociatedStat.StatType;
                if (statAchievementsDictionary.ContainsKey(statType))
                {
                    statAchievementsDictionary[statType].Add(achievement);
                }
                else
                {
                    statAchievementsDictionary.Add(statType, new List<Achievement>() { achievement });
                }
            }
        }

        public void DownloadStats()
        {
            if (!HasInitialized)
            {
                return;
            }

            currentAchievementsManager.DownloadStats();
        }

        public static event Action OnStatsSyncedToServerValues;
        public static event Action OnGlobalStatsSyncedToServerValues;

        private void OnStatsDownloaded()
        {
            foreach (Achievement achievement in achievements.Values)
            {
                if (achievement.IsUnlocked)
                {
                    if (statAchievementsDictionary.ContainsKey(achievement.AssociatedStat.StatType))
                    {
                        List<Achievement> achList = statAchievementsDictionary[achievement.AssociatedStat.StatType];
                        if (achList.Contains(achievement))
                        {
                            achList.Remove(achievement);
                            //statAchievementsDictionary[achievement.AssociatedStat.StatType] = achList;
                        }
                    }
                }
            }

            OnStatsSyncedToServerValues?.Invoke();
        }

        private void OnGlobalStatsDownloaded()
        {
            OnGlobalStatsSyncedToServerValues?.Invoke();
        }

        /// <summary>
        /// Increment the given int stat by the given amount. Also updates progress on any achievements associated with this stat.
        /// </summary>
        /// <param name="statType"></param>
        /// <param name="incrementAmount">Increase amount</param>
        public void IncrementStat(StatType statType, int incrementAmount, bool alsoForceUploadStats = false)
        {
            if (stats.ContainsKey(statType))
            {
                stats[statType].IncrementValue(incrementAmount);
                Debug.Log("adding " + incrementAmount + " to " + statType.ToString());
                currentAchievementsManager.UpdateStat(stats[statType]);
                CheckAndUpdateAssociatedAchievements(stats[statType], alsoForceUploadStats);
            }
        }

        /// <summary>
        /// Increment the given float stat by the given amount. Also updates progress on any achievements associated with this stat.
        /// </summary>
        /// <param name="statType"></param>
        /// <param name="incrementAmount"></param>
        public void IncrementStat(StatType statType, float incrementAmount, bool alsoForceUploadStats = false)
        {
            if (stats.ContainsKey(statType))
            {
                stats[statType].IncrementValue(incrementAmount);
                currentAchievementsManager.UpdateStat(stats[statType]);
                CheckAndUpdateAssociatedAchievements(stats[statType], alsoForceUploadStats);
            }
        }

        public int GetIntStatValue(StatType stat)
        {
            if (stats.ContainsKey(stat))
            {
                if (stats[stat].StatDataType == StatDataType.INT)
                {
                    return stats[stat].IntValue;
                }
            }

            return 0;
        }

        public float GetFloatStatValue(StatType stat)
        {
            if (stats.ContainsKey(stat))
            {
                if (stats[stat].StatDataType == StatDataType.FLOAT)
                {
                    return stats[stat].FloatValue;
                }
            }

            return 0f;
        }

        public long GetGlobalIntStatValue(StatType stat)
        {
            if (globalStats.ContainsKey(stat))
            {
                if (globalStats[stat].StatDataType == StatDataType.INT && globalStats[stat].TrackGlobalValue)
                {
                    return globalStats[stat].GlobalIntValue;
                }
            }

            return 0;
        }

        public double GetGlobalFloatStatValue(StatType stat)
        {
            if (globalStats.ContainsKey(stat))
            {
                if (globalStats[stat].StatDataType == StatDataType.FLOAT && globalStats[stat].TrackGlobalValue)
                {
                    return globalStats[stat].GlobalFloatValue;
                }
            }

            return 0f;
        }

        /// <summary>
        /// Checks if any achievement associated with the given stat can be unlocked, and if so, unlocks that achievement
        /// </summary>
        /// <param name="stat"></param>
        private void CheckAndUpdateAssociatedAchievements(Stat stat, bool forceUploadStats = false)
        {
            bool wasAnyAchievementUnlocked = false;

            if (statAchievementsDictionary.ContainsKey(stat.StatType))
            {
                foreach (Achievement achievement in statAchievementsDictionary[stat.StatType])
                {
                    if (!achievement.IsUnlocked && achievement.CanUnlock())
                    {
                        achievement.IsUnlocked = true;
                        currentAchievementsManager.UnlockAchievement(achievement);
                        wasAnyAchievementUnlocked = true;
                    }
                }
            }

            if (wasAnyAchievementUnlocked || forceUploadStats)
            {
                currentAchievementsManager.UploadStats(forceUploadStats);
            }
        }

        public void ForceUploadStats()
        {
            currentAchievementsManager.UploadStats(true);
        }
    }
}