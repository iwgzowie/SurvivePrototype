using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Survive.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Survive.Editor
{
    /// <summary>Fotografía los modelos reales del proyecto en una escena de preview aislada.</summary>
    public static class WeaponIconSetup
    {
        public const string IconsFolder = "Assets/UI/WeaponIcons";
        public const int Width = 512;
        public const int Height = 320;
        public const string PreviewArgument = "-surviveWeaponIconPreview";
        public static readonly string[] WeaponIds =
        {
            "Pistol", "Revolver", "SMG", "AssaultRifle", "Shotgun", "HuntingRifle", "SniperRifle", "MachineGun"
        };
        private const float Padding = .10f;

        public static string IconPath(string id) => $"{IconsFolder}/Icon_{id}.png";

        [MenuItem("Survive/Weapons/Generar fotos reales de las armas")]
        public static void CreateAndIntegrate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Salir de Play Mode antes de fotografiar las armas.");
            EnsureFolder(IconsFolder);
            string previewPath = CommandLineValue(PreviewArgument);
            Texture2D contact = !string.IsNullOrWhiteSpace(previewPath)
                ? new Texture2D(Width * 4, Height * 2, TextureFormat.RGBA32, false) : null;
            try
            {
                for (int index = 0; index < WeaponIds.Length; index++)
                {
                    string id = WeaponIds[index];
                    Texture2D photo = RenderWeapon(id);
                    try
                    {
                        File.WriteAllBytes(IconPath(id), photo.EncodeToPNG());
                        if (contact != null)
                            contact.SetPixels(index % 4 * Width, (1 - index / 4) * Height,
                                Width, Height, photo.GetPixels());
                    }
                    finally { Object.DestroyImmediate(photo); }
                    AssetDatabase.ImportAsset(IconPath(id), ImportAssetOptions.ForceSynchronousImport);
                    ConfigureImporter(IconPath(id));
                    BindDefinition(id, AssetDatabase.LoadAssetAtPath<Sprite>(IconPath(id)));
                }
                if (contact != null)
                {
                    contact.Apply(false, false);
                    string directory = Path.GetDirectoryName(Path.GetFullPath(previewPath));
                    Directory.CreateDirectory(directory);
                    File.WriteAllBytes(previewPath, contact.EncodeToPNG());
                }
                AssetDatabase.SaveAssets();
                Debug.Log($"SURVIVE_WEAPON_ICONS=PASS count={WeaponIds.Length} size={Width}x{Height} folder={IconsFolder}");
            }
            finally { if (contact != null) Object.DestroyImmediate(contact); }
        }

        private static Texture2D RenderWeapon(string id)
        {
            string prefabPath = $"{WeaponSystemSetup.PrefabsFolder}/Weapon_{id}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) throw new InvalidOperationException("Falta el prefab " + prefabPath);
            Transform visual = prefab.transform.Find("SyntyVisual");
            if (visual == null)
                visual = prefab.transform.Cast<Transform>().FirstOrDefault(child => child.name.StartsWith("SM_Wep_", StringComparison.Ordinal));
            if (visual == null) throw new InvalidOperationException("No se encontró la jerarquía visual Synty de " + id);

            Scene preview = EditorSceneManager.NewPreviewScene();
            RenderTexture target = null;
            RenderTexture previousTarget = RenderTexture.active;
            Texture2D photo = null;
            try
            {
                GameObject stage = PreviewObject("WeaponIconStage", preview);
                GameObject model = Object.Instantiate(visual.gameObject, stage.transform, false);
                model.name = "ActualSyntyModel_" + id;
                model.transform.localPosition = Vector3.zero;
                model.SetActive(true);
                PrepareVisual(model);
                Renderer[] renderers = model.GetComponentsInChildren<MeshRenderer>(false)
                    .Where(renderer => renderer.enabled && renderer.GetComponent<MeshFilter>()?.sharedMesh != null).Cast<Renderer>().ToArray();
                if (renderers.Length == 0) throw new InvalidOperationException("El modelo no contiene mallas visibles: " + id);

                GameObject cameraObject = PreviewObject("WeaponIconCamera", preview);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.scene = preview;
                camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(preview);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.orthographic = true;
                camera.aspect = (float)Width / Height;
                camera.nearClipPlane = .01f;
                camera.farClipPlane = 50f;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.useOcclusionCulling = false;
                UniversalAdditionalCameraData additional = cameraObject.AddComponent<UniversalAdditionalCameraData>();
                additional.renderPostProcessing = false;
                additional.antialiasing = AntialiasingMode.None;
                additional.renderShadows = false;
                additional.requiresColorOption = CameraOverrideOption.Off;
                additional.requiresDepthOption = CameraOverrideOption.Off;
                additional.volumeLayerMask = 0;
                additional.allowHDROutput = false;

                AddLight(preview, "Key", new Vector3(3f, 4f, -2f), 1.8f, new Color(1f, .97f, .93f));
                AddLight(preview, "Fill", new Vector3(4f, 1f, 4f), .7f, new Color(.85f, .91f, 1f));
                AddLight(preview, "Rim", new Vector3(-3f, 3f, -2f), 1.2f, Color.white);
                FrameCamera(camera, renderers);
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                {
                    name = "WeaponIconRGBA", hideFlags = HideFlags.HideAndDontSave,
                    antiAliasing = 1, filterMode = FilterMode.Bilinear
                };
                target.Create();
                camera.targetTexture = target;
                photo = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false);
                ReadFrame(camera, target, photo);
                RectInt alpha = AlphaBounds(photo, out float coverage);
                if (alpha.width == 0 || coverage > .7f)
                    throw new InvalidOperationException($"Preview inválida de {id}: alpha coverage={coverage:F4}.");

                // Reencuadrar la cámara desde la silueta real evita que los bounds
                // de accesorios vacíos hagan diminutas las pistolas en el HUD.
                float worldHeight = camera.orthographicSize * 2f;
                camera.transform.position += camera.transform.right * ((alpha.x + alpha.width * .5f - Width * .5f) / Width * worldHeight * camera.aspect)
                    + camera.transform.up * ((alpha.y + alpha.height * .5f - Height * .5f) / Height * worldHeight);
                camera.orthographicSize *= Mathf.Max(alpha.width / (Width * (1f - Padding * 2f)),
                    alpha.height / (Height * (1f - Padding * 2f)));
                ReadFrame(camera, target, photo);
                alpha = AlphaBounds(photo, out coverage);
                if (alpha.x < 2 || alpha.y < 2 || alpha.xMax >= Width - 1 || alpha.yMax >= Height - 1 || coverage < .005f)
                    throw new InvalidOperationException($"Encuadre inválido de {id}: alphaBounds={alpha} coverage={coverage:F4}.");
                Debug.Log($"SURVIVE_WEAPON_ICON id={id} source={prefabPath}/{visual.name} meshes={renderers.Length} alphaBounds={alpha} coverage={coverage:F4}");
                Texture2D result = photo;
                photo = null;
                return result;
            }
            finally
            {
                RenderTexture.active = previousTarget;
                if (photo != null) Object.DestroyImmediate(photo);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static void PrepareVisual(GameObject model)
        {
            foreach (MonoBehaviour script in model.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(script);
            foreach (Animator animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (Rigidbody body in model.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.detectCollisions = false; }
            foreach (ParticleSystem particle in model.GetComponentsInChildren<ParticleSystem>(true)) particle.gameObject.SetActive(false);
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is MeshRenderer) || renderer.name == "MuzzleFlash" || renderer.name.StartsWith("ShotTracer", StringComparison.Ordinal)) renderer.enabled = false;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            foreach (LODGroup group in model.GetComponentsInChildren<LODGroup>(true))
            {
                LOD[] levels = group.GetLODs();
                var first = levels.Length > 0 ? new HashSet<Renderer>(levels[0].renderers) : new HashSet<Renderer>();
                group.enabled = false;
                for (int index = 1; index < levels.Length; index++)
                    foreach (Renderer renderer in levels[index].renderers)
                        if (renderer != null && !first.Contains(renderer)) renderer.enabled = false;
            }
        }

        private static void FrameCamera(Camera camera, Renderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            Vector3 forward = -new Vector3(4f, 1.5f, 1f).normalized;
            Vector3 right = Vector3.ProjectOnPlane(Vector3.forward, forward).normalized;
            camera.transform.SetPositionAndRotation(bounds.center - forward * 5f,
                Quaternion.LookRotation(forward, Vector3.Cross(forward, right)));
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (Renderer renderer in renderers)
                for (int corner = 0; corner < 8; corner++)
                {
                    Bounds mesh = renderer.bounds;
                    Vector3 point = mesh.center + Vector3.Scale(mesh.extents,
                        new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                    Vector3 projected = camera.transform.InverseTransformPoint(point);
                    minimum = Vector2.Min(minimum, projected);
                    maximum = Vector2.Max(maximum, projected);
                }
            Vector2 center = (minimum + maximum) * .5f;
            camera.transform.position += camera.transform.right * center.x + camera.transform.up * center.y;
            camera.orthographicSize = Mathf.Max((maximum.y - minimum.y) * .5f,
                (maximum.x - minimum.x) * .5f / camera.aspect) / (1f - Padding * 2f);
        }

        private static void ReadFrame(Camera camera, RenderTexture target, Texture2D photo)
        {
            camera.Render();
            RenderTexture.active = target;
            photo.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
            photo.Apply(false, false);
        }

        private static RectInt AlphaBounds(Texture2D photo, out float coverage)
        {
            Color32[] pixels = photo.GetPixels32();
            int minX = Width, minY = Height, maxX = -1, maxY = -1, count = 0;
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    if (pixels[y * Width + x].a < 16) continue;
                    minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y); count++;
                }
            coverage = (float)count / pixels.Length;
            return count == 0 ? new RectInt() : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private static GameObject PreviewObject(string name, Scene scene)
        {
            var item = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(item, scene);
            return item;
        }

        private static void AddLight(Scene scene, string name, Vector3 from, float intensity, Color color)
        {
            Light light = PreviewObject("IconLight_" + name, scene).AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            light.transform.rotation = Quaternion.LookRotation(-from.normalized);
        }

        private static void ConfigureImporter(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 512;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 100f;
            importer.spritePivot = new Vector2(.5f, .5f);
            importer.SaveAndReimport();
        }

        private static void BindDefinition(string id, Sprite icon)
        {
            if (icon == null) throw new InvalidOperationException("No se importó el sprite " + id);
            WeaponDefinition definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{WeaponSystemSetup.DefinitionsFolder}/WD_{id}.asset");
            if (definition == null) throw new InvalidOperationException("Falta la definición " + id);
            var data = new SerializedObject(definition);
            SerializedProperty property = data.FindProperty("icon");
            if (property == null) throw new InvalidOperationException("WeaponDefinition necesita el campo icon.");
            string existing = AssetDatabase.GetAssetPath(property.objectReferenceValue);
            if (property.objectReferenceValue != null && !existing.StartsWith(IconsFolder + "/", StringComparison.Ordinal)) return;
            property.objectReferenceValue = icon;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private static string CommandLineValue(string flag)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index + 1 < arguments.Length; index++)
                if (arguments[index] == flag) return arguments[index + 1];
            return null;
        }
    }
}
