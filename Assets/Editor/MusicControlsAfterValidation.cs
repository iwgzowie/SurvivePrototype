using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Survive.Audio;
using Survive.Combat;
using Survive.Items;
using Object = UnityEngine.Object;

namespace Survive.Editor
{
    [InitializeOnLoad] public static class MusicControlsAfterValidation
    {
        const string Key = "Survive.MusicControlsAfterValidation", Folder = "Logs/MusicControls-20261105";
        [Serializable] class Report { public List<string> checks = new List<string>(); public int shots, maxShotVoices, musicVirtualFrames, ambienceVirtualFrames, samples; public float musicMaxRms; }
        static Report report;
        static MenuBehaviour menu;
        static MenuOptions options;
        static PlayerWeaponFeedback feedback;
        static WeaponItem weapon;
        static AudioSource[] music, pool;
        static AudioSource ambience;
        static int stage;
        static double next, endFire, nextShot;
        static float globalVolume, ambienceVolume, musicTime;
        static int[] pausedSamples;
        static MethodInfo playShot = typeof(PlayerWeaponFeedback).GetMethod("PlayShot", BindingFlags.Instance | BindingFlags.NonPublic);
        static MusicControlsAfterValidation()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state => {
                if (!SessionState.GetBool(Key, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode) { report = new Report(); stage = 0; next = EditorApplication.timeSinceStartup + 2; }
                if (state == PlayModeStateChange.EnteredEditMode) { SessionState.SetBool(Key, false); if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(Key + ".Exit", 1)); }
            };
        }
        public static void Run()
        {
            SessionState.SetBool(Key + ".Had", PlayerPrefs.HasKey(MusicVolumeSettings.PreferenceKey));
            SessionState.SetFloat(Key + ".Value", PlayerPrefs.GetFloat(MusicVolumeSettings.PreferenceKey, 1));
            EditorSceneManager.OpenScene("Assets/Scenes/SceneAlejandro.unity");
            SessionState.SetBool(Key, true); EditorApplication.EnterPlaymode();
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            typeof(CharacterCustomizationValidation).GetMethod("PrepareBatchGameView", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null); EditorApplication.QueuePlayerLoopUpdate();
            double now = EditorApplication.timeSinceStartup;
            try
            {
                if (stage == 6)
                {
                    if (now >= nextShot && now < endFire)
                    { playShot.Invoke(feedback, new object[] { weapon }); report.shots++; nextShot = now + 1.0 / weapon.ShotsPerSecond; }
                    report.samples++;
                    report.maxShotVoices = Mathf.Max(report.maxShotVoices, pool.Count(s => s.isPlaying));
                    if (music.Any(s => s.isPlaying && s.volume > .001f && s.isVirtual)) report.musicVirtualFrames++;
                    if (ambience.isVirtual) report.ambienceVirtualFrames++;
                    foreach (var source in music.Where(s => s.isPlaying)) { var data = new float[1024]; source.GetOutputData(data, 0); report.musicMaxRms = Mathf.Max(report.musicMaxRms, Mathf.Sqrt(data.Sum(x => x * x) / data.Length)); }
                    if (now < endFire) return;
                }
                if (now < next) return;
                next = now + .4;
                switch (stage++)
                {
                    case 0:
                        options = Object.FindFirstObjectByType<MenuOptions>();
                        Check(options.musicSlider != null && options.musicValueLabel != null, "Opciones tiene slider de música y porcentaje conectados");
                        globalVolume = AudioListener.volume;
                        options.musicSlider.value = .35f;
                        Check(Mathf.Approximately(MusicVolumeSettings.Volume, .35f) && options.musicValueLabel.text == "35%", "Slider real de opciones aplica 35 %");
                        Check(Mathf.Approximately(AudioListener.volume, globalVolume), "Slider de música no modifica volumen global");
                        Check(Mathf.Approximately(PlayerPrefs.GetFloat(MusicVolumeSettings.PreferenceKey), .35f), "Preferencia independiente guardada");
                        foreach (var t in options.musicSlider.GetComponentsInParent<Transform>(true)) t.gameObject.SetActive(true);
                        var audioPanel = options.musicSlider.transform.parent.parent;
                        foreach (Transform child in audioPanel.parent) if (child.name == "PanelVideo" || child.name == "PanelControles") child.gameObject.SetActive(false);
                        next += .5; break;
                    case 1:
                        Capture("opciones-musica.png"); SceneManager.LoadScene("Level 2"); next = now + 3; break;
                    case 2:
                        foreach (var s in Object.FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None)) { s.CancelWaves(); s.enabled = false; }
                        foreach (var e in Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None)) e.gameObject.SetActive(false);
                        menu = Object.FindFirstObjectByType<MenuBehaviour>();
                        music = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Where(s => s.name == "Music_A" || s.name == "Music_B").ToArray();
                        ambience = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).First(s => s.name == "Ambience_day"); ambienceVolume = ambience.volume;
                        Check(music.Length == 2 && music.All(s => s.priority == 0), "Dos fuentes musicales protegidas frente a virtualización por disparos");
                        Check(Mathf.Approximately(MusicVolumeSettings.Volume, .35f) && music.Any(s => s.volume > 0 && s.volume <= .2451f), "Level 2 conserva la preferencia al cambiar de escena");
                        menu.PauseGame(); options = menu.container.GetComponent<MenuOptions>();
                        Check(options != null && options.musicSlider.gameObject.activeInHierarchy, "Pausa muestra el slider conectado");
                        options.musicSlider.value = 0;
                        Check(music.All(s => s.volume == 0) && options.musicValueLabel.text == "0%", "Se puede silenciar toda la música durante la pausa");
                        Check(Mathf.Approximately(ambience.volume, ambienceVolume) && Mathf.Approximately(AudioListener.volume, globalVolume), "Silenciar música conserva ambiente y volumen general");
                        musicTime = music.Where(s => s.clip != null).Sum(s => s.time); next += .5; break;
                    case 3:
                        Check(music.Any(s => s.isPlaying) && music.Where(s => s.clip != null).Sum(s => s.time) > musicTime, "Silenciar música conserva reproducción y sincronización DSP");
                        Capture("pausa-musica.png"); options.musicSlider.value = .6f; menu.ResumeGame();
                        Check(Mathf.Approximately(PlayerPrefs.GetFloat(MusicVolumeSettings.PreferenceKey), .6f), "Cerrar pausa guarda el valor elegido");
                        break;
                    case 4:
                        menu.PauseGame(); Check(Mathf.Approximately(options.musicSlider.value, .6f) && options.musicValueLabel.text == "60%", "Reabrir pausa conserva slider y porcentaje");
                        options.musicSlider.value = 1; menu.ResumeGame(); break;
                    case 5:
                        feedback = Object.FindFirstObjectByType<PlayerWeaponFeedback>();
                        weapon = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Items/Weapons/Weapon_AssaultRifle.prefab")).GetComponent<WeaponItem>();
                        pool = feedback.GetComponentsInChildren<AudioSource>(true).Where(s => s.gameObject.name == "WeaponShotVoices" || s.gameObject.name == "Shots").ToArray();
                        Check(pool.Length == PlayerWeaponFeedback.MaxShotVoices, "El pool contiene exactamente doce voces de disparo");
                        Check(pool.All(s => s.outputAudioMixerGroup != null && s.outputAudioMixerGroup.name == "SFX"), "Todas las capas de tiro conservan el mixer SFX");
                        endFire = now + 10; nextShot = now; next = endFire; break;
                    case 6:
                        Check(report.shots >= 65 && report.maxShotVoices <= 12, "Fuego sostenido de rifle mantiene el máximo de doce capas activas");
                        Check(report.musicVirtualFrames == 0 && report.musicMaxRms > .00001f, "Música con señal PCM durante disparos, sin quedar virtualizada");
                        Check(report.ambienceVirtualFrames == 0 && ambience.isPlaying, "Ambiente continuo durante fuego sostenido");
                        menu.PauseGame(); break;
                    case 7:
                        Check(pool.All(s => !s.isPlaying), "Pausa detiene temporalmente todas las voces del pool tras LateUpdate");
                        pausedSamples = pool.Select(s => s.timeSamples).ToArray(); break;
                    case 8:
                        Check(pool.Select((s, i) => s.timeSamples == pausedSamples[i]).All(x => x), "Posición de audio de disparos no avanza en pausa");
                        menu.ResumeGame(); break;
                    case 9:
                        Check(pool.Any(s => s.isPlaying), "Reanudar permite terminar las colas pendientes");
                        feedback.enabled = false; Check(pool.All(s => !s.isPlaying), "Deshabilitar feedback limpia todas las voces");
                        float saved = MusicVolumeSettings.Volume;
                        typeof(MusicVolumeSettings).GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                        Check(Mathf.Approximately(MusicVolumeSettings.Volume, saved), "Inicialización nueva recupera el valor persistido");
                        Finish(); break;
                }
            }
            catch (Exception e) { Check(false, e.ToString()); Finish(); }
        }
        static void Check(bool pass, string label) { report.checks.Add((pass ? "PASS " : "FAIL ") + label); Debug.Log(report.checks.Last()); }
        static void Finish()
        {
            if (SessionState.GetBool(Key + ".Had", false)) PlayerPrefs.SetFloat(MusicVolumeSettings.PreferenceKey, SessionState.GetFloat(Key + ".Value", 1));
            else PlayerPrefs.DeleteKey(MusicVolumeSettings.PreferenceKey);
            PlayerPrefs.Save(); MusicVolumeSettings.SetVolume(SessionState.GetFloat(Key + ".Value", 1), false);
            File.WriteAllText(Folder + "/after.json", JsonUtility.ToJson(report, true));
            SessionState.SetInt(Key + ".Exit", report.checks.Any(s => s.StartsWith("FAIL")) ? 1 : 0); EditorApplication.ExitPlaymode();
        }
        static void Capture(string name)
        {
            var rt = (RenderTexture)typeof(CharacterCustomizationValidation).GetMethod("GameViewFramebuffer", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null); if (rt == null) return;
            var previous = RenderTexture.active; RenderTexture.active = rt; var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply();
            if (SystemInfo.graphicsUVStartsAtTop) { var p = image.GetPixels32(); var f = new Color32[p.Length]; for (int y = 0; y < rt.height; y++) Array.Copy(p, y * rt.width, f, (rt.height - y - 1) * rt.width, rt.width); image.SetPixels32(f); image.Apply(); }
            File.WriteAllBytes(Folder + "/" + name, image.EncodeToPNG()); Object.DestroyImmediate(image); RenderTexture.active = previous;
        }
    }
}
