using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Survive.Character;
using UnityEditor;
using UnityEngine;

public static class SidekickCharacterSetup
{
    private const string SidekickRoot = "Assets/Synty/SidekickCharacters";
    private const string CatalogPath = "Assets/Resources/Character/SidekickCatalog.asset";
    private const string PreviewPath = "Assets/Prefabs/Character/SidekickPreviewRig.prefab";
    private const string PlayerPath = "Assets/Prefabs/Player/UPPPlayer.prefab";
    private static readonly string[] Slots =
    {
        "", "Head", "Hair", "EyebrowLeft", "EyebrowRight", "EyeLeft", "EyeRight", "EarLeft", "EarRight",
        "FacialHair", "Torso", "ArmUpperLeft", "ArmUpperRight", "ArmLowerLeft", "ArmLowerRight", "HandLeft",
        "HandRight", "Hips", "LegLeft", "LegRight", "FootLeft", "FootRight", "AttachmentHead", "AttachmentFace",
        "AttachmentBack", "AttachmentHipsFront", "AttachmentHipsBack", "AttachmentHipsLeft", "AttachmentHipsRight",
        "AttachmentShoulderLeft", "AttachmentShoulderRight", "AttachmentElbowLeft", "AttachmentElbowRight",
        "AttachmentKneeLeft", "AttachmentKneeRight", "Nose", "Teeth", "Tongue", "Wrap"
    };

    [MenuItem("Survive/Character/Crear e integrar catálogo Sidekick")]
    public static void CreateAndIntegrate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Salí de Play Mode antes de generar el catálogo Sidekick.");
        EnsureFolder("Assets/Resources/Character");
        EnsureFolder("Assets/Prefabs/Character");
        SidekickCharacterCatalog catalog = AssetDatabase.LoadAssetAtPath<SidekickCharacterCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<SidekickCharacterCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        var recipes = new List<SidekickCharacterCatalog.Recipe>();
        var materialByPart = new Dictionary<string, Material>(StringComparer.Ordinal);
        var materialByPack = new Dictionary<string, Material>(StringComparer.Ordinal);
        foreach (string file in Directory.GetFiles(SidekickRoot + "/Characters", "*.sk", SearchOption.AllDirectories).OrderBy(p => p))
        {
            string path = file.Replace('\\', '/');
            string id = Path.GetFileNameWithoutExtension(path);
            string pack = path.Split('/')[4];
            var recipe = new SidekickCharacterCatalog.Recipe { id = id, label = PrettyName(id), pack = pack };
            string text = File.ReadAllText(path);
            foreach (Match match in Regex.Matches(text, @"- Name: (\S+)\r?\n\s+PartType: (\S+)"))
                recipe.parts.Add(new CharacterPartSelection { partId = match.Groups[1].Value, slot = match.Groups[2].Value });
            Material material = AssetDatabase.LoadAssetAtPath<Material>(Path.GetDirectoryName(path).Replace('\\', '/') + "/Materials/" + id + ".mat");
            if (material != null)
            {
                if (!materialByPack.ContainsKey(pack)) materialByPack[pack] = material;
                foreach (CharacterPartSelection part in recipe.parts)
                    if (!materialByPart.ContainsKey(part.partId)) materialByPart[part.partId] = material;
            }
            recipes.Add(recipe);
        }

        Material humanMaterial = AssetDatabase.LoadAssetAtPath<Material>(SidekickRoot + "/Characters/HumanSpecies/HumanSpecies_01/Materials/HumanSpecies_01.mat");
        var parts = new List<SidekickCharacterCatalog.Part>();
        foreach (string file in Directory.GetFiles(SidekickRoot + "/Resources/Meshes", "*.fbx", SearchOption.AllDirectories).OrderBy(p => p))
        {
            string path = file.Replace('\\', '/');
            string id = Path.GetFileNameWithoutExtension(path);
            Match match = Regex.Match(id, @"_(\d{2})([A-Z]+)_[A-Z0-9]+$");
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out int number) || number <= 0 || number >= Slots.Length)
                throw new InvalidDataException("No se pudo clasificar el módulo Sidekick: " + path);
            string pack = Path.GetFileName(Path.GetDirectoryName(path));
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) == null)
                throw new InvalidDataException("El módulo no se importó como SkinnedMeshRenderer: " + path);
            materialByPart.TryGetValue(id, out Material material);
            if (pack == "Humans" && humanMaterial != null) material = humanMaterial;
            if (material == null) materialByPack.TryGetValue(pack, out material);
            if (material == null)
            {
                string speciesPack = pack == "Zombies" ? "ApocalypseZombies" : pack == "Robots" ? "ScifiRobots" : "HumanSpecies";
                materialByPack.TryGetValue(speciesPack, out material);
            }
            if (material == null) throw new InvalidDataException("No se encontró material para: " + id);
            parts.Add(new SidekickCharacterCatalog.Part { id = id, label = LabelForPart(id, pack), slot = Slots[number], pack = pack, prefab = prefab, material = material });
        }
        var partsById = parts.ToDictionary(p => p.id, StringComparer.Ordinal);
        foreach (SidekickCharacterCatalog.Recipe recipe in recipes)
        {
            recipe.parts.RemoveAll(p => !partsById.ContainsKey(p.partId));
            // Some original recipes intentionally omit unexposed body pieces; retain a complete playable humanoid contract.
            foreach (string slot in SidekickCharacterCatalog.RequiredSlots)
                if (!recipe.parts.Any(p => p.slot == slot))
                {
                    SidekickCharacterCatalog.Part fallback = parts.First(p => p.slot == slot && p.pack == "Humans");
                    recipe.parts.Add(new CharacterPartSelection { slot = slot, partId = fallback.id });
                }
        }
        catalog.parts = parts.ToArray();
        catalog.recipes = recipes.ToArray();
        EditorUtility.SetDirty(catalog);
        CreatePreview(catalog);
        IntegratePlayer(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log("SURVIVE_SIDEKICK_SETUP=PASS parts=" + parts.Count + " recipes=" + recipes.Count + " preview=" + PreviewPath);
    }

    private static void CreatePreview(SidekickCharacterCatalog catalog)
    {
        string path = SidekickRoot + "/Characters/HumanSpecies/HumanSpecies_01/HumanSpecies_01.prefab";
        GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (template == null) throw new FileNotFoundException("No se encontró el rig humano Sidekick.", path);
        GameObject preview = UnityEngine.Object.Instantiate(template);
        try
        {
            preview.name = "SidekickPreviewRig";
            Animator animator = preview.GetComponent<Animator>();
            if (animator != null) { animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
            SidekickCharacterAppearance appearance = preview.GetComponent<SidekickCharacterAppearance>() ?? preview.AddComponent<SidekickCharacterAppearance>();
            appearance.Configure(catalog, false);
            catalog.previewRig = PrefabUtility.SaveAsPrefabAsset(preview, PreviewPath);
            EditorUtility.SetDirty(catalog);
        }
        finally { UnityEngine.Object.DestroyImmediate(preview); }
    }

    private static void IntegratePlayer(SidekickCharacterCatalog catalog)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath) == null) throw new FileNotFoundException("No se encontró el prefab del jugador.", PlayerPath);
        GameObject player = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            SidekickCharacterAppearance appearance = player.GetComponent<SidekickCharacterAppearance>() ?? player.AddComponent<SidekickCharacterAppearance>();
            appearance.Configure(catalog, true);
            PrefabUtility.SaveAsPrefabAsset(player, PlayerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }
    }

    private static string PrettyName(string name) => Regex.Replace(name.Replace('_', ' '), "([a-z])([A-Z])", "$1 $2");
    private static string LabelForPart(string id, string pack)
    {
        Match match = Regex.Match(id, @"_(\d{2})_\d{2}[A-Z]+_");
        string variant = match.Success ? match.Groups[1].Value : id;
        return PrettyName(pack) + " " + variant;
    }

    private static void EnsureFolder(string path)
    {
        string[] pieces = path.Split('/');
        string parent = pieces[0];
        for (int i = 1; i < pieces.Length; i++)
        {
            string child = parent + "/" + pieces[i];
            if (!AssetDatabase.IsValidFolder(child)) AssetDatabase.CreateFolder(parent, pieces[i]);
            parent = child;
        }
    }
}
