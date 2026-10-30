using UnityEngine;

namespace Survive.Leaderboard.Compatibility
{
    // Conserva el MonoScript y sus campos serializados; comparte estadísticas y tabla.
    public class GameOverUI : global::GameOverUI
    {
        protected override bool MonitorPlayerDeath => false;
        protected override bool PauseWhenShown => false;
        protected override GameObject DisplayPanel => gameObject;
    }
}
