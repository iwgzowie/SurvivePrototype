using UnityEngine;

namespace Survive.Items
{
    [CreateAssetMenu(menuName = "Survive/Items/Cargador explosivo")]
    public sealed class ExplosiveAmmoDefinition : ScriptableObject
    {
        public string displayName = "Cargador explosivo";
        public Sprite icon;
        [Min(.1f)] public float radius = 2.5f;
        [Min(1)] public float damage = 180;
        [Min(0)] public float impulse = 6;
        [Header("Mini explosión al impactar")]
        public GameObject impactEffectPrefab;
        [Min(.001f)] public float impactEffectScale = .3f;
        [Min(.02f)] public float impactEffectLifetime = 2.5f;
    }
}
