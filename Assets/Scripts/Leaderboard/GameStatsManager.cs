using UnityEngine;

namespace Survive.Leaderboard.Compatibility
{
    // Conserva la fórmula anterior para componentes existentes, con un solo singleton.
    public class GameStatsManager : global::GameStatsManager
    {
        protected override int KillScore => enemiesKilled * 100;
        protected override int AccuracyBonus => Mathf.RoundToInt(
            (shotsFired > 0 ? (float)shotsHit / shotsFired : 0f) * 500);
    }
}
