using UnityEngine;

namespace Survive.Turrets
{
    [CreateAssetMenu(menuName = "Survive/Items/Torreta", fileName = "TurretDefinition")]
    public sealed class TurretDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Torreta automática";
        [SerializeField] private Sprite icon;
        [SerializeField] private AutoTurret deployedPrefab;
        [SerializeField, Min(.1f)] private float range = 15f;
        [SerializeField, Min(.1f)] private float damage = 20f;
        [SerializeField, Min(.1f)] private float shotsPerSecond = 4f;
        [SerializeField, Min(.1f)] private float footprintRadius = .65f;
        [SerializeField, Min(.1f)] private float height = 1.4f;
        [SerializeField, Range(0, 45)] private float maximumSlope = 15f;
        [SerializeField, Min(0)] private float lifetimeSeconds;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public AutoTurret DeployedPrefab => deployedPrefab;
        public float Range => Mathf.Max(.1f, range);
        public float Damage => Mathf.Max(.1f, damage);
        public float ShotsPerSecond => Mathf.Max(.1f, shotsPerSecond);
        public float FootprintRadius => Mathf.Max(.1f, footprintRadius);
        public float Height => Mathf.Max(.1f, height);
        public float MaximumSlope => Mathf.Clamp(maximumSlope, 0, 45);
        public float LifetimeSeconds => Mathf.Max(0, lifetimeSeconds);
    }
}
