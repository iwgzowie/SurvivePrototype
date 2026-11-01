using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Survive.Combat;
using Survive.Economy;
using Survive.Items;
using Survive.Turrets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UPP.ThirdPersonController;
using Object = UnityEngine.Object;
using InputKey = UnityEngine.InputSystem.Key;

namespace Survive.Editor
{
    [InitializeOnLoad]
    public static class LevelTwoGameplayValidation
    {
        const string Key = "Survive.LevelTwoGameplayValidation";
        const string Level = "Assets/Scenes/Level 2.unity";
        static readonly List<string> checks = new List<string>(), errors = new List<string>();
        static int stage, failures;
        static double started, nextStep;
        static PlayerWeaponInventory inventory;
        static PlayerWeaponController weapons;
        static PlayerTurretController builder;
        static PlayerWallet wallet;
        static UPPCharacterMovementComponent player;
        static Camera camera;
        static Keyboard keys;
        static float priorYaw;
        static Quaternion placedRotation;
        static Rigidbody baselineBody, reducedBody;
        static float baselineStart, reducedStart, baselinePeak, reducedPeak;
        static bool measuringJump;
        static InputSettings originalInputSettings, testInputSettings;

        [Serializable] sealed class Result
        {
            public string scope = "Inicio real de Level 2; Q/E en Play Mode; colocación y salto UPP en una zona controlada de esa sesión. Sin build ni partida completa.";
            public int passed, failed;
            public float baselineJumpHeight, reducedJumpHeight, heightRatio;
            public string[] checks, errors;
        }

        static LevelTwoGameplayValidation()
        {
            EditorApplication.update += Tick;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
                checks.Clear(); errors.Clear(); stage = failures = 0;
                measuringJump = false; baselinePeak = reducedPeak = 0;
                started = EditorApplication.timeSinceStartup; nextStep = started + .5;
                originalInputSettings = InputSystem.settings;
                testInputSettings = Object.Instantiate(originalInputSettings);
                testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                testInputSettings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
                InputSystem.settings = testInputSettings;
                Application.logMessageReceived += OnLog;
            };
        }

        [MenuItem("Survive/Gameplay/Validar inicio, torreta y salto de Level 2")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salir de Play Mode.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SessionState.SetBool(Key, true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!SessionState.GetBool(Key, false) || scene.path != Level) return;
            // Aislar los tres cambios sin alterar ni guardar los spawners de la escena.
            foreach (EnemySpawner spawner in Object.FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None)) spawner.enabled = false;
            foreach (BossSpawner spawner in Object.FindObjectsByType<BossSpawner>(FindObjectsSortMode.None)) spawner.enabled = false;
            foreach (EnemyHealth enemy in Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None)) enemy.gameObject.SetActive(false);
        }

        static void OnLog(string text, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(text + "\n" + stack);
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            double now = EditorApplication.timeSinceStartup;
            if (measuringJump)
            {
                baselinePeak = Mathf.Max(baselinePeak, baselineBody.position.y - baselineStart);
                reducedPeak = Mathf.Max(reducedPeak, reducedBody.position.y - reducedStart);
            }
            if (now - started > 150) { errors.Add("Timeout de validación"); Finish(); return; }
            if (now < nextStep) return;
            nextStep = now + .25;
            try
            {
                switch (stage++)
                {
                    case 0:
                        Time.timeScale = 1;
                        EditorSceneManager.LoadSceneInPlayMode(Level, new LoadSceneParameters(LoadSceneMode.Single));
                        nextStep = now + 1;
                        break;
                    case 1: CheckLevelStartup(); break;
                    case 2:
                        Check(weapons.EquippedWeapon == inventory.GetWeapon(0), "Pistola inicial seleccionada al habilitar controles");
                        inventory.enabled = false; inventory.enabled = true;
                        break;
                    case 3: BeginRotationChecks(); break;
                    case 4:
                        Check(Mathf.DeltaAngle(priorYaw, builder.PlacementYaw) < -1, $"Mantener Q rota izquierda: {priorYaw:F1} -> {builder.PlacementYaw:F1}");
                        priorYaw = builder.PlacementYaw; Press(InputKey.E); break;
                    case 5:
                        Check(Mathf.DeltaAngle(priorYaw, builder.PlacementYaw) > 1, $"Mantener E rota derecha: {priorYaw:F1} -> {builder.PlacementYaw:F1}");
                        Check(OccupiedSlots() == 1, "E durante colocación no recoge armas ni altera inventario");
                        priorYaw = builder.PlacementYaw; Press(InputKey.Q, InputKey.E); break;
                    case 6:
                        Check(Mathf.Abs(Mathf.DeltaAngle(priorYaw, builder.PlacementYaw)) < .1f, "Q y E simultáneas cancelan el giro");
                        Press(); priorYaw = builder.PlacementYaw;
                        player.transform.rotation = Quaternion.Euler(0, 235, 0);
                        break;
                    case 7:
                        Check(Mathf.Abs(Mathf.DeltaAngle(priorYaw, builder.PlacementYaw)) < .1f, "Girar al personaje no cambia la orientación de colocación");
                        Check(builder.RotatePlacement(73f), "Rotación manual admite un ángulo propio");
                        Check(!builder.RotatePlacement(float.NaN) && !builder.RotatePlacement(float.PositiveInfinity), "Rotación rechaza ángulos inválidos");
                        AimAtGround(); placedRotation = Quaternion.Euler(0, builder.PlacementYaw, 0);
                        Check(builder.TryPlace() && builder.Count == 0, "Colocación válida consume un kit");
                        AutoTurret deployed = Object.FindObjectsByType<AutoTurret>(FindObjectsSortMode.None).FirstOrDefault(item => item.enabled);
                        Check(deployed != null && Quaternion.Angle(deployed.transform.rotation, placedRotation) < .1f, "La base desplegada conserva el ángulo elegido");
                        Check(!builder.RotatePlacement(10), "Fuera de colocación Q/E no rotan torretas");
                        StartJumpMeasurement(); nextStep = now + 1.8; break;
                    case 8:
                        measuringJump = false;
                        float ratio = reducedPeak / baselinePeak;
                        Check(baselinePeak > 1 && reducedPeak > .1f, "Se midieron dos saltos con impulso UPP y gravedad real");
                        Check(Mathf.Abs(ratio - .3f) < .025f, $"Altura reducida al 30%: anterior={baselinePeak:F3} m nueva={reducedPeak:F3} m proporción={ratio:F3}");
                        Check(wallet.Coins == 0 && OccupiedSlots() == 1, "La sesión sigue con cero oro y una sola pistola");
                        Finish(); break;
                }
            }
            catch (Exception exception) { errors.Add(exception.ToString()); Finish(); }
        }

        static void CheckLevelStartup()
        {
            player = Object.FindObjectsByType<UPPCharacterMovementComponent>(FindObjectsSortMode.None).Single(item => item.IsPlayer);
            inventory = player.GetComponent<PlayerWeaponInventory>(); weapons = player.GetComponent<PlayerWeaponController>();
            builder = player.GetComponent<PlayerTurretController>(); wallet = player.GetComponent<PlayerWallet>();
            Check(player.gameObject.scene.path == Level, "Jugador real de Level 2 cargado");
            Check(wallet.Coins == 0 && !wallet.TrySpend(1), "Inicio con cero oro y sin posibilidad de gastar");
            WeaponItem pistol = inventory.GetWeapon(0);
            var definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Weapons/WD_Pistol.asset");
            Check(pistol != null && pistol.Definition == definition && OccupiedSlots() == 1, "Pistola en primer slot; los otros cinco espacios vacíos");
            Check(pistol != null && pistol.AmmoInMagazine == pistol.MagazineSize && pistol.ReserveAmmo == definition.StartingReserveAmmo,
                "Pistola conserva cargador y reserva del preset existente");
            Check(player.JumpForce > 0 && player.JumpForce < 3f, "Impulso de salto reducido respecto del valor anterior");
            VendorTurretSetup.Set(weapons, "requireLockedCursor", false);
            VendorTurretSetup.Set(builder, "requireLockedCursor", false);
            VendorTurretSetup.Set(player.GetComponent<PlayerInteractionController>(), "requireLockedCursor", false);
            player.UseDefaultControllerInput = false;
        }

        static void BeginRotationChecks()
        {
            Check(OccupiedSlots() == 1, "Deshabilitar y habilitar inventario no duplica la pistola inicial");
            foreach (var controller in Object.FindObjectsByType<UPP.ThirdPersonController.CameraSystems.UPPTPSCameraController>(FindObjectsSortMode.None)) controller.enabled = false;
            camera = Camera.main;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "ValidationFloor";
            floor.transform.position = new Vector3(5000, 99.5f, 5000); floor.transform.localScale = new Vector3(30, 1, 30);
            Rigidbody body = player.GetComponent<Rigidbody>(); body.position = new Vector3(5000, 100.05f, 5000);
            body.linearVelocity = Vector3.zero;
            player.transform.rotation = Quaternion.Euler(0, 67, 0);
            WeaponItem pickup = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Items/Weapons/Weapon_AssaultRifle.prefab"),
                new Vector3(5000, 100, 5002), Quaternion.identity).GetComponent<WeaponItem>();
            camera.transform.SetPositionAndRotation(new Vector3(5000, 101.5f, 4998), Quaternion.LookRotation(new Vector3(0, -.5f, 4)));
            Physics.SyncTransforms();
            Check(builder.TryAdd(builder.Definition) && builder.BeginPlacement(), "Inicia colocación con un kit de prueba");
            Check(Mathf.Abs(Mathf.DeltaAngle(builder.PlacementYaw, 0)) < .1f, "Ángulo inicial independiente de la rotación del personaje");
            Check(!inventory.TrySelect(1) && !player.GetComponent<PlayerInteractionController>().TryInteract(), "Construcción bloquea selección e interacción normal");
            keys = InputSystem.AddDevice<Keyboard>("GameplayValidationKeyboard");
            priorYaw = builder.PlacementYaw; Press(InputKey.Q);
        }

        static void AimAtGround()
        {
            Vector3 position = new Vector3(5000, 102, 4997);
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(new Vector3(5002.4f, 100, 5001) - position));
            Physics.SyncTransforms();
        }

        static void Press(params InputKey[] held)
        {
            InputSystem.QueueStateEvent(keys, new KeyboardState(held));
            InputSystem.Update();
            keys.MakeCurrent();
        }

        static int OccupiedSlots() => Enumerable.Range(0, PlayerWeaponInventory.Capacity).Count(index => inventory.GetWeapon(index) != null);

        static void StartJumpMeasurement()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VendorTurretSetup.PlayerPath);
            baselineBody = JumpBody(prefab, new Vector3(4995, 100.2f, 5006), 3f);
            reducedBody = JumpBody(prefab, new Vector3(5005, 100.2f, 5006), player.JumpForce);
            baselineStart = baselineBody.position.y; reducedStart = reducedBody.position.y;
            measuringJump = true;
        }

        static Rigidbody JumpBody(GameObject prefab, Vector3 position, float force)
        {
            GameObject model = Object.Instantiate(prefab, position, Quaternion.identity);
            foreach (Behaviour component in model.GetComponentsInChildren<Behaviour>(true)) component.enabled = false;
            var movement = model.GetComponent<UPPCharacterMovementComponent>();
            movement.UseDefaultControllerInput = false;
            movement.enabled = true;
            Animator animator = model.GetComponent<Animator>(); animator.enabled = true; animator.Rebind(); animator.Update(0); animator.enabled = false;
            Rigidbody body = model.GetComponent<Rigidbody>(); body.linearVelocity = Vector3.zero;
            movement.JumpForce = force; movement.IsGrounded = true; movement.CanJump = true; movement.IsJumping = false;
            movement.IsRolling = false; movement._Jump();
            return body;
        }

        static void Check(bool pass, string text)
        {
            checks.Add((pass ? "PASS " : "FAIL ") + text); if (!pass) failures++;
            Debug.Log("LEVEL2_GAMEPLAY_CHECK " + checks[checks.Count - 1]);
        }

        static void Finish()
        {
            SessionState.SetBool(Key, false); measuringJump = false;
            if (keys != null) InputSystem.RemoveDevice(keys);
            if (originalInputSettings != null) InputSystem.settings = originalInputSettings;
            if (testInputSettings != null) Object.DestroyImmediate(testInputSettings);
            Application.logMessageReceived -= OnLog;
            var result = new Result { passed = checks.Count - failures, failed = failures, baselineJumpHeight = baselinePeak,
                reducedJumpHeight = reducedPeak, heightRatio = baselinePeak > 0 ? reducedPeak / baselinePeak : 0,
                checks = checks.ToArray(), errors = errors.ToArray() };
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/LevelTwoGameplay-Validation.json", JsonUtility.ToJson(result, true));
            bool success = failures == 0 && errors.Count == 0;
            Debug.Log($"LEVEL2_GAMEPLAY_VALIDATION={(success ? "PASS" : "FAIL")} passed={result.passed} failed={failures} errors={errors.Count}");
            Time.timeScale = 1; EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
