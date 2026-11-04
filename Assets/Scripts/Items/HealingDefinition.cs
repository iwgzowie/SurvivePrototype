using UnityEngine;

namespace Survive.Items
{
    [CreateAssetMenu(menuName = "Survive/Items/Curación")]
    public sealed class HealingDefinition : ScriptableObject
    {
        public string displayName = "Vendas";
        public Sprite icon;
        [Min(1)] public float healing = 35;
        [Min(1)] public int stackLimit = 5;
        public HealingPickup worldPrefab;
    }
}
