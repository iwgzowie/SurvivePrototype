using System;
using System.IO;
using System.Linq;
using Survive.Items;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Survive.Editor
{
    /// <summary>Importa el modelo suministrado y reemplaza sólo el visual del pickup existente.</summary>
    public static class MedicalBandageModelSetup
    {
        public const string ModelPath = "Assets/Models/Consumables/MedicalBandage/MedicalBandage.fbx";
        const string Textures = "Assets/Models/Consumables/MedicalBandage/Textures/";
        const string MaterialPath = "Assets/Materials/Consumables/MedicalBandage.mat";
        const float WorldSize = .42f;

        public static void PrepareAssets()
        {
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.SaveAndReimport();
            ConfigureTexture("Color.jpg", true, false, false);
            ConfigureTexture("Normal.png", false, true, false);
            ConfigureTexture("Metallic.jpg", false, false, true);
            ConfigureTexture("Roughness.jpg", false, false, true);
            Texture2D metallic = Texture("Metallic.jpg"), roughness = Texture("Roughness.jpg");
            if (metallic.width != roughness.width || metallic.height != roughness.height) throw new InvalidOperationException("Los mapas PBR tienen tamaños diferentes.");
            Color32[] metalPixels = metallic.GetPixels32(), roughPixels = roughness.GetPixels32();
            for (int i = 0; i < metalPixels.Length; i++) metalPixels[i] = new Color32(metalPixels[i].r, 0, 0, (byte)(255 - roughPixels[i].r));
            var packed = new Texture2D(metallic.width, metallic.height, TextureFormat.RGBA32, false, true);
            try { packed.SetPixels32(metalPixels); packed.Apply(); File.WriteAllBytes(Textures + "MetallicSmoothness.png", packed.EncodeToPNG()); }
            finally { Object.DestroyImmediate(packed); }
            AssetDatabase.ImportAsset(Textures + "MetallicSmoothness.png", ImportAssetOptions.ForceSynchronousImport);
            ConfigureTexture("MetallicSmoothness.png", false, false, false);
            ConfigureTexture("Metallic.jpg", false, false, false);
            ConfigureTexture("Roughness.jpg", false, false, false);

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, MaterialPath); }
            material.shader = Shader.Find("Universal Render Pipeline/Lit"); material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", Texture("Color.jpg")); material.SetTexture("_BumpMap", Texture("Normal.png"));
            material.SetTexture("_MetallicGlossMap", Texture("MetallicSmoothness.png"));
            material.SetFloat("_BumpScale", 1); material.SetFloat("_Metallic", 1); material.SetFloat("_Smoothness", 1);
            material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP"); EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            foreach (Material source in AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Material>())
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source.name), material);
            importer.SaveAndReimport();
        }

        public static void InstallVisual(GameObject root)
        {
            while (root.transform.childCount > 0) Object.DestroyImmediate(root.transform.GetChild(0).gameObject);
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (asset == null) throw new InvalidOperationException("Falta el FBX de medical bandage.");
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(asset, root.transform); visual.name = "MedicalBandageModel";
            visual.transform.localPosition = Vector3.zero;
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach (Rigidbody body in visual.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0 || material == null) throw new InvalidOperationException("El modelo no tiene geometría o material.");
            foreach (Renderer renderer in renderers) renderer.sharedMaterials = Enumerable.Repeat(material, Mathf.Max(1, renderer.sharedMaterials.Length)).ToArray();
            Bounds bounds = BoundsOf(visual);
            float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (largest < .00001f) throw new InvalidOperationException("El modelo tiene escala inválida.");
            visual.transform.localScale *= WorldSize / largest;
            bounds = BoundsOf(visual);
            visual.transform.localPosition -= root.transform.InverseTransformPoint(bounds.center);
        }

        [MenuItem("Survive/Gameplay/Reemplazar vendas por Medical Bandage")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salí de Play Mode.");
            HealingDefinition definition = AssetDatabase.LoadAssetAtPath<HealingDefinition>(HealingExplosiveSetup.HealingPath);
            string beforeGuid; long beforeId;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(definition.worldPrefab, out beforeGuid, out beforeId);
            PrepareAssets();
            GameObject root = PrefabUtility.LoadPrefabContents(HealingExplosiveSetup.PickupPath);
            try { InstallVisual(root); PrefabUtility.SaveAsPrefabAsset(root, HealingExplosiveSetup.PickupPath); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            foreach (string name in new[] { "BandageFabric", "BandageCore", "BandageCross" })
                AssetDatabase.DeleteAsset("Assets/Materials/Consumables/" + name + ".mat");
            AssetDatabase.SaveAssets();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HealingExplosiveSetup.PickupPath);
            string afterGuid; long afterId;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefab.GetComponent<HealingPickup>(), out afterGuid, out afterId);
            if (beforeGuid != afterGuid || beforeId != afterId || definition.worldPrefab != prefab.GetComponent<HealingPickup>())
                throw new InvalidOperationException("Se alteró la referencia original del pickup.");
            if (prefab.transform.childCount != 1 || prefab.transform.GetChild(0).name != "MedicalBandageModel") throw new InvalidOperationException("Quedó geometría anterior.");
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>())
                foreach (Material material in renderer.sharedMaterials)
                    if (material == null || material.shader.name != "Universal Render Pipeline/Lit" || !material.shader.isSupported
                        || material.GetTexture("_BaseMap") == null || material.GetTexture("_BumpMap") == null || material.GetTexture("_MetallicGlossMap") == null)
                        throw new InvalidOperationException("El material PBR está incompleto.");
            Directory.CreateDirectory("Logs/MedicalBandageModel");
            File.WriteAllText("Logs/MedicalBandageModel/integration.txt", "PASS: FBX importado y centrado a 0.42 m; material URP con color, normal y metallic/smoothness.\nPASS: geometría anterior y sus tres materiales eliminados.\nPASS: GUID y fileID del pickup conservados: " + afterGuid + " / " + afterId + ".\n");
            Debug.Log("MEDICAL_BANDAGE_MODEL_PASS");
        }

        public static void RunAndValidate() { Run(); HealingExplosiveValidation.Run(); }
        static Texture2D Texture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + name) ?? throw new InvalidOperationException("Falta textura " + name);
        static void ConfigureTexture(string name, bool srgb, bool normal, bool readable)
        {
            string path = Textures + name; AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb; importer.isReadable = readable; importer.maxTextureSize = 2048;
            importer.textureCompression = readable ? TextureImporterCompression.Uncompressed : TextureImporterCompression.Compressed;
            if (name == "MetallicSmoothness.png") importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.SaveAndReimport();
        }
        static Bounds BoundsOf(GameObject model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true); Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds); return bounds;
        }
    }
}
