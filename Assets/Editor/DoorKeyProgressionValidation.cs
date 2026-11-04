using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Survive.Combat;
using Survive.Items;
using Survive.UI;
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
    public static class DoorKeyProgressionValidation
    {
        const string Key = "Survive.DoorKeyProgressionValidation", TestScene = "DoorKeyProgressionValidation";
        static readonly List<string> checks = new List<string>(), errors = new List<string>();
        static int stage, failures, frame; static double started, next;
        static GameObject gates; static PuertaConLlave[] doors; static GameObject player;
        static PlayerKeyInventory keys; static PlayerInteractionController interaction; static Camera camera;
        static KeyPickup pickup; static Keyboard keyboard; static InputSettings previousSettings, settings;
        static SyntyGameplayHUD hud;
        [Serializable] sealed class Result { public string scope = "Puertas y barreras copiadas con su configuración real de Level 2; prefabs reales de jugador/llave/HUD en escena aislada. E, orden, doble puerta, consumo, múltiples llaves y separación por jugador. Sin build ni partida completa."; public int passed, failed; public string[] checks, errors; }
        static DoorKeyProgressionValidation()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Key, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    checks.Clear(); errors.Clear(); stage = failures = 0; started = EditorApplication.timeSinceStartup; next = started + 3;
                    Application.runInBackground = true; previousSettings = InputSystem.settings; settings = Object.Instantiate(previousSettings);
                    settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    InputSystem.settings = settings; keyboard = InputSystem.AddDevice<Keyboard>("DoorProgressionKeyboard"); Application.logMessageReceived += OnLog;
                }
                if (state == PlayModeStateChange.EnteredEditMode)
                { SessionState.SetBool(Key, false); if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(Key + ".Exit", 1)); }
            };
        }
        [MenuItem("Survive/Boss/Validar secuencia Zona2 Zona3 Zona4")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salí de Play Mode.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(DoorKeyProgressionSetup.Folder);
            Scene source = EditorSceneManager.OpenScene(DoorKeyProgressionSetup.Level, OpenSceneMode.Single);
            PuertaConLlave[] originals = DoorKeyProgressionSetup.Names.Select(n => source.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PuertaConLlave>(true)).Single(d => d.name == n)).ToArray();
            Scene test = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); test.name = TestScene; SceneManager.SetActiveScene(test);
            var root = new GameObject("ConfiguredLevel2Doors"); var map = new Dictionary<PuertaConLlave, PuertaConLlave>();
            for (int i = 0; i < originals.Length; i++)
            {
                PuertaConLlave original = originals[i]; GameObject clone = Object.Instantiate(original.gameObject); clone.name = original.name;
                clone.transform.SetParent(root.transform, true); clone.transform.SetPositionAndRotation(original.transform.position, original.transform.rotation); clone.transform.localScale = original.transform.lossyScale;
                Bounds bounds = clone.GetComponent<Collider>().bounds;
                Vector3 offset = new Vector3(5000 + i * 300 - bounds.center.x, 100 - bounds.min.y, 5005 - bounds.center.z);
                clone.transform.position += offset; PuertaConLlave door = clone.GetComponent<PuertaConLlave>(); map[original] = door;
                var data = new SerializedObject(door); var barriers = new SerializedObject(original).FindProperty("blockingObjects"); var copies = data.FindProperty("blockingObjects"); copies.arraySize = barriers.arraySize;
                for (int j = 0; j < barriers.arraySize; j++)
                {
                    GameObject barrier = (GameObject)barriers.GetArrayElementAtIndex(j).objectReferenceValue;
                    GameObject copy = Object.Instantiate(barrier, root.transform); copy.name = barrier.name; copy.transform.SetPositionAndRotation(barrier.transform.position + offset, barrier.transform.rotation); copy.transform.localScale = barrier.transform.lossyScale;
                    copies.GetArrayElementAtIndex(j).objectReferenceValue = copy;
                    foreach (DoorBarrierInteraction proxy in copy.GetComponentsInChildren<DoorBarrierInteraction>(true)) VendorTurretSetup.Set(proxy, "door", door);
                }
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var pair in map)
            {
                var data = new SerializedObject(pair.Value);
                foreach (string name in new[] { "prerequisiteDoors", "linkedDoors" })
                {
                    var property = data.FindProperty(name);
                    for (int i = 0; i < property.arraySize; i++)
                        property.GetArrayElementAtIndex(i).objectReferenceValue = map[(PuertaConLlave)property.GetArrayElementAtIndex(i).objectReferenceValue];
                }
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.CloseScene(source, true); SessionState.SetBool(Key, true); Prepare(); EditorApplication.EnterPlaymode();
        }
        static void Prepare() => typeof(CharacterCustomizationValidation).GetMethod("PrepareBatchGameView", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            Prepare(); EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup - started > 160) { Check(false, "Timeout"); Finish(); return; }
            if (EditorApplication.timeSinceStartup < next || Time.frameCount <= frame + 3) return;
            frame = Time.frameCount; next = EditorApplication.timeSinceStartup + .8;
            try
            {
                switch (stage++)
                {
                    case 0: CreateScenario(); break;
                    case 1:
                        Check(keys.KeyCount == 0 && doors.All(d => !d.IsOpen && d.gameObject.activeSelf), "Inicio sin llaves ni puertas abiertas");
                        Check(!doors[0].Interact(player) && keys.KeyCount == 0, "Primera puerta también requiere una llave");
                        Check(Read<PuertaConLlave[]>(doors[1], "prerequisiteDoors").Single() == doors[0], "Zona3 referencia Zona2 real");
                        Check(Read<PuertaConLlave[]>(doors[2], "linkedDoors").Single() == doors[3] && Read<PuertaConLlave[]>(doors[3], "linkedDoors").Single() == doors[2], "Zona4 y Zona4.1 están enlazadas en ambos sentidos");
                        camera = player.GetComponent<UPPCharacterMovementComponent>().MyPivotCamera.mCamera; player.GetComponent<UPPCharacterMovementComponent>().MyPivotCamera.enabled = false;
                        SpawnKey(); break;
                    case 2:
                        Aim(pickup.GetComponent<Collider>().bounds.center); interaction.RefreshTarget();
                        Require(interaction.CurrentInteractable == pickup, "Llave seleccionada por el raycast de interacción");
                        Check(keys.KeyCount == 0, "Mirar o acercarse no recoge automáticamente"); Press(); break;
                    case 3:
                        Release(); Require(keys.KeyCount == 1, "E recoge la primera llave");
                        foreach (PuertaConLlave door in doors.Skip(1)) Check(!door.Interact(player), "No saltea el orden hacia " + door.name);
                        Check(keys.KeyCount == 1 && doors.All(d => !d.IsOpen), "Intentos fuera de orden no gastan la llave ni abren barreras");
                        PositionAtDoor(doors[1]); interaction.RefreshTarget(); Check(interaction.CurrentPrompt.Contains("PuertaZona2"), "Aviso indica abrir primero PuertaZona2");
                        PositionAtDoor(doors[0]); break;
                    case 4:
                        Require(Selected(doors[0]), "Mira selecciona PuertaZona2"); Check(!doors[0].IsOpen, "PuertaZona2 no abre automáticamente con llave"); Press(); break;
                    case 5:
                        Release(); Require(doors[0].IsOpen && keys.KeyCount == 0, "Primera llave abre Zona2 y se consume");
                        Check(BarriersOpen(doors[0]) && doors.Skip(1).All(d => !d.IsOpen), "Retira sólo barreras de Zona2");
                        Check(!doors[1].Interact(player), "Zona3 requiere una segunda llave"); SpawnKey(); break;
                    case 6: Aim(pickup.GetComponent<Collider>().bounds.center); Press(); break;
                    case 7:
                        Release(); Require(keys.KeyCount == 1, "E recoge segunda llave"); PositionAtDoor(doors[1], true); break;
                    case 8: Require(Selected(doors[1]), "PuertaZona3 se interactúa desde el lado opuesto"); Press(); break;
                    case 9:
                        Release(); Require(doors[1].IsOpen && keys.KeyCount == 0, "Segunda llave abre Zona3 y se consume");
                        Check(!doors[2].Interact(player) && !doors[3].Interact(player) && !doors[4].Interact(player), "Sin otra llave no abre Zona4 ni Zona5");
                        SpawnKey(); break;
                    case 10: Aim(pickup.GetComponent<Collider>().bounds.center); Press(); break;
                    case 11:
                        Release(); Require(keys.KeyCount == 1, "Recoge llave para el par Zona4");
                        Check(!doors[4].Interact(player) && keys.KeyCount == 1, "Zona5 no saltea el par Zona4"); PositionAtDoor(doors[3], true); break;
                    case 12: Require(Selected(doors[3]), "Selecciona Zona4.1 desde la barrera enlazada"); Press(); break;
                    case 13:
                        Release(); Require(doors[2].IsOpen && doors[3].IsOpen && keys.KeyCount == 0, "Abrir Zona4.1 abre ambas Zona4 con una sola llave");
                        Check(BarriersOpen(doors[2]) && BarriersOpen(doors[3]), "Las dos puertas permanecen visibles y liberan sus barreras");
                        Check(!doors[2].Interact(player) && !doors[3].Interact(player), "No repite aperturas ni consumo en el grupo abierto");
                        Capture("01-zona4-shared-unlock.png"); SpawnKey(); break;
                    case 14: Aim(pickup.GetComponent<Collider>().bounds.center); Press(); break;
                    case 15:
                        Release(); Require(keys.KeyCount == 1 && doors[4].Interact(player) && keys.KeyCount == 0, "Cuarta llave abre Zona5 después de ambas Zona4");
                        StartSecondScenario(); break;
                    case 16:
                        Check(Read<TMPro.TMP_Text>(hud, "keyText").text == "LLAVES  3", "HUD muestra varias llaves acumuladas");
                        Check(!doors[1].Interact(player) && !doors[2].Interact(player) && keys.KeyCount == 3, "Tener tres llaves no permite saltear puertas");
                        Require(doors[0].Interact(player) && keys.KeyCount == 2, "Primera apertura usa una de las llaves acumuladas");
                        Require(doors[1].Interact(player) && keys.KeyCount == 1, "Segunda apertura usa otra llave acumulada");
                        bool reentered = false; Action duringConsumption = () => reentered = doors[3].Interact(player);
                        keys.Changed += duringConsumption;
                        bool opened = doors[2].Interact(player); keys.Changed -= duringConsumption;
                        Require(opened && doors[2].IsOpen && doors[3].IsOpen && keys.KeyCount == 0, "Abrir Zona4 primero también abre las dos con una llave");
                        Check(!reentered, "Consumo del grupo está protegido contra interacción repetida durante eventos");
                        Check(!doors[4].Interact(player), "Grupo abierto no regala una apertura adicional");
                        Check(keys.TryAddKey() && keys.TryAddKey() && keys.KeyCount == 2, "Puede acumular nuevas llaves después de abrir puertas");
                        var other = new GameObject("OtherPlayer"); other.AddComponent<PlayerHealth>(); PlayerKeyInventory otherKeys = PlayerKeyInventory.ForPlayer(other, true);
                        Check(otherKeys.KeyCount == 0 && !doors[4].Interact(other) && keys.KeyCount == 2, "Llaves de un jugador no se comparten globalmente");
                        Check(otherKeys.TryAddKey() && doors[4].Interact(other) && otherKeys.KeyCount == 0 && keys.KeyCount == 2, "Cada jugador consume únicamente su propia llave");
                        StartFreshDoors();
                        Time.timeScale = 0; Check(!doors[0].Interact(player) && !keys.TryAddKey() && keys.KeyCount == 2, "Pausa bloquea apertura y recogida sin gastar llaves"); Time.timeScale = 1;
                        player.GetComponent<PlayerHealth>().TakeDamage(10000);
                        Check(!doors[0].Interact(player) && !keys.TryConsumeKey() && keys.KeyCount == 2, "Jugador muerto no usa ni pierde llaves");
                        Check(!doors[0].IsOpen && doors.All(d => !d.IsOpen), "Instancias nuevas de puertas comienzan con la secuencia cerrada");
                        Finish(); break;
                }
            }
            catch (Exception e) { Check(false, e.ToString()); Finish(); }
        }
        static void CreateScenario()
        {
            gates = GameObject.Find("ConfiguredLevel2Doors"); SelectDoors();
            var light = new GameObject("Sun").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.4f; light.transform.rotation = Quaternion.Euler(45, -25, 0); RenderSettings.ambientLight = Color.gray;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.position = new Vector3(5000, 99.5f, 4980); floor.transform.localScale = new Vector3(30, 1, 30);
            player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(VendorTurretSetup.PlayerPath), new Vector3(5000, 100.05f, 4980), Quaternion.identity);
            player.GetComponent<UPPCharacterMovementComponent>().UseDefaultControllerInput = false; player.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
            interaction = player.GetComponent<PlayerInteractionController>(); VendorTurretSetup.Set(interaction, "requireLockedCursor", false);
            VendorTurretSetup.Set(player.GetComponent<PlayerWeaponController>(), "requireLockedCursor", false);
            keys = PlayerKeyInventory.ForPlayer(player, true); hud = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SyntyGameplayHudSetup.PrefabPath)).GetComponent<SyntyGameplayHUD>();
        }
        static void SelectDoors()
        {
            doors = DoorKeyProgressionSetup.Names.Select(n => gates.GetComponentsInChildren<PuertaConLlave>(true).Single(d => d.name == n)).ToArray();
            foreach (var door in doors) { var data = new SerializedObject(door); data.FindProperty("openingDuration").floatValue = .1f; data.ApplyModifiedPropertiesWithoutUndo(); }
        }
        static void SpawnKey()
        {
            Move(new Vector3(5000, 100.05f, 4980)); pickup = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Bosskey.prefab"), new Vector3(5000, 100.6f, 4982), Quaternion.identity).GetComponent<KeyPickup>();
            Aim(pickup.GetComponent<Collider>().bounds.center);
        }
        static void PositionAtDoor(PuertaConLlave door, bool reverse = false)
        {
            Bounds b = door.GetComponent<Collider>().bounds; Vector3 normal = b.size.x < b.size.z ? Vector3.right : Vector3.forward; if (reverse) normal = -normal;
            float extent = normal.x != 0 ? b.extents.x : b.extents.z;
            Vector3 point = b.center - normal * (extent + 1.3f); point.y = b.min.y + .05f; Move(point);
            Vector3 eye = point + Vector3.up * 1.4f - normal;
            Vector3 aim = new Vector3(b.center.x, b.min.y + Mathf.Min(1.3f, b.size.y * .5f), b.center.z);
            camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(aim - eye)); Physics.SyncTransforms();
        }
        static void Move(Vector3 position) { player.transform.position = position; player.GetComponent<Rigidbody>().position = position; Physics.SyncTransforms(); }
        static void Aim(Vector3 point) { Vector3 position = player.transform.position + new Vector3(.5f, 1.4f, -1.5f); camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(point - position)); Physics.SyncTransforms(); }
        static bool Selected(PuertaConLlave door) { interaction.RefreshTarget(); return interaction.CurrentInteractable == door || interaction.CurrentInteractable is DoorBarrierInteraction proxy && proxy.Door == door; }
        static bool BarriersOpen(PuertaConLlave door) => door.IsOpen && !door.IsAnimating && door.gameObject.activeSelf && Read<GameObject[]>(door, "blockingObjects").All(b => b == null || b.activeSelf);
        static void StartFreshDoors()
        {
            GameObject old = gates; gates = Object.Instantiate(old); gates.name = "FreshLevel2Doors"; gates.transform.position += Vector3.forward * 300;
            old.SetActive(false); SelectDoors();
            foreach (PuertaConLlave door in doors) { door.gameObject.SetActive(true); foreach (GameObject barrier in Read<GameObject[]>(door, "blockingObjects")) if (barrier != null) barrier.SetActive(true); }
            Physics.SyncTransforms();
        }
        static void StartSecondScenario()
        {
            StartFreshDoors();
            for (int i = 0; i < 3; i++)
            {
                KeyPickup key = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Bosskey.prefab")).GetComponent<KeyPickup>();
                Require(key.Interact(player), "Recogida independiente agrega una llave a la reserva");
            }
        }
        static T Read<T>(object source, string name) => (T)source.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(source);
        static void Press() { keyboard.MakeCurrent(); InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.E)); }
        static void Release() => InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        static void Capture(string file)
        {
            var rt = (RenderTexture)typeof(CharacterCustomizationValidation).GetMethod("GameViewFramebuffer", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null); if (rt == null) return;
            var photo = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); RenderTexture prior = RenderTexture.active;
            try
            {
                RenderTexture.active = rt; photo.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                if (SystemInfo.graphicsUVStartsAtTop) { var pixels = photo.GetPixels32(); for (int y = 0; y < rt.height / 2; y++) for (int x = 0; x < rt.width; x++) { int a = y * rt.width + x, b = (rt.height - 1 - y) * rt.width + x; Color32 c = pixels[a]; pixels[a] = pixels[b]; pixels[b] = c; } photo.SetPixels32(pixels); }
                photo.Apply(); File.WriteAllBytes(DoorKeyProgressionSetup.Folder + "/" + file, photo.EncodeToPNG());
            }
            finally { RenderTexture.active = prior; Object.DestroyImmediate(photo); }
        }
        static void OnLog(string text, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(text + "\n" + stack); }
        static void Check(bool ok, string text) { checks.Add((ok ? "PASS " : "FAIL ") + text); if (!ok) failures++; Debug.Log("DOOR_PROGRESSION_" + (ok ? "PASS " : "FAIL ") + text); }
        static void Require(bool ok, string text) { Check(ok, text); if (!ok) throw new InvalidOperationException(text); }
        static void Finish()
        {
            Application.logMessageReceived -= OnLog; if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (previousSettings != null) InputSystem.settings = previousSettings; if (settings != null) Object.DestroyImmediate(settings);
            File.WriteAllText(DoorKeyProgressionSetup.Folder + "/result.json", JsonUtility.ToJson(new Result { passed = checks.Count - failures, failed = failures, checks = checks.ToArray(), errors = errors.ToArray() }, true));
            SessionState.SetInt(Key + ".Exit", failures == 0 && errors.Count == 0 ? 0 : 1); EditorApplication.ExitPlaymode();
        }
    }
}
