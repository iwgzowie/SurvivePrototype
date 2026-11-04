using UnityEngine;

namespace Survive.Items
{
    [CreateAssetMenu(menuName = "Survive/Items/Caja de munición común")]
    public sealed class AmmoBoxDefinition : ScriptableObject
    {
        public string displayName = "Caja de munición común";
        public Sprite icon;
        [Min(1)] public int rounds = 60;
    }
}
