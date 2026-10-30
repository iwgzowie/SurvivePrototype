using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Survive.Character;
using Survive.Combat;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Survive.Editor
{
    /// <summary>Prueba el catálogo completo y el flujo real de menú usando un perfil aislado.</summary>
    [InitializeOnLoad]
    public static class CharacterCustomizationValidation
    {
        const string SessionKey = "Survive.CharacterValidation.Active";
        const string MainScene = "Assets/Scenes/SceneAlejandro.unity";
        const string PlayerPrefab = "Assets/Prefabs/Player/UPPPlayer.prefab";
        static readonly string EvidenceFolder = Path.GetFullPath("Logs/CharacterCustomization");
        static readonly string TestProfilePath = Path.Combine(EvidenceFolder, "runtime-profile.json");
        static int passed, failed, stage;
        static int stageFrame;
        static double stageStarted;
        static EditorWindow validationGameView;
        static PropertyInfo validationSizeSelection;
        static int validationSizeIndex = -1;
        static string lastUiReadiness;
        static double nextUiReadinessLog;
        static readonly Stack<IEnumerator> uiSteps = new Stack<IEnumerator>();
        static int uiStepFrame;
        static readonly List<string> failures = new List<string>();
        static readonly List<string> runtimeErrors = new List<string>();
        static string expectedProfileJson;
        static readonly Dictionary<Canvas, bool> menuCanvasStates = new Dictionary<Canvas, bool>();
        static readonly Dictionary<Selectable, bool> menuControlStates = new Dictionary<Selectable, bool>();
        static readonly Dictionary<Camera, CameraState> menuCameraStates = new Dictionary<Camera, CameraState>();
        static readonly HashSet<int> menuStudioMaterials = new HashSet<int>();

        sealed class CameraState
        {
            public bool enabled;
            public int mask;
            public RenderTexture target;
        }

        sealed class StudioGeometry
        {
            public string name;
            public Vector3[] vertices;
            public int[] triangles;
        }

        sealed class StudioPresentationState
        {
            public Vector3 cameraPosition;
            public Quaternion cameraRotation;
            public Matrix4x4 projection;
            public float fieldOfView;
            public Vector3 rigScale;
            public readonly Dictionary<Transform, Vector3> anchors = new Dictionary<Transform, Vector3>();
        }

        [Serializable]
        sealed class Result
        {
            public string scope;
            public int passed;
            public int failed;
            public string[] failures;
            public string[] runtimeErrors;
        }

        static CharacterCustomizationValidation()
        {
            EditorApplication.playModeStateChanged += OnPlayStateChanged;
            EditorApplication.update += Tick;
        }

        [MenuItem("Survive/Characters/Validar catálogo, perfil y rig")]
        public static void Run()
        {
            ResetCounters();
            Directory.CreateDirectory(EvidenceFolder);
            SidekickCharacterCatalog catalog = SidekickCharacterCatalog.Load();
            Require(catalog != null, "Catálogo presente en Resources");
            Require(catalog.previewRig != null, "Rig de preview referenciado");
            Require(catalog.parts != null && catalog.parts.Length > 0, "Catálogo contiene módulos");
            string[] importedModels = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Synty/SidekickCharacters/Resources/Meshes" })
                .Select(AssetDatabase.GUIDToAssetPath).Where(path => path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)).ToArray();
            Check(catalog.parts.Length == importedModels.Length, $"Todos los FBX modulares incluidos: {catalog.parts.Length}/{importedModels.Length}");
            Check(catalog.parts.Select(part => part.id).Distinct().Count() == catalog.parts.Length, "IDs de módulos únicos");
            Check(catalog.recipes != null && catalog.recipes.Length == 37, "Las 37 recetas Sidekick incluidas");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            Require(prefab != null, "Prefab de jugador presente");
            Check(prefab.GetComponent<SidekickCharacterAppearance>() != null, "Apariencia integrada al prefab del jugador");
            Check(prefab.GetComponent<PlayerWeaponController>() != null, "Controlador de arma conservado");
            Animator originalAnimator = prefab.GetComponent<Animator>();
            Require(originalAnimator != null && originalAnimator.avatar != null && originalAnimator.avatar.isHuman, "Avatar humanoide conservado");
            Require(originalAnimator.runtimeAnimatorController != null, "Animator TPS conservado");
            var boneNames = new HashSet<string>(prefab.GetComponentsInChildren<Transform>(true).Select(item => item.name));
            var missingModels = new List<string>();
            var missingMaterials = new List<string>();
            var missingBones = new List<string>();
            var invalidBindings = new List<string>();
            foreach (var part in catalog.parts)
            {
                if (part.prefab == null) { missingModels.Add(part.id); continue; }
                SkinnedMeshRenderer source = part.prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (source == null || source.sharedMesh == null) { missingModels.Add(part.id); continue; }
                if (part.material == null) missingMaterials.Add(part.id);
                if (source.bones.Any(bone => bone == null) || source.bones.Length != source.sharedMesh.bindposes.Length)
                    invalidBindings.Add(part.id);
                foreach (Transform bone in source.bones)
                    if (bone == null || !boneNames.Contains(bone.name))
                        missingBones.Add(part.id + ":" + (bone == null ? "null" : bone.name));
            }
            Check(missingModels.Count == 0, "Todos los módulos tienen malla skinned", missingModels);
            Check(missingMaterials.Count == 0, "Todos los módulos tienen material", missingMaterials);
            Check(invalidBindings.Count == 0, "Todos los módulos tienen bindposes y huesos válidos", invalidBindings);
            foreach (var part in catalog.parts.Where(part => part.slot == "Head" || part.slot == "Torso").Take(2))
            {
                Mesh mesh = part.prefab.GetComponentInChildren<SkinnedMeshRenderer>(true).sharedMesh;
                for (int shapeIndex = 0; shapeIndex < mesh.blendShapeCount; shapeIndex++)
                {
                    string shapeName = mesh.GetBlendShapeName(shapeIndex);
                    if (!shapeName.EndsWith("masculineFeminine", StringComparison.OrdinalIgnoreCase)) continue;
                    int frames = mesh.GetBlendShapeFrameCount(shapeIndex);
                    string weights = string.Join(",", Enumerable.Range(0, frames).Select(frame => mesh.GetBlendShapeFrameWeight(shapeIndex, frame).ToString("F1")));
                    Debug.Log($"CHARACTER_GENDER_SHAPE part={part.id} shape={shapeName} frames={frames} weights={weights} male=0 female=100");
                }
            }
            // Los módulos pueden incluir huesos auxiliares de pelo/accesorios que
            // el compositor agrega sin reemplazar el esqueleto de locomoción.
            Debug.Log($"CHARACTER_BONE_AUDIT modules={catalog.parts.Length} auxiliaryReferences={missingBones.Count}");

            CharacterProfile profile = catalog.CreateDefault();
            profile.displayName = "Prueba Sidekick";
            Check(catalog.Validate(profile, out string error), "Perfil por defecto válido: " + error);
            CharacterProfile clone = profile.Clone();
            clone.displayName = "Clon";
            if (clone.parts.Count > 0) clone.parts[0].partId = "invalid-copy";
            Check(profile.displayName == "Prueba Sidekick" && profile.parts.All(part => part.partId != "invalid-copy"), "Clonar no modifica el borrador original");
            CharacterProfile invalid = profile.Clone();
            invalid.parts[0].partId = "missing-asset-id";
            Check(!catalog.Validate(invalid, out _), "ID inexistente rechazado");
            invalid = profile.Clone();
            invalid.parts.Clear();
            Check(!catalog.Validate(invalid, out _), "Perfil sin partes obligatorias rechazado");
            ValidatePersistence(profile);
            ValidateComposition(catalog, prefab, profile);
            WriteResult("edit-mode", "edit-result.json");
            Debug.Log($"SURVIVE_CHARACTER_VALIDATION={(failed == 0 ? "PASS" : "FAIL")} passed={passed} failed={failed} modules={catalog.parts.Length}");
            if (failed != 0) throw new InvalidOperationException("Falló validación de customización. Ver Logs/CharacterCustomization/edit-result.json.");
        }

        static void ValidatePersistence(CharacterProfile profile)
        {
            string path = Path.Combine(EvidenceFolder, "store-profile.json");
            DeleteTestProfile(path);
            Check(!CharacterProfileStore.TryLoad(path, out _), "Sin archivo no hay perfil");
            Check(CharacterProfileStore.TrySave(profile, path, out string saveError), "Guardar perfil: " + saveError);
            Check(CharacterProfileStore.TryLoad(path, out CharacterProfile loaded), "Leer perfil desde disco");
            Check(loaded != null && JsonUtility.ToJson(loaded) == JsonUtility.ToJson(profile), "Round-trip conserva nombre, género, cuerpo y selección");
            CharacterProfile changed = profile.Clone();
            changed.gender = CharacterGender.Feminine;
            changed.bodySize = 70;
            changed.muscle = 85;
            Check(CharacterProfileStore.TrySave(changed, path, out saveError), "Segundo guardado atómico: " + saveError);
            Check(CharacterProfileStore.TryLoad(path, out loaded) && loaded.gender == CharacterGender.Feminine && Mathf.Approximately(loaded.bodySize, 70), "Segundo perfil vuelve a leerse");
            File.WriteAllText(path, "{broken-json");
            Check(CharacterProfileStore.TryLoad(path, out loaded) && loaded.displayName == profile.displayName, "Perfil corrupto recupera el backup válido");
            DeleteTestProfile(path);
        }

        static void ValidateComposition(SidekickCharacterCatalog catalog, GameObject prefab, CharacterProfile baseProfile)
        {
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            instance.SetActive(false);
            try
            {
                Animator animator = instance.GetComponent<Animator>();
                var avatar = animator.avatar;
                var controller = animator.runtimeAnimatorController;
                Transform[] originalTransforms = instance.GetComponentsInChildren<Transform>(true);
                Transform hand = originalTransforms.FirstOrDefault(item => item.name == "hand_r");
                Transform socket = originalTransforms.FirstOrDefault(item => item.name == "WeaponSocket");
                SidekickCharacterAppearance appearance = instance.GetComponent<SidekickCharacterAppearance>();
                appearance.Configure(catalog, false);
                // Un lote toma un módulo distinto de cada slot. Así se prueba
                // cada FBX a través del compositor real con muchas menos
                // recomposiciones que una prueba completa por módulo.
                var bySlot = catalog.parts.GroupBy(part => part.slot).Select(group => group.ToArray()).ToArray();
                int batches = bySlot.Max(group => group.Length);
                int testedParts = 0;
                for (int index = 0; index < batches; index++)
                {
                    CharacterProfile batch = baseProfile.Clone();
                    foreach (var group in bySlot)
                    {
                        if (index >= group.Length) continue;
                        var part = group[index];
                        batch.parts.RemoveAll(selection => selection.slot == part.slot);
                        batch.parts.Add(new CharacterPartSelection { slot = part.slot, partId = part.id });
                        testedParts++;
                    }
                    bool applied = appearance.Apply(batch);
                    Check(applied, "Lote modular " + index + ": " + appearance.LastError);
                    if (applied)
                    {
                        var rendered = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(renderer => renderer.enabled).ToArray();
                        Check(rendered.All(renderer => renderer.sharedMesh != null && renderer.bones.All(bone => bone != null)), "Lote " + index + " con huesos resueltos");
                    }
                }
                Check(testedParts == catalog.parts.Length, "Todos los módulos atravesaron el compositor real");
                foreach (CharacterGender gender in Enum.GetValues(typeof(CharacterGender)))
                {
                    CharacterProfile profile = baseProfile.Clone();
                    profile.gender = gender;
                    Check(appearance.Apply(profile), "Aplicar género " + gender);
                    ValidateShapeWeights(instance, profile);
                    foreach (var recipe in catalog.recipes)
                    {
                        profile = baseProfile.Clone();
                        profile.gender = gender;
                        catalog.ApplyFaceRecipe(profile, recipe.id);
                        catalog.ApplyOutfitRecipe(profile, recipe.id);
                        Check(catalog.Validate(profile, out string error) && appearance.Apply(profile), "Componer receta " + recipe.id + "/" + gender + ": " + error);
                    }
                    foreach (float size in new[] { -100f, 0f, 100f })
                    {
                        profile = baseProfile.Clone();
                        profile.gender = gender;
                        profile.bodySize = size;
                        profile.muscle = size == 0 ? 100 : 0;
                        Check(appearance.Apply(profile), "Componer cuerpo " + gender + " size=" + size);
                        ValidateShapeWeights(instance, profile);
                    }
                }
                Check(instance.GetComponent<Animator>() == animator && animator.avatar == avatar && animator.runtimeAnimatorController == controller, "Composición conserva Animator, Avatar y controlador");
                Check(hand != null && hand.IsChildOf(instance.transform), "Mano derecha original conservada");
                Check(socket != null && socket.IsChildOf(instance.transform), "Socket original de arma conservado");
                var generated = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(renderer => !originalTransforms.Contains(renderer.transform)).ToArray();
                Check(generated.Length > 0 && generated.All(renderer => renderer.sharedMesh != null && renderer.bones.All(bone => bone != null)), "Renderers generados tienen malla y huesos resueltos");
                Check(originalTransforms.All(item => item != null && item.IsChildOf(instance.transform)), "Todos los transforms originales sobreviven a las composiciones");
                ValidateBakedGeometry(instance, catalog, baseProfile);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        static void ValidateShapeWeights(GameObject instance, CharacterProfile profile)
        {
            bool coherent = true;
            int genderShapes = 0;
            foreach (SkinnedMeshRenderer renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!renderer.enabled || renderer.sharedMesh == null || !IsActiveBelow(renderer.transform, instance.transform)) continue;
                Mesh mesh = renderer.sharedMesh;
                for (int index = 0; index < mesh.blendShapeCount; index++)
                {
                    string shape = mesh.GetBlendShapeName(index);
                    float expected;
                    if (shape.EndsWith("masculineFeminine", StringComparison.OrdinalIgnoreCase))
                    { expected = profile.gender == CharacterGender.Feminine ? 100 : 0; genderShapes++; }
                    else if (shape.EndsWith("defaultSkinny", StringComparison.OrdinalIgnoreCase)) expected = Mathf.Max(0, -profile.bodySize);
                    else if (shape.EndsWith("defaultHeavy", StringComparison.OrdinalIgnoreCase)) expected = Mathf.Max(0, profile.bodySize);
                    else if (shape.EndsWith("defaultBuff", StringComparison.OrdinalIgnoreCase)) expected = profile.muscle;
                    else continue;
                    coherent &= Mathf.Approximately(renderer.GetBlendShapeWeight(index), expected);
                }
            }
            Check(coherent && genderShapes > 0, $"Pesos coherentes en cabeza, cuerpo y ropa: {profile.gender}/{profile.bodySize}/{profile.muscle}");
        }

        static void ValidateBakedGeometry(GameObject instance, SidekickCharacterCatalog catalog, CharacterProfile original)
        {
            var appearance = instance.GetComponent<SidekickCharacterAppearance>();
            CharacterProfile masculine = original.Clone();
            masculine.gender = CharacterGender.Masculine;
            appearance.Apply(masculine);
            var torso = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(renderer =>
                renderer.enabled && IsActiveBelow(renderer.transform, instance.transform) && renderer.name.StartsWith("Torso/", StringComparison.Ordinal));
            Require(torso != null, "Torso modular presente para comprobar geometría");
            var maleMesh = new Mesh();
            var femaleMesh = new Mesh();
            try
            {
                torso.BakeMesh(maleMesh);
                maleMesh.RecalculateBounds();
                CharacterProfile feminine = masculine.Clone();
                feminine.gender = CharacterGender.Feminine;
                appearance.Apply(feminine);
                torso = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(renderer =>
                    renderer.enabled && IsActiveBelow(renderer.transform, instance.transform) && renderer.name.StartsWith("Torso/", StringComparison.Ordinal));
                torso.BakeMesh(femaleMesh);
                femaleMesh.RecalculateBounds();
                Vector3[] male = maleMesh.vertices;
                Vector3[] female = femaleMesh.vertices;
                Check(male.Length == female.Length && male.Zip(female, (a, b) => (a - b).sqrMagnitude).Any(distance => distance > .0000001f), "Género cambia geometría real del torso");
                Check(male.All(IsFinite) && female.All(IsFinite), "Geometría skinned contiene posiciones finitas");
                Bounds neutralBounds = femaleMesh.bounds;
                Debug.Log($"CHARACTER_TORSO_GEOMETRY maleBounds={maleMesh.bounds} femaleBounds={neutralBounds}");
                foreach (var body in new[] { new Vector2(-100, 0), new Vector2(100, 0), new Vector2(0, 100) })
                {
                    CharacterProfile changed = feminine.Clone();
                    changed.bodySize = body.x;
                    changed.muscle = body.y;
                    appearance.Apply(changed);
                    torso = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(renderer =>
                        renderer.enabled && IsActiveBelow(renderer.transform, instance.transform) && renderer.name.StartsWith("Torso/", StringComparison.Ordinal));
                    torso.BakeMesh(maleMesh);
                    maleMesh.RecalculateBounds();
                    Vector3[] changedVertices = maleMesh.vertices;
                    float maxDelta = changedVertices.Length == female.Length
                        ? changedVertices.Zip(female, (a, b) => (a - b).magnitude).DefaultIfEmpty(0).Max() : 0;
                    Debug.Log($"CHARACTER_BODY_GEOMETRY size={body.x} muscle={body.y} maxVertexDelta={maxDelta:F6} bounds={maleMesh.bounds} neutralBounds={neutralBounds}");
                    // La ropa puede mover vértices sin desplazar los vértices que
                    // definen su AABB. La deformación real se comprueba sobre la
                    // geometría horneada; los bounds se registran como diagnóstico.
                    Check(maxDelta > .00001f, $"Cuerpo deforma vértices reales del torso: size={body.x} muscle={body.y}");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(maleMesh);
                UnityEngine.Object.DestroyImmediate(femaleMesh);
            }
        }

        static bool IsFinite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        static bool IsActiveBelow(Transform item, Transform root)
        {
            while (item != null && item != root)
            {
                if (!item.gameObject.activeSelf) return false;
                item = item.parent;
            }
            return item == root;
        }

        [MenuItem("Survive/Characters/Validar flujo real en Play Mode")]
        public static void RunPlayMode()
        {
            StartPlayValidation(false);
        }

        [MenuItem("Survive/Characters/Validar sólo UI del creador")]
        public static void RunCreatorUI()
        {
            StartPlayValidation(true);
        }

        static void StartPlayValidation(bool creatorOnly)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("La prueba debe iniciarse fuera de Play Mode.");
            Directory.CreateDirectory(EvidenceFolder);
            DeleteTestProfile(TestProfilePath);
            SessionState.SetBool(SessionKey + ".CrossSession", false);
            SessionState.SetBool(SessionKey + ".CreatorOnly", creatorOnly);
            SessionState.SetBool(SessionKey, true);
            EditorSceneManager.OpenScene(MainScene);
            PrepareBatchGameView();
            EditorApplication.EnterPlaymode();
        }

        public static void RunAll()
        {
            Run();
            RunPlayMode();
        }

        /// <summary>Invocar en otro proceso Unity después de RunAll para probar lectura entre ejecuciones.</summary>
        public static void RunCrossSession()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Iniciar fuera de Play Mode.");
            if (!File.Exists(TestProfilePath)) throw new FileNotFoundException("Primero ejecutar RunAll para crear el perfil aislado.", TestProfilePath);
            SessionState.SetBool(SessionKey + ".CrossSession", true);
            SessionState.SetBool(SessionKey + ".CreatorOnly", false);
            SessionState.SetBool(SessionKey, true);
            EditorSceneManager.OpenScene(MainScene);
            PrepareBatchGameView();
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                ResetCounters();
                runtimeErrors.Clear();
                SetProfileOverride(TestProfilePath);
                Application.logMessageReceived += CaptureLog;
                PrepareBatchGameView();
                SetStage(0);
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(SessionKey, false);
                if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(SessionKey + ".ExitCode", 1));
            }
        }

        static void Tick()
        {
            if (!SessionState.GetBool(SessionKey, false) || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
            if (EditorApplication.timeSinceStartup - stageStarted > 90)
            {
                Check(false, "Timeout en etapa de runtime " + stage + ": " + lastUiReadiness);
                FinishRuntime();
                return;
            }
            if (EditorApplication.timeSinceStartup - stageStarted < 1.5) return;
            // El tiempo de Editor puede avanzar durante carga/importación sin que se
            // haya renderizado un frame de jugador. Raycast exige Graphic.depth válido.
            if (Time.frameCount - stageFrame < 2)
            {
                RequestPlayerFrame();
                return;
            }
            try
            {
                if (!ValidationFramebufferReady()) return;
                if ((stage == 0 || stage == 2 || stage == 6)
                    && SceneManager.GetActiveScene().name == "SceneAlejandro" && !MainMenuGraphicsReady()) return;
                switch (stage)
                {
                    case 0:
                        Require(SceneManager.GetActiveScene().name == "SceneAlejandro", "Arranque en menú principal real");
                        if (SessionState.GetBool(SessionKey + ".CrossSession", false))
                        {
                            Require(CharacterProfileStore.TryLoad(TestProfilePath, out CharacterProfile previous), "Perfil leído desde una ejecución Unity anterior");
                            expectedProfileJson = JsonUtility.ToJson(previous);
                            InvokePlayButton();
                            SetStage(7);
                            break;
                        }
                        Check(!CharacterProfileStore.TryLoad(TestProfilePath, out _), "Primera ejecución sin perfil");
                        SnapshotMenuState();
                        InvokePlayButton();
                        SetStage(1);
                        break;
                    case 1:
                        if (uiSteps.Count == 0)
                        {
                            Require(FindCreator() != null, "Play sin perfil abre el creador");
                            Check(SceneManager.GetActiveScene().name == "SceneAlejandro", "Crear personaje mantiene la escena del menú");
                            Check(Cursor.visible && Cursor.lockState == CursorLockMode.None, "Cursor disponible durante creación");
                            ValidateCreatorPresentation(FindCreator());
                            CaptureScreen("01-creator.png");
                            uiSteps.Push(ValidatePopupAndCancel());
                            uiStepFrame = Time.frameCount - 1;
                        }
                        if (!AdvanceUiSteps()) return;
                        SetStage(2);
                        break;
                    case 2:
                        Check(FindCreator() == null, "Cancelar cierra creador");
                        Check(!File.Exists(TestProfilePath), "Cancelar no crea perfil");
                        ValidateMenuRestored();
                        InvokePlayButton();
                        SetStage(3);
                        break;
                    case 3:
                        if (uiSteps.Count == 0)
                        {
                            Require(FindCreator() != null, "Play vuelve a abrir el borrador");
                            Check(UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
                                .Count(camera => camera.name == "CharacterPreviewCamera") == 1, "Reabrir crea una sola cámara de estudio");
                            uiSteps.Push(ConfigureDraftThroughControls());
                            uiStepFrame = Time.frameCount - 1;
                        }
                        if (!AdvanceUiSteps()) return;
                        SetStage(10);
                        break;
                    case 10:
                        if (uiSteps.Count == 0)
                        {
                            uiSteps.Push(ValidateStableRecipeFraming(FindCreator()));
                            uiStepFrame = Time.frameCount - 1;
                        }
                        if (!AdvanceUiSteps()) return;
                        SetStage(11);
                        break;
                    case 11:
                        if (uiSteps.Count == 0)
                        {
                            uiSteps.Push(ValidateStableAllModuleFraming(FindCreator()));
                            uiStepFrame = Time.frameCount - 1;
                        }
                        if (!AdvanceUiSteps()) return;
                        ValidateCreatorPresentation(FindCreator());
                        CaptureScreen("02-feminine-character.png");
                        if (SessionState.GetBool(SessionKey + ".CreatorOnly", false))
                        {
                            ClickCreatorButton(FindCreator(), "Volver");
                            SetStage(9);
                            break;
                        }
                        SetStage(4);
                        break;
                    case 4:
                        Button confirm = FindCreator().GetComponentsInChildren<Button>(true).FirstOrDefault(button =>
                            button.GetComponentInChildren<TMP_Text>(true)?.text.IndexOf("Crear", StringComparison.OrdinalIgnoreCase) >= 0);
                        Require(confirm != null, "Botón Crear y jugar disponible");
                        ClickThroughEventSystem(confirm, "Crear y jugar");
                        SetStage(5);
                        break;
                    case 5:
                        if (!GameplayReady()) return;
                        Require(SceneManager.GetActiveScene().name == "Level 1", "Confirmar carga Level 1");
                        ValidateStudioCleaned();
                        Require(CharacterProfileStore.TryLoad(TestProfilePath, out CharacterProfile saved), "Confirmar persiste perfil");
                        Check(saved.displayName == "Validadora Sidekick", "Nombre escrito en UI persistido");
                        Check(saved.gender == CharacterGender.Feminine, "Género femenino seleccionado en UI persistido");
                        expectedProfileJson = JsonUtility.ToJson(saved);
                        ValidateRuntimePlayer();
                        CaptureScreen("03-gameplay-profile.png");
                        SceneManager.LoadScene("SceneAlejandro");
                        SetStage(6);
                        break;
                    case 6:
                        if (SceneManager.GetActiveScene().name != "SceneAlejandro") return;
                        Require(SceneManager.GetActiveScene().name == "SceneAlejandro", "Vuelta a menú");
                        InvokePlayButton();
                        SetStage(7);
                        break;
                    case 7:
                        if (!GameplayReady()) return;
                        Require(SceneManager.GetActiveScene().name == "Level 1", "Segundo Play entra directo a partida");
                        Check(FindCreator() == null, "Con perfil no reaparece creador");
                        Check(CharacterProfileStore.TryLoad(TestProfilePath, out CharacterProfile reloaded) && JsonUtility.ToJson(reloaded) == expectedProfileJson, "Perfil estable después de recargar escenas");
                        ValidateRuntimePlayer();
                        CaptureScreen("04-returning-player.png");
                        SetStage(8);
                        break;
                    case 8:
                        FinishRuntime();
                        break;
                    case 9:
                        Check(FindCreator() == null, "La validación enfocada cierra el creador después de personalizar");
                        Check(SceneManager.GetActiveScene().name == "SceneAlejandro", "La validación de UI permanece en el menú");
                        Check(!File.Exists(TestProfilePath), "Cancelar el borrador personalizado no persiste un perfil");
                        ValidateMenuRestored();
                        FinishRuntime();
                        break;
                }
            }
            catch (Exception exception)
            {
                Check(false, exception.ToString());
                FinishRuntime();
            }
        }

        // Cada paso deja renderizar un frame real. Esto detecta el orden de Canvas,
        // los bloqueadores y los layouts que un cambio directo de .value omitía.
        static bool AdvanceUiSteps()
        {
            if (Time.frameCount <= uiStepFrame) { RequestPlayerFrame(); return false; }
            uiStepFrame = Time.frameCount;
            while (uiSteps.Count > 0)
            {
                IEnumerator step = uiSteps.Peek();
                if (!step.MoveNext()) { uiSteps.Pop(); continue; }
                if (step.Current is IEnumerator nested) { uiSteps.Push(nested); continue; }
                RequestPlayerFrame();
                return false;
            }
            return true;
        }

        static IEnumerator ConfigureDraftThroughControls()
        {
            CharacterCreationMenu creator = FindCreator();
            StudioPresentationState stableStudio = SnapshotStudioPresentation();
            TMP_InputField name = creator.GetComponentInChildren<TMP_InputField>(true);
            Require(name != null, "Campo de nombre presente");
            name.text = "Validadora Sidekick";
            TMP_Dropdown gender = FindDropdown(creator, "Género");
            yield return OpenDropdown(gender, "Género");
            CaptureScreen("05-gender-popup.png");
            ClickOutsidePopup(creator, gender);
            yield return null; yield return null;
            ValidateNoDropdownOrphans("clic fuera del género");
            Check(creator.IsOpen, "Clic fuera sólo cierra el desplegable");

            yield return OpenDropdown(gender, "Género para Escape");
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            Require(selected != null && selected.transform.IsChildOf(FindDropdownPopup(gender).transform),
                "La opción del popup recibe navegación de teclado");
            ExecuteEvents.ExecuteHierarchy(selected, new BaseEventData(EventSystem.current), ExecuteEvents.cancelHandler);
            yield return null; yield return null;
            ValidateNoDropdownOrphans("Escape/cancel del desplegable");
            Check(creator.IsOpen, "Escape/cancel del popup conserva el creador");
            int feminine = gender.options.FindIndex(option => option.text.IndexOf("Femenino", StringComparison.OrdinalIgnoreCase) >= 0);
            yield return ChooseDropdownOption(gender, feminine, "Género femenino");
            Check(creator.Draft.gender == CharacterGender.Feminine, "Elegir Toggle femenino actualiza el borrador");
            ValidateStableStudio(stableStudio, "cambiar género");

            ClickCreatorTab(creator, "CUERPO");
            yield return null; yield return null;
            ValidateNoDropdownOrphans("cambio a CUERPO");
            Button muscular = creator.GetComponentsInChildren<Button>().FirstOrDefault(button => button.name == "Musculoso");
            Require(muscular != null, "Preset Musculoso presente");
            yield return ScrollIntoView(muscular.transform as RectTransform, FindOptionsScroll(creator), "preset Musculoso");
            ClickThroughEventSystem(muscular, "Preset Musculoso");
            yield return null; yield return null;
            Check(Mathf.Approximately(creator.Draft.muscle, 100), "Preset corporal responde al clic real");
            Button selectedPreset = creator.GetComponentsInChildren<Button>().First(button => button.name == "Musculoso");
            Color selectedPresetColor = selectedPreset.GetComponent<Image>().color;
            Check(creator.GetComponentsInChildren<Button>().Where(button => button.transform.parent.name == "BodyPresets")
                .All(button => (button.GetComponent<Image>().color == selectedPresetColor) == (button == selectedPreset)),
                "El preset Musculoso elegido queda resaltado y los demás quedan sin seleccionar");
            ClickCreatorTab(creator, "CARA");
            yield return null; yield return null;
            ClickCreatorTab(creator, "CUERPO");
            yield return null; yield return null;
            selectedPreset = creator.GetComponentsInChildren<Button>().First(button => button.name == "Musculoso");
            Check(selectedPreset.GetComponent<Image>().color == selectedPresetColor && Mathf.Approximately(creator.Draft.muscle, 100),
                "Volver a CUERPO conserva el preset elegido y su resaltado");
            ValidateStableStudio(stableStudio, "aplicar preset corporal");
            foreach (Slider slider in creator.GetComponentsInChildren<Slider>())
            {
                yield return ScrollIntoView(slider.transform as RectTransform, FindOptionsScroll(creator), slider.transform.parent.name);
                DragSlider(slider, .65f);
                yield return null;
            }
            Check(creator.GetComponentsInChildren<Slider>(true).Length >= 2, "Controles de tamaño y músculo presentes");
            Check(Mathf.Approximately(creator.Draft.bodySize, 30) && Mathf.Approximately(creator.Draft.muscle, 65), "Sliders actualizan tamaño y musculatura del borrador");
            Check(creator.GetComponentsInChildren<Button>().Where(button => button.transform.parent.name == "BodyPresets")
                .All(button => button.GetComponent<Image>().color != selectedPresetColor),
                "Una complexión manual quita el resaltado de presets que ya no coinciden");
            ValidateStableStudio(stableStudio, "editar proporciones con sliders");
            string oldOutfit = SelectedPartsKey(creator.Draft, false);
            ClickCreatorTab(creator, "ROPA");
            yield return null; yield return null;
            ValidateNoDropdownOrphans("cambio a ROPA");
            TMP_Dropdown outfitDropdown = FindDropdown(creator, "Conjuntos Sidekick");
            Require(outfitDropdown != null && outfitDropdown.options.Count > 1, "Selector de conjuntos disponible");
            yield return OpenDropdown(outfitDropdown, "Conjuntos Sidekick");
            CaptureScreen("06-outfit-popup.png");
            yield return ChooseOpenDropdownOption(outfitDropdown, 1, "Conjunto Sidekick");
            Check(SelectedPartsKey(creator.Draft, false) != oldOutfit, "Elegir conjunto cambia IDs de ropa desde UI");
            ValidateStableStudio(stableStudio, "aplicar conjunto");
            yield return ValidateRecipeRetained(creator, false, 1, stableStudio);
            yield return ValidateFilterAndModule(creator, false);
            ValidateStableStudio(stableStudio, "editar una pieza de ropa");
            string oldFace = SelectedPartsKey(creator.Draft, true);
            ClickCreatorTab(creator, "CARA");
            yield return null; yield return null;
            ValidateNoDropdownOrphans("cambio a CARA");
            TMP_Dropdown faceDropdown = FindDropdown(creator, "Caras de los personajes Sidekick");
            Require(faceDropdown != null && faceDropdown.options.Count > 1, "Selector de caras disponible");
            SidekickCharacterCatalog catalog = SidekickCharacterCatalog.Load();
            var faceRecipes = catalog.recipes.Where(recipe => recipe.parts != null && recipe.parts.Any(selection =>
                SidekickCharacterCatalog.IsFaceSlot(selection.slot))).ToArray();
            int faceIndex = Array.FindIndex(faceRecipes, recipe => recipe.id == "HumanSpecies_02");
            Require(faceIndex >= 0, "Receta facial humana de prueba disponible");
            yield return ChooseDropdownOption(faceDropdown, faceIndex + 1, "Cara humana Sidekick");
            Check(SelectedPartsKey(creator.Draft, true) != oldFace, "Elegir cara cambia IDs faciales desde UI");
            ValidateStableStudio(stableStudio, "aplicar cara");
            yield return ValidateRecipeRetained(creator, true, faceIndex + 1, stableStudio);
            yield return ValidateFilterAndModule(creator, true);
            ValidateStableStudio(stableStudio, "editar una pieza facial");
            ClickCreatorTab(creator, "CUERPO");
            yield return null; yield return null;
            ValidateNoDropdownOrphans("rebuild y cambio de pestaña finales");
            Camera studioCamera = FindStudioCamera();
            Check(studioCamera != null && studioCamera.enabled && studioCamera.targetTexture == null,
                "Estudio 3D se dibuja con cámara de pantalla completa");
            var preview = creator.GetComponentsInChildren<SidekickCharacterAppearance>(true).FirstOrDefault();
            if (preview == null) preview = UnityEngine.Object.FindObjectsByType<SidekickCharacterAppearance>(FindObjectsSortMode.None).FirstOrDefault();
            Require(preview != null && preview.AppliedProfile != null, "Preview aplica el borrador");
            Check(preview.AppliedProfile.gender == CharacterGender.Feminine, "Preview femenino coincide con UI");
            Check(Mathf.Approximately(preview.AppliedProfile.bodySize, 30) && Mathf.Approximately(preview.AppliedProfile.muscle, 65), "Preview aplica tamaño y musculatura elegidos con sliders");
            ValidateShapeWeights(preview.gameObject, preview.AppliedProfile);
        }

        static IEnumerator ValidatePopupAndCancel()
        {
            CharacterCreationMenu creator = FindCreator();
            yield return OpenDropdown(FindDropdown(creator, "Género"), "Género antes de cancelar");
            Button cancel = creator.GetComponentsInChildren<Button>().First(button => button.name == "Volver");
            GameObject blocker = LiveDropdownObjects().First(item => item.name == "Blocker" && item.activeInHierarchy);
            ClickPointer(blocker, ScreenRect(cancel.transform as RectTransform).center, "Blocker sobre Volver");
            yield return null; yield return null;
            Check(creator.IsOpen, "El primer clic sobre Volver con popup abierto sólo cierra la lista");
            ValidateNoDropdownOrphans("clic sobre formulario detrás del popup");
            ClickThroughEventSystem(cancel, "Volver tras cerrar popup");
            yield return null; yield return null;
            ValidateNoDropdownOrphans("Cancelar después de usar desplegable");
        }

        static void ClickCreatorTab(CharacterCreationMenu creator, string title)
        {
            Button tab = creator.GetComponentsInChildren<Button>(true).FirstOrDefault(button =>
                string.Equals(button.GetComponentInChildren<TMP_Text>(true)?.text, title, StringComparison.OrdinalIgnoreCase));
            Require(tab != null, "Tab " + title + " disponible");
            ClickThroughEventSystem(tab, "Tab " + title);
        }

        static TMP_Dropdown FindDropdown(CharacterCreationMenu creator, string row) => creator
            .GetComponentsInChildren<TMP_Dropdown>().FirstOrDefault(dropdown => dropdown.transform.parent.name == row);

        static ScrollRect FindOptionsScroll(CharacterCreationMenu creator) => creator
            .GetComponentsInChildren<ScrollRect>().First(scroll => scroll.name == "Options");

        static GameObject FindDropdownPopup(TMP_Dropdown dropdown) => dropdown != null
            ? dropdown.GetComponentsInChildren<Canvas>().Select(canvas => canvas.gameObject)
                .FirstOrDefault(item => item.name == "Dropdown List" && item.activeInHierarchy) : null;

        static GameObject[] LiveDropdownObjects() => UnityEngine.Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include, FindObjectsSortMode.None).Where(item => item.name == "Dropdown List" || item.name == "Blocker")
            .Select(item => item.gameObject).ToArray();

        static void ValidateNoDropdownOrphans(string description)
        {
            GameObject[] remaining = LiveDropdownObjects();
            Check(remaining.Length == 0, "Sin listas ni bloqueadores huérfanos después de " + description,
                remaining.Select(item => item.name));
        }

        static IEnumerator OpenDropdown(TMP_Dropdown dropdown, string description)
        {
            Require(dropdown != null && dropdown.options.Count > 0, "Dropdown disponible: " + description);
            yield return ScrollIntoView(dropdown.transform as RectTransform, FindOptionsScroll(FindCreator()), description);
            ClickThroughEventSystem(dropdown, "Abrir " + description);
            yield return null; yield return null;
            Require(dropdown != null && dropdown.IsExpanded, "Clic real abre " + description);
            ValidateOpenPopup(dropdown, description);
        }

        static IEnumerator ChooseDropdownOption(TMP_Dropdown dropdown, int index, string description)
        {
            yield return OpenDropdown(dropdown, description);
            yield return ChooseOpenDropdownOption(dropdown, index, description);
        }

        static IEnumerator ChooseOpenDropdownOption(TMP_Dropdown dropdown, int index, string description)
        {
            Require(index >= 0 && index < dropdown.options.Count, "Índice válido para " + description);
            GameObject popup = FindDropdownPopup(dropdown);
            Require(popup != null, "Lista real presente para " + description);
            string label = dropdown.options[index].text;
            Toggle option = popup.GetComponentsInChildren<Toggle>().FirstOrDefault(toggle =>
                toggle.GetComponentInChildren<TMP_Text>()?.text == label);
            Require(option != null, "Toggle generado para " + description + ": " + label);
            yield return ScrollIntoView(option.transform as RectTransform, popup.GetComponent<ScrollRect>(), description + " opción");
            ValidateOpenPopup(dropdown, description);
            ClickThroughEventSystem(option, "Elegir " + description + ": " + label);
            yield return null; yield return null;
            ValidateNoDropdownOrphans("seleccionar " + description + " y reconstruir opciones");
        }

        static IEnumerator ValidateFilterAndModule(CharacterCreationMenu creator, bool face)
        {
            TMP_Dropdown filter = FindDropdown(creator, "Filtrar piezas");
            Require(filter != null && filter.options.Count > 1, "Filtro de packs presente en " + (face ? "CARA" : "ROPA"));
            string pack = filter.options[1].text;
            yield return ChooseDropdownOption(filter, 1, "Filtrar piezas por " + pack);
            TMP_Dropdown filtered = FindDropdown(creator, "Filtrar piezas");
            Check(filtered != null && filtered.value == 1 && filtered.captionText.text == pack, "Filtro de pack refleja la opción elegida");
            string currentTab = face ? "CARA" : "ROPA", otherTab = face ? "ROPA" : "CARA";
            ClickCreatorTab(creator, otherTab);
            yield return null; yield return null;
            TMP_Dropdown otherFilter = FindDropdown(creator, "Filtrar piezas");
            int otherIndex = otherFilter.options.Count > 2 ? 2 : 0;
            string otherPack = otherFilter.options[otherIndex].text;
            yield return ChooseDropdownOption(otherFilter, otherIndex, "Filtro independiente de " + otherTab);
            ClickCreatorTab(creator, currentTab);
            yield return null; yield return null;
            filtered = FindDropdown(creator, "Filtrar piezas");
            Check(filtered != null && filtered.value == 1 && filtered.captionText.text == pack,
                "El filtro de " + currentTab + " se mantiene después de usar el filtro de " + otherTab);
            TMP_Dropdown module = creator.GetComponentsInChildren<TMP_Dropdown>().FirstOrDefault(dropdown =>
                dropdown.transform.parent.name != "Género" && dropdown.transform.parent.name != "Filtrar piezas"
                && dropdown.transform.parent.name != "Conjuntos Sidekick" && dropdown.transform.parent.name != "Caras de los personajes Sidekick"
                && dropdown.options.Where(option => option.text != "Sin pieza" && option.text != dropdown.captionText.text).Any());
            Require(module != null, "Módulo con alternativas del pack filtrado");
            Check(module.options.All(option => option.text == "Sin pieza" || IsBasicCharacterLabel(option.text, creator.Draft.gender)),
                "Las piezas usan nombres simples con género y número de variante");
            string slot = module.transform.parent.name;
            yield return ScrollIntoView(module.transform as RectTransform, FindOptionsScroll(creator), "pieza para conservar scroll");
            float savedScroll = FindOptionsScroll(creator).verticalNormalizedPosition;
            ClickCreatorTab(creator, otherTab);
            yield return null; yield return null;
            Check(FindDropdown(creator, "Filtrar piezas").captionText.text == otherPack,
                "El segundo filtro también conserva su selección en " + otherTab);
            ClickCreatorTab(creator, currentTab);
            yield return null; yield return null;
            Check(Mathf.Abs(FindOptionsScroll(creator).verticalNormalizedPosition - savedScroll) <= .02f,
                "La posición de la lista se mantiene al volver a " + currentTab);
            module = FindDropdown(creator, slot);
            string before = SelectedPartsKey(creator.Draft, face);
            int alternative = module.options.FindIndex(option => option.text != "Sin pieza" && option.text != module.captionText.text);
            Require(alternative >= 0, "Alternativa modular visible en " + module.transform.parent.name);
            string chosenLabel = module.options[alternative].text;
            yield return ChooseDropdownOption(module, alternative, "Módulo " + slot);
            Check(SelectedPartsKey(creator.Draft, face) != before, "Clic en módulo cambia IDs de " + (face ? "cara" : "ropa"));
            TMP_Dropdown changedModule = FindDropdown(creator, slot);
            Check(changedModule != null && changedModule.value == changedModule.options.FindIndex(option => option.text == chosenLabel)
                && changedModule.captionText.text == chosenLabel,
                "La pieza manual elegida conserva su selección después de reconstruir la lista");
            SidekickCharacterCatalog catalog = SidekickCharacterCatalog.Load();
            bool matchesRecipe = catalog.recipes.Any(recipe => RecipePartsKey(recipe, face) == SelectedPartsKey(creator.Draft, face));
            if (!matchesRecipe)
                Check(FindDropdown(creator, face ? "Caras de los personajes Sidekick" : "Conjuntos Sidekick").value == 0,
                    "Una combinación manual no muestra una receta que ya no coincide");
            Check(FindDropdown(creator, "Filtrar piezas").value == 1, "Editar una pieza mantiene el filtro activo");
        }

        static IEnumerator ValidateRecipeRetained(CharacterCreationMenu creator, bool face, int recipeIndex, StudioPresentationState stableStudio)
        {
            string row = face ? "Caras de los personajes Sidekick" : "Conjuntos Sidekick";
            string selectedParts = SelectedPartsKey(creator.Draft, face);
            TMP_Dropdown recipe = FindDropdown(creator, row);
            Check(recipe.options.Skip(1).All(option => IsBasicCharacterLabel(option.text, creator.Draft.gender)),
                "Las recetas usan nombres simples con género y número: " + row);
            Check(recipe.value == recipeIndex && recipe.captionText.text == recipe.options[recipeIndex].text,
                "La receta elegida queda seleccionada después de aplicarse: " + row);
            ClickCreatorTab(creator, "CUERPO");
            yield return null; yield return null;
            ClickCreatorTab(creator, face ? "ROPA" : "CARA");
            yield return null; yield return null;
            ClickCreatorTab(creator, face ? "CARA" : "ROPA");
            yield return null; yield return null;
            recipe = FindDropdown(creator, row);
            Check(recipe.value == recipeIndex && SelectedPartsKey(creator.Draft, face) == selectedParts,
                "Cambiar de pestaña conserva receta e IDs: " + row);
            foreach (string genderName in new[] { "Masculino", "Femenino" })
            {
                TMP_Dropdown gender = FindDropdown(creator, "Género");
                int genderIndex = gender.options.FindIndex(option => option.text == genderName);
                Require(genderIndex >= 0, "Género disponible para comprobar selección: " + genderName);
                yield return ChooseDropdownOption(gender, genderIndex, "Conservar receta con género " + genderName);
                recipe = FindDropdown(creator, row);
                Check(recipe.value == recipeIndex && recipe.captionText.text == recipe.options[recipeIndex].text
                    && SelectedPartsKey(creator.Draft, face) == selectedParts,
                    "Cambiar a " + genderName + " conserva receta y módulos: " + row);
                Check(IsBasicCharacterLabel(recipe.captionText.text, creator.Draft.gender),
                    "El nombre simple de la receta acompaña al género " + genderName);
                ValidateStableStudio(stableStudio, "conservar receta al cambiar género a " + genderName);
            }
        }

        static string RecipePartsKey(SidekickCharacterCatalog.Recipe recipe, bool face) => string.Join("|", recipe.parts
            .Where(selection => SidekickCharacterCatalog.IsFaceSlot(selection.slot) == face)
            .OrderBy(selection => selection.slot).Select(selection => selection.slot + "=" + selection.partId));

        static bool IsBasicCharacterLabel(string label, CharacterGender gender) => System.Text.RegularExpressions.Regex.IsMatch(label,
            "^" + (gender == CharacterGender.Feminine ? "Mujer" : "Hombre") + "(?: [a-záéíóúñü]+)* [1-9][0-9]*$");

        static Rect ScreenRect(RectTransform rect)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        static IEnumerator ScrollIntoView(RectTransform target, ScrollRect scroll, string description)
        {
            Require(target != null && scroll != null, "Scroll disponible para " + description);
            for (int attempt = 0; attempt < 100; attempt++)
            {
                Canvas.ForceUpdateCanvases();
                Rect viewport = ScreenRect(scroll.viewport), item = ScreenRect(target);
                if (item.yMin >= viewport.yMin - .5f && item.yMax <= viewport.yMax + .5f)
                {
                    Check(true, "Control accesible con scroll real: " + description);
                    yield break;
                }
                Vector2 position = viewport.center;
                PointerEventData pointer = PointerAt(scroll.gameObject, position);
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, hits);
                GameObject handler = hits.Count > 0 ? ExecuteEvents.GetEventHandler<IScrollHandler>(hits[0].gameObject) : null;
                Require(handler == scroll.gameObject, "Raycast de scroll alcanza " + description
                    + " (primer hit: " + (hits.Count > 0 ? hits[0].gameObject.name : "ninguno") + ")");
                pointer.pointerCurrentRaycast = hits[0];
                float distance = item.center.y - viewport.center.y;
                float scale = Mathf.Max(.01f, Mathf.Abs(scroll.transform.lossyScale.y));
                pointer.scrollDelta = new Vector2(0, Mathf.Clamp(distance / (scroll.scrollSensitivity * scale), -3f, 3f));
                ExecuteEvents.Execute(handler, pointer, ExecuteEvents.scrollHandler);
                yield return null;
            }
            Require(false, "No se pudo alcanzar con scroll " + description);
        }

        static void ValidateOpenPopup(TMP_Dropdown dropdown, string description)
        {
            Canvas.ForceUpdateCanvases();
            GameObject popup = FindDropdownPopup(dropdown);
            Require(popup != null, "Popup activo: " + description);
            GameObject blocker = LiveDropdownObjects().FirstOrDefault(item => item.name == "Blocker" && item.activeInHierarchy);
            Require(blocker != null, "Blocker activo: " + description);
            Canvas formCanvas = FindCreator().GetComponent<Canvas>();
            Canvas popupCanvas = popup.GetComponent<Canvas>(), blockerCanvas = blocker.GetComponent<Canvas>();
            Require(popupCanvas != null && blockerCanvas != null, "Popup y blocker tienen Canvas propios");
            Check(popupCanvas.overrideSorting && blockerCanvas.overrideSorting
                && popupCanvas.sortingLayerID == formCanvas.sortingLayerID
                && popupCanvas.sortingOrder > blockerCanvas.sortingOrder && blockerCanvas.sortingOrder > formCanvas.sortingOrder,
                $"Orden popup > blocker > formulario ({popupCanvas.sortingOrder}>{blockerCanvas.sortingOrder}>{formCanvas.sortingOrder}): {description}");
            Image background = popup.GetComponent<Image>();
            Check(background != null && background.color.a >= .99f, "Fondo opaco evita superposición de textos: " + description);
            Rect viewport = ScreenRect(popup.GetComponent<ScrollRect>().viewport);
            int tested = 0;
            foreach (Toggle toggle in popup.GetComponentsInChildren<Toggle>())
            {
                TMP_Text text = toggle.GetComponentInChildren<TMP_Text>();
                if (text == null || !viewport.Contains(ScreenRect(text.rectTransform).center)) continue;
                float alpha = text.color.a;
                foreach (CanvasGroup group in text.GetComponentsInParent<CanvasGroup>()) alpha *= group.alpha;
                Color foreground = text.color, back = toggle.GetComponent<Image>().color;
                float a = RelativeLuminance(foreground), b = RelativeLuminance(back);
                Check(alpha >= .95f && (Mathf.Max(a, b) + .05f) / (Mathf.Min(a, b) + .05f) >= 4.5f,
                    "Opción opaca y legible: " + text.text);
                Vector2 point = ScreenRect(toggle.transform as RectTransform).center;
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(PointerAt(toggle.gameObject, point), hits);
                Check(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == toggle.gameObject,
                    "La opción recibe el clic por encima del formulario: " + text.text);
                tested++;
            }
            Check(tested > 0, "Hay opciones legibles y alcanzables: " + description);
            // El bloqueo se comprueba sobre un botón real del formulario que queda
            // fuera del popup: el mismo clic no puede activar la pestaña subyacente.
            Button body = FindCreator().GetComponentsInChildren<Button>().First(button => button.name == "CUERPO");
            Vector2 bodyPoint = ScreenRect(body.transform as RectTransform).center;
            if (!ScreenRect(popup.transform as RectTransform).Contains(bodyPoint))
            {
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(PointerAt(body.gameObject, bodyPoint), hits);
                Check(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == blocker,
                    "El blocker intercepta controles de fondo: " + description);
            }
        }

        static void ClickOutsidePopup(CharacterCreationMenu creator, TMP_Dropdown dropdown)
        {
            GameObject blocker = LiveDropdownObjects().First(item => item.name == "Blocker" && item.activeInHierarchy);
            Vector2 position = ScreenRect(creator.transform.Find("StudioDragArea") as RectTransform).center;
            Require(!ScreenRect(FindDropdownPopup(dropdown).transform as RectTransform).Contains(position), "Clic exterior fuera del popup");
            ClickPointer(blocker, position, "Cerrar desplegable con clic exterior");
        }

        static void DragSlider(Slider slider, float normalized)
        {
            Require(slider != null && slider.IsInteractable(), "Slider interactivo: " + slider.transform.parent.name);
            RectTransform area = slider.handleRect.parent as RectTransform;
            Rect limits = ScreenRect(area);
            Vector2 start = ScreenRect(slider.handleRect).center;
            Vector2 finish = new Vector2(Mathf.Lerp(limits.xMin, limits.xMax, normalized), limits.center.y);
            PointerEventData pointer = PointerAt(slider.gameObject, start);
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Require(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerDownHandler>(hits[0].gameObject) == slider.gameObject,
                "Raycast real alcanza slider " + slider.transform.parent.name);
            pointer.pointerPress = slider.gameObject;
            pointer.rawPointerPress = hits[0].gameObject;
            pointer.pointerDrag = slider.gameObject;
            pointer.pointerCurrentRaycast = pointer.pointerPressRaycast = hits[0];
            pointer.pressPosition = start;
            pointer.useDragThreshold = false;
            EventSystem.current.SetSelectedGameObject(slider.gameObject, pointer);
            ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.initializePotentialDrag);
            ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.beginDragHandler);
            pointer.position = finish;
            pointer.delta = finish - start;
            pointer.dragging = true;
            ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(slider.gameObject, pointer, ExecuteEvents.endDragHandler);
            float expected = Mathf.Round(Mathf.Lerp(slider.minValue, slider.maxValue, normalized));
            Check(Mathf.Abs(slider.value - expected) <= 1f, "Arrastre real actualiza " + slider.transform.parent.name);
        }

        static string SelectedPartsKey(CharacterProfile profile, bool face) => string.Join("|", profile.parts
            .Where(selection => SidekickCharacterCatalog.IsFaceSlot(selection.slot) == face)
            .OrderBy(selection => selection.slot).Select(selection => selection.slot + "=" + selection.partId));

        static Camera FindStudioCamera() => UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
            .FirstOrDefault(camera => camera.name == "CharacterPreviewCamera");

        static StudioPresentationState SnapshotStudioPresentation()
        {
            Camera camera = FindStudioCamera();
            Require(camera != null, "Cámara presente para comprobar estabilidad del estudio");
            Transform rig = camera.transform.parent.Find("CharacterPreviewRig");
            Require(rig != null, "Rig presente para comprobar escala estable");
            var result = new StudioPresentationState
            {
                cameraPosition = camera.transform.position,
                cameraRotation = camera.transform.rotation,
                projection = camera.projectionMatrix,
                fieldOfView = camera.fieldOfView,
                rigScale = rig.localScale
            };
            foreach (MeshFilter item in camera.transform.parent.GetComponentsInChildren<MeshFilter>())
                if (!item.transform.IsChildOf(rig)) result.anchors[item.transform] = camera.WorldToViewportPoint(item.transform.position);
            Require(result.anchors.Count > 0, "Anclas del escenario 3D presentes para detectar saltos de escala");
            return result;
        }

        static bool StudioIsStable(StudioPresentationState state, out string detail)
        {
            Camera camera = FindStudioCamera();
            if (camera == null) { detail = "cámara ausente"; return false; }
            Transform rig = camera.transform.parent.Find("CharacterPreviewRig");
            float positionDelta = Vector3.Distance(state.cameraPosition, camera.transform.position);
            float angleDelta = Quaternion.Angle(state.cameraRotation, camera.transform.rotation);
            float fovDelta = Mathf.Abs(state.fieldOfView - camera.fieldOfView);
            float projectionDelta = Enumerable.Range(0, 16).Max(index => Mathf.Abs(state.projection[index] - camera.projectionMatrix[index]));
            float scaleDelta = rig != null ? Vector3.Distance(state.rigScale, rig.localScale) : float.PositiveInfinity;
            float anchorDelta = state.anchors.Max(item => item.Key != null
                ? Vector3.Distance(item.Value, camera.WorldToViewportPoint(item.Key.position)) : float.PositiveInfinity);
            detail = $"position={positionDelta:F6} angle={angleDelta:F6} FOV={fovDelta:F6} projection={projectionDelta:F6} rigScale={scaleDelta:F6} stage={anchorDelta:F6}";
            return positionDelta < .0001f && angleDelta < .001f && fovDelta < .0001f
                && projectionDelta < .0001f && scaleDelta < .0001f && anchorDelta < .0001f;
        }

        static void ValidateStableStudio(StudioPresentationState state, string description)
        {
            bool stable = StudioIsStable(state, out string detail);
            Check(stable, "Cámara, escala del rig y escenario estables al " + description + ": " + detail);
        }

        // Este barrido usa el mismo refresco del creador y mallas realmente horneadas.
        // La interacción se cubre por separado con raycasts, clics y arrastres reales.
        static IEnumerator ValidateStableRecipeFraming(CharacterCreationMenu creator)
        {
            Require(creator != null, "Creador presente para barrido de encuadre");
            SidekickCharacterCatalog catalog = SidekickCharacterCatalog.Load();
            CharacterProfile original = creator.Draft.Clone();
            StudioPresentationState stableStudio = SnapshotStudioPresentation();
            MethodInfo refresh = typeof(CharacterCreationMenu).GetMethod("RefreshPreview", BindingFlags.NonPublic | BindingFlags.Instance);
            Require(refresh != null, "Refresco real del creador disponible para barrido de recetas");
            var framingFailures = new List<string>();
            var movementFailures = new List<string>();
            int combinations = 0;
            try
            {
                foreach (var recipe in catalog.recipes)
                foreach (CharacterGender gender in Enum.GetValues(typeof(CharacterGender)))
                foreach (float size in new[] { -100f, 100f })
                {
                    CharacterProfile sample = catalog.CreateDefault();
                    sample.gender = gender;
                    sample.bodySize = size;
                    sample.muscle = 100;
                    catalog.ApplyFaceRecipe(sample, recipe.id);
                    catalog.ApplyOutfitRecipe(sample, recipe.id);
                    CopyDraft(sample, creator.Draft);
                    refresh.Invoke(creator, null);
                    yield return null;
                    combinations++;
                    string description = recipe.id + "/" + gender + "/size=" + size;
                    if (!StudioIsStable(stableStudio, out string movement)) movementFailures.Add(description + ": " + movement);
                    if (!PreviewFitsFreeViewport(creator, FindStudioCamera(), out string framing)) framingFailures.Add(description + ": " + framing);
                }
            }
            finally
            {
                CopyDraft(original, creator.Draft);
                refresh.Invoke(creator, null);
            }
            yield return null;
            Check(combinations == catalog.recipes.Length * 4, "Se probaron todas las recetas, ambos géneros y extremos corporales: " + combinations);
            Check(movementFailures.Count == 0, "Cambiar entre todas las recetas conserva cámara, FOV, escala y anclas del escenario", movementFailures);
            Check(framingFailures.Count == 0, "Todas las recetas y extremos corporales se ven completos con el mismo encuadre", framingFailures);
            Check(JsonUtility.ToJson(creator.Draft) == JsonUtility.ToJson(original), "El barrido de encuadre restaura el borrador elegido desde UI");
        }

        static IEnumerator ValidateStableAllModuleFraming(CharacterCreationMenu creator)
        {
            Require(creator != null, "Creador presente para barrido de todos los módulos");
            SidekickCharacterCatalog catalog = SidekickCharacterCatalog.Load();
            CharacterProfile original = creator.Draft.Clone();
            StudioPresentationState stableStudio = SnapshotStudioPresentation();
            MethodInfo refresh = typeof(CharacterCreationMenu).GetMethod("RefreshPreview", BindingFlags.NonPublic | BindingFlags.Instance);
            Require(refresh != null, "Refresco real del creador disponible para barrido modular");
            Transform rig = FindStudioCamera().transform.parent.Find("CharacterPreviewRig");
            SidekickCharacterAppearance appearance = rig.GetComponent<SidekickCharacterAppearance>();
            Require(appearance != null, "Compositor del creador presente para comprobar cobertura de módulos");
            var bySlot = catalog.parts.GroupBy(part => part.slot).OrderBy(group => group.Key)
                .Select(group => group.OrderBy(part => part.id, StringComparer.Ordinal).ToArray()).ToArray();
            int batches = bySlot.Max(group => group.Length);
            var framingFailures = new List<string>();
            var movementFailures = new List<string>();
            var compositionFailures = new List<string>();
            var coverage = new Dictionary<string, HashSet<string>>();
            int combinations = 0;
            try
            {
                for (int batch = 0; batch < batches; batch++)
                foreach (CharacterGender gender in Enum.GetValues(typeof(CharacterGender)))
                foreach (float size in new[] { -100f, 100f })
                {
                    string scenario = gender + "/size=" + size;
                    string description = "batch=" + batch + "/" + scenario;
                    CharacterProfile sample = catalog.CreateDefault();
                    sample.gender = gender;
                    sample.bodySize = size;
                    sample.muscle = 100;
                    var batchParts = bySlot.Where(group => batch < group.Length).Select(group => group[batch]).ToArray();
                    foreach (var part in batchParts)
                    {
                        sample.parts.RemoveAll(selection => selection.slot == part.slot);
                        sample.parts.Add(new CharacterPartSelection { slot = part.slot, partId = part.id });
                    }
                    CopyDraft(sample, creator.Draft);
                    refresh.Invoke(creator, null);
                    yield return null;
                    combinations++;
                    if (!coverage.TryGetValue(scenario, out HashSet<string> seen)) coverage[scenario] = seen = new HashSet<string>(StringComparer.Ordinal);
                    var rendererNames = new HashSet<string>(rig.GetComponentsInChildren<SkinnedMeshRenderer>()
                        .Where(renderer => renderer.enabled && renderer.sharedMesh != null && renderer.gameObject.activeInHierarchy)
                        .Select(renderer => renderer.name), StringComparer.Ordinal);
                    foreach (var part in batchParts)
                        if (rendererNames.Contains(part.slot + "/" + part.id)) seen.Add(part.id);
                        else compositionFailures.Add(description + ": renderer ausente " + part.id);
                    if (appearance.AppliedProfile == null || JsonUtility.ToJson(appearance.AppliedProfile) != JsonUtility.ToJson(sample))
                        compositionFailures.Add(description + ": el compositor no aplicó exactamente el perfil solicitado");
                    if (!StudioIsStable(stableStudio, out string movement)) movementFailures.Add(description + ": " + movement);
                    if (!PreviewFitsFreeViewport(creator, FindStudioCamera(), out string framing))
                        framingFailures.Add(description + ": " + framing + "; parts=" + string.Join(",", batchParts.Select(part => part.id)));
                }
            }
            finally
            {
                CopyDraft(original, creator.Draft);
                refresh.Invoke(creator, null);
            }
            yield return null;
            Check(combinations == batches * 4, "Se probaron todos los lotes modulares, ambos géneros y extremos corporales: " + combinations);
            Check(compositionFailures.Count == 0, "Cada módulo solicitado tiene renderer activo y perfil aplicado exactamente", compositionFailures);
            var expectedIds = new HashSet<string>(catalog.parts.Select(part => part.id), StringComparer.Ordinal);
            var coverageFailures = coverage.Where(item => !item.Value.SetEquals(expectedIds))
                .Select(item => item.Key + ": faltan " + string.Join(",", expectedIds.Except(item.Value))).ToList();
            Check(coverage.Count == 4 && coverageFailures.Count == 0,
                "Los " + expectedIds.Count + " módulos fueron renderizados en ambos géneros y extremos corporales", coverageFailures);
            Check(movementFailures.Count == 0, "Todos los módulos mantienen cámara, FOV, escala y anclas del escenario", movementFailures);
            Check(framingFailures.Count == 0, "Todos los módulos se ven completos con el mismo encuadre fijo", framingFailures);
            Check(JsonUtility.ToJson(creator.Draft) == JsonUtility.ToJson(original), "El barrido de todos los módulos restaura el borrador elegido desde UI");
        }

        static void CopyDraft(CharacterProfile source, CharacterProfile target)
        {
            target.displayName = source.displayName;
            target.gender = source.gender;
            target.bodySize = source.bodySize;
            target.muscle = source.muscle;
            target.parts = source.parts.Select(selection => new CharacterPartSelection { slot = selection.slot, partId = selection.partId }).ToList();
        }

        static bool PreviewFitsFreeViewport(CharacterCreationMenu creator, Camera camera, out string detail)
        {
            Transform rig = camera.transform.parent.Find("CharacterPreviewRig");
            var projected = new List<Vector3>();
            foreach (SkinnedMeshRenderer renderer in rig.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!renderer.enabled || renderer.sharedMesh == null || !renderer.gameObject.activeInHierarchy) continue;
                var baked = new Mesh();
                try
                {
                    renderer.BakeMesh(baked);
                    projected.AddRange(baked.vertices.Select(vertex => camera.WorldToViewportPoint(renderer.transform.TransformPoint(vertex))));
                }
                finally { UnityEngine.Object.DestroyImmediate(baked); }
            }
            if (projected.Count == 0) { detail = "sin geometría visible"; return false; }
            var panel = (RectTransform)creator.transform.Find("Customization");
            float panelLeft = ScreenRect(panel).xMin / Screen.width;
            float minX = projected.Min(point => point.x), maxX = projected.Max(point => point.x);
            float minY = projected.Min(point => point.y), maxY = projected.Max(point => point.y);
            detail = $"x={minX:F3}..{maxX:F3} y={minY:F3}..{maxY:F3} panel={panelLeft:F3}";
            return projected.All(IsFinite) && projected.All(point => point.z > camera.nearClipPlane && point.z < camera.farClipPlane)
                && minX >= .01f && maxX <= panelLeft - .01f && minY >= .01f && maxY <= .99f;
        }

        static void SnapshotMenuState()
        {
            menuCanvasStates.Clear();
            menuControlStates.Clear();
            menuCameraStates.Clear();
            menuStudioMaterials.Clear();
            foreach (Material material in Resources.FindObjectsOfTypeAll<Material>())
                if (material.name.StartsWith("Studio", StringComparison.Ordinal)) menuStudioMaterials.Add(material.GetInstanceID());
            foreach (Canvas canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                menuCanvasStates.Add(canvas, canvas.enabled);
            foreach (Selectable control in UnityEngine.Object.FindObjectsByType<Selectable>(FindObjectsSortMode.None))
                menuControlStates.Add(control, control.interactable);
            foreach (Camera camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                menuCameraStates.Add(camera, new CameraState
                { enabled = camera.enabled, mask = camera.cullingMask, target = camera.targetTexture });
        }

        static void ValidateMenuRestored()
        {
            ValidateStudioCleaned();
            Check(menuCanvasStates.All(item => item.Key != null && item.Key.enabled == item.Value),
                "Cancelar restaura los Canvas originales");
            Check(menuControlStates.All(item => item.Key != null && item.Key.interactable == item.Value),
                "Cancelar restaura la interacción original");
            Check(menuCameraStates.All(item => item.Key != null && item.Key.enabled == item.Value.enabled
                && item.Key.cullingMask == item.Value.mask && item.Key.targetTexture == item.Value.target),
                "Cancelar restaura cámaras y sus máscaras/targets");
            string[] leakedMaterials = Resources.FindObjectsOfTypeAll<Material>()
                .Where(material => material.name.StartsWith("Studio", StringComparison.Ordinal) && !menuStudioMaterials.Contains(material.GetInstanceID()))
                .Select(material => material.name).ToArray();
            Check(leakedMaterials.Length == 0, "Cancelar libera materiales generados del estudio", leakedMaterials);
        }

        static void ValidateStudioCleaned()
        {
            ValidateNoDropdownOrphans("cerrar/cancelar el creador");
            string[] leaked = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(item => item.name == "CharacterCreationPreview" || item.name == "CharacterPreviewCamera"
                    || item.name == "CharacterPreviewRig" || item.name == "CharacterCreationMenu")
                .Select(item => item.name).ToArray();
            Check(leaked.Length == 0, "Cierre elimina estudio, cámara, rig y creador", leaked);
        }

        static void ClickCreatorButton(CharacterCreationMenu creator, string title)
        {
            Button button = creator.GetComponentsInChildren<Button>(true).FirstOrDefault(item =>
                string.Equals(item.GetComponentInChildren<TMP_Text>(true)?.text, title, StringComparison.OrdinalIgnoreCase));
            Require(button != null, "Botón " + title + " disponible");
            ClickThroughEventSystem(button, title);
        }

        static void ClickThroughEventSystem(Selectable control, string description)
        {
            Require(EventSystem.current != null, "EventSystem disponible para " + description);
            Require(control != null && control.isActiveAndEnabled && control.IsInteractable(), "Control interactivo: " + description);
            Canvas.ForceUpdateCanvases();
            ClickPointer(control.gameObject, ScreenRect(control.transform as RectTransform).center, description);
        }

        static PointerEventData PointerAt(GameObject control, Vector2 position)
        {
            Canvas canvas = control.GetComponentInParent<Canvas>().rootCanvas;
            return new PointerEventData(EventSystem.current)
            { position = position, displayIndex = canvas.targetDisplay,
                button = PointerEventData.InputButton.Left, pointerId = -1, eligibleForClick = true };
        }

        static void ClickPointer(GameObject expected, Vector2 position, string description)
        {
            var pointer = PointerAt(expected, position);
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            GameObject target = hits.Count > 0 ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) : null;
            Require(target == expected, "Raycast real alcanza " + description
                + (hits.Count == 0 ? " (sin hits) " + DescribeUiState(expected.GetComponent<Button>(), position)
                    : " (primer hit: " + hits[0].gameObject.name + ")"));
            pointer.pointerCurrentRaycast = hits[0];
            pointer.pointerPressRaycast = hits[0];
            pointer.pressPosition = position;
            pointer.pointerPress = target;
            pointer.rawPointerPress = hits[0].gameObject;
            EventSystem.current.SetSelectedGameObject(target, pointer);
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerClickHandler);
        }

        static void ValidateCreatorPresentation(CharacterCreationMenu creator)
        {
            Require(creator != null, "Creador presente para validación visual");
            Camera camera = FindStudioCamera();
            Require(camera != null && camera.enabled && camera.targetTexture == null, "Cámara real del estudio activa y sin RenderTexture");
            Check(camera.rect == new Rect(0, 0, 1, 1) && !camera.orthographic, "Estudio ocupa la pantalla con perspectiva 3D");
            ValidateCreatorText(creator);
            ValidateStudioVisibility(creator, camera);
            Quaternion originalRotation = camera.transform.parent.Find("CharacterPreviewRig").rotation;
            for (int i = 0; i < 3; i++) ClickCreatorButton(creator, "←");
            Check(Quaternion.Angle(originalRotation, camera.transform.parent.Find("CharacterPreviewRig").rotation) > 80,
                "Controles de giro rotan el personaje en el estudio");
            ValidateStudioVisibility(creator, camera);
            ClickCreatorButton(creator, "Frente");
            Check(Quaternion.Angle(originalRotation, camera.transform.parent.Find("CharacterPreviewRig").rotation) < .1f,
                "Frente restaura la orientación original");
        }

        static void ValidateCreatorText(CharacterCreationMenu creator)
        {
            string[] titles = { "CARA", "CUERPO", "ROPA", "CREAR Y JUGAR" };
            foreach (string title in titles)
            {
                Button button = creator.GetComponentsInChildren<Button>(true).FirstOrDefault(item =>
                    item.GetComponentInChildren<TMP_Text>(true)?.text == title);
                Require(button != null, "Texto de control presente: " + title);
                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                Color background = button.GetComponent<Image>().color;
                Check(label.color.a >= .95f, "Texto opaco en " + title);
                float foregroundLuminance = RelativeLuminance(label.color);
                float backgroundLuminance = RelativeLuminance(background);
                float contrast = (Mathf.Max(foregroundLuminance, backgroundLuminance) + .05f)
                    / (Mathf.Min(foregroundLuminance, backgroundLuminance) + .05f);
                Check(contrast >= 4.5f, $"Contraste legible en {title}: {contrast:F2}:1");
            }
        }

        static float RelativeLuminance(Color color)
        {
            float Linear(float channel) => channel <= .04045f ? channel / 12.92f : Mathf.Pow((channel + .055f) / 1.055f, 2.4f);
            return .2126f * Linear(color.r) + .7152f * Linear(color.g) + .0722f * Linear(color.b);
        }

        static void ValidateStudioVisibility(CharacterCreationMenu creator, Camera camera)
        {
            SidekickCharacterAppearance preview = UnityEngine.Object.FindObjectsByType<SidekickCharacterAppearance>(FindObjectsSortMode.None)
                .FirstOrDefault(item => item.name == "CharacterPreviewRig");
            Require(preview != null && preview.AppliedProfile != null, "Rig del estudio tiene apariencia aplicada");
            var vertices = new List<Vector3>();
            var samples = new List<Vector3>();
            foreach (SkinnedMeshRenderer renderer in preview.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!renderer.enabled || renderer.sharedMesh == null || !renderer.gameObject.activeInHierarchy) continue;
                var baked = new Mesh();
                try
                {
                    renderer.BakeMesh(baked);
                    Vector3[] world = baked.vertices.Select(renderer.transform.TransformPoint).ToArray();
                    vertices.AddRange(world);
                    // Se muestrean módulos de todo el cuerpo, además de comprobar el encuadre
                    // de cada vértice. Los bounds declarados del compositor son expandidos.
                    for (int index = 0; index < world.Length; index += Mathf.Max(1, world.Length / 16))
                        samples.Add(world[index]);
                }
                finally { UnityEngine.Object.DestroyImmediate(baked); }
            }
            Require(vertices.Count > 0 && vertices.All(IsFinite), "Mallas horneadas del preview contienen geometría finita");
            var panel = (RectTransform)creator.transform.Find("Customization");
            Require(panel != null, "Panel lateral presente para comprobar área libre");
            var corners = new Vector3[4];
            panel.GetWorldCorners(corners);
            Canvas canvas = creator.GetComponent<Canvas>();
            float panelLeft = corners.Select(corner => RectTransformUtility.WorldToScreenPoint(
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, corner).x).Min();
            panelLeft = camera.ScreenToViewportPoint(new Vector3(panelLeft, 0, 0)).x;
            Vector3[] projected = vertices.Select(camera.WorldToViewportPoint).ToArray();
            float minX = projected.Min(point => point.x), maxX = projected.Max(point => point.x);
            float minY = projected.Min(point => point.y), maxY = projected.Max(point => point.y);
            Check(projected.All(point => point.z > camera.nearClipPlane && point.z < camera.farClipPlane),
                "Toda la geometría real queda dentro de los planos de cámara");
            Check(minX >= .01f && maxX <= panelLeft - .01f && minY >= .01f && maxY <= .99f,
                $"Personaje completo en viewport libre izquierdo: x={minX:F3}..{maxX:F3} y={minY:F3}..{maxY:F3} panel={panelLeft:F3}");
            Check(maxY - minY >= .4f, "El cuerpo completo ocupa al menos el 40% de la altura de pantalla");

            var environment = new List<StudioGeometry>();
            foreach (MeshFilter filter in preview.transform.parent.GetComponentsInChildren<MeshFilter>(true))
            {
                Renderer renderer = filter.GetComponent<Renderer>();
                if (filter.transform.IsChildOf(preview.transform) || renderer == null || !renderer.enabled
                    || !filter.gameObject.activeInHierarchy || filter.sharedMesh == null
                    || (camera.cullingMask & (1 << filter.gameObject.layer)) == 0) continue;
                environment.Add(new StudioGeometry { name = filter.name,
                    vertices = filter.sharedMesh.vertices.Select(filter.transform.TransformPoint).ToArray(), triangles = filter.sharedMesh.triangles });
            }
            Require(environment.Count > 0, "Entorno 3D contiene geometría real");
            var obstructions = new List<string>();
            foreach (Vector3 sample in samples)
            {
                Vector3 direction = sample - camera.transform.position;
                float distance = direction.magnitude;
                var ray = new Ray(camera.transform.position, direction / distance);
                foreach (StudioGeometry geometry in environment)
                {
                    if (!MeshBlocksRay(geometry, ray, distance - .003f)) continue;
                    obstructions.Add(geometry.name);
                    break;
                }
            }
            Check(obstructions.Count == 0, $"Geometría del cuerpo sin obstrucciones de escenario: {samples.Count - obstructions.Count}/{samples.Count}",
                obstructions.Distinct());
        }

        static bool MeshBlocksRay(StudioGeometry mesh, Ray ray, float maximumDistance)
        {
            for (int index = 0; index + 2 < mesh.triangles.Length; index += 3)
            {
                Vector3 a = mesh.vertices[mesh.triangles[index]];
                Vector3 edge1 = mesh.vertices[mesh.triangles[index + 1]] - a;
                Vector3 edge2 = mesh.vertices[mesh.triangles[index + 2]] - a;
                Vector3 p = Vector3.Cross(ray.direction, edge2);
                float determinant = Vector3.Dot(edge1, p);
                if (Mathf.Abs(determinant) < .0000001f) continue;
                float inverse = 1 / determinant;
                Vector3 offset = ray.origin - a;
                float u = Vector3.Dot(offset, p) * inverse;
                if (u < 0 || u > 1) continue;
                Vector3 q = Vector3.Cross(offset, edge1);
                float v = Vector3.Dot(ray.direction, q) * inverse;
                if (v < 0 || u + v > 1) continue;
                float distance = Vector3.Dot(edge2, q) * inverse;
                if (distance > .0001f && distance < maximumDistance) return true;
            }
            return false;
        }

        static void ValidateRuntimePlayer()
        {
            PlayerWeaponController weapon = UnityEngine.Object.FindFirstObjectByType<PlayerWeaponController>();
            Require(weapon != null, "Jugador con armas existe en partida");
            var appearance = weapon.GetComponent<SidekickCharacterAppearance>();
            Check(appearance != null, "Apariencia en jugador real");
            Check(appearance != null && appearance.AppliedProfile != null && JsonUtility.ToJson(appearance.AppliedProfile) == expectedProfileJson, "Jugador real aplica exactamente el perfil guardado");
            Animator animator = weapon.GetComponent<Animator>();
            Check(animator != null && animator.isHuman && animator.runtimeAnimatorController != null, "Avatar y Animator humanoide activos en partida");
            Check(weapon.GetComponentsInChildren<SkinnedMeshRenderer>(true).Count(renderer => renderer.enabled && renderer.sharedMesh != null) > 1, "Partes modulares visibles en partida");
            Check(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(listener => listener.isActiveAndEnabled) == 1, "Un AudioListener activo");
            Check(Time.timeScale > 0f, "Partida sin pausa residual del creador");
        }

        static CharacterCreationMenu FindCreator() => UnityEngine.Object.FindObjectsByType<CharacterCreationMenu>(FindObjectsSortMode.None).FirstOrDefault(item => item.IsOpen);

        static void InvokePlayButton()
        {
            Button play = FindPlayButton();
            Require(play != null, "Botón Play de escena enlazado a StartGame");
            ClickThroughEventSystem(play, "Play del menú principal");
        }

        static Button FindPlayButton() => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(button =>
            Enumerable.Range(0, button.onClick.GetPersistentEventCount()).Any(index => button.onClick.GetPersistentMethodName(index) == "StartGame"));

        static void PrepareBatchGameView()
        {
            if (validationGameView == null)
            {
                Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                if (gameViewType == null) throw new InvalidOperationException("GameView no disponible para inicializar framebuffer del harness.");
                validationGameView = EditorWindow.GetWindow(gameViewType, false, "Game", true);
                // Las cámaras y los Canvas de SceneAlejandro usan Display 0. El
                // raycaster de uGUI consulta el display activo del GameView en Editor.
                var serialized = new SerializedObject(validationGameView);
                SerializedProperty display = serialized.FindProperty("m_TargetDisplay");
                if (display != null) { display.intValue = 0; serialized.ApplyModifiedPropertiesWithoutUndo(); }
                SetValidationGameViewSize(gameViewType);
            }
            else if (validationSizeSelection != null && validationSizeIndex >= 0
                && (int)validationSizeSelection.GetValue(validationGameView) != validationSizeIndex)
            {
                // La inicialización tardía del Editor puede volver a Free Aspect.
                // Sólo se vuelve a elegir la resolución si efectivamente cambió.
                validationSizeSelection.SetValue(validationGameView, validationSizeIndex);
                Debug.Log("CHARACTER_GAMEVIEW_RESTORED size=1440x900 index=" + validationSizeIndex);
            }
            validationGameView.Repaint();
        }

        static void SetValidationGameViewSize(Type gameViewType)
        {
            Assembly editor = typeof(EditorWindow).Assembly;
            Type sizesType = editor.GetType("UnityEditor.GameViewSizes"), sizeType = editor.GetType("UnityEditor.GameViewSize");
            Type sizeKind = editor.GetType("UnityEditor.GameViewSizeType");
            Require(sizesType != null && sizeType != null && sizeKind != null, "APIs de tamaños de GameView disponibles");
            Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            object groupKind = sizesType.GetProperty("currentGroupType", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(sizes);
            object group = sizesType.GetMethod("GetGroup", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Invoke(sizes, new[] { groupKind });
            Type groupType = group.GetType();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            MethodInfo getSize = groupType.GetMethod("GetGameViewSize", flags);
            int count = (int)groupType.GetMethod("GetTotalCount", flags).Invoke(group, null), chosen = -1;
            for (int index = 0; index < count; index++)
            {
                object size = getSize.Invoke(group, new object[] { index });
                int width = (int)sizeType.GetProperty("width", flags).GetValue(size);
                int height = (int)sizeType.GetProperty("height", flags).GetValue(size);
                object kind = sizeType.GetProperty("sizeType", flags).GetValue(size);
                if (width == 1440 && height == 900 && kind.ToString() == "FixedResolution") { chosen = index; break; }
            }
            if (chosen < 0)
            {
                object size = Activator.CreateInstance(sizeType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, new[] { Enum.Parse(sizeKind, "FixedResolution"), (object)1440, 900, "Character validation 1440x900" }, null);
                groupType.GetMethod("AddCustomSize", flags).Invoke(group, new[] { size });
                chosen = count;
            }
            PropertyInfo selection = gameViewType.GetProperty("selectedSizeIndex", flags);
            Require(selection != null && selection.CanWrite, "GameView permite elegir resolución fija");
            validationSizeSelection = selection;
            validationSizeIndex = chosen;
            selection.SetValue(validationGameView, chosen);
            validationGameView.position = new Rect(0, 0, 1480, 960);
            validationGameView.Show();
            Debug.Log("CHARACTER_GAMEVIEW_FIXED size=1440x900 index=" + chosen);
        }

        static void RequestPlayerFrame()
        {
            PrepareBatchGameView();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        static RenderTexture GameViewFramebuffer()
        {
            if (validationGameView == null) return null;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            for (Type type = validationGameView.GetType(); type != null; type = type.BaseType)
            {
                PropertyInfo property = type.GetProperty("targetRenderTexture", flags);
                if (property != null && property.GetValue(validationGameView) is RenderTexture propertyTexture) return propertyTexture;
                FieldInfo field = type.GetField("m_RenderTexture", flags);
                if (field != null && field.GetValue(validationGameView) is RenderTexture fieldTexture) return fieldTexture;
            }
            return null;
        }

        static bool ValidationFramebufferReady()
        {
            PrepareBatchGameView();
            RenderTexture framebuffer = GameViewFramebuffer();
            if (framebuffer != null && framebuffer.IsCreated() && framebuffer.width == 1440 && framebuffer.height == 900) return true;
            lastUiReadiness = $"GameView framebuffer={(framebuffer == null ? "null" : framebuffer.width + "x" + framebuffer.height)} "
                + $"contextScreen={Screen.width}x{Screen.height} sizeIndex={validationSizeSelection?.GetValue(validationGameView)} expectedIndex={validationSizeIndex}";
            if (EditorApplication.timeSinceStartup >= nextUiReadinessLog)
            {
                Debug.Log("CHARACTER_FRAMEBUFFER_WAIT " + lastUiReadiness);
                nextUiReadinessLog = EditorApplication.timeSinceStartup + 5;
            }
            RequestPlayerFrame();
            return false;
        }

        static bool MainMenuGraphicsReady()
        {
            Canvas.ForceUpdateCanvases();
            Button play = FindPlayButton();
            Canvas canvas = play != null ? play.GetComponentInParent<Canvas>()?.rootCanvas : null;
            Graphic graphic = play != null ? play.targetGraphic : null;
            Vector2 position = play != null ? RectTransformUtility.WorldToScreenPoint(
                canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null,
                ((RectTransform)play.transform).TransformPoint(((RectTransform)play.transform).rect.center)) : Vector2.zero;
            bool ready = play != null && canvas != null && canvas.isActiveAndEnabled && graphic != null
                && graphic.depth >= 0 && !graphic.canvasRenderer.cull
                && Mathf.Approximately(canvas.pixelRect.width, 1440) && Mathf.Approximately(canvas.pixelRect.height, 900)
                && Mathf.Abs(canvas.transform.lossyScale.x) > .00001f && Mathf.Abs(canvas.transform.lossyScale.y) > .00001f
                && canvas.pixelRect.Contains(position);
            if (ready) return true;
            string diagnostic = DescribeUiState(play, position);
            lastUiReadiness = diagnostic;
            if (EditorApplication.timeSinceStartup >= nextUiReadinessLog)
            {
                Debug.Log("CHARACTER_UI_WAIT " + diagnostic);
                nextUiReadinessLog = EditorApplication.timeSinceStartup + 5;
            }
            RequestPlayerFrame();
            return false;
        }

        static string DescribeUiState(Button button, Vector2 position)
        {
            Canvas canvas = button != null ? button.GetComponentInParent<Canvas>()?.rootCanvas : null;
            Graphic graphic = button != null ? button.targetGraphic : null;
            GraphicRaycaster raycaster = canvas != null ? canvas.GetComponent<GraphicRaycaster>() : null;
            object activeDisplay = typeof(Display).GetProperty("activeEditorGameViewTarget", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
            return $"frame={Time.frameCount} stageFrame={stageFrame} screen={Screen.width}x{Screen.height} pointer={position} "
                + $"canvas={(canvas != null ? canvas.name : "null")} enabled={canvas?.isActiveAndEnabled} rect={canvas?.pixelRect} "
                + $"scale={canvas?.transform.lossyScale} display={canvas?.targetDisplay} editorDisplay={activeDisplay} "
                + $"graphicDepth={graphic?.depth} cull={graphic?.canvasRenderer.cull} raycastTarget={graphic?.raycastTarget} "
                + $"raycaster={raycaster?.isActiveAndEnabled} registeredRaycasters={RaycasterManager.GetRaycasters().Count} "
                + $"registeredGraphics={(canvas != null ? GraphicRegistry.GetRaycastableGraphicsForCanvas(canvas).Count : 0)}";
        }

        static bool GameplayReady()
        {
            if (SceneManager.GetActiveScene().name != "Level 1") return false;
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerWeaponController>();
            var appearance = player != null ? player.GetComponent<SidekickCharacterAppearance>() : null;
            return appearance != null && appearance.AppliedProfile != null && Camera.main != null;
        }

        static void CaptureScreen(string fileName)
        {
            // Se captura el framebuffer ya renderizado del GameView fijo: incluye
            // el Canvas overlay, los popups y su orden real sin moverlos de capa,
            // cambiar la cámara ni reencuadrar la evidencia.
            RenderTexture framebuffer = GameViewFramebuffer();
            Require(framebuffer != null && framebuffer.IsCreated() && framebuffer.width == 1440 && framebuffer.height == 900,
                "Framebuffer real 1440x900 disponible para " + fileName);
            var pixels = new Texture2D(framebuffer.width, framebuffer.height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                // Screen.width en EditorApplication.update puede describir otra vista
                // del Editor. Se leen los píxeles de la textura real de GameView.
                RenderTexture.active = framebuffer;
                pixels.ReadPixels(new Rect(0, 0, framebuffer.width, framebuffer.height), 0, 0);
                // D3D almacena el RT del GameView con el origen vertical superior.
                // Se normalizan sólo las filas del readback para codificar el PNG;
                // no se modifica el framebuffer, el Canvas ni la cámara del juego.
                if (SystemInfo.graphicsUVStartsAtTop)
                {
                    Color32[] rows = pixels.GetPixels32();
                    int width = pixels.width, height = pixels.height;
                    for (int row = 0; row < height / 2; row++)
                    {
                        int upper = row * width, lower = (height - 1 - row) * width;
                        for (int column = 0; column < width; column++)
                        {
                            Color32 temporary = rows[upper + column];
                            rows[upper + column] = rows[lower + column];
                            rows[lower + column] = temporary;
                        }
                    }
                    pixels.SetPixels32(rows);
                }
                pixels.Apply();
                Require(pixels.width == 1440 && pixels.height == 900,
                    $"Captura conserva tamaño de GameView: {pixels.width}x{pixels.height}");
                Color32[] colors = pixels.GetPixels32();
                int visible = 0;
                for (int index = 0; index < colors.Length; index += 37)
                {
                    Color32 color = colors[index];
                    if (color.r > 10 || color.g > 10 || color.b > 10) visible++;
                }
                Check(visible > colors.Length / 370, "Framebuffer contiene imagen visible: " + fileName);
                string path = Path.Combine(EvidenceFolder, fileName);
                File.WriteAllBytes(path, pixels.EncodeToPNG());
                Check(File.Exists(path) && new FileInfo(path).Length > 4096, "Captura real generada: " + fileName);
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(pixels);
            }
        }

        static void SetProfileOverride(string path)
        {
            var property = typeof(CharacterProfileStore).GetProperty("ProfilePathOverride", BindingFlags.Public | BindingFlags.Static);
            if (property == null) throw new MissingMemberException("CharacterProfileStore.ProfilePathOverride");
            property.SetValue(null, path);
        }

        static void FinishRuntime()
        {
            Application.logMessageReceived -= CaptureLog;
            Check(runtimeErrors.Count == 0, "Sin errores de runtime durante flujo", runtimeErrors);
            bool crossSession = SessionState.GetBool(SessionKey + ".CrossSession", false);
            bool creatorOnly = SessionState.GetBool(SessionKey + ".CreatorOnly", false);
            WriteResult(creatorOnly ? "creator-ui" : crossSession ? "cross-session" : "play-mode",
                creatorOnly ? "creator-ui-result.json" : crossSession ? "cross-session-result.json" : "play-result.json");
            string resultLabel = creatorOnly ? "SURVIVE_CHARACTER_UI_VALIDATION" : "SURVIVE_CHARACTER_PLAY_VALIDATION";
            Debug.Log($"{resultLabel}={(failed == 0 ? "PASS" : "FAIL")} passed={passed} failed={failed} errors={runtimeErrors.Count}");
            SetProfileOverride(null);
            SessionState.SetInt(SessionKey + ".ExitCode", failed == 0 ? 0 : 1);
            EditorApplication.ExitPlaymode();
        }

        static void CaptureLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                runtimeErrors.Add("[" + type + "] " + message + (string.IsNullOrEmpty(stack) ? "" : "\n" + stack));
        }

        static void ResetCounters() { passed = failed = 0; failures.Clear(); uiSteps.Clear(); }
        static void SetStage(int value)
        { stage = value; stageStarted = EditorApplication.timeSinceStartup; stageFrame = Time.frameCount; lastUiReadiness = null; nextUiReadinessLog = 0; }

        static void Require(bool condition, string message)
        {
            Check(condition, message);
            if (!condition) throw new InvalidOperationException(message);
        }

        static void Check(bool condition, string message, IEnumerable<string> details = null)
        {
            if (condition) { passed++; return; }
            failed++;
            string full = message + (details == null ? "" : " | " + string.Join(", ", details.Take(12)));
            failures.Add(full);
            Debug.LogWarning("CHARACTER_CHECK_FAIL " + full);
        }

        static void DeleteTestProfile(string path)
        {
            // Todos los paths son nombres fijos dentro de la carpeta de evidencia del proyecto.
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
                if (File.Exists(path + suffix)) File.Delete(path + suffix);
        }

        static void WriteResult(string scope, string name)
        {
            File.WriteAllText(Path.Combine(EvidenceFolder, name), JsonUtility.ToJson(new Result
            { scope = scope, passed = passed, failed = failed, failures = failures.ToArray(), runtimeErrors = runtimeErrors.ToArray() }, true));
        }
    }
}
