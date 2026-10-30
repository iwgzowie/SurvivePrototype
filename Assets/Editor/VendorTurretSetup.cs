using System;
using System.IO;
using System.Linq;
using Survive.Combat;
using Survive.Economy;
using Survive.Items;
using Survive.Turrets;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Survive.Editor
{
    /// <summary>Genera assets propios e integra el vendedor en Level 1 sin editar los originales Synty.</summary>
    public static class VendorTurretSetup
    {
        public const string Root = "Assets/Prefabs/Vendor";
        public const string TurretPath = Root + "/AutoTurret.prefab";
        public const string ItemPath = Root + "/TurretItem.prefab";
        public const string VendorPath = Root + "/NpcVendor.prefab";
        public const string DefinitionPath = "Assets/Data/Vendor/TD_AutoTurret.asset";
        public const string CatalogPath = "Assets/Data/Vendor/VC_Armero.asset";
        public const string PlayerPath = "Assets/Prefabs/Player/UPPPlayer.prefab";
        public const string LevelPath = "Assets/Scenes/Level 1.unity";

        [MenuItem("Survive/Vendedor/Crear e integrar vendedor y torretas")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salir de Play Mode.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Folder(Root); Folder("Assets/Data/Vendor"); Folder("Assets/Materials/Vendor");
            Backup(PlayerPath); Backup(LevelPath);
            Material body = Material("TurretBody", new Color(.08f, .13f, .17f));
            Material accent = Material("TurretAccent", new Color(.15f, .8f, .7f));
            Material tracer = Material("TurretTracer", new Color(1, .75f, .2f), true);
            Material valid = Material("PlacementValid", new Color(.12f, .85f, .45f, .42f), true, true);
            Material invalid = Material("PlacementInvalid", new Color(1, .15f, .12f, .42f), true, true);
            TurretDefinition definition = AssetDatabase.LoadAssetAtPath<TurretDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<TurretDefinition>();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }
            CreateTurret(definition, body, accent, tracer);
            CreateItem(definition, body, accent);
            VendorCatalog catalog = CreateCatalog(definition);
            CreateNpc(catalog, body, accent);
            RepairNpcMaterial();
            IntegratePlayer(definition, valid, invalid);
            IntegrateLevel();
            AssetDatabase.SaveAssets();
            File.WriteAllText("Logs/VendorTurret-Setup.txt", $"PASS\nCatalog={catalog.Count} offers\nTurret={TurretPath}\nPlayer={PlayerPath}\nScene={LevelPath}\n");
            Debug.Log("SURVIVE_VENDOR_TURRET_SETUP=PASS offers=" + catalog.Count);
        }

        public static void CreateTurretIcon()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            const string path = "Assets/UI/Vendor/Icon_AutoTurret.png";
            Folder("Assets/UI/Vendor");
            Scene scene = EditorSceneManager.NewPreviewScene();
            RenderTexture texture = null;
            RenderTexture previous = RenderTexture.active;
            Texture2D photo = null;
            try
            {
                var stage = new GameObject("TurretPhoto"); SceneManager.MoveGameObjectToScene(stage, scene);
                var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(TurretPath), stage.transform);
                model.GetComponent<AutoTurret>().enabled = false;
                Camera camera = new GameObject("Camera").AddComponent<Camera>(); camera.transform.SetParent(stage.transform);
                camera.scene = scene; camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
                camera.orthographic = true; camera.orthographicSize = 1.05f; camera.nearClipPlane = .01f; camera.farClipPlane = 30;
                camera.transform.position = new Vector3(2, 1.8f, 3); camera.transform.LookAt(new Vector3(0, .65f, 0));
                Light light = new GameObject("Light").AddComponent<Light>(); light.transform.SetParent(stage.transform);
                light.type = LightType.Directional; light.intensity = 2; light.transform.rotation = Quaternion.Euler(35, -25, 0);
                texture = new RenderTexture(256, 256, 24); texture.Create(); camera.targetTexture = texture;
                camera.Render(); RenderTexture.active = texture;
                photo = new Texture2D(256, 256, TextureFormat.RGBA32, false);
                photo.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); photo.Apply();
                File.WriteAllBytes(path, photo.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (texture != null) { texture.Release(); Object.DestroyImmediate(texture); }
                if (photo != null) Object.DestroyImmediate(photo);
                EditorSceneManager.ClosePreviewScene(scene);
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false; importer.SaveAndReimport();
            Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (icon == null) throw new InvalidOperationException("No se importó el icono de torreta.");
            Set(AssetDatabase.LoadAssetAtPath<TurretDefinition>(DefinitionPath), "icon", icon);
            AssetDatabase.SaveAssets();
        }

        private static void CreateTurret(TurretDefinition definition, Material body, Material accent, Material tracer)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(TurretPath) != null) return;
            var root = new GameObject("AutoTurret");
            try
            {
                var turret = root.AddComponent<AutoTurret>();
                var box = root.AddComponent<BoxCollider>();
                box.center = new Vector3(0, .65f, 0); box.size = new Vector3(1.1f, 1.3f, 1.1f);
                var obstacle = root.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Capsule;
                obstacle.center = new Vector3(0, .7f, 0); obstacle.radius = .65f; obstacle.height = 1.4f;
                obstacle.carving = true;
                Part(root.transform, "Base", new Vector3(0, .12f, 0), new Vector3(1.1f, .24f, 1.1f), body);
                Part(root.transform, "Column", new Vector3(0, .52f, 0), new Vector3(.22f, .8f, .22f), body);
                Part(root.transform, "Battery", new Vector3(0, .37f, -.3f), new Vector3(.5f, .35f, .3f), accent);
                Transform head = new GameObject("RotatingHead").transform;
                head.SetParent(root.transform, false); head.localPosition = new Vector3(0, .95f, -.2f);
                Part(head, "Receiver", Vector3.zero, new Vector3(.42f, .24f, .45f), body);
                WeaponItem weapon = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Items/Weapons/Weapon_MachineGun.prefab")?.GetComponent<WeaponItem>();
                if (weapon == null) throw new InvalidOperationException("Falta Weapon_MachineGun.");
                Transform visual = weapon.transform.Find("SyntyVisual");
                if (visual == null) visual = weapon.transform.Cast<Transform>()
                    .FirstOrDefault(child => child.name.StartsWith("SM_Wep_", StringComparison.Ordinal));
                if (visual == null) throw new InvalidOperationException("Falta SyntyVisual de la ametralladora.");
                GameObject model = Object.Instantiate(visual.gameObject, head, false);
                model.name = "MachineGunVisual"; model.transform.localScale *= .65f;
                foreach (Collider collider in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
                Transform muzzle = new GameObject("Muzzle").transform;
                muzzle.SetParent(head, false);
                muzzle.localPosition = weapon.transform.InverseTransformPoint(weapon.Muzzle.position) * .65f;
                LineRenderer line = new GameObject("Tracer").AddComponent<LineRenderer>();
                line.transform.SetParent(root.transform, false); line.positionCount = 2;
                line.useWorldSpace = true; line.widthMultiplier = .025f; line.sharedMaterial = tracer;
                line.enabled = false; line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                AudioSource audio = root.AddComponent<AudioSource>();
                audio.playOnAwake = false; audio.spatialBlend = 1; audio.minDistance = 2; audio.maxDistance = 35;
                Set(turret, "definition", definition, "head", head, "muzzle", muzzle, "tracer", line,
                    "shotAudio", audio, "presentation", weapon.Definition);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, TurretPath);
                Set(definition, "deployedPrefab", prefab.GetComponent<AutoTurret>());
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void CreateItem(TurretDefinition definition, Material body, Material accent)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ItemPath) != null) return;
            var root = new GameObject("TurretItem");
            try
            {
                TurretItem item = root.AddComponent<TurretItem>();
                root.GetComponent<SphereCollider>().isTrigger = true;
                root.GetComponent<SphereCollider>().radius = .6f;
                root.GetComponent<Rigidbody>().isKinematic = true;
                root.GetComponent<Rigidbody>().useGravity = false;
                Part(root.transform, "KitCase", Vector3.zero, new Vector3(.55f, .3f, .4f), body);
                Part(root.transform, "KitMark", new Vector3(0, .16f, 0), new Vector3(.4f, .035f, .12f), accent);
                Set(item, "definition", definition);
                PrefabUtility.SaveAsPrefabAsset(root, ItemPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static VendorCatalog CreateCatalog(TurretDefinition definition)
        {
            VendorCatalog existing = AssetDatabase.LoadAssetAtPath<VendorCatalog>(CatalogPath);
            if (existing != null) return existing;
            string[] ids = { "Pistol", "Revolver", "SMG", "AssaultRifle", "Shotgun", "HuntingRifle", "SniperRifle", "MachineGun" };
            int[] prices = { 50, 90, 120, 160, 140, 180, 240, 220 };
            var catalog = ScriptableObject.CreateInstance<VendorCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            var data = new SerializedObject(catalog);
            SerializedProperty offers = data.FindProperty("offers"); offers.arraySize = ids.Length + 1;
            for (int i = 0; i < ids.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Items/Weapons/Weapon_{ids[i]}.prefab");
                if (prefab == null) throw new InvalidOperationException("Falta el arma " + ids[i]);
                SerializedProperty offer = offers.GetArrayElementAtIndex(i);
                offer.FindPropertyRelative("weaponPrefab").objectReferenceValue = prefab.GetComponent<WeaponItem>();
                offer.FindPropertyRelative("turret").objectReferenceValue = null;
                offer.FindPropertyRelative("price").intValue = prices[i];
            }
            SerializedProperty last = offers.GetArrayElementAtIndex(ids.Length);
            last.FindPropertyRelative("weaponPrefab").objectReferenceValue = null;
            last.FindPropertyRelative("turret").objectReferenceValue = definition;
            last.FindPropertyRelative("price").intValue = 150;
            data.ApplyModifiedPropertiesWithoutUndo();
            return catalog;
        }

        private static void CreateNpc(VendorCatalog catalog, Material body, Material accent)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(VendorPath) != null) return;
            var root = new GameObject("NpcVendor_Armero");
            try
            {
                var vendor = root.AddComponent<NpcVendor>(); Set(vendor, "catalog", catalog);
                var collider = root.AddComponent<CapsuleCollider>(); collider.center = Vector3.up; collider.height = 2; collider.radius = .4f;
                var obstacle = root.AddComponent<NavMeshObstacle>(); obstacle.center = Vector3.up; obstacle.radius = .45f;
                obstacle.height = 2; obstacle.carving = true;
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolygonApocalypse/Prefabs/Characters/SM_Chr_Soldier_Male_01.prefab");
                if (source == null) throw new InvalidOperationException("Falta el modelo del armero.");
                GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(source);
                visual.transform.SetParent(root.transform, false);
                visual.name = "SyntySoldier";
                ApplyNpcMaterials(visual);
                Animator animator = visual.GetComponentInChildren<Animator>();
                string animatorPath = "Assets/Data/Vendor/VendorIdle.controller";
                AnimatorController idle = AssetDatabase.LoadAssetAtPath<AnimatorController>(animatorPath);
                if (idle == null)
                {
                    idle = AnimatorController.CreateAnimatorControllerAtPath(animatorPath);
                    AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath("Assets/Animations/Player/Clips/Idle/Neutral Idle.fbx")
                        .OfType<AnimationClip>().FirstOrDefault(candidate => !candidate.name.StartsWith("__"));
                    if (clip == null) throw new InvalidOperationException("Falta la animación idle.");
                    AnimatorState state = idle.layers[0].stateMachine.AddState("VendorIdle"); state.motion = clip;
                    idle.layers[0].stateMachine.defaultState = state;
                }
                if (animator != null) { animator.runtimeAnimatorController = idle; animator.applyRootMotion = false; }
                Part(root.transform, "SupplyCrate", new Vector3(.85f, .25f, 0), new Vector3(.65f, .5f, .6f), body);
                Part(root.transform, "SupplyStripe", new Vector3(.85f, .51f, 0), new Vector3(.68f, .035f, .15f), accent);
                var label = new GameObject("VendorName").AddComponent<TextMeshPro>();
                label.transform.SetParent(root.transform, false); label.transform.localPosition = new Vector3(0, 2.35f, 0);
                label.rectTransform.sizeDelta = new Vector2(3, .7f); label.fontSize = 3;
                label.alignment = TextAlignmentOptions.Center; label.text = "ARMERO\n<size=70%>[E] Armas y torretas</size>";
                label.color = new Color(.35f, 1, .85f); label.gameObject.AddComponent<VendorNameBillboard>();
                PrefabUtility.SaveAsPrefabAsset(root, VendorPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [MenuItem("Survive/Vendedor/Corregir material URP del NPC")]
        public static void RepairNpcMaterial()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salir de Play Mode.");
            Folder("Assets/Materials/Vendor");
            string backup = ".utmp/VendorMaterial-Backup/NpcVendor.prefab";
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            if (!File.Exists(backup)) File.Copy(VendorPath, backup);
            GameObject root = PrefabUtility.LoadPrefabContents(VendorPath);
            int rendererCount = 0;
            try
            {
                Transform visual = root.transform.Find("SyntySoldier");
                if (visual == null) throw new InvalidOperationException("Falta el modelo del vendedor.");
                ApplyNpcMaterials(visual.gameObject);
                foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material == null || material.shader == null
                            || material.shader.name != "Universal Render Pipeline/Lit"
                            || material.GetTexture("_BaseMap") == null)
                            throw new InvalidOperationException("Material o textura URP ausente en " + renderer.name);
                        rendererCount++;
                    }
                if (root.GetComponent<NpcVendor>() == null || root.GetComponent<CapsuleCollider>() == null
                    || root.GetComponentInChildren<Animator>().runtimeAnimatorController == null)
                    throw new InvalidOperationException("Faltan componentes del vendedor.");
                PrefabUtility.SaveAsPrefabAsset(root, VendorPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("SURVIVE_VENDOR_MATERIAL=PASS slots=" + rendererCount + " shader=URP/Lit baseMap=present");
        }

        private static void ApplyNpcMaterials(GameObject visual)
        {
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int index = 0; index < materials.Length; index++)
                    materials[index] = WeaponSystemSetup.EnsureUrpMaterial(materials[index], "Assets/Materials/Vendor");
                renderer.sharedMaterials = materials;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
        }

        public static void RepairNpcAndPreview()
        {
            RepairNpcMaterial();
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                throw new InvalidOperationException("Se necesita GPU para comprobar el render del vendedor.");
            Scene scene = EditorSceneManager.NewPreviewScene();
            RenderTexture texture = null;
            RenderTexture previous = RenderTexture.active;
            Texture2D photo = null;
            try
            {
                var stage = new GameObject("VendorMaterialPreview"); SceneManager.MoveGameObjectToScene(stage, scene);
                GameObject model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(VendorPath), stage.transform);
                model.transform.Find("VendorName").gameObject.SetActive(false);
                Animator animator = model.GetComponentInChildren<Animator>();
                animator.Rebind(); animator.Update(.4f);
                Camera camera = new GameObject("Camera").AddComponent<Camera>(); camera.transform.SetParent(stage.transform);
                camera.scene = scene; camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.09f, .11f, .13f);
                camera.orthographic = true; camera.orthographicSize = 1.3f; camera.nearClipPlane = .01f; camera.farClipPlane = 30;
                camera.transform.position = new Vector3(2.8f, 1.7f, 5); camera.transform.LookAt(new Vector3(.2f, 1, 0));
                Light key = new GameObject("Key").AddComponent<Light>(); key.transform.SetParent(stage.transform);
                key.type = LightType.Directional; key.intensity = 1.6f; key.transform.rotation = Quaternion.Euler(35, -25, 0);
                key.color = Color.white;
                Light fill = new GameObject("Fill").AddComponent<Light>(); fill.transform.SetParent(stage.transform);
                fill.type = LightType.Directional; fill.intensity = .5f; fill.transform.rotation = Quaternion.Euler(20, 140, 0);
                texture = new RenderTexture(768, 768, 24); texture.Create(); camera.targetTexture = texture;
                camera.Render(); RenderTexture.active = texture;
                photo = new Texture2D(768, 768, TextureFormat.RGB24, false);
                photo.ReadPixels(new Rect(0, 0, 768, 768), 0, 0); photo.Apply();
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes("Logs/VendorNpc-Material.png", photo.EncodeToPNG());
                Debug.Log("SURVIVE_VENDOR_MATERIAL_RENDER=PASS path=Logs/VendorNpc-Material.png");
            }
            finally
            {
                RenderTexture.active = previous;
                if (texture != null) { texture.Release(); Object.DestroyImmediate(texture); }
                if (photo != null) Object.DestroyImmediate(photo);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void IntegratePlayer(TurretDefinition definition, Material valid, Material invalid)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPath);
            try
            {
                if (root.GetComponent<PlayerWallet>() == null) root.AddComponent<PlayerWallet>();
                PlayerTurretController builder = root.GetComponent<PlayerTurretController>() ?? root.AddComponent<PlayerTurretController>();
                Set(builder, "definition", definition, "validPreviewMaterial", valid, "invalidPreviewMaterial", invalid);
                if (root.GetComponent<PlayerVendorController>() == null) root.AddComponent<PlayerVendorController>();
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void IntegrateLevel()
        {
            Scene scene = EditorSceneManager.OpenScene(LevelPath, OpenSceneMode.Single);
            if (Object.FindObjectsByType<NpcVendor>(FindObjectsSortMode.None).Any(npc => npc.gameObject.scene == scene)) return;
            PlayerWeaponController player = Object.FindObjectsByType<PlayerWeaponController>(FindObjectsSortMode.None).FirstOrDefault(p => p.gameObject.scene == scene);
            if (player == null) throw new InvalidOperationException("Falta el jugador de Level 1.");
            Physics.SyncTransforms();
            Vector3 position = default;
            bool found = false;
            // Elegir un lugar despejado cerca del spawn, accesible desde el comienzo.
            for (int radius = 2; radius <= 6 && !found; radius++)
                for (int index = 0; index < 16 && !found; index++)
                {
                    float angle = index * Mathf.PI / 8;
                    Vector3 point = player.transform.position + new Vector3(Mathf.Sin(angle) * radius, 3, Mathf.Cos(angle) * radius);
                    if (!Physics.Raycast(point, Vector3.down, out RaycastHit ground, 7, ~0, QueryTriggerInteraction.Ignore)
                        || Vector3.Angle(ground.normal, Vector3.up) > 15
                        || Mathf.Abs(ground.point.y - player.transform.position.y) > 1.5f) continue;
                    if (Physics.CheckCapsule(ground.point + Vector3.up * .45f, ground.point + Vector3.up * 1.7f,
                            .42f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    position = ground.point; found = true;
                }
            if (!found) throw new InvalidOperationException("No se encontró un suelo libre cerca del spawn para el armero.");
            GameObject npc = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(VendorPath), scene);
            npc.transform.position = position;
            Vector3 look = player.transform.position - position; look.y = 0;
            npc.transform.rotation = Quaternion.LookRotation(look);
            PrefabUtility.RecordPrefabInstancePropertyModifications(npc.transform);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"VENDOR_SPAWN player={player.transform.position} vendor={position}");
        }

        public static void Set(Object target, params object[] values)
        {
            var data = new SerializedObject(target);
            for (int i = 0; i < values.Length; i += 2)
            {
                SerializedProperty property = data.FindProperty((string)values[i]);
                object value = values[i + 1];
                if (value is int number) property.intValue = number;
                else if (value is float scalar) property.floatValue = scalar;
                else if (value is bool flag) property.boolValue = flag;
                else property.objectReferenceValue = value as Object;
            }
            data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target);
        }

        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'); Folder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static Material Material(string name, Color color, bool unlit = false, bool transparent = false)
        {
            string path = "Assets/Materials/Vendor/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            if (transparent)
            {
                material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0); material.SetOverrideTag("RenderType", "Transparent");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = 3000;
            }
            AssetDatabase.CreateAsset(material, path); return material;
        }

        private static void Part(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube); part.name = name;
            part.transform.SetParent(parent, false); part.transform.localPosition = position; part.transform.localScale = scale;
            Object.DestroyImmediate(part.GetComponent<Collider>()); part.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void Backup(string path)
        {
            string destination = ".utmp/VendorTurrets-Backup/" + path;
            if (File.Exists(destination)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.Copy(path, destination);
        }
    }
}
