public struct LeaderboardEntry
{
    public string PlayerName { get; private set; }
    public float Time { get; private set; }

    public int GlobalRank { get; private set; }

    public ulong PlayerID { get; private set; }

    public LeaderboardEntry(string playerName, float time, int globalRank, ulong playerID)
    {
        PlayerName = playerName;
        Time = time;
        GlobalRank = globalRank;
        PlayerID = playerID;
    }
}