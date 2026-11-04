using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Survive.World
{
    // La tormenta conserva el reloj, el trigger y el control de intensidad/rotación del sol.
    [DefaultExecutionOrder(100)]
    public sealed class CityAtmosphere : MonoBehaviour
    {
        [SerializeField] private StormLightingSkybox storm;
        [SerializeField] private Light sun;
        [SerializeField] private Volume dayVolume;
        [SerializeField] private Volume nightVolume;

        [Header("Ambiente de día")]
        [SerializeField] private Color daySky = new Color(.78f, .82f, .88f);
        [SerializeField] private Color dayEquator = new Color(.65f, .67f, .70f);
        [SerializeField] private Color dayGround = new Color(.36f, .34f, .30f);
        [SerializeField] private Color daySun = new Color(1f, .91f, .78f);
        [SerializeField] private Color dayFog = new Color(.48f, .48f, .43f);
        [SerializeField, Range(0, .02f)] private float dayFogDensity = .0035f;
        [SerializeField, Range(0, 1)] private float dayReflections = .8f;

        [Header("Ambiente de noche")]
        [SerializeField] private Color nightSky = new Color(.55f, .60f, .68f);
        [SerializeField] private Color nightEquator = new Color(.42f, .46f, .53f);
        [SerializeField] private Color nightGround = new Color(.22f, .25f, .30f);
        [SerializeField] private Color nightSun = new Color(.56f, .69f, .93f);
        [SerializeField] private Color nightFog = new Color(.065f, .09f, .135f);
        [SerializeField, Range(0, .02f)] private float nightFogDensity = .005f;
        [SerializeField, Range(0, 1)] private float nightReflections = .3f;

        private Camera configuredCamera;
        private float appliedProgress = float.NaN;

        private void OnEnable() => appliedProgress = float.NaN;
        public float NightBlend => storm != null ? storm.NightProgress : 0;
        public Volume DayVolume => dayVolume;
        public Volume NightVolume => nightVolume;

        private void Start() => Apply(NightBlend);

        private void LateUpdate()
        {
            if (!Mathf.Approximately(appliedProgress, NightBlend)) Apply(NightBlend);
            var camera = Camera.main;
            if (camera != null && camera != configuredCamera && camera.gameObject.scene == gameObject.scene)
            {
                ConfigureCamera(camera);
                configuredCamera = camera;
            }
        }

        public static void ConfigureCamera(Camera camera)
        {
            camera.allowHDR = true;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.volumeLayerMask |= 1; // Los dos volúmenes de Level 2 están en Default.
            camera.SetVolumeFrameworkUpdateMode(VolumeFrameworkUpdateMode.EveryFrame);
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.dithering = true;
            data.stopNaN = true;
        }

        // También permite previsualizar el mismo ambiente en el editor, sin otro reloj de día/noche.
        public void Apply(float progress)
        {
            float t = Mathf.Clamp01(progress);
            appliedProgress = t;
            if (dayVolume != null) dayVolume.weight = 1;
            if (nightVolume != null) nightVolume.weight = t;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(daySky, nightSky, t);
            RenderSettings.ambientEquatorColor = Color.Lerp(dayEquator, nightEquator, t);
            RenderSettings.ambientGroundColor = Color.Lerp(dayGround, nightGround, t);
            RenderSettings.reflectionIntensity = Mathf.Lerp(dayReflections, nightReflections, t);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Color.Lerp(dayFog, nightFog, t);
            RenderSettings.fogDensity = Mathf.Lerp(dayFogDensity, nightFogDensity, t);
            if (sun != null) sun.color = Color.Lerp(daySun, nightSun, t);
        }
    }
}
