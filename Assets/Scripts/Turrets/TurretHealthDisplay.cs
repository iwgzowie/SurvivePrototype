using Survive.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Survive.Turrets
{
    [RequireComponent(typeof(AutoTurret))]
    public sealed class TurretHealthDisplay : MonoBehaviour
    {
        AutoTurret turret;
        Canvas canvas;
        RectTransform fill;
        Image fillImage;
        TMP_Text label;
        void Awake()
        {
            turret = GetComponent<AutoTurret>();
            var go = new GameObject("TurretHealth", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(transform, false); go.transform.localPosition = Vector3.up * (turret.Definition != null ? turret.Definition.Height + .35f : 1.75f);
            go.transform.localScale = Vector3.one * .006f;
            canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)go.transform).sizeDelta = new Vector2(180,45);
            var bg = Child("Background",go.transform,new Vector2(180,12),new Vector2(0,-12));
            bg.gameObject.AddComponent<Image>().color = new Color(.02f,.04f,.05f,.9f);
            fill = Child("Fill",bg,new Vector2(176,8),Vector2.zero); fillImage=fill.gameObject.AddComponent<Image>();
            label = Child("Value",go.transform,new Vector2(180,28),new Vector2(0,12)).gameObject.AddComponent<TextMeshProUGUI>();
            var theme=VendorUITheme.Load(); if(theme!=null) label.font=theme.font;
            label.fontSize=24; label.alignment=TextAlignmentOptions.Center; label.raycastTarget=false;
            foreach(var image in go.GetComponentsInChildren<Image>()) image.raycastTarget=false;
            turret.HealthChanged += Refresh; Refresh();
        }
        static RectTransform Child(string name,Transform parent,Vector2 size,Vector2 position)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent,false);
            rect.sizeDelta=size; rect.anchoredPosition=position; return rect;
        }
        void LateUpdate()
        {
            bool visible=turret.isActiveAndEnabled && !turret.IsDepleted;
            canvas.gameObject.SetActive(visible); if(!visible) return;
            var camera=Camera.main; if(camera!=null) canvas.transform.rotation=camera.transform.rotation;
        }
        void Start() => Refresh();
        void Refresh()
        {
            if(fill==null) return;
            float fraction=Mathf.Clamp01(turret.CurrentHealth/turret.MaxHealth);
            fill.sizeDelta=new Vector2(176*fraction,8); fill.anchoredPosition=new Vector2(-88*(1-fraction),0);
            fillImage.color=fraction>.3f ? new Color(.4f,.95f,.65f) : new Color(1,.45f,.2f);
            label.text=$"{Mathf.CeilToInt(turret.CurrentHealth)} / {turret.MaxHealth:0}";
        }
        void OnDisable() { if(canvas!=null) canvas.gameObject.SetActive(false); }
        void OnDestroy() { if(turret!=null) turret.HealthChanged-=Refresh; }
    }
}
