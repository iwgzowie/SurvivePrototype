using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Survive.Combat;
using Survive.Items;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Survive.Editor
{
    /// <summary>Crea presets propios y adapta el jugador sin modificar el paquete de Synty.</summary>
    public static class WeaponSystemSetup
    {
        public const string DefinitionsFolder = "Assets/Data/Weapons";
        public const string PrefabsFolder = "Assets/Prefabs/Items/Weapons";
        const string MaterialsFolder = "Assets/Materials/Items/Weapons";
        const string SourceFolder = "Assets/PolygonApocalypse/Prefabs/Weapons/Guns";
        const string PlayerPath = "Assets/Prefabs/Player/UPPPlayer.prefab";
        const string LegacyRiflePath = "Assets/Prefabs/Items/Weapon_Rifle.prefab";
        const string LevelPath = "Assets/Scenes/Level 1.unity";
        const string SceneRootName = "WeaponSystem_Presets";

        readonly struct Preset
        {
            public readonly string Id, DisplayName, Source;
            public readonly float Damage, Range, Rate, Reload, Spread;
            public readonly bool Automatic;
            public readonly int Magazine, Reserve, Pellets;
            public readonly Vector3 VisualOffset, Muzzle;

            public Preset(string id, string displayName, string source, float damage, float range,
                float rate, bool automatic, int magazine, int reserve, float reload, int pellets,
                float spread, Vector3 muzzle, float gripHeight = .05f)
            {
                Id = id; DisplayName = displayName; Source = source;
                Damage = damage; Range = range; Rate = rate; Automatic = automatic;
                Magazine = magazine; Reserve = reserve; Reload = reload; Pellets = pellets;
                Spread = spread; VisualOffset = Vector3.up * gripHeight; Muzzle = muzzle;
            }
        }

        // +Z es el cañón y +Y la vertical. Las bocas se calibraron con los vértices FBX,
        // expresados en centímetros, y las piezas activas del prefab original.
        static readonly Preset[] Presets =
        {
            new Preset("Pistol", "Pistola", "SM_Wep_Pistol_01", 24, 60, 4, true,
                12, 72, 1.5f, 1, .35f, new Vector3(0, .0455f, .218f), .055f),
            new Preset("Revolver", "Revólver", "SM_Wep_Revolver_01", 48, 80, 2, true,
                6, 36, 2.3f, 1, .25f, new Vector3(0, .0692f, .328f), .055f),
            new Preset("SMG", "Subfusil", "SM_Wep_SubMGun_01", 16, 55, 12, true,
                30, 150, 1.8f, 1, 1.6f, new Vector3(-.0081f, .0511f, .345f)),
            new Preset("AssaultRifle", "Rifle de asalto", "SM_Wep_AssaultRifle_01", 25, 100, 8, true,
                30, 150, 2.2f, 1, .75f, new Vector3(0, .0827f, .728f)),
            new Preset("Shotgun", "Escopeta", "SM_Wep_Shotgun_01", 12, 35, 1.1f, true,
                8, 40, 2.8f, 8, 5, new Vector3(0, .0579f, .532f)),
            new Preset("HuntingRifle", "Rifle de caza", "SM_Wep_HuntingRifle_Clean_01", 75, 160, 1.2f, true,
                5, 30, 2.6f, 1, .15f, new Vector3(.0009f, .0414f, .967f)),
            new Preset("SniperRifle", "Rifle de precisión", "SM_Wep_SniperRifle_01", 110, 240, .8f, true,
                5, 20, 3, 1, .05f, new Vector3(.0002f, .0558f, .736f)),
            new Preset("MachineGun", "Ametralladora", "SM_Wep_MachineGun_01", 22, 120, 10, true,
                60, 180, 3.8f, 1, 1.3f, new Vector3(0, .084f, 1.042f))
        };

        [MenuItem("Survive/Weapons/Crear presets e integrar jugador")]
        public static void CreateAndIntegrate()
        {
            EnsureNotPlaying();
            CreatePresets();
            IntegratePlayer();
            BindLegacyRifle();
            WeaponFeelSetup.CreateAndIntegrate();
            Debug.Log("SURVIVE_WEAPON_SETUP=PASS presets=8 player=integrated level=unchanged");
        }

        /// <summary>La creación es idempotente: conserva valores de presets y prefabs existentes.</summary>
        public static void CreatePresets()
        {
            EnsureNotPlaying();
            Folder(DefinitionsFolder);
            Folder(PrefabsFolder);
            Folder(MaterialsFolder);
            Material tracer = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Items/Weapon_Tracer.mat");
            if (tracer == null) throw new InvalidOperationException("Falta el material canónico Weapon_Tracer.");
            foreach (Preset preset in Presets)
            {
                WeaponDefinition definition = EnsureDefinition(preset);
                EnsurePrefab(preset, definition, tracer);
            }
        }

        static WeaponDefinition EnsureDefinition(Preset preset)
        {
            string path = $"{DefinitionsFolder}/WD_{preset.Id}.asset";
            WeaponDefinition definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
            if (definition != null) return definition;
            definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            definition.name = $"WD_{preset.Id}";
            var data = new SerializedObject(definition);
            Property(data, "displayName").stringValue = preset.DisplayName;
            Property(data, "damage").floatValue = preset.Damage;
            Property(data, "range").floatValue = preset.Range;
            Property(data, "shotsPerSecond").floatValue = preset.Rate;
            Property(data, "automatic").boolValue = preset.Automatic;
            Property(data, "magazineSize").intValue = preset.Magazine;
            Property(data, "startingReserveAmmo").intValue = preset.Reserve;
            Property(data, "reloadDuration").floatValue = preset.Reload;
            Property(data, "pelletsPerShot").intValue = preset.Pellets;
            Property(data, "spreadDegrees").floatValue = preset.Spread;
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(definition, path);
            return definition;
        }

        static void EnsurePrefab(Preset preset, WeaponDefinition definition, Material tracer)
        {
            string path = $"{PrefabsFolder}/Weapon_{preset.Id}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>($"{SourceFolder}/{preset.Source}.prefab");
            if (source == null) throw new InvalidOperationException($"No se encontró el arma Synty: {preset.Source}");
            var root = new GameObject($"Weapon_{preset.Id}");
            try
            {
                WeaponItem item = root.AddComponent<WeaponItem>();
                SphereCollider trigger = root.GetComponent<SphereCollider>();
                trigger.isTrigger = true;
                trigger.radius = .6f;
                Rigidbody body = root.GetComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
                visual.transform.localPosition = preset.VisualOffset;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;
                PrefabUtility.RecordPrefabInstancePropertyModifications(visual.transform);
                foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
                    UnityEngine.Object.DestroyImmediate(collider);
                // La pistola estándar usa el mismo modelo, sin culata ni silenciador opcionales.
                if (preset.Id == "Pistol")
                {
                    foreach (Transform child in visual.GetComponentsInChildren<Transform>(true))
                    {
                        if (!child.name.EndsWith("_Silencer_01", StringComparison.Ordinal) &&
                            !child.name.EndsWith("_Stock_01", StringComparison.Ordinal)) continue;
                        child.gameObject.SetActive(false);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);
                    }
                }
                foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(material => EnsureUrpMaterial(material)).ToArray();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
                Transform muzzle = new GameObject("Muzzle").transform;
                muzzle.SetParent(root.transform, false);
                muzzle.localPosition = preset.VisualOffset + preset.Muzzle;
                GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                flash.name = "MuzzleFlash";
                flash.transform.SetParent(muzzle, false);
                flash.transform.localPosition = new Vector3(0, 0, .035f);
                flash.transform.localScale = new Vector3(.055f, .055f, .1f);
                UnityEngine.Object.DestroyImmediate(flash.GetComponent<Collider>());
                flash.GetComponent<Renderer>().sharedMaterial = tracer;
                flash.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                flash.SetActive(false);
                var data = new SerializedObject(item);
                Property(data, "definition").objectReferenceValue = definition;
                Property(data, "muzzle").objectReferenceValue = muzzle;
                Property(data, "tracerMaterial").objectReferenceValue = tracer;
                Property(data, "muzzleFlash").objectReferenceValue = flash;
                Property(data, "equippedLocalPosition").vector3Value = Vector3.zero;
                Property(data, "equippedLocalEulerAngles").vector3Value = Vector3.zero;
                data.ApplyModifiedPropertiesWithoutUndo();
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                    throw new InvalidOperationException($"No se pudo guardar {path}");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        internal static Material EnsureUrpMaterial(Material original, string destinationFolder = MaterialsFolder)
        {
            if (original == null) return null;
            if (original.shader != null && original.shader.name.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal))
                return original;
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(original));
            string path = $"{destinationFolder}/{original.name}_{guid.Substring(0, 8)}_URP.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("Falta Universal Render Pipeline/Lit.");
            material = new Material(shader) { name = original.name + "_URP" };
            if (original.HasProperty("_MainTex")) material.SetTexture("_BaseMap", original.GetTexture("_MainTex"));
            if (original.HasProperty("_Color")) material.SetColor("_BaseColor", original.GetColor("_Color"));
            if (original.HasProperty("_Metallic")) material.SetFloat("_Metallic", original.GetFloat("_Metallic"));
            if (original.HasProperty("_Glossiness")) material.SetFloat("_Smoothness", original.GetFloat("_Glossiness"));
            if (original.HasProperty("_BumpMap") && original.GetTexture("_BumpMap") != null)
            {
                material.SetTexture("_BumpMap", original.GetTexture("_BumpMap"));
                if (original.HasProperty("_BumpScale")) material.SetFloat("_BumpScale", original.GetFloat("_BumpScale"));
                material.EnableKeyword("_NORMALMAP");
            }
            material.enableInstancing = true;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void BindLegacyRifle()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(LegacyRiflePath);
            try
            {
                WeaponItem item = root.GetComponent<WeaponItem>();
                if (item == null) throw new InvalidOperationException("Weapon_Rifle no tiene WeaponItem.");
                var data = new SerializedObject(item);
                SerializedProperty definition = Property(data, "definition");
                if (definition.objectReferenceValue != null) return;
                definition.objectReferenceValue = AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{DefinitionsFolder}/WD_AssaultRifle.asset");
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, LegacyRiflePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void IntegratePlayer()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPath);
            try
            {
                PlayerWeaponController controller = root.GetComponent<PlayerWeaponController>();
                PlayerHealth health = root.GetComponent<PlayerHealth>();
                if (controller == null || health == null)
                    throw new InvalidOperationException("El jugador canónico necesita su controlador de armas y su salud existentes.");
                if (root.GetComponentInChildren<PlayerWeaponHUD>(true) != null) return;
                Canvas canvas = root.GetComponentsInChildren<Canvas>(true)
                    .FirstOrDefault(c => c.name == "PlayerInteractionHUD" && c.renderMode == RenderMode.ScreenSpaceOverlay);
                if (canvas == null)
                    throw new InvalidOperationException("No se encontró el Canvas gameplay existente PlayerInteractionHUD.");
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
                if (font == null) font = TMP_Settings.defaultFontAsset;
                if (font == null) throw new InvalidOperationException("Falta una fuente TMP para el HUD de armas.");

                var panel = new GameObject("WeaponHUD", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
                panel.layer = canvas.gameObject.layer;
                RectTransform rect = panel.GetComponent<RectTransform>();
                rect.SetParent(canvas.transform, false);
                rect.anchorMin = rect.anchorMax = new Vector2(1, 0);
                rect.pivot = new Vector2(1, 0);
                rect.anchoredPosition = new Vector2(-28, 28);
                rect.sizeDelta = new Vector2(300, 130);
                Image background = panel.GetComponent<Image>();
                background.color = new Color(.025f, .04f, .055f, .82f);
                background.raycastTarget = false;
                CanvasGroup group = panel.GetComponent<CanvasGroup>();
                group.interactable = false;
                group.blocksRaycasts = false;
                TMP_Text weaponName = Text(rect, "WeaponName", "SIN ARMA", new Vector2(16, 92), new Vector2(268, 28), 23, font);
                TMP_Text ammo = Text(rect, "Ammo", "-- / --", new Vector2(16, 47), new Vector2(268, 44), 34, font);
                TMP_Text status = Text(rect, "WeaponStatus", "E: recoger  ·  R: recargar", new Vector2(16, 12), new Vector2(268, 28), 18, font);
                status.color = new Color(.7f, .8f, .86f, 1);
                PlayerWeaponHUD hud = panel.AddComponent<PlayerWeaponHUD>();
                var data = new SerializedObject(hud);
                Property(data, "weaponController").objectReferenceValue = controller;
                Property(data, "playerHealth").objectReferenceValue = health;
                Property(data, "weaponNameText").objectReferenceValue = weaponName;
                Property(data, "ammoText").objectReferenceValue = ammo;
                Property(data, "statusText").objectReferenceValue = status;
                Property(data, "hudCanvasGroup").objectReferenceValue = group;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static TMP_Text Text(RectTransform parent, string name, string initial, Vector2 position,
            Vector2 size, float fontSize, TMP_FontAsset font)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.layer = parent.gameObject.layer;
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = initial;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineRight;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Integración explícita de escena; conserva una escena abierta con cambios sin guardar.</summary>
        [MenuItem("Survive/Weapons/Integrar tres pickups en Level 1")]
        public static void IntegrateLevel1()
        {
            EnsureNotPlaying();
            Scene scene = SceneManager.GetSceneByPath(LevelPath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (!openedHere && scene.isDirty)
                throw new InvalidOperationException("Level 1 tiene cambios sin guardar; la integración no los sobrescribe.");
            if (openedHere) scene = EditorSceneManager.OpenScene(LevelPath, OpenSceneMode.Additive);
            try
            {
                if (scene.GetRootGameObjects().Any(o => o.name == SceneRootName))
                {
                    Debug.Log("SURVIVE_WEAPON_LEVEL=PASS existing=true");
                    return;
                }
                PlayerWeaponController player = scene.GetRootGameObjects()
                    .SelectMany(o => o.GetComponentsInChildren<PlayerWeaponController>(true)).FirstOrDefault();
                if (player == null) throw new InvalidOperationException("Level 1 no contiene un jugador con armas.");
                string[] ids = { "Pistol", "Shotgun", "AssaultRifle" };
                var prefabs = ids.Select(id => AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsFolder}/Weapon_{id}.prefab")).ToArray();
                if (prefabs.Any(p => p == null)) throw new InvalidOperationException("Crear los presets antes de integrar Level 1.");
                Physics.SyncTransforms();
                List<Vector3> positions = FindPickupPositions(scene, player.transform, ids.Length);
                if (positions.Count < ids.Length)
                    throw new InvalidOperationException("No se encontraron tres posiciones libres cerca del jugador; no se modificó Level 1.");
                var container = new GameObject(SceneRootName);
                SceneManager.MoveGameObjectToScene(container, scene);
                try
                {
                    for (int i = 0; i < ids.Length; i++)
                    {
                        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i], scene);
                        instance.transform.SetParent(container.transform, false);
                        instance.name = $"Weapon_{ids[i]}_Pickup";
                        instance.transform.position = positions[i];
                        instance.transform.rotation = player.transform.rotation;
                        PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
                        Debug.Log($"SURVIVE_WEAPON_PICKUP={ids[i]} ground={positions[i]:F3}");
                    }
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("No se pudo guardar Level 1.");
                }
                catch
                {
                    UnityEngine.Object.DestroyImmediate(container);
                    throw;
                }
                Debug.Log("SURVIVE_WEAPON_LEVEL=PASS pickups=3");
            }
            finally { if (openedHere) EditorSceneManager.CloseScene(scene, true); }
        }

        static List<Vector3> FindPickupPositions(Scene scene, Transform player, int count)
        {
            var positions = new List<Vector3>();
            float[] distances = { 2.5f, 3.8f, 1.8f, 5f };
            float[] sides = { -2.2f, 0, 2.2f, -3.5f, 3.5f };
            foreach (float distance in distances)
            foreach (float side in sides)
            {
                Vector3 candidate = player.position + player.forward * distance + player.right * side;
                RaycastHit[] hits = Physics.RaycastAll(candidate + Vector3.up * 2, Vector3.down, 5, ~0, QueryTriggerInteraction.Ignore);
                foreach (RaycastHit hit in hits.OrderBy(h => h.distance))
                {
                    if (!IsEnvironment(hit.collider, scene, player) || hit.normal.y < .7f ||
                        hit.point.y > player.position.y + .55f || hit.point.y < player.position.y - 1.5f) continue;
                    Vector3 point = hit.point + Vector3.up * .025f;
                    if (positions.Any(p => (p - point).sqrMagnitude < 2.25f)) continue;
                    if (Physics.OverlapCapsule(point + Vector3.up * .5f, point + Vector3.up * 1.35f,
                        .35f, ~0, QueryTriggerInteraction.Ignore).Any(c => IsEnvironment(c, scene, player))) continue;
                    Vector3 eye = player.position + Vector3.up;
                    Vector3 direction = point + Vector3.up - eye;
                    if (Physics.RaycastAll(eye, direction.normalized, direction.magnitude, ~0,
                        QueryTriggerInteraction.Ignore).Any(h => IsEnvironment(h.collider, scene, player))) continue;
                    positions.Add(point);
                    if (positions.Count == count) return positions;
                    break;
                }
            }
            return positions;
        }

        static bool IsEnvironment(Collider collider, Scene scene, Transform player) =>
            collider != null && collider.gameObject.scene == scene && !collider.transform.IsChildOf(player) &&
            collider.GetComponentInParent<MasterItem>() == null;

        static SerializedProperty Property(SerializedObject data, string name) => data.FindProperty(name)
            ?? throw new InvalidOperationException($"Falta el campo serializado {name} en {data.targetObject.GetType().Name}.");

        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) throw new InvalidOperationException($"Ruta inválida: {path}");
            Folder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void EnsureNotPlaying()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("El setup se ejecuta fuera de Play Mode.");
        }
    }
}
