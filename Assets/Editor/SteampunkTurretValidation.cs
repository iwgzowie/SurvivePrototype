using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Survive.Combat;
using Survive.Economy;
using Survive.Turrets;
using Survive.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UPP.ThirdPersonController;
using Object = UnityEngine.Object;

namespace Survive.Editor
{
    [InitializeOnLoad]
    public static class SteampunkTurretValidation
    {
        const string Key = "Survive.SteampunkTurretValidation", SceneName = "SteampunkTurretValidation";
        static readonly List<string> checks = new List<string>(), errors = new List<string>();
        static double started, next; static int stage, failures, frame, observedShots, blockedShots;
        static bool sawLeft, sawRight;
        static GameObject player, wall;
        static PlayerVendorController shop; static PlayerTurretController builder; static PlayerWallet wallet;
        static NpcVendor vendor; static AutoTurret turret; static EnemyHealth enemy; static Camera camera;
        static Quaternion baseRotation; static float blockedHealth;
        [Serializable] sealed class Result { public string scope = "Escena aislada con prefabs reales: compra por UI, preview, giro Q/E, colocación, pivotes, doble cañón, daño, obstáculos y pausa. Sin build ni partida completa."; public int passed, failed; public string[] checks, errors; }
        static SteampunkTurretValidation()
        {
            EditorApplication.update += Tick;
            SceneManager.sceneLoaded += (scene, mode) => { if (SessionState.GetBool(Key, false) && scene.name != SceneName) foreach (GameObject root in scene.GetRootGameObjects()) root.SetActive(false); };
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Key, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                { checks.Clear(); errors.Clear(); failures = stage = observedShots = 0; sawLeft = sawRight = false; started = EditorApplication.timeSinceStartup; next = started + 3; Application.runInBackground = true; Application.logMessageReceived += OnLog; }
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    SessionState.SetBool(Key, false);
                    if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(Key + ".Exit", 1));
                    else
                    {
                        Scene original = SceneManager.GetSceneByPath(SessionState.GetString(Key + ".Scene", "")); if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                        for (int i = SceneManager.sceneCount - 1; i >= 0; i--) { Scene scene = SceneManager.GetSceneAt(i); if (scene.name == SceneName) EditorSceneManager.CloseScene(scene, true); }
                    }
                }
            };
        }
        [MenuItem("Survive/Vendedor/Validar torreta Steampunk")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salir de Play Mode.");
            Directory.CreateDirectory(SteampunkTurretModelSetup.Folder); SessionState.SetString(Key + ".Scene", SceneManager.GetActiveScene().path);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive); scene.name = SceneName;
            SceneManager.SetActiveScene(scene); SessionState.SetBool(Key, true); Prepare(); EditorApplication.EnterPlaymode();
        }
        static void Prepare() => typeof(CharacterCustomizationValidation).GetMethod("PrepareBatchGameView", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            Prepare(); EditorApplication.QueuePlayerLoopUpdate();
            if (turret != null && turret.ShotsFired > observedShots)
            {
                observedShots = turret.ShotsFired; Vector3 local = Read<Transform>(turret, "barrelPitch").InverseTransformPoint(turret.LastShotOrigin);
                sawLeft |= local.x < -.2f; sawRight |= local.x > .2f;
            }
            if (EditorApplication.timeSinceStartup - started > 120) { Check(false, "Timeout"); Finish(); return; }
            if (EditorApplication.timeSinceStartup < next || Time.frameCount <= frame + 3) return;
            frame = Time.frameCount; next = EditorApplication.timeSinceStartup + 1;
            try
            {
                switch (stage++)
                {
                    case 0: CreateScenario(); break;
                    case 1:
                        Require(shop.TryOpen(vendor), "Abre el mercader real");
                        Check(vendor.Catalog.GetOffer(8).turret == builder.Definition && vendor.Catalog.GetOffer(8).price == 150, "Oferta conserva definición y precio de 150 monedas");
                        Check(builder.Definition.Icon != null && builder.Definition.Icon.texture.width == 256, "Icono del nuevo modelo asignado al catálogo"); break;
                    case 2:
                        Click(Read<Button[]>(shop, "buyButtons")[8], "Comprar torreta Steampunk");
                        Require(builder.Count == 1 && wallet.Coins == 350, "Compra por clic entrega un kit y cobra una vez"); Capture("07-vendor-new-icon.png"); shop.Close(); break;
                    case 3:
                        UPPCharacterMovementComponent movement = player.GetComponent<UPPCharacterMovementComponent>(); camera = movement.MyPivotCamera.mCamera; movement.MyPivotCamera.enabled = false;
                        camera.transform.SetPositionAndRotation(new Vector3(0, 2, -2), Quaternion.LookRotation(new Vector3(3, 0, 1) - new Vector3(0, 2, -2)));
                        Require(builder.BeginPlacement(), "Inicia colocación con el nuevo prefab"); break;
                    case 4:
                        Require(builder.HasValidPlacement, "Modelo puede colocarse sobre suelo libre");
                        AutoTurret preview = Read<AutoTurret>(builder, "preview");
                        Check(preview.GetComponentsInChildren<MeshFilter>().Length == 3 && preview.transform.Find("SteampunkBase") != null, "Preview usa las tres piezas del modelo importado");
                        Check(preview.GetComponentsInChildren<Collider>().All(c => !c.enabled), "Preview no deja colisiones activas");
                        Require(builder.RotatePlacement(65), "Rotación manual aplica 65 grados"); break;
                    case 5:
                        Capture("08-placement-preview.png");
                        Require(builder.TryPlace() && builder.Count == 0, "Colocar consume exactamente un kit");
                        turret = Object.FindObjectsByType<AutoTurret>(FindObjectsSortMode.None).First(t => t.enabled);
                        Check(Mathf.Abs(Mathf.DeltaAngle(turret.transform.eulerAngles.y, 65)) < .1f, "Torreta conserva orientación elegida al colocar");
                        Check(turret.GetComponent<NavMeshObstacle>().carving, "Conserva obstáculo de navegación");
                        Check(turret.GetComponentsInChildren<MeshRenderer>().All(r => r.sharedMaterial != null && r.sharedMaterial.shader.name == "Universal Render Pipeline/Lit" && r.sharedMaterial.GetTexture("_BaseMap") != null), "Todas las piezas tienen textura y material URP");
                        baseRotation = turret.transform.Find("SteampunkBase").rotation;
                        PositionCamera(); break;
                    case 6:
                        Capture("09-placed-turret.png");
                        enemy = Zombie(turret.transform.position + new Vector3(0, 2, 5));
                        var platform = GameObject.CreatePrimitive(PrimitiveType.Cube); platform.transform.position = enemy.transform.position - Vector3.up * 1.5f; platform.transform.localScale = new Vector3(3, 1, 3);
                        break;
                    case 7:
                        Require(turret.ShotsFired >= 2 && enemy.CurrentHealth < enemy.MaxHealth, "Torreta detecta y daña al zombi automáticamente");
                        Check(sawLeft && sawRight, "Alterna las salidas de ambos cañones");
                        Check(Mathf.Approximately(enemy.MaxHealth - enemy.CurrentHealth, turret.ShotsFired * turret.Definition.Damage), "Cada disparo aplica el daño original una sola vez");
                        Check(Quaternion.Angle(baseRotation, turret.transform.Find("SteampunkBase").rotation) < .01f, "La base permanece fija al apuntar");
                        Transform yaw = Read<Transform>(turret, "head"), pitch = Read<Transform>(turret, "barrelPitch");
                        Check(Vector3.Dot(yaw.up, Vector3.up) > .999f && Quaternion.Angle(pitch.localRotation, Quaternion.identity) > 1, "Giro horizontal y elevación funcionan en pivotes separados");
                        Transform last = Read<Transform>(turret, turret.ShotsFired % 2 == 1 ? "muzzle" : "alternateMuzzle");
                        Check(Vector3.Distance(turret.LastShotOrigin, last.position) < .01f && Vector3.Dot(last.forward, (enemy.GetComponent<Collider>().bounds.center - last.position).normalized) > .98f, "Disparo y cañón visual están alineados");
                        Capture("10-tracking-and-firing.png");
                        wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "TurretSightWall"; wall.transform.position = turret.transform.position + new Vector3(0, 1.5f, 2.5f); wall.transform.localScale = new Vector3(4, 3, .25f);
                        Physics.SyncTransforms(); blockedShots = turret.ShotsFired; blockedHealth = enemy.CurrentHealth; break;
                    case 8:
                        Check(turret.ShotsFired == blockedShots && enemy.CurrentHealth == blockedHealth && !turret.TryFire(), "Pared bloquea adquisición y disparos desde ambos cañones");
                        Object.DestroyImmediate(wall); Time.timeScale = 0; turret.AcquireTarget(); Check(!turret.TryFire(), "Pausa impide disparar"); Time.timeScale = 1;
                        enemy.transform.position = turret.transform.position + new Vector3(-5, 2, 0); Physics.SyncTransforms(); break;
                    case 9:
                        Check(turret.CurrentTarget == enemy && turret.ShotsFired > blockedShots, "Recupera objetivo y sigue al zombi que cambia de lado");
                        Check(Quaternion.Angle(baseRotation, turret.transform.Find("SteampunkBase").rotation) < .01f, "Reorientar el cabezal no gira la base");
                        enemy.TakeDamage(enemy.MaxHealth); blockedShots = turret.ShotsFired;
                        TurretItem kit = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(VendorTurretSetup.ItemPath), new Vector3(-2, 1, 0), Quaternion.identity).GetComponent<TurretItem>();
                        Check(kit.transform.Find("SteampunkTurretModel") != null && kit.Interact(player) && builder.Count == 1, "Pickup del mundo usa el modelo nuevo y entrega un kit"); break;
                    case 10:
                        Check(turret.ShotsFired == blockedShots, "Deja de disparar al zombi muerto");
                        Check(turret.Definition.Damage == 20 && turret.Definition.ShotsPerSecond == 2 && turret.Definition.Range == 15, "Conserva daño y alcance con cadencia reducida"); Finish(); break;
                }
            }
            catch (Exception e) { Check(false, e.ToString()); Finish(); }
        }
        static void CreateScenario()
        {
            var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.7f; sun.transform.rotation = Quaternion.Euler(40, -25, 0); RenderSettings.ambientLight = Color.gray;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.position = new Vector3(0, -.5f, 0); floor.transform.localScale = new Vector3(50, 1, 50);
            player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(VendorTurretSetup.PlayerPath), new Vector3(0, .05f, 0), Quaternion.identity);
            player.GetComponent<UPPCharacterMovementComponent>().UseDefaultControllerInput = false;
            shop = player.GetComponent<PlayerVendorController>(); builder = player.GetComponent<PlayerTurretController>(); wallet = player.GetComponent<PlayerWallet>(); wallet.AddCoins(500);
            VendorTurretSetup.Set(shop, "requireLockedCursor", false); VendorTurretSetup.Set(builder, "requireLockedCursor", false);
            vendor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(VendorTurretSetup.VendorPath), new Vector3(0, 0, 2), Quaternion.identity).GetComponent<NpcVendor>();
            Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SyntyGameplayHudSetup.PrefabPath));
        }
        static EnemyHealth Zombie(Vector3 position)
        {
            var holder = new GameObject("InactiveEnemySpawn"); holder.SetActive(false);
            GameObject root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Players/EnemyChasing.prefab"), holder.transform);
            root.GetComponent<NavMeshAgent>().enabled = false; root.GetComponent<EnemyBaseController>().enabled = false; root.transform.position = position; root.transform.SetParent(null, true); Object.Destroy(holder);
            EnemyHealth health = root.GetComponent<EnemyHealth>(); health.ApplyWaveMultipliers(20); return health;
        }
        static void PositionCamera() { Vector3 target = turret.transform.position + Vector3.up * .7f; camera.transform.SetPositionAndRotation(target + new Vector3(2.5f, 1.3f, 3), Quaternion.LookRotation(-new Vector3(2.5f, 1.3f, 3))); }
        static T Read<T>(object source, string name) => (T)source.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(source);
        static void Click(Button button, string label) => typeof(CharacterCustomizationValidation).GetMethod("ClickThroughEventSystem", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { button, label });
        static void Capture(string file)
        {
            var rt = (RenderTexture)typeof(CharacterCustomizationValidation).GetMethod("GameViewFramebuffer", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null); if (rt == null) throw new InvalidOperationException("Framebuffer ausente");
            var photo = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = rt; photo.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                if (SystemInfo.graphicsUVStartsAtTop)
                {
                    Color32[] p = photo.GetPixels32(); for (int y = 0; y < rt.height / 2; y++) for (int x = 0; x < rt.width; x++) { int a = y * rt.width + x, b = (rt.height - 1 - y) * rt.width + x; Color32 pixel = p[a]; p[a] = p[b]; p[b] = pixel; } photo.SetPixels32(p);
                }
                photo.Apply(); File.WriteAllBytes(SteampunkTurretModelSetup.Folder + "/" + file, photo.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(photo); }
        }
        static void OnLog(string text, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(text + "\n" + stack); }
        static void Check(bool ok, string text) { checks.Add((ok ? "PASS " : "FAIL ") + text); if (!ok) failures++; Debug.Log("STEAMPUNK_TURRET_" + (ok ? "PASS " : "FAIL ") + text); }
        static void Require(bool ok, string text) { Check(ok, text); if (!ok) throw new InvalidOperationException(text); }
        static void Finish() { Application.logMessageReceived -= OnLog; File.WriteAllText(SteampunkTurretModelSetup.Folder + "/result.json", JsonUtility.ToJson(new Result { passed = checks.Count - failures, failed = failures, checks = checks.ToArray(), errors = errors.ToArray() }, true)); SessionState.SetInt(Key + ".Exit", failures == 0 && errors.Count == 0 ? 0 : 1); EditorApplication.ExitPlaymode(); }
    }
}
