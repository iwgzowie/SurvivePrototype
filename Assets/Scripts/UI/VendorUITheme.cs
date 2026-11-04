using TMPro;
using UnityEngine;

namespace Survive.UI
{
    [CreateAssetMenu(menuName = "Survive/UI/Tema del vendedor")]
    public sealed class VendorUITheme : ScriptableObject
    {
        public TMP_FontAsset font;
        public Sprite panel, card, frame, button, currency;
        public Color background = new Color(.025f, .045f, .065f, .98f);
        public Color cardColor = new Color(.045f, .075f, .10f, 1);
        public Color accent = new Color(.94f, .74f, .36f);
        public Color muted = new Color(.66f, .74f, .80f);
        public static VendorUITheme Load() => Resources.Load<VendorUITheme>("UI/SyntyVendorTheme");
    }
}
