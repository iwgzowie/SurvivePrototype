using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Survive.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UPP.ThirdPersonController;
using UPP.ThirdPersonController.CameraSystems;
using Object = UnityEngine.Object;

namespace Survive.Editor
{
    [InitializeOnLoad]
    public static class CityAtmosphereValidation
    {
        const string Key = "Survive.CityAtmosphereValidation";
        static readonly List<string> checks = new List<string>(), errors = new List<string>(), baseline = new List<string>();
        static int stage, failures, frame;
        static double start, next;
        static CityAtmosphere atmosphere;
        static StormLightingSkybox storm;
        static Light sun;
        static Camera camera;
        static float pausedProgress;
        static Color pausedFog;
        [Serializable] class Result
        {
            public int passed, failed;
            public string scope = "Level 2 real en Play: cámara de gameplay, perfiles/stack URP, render de día/transición/noche, trigger existente, pausa, faroles/lluvia/relámpagos y aislamiento al cambiar de escena. Capturas del Game View; sin medición de FPS ni build distribuido.";
            public string[] checks, errors, baselineErrors;
        }

        static CityAtmosphereValidation()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Key, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    checks.Clear(); errors.Clear(); baseline.Clear(); failures = stage = 0;
                    start = EditorApplication.timeSinceStartup; next = start + 3;
                    Application.runInBackground = true; Application.logMessageReceived += OnLog;
                }
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    SessionState.SetBool(Key, false);
                    if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(Key + ".Exit", 1));
                }
            };
        }

        [MenuItem("Survive/Ambiente/Validar día y noche en Level 2")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Salí de Play Mode.");
            Directory.CreateDirectory(CityAtmosphereSetup.Logs);
            EditorSceneManager.OpenScene(CityAtmosphereSetup.Level);
            Prepare(); SessionState.SetBool(Key, true); EditorApplication.EnterPlaymode();
        }

        static void Prepare() => typeof(CharacterCustomizationValidation).GetMethod("PrepareBatchGameView", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);

        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            Prepare(); EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup - start > 160) { Check(false, "Timeout etapa " + stage); Finish(); return; }
            if (EditorApplication.timeSinceStartup < next || Time.frameCount < frame + 3) return;
            frame = Time.frameCount; next = EditorApplication.timeSinceStartup + .4;
            try
            {
                switch (stage)
                {
                    case 0:
                        atmosphere = Object.FindFirstObjectByType<CityAtmosphere>();
                        storm = Object.FindFirstObjectByType<StormLightingSkybox>();
                        Require(atmosphere != null && storm != null, "Ambiente integrado una vez en Level 2");
                        sun = Read<Light>(storm, "luzSol"); camera = Camera.main;
                        Require(camera != null && camera.GetUniversalAdditionalCameraData().renderPostProcessing, "Cámara real de gameplay con postproceso activo");
                        Check(camera.allowHDR && camera.GetUniversalAdditionalCameraData().antialiasing == AntialiasingMode.SubpixelMorphologicalAntiAliasing, "HDR y SMAA activos en cámara creada por UPP");
                        Check(camera.GetVolumeFrameworkUpdateMode() == VolumeFrameworkUpdateMode.EveryFrame, "Volúmenes evaluados cada frame durante la transición");
                        Check(atmosphere.DayVolume.sharedProfile != atmosphere.NightVolume.sharedProfile && atmosphere.NightVolume.priority > atmosphere.DayVolume.priority, "Perfiles separados con prioridad nocturna correcta");
                        Check(storm.NightProgress == 0 && atmosphere.NightVolume.weight == 0, "Partida comienza de día");
                        Check(Read<float>(storm, "tiempoTransicion") == 30, "Duración original de tormenta de 30 segundos preservada");
                        var street = Read<StreetLightsController>(storm, "streetLightsController");
                        Require(street.GetComponentsInChildren<Light>(true).Length > 0, "Luces de calle reales presentes en el controlador existente");
                        Check(street.GetComponentsInChildren<Light>(true).All(l => !l.enabled), "Luces de calle apagadas de día");
                        var waves = Object.FindFirstObjectByType<EnemySpawner>(); waves.CancelWaves();
                        foreach (var enemy in Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None)) if (enemy != null) Object.Destroy(enemy.gameObject);
                        var player = Object.FindFirstObjectByType<PlayerHealth>().gameObject;
                        player.GetComponent<UPPCharacterMovementComponent>().UseDefaultControllerInput = false;
                        player.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
                        Object.FindFirstObjectByType<UPPTPSCameraController>().EnableAutoRotator = false;
                        Cursor.lockState = CursorLockMode.None; Time.timeScale = 0;
                        stage++; next += 1; break;
                    case 1:
                        AssertProfile(false); Capture("01-day-gameplay.png");
                        Check(RenderSettings.ambientMode == AmbientMode.Trilight && RenderSettings.ambientSkyColor.maxColorComponent > .4f, "Luz ambiental diurna mantiene detalle en sombras");
                        Check(RenderSettings.fog && RenderSettings.fogDensity < .005f, "Bruma diurna suave sin tapar distancias de combate");
                        var zone = Object.FindFirstObjectByType<StormZoneSkybox>();
                        Require(zone != null, "Zona original de tormenta sigue presente");
                        var playerTransform = Read<Transform>(zone, "jugador");
                        Require(playerTransform != null && playerTransform.GetComponent<PlayerHealth>() != null, "Trigger original referencia al jugador de Level 2");
                        var dummy = new GameObject("NonPlayerStormTest"); var other = dummy.AddComponent<BoxCollider>();
                        zone.SendMessage("OnTriggerEnter", other); Check(!storm.IsTransitioning, "Enemigo u otro collider no activa la noche"); Object.Destroy(dummy);
                        zone.SendMessage("OnTriggerEnter", playerTransform.GetComponent<Collider>());
                        Require(storm.IsTransitioning, "Interacción del jugador con trigger original inicia la tormenta");
                        pausedProgress = storm.NightProgress; pausedFog = RenderSettings.fogColor;
                        stage++; next += 1; break;
                    case 2:
                        Check(storm.NightProgress == pausedProgress && RenderSettings.fogColor == pausedFog, "Pausa congela transición y ambiente");
                        Time.timeScale = 4; stage++; break;
                    case 3:
                        if (storm.NightProgress < .45f) break;
                        Time.timeScale = 0; stage++; next += .6; break;
                    case 4:
                        Check(storm.NightProgress > .4f && storm.NightProgress < .8f && Mathf.Abs(atmosphere.NightVolume.weight - storm.NightProgress) < .001f, "Mezcla nocturna usa exactamente el progreso de la tormenta");
                        Capture("02-transition-gameplay.png");
                        pausedProgress = storm.NightProgress; storm.ActivarNoche();
                        Check(storm.NightProgress == pausedProgress, "Activar nuevamente no reinicia la transición");
                        Check(RenderSettings.fogDensity > .0035f && RenderSettings.fogDensity < .005f, "Bruma interpolada sin salto a noche");
                        Time.timeScale = 4; stage++; break;
                    case 5:
                        if (storm.NightProgress < 1) break;
                        Time.timeScale = 0; stage++; next += .6; break;
                    case 6:
                        Require(!storm.IsTransitioning && atmosphere.NightVolume.weight == 1, "Transición completa mantiene perfil nocturno");
                        AssertProfile(true);
                        Check(Mathf.Abs(sun.intensity - .28f) < .001f && sun.color.b > sun.color.r && sun.transform.forward.y < 0, "Luz lunar tenue azul ilumina desde arriba");
                        Check(RenderSettings.ambientSkyColor.b >= .33f && RenderSettings.ambientEquatorColor.b >= .19f, "Noche mantiene relleno ambiental para leer enemigos");
                        Check(Read<StreetLightsController>(storm, "streetLightsController").GetComponentsInChildren<Light>(true).All(l => l.enabled), "Tormenta sigue encendiendo los faroles existentes");
                        var lightning = Read<LightningController>(storm, "lightningController");
                        Check(Read<bool>(lightning, "relampagosActivados"), "Relámpagos originales siguen activándose al terminar");
                        // Captura estable, entre relámpagos; no se altera la configuración guardada.
                        lightning.enabled = false; lightning.CancelInvoke();
                        Read<Light>(lightning, "luzRelampago1").intensity = 0; Read<Light>(lightning, "luzRelampago2").intensity = 0;
                        var rain = Object.FindFirstObjectByType<RainController>();
                        Check(Read<ParticleSystem>(rain, "lluvia").isPlaying, "Lluvia original sigue activa de noche");
                        stage++; next += .7; break;
                    case 7:
                        Capture("03-night-gameplay.png");
                        storm.ActivarNoche(); Check(storm.NightProgress == 1 && !storm.IsTransitioning, "Noche completa no vuelve al día al repetir activación");
                        // Segunda vista del mapa para revisar bruma, faroles y separación de edificios.
                        var rig = Object.FindFirstObjectByType<UPPTPSCameraController>(); rig.enabled = false;
                        camera.transform.SetParent(null); camera.transform.position += new Vector3(0, 15, -8);
                        camera.transform.rotation = Quaternion.Euler(35, camera.transform.eulerAngles.y, 0);
                        stage++; next += .8; break;
                    case 8:
                        Capture("04-night-city.png");
                        Time.timeScale = 1; UnityEngine.SceneManagement.SceneManager.LoadScene("SceneAlejandro"); stage++; next += 3; break;
                    case 9:
                        Check(Object.FindFirstObjectByType<CityAtmosphere>() == null, "Ambiente de gameplay no persiste en menú principal");
                        Check(Object.FindObjectsByType<Volume>(FindObjectsSortMode.None).All(v => v.name != "Day Post Process" && v.name != "Night Post Process"), "Volúmenes se descargan al abandonar Level 2");
                        Check(errors.Count == 0, "Sin errores nuevos durante día, transición, noche y salida");
                        Finish(); break;
                }
            }
            catch (Exception e) { Check(false, e.ToString()); Finish(); }
        }

        static void AssertProfile(bool night)
        {
            var stack = VolumeManager.instance.stack;
            Check(stack.GetComponent<Tonemapping>().mode.value == TonemappingMode.ACES, "Tonemapping ACES efectivo " + (night ? "noche" : "día"));
            float expected = night ? .24f : .16f;
            Check(Mathf.Abs(stack.GetComponent<Bloom>().intensity.value - expected) < .01f, "Bloom efectivo controlado " + (night ? "noche" : "día"));
            Check(stack.GetComponent<DepthOfField>().mode.value == DepthOfFieldMode.Off && stack.GetComponent<MotionBlur>().intensity.value == 0 && stack.GetComponent<ChromaticAberration>().intensity.value == 0, "Combate nítido sin desenfoque ni aberración " + (night ? "noche" : "día"));
        }

        static T Read<T>(object target, string field) => (T)target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
        static void Capture(string name)
        {
            var rt = (RenderTexture)typeof(CharacterCustomizationValidation).GetMethod("GameViewFramebuffer", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            Require(rt != null, "Framebuffer disponible: " + name);
            var old = RenderTexture.active; RenderTexture.active = rt;
            var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply();
            if (SystemInfo.graphicsUVStartsAtTop)
            {
                var p = image.GetPixels32(); var f = new Color32[p.Length];
                for (int y = 0; y < rt.height; y++) Array.Copy(p, y * rt.width, f, (rt.height - y - 1) * rt.width, rt.width);
                image.SetPixels32(f); image.Apply();
            }
            File.WriteAllBytes(CityAtmosphereSetup.Logs + "/" + name, image.EncodeToPNG());
            var directions = new[] { Vector3.up, Vector3.right, Vector3.down }; var samples = new Color[3];
            RenderSettings.ambientProbe.Evaluate(directions, samples);
            var pixels = image.GetPixels(); double mean = 0; int dark = 0, count = 0;
            for (int y = image.height / 4; y < image.height * 3 / 4; y++)
                for (int x = image.width / 10; x < image.width * 9 / 10; x++)
                {
                    var c = pixels[y * image.width + x]; float luma = c.r * .2126f + c.g * .7152f + c.b * .0722f;
                    mean += luma; if (luma < .035f) dark++; count++;
                }
            Debug.Log($"CITY_RENDER {name} mean={mean / count:F3} dark={100f * dark / count:F1}% ambientSH={samples[0]}/{samples[1]}/{samples[2]}");
            Object.DestroyImmediate(image); RenderTexture.active = old;
        }
        static void Check(bool pass, string label) { checks.Add((pass ? "PASS " : "FAIL ") + label); if (!pass) failures++; }
        static void Require(bool pass, string label) { Check(pass, label); if (!pass) throw new Exception(label); }
        static void OnLog(string message, string trace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (message.Contains("Assets/New Terrain 3.asset") || message.Contains("e03da6a7ced7b0f45b5982deba906d5c") || message.StartsWith("Problem detected while opening the Scene file: 'Assets/Scenes/SceneAlejandro.unity'.") || message.StartsWith("SocketException:") && trace.Contains("System.Net.")) baseline.Add(message);
            else errors.Add(message);
        }
        static void Finish()
        {
            Application.logMessageReceived -= OnLog;
            File.WriteAllText(CityAtmosphereSetup.Logs + "/validation.json", JsonUtility.ToJson(new Result { passed = checks.Count - failures, failed = failures, checks = checks.ToArray(), errors = errors.ToArray(), baselineErrors = baseline.ToArray() }, true));
            Time.timeScale = 1; SessionState.SetInt(Key + ".Exit", failures == 0 ? 0 : 1);
            Debug.Log("CITY_ATMOSPHERE_VALIDATION=" + (failures == 0 ? "PASS" : "FAIL") + " " + (checks.Count - failures) + "/" + checks.Count);
            EditorApplication.ExitPlaymode();
        }
    }
}
