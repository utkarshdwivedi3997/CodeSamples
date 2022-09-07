namespace Fling.Achievements
{
    public partial class AchievementsManager
    {
        private interface IPlatformSpecificAchievementsManager
        {
            Platform Platform { get; }
            bool Initialize();
            void UploadStats(bool forceUpload = false);
            void DownloadStats();

            void DownloadGlobalStats();
            void UpdateStat(Stat stat);
            void UnlockAchievement(Achievement achievement);
        }
    }
}