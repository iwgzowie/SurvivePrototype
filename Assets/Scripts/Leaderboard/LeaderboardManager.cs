namespace Survive.Leaderboard.Compatibility
{
    // Los datos y la persistencia pertenecen al gestor compartido.
    public class LeaderboardManager : global::LeaderboardManager
    {
        private void Reset()
        {
            maxEntries = 10;
        }
    }
}
