using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Survive.Editor
{
    public static class UnusedContentCleanup
    {
        const string Report = "Logs/UnusedContent/audit.json";
        static readonly StringComparer Paths = StringComparer.OrdinalIgnoreCase;
        static readonly Regex GuidPattern = new Regex(@"\bguid[""\\]*\s*:\s*[""\\]*([0-9a-fA-F]{32})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static readonly HashSet<string> ContentExtensions = new HashSet<string>(new[] {
            ".prefab", ".unity", ".asset", ".mat", ".fbx", ".obj", ".blend", ".dae", ".3ds", ".max",
            ".anim", ".controller", ".overridecontroller", ".mask", ".png", ".jpg", ".jpeg", ".tga", ".tif", ".tiff",
            ".psd", ".exr", ".hdr", ".dds", ".wav", ".ogg", ".mp3", ".aiff", ".aif", ".spriteatlas", ".vfx",
            ".fontsettings", ".ttf", ".otf", ".mp4", ".webm", ".mov", ".svg"
        }, Paths);
        static readonly HashSet<string> SerializedExtensions = new HashSet<string>(new[] {
            ".prefab", ".unity", ".asset", ".mat", ".anim", ".controller", ".overridecontroller", ".mask",
            ".spriteatlas", ".vfx", ".shadergraph", ".shadersubgraph", ".sk", ".json", ".lighting"
        }, Paths);

        [Serializable] public class Entry
        {
            public string path, guid, sha256, metaSha256, backupFile;
            public long bytes, metaBytes;
        }
        [Serializable] public class Audit
        {
            public string project, status, backupRoot;
            public int totalAssets, retainedAssets;
            public long candidateBytes;
            public string[] roots, retained, blockers, missingBefore, missingAfter, failed, deleted;
            public Entry[] candidates;
        }

        [MenuItem("Survive/Mantenimiento/Auditar contenido sin uso")]
        public static void Scan()
        {
            Ready(); var audit = Collect();
            Directory.CreateDirectory("Logs/UnusedContent");
            File.WriteAllText(Report, JsonUtility.ToJson(audit, true));
            Debug.Log($"UNUSED_CONTENT_SCAN assets={audit.totalAssets} retained={audit.retainedAssets} candidates={audit.candidates.Length} bytes={audit.candidateBytes} blockers={audit.blockers.Length}");
        }

        static Audit Collect()
        {
            var all = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/") && !AssetDatabase.IsValidFolder(p) && File.Exists(p)).ToArray();
            var rootSet = new HashSet<string>(Paths);
            foreach (var scene in EditorBuildSettings.scenes) if (File.Exists(scene.path)) rootSet.Add(scene.path);
            foreach (string p in all)
            {
                string ext = Path.GetExtension(p);
                // Conserva código, shaders, licencias, metadata de importación y herramientas.
                if (!ContentExtensions.Contains(ext) || p.Contains("/Resources/") || p.Contains("/StreamingAssets/")
                    || p.Contains("/Editor/") || p.StartsWith("Assets/Scenes/") && ext == ".unity"
                    || KeepLibraryResource(p)
                    || p.IndexOf("license", StringComparison.OrdinalIgnoreCase) >= 0) rootSet.Add(p);
            }
            rootSet.UnionWith(AssetAudioVfxAudit.RequiredContentRoots(out var blockers));
            // Los constructores resuelven parte de los assets por nombre, además de rutas/GUID.
            string code = string.Join("\n", all.Where(p => p.EndsWith(".cs") && !p.EndsWith("UnusedContentCleanup.cs") && !p.EndsWith("AssetAudioVfxAudit.cs")).Select(File.ReadAllText));
            var literalNames = new HashSet<string>(Regex.Matches(code, "\"([^\"\r\n]{6,200})\"").Cast<Match>().Select(m => m.Groups[1].Value), Paths);
            foreach (string literal in literalNames.ToArray())
            {
                string value = literal.Replace('\\', '/');
                literalNames.Add(value.Substring(value.LastIndexOf('/') + 1));
            }
            foreach (string p in all)
                if (literalNames.Contains(Path.GetFileNameWithoutExtension(p)) || literalNames.Contains(Path.GetFileName(p))) rootSet.Add(p);
            // Las recetas .sk y sus materiales son fuentes del creador, aunque no sean GameObjects.
            foreach (string p in all.Where(p => p.StartsWith("Assets/Synty/SidekickCharacters/Characters/"))) rootSet.Add(p);

            Debug.Log("UNUSED_CONTENT_ROOTS=" + rootSet.Count);
            var keep = new HashSet<string>(AssetDatabase.GetDependencies(rootSet.ToArray(), true), Paths);
            keep.UnionWith(rootSet);
            Debug.Log("UNUSED_CONTENT_NATIVE_GRAPH=" + keep.Count);
            var seen = new HashSet<string>(Paths);
            var guidCache = new Dictionary<string, string>(StringComparer.Ordinal);
            var missing = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>(keep.Where(p => p.StartsWith("Assets/")));
            // Complementa el grafo nativo con GUID serializados: incluye referencias a assets con fallos de importación previos.
            while (queue.Count > 0)
            {
                string p = queue.Dequeue(); if (!seen.Add(p) || !File.Exists(p)) continue;
                string text = File.Exists(p + ".meta") ? File.ReadAllText(p + ".meta") : "";
                if (SerializedExtensions.Contains(Path.GetExtension(p))) text += "\n" + File.ReadAllText(p);
                foreach (Match m in GuidPattern.Matches(text))
                {
                    string guid = m.Groups[1].Value.ToLowerInvariant();
                    if (!guidCache.TryGetValue(guid, out string target)) guidCache[guid] = target = AssetDatabase.GUIDToAssetPath(guid);
                    if (target.StartsWith("Assets/") && File.Exists(target))
                    {
                        if (keep.Add(target))
                            foreach (string dep in AssetDatabase.GetDependencies(target, true)) if (keep.Add(dep) && dep.StartsWith("Assets/")) queue.Enqueue(dep);
                        if (!seen.Contains(target)) queue.Enqueue(target);
                    }
                    else if (string.IsNullOrEmpty(target) && !guid.StartsWith("0000000000000000")) missing.Add(p + " | " + guid);
                }
            }
            Debug.Log("UNUSED_CONTENT_GRAPH retained=" + keep.Count + " examined=" + seen.Count);
            var candidates = all.Where(p => !keep.Contains(p) && ContentExtensions.Contains(Path.GetExtension(p))).OrderBy(p => p).Select(p => new Entry {
                path = p, guid = AssetDatabase.AssetPathToGUID(p), bytes = new FileInfo(p).Length,
                metaBytes = File.Exists(p + ".meta") ? new FileInfo(p + ".meta").Length : 0
            }).ToArray();
            Parallel.ForEach(candidates, new ParallelOptions { MaxDegreeOfParallelism = 4 }, e => { e.sha256 = Hash(e.path); e.metaSha256 = Hash(e.path + ".meta"); });
            return new Audit { project = Root, status = "audited", totalAssets = all.Length, retainedAssets = all.Count(keep.Contains),
                roots = rootSet.OrderBy(p => p).ToArray(), retained = all.Where(keep.Contains).OrderBy(p => p).ToArray(), candidates = candidates,
                candidateBytes = candidates.Sum(e => e.bytes + e.metaBytes), blockers = blockers,
                missingBefore = missing.OrderBy(p => p).ToArray() };
        }

        // Invocación explícita desde línea de comandos después de revisar audit.json.
        public static void Apply()
        {
            Ready(); var previous = JsonUtility.FromJson<Audit>(File.ReadAllText(Report));
            if (!Paths.Equals(previous.project, Root) || previous.status != "audited") throw new Exception("Auditoría inválida.");
            var fresh = Collect();
            if (fresh.blockers.Length != 0) throw new Exception(string.Join("\n", fresh.blockers));
            var reviewed = previous.candidates.ToDictionary(e => e.path, Paths);
            foreach (var now in fresh.candidates)
                if (!reviewed.TryGetValue(now.path, out var e) || e.guid != now.guid || e.sha256 != now.sha256 || e.metaSha256 != now.metaSha256)
                    throw new Exception("Candidato nuevo o modificado sin revisar: " + now.path);
            string backup = ".utmp/UnusedAssets-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
            string absoluteBackup = Path.GetFullPath(backup);
            if (!absoluteBackup.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new Exception("Respaldo fuera del workspace.");
            fresh.backupRoot = absoluteBackup;
            // Se verifica la copia completa antes de borrar el primer archivo.
            foreach (var e in fresh.candidates)
            {
                ValidatePath(e.path); ValidatePath(e.path + ".meta");
                e.backupFile = "files/" + e.guid + Path.GetExtension(e.path);
            }
            Parallel.ForEach(fresh.candidates, new ParallelOptions { MaxDegreeOfParallelism = 4 }, e => {
                Copy(e.path, Path.Combine(backup, e.backupFile), e.sha256);
                Copy(e.path + ".meta", Path.Combine(backup, e.backupFile + ".meta"), e.metaSha256);
            });
            fresh.status = "backup-verified"; Write(backup + "/manifest.json", fresh);
            foreach (var e in fresh.candidates)
                if (Hash(e.path) != e.sha256 || Hash(e.path + ".meta") != e.metaSha256) throw new Exception("Cambió durante el respaldo: " + e.path);
            Debug.Log("UNUSED_CONTENT_BACKUP_VERIFIED files=" + fresh.candidates.Length + " root=" + absoluteBackup);
            var failed = new List<string>();
            AssetDatabase.DeleteAssets(fresh.candidates.Select(e => e.path).ToArray(), failed);
            AssetDatabase.SaveAssets();
            fresh.failed = failed.ToArray(); fresh.deleted = fresh.candidates.Where(e => !File.Exists(e.path)).Select(e => e.path).ToArray();
            fresh.status = failed.Count == 0 && fresh.deleted.Length == fresh.candidates.Length ? "pruned" : "partial";
            Write(backup + "/result.json", fresh); Write("Logs/UnusedContent/result.json", fresh);
            // Ningún contenido conservado debe perder nuevas referencias.
            var after = Collect(); fresh.missingAfter = after.missingBefore;
            var oldMissing = new HashSet<string>(fresh.missingBefore);
            string[] newMissing = fresh.missingAfter.Where(p => !oldMissing.Contains(p)).ToArray();
            Write("Logs/UnusedContent/result.json", fresh);
            if (failed.Count != 0 || newMissing.Length != 0 || after.candidates.Length != 0)
                throw new Exception($"Verificar poda: fallos={failed.Count}, referencias nuevas faltantes={newMissing.Length}, candidatos restantes={after.candidates.Length}");
            Debug.Log($"UNUSED_CONTENT_PRUNE=PASS deleted={fresh.deleted.Length} bytes={fresh.candidateBytes} newMissing=0 backup={absoluteBackup}");
        }

        public static void Validate()
        {
            CharacterCustomizationValidation.Run();
            CityAtmosphereValidation.Run();
        }

        // Preferencia confirmada: conservar recursos para ampliar el juego; retirar demos y ejemplos.
        static bool KeepLibraryResource(string path)
        {
            string lower = path.ToLowerInvariant();
            bool example = lower.Contains("/samples/") || lower.Contains("/_demos/") || lower.Contains("/demo/")
                || lower.Contains("/examples/") || lower.EndsWith(".unity");
            if (example) return false;
            return path.StartsWith("Assets/PolygonApocalypse/") || path.StartsWith("Assets/Synty/")
                || path.StartsWith("Assets/Hovl Studio/") || path.StartsWith("Assets/HIVEMIND/")
                || path.StartsWith("Assets/Materials/") || path.StartsWith("Assets/Models/")
                || path.StartsWith("Assets/Sprites/") || path.StartsWith("Assets/Animations/")
                || path.StartsWith("Assets/Skybox/")
                || path.StartsWith("Assets/StarterAssets/ThirdPersonController/Character/Sfx/")
                || path.StartsWith("Assets/ProBuilder Data/")
                || path.StartsWith("Assets/New Terrain", StringComparison.OrdinalIgnoreCase);
        }

        static string Root => Path.GetFullPath(Application.dataPath + "/..").TrimEnd(Path.DirectorySeparatorChar);
        static void ValidatePath(string relative)
        {
            string absolute = Path.GetFullPath(relative);
            if (!absolute.StartsWith(Root + Path.DirectorySeparatorChar + "Assets" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new Exception("Ruta fuera de Assets: " + relative);
            for (string p = absolute; !Paths.Equals(p, Root); p = Path.GetDirectoryName(p))
                if ((File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0) throw new Exception("No se podan junctions: " + relative);
        }
        static string Hash(string p) { if (!File.Exists(p)) return ""; using (var sha = SHA256.Create()) using (var stream = File.OpenRead(p)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(); }
        static void Copy(string from, string to, string hash) { Directory.CreateDirectory(Path.GetDirectoryName(to)); File.Copy(from, to, false); if (Hash(to) != hash) throw new Exception("Respaldo inválido: " + from); }
        static void Write(string path, Audit audit) => File.WriteAllText(path, JsonUtility.ToJson(audit, true));
        static void Ready() { if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) throw new Exception("Esperar importación y salir de Play Mode."); }
    }
}
