using System;
using System.IO;
using System.Linq;
using Survive.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Survive.Editor
{
    public static class CityAtmosphereSetup
    {
        public const string Level = "Assets/Scenes/Level 2.unity";
        public const string Folder = "Assets/Settings/Level2Atmosphere";
        public const string Logs = "Logs/CityAtmosphere";

        [MenuItem("Survive/Ambiente/Integrar ciudad postapocalíptica")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salí de Play Mode.");
            Directory.CreateDirectory(Logs);
            Directory.CreateDirectory(Folder);
            Directory.CreateDirectory(".utmp/CityAtmosphereBackup");
            AssetDatabase.Refresh();
            var scene = SceneManager.GetSceneByPath(Level);
            if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(Level, OpenSceneMode.Single);
            if (scene.isDirty) throw new InvalidOperationException("Guardá los cambios pendientes de Level 2 antes de integrar el ambiente.");
            if (!File.Exists(".utmp/CityAtmosphereBackup/Level 2.unity"))
                File.Copy(Level, ".utmp/CityAtmosphereBackup/Level 2.unity");
            var roots = scene.GetRootGameObjects();
            var storm = roots.SelectMany(r => r.GetComponentsInChildren<StormLightingSkybox>(true)).Single();
            var stormData = new SerializedObject(storm);
            var sun = (Light)stormData.FindProperty("luzSol").objectReferenceValue;
            var skybox = (SkyboxController)stormData.FindProperty("skyboxController").objectReferenceValue;

            var day = BuildProfile("City_Day", false);
            var night = BuildProfile("City_Night", true);
            var atmosphere = roots.SelectMany(r => r.GetComponentsInChildren<CityAtmosphere>(true)).SingleOrDefault();
            if (atmosphere == null)
            {
                var root = new GameObject("CityAtmosphere"); SceneManager.MoveGameObjectToScene(root, scene);
                atmosphere = root.AddComponent<CityAtmosphere>();
            }
            var dayVolume = VolumeFor(atmosphere.transform, "Day Post Process", day, 20, 1);
            var nightVolume = VolumeFor(atmosphere.transform, "Night Post Process", night, 21, 0);
            Set(atmosphere, "storm", storm); Set(atmosphere, "sun", sun);
            Set(atmosphere, "dayVolume", dayVolume); Set(atmosphere, "nightVolume", nightVolume);
            var environment = new SerializedObject(atmosphere);
            environment.FindProperty("daySky").colorValue = new Color(.78f, .82f, .88f);
            environment.FindProperty("dayEquator").colorValue = new Color(.65f, .67f, .70f);
            environment.FindProperty("dayGround").colorValue = new Color(.36f, .34f, .30f);
            environment.FindProperty("nightSky").colorValue = new Color(.55f, .60f, .68f);
            environment.FindProperty("nightEquator").colorValue = new Color(.42f, .46f, .53f);
            environment.FindProperty("nightGround").colorValue = new Color(.22f, .25f, .30f);
            environment.ApplyModifiedPropertiesWithoutUndo();

            sun.intensity = 1.15f; sun.shadowStrength = .85f;
            RenderSettings.sun = sun;
            // Una luz lunar desde arriba evita iluminar el suelo desde debajo del mapa.
            stormData.FindProperty("intensidadNoche").floatValue = .28f;
            stormData.FindProperty("rotacionNoche").vector3Value = new Vector3(35, -110, 0);
            stormData.ApplyModifiedPropertiesWithoutUndo();

            var skyData = new SerializedObject(skybox);
            var source = (Material)skyData.FindProperty("skyboxMaterial").objectReferenceValue;
            string skyPath = Folder + "/City_Skybox.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (material == null) { material = new Material(source); AssetDatabase.CreateAsset(material, skyPath); }
            material.SetColor("_ZenithColor", new Color(.25f, .36f, .44f));
            material.SetColor("_HorizonColor", new Color(.60f, .55f, .43f));
            material.SetColor("_HorizonFogColor", new Color(.48f, .48f, .43f));
            material.SetColor("_GroundColor", new Color(.25f, .24f, .21f));
            material.SetFloat("_SkyExposure", .85f);
            material.SetFloat("_SkySaturation", .8f);
            material.SetFloat("_SunSize", .018f);
            EditorUtility.SetDirty(material);
            skyData.FindProperty("skyboxMaterial").objectReferenceValue = material;
            skyData.FindProperty("zenithColorNoche").colorValue = new Color(.025f, .04f, .075f);
            skyData.FindProperty("horizonColorNoche").colorValue = new Color(.09f, .12f, .18f);
            skyData.FindProperty("horizonFogColorNoche").colorValue = new Color(.065f, .09f, .135f);
            skyData.FindProperty("groundColorNoche").colorValue = new Color(.025f, .035f, .055f);
            skyData.ApplyModifiedPropertiesWithoutUndo();
            RenderSettings.skybox = material;
            atmosphere.Apply(0);
            foreach (var camera in roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true))) CityAtmosphere.ConfigureCamera(camera);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("CITY_ATMOSPHERE_SETUP=PASS: perfiles día/noche conectados a StormLightingSkybox, transición original preservada.");
        }

        public static void SetupAndValidate() { Run(); CityAtmosphereValidation.Run(); }

        static Volume VolumeFor(Transform parent, string name, VolumeProfile profile, float priority, float weight)
        {
            var child = parent.Find(name);
            if (child == null) { child = new GameObject(name).transform; child.SetParent(parent, false); }
            child.gameObject.layer = 0;
            var volume = child.GetComponent<Volume>() ?? child.gameObject.AddComponent<Volume>();
            volume.isGlobal = true; volume.priority = priority; volume.weight = weight; volume.sharedProfile = profile;
            return volume;
        }

        static VolumeProfile BuildProfile(string name, bool night)
        {
            string path = Folder + "/" + name + ".asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, path); }
            var tone = Override<Tonemapping>(profile); tone.mode.Override(TonemappingMode.ACES);
            var color = Override<ColorAdjustments>(profile);
            color.postExposure.Override(night ? .85f : .35f);
            color.contrast.Override(night ? 4 : 6);
            color.saturation.Override(night ? -16 : -12);
            color.colorFilter.Override(Color.white);
            var white = Override<WhiteBalance>(profile); white.temperature.Override(night ? -8 : 4); white.tint.Override(0);
            var bloom = Override<Bloom>(profile);
            bloom.intensity.Override(night ? .24f : .16f); bloom.threshold.Override(1.15f);
            bloom.scatter.Override(.55f); bloom.clamp.Override(3);
            bloom.highQualityFiltering.Override(true);
            var split = Override<SplitToning>(profile);
            split.shadows.Override(night ? new Color(.46f, .485f, .53f) : new Color(.48f, .495f, .52f));
            split.highlights.Override(new Color(.53f, .51f, .48f)); split.balance.Override(0);
            var vignette = Override<Vignette>(profile); vignette.intensity.Override(night ? .18f : .13f);
            vignette.smoothness.Override(.4f); vignette.color.Override(new Color(.025f, .03f, .035f));
            Override<MotionBlur>(profile).intensity.Override(0);
            Override<DepthOfField>(profile).mode.Override(DepthOfFieldMode.Off);
            Override<ChromaticAberration>(profile).intensity.Override(0);
            Override<FilmGrain>(profile).intensity.Override(0);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        static T Override<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet<T>(out var component)) { component = profile.Add<T>(); AssetDatabase.AddObjectToAsset(component, profile); }
            component.active = true; EditorUtility.SetDirty(component); return component;
        }
        static void Set(Object target, string field, Object value)
        {
            var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
