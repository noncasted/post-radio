namespace Global.Publisher
{
    public class LeaderboardLink : ILeaderboardLink
    {
        public LeaderboardLink(string leaderboard)
        {
            _leaderboard = leaderboard;
        }
        
        private readonly string _leaderboard;
        
        public string GetLeaderboardName()
        {
            return _leaderboard;
        }
    }
}