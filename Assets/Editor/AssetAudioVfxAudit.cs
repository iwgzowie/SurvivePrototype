using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace Survive.Editor
{
    /// <summary>Audita dependencias de audio/VFX y sólo poda un manifiesto externo aprobado y verificado.</summary>
    public static class AssetAudioVfxAudit
    {
        const string GunFxRoot = "Assets/HIVEMIND/GunFX/URP";
        const string PolygonFxRoot = "Assets/PolygonApocalypse/Prefabs/FX";
        const string LocomotionAudioRoot = "Assets/StarterAssets/ThirdPersonController/Character/Sfx";
        static readonly StringComparer Paths = StringComparer.OrdinalIgnoreCase;
        static readonly Regex GuidPattern = new Regex(@"\b[0-9a-fA-F]{32}\b", RegexOptions.Compiled);
        static readonly Regex AssetLiteralPattern = new Regex("\"(Assets/[^\"\\r\\n]+)\"", RegexOptions.Compiled);

        [Serializable] public sealed class RootEntry { public string path, kind; }
        [Serializable] public sealed class AssetEntry
        {
            public string path, guid, assetType, reason, sha256, metaSha256, backupAsset, backupMeta;
            public long bytes, metaBytes;
            public List<string> referrers = new List<string>();
        }
        [Serializable] public sealed class AuditReport
        {
            public int schemaVersion = 1;
            public string projectPath, createdUtc, status, backupRoot;
            public int audioCount, dedicatedVfxPrefabCount, gunFxDependencyCount, polygonFxDependencyCount;
            public long candidateBytes;
            public List<RootEntry> roots = new List<RootEntry>();
            public List<AssetEntry> candidates = new List<AssetEntry>();
            public List<AssetEntry> retained = new List<AssetEntry>();
            public List<string> protectedOutsideScope = new List<string>();
            public List<string> dynamicReferences = new List<string>();
            public List<string> warnings = new List<string>();
            public List<string> blockingIssues = new List<string>();
            public string[] approvedPaths = Array.Empty<string>();
            public List<string> deletedPaths = new List<string>();
            public List<string> failedPaths = new List<string>();
        }

        /// <summary>Escribe el inventario externo; no modifica ni elimina assets.</summary>
        public static void ScanOnly()
        {
            EnsureEditorReady();
            string reportPath = ExternalPath(RequiredArgument("-surviveEffectsAuditPath"));
            AuditReport report = Scan();
            WriteReport(reportPath, report);
            Debug.Log($"SURVIVE_EFFECTS_AUDIT_SCAN=PASS candidates={report.candidates.Count} retained={report.retained.Count} bytes={report.candidateBytes} blockers={report.blockingIssues.Count} report={reportPath}");
        }

        /// <summary>La aprobación es la lista approvedPaths de un manifiesto externo invocado explícitamente.</summary>
        public static void PruneApproved()
        {
            EnsureEditorReady();
            string manifestPath = ExternalPath(Argument("-surviveEffectsApprovedManifest") ?? RequiredArgument("-surviveEffectsPruneManifest"));
            AuditReport approved = JsonUtility.FromJson<AuditReport>(File.ReadAllText(manifestPath));
            if (approved == null || approved.schemaVersion != 1 || !SameDirectory(approved.projectPath, ProjectRoot))
                throw new InvalidOperationException("El manifiesto no pertenece a este proyecto o su versión no es compatible.");
            if (approved.approvedPaths == null || approved.approvedPaths.Length == 0)
                throw new InvalidOperationException("El manifiesto necesita una lista approvedPaths explícita y no vacía.");

            AuditReport fresh = Scan();
            if (fresh.blockingIssues.Count > 0)
                throw new InvalidOperationException("La auditoría actual tiene bloqueos: " + string.Join("; ", fresh.blockingIssues));
            Dictionary<string, AssetEntry> previous = approved.candidates.ToDictionary(entry => entry.path, Paths);
            Dictionary<string, AssetEntry> current = fresh.candidates.ToDictionary(entry => entry.path, Paths);
            string[] selected = approved.approvedPaths.Distinct(Paths).ToArray();
            foreach (string path in selected)
            {
                if (!ValidAssetPath(path) || !previous.TryGetValue(path, out AssetEntry before)
                    || !current.TryGetValue(path, out AssetEntry now))
                    throw new InvalidOperationException("El asset aprobado ya no es candidato dentro del alcance permitido: " + path);
                if (before.sha256 != now.sha256 || before.metaSha256 != now.metaSha256
                    || before.bytes != now.bytes || before.metaBytes != now.metaBytes || before.guid != now.guid)
                    throw new InvalidOperationException("El asset o su meta cambió después de la revisión: " + path);
                ValidatePruneTarget(path);
            }

            // Se conserva una copia completa y verificada antes de efectuar la primera eliminación.
            string backup = Path.Combine(Path.GetDirectoryName(manifestPath), "effects-backup-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            ExternalPath(Path.Combine(backup, "manifest.json"));
            foreach (string path in selected)
            {
                AssetEntry entry = current[path];
                string absolute = ValidatePruneTarget(path);
                // Los nombres por GUID evitan MAX_PATH de Mono sin perder la ruta original del manifiesto.
                entry.backupAsset = "files/" + entry.guid + Path.GetExtension(path);
                entry.backupMeta = entry.backupAsset + ".meta";
                BackupFile(absolute, Path.Combine(backup, entry.backupAsset), entry.sha256);
                BackupFile(absolute + ".meta", Path.Combine(backup, entry.backupMeta), entry.metaSha256);
            }
            fresh.approvedPaths = selected;
            fresh.backupRoot = backup;
            fresh.status = "backup-verified-before-prune";
            WriteReport(Path.Combine(backup, "manifest.json"), fresh);

            // Se recalcula también el grafo: otros assets podrían haber agregado referencias durante la copia.
            AuditReport finalScan = Scan();
            if (finalScan.blockingIssues.Count > 0)
                throw new InvalidOperationException("Apareció un bloqueo durante el respaldo; no se eliminó ningún asset.");
            Dictionary<string, AssetEntry> finalCandidates = finalScan.candidates.ToDictionary(entry => entry.path, Paths);
            foreach (string path in selected)
            {
                AssetEntry entry = current[path];
                if (!finalCandidates.TryGetValue(path, out AssetEntry finalEntry)
                    || finalEntry.sha256 != entry.sha256 || finalEntry.metaSha256 != entry.metaSha256 || finalEntry.guid != entry.guid)
                    throw new InvalidOperationException("Cambió el asset o una referencia durante el respaldo; no se eliminó ningún asset: " + path);
                ValidatePruneTarget(path);
            }
            var failed = new List<string>();
            AssetDatabase.DeleteAssets(selected, failed);
            AssetDatabase.SaveAssets();
            fresh.failedPaths = failed;
            fresh.deletedPaths = selected.Where(path => !failed.Contains(path) && !File.Exists(path)).ToList();
            fresh.status = failed.Count == 0 && fresh.deletedPaths.Count == selected.Length ? "pruned" : "partial-prune";
            WriteReport(Path.Combine(backup, "result.json"), fresh);
            string resultPath = Argument("-surviveEffectsAuditPath");
            if (!string.IsNullOrEmpty(resultPath)) WriteReport(ExternalPath(resultPath), fresh);
            Debug.Log($"SURVIVE_EFFECTS_AUDIT_PRUNE={(fresh.status == "pruned" ? "PASS" : "FAIL")} deleted={fresh.deletedPaths.Count} failed={failed.Count} backup={backup}");
            if (fresh.status != "pruned") throw new InvalidOperationException("La poda fue parcial; consultar result.json y el respaldo verificado.");
        }

        static AuditReport Scan()
        {
            string[] assets = AssetDatabase.GetAllAssetPaths().Where(path => ValidAssetPath(path) && !AssetDatabase.IsValidFolder(path)).ToArray();
            var report = new AuditReport { projectPath = ProjectRoot, createdUtc = DateTime.UtcNow.ToString("O"), status = "scan-only" };
            var production = new HashSet<string>(Paths);
            var editorSources = new HashSet<string>(Paths);
            var surviving = new HashSet<string>(Paths);
            var demos = new HashSet<string>(Paths);
            var audio = new HashSet<string>(Paths);
            var vfxPrefabs = new HashSet<string>(Paths);
            var universe = new HashSet<string>(Paths);

            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                // Un AudioClip embebido no autoriza borrar su contenedor FBX/modelo.
                if (AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(AudioClip)) audio.Add(path);
                else if (!string.IsNullOrEmpty(path)) report.protectedOutsideScope.Add(path + " | audio embebido en contenedor no AudioClip");
            }
            universe.UnionWith(audio);
            foreach (string path in assets.Where(path => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)))
                if (Under(path, PolygonFxRoot) || (Under(path, GunFxRoot) && IsVfxPrefab(path))
                    || (Under(path, "Assets/StarterAssets") && InDedicatedVfxFolder(path) && IsVfxPrefab(path)))
                    vfxPrefabs.Add(path);
            universe.UnionWith(vfxPrefabs);
            string[] gunFxPrefabs = vfxPrefabs.Where(path => Under(path, GunFxRoot)).ToArray();
            var gunFxDependencies = new HashSet<string>(Dependencies(gunFxPrefabs).Where(path => Under(path, GunFxRoot) && AllowedGunFxResource(path)), Paths);
            foreach (string path in assets.Where(path => Under(path, GunFxRoot) && path.EndsWith(".vfx", StringComparison.OrdinalIgnoreCase))) gunFxDependencies.Add(path);
            universe.UnionWith(gunFxDependencies);
            // Esta carpeta es exclusiva de efectos. Los recursos compartidos con assets
            // supervivientes se protegen más abajo mediante el mismo grafo de dependencias.
            var polygonFxDependencies = new HashSet<string>(assets.Where(path => Under(path, PolygonFxRoot) && AllowedGunFxResource(path)), Paths);
            universe.UnionWith(polygonFxDependencies);
            report.audioCount = audio.Count; report.dedicatedVfxPrefabCount = vfxPrefabs.Count; report.gunFxDependencyCount = gunFxDependencies.Count; report.polygonFxDependencyCount = polygonFxDependencies.Count;

            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                if (scene.enabled && !string.IsNullOrEmpty(scene.path)) AddRoot(production, scene.path, "enabled-build-scene", report);
            foreach (string path in assets)
            {
                if (UnderAny(path, "Assets/Prefabs", "Assets/Players", "Assets/Data", "Assets/Resources", "Assets/Gustavo UI"))
                    AddRoot(production, path, "owned-content", report);
                if (path.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0)
                    AddRoot(production, path, "resources-runtime", report);
                if (audio.Contains(path) && Under(path, LocomotionAudioRoot)) AddRoot(production, path, "protected-locomotion-audio", report);
                if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) && !production.Contains(path))
                    AddRoot(demos, path, IsDemo(path) ? "surviving-demo-scene" : "surviving-nonbuild-scene", report);
                // Los assets no VFX que sobreviven no deben perder materiales, sonidos ni subassets compartidos.
                if (!universe.Contains(path) && !Under(path, GunFxRoot) && !Under(path, PolygonFxRoot)
                    && HasSerializedDependencies(path) && !path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                    AddRoot(surviving, path, "surviving-non-vfx-asset", report);
            }
            foreach (UObject preloaded in PlayerSettings.GetPreloadedAssets())
                if (preloaded != null) AddRoot(production, AssetDatabase.GetAssetPath(preloaded), "preloaded-asset", report);
            foreach (string configuration in Directory.GetFiles("ProjectSettings", "*.asset"))
                AddGuidReferences(File.ReadAllText(configuration), production, "project-settings:" + configuration.Replace('\\', '/'), report);
            AddAddressables(production, report);
            AddCodeBindings(assets, editorSources, report);
            AddRequiredEditorSources(editorSources, report);

            HashSet<string> productionReach = Dependencies(production);
            HashSet<string> editorReach = Dependencies(editorSources);
            HashSet<string> survivingReach = Dependencies(surviving);
            HashSet<string> demoReach = Dependencies(demos);
            var demoReferrers = new Dictionary<string, List<string>>(Paths);
            foreach (string demo in demos)
                foreach (string dependency in Dependencies(new[] { demo }).Where(universe.Contains))
                {
                    if (!demoReferrers.TryGetValue(dependency, out List<string> entries)) demoReferrers[dependency] = entries = new List<string>();
                    entries.Add(demo);
                }
            foreach (string path in universe.OrderBy(path => path, StringComparer.Ordinal))
            {
                AssetEntry entry = Entry(path);
                if (!File.Exists(path) || !File.Exists(path + ".meta")) report.blockingIssues.Add("Falta el asset o su meta en el inventario: " + path);
                var reasons = new List<string>();
                if (productionReach.Contains(path)) reasons.Add("production-dependency");
                if (editorReach.Contains(path)) reasons.Add("editor-source-or-regeneration-dependency");
                if (survivingReach.Contains(path)) reasons.Add("shared-with-surviving-non-vfx-asset");
                if (demoReach.Contains(path))
                {
                    reasons.Add("surviving-demo-or-nonbuild-scene-dependency");
                    if (demoReferrers.TryGetValue(path, out List<string> refs)) entry.referrers.AddRange(refs);
                }
                if (reasons.Count > 0)
                {
                    entry.reason = string.Join(";", reasons); report.retained.Add(entry);
                }
                else
                {
                    entry.reason = audio.Contains(path) ? "AudioClip sin dependencias productivas, de regeneracion, compartidas ni demos supervivientes"
                        : "VFX dedicado o recurso de carpeta FX exclusiva sin referencias supervivientes";
                    report.candidates.Add(entry); report.candidateBytes += entry.bytes + entry.metaBytes;
                }
            }
            foreach (string path in assets.Where(path => Under(path, GunFxRoot) && !universe.Contains(path)))
                report.protectedOutsideScope.Add(path + " | fuera del cierre de recursos VFX o tipo protegido; los scripts nunca se podan");
            report.dynamicReferences = report.dynamicReferences.Distinct().OrderBy(value => value).ToList();
            return report;
        }

        // Comparte la resolución de cargas y fuentes con la limpieza general de contenido.
        public static string[] RequiredContentRoots(out string[] blockers)
        {
            var report = new AuditReport();
            var roots = new HashSet<string>(Paths);
            string[] assets = AssetDatabase.GetAllAssetPaths().Where(path => ValidAssetPath(path) && !AssetDatabase.IsValidFolder(path)).ToArray();
            AddCodeBindings(assets, roots, report);
            AddRequiredEditorSources(roots, report);
            AddAddressables(roots, report);
            foreach (UObject asset in PlayerSettings.GetPreloadedAssets())
                if (asset != null) AddRoot(roots, AssetDatabase.GetAssetPath(asset), "preloaded", report);
            foreach (string settings in Directory.GetFiles("ProjectSettings", "*.asset"))
                AddGuidReferences(File.ReadAllText(settings), roots, "settings", report);
            blockers = report.blockingIssues.ToArray();
            return roots.ToArray();
        }

        static void AddCodeBindings(string[] assets, HashSet<string> roots, AuditReport report)
        {
            foreach (string script in assets.Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
            {
                string text = File.ReadAllText(script);
                AddGuidReferences(text, roots, "code-guid:" + script, report);
                foreach (Match match in AssetLiteralPattern.Matches(text))
                {
                    string path = match.Groups[1].Value.Replace("\\\\", "/");
                    if (!ValidAssetPath(path)) continue;
                    if (AssetDatabase.IsValidFolder(path)) report.dynamicReferences.Add(script + " | folder literal (no equivale a cargar todos sus assets): " + path);
                    else if (File.Exists(path)) AddRoot(roots, path, "code-asset-literal:" + script, report);
                    else report.dynamicReferences.Add(script + " | ruta interpolada, parcial o inexistente: " + path);
                }
                bool editor = script.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!editor && (text.Contains("AssetBundle.Load") || text.Contains("AssetBundle.GetAllLoadedAssetBundles")))
                    report.blockingIssues.Add(script + " usa AssetBundle dinamico; incorporar sus assets como raices explicitas antes de podar.");
                if (!editor && text.Contains("Resources.Load")) report.dynamicReferences.Add(script + " | Resources.Load cubierto conservando todos los Resources");
                if (!editor && text.Contains("Addressables.Load")) report.dynamicReferences.Add(script + " | carga Addressables; entradas de settings conservadas como raices");
            }
        }
        static void AddRequiredEditorSources(HashSet<string> roots, AuditReport report)
        {
            Type setup = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("Survive.Editor.WeaponVfxSetup", false)).FirstOrDefault(type => type != null);
            if (setup == null) { report.warnings.Add("WeaponVfxSetup no existe todavia; volver a auditar despues de integrarlo."); return; }
            object sources = setup.GetField("RequiredSourceAssets", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
                ?? setup.GetProperty("RequiredSourceAssets", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (!(sources is IEnumerable enumerable)) { report.blockingIssues.Add("WeaponVfxSetup no expone RequiredSourceAssets para proteger su regeneracion."); return; }
            foreach (object value in enumerable)
            {
                string path = value as string;
                if (!ValidAssetPath(path) || !File.Exists(path)) { report.blockingIssues.Add("Falta una fuente declarada por WeaponVfxSetup: " + path); continue; }
                AddRoot(roots, path, "weapon-vfx-regeneration-source", report);
            }
        }
        static void AddAddressables(HashSet<string> roots, AuditReport report)
        {
            string[] settings = AssetDatabase.FindAssets("t:AddressableAssetSettings");
            foreach (string guid in settings)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid); UObject instance = AssetDatabase.LoadMainAssetAtPath(path); AddRoot(roots, path, "addressables-settings", report);
                object groups = Member(instance, "groups");
                if (!(groups is IEnumerable enumerable)) { report.blockingIssues.Add("No se pudieron enumerar los grupos Addressables: " + path); continue; }
                foreach (object group in enumerable)
                {
                    if (!(Member(group, "entries") is IEnumerable entries)) continue;
                    foreach (object entry in entries)
                    {
                        string entryPath = AssetDatabase.GUIDToAssetPath(Member(entry, "guid") as string);
                        if (AssetDatabase.IsValidFolder(entryPath))
                            foreach (string child in AssetDatabase.FindAssets("", new[] { entryPath })) AddRoot(roots, AssetDatabase.GUIDToAssetPath(child), "addressables-folder-entry", report);
                        else AddRoot(roots, entryPath, "addressables-entry", report);
                    }
                }
            }
        }
        static object Member(object instance, string name) => instance == null ? null : instance.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(instance)
            ?? instance.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(instance);
        static void AddGuidReferences(string text, HashSet<string> roots, string kind, AuditReport report)
        {
            foreach (Match match in GuidPattern.Matches(text))
            {
                string path = AssetDatabase.GUIDToAssetPath(match.Value.ToLowerInvariant());
                if (!string.IsNullOrEmpty(path)) AddRoot(roots, path, kind, report);
            }
        }
        static void AddRoot(HashSet<string> roots, string path, string kind, AuditReport report)
        {
            if (!ValidAssetPath(path) || AssetDatabase.IsValidFolder(path)) return;
            if (roots.Add(path)) report.roots.Add(new RootEntry { path = path, kind = kind });
        }
        static HashSet<string> Dependencies(IEnumerable<string> roots)
        {
            string[] paths = roots.Where(path => !string.IsNullOrEmpty(path)).Distinct(Paths).ToArray();
            var result = new HashSet<string>(paths, Paths);
            if (paths.Length > 0) result.UnionWith(AssetDatabase.GetDependencies(paths, true));
            return result;
        }
        static bool IsVfxPrefab(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return false;
            if (prefab.GetComponentInChildren<ParticleSystem>(true) != null || prefab.GetComponentInChildren<TrailRenderer>(true) != null || prefab.GetComponentInChildren<LineRenderer>(true) != null) return true;
            return prefab.GetComponentsInChildren<Component>(true).Any(component => component != null && (component.GetType().Name == "VisualEffect" || component.GetType().Name == "DecalProjector"));
        }
        static bool AllowedGunFxResource(string path)
        {
            if (!ValidAssetPath(path) || AssetDatabase.IsValidFolder(path)) return false;
            Type type = AssetDatabase.GetMainAssetTypeAtPath(path);
            if (type == typeof(MonoScript) || path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase)) return false;
            string extension = Path.GetExtension(path).ToLowerInvariant();
            return type == typeof(Material) || type == typeof(Texture2D) || type == typeof(Mesh) || type == typeof(Shader)
                || extension == ".fbx" || extension == ".obj" || extension == ".shadergraph" || extension == ".shadersubgraph" || extension == ".vfx" || extension == ".anim" || extension == ".controller";
        }
        static bool HasSerializedDependencies(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            return new[] { ".prefab", ".unity", ".asset", ".mat", ".fbx", ".obj", ".shadergraph", ".shadersubgraph", ".controller", ".overridecontroller", ".anim", ".playable", ".spriteatlas" }.Contains(extension);
        }
        static bool IsDemo(string path) => path.IndexOf("demo", StringComparison.OrdinalIgnoreCase) >= 0 || path.IndexOf("/Examples/", StringComparison.OrdinalIgnoreCase) >= 0 || path.IndexOf("/Samples/", StringComparison.OrdinalIgnoreCase) >= 0;
        static bool InDedicatedVfxFolder(string path) => new[] { "/FX/", "/VFX/", "/Effects/", "/Particles/" }.Any(segment => path.IndexOf(segment, StringComparison.OrdinalIgnoreCase) >= 0);
        static bool Under(string path, string root) => path != null && (path.Equals(root, StringComparison.OrdinalIgnoreCase) || path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase));
        static bool UnderAny(string path, params string[] roots) => roots.Any(root => Under(path, root));
        static bool ValidAssetPath(string path) => !string.IsNullOrEmpty(path) && path.StartsWith("Assets/", StringComparison.Ordinal) && !path.Contains("..") && !path.Contains("\\") && !path.Contains(":");
        static string ValidatePruneTarget(string assetPath)
        {
            if (!ValidAssetPath(assetPath)) throw new InvalidOperationException("Ruta de asset inválida para la poda: " + assetPath);
            string project = ProjectRoot;
            string absolute = Path.GetFullPath(Path.Combine(project, assetPath.Replace('/', Path.DirectorySeparatorChar)));
            if (!absolute.StartsWith(project + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("La ruta resuelta sale del proyecto: " + absolute);
            if (!File.Exists(absolute) || !File.Exists(absolute + ".meta"))
                throw new InvalidOperationException("El asset y su meta deben existir antes de respaldar o eliminar: " + absolute);
            RejectReparsePoint(absolute);
            RejectReparsePoint(absolute + ".meta");
            string ancestor = Path.GetDirectoryName(absolute);
            while (!string.IsNullOrEmpty(ancestor))
            {
                RejectReparsePoint(ancestor);
                if (SameDirectory(ancestor, project)) return absolute;
                if (!ancestor.StartsWith(project + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) break;
                ancestor = Path.GetDirectoryName(ancestor);
            }
            throw new InvalidOperationException("No se pudo verificar la ascendencia del asset dentro del proyecto: " + absolute);
        }
        static void RejectReparsePoint(string path)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("La poda no opera sobre enlaces simbólicos ni junctions: " + path);
        }
        static AssetEntry Entry(string path) => new AssetEntry { path = path, guid = AssetDatabase.AssetPathToGUID(path), assetType = AssetDatabase.GetMainAssetTypeAtPath(path)?.FullName ?? "unknown", bytes = File.Exists(path) ? new FileInfo(path).Length : 0, metaBytes = File.Exists(path + ".meta") ? new FileInfo(path + ".meta").Length : 0, sha256 = Hash(path), metaSha256 = Hash(path + ".meta") };
        static string Hash(string path) { if (!File.Exists(path)) return ""; using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
        static void BackupFile(string source, string destination, string expected)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.Copy(source, destination, false);
            if (Hash(destination) != expected) throw new IOException("El respaldo no coincide con el asset aprobado: " + source);
        }
        static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..")).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        static bool SameDirectory(string left, string right) => !string.IsNullOrEmpty(left) && Paths.Equals(Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        static string ExternalPath(string path)
        {
            string full = Path.GetFullPath(path);
            if (SameDirectory(full, ProjectRoot) || full.StartsWith(ProjectRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("El reporte, manifiesto y respaldo deben quedar fuera del repositorio: " + full);
            return full;
        }
        static string Argument(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == key) return args[i + 1];
            return null;
        }
        static string RequiredArgument(string key) => Argument(key) ?? throw new InvalidOperationException("Falta el argumento explícito " + key);
        static void EnsureEditorReady() { if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) throw new InvalidOperationException("Esperar la importación/compilación y salir de PlayMode antes de auditar o podar."); }
        static void WriteReport(string path, AuditReport report) { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, JsonUtility.ToJson(report, true)); }
    }
}
