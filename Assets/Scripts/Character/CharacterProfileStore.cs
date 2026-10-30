using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Survive.Character
{
    /// <summary>Guarda sólo personajes confirmados, con reemplazo atómico y respaldo del perfil anterior.</summary>
    public static class CharacterProfileStore
    {
        private const long MaxProfileBytes = 64 * 1024;
#if UNITY_EDITOR
        // Las validaciones del Editor nunca deben tocar el personaje real del usuario.
        public static string ProfilePathOverride { get; set; }
#endif
        public static string ProfilePath
        {
            get
            {
#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(ProfilePathOverride)) return ProfilePathOverride;
#endif
                return Path.Combine(Application.persistentDataPath, "character-profile.json");
            }
        }

        public static bool TryLoad(out CharacterProfile profile) => TryLoad(ProfilePath, out profile);

        public static bool TryLoad(string path, out CharacterProfile profile)
        {
            if (TryRead(path, out profile)) return true;
            // Una interrupción o archivo dañado no debe descartar un personaje anterior válido.
            return TryRead(path + ".bak", out profile);
        }

        private static bool TryRead(string path, out CharacterProfile profile)
        {
            profile = null;
            try
            {
                var file = new FileInfo(path);
                if (!file.Exists || file.Length <= 0 || file.Length > MaxProfileBytes) return false;
                var candidate = JsonUtility.FromJson<CharacterProfile>(File.ReadAllText(path, Encoding.UTF8));
                if (candidate == null || !candidate.ValidateData(out _)) return false;
                profile = candidate;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException
                || exception is ArgumentException)
            {
                return false;
            }
        }

        public static bool TrySave(CharacterProfile profile, out string error) => TrySave(profile, ProfilePath, out error);

        public static bool TrySave(CharacterProfile profile, string path, out string error)
        {
            error = null;
            if (profile == null || !profile.ValidateData(out error))
            {
                error ??= "No hay un personaje para guardar.";
                return false;
            }

            string temporaryPath = path + ".tmp";
            try
            {
                string folder = Path.GetDirectoryName(Path.GetFullPath(path));
                Directory.CreateDirectory(folder);
                CharacterProfile copy = profile.Clone();
                copy.displayName = copy.displayName.Trim();
                byte[] bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(copy, true));
                using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporaryPath, path, path + ".bak");
                else File.Move(temporaryPath, path);
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException
                || exception is ArgumentException || exception is NotSupportedException)
            {
                Debug.LogWarning("No se pudo guardar el personaje: " + exception.Message);
                error = "No se pudo guardar el perfil. Revisá el espacio disponible y los permisos de la carpeta.";
                return false;
            }
            finally
            {
                try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
