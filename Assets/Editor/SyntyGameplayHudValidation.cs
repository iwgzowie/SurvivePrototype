using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Survive.Combat;
using Survive.Economy;
using Survive.Items;
using Survive.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Survive.Editor
{
    [InitializeOnLoad]
    public static class SyntyGameplayHudValidation
    {
        const string Key = "Survive.SyntyHudValidation";
        static readonly List<string> checks = new List<string>(), errors = new List<string>();
        static int stage, failures, frame;
        static double started, next;
        static SyntyGameplayHUD hud;
        static EnemySpawner waves;
        static WeaponItem weapon;
        static GameObject testBoss;
        static bool finishing;
        static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        [Serializable] sealed class Result { public int passed, failed; public string scope; public string[] checks, runtimeErrors; }

        static SyntyGameplayHudValidation()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlay;
            SceneManager.sceneLoaded += OnScene;
        }
        private static void OnScene(Scene scene, LoadSceneMode mode)
        {
            if (!SessionState.GetBool(Key, false)) return;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (EnemySpawner source in root.GetComponentsInChildren<EnemySpawner>()) source.enabled = false;
                foreach (BossSpawner source in root.GetComponentsInChildren<BossSpawner>()) source.enabled = false;
                foreach (EnemyHealth source in root.GetComponentsInChildren<EnemyHealth>()) source.gameObject.SetActive(false);
            }
        }
        private static void OnPlay(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                checks.Clear(); errors.Clear(); failures = stage = 0; finishing = false;
                started = EditorApplication.timeSinceStartup; next = started + 3;
                Application.logMessageReceived += OnLog;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Key, false);
                if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(Key + ".Exit", 1));
            }
        }
        [MenuItem("Survive/UI/Validar HUD Synty en Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salí de Play Mode antes de validar.");
            SessionState.SetBool(Key, true);
            Scene scene = SceneManager.GetSceneByPath("Assets/Scenes/Level 2 UI.unity");
            if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/Level 2 UI.unity");
            SceneManager.SetActiveScene(scene);
            PrepareGameView(); EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || finishing) return;
            EditorApplication.QueuePlayerLoopUpdate(); PrepareGameView();
            if (EditorApplication.timeSinceStartup - started > 180) { Check(false, "Timeout"); Finish(); return; }
            if (EditorApplication.timeSinceStartup < next || Time.frameCount <= frame + 3) return;
            frame = Time.frameCount; next = EditorApplication.timeSinceStartup + 2;
            try
            {
                switch (stage++)
                {
                    case 0:
                        hud = Object.FindFirstObjectByType<SyntyGameplayHUD>();
                        Require(hud != null && hud.IsBound, "HUD Synty conectado al jugador de Level 2 UI");
                        Check(Object.FindObjectsByType<SyntyGameplayHUD>(FindObjectsSortMode.None).Length == 1, "Un solo HUD de gameplay");
                        Check(hud.BoundInventory.SlotCount == 6 && Read<SyntyGameplayHUD.Slot[]>("slots").Length == 6, "Seis slots conectados al inventario real");
                        Check(Label("healthText").Contains("100") && Mathf.Approximately(Read<Slider>("healthBar").value, 1), "Vida inicial y barra completas");
                        Check(Read<Image>("healthFill").sprite != null, "Barra de vida usa sprite Synty");
                        Check(hud.GetComponentsInChildren<TMP_Text>(true).All(t => t.font != null), "Tipografía asignada a todos los textos");
                        Check(hud.GetComponentsInChildren<Graphic>(true).All(g => !g.raycastTarget), "HUD no bloquea raycasts del gameplay");
                        Canvas.ForceUpdateCanvases();
                        Check(hud.GetComponentsInChildren<TMP_Text>().All(t => !t.isTextOverflowing), "Textos activos completos dentro de sus campos");
                        Check(hud.GetComponentsInChildren<Graphic>().All(g => FitsScreen(g.rectTransform)), "Elementos visibles dentro del viewport");
                        Check(Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(c => c.name == "WaveHUD" || c.name == "PlayerInteractionHUD").All(c => !c.enabled), "Canvases anteriores ocultos");
                        Check(hud.BoundHealth.GetComponent<PlayerVendorController>().ExternalGameplayHud, "Contadores de tienda delegados al HUD Synty");
                        VendorTurretSetup.Set(hud.BoundHealth.GetComponent<PlayerWeaponController>(), "requireLockedCursor", false);
                        waves = Object.FindFirstObjectByType<EnemySpawner>();
                        Capture("01-level2-ui.png");
                        hud.BoundHealth.TakeDamage(37); hud.BoundWallet.AddCoins(237);
                        break;
                    case 1:
                        Check(Label("healthText").Contains("63") && Mathf.Abs(Read<Slider>("healthBar").value - .63f) < .001f, "Daño real actualiza vida y barra");
                        Check(Label("coinsText") == "237", "Monedas reales actualizan contador");
                        hud.BoundHealth.Heal(17);
                        GameObject rifle = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Items/Weapons/Weapon_AssaultRifle.prefab"));
                        Check(hud.BoundInventory.TryCollect(rifle.GetComponent<WeaponItem>()), "Arma recogida entra al inventario existente");
                        break;
                    case 2:
                        Check(Label("healthText").Contains("80") && Mathf.Abs(Read<Slider>("healthBar").value - .8f) < .001f, "Curación real actualiza vida y barra");
                        var slots = Read<SyntyGameplayHUD.Slot[]>("slots");
                        Check(slots.Count(s => s.icon.enabled) >= 2, "Iconos Synty representan las armas que se tienen");
                        Check(slots.Where(s => s.icon.enabled).All(s => AssetDatabase.GetAssetPath(s.icon.sprite).StartsWith(SyntyGameplayHudSetup.Source)), "Iconos de armas provienen del paquete importado");
                        hud.BoundInventory.TrySelect(0);
                        weapon = hud.BoundHealth.GetComponent<PlayerWeaponController>().EquippedWeapon;
                        Require(weapon != null, "Arma equipada para validar munición");
                        weapon.TryConsumeRound();
                        break;
                    case 3:
                        Check(Label("ammoText") == weapon.AmmoInMagazine.ToString("00"), "Consumo de bala actualiza munición del HUD");
                        Check(Label("reserveText").Contains(weapon.ReserveAmmo.ToString()), "Reserva coincide con arma real");
                        Check(weapon.TryStartReload(), "Recarga real iniciada");
                        next = EditorApplication.timeSinceStartup + .15;
                        break;
                    case 4:
                        Check(Label("weaponStatus").Contains("RECARGANDO"), "Estado de recarga visible");
                        Check(Read<Slider>("reloadBar").gameObject.activeSelf, "Barra de progreso de recarga visible");
                        Capture("02-reload.png"); weapon.TickReload(weapon.ReloadDuration + 1);
                        break;
                    case 5:
                        Check(Label("ammoText") == weapon.MagazineSize.ToString("00"), "Recarga completada refleja cargador lleno");
                        Check(!Read<Slider>("reloadBar").gameObject.activeSelf, "Progreso desaparece al completar recarga");
                        Check(hud.BoundInventory.TrySelect(2), "Puede seleccionarse un slot vacío");
                        SetBacking(waves, "CurrentWaveIndex", 2); SetBacking(waves, "State", WaveRunState.Spawning);
                        PlayerKeyInventory.ForPlayer(hud.BoundHealth.gameObject, true).TryAddKey();
                        break;
                    case 6:
                        Check(Label("weaponName") == "SIN ARMA" && Label("ammoText") == "--", "Slot vacío se presenta sin datos ficticios");
                        Check(Read<SyntyGameplayHUD.Slot[]>("slots")[2].selected.activeSelf, "Selección visual sigue al slot activo");
                        Check(Label("waveText") == "OLEADA 03", "Número de oleada proviene del spawner");
                        Check(Label("keyText") == "LLAVE DISPONIBLE", "Estado de llave actualizado");
                        SetBacking(waves, "State", WaveRunState.Resting); SetBacking(waves, "RestTimeRemaining", 75f);
                        break;
                    case 7:
                        Check(Label("phaseText").Contains("DESCANSO") && Label("phaseText").Contains("01:15"), "Temporizador de descanso refleja al spawner");
                        testBoss = new GameObject("Jefe de validación"); EnemyHealth boss = testBoss.AddComponent<EnemyHealth>();
                        typeof(SyntyGameplayHUD).GetMethod("OnBoss", Flags).Invoke(hud, new object[] { boss }); boss.TakeDamage(25);
                        break;
                    case 8:
                        Check(Read<GameObject>("bossRoot").activeSelf && Mathf.Abs(Read<Slider>("bossBar").value - .75f) < .001f, "Barra de jefe sigue la salud real");
                        Object.Destroy(testBoss); hud.BoundInventory.TrySelect(1);
                        hud.BoundHealth.TakeDamage(60);
                        break;
                    case 9:
                        Check(Read<Image>("healthFill").color.r > .9f && Label("healthText").Contains("20"), "Vida crítica usa advertencia roja y valor correcto");
                        Check(!Read<GameObject>("bossRoot").activeSelf, "Barra de jefe se retira al desaparecer el jefe");
                        Capture("03-combat-state.png");
                        EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Level 2.unity", new LoadSceneParameters(LoadSceneMode.Single));
                        break;
                    case 10:
                        hud = Object.FindFirstObjectByType<SyntyGameplayHUD>();
                        Require(hud != null && hud.IsBound && hud.gameObject.scene.name == "Level 2", "Mismo HUD conectado en el mapa Level 2");
                        Check(Object.FindObjectsByType<SyntyGameplayHUD>(FindObjectsSortMode.None).Length == 1, "Cambiar de escena no duplica HUD");
                        Check(Label("coinsText") == hud.BoundWallet.Coins.ToString("N0"), "Contadores conectados al nuevo jugador");
                        Capture("04-level2.png"); Finish();
                        break;
                }
            }
            catch (Exception exception) { Check(false, exception.ToString()); Finish(); }
        }
        private static T Read<T>(string field) => (T)typeof(SyntyGameplayHUD).GetField(field, Flags).GetValue(hud);
        private static string Label(string field) => Read<TMP_Text>(field).text;
        private static bool FitsScreen(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            return corners.All(p => p.x >= -1 && p.y >= -1 && p.x <= Screen.width + 1 && p.y <= Screen.height + 1);
        }
        private static void SetBacking(object target, string field, object value) => target.GetType().GetField("<" + field + ">k__BackingField", Flags).SetValue(target, value);
        private static void PrepareGameView() => typeof(CharacterCustomizationValidation).GetMethod("PrepareBatchGameView", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        private static void Capture(string file)
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
            RenderTexture rt = (RenderTexture)typeof(CharacterCustomizationValidation).GetMethod("GameViewFramebuffer", flags).Invoke(null, null);
            if (rt == null || !rt.IsCreated()) { Check(false, "Framebuffer ausente para " + file); return; }
            var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                if (SystemInfo.graphicsUVStartsAtTop)
                {
                    Color32[] rows = image.GetPixels32();
                    for (int y = 0; y < image.height / 2; y++)
                        for (int x = 0; x < image.width; x++)
                        { int a = y * image.width + x, b = (image.height - 1 - y) * image.width + x; Color32 pixel = rows[a]; rows[a] = rows[b]; rows[b] = pixel; }
                    image.SetPixels32(rows);
                }
                image.Apply(); File.WriteAllBytes(SyntyGameplayHudSetup.Output + "/" + file, image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
        }
        private static void Check(bool ok, string text) { checks.Add((ok ? "PASS " : "FAIL ") + text); if (!ok) failures++; Debug.Log("SYNTY_HUD_" + (ok ? "PASS " : "FAIL ") + text); }
        private static void Require(bool ok, string text) { Check(ok, text); if (!ok) throw new InvalidOperationException(text); }
        private static void OnLog(string text, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(text + "\n" + stack); }
        private static void Finish()
        {
            finishing = true; Application.logMessageReceived -= OnLog;
            File.WriteAllText(SyntyGameplayHudSetup.Output + "/validation-result.json", JsonUtility.ToJson(new Result { passed = checks.Count - failures, failed = failures,
                scope = "HUD real en Level 2 UI y Level 2, datos de salud/economía/inventario y estados controlados de oleadas/jefe. Sin build ni partida completa.", checks = checks.ToArray(), runtimeErrors = errors.ToArray() }, true));
            SessionState.SetInt(Key + ".Exit", failures == 0 && errors.Count == 0 ? 0 : 1);
            EditorApplication.ExitPlaymode();
        }
    }
}
