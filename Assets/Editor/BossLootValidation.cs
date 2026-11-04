using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Survive.Combat;
using Survive.Economy;
using Survive.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UPP.ThirdPersonController;
using Object = UnityEngine.Object;

namespace Survive.Editor
{
    [InitializeOnLoad]
    public static class BossLootValidation
    {
        const string Key = "Survive.BossLootValidation";
        static readonly List<string> checks = new List<string>(), errors = new List<string>();
        static int stage, failures, frame;
        static double started, next;
        static UPPCharacterMovementComponent player;
        static PlayerInteractionController interaction;
        static Camera camera;
        static KeyPickup pickup;
        static PuertaConLlave door;
        static Keyboard keyboard;
        static PlayerKeyInventory Keys => player != null ? PlayerKeyInventory.ForPlayer(player.gameObject, true) : null;
        static bool HasKey => Keys != null && Keys.HasKey;
        static InputSettings originalSettings, testSettings;
        static Vector3 doorCenter, normal;
        static GameObject blocker;
        static readonly Dictionary<Renderer, bool> rendererStates = new Dictionary<Renderer, bool>();

        [Serializable] sealed class Result { public int passed, failed; public string[] checks, runtimeErrors; }

        static BossLootValidation()
        {
            EditorApplication.update += Tick;
            SceneManager.sceneLoaded += (scene, _) =>
            {
                if (!SessionState.GetBool(Key, false) || scene.path != BossLootSetup.Level) return;
                foreach (EnemySpawner item in Object.FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None)) item.enabled = false;
                foreach (BossSpawner item in Object.FindObjectsByType<BossSpawner>(FindObjectsSortMode.None)) item.enabled = false;
                foreach (EnemyHealth item in Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None)) item.gameObject.SetActive(false);
            };
            EditorApplication.playModeStateChanged += OnPlay;
        }

        [MenuItem("Survive/Boss/Validar drops y llave con E en Level 2")]
        public static void Run()
        {
            Directory.CreateDirectory(BossLootSetup.Evidence);
            SessionState.SetBool(Key, true);
            EditorSceneManager.OpenScene(BossLootSetup.Level);
            typeof(CharacterCustomizationValidation).GetMethod("PrepareBatchGameView", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            EditorApplication.EnterPlaymode();
        }

        static void OnPlay(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                checks.Clear(); errors.Clear(); failures = stage = 0;
                started = EditorApplication.timeSinceStartup; next = started + 3;
                originalSettings = InputSystem.settings;
                testSettings = Object.Instantiate(originalSettings);
                testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings = testSettings;
                keyboard = InputSystem.AddDevice<Keyboard>("BossLootValidationKeyboard");
                keyboard.MakeCurrent();
                Application.logMessageReceived += OnLog;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Key, false);
                if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(Key + ".Exit", 1));
            }
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup - started > 150) { Check(false, "Timeout"); Finish(); return; }
            if (EditorApplication.timeSinceStartup < next || Time.frameCount < frame + 3) return;
            frame = Time.frameCount; next = EditorApplication.timeSinceStartup + 2;
            try
            {
                switch (stage++)
                {
                    case 0: Setup(); break;
                    case 1:
                        interaction.RefreshTarget();
                        Check(interaction.CurrentInteractable == pickup, "Llave detectada por el mismo raycast de las armas");
                        Check(!HasKey && pickup != null, "Mirar y acercarse a llave no la recoge automáticamente");
                        ScreenCapture.CaptureScreenshot(Path.GetFullPath(BossLootSetup.Evidence + "/drops.png"));
                        rendererStates.Clear();
                        foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>())
                        { rendererStates[renderer] = renderer.enabled; renderer.enabled = false; }
                        // Entrar en el trigger tampoco debe recogerla.
                        MovePlayer(pickup.transform.position);
                        break;
                    case 2:
                        foreach (var state in rendererStates) if (state.Key != null) state.Key.enabled = state.Value;
                        Check(!HasKey && pickup != null, "Contacto físico con llave no recoge sin E");
                        MovePlayer(new Vector3(5000, 100.05f, 5000));
                        Aim(pickup.GetComponent<SphereCollider>().bounds.center);
                        Press();
                        break;
                    case 3:
                        Check(HasKey && (pickup == null || !pickup.gameObject.activeSelf), "E recoge la llave y actualiza posesión");
                        Release();
                        pickup = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Bosskey.prefab"), new Vector3(5010, 100.5f, 5000), Quaternion.identity).GetComponent<KeyPickup>();
                        Check(HasKey, "Aparecer otra llave no borra la que ya se tiene");
                        Check(pickup.CanInteract(player.gameObject), "Se pueden recoger otras llaves sin perder la que se lleva");
                        PositionAtDoor();
                        break;
                    case 4:
                        Check(door.gameObject.activeSelf && !door.IsOpen && HasKey, "Acercarse a puerta con llave no la abre automáticamente");
                        interaction.RefreshTarget();
                        Check(interaction.CurrentInteractable == door, "Puerta real de Level 2 seleccionada bajo la mira; objetivo=" + interaction.CurrentInteractable);
                        WriteGateAudit();
                        Press();
                        break;
                    case 5:
                        Check(door.IsOpen && door.gameObject.activeSelf, "E desbloquea puerta real de Level 2");
                        var barriers = new SerializedObject(door).FindProperty("blockingObjects");
                        Check(Enumerable.Range(0, barriers.arraySize).All(i => !((GameObject)barriers.GetArrayElementAtIndex(i).objectReferenceValue).activeSelf), "Abrir retira también las barreras físicas asociadas");
                        Check(!Physics.RaycastAll(doorCenter - normal * 1.1f, normal, 2.2f, ~0, QueryTriggerInteraction.Ignore)
                            .Any(hit => !hit.transform.IsChildOf(player.transform)), "Paso físico queda libre después de desbloquear");
                        Check(!HasKey, "Llave se consume únicamente al abrir");
                        Check(!door.Interact(player.gameObject), "Puerta abierta no puede consumir otra llave");
                        Release();
                        Keys.ResetKeys();
                        PuertaConLlave second = Object.FindObjectsByType<PuertaConLlave>(FindObjectsSortMode.None).Single(d => d.name == "PuertaZona3");
                        Check(!second.Interact(player.gameObject) && second.gameObject.activeSelf, "Sin llave la puerta permanece cerrada");
                        Check(second.InteractionPrompt.Contains("llave"), "Aviso de llave requerida");
                        door = second;
                        Keys.TryAddKey();
                        PositionAtDoor(true);
                        break;
                    case 6:
                        interaction.RefreshTarget();
                        Check(interaction.CurrentInteractable == door || interaction.CurrentInteractable is DoorBarrierInteraction proxy && proxy.Door == door,
                            "Puerta seleccionable también desde el lado opuesto");
                        Press();
                        break;
                    case 7:
                        Check(door.IsOpen && !HasKey, "E abre desde el lado opuesto y consume una sola llave");
                        Release();
                        TestBlockedInteraction();
                        break;
                    case 8:
                        interaction.RefreshTarget();
                        Check(!interaction.HasTarget && !interaction.TryInteract(), "Pared bloquea interacción con llave");
                        Object.DestroyImmediate(blocker);
                        Physics.SyncTransforms();
                        Time.timeScale = 0;
                        Check(!interaction.TryInteract() && !HasKey, "Pausa bloquea recogida y consumo");
                        Time.timeScale = 1;
                        Aim(pickup.GetComponent<SphereCollider>().bounds.center);
                        interaction.RefreshTarget();
                        Check(interaction.CurrentInteractable == pickup, "Retirar pared permite volver a seleccionar la llave");
                        player.GetComponent<PlayerHealth>().TakeDamage(10000);
                        Check(!interaction.TryInteract() && !pickup.Interact(player.gameObject), "Jugador muerto no puede usar llave");
                        Finish();
                        break;
                }
            }
            catch (Exception exception) { Check(false, exception.ToString()); Finish(); }
        }

        static void Setup()
        {
            foreach (string path in new[] { "Assets/Prefabs/coin.prefab", "Assets/Prefabs/coin 1.prefab", "Assets/Prefabs/Bosskey.prefab", "Assets/Prefabs/Bosskey 1.prefab" })
            {
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Check(asset != null && asset.GetComponentsInChildren<Renderer>().Length > 0, "Modelo 3D presente: " + path);
                Check(asset.GetComponentsInChildren<Renderer>().All(r => r.enabled && r.sharedMaterials.All(m => m != null && m.shader != null && m.shader.name == "Universal Render Pipeline/Lit")), "Material URP asignado: " + path);
                Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(asset) == 0 && asset.GetComponent<Collider>() != null, "Scripts y collider presentes: " + path);
            }
            PuertaConLlave[] doors = Object.FindObjectsByType<PuertaConLlave>(FindObjectsSortMode.None);
            Check(doors.Length == 5, "Cinco puertas del boss conectadas en Level 2");
            Check(doors.All(d => d.GetComponentsInChildren<Collider>().All(c => !c.isTrigger)), "Puertas mantienen colisión física antes de desbloquear");
            door = doors.OrderBy(d => d.name).First();
            player = Object.FindObjectsByType<UPPCharacterMovementComponent>(FindObjectsSortMode.None).Single(p => p.IsPlayer);
            player.UseDefaultControllerInput = false;
            interaction = player.GetComponent<PlayerInteractionController>();
            VendorTurretSetup.Set(interaction, "requireLockedCursor", false);
            foreach (var controller in Object.FindObjectsByType<UPP.ThirdPersonController.CameraSystems.UPPTPSCameraController>(FindObjectsSortMode.None)) controller.enabled = false;
            Rigidbody body = player.GetComponent<Rigidbody>(); body.constraints = RigidbodyConstraints.FreezeAll;
            camera = Camera.main;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(5000, 99.5f, 5000); floor.transform.localScale = new Vector3(30, 1, 30);
            MovePlayer(new Vector3(5000, 100.05f, 5000));
            var sun = new GameObject("DropsValidationLight").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.4f; sun.transform.rotation = Quaternion.Euler(40, -30, 0);
            RenderSettings.ambientLight = Color.gray;
            pickup = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Bosskey.prefab"), new Vector3(5000, 100.5f, 5002), Quaternion.identity).GetComponent<KeyPickup>();
            CoinPickup coin = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/coin.prefab"), new Vector3(5001, 100.4f, 5002), Quaternion.identity).GetComponent<CoinPickup>();
            Aim(pickup.GetComponent<SphereCollider>().bounds.center);
            PlayerWallet wallet = player.GetComponent<PlayerWallet>(); int gold = wallet.Coins;
            typeof(CoinPickup).GetMethod("OnTriggerEnter", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(coin, new object[] { player.GetComponent<Collider>() });
            Check(wallet.Coins == gold + 1, "Monedas siguen acreditando oro al recogerlas");
            Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/coin.prefab"), new Vector3(5001, 100.4f, 5002), Quaternion.identity);
        }

        static void MovePlayer(Vector3 point)
        {
            player.GetComponent<Rigidbody>().position = point;
            player.transform.position = point;
            player.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
        }
        static void Aim(Vector3 point)
        {
            Vector3 origin = player.transform.position + new Vector3(0, 1.4f, -1.5f);
            camera.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(point - origin));
            Physics.SyncTransforms();
        }
        static void PositionAtDoor(bool reverse = false)
        {
            Collider collider = door.GetComponent<Collider>();
            doorCenter = collider.bounds.center;
            normal = collider.bounds.size.x < collider.bounds.size.z ? Vector3.right : Vector3.forward;
            if (reverse) normal = -normal;
            float radius = normal.x != 0 ? collider.bounds.extents.x : collider.bounds.extents.z;
            Vector3 point = doorCenter - normal * (radius + 1.3f);
            point.y = collider.bounds.min.y + .05f;
            MovePlayer(point);
            Vector3 eye = point + Vector3.up * 1.4f - normal;
            camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(doorCenter - eye));
            Physics.SyncTransforms();
        }
        static void WriteGateAudit()
        {
            var gate = new List<string>();
            foreach (RaycastHit hit in Physics.RaycastAll(camera.transform.position, doorCenter - camera.transform.position, 8, ~0, QueryTriggerInteraction.Collide).OrderBy(h => h.distance))
                gate.Add($"RAY {hit.collider.name} pos={hit.collider.transform.position} distance={hit.distance:F3}");
            foreach (Collider collider in Physics.OverlapBox(door.GetComponent<Collider>().bounds.center, door.GetComponent<Collider>().bounds.extents + Vector3.one * .15f))
                gate.Add($"OVERLAP {collider.name} center={collider.bounds.center} size={collider.bounds.size}");
            File.WriteAllLines(BossLootSetup.Evidence + "/door-audit.txt", gate);
        }
        static void TestBlockedInteraction()
        {
            MovePlayer(new Vector3(5000, 100.05f, 5000));
            pickup.transform.position = new Vector3(5000, 100.5f, 5002);
            pickup.enabled = false; // Mantener la geometría del ensayo fija, sin balanceo.
            pickup.enabled = true;
            typeof(KeyPickup).GetField("startPosition", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(pickup, pickup.transform.position);
            Aim(pickup.GetComponent<SphereCollider>().bounds.center);
            blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.transform.position = new Vector3(5000, 101, 5001);
            blocker.transform.localScale = new Vector3(3, 3, .2f);
            Physics.SyncTransforms();
        }
        static void Press() => InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.E));
        static void Release() => InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        static void Check(bool ok, string text)
        {
            checks.Add((ok ? "PASS " : "FAIL ") + text);
            if (ok) Debug.Log("BOSS_LOOT_PASS " + text);
            else { failures++; Debug.LogWarning("BOSS_LOOT_FAIL " + text); }
        }
        static void OnLog(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack); }
        static void Finish()
        {
            Application.logMessageReceived -= OnLog;
            File.WriteAllText(BossLootSetup.Evidence + "/result.json", JsonUtility.ToJson(new Result { passed = checks.Count - failures, failed = failures, checks = checks.ToArray(), runtimeErrors = errors.ToArray() }, true));
            SessionState.SetInt(Key + ".Exit", failures == 0 && errors.Count == 0 ? 0 : 1);
            Time.timeScale = 1;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (originalSettings != null) InputSystem.settings = originalSettings;
            if (testSettings != null) Object.DestroyImmediate(testSettings);
            EditorApplication.ExitPlaymode();
        }
    }
}
