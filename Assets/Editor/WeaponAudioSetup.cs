using System;
using System.Linq;
using Survive.Combat;
using Survive.Items;
using UnityEditor;
using UnityEngine;

namespace Survive.Editor
{
    /// <summary>Vincula las grabaciones del paquete al sistema de armas existente.</summary>
    public static class WeaponAudioSetup
    {
        public const string SoundsFolder = "Assets/Modern Weapons ALL-IN-ONE Sound System/Sounds";
        public const string ReloadSoundsFolder = "Assets/Audio/Weapons/Reload";
        const string DefinitionsFolder = "Assets/Data/Weapons";
        const string PlayerPath = "Assets/Prefabs/Player/UPPPlayer.prefab";
        const string GeneratedReloadGuid = "1b89ee5493757bb49aae051eeb8870a3";

        readonly struct ShotProfile
        {
            public readonly string Id, Folder, Prefix;
            public readonly int Variants;
            public readonly float Near, Stop, Tail;
            public ShotProfile(string id, string folder, string prefix, int variants,
                float near, float stop, float tail)
            {
                Id = id; Folder = folder; Prefix = prefix; Variants = variants;
                Near = near; Stop = stop; Tail = tail;
            }
        }

        // El paquete no contiene revólver, rifle de caza ni sonidos de recarga.
        // Desert Eagle y M2010 aportan el timbre del disparo, sin fingir mecanismos.
        static readonly ShotProfile[] Profiles =
        {
            new ShotProfile("Pistol", "FN 509 Semi-Automatic Pistol", "FN509", 6, .30f, .08f, .045f),
            new ShotProfile("Revolver", "Desert Eagle Semi-Automatic Pistol", "DesertEagle", 4, .38f, .10f, .055f),
            new ShotProfile("SMG", "SIG MPX Submachine Gun", "SIG_MPX", 4, .24f, .065f, .025f),
            new ShotProfile("AssaultRifle", "AK-74 Assault Rifle", "AK74", 4, .30f, .075f, .03f),
            new ShotProfile("Shotgun", "Kel-Tec KSG Pump-Action Shotgun", "Kel-Tec", 4, .40f, .10f, .06f),
            new ShotProfile("HuntingRifle", "M2010 ESR Sniper Rifle", "M2010", 4, .36f, .09f, .035f),
            new ShotProfile("SniperRifle", "CheyTac M200 Sniper Rifle", "CheyTac", 4, .42f, .10f, .05f),
            new ShotProfile("MachineGun", "M249 Light Machine Gun", "M249", 4, .28f, .07f, .025f)
        };

        readonly struct ReloadCue
        {
            public readonly float Time, Volume;
            public readonly string Sound;
            public ReloadCue(float time, string sound, float volume = 1f)
            { Time = time; Sound = sound; Volume = volume; }
        }

        readonly struct ReloadProfile
        {
            public readonly string Id;
            public readonly float Volume;
            public readonly ReloadCue[] Cues;
            public ReloadProfile(string id, float volume, params ReloadCue[] cues)
            { Id = id; Volume = volume; Cues = cues; }
        }

        // Los cortes conservan los golpes reales de JU TPS. Se sincronizan con las
        // piezas propias de Synty; no son ocho grabaciones específicas de cada modelo.
        static readonly ReloadProfile[] ReloadProfiles =
        {
            new ReloadProfile("Pistol", .50f, new ReloadCue(.16f, "Reload_A", .7f),
                new ReloadCue(.65f, "Reload_B"), new ReloadCue(.78f, "Bolt_A", .9f), new ReloadCue(.92f, "Bolt_B")),
            new ReloadProfile("Revolver", .52f, new ReloadCue(.17f, "Bolt_A", .9f),
                new ReloadCue(.36f, "Reload_A", .7f), new ReloadCue(.60f, "Reload_B", .9f), new ReloadCue(.84f, "Bolt_B")),
            new ReloadProfile("SMG", .48f, new ReloadCue(.16f, "Reload_A", .7f),
                new ReloadCue(.65f, "Reload_B"), new ReloadCue(.78f, "Bolt_A", .9f), new ReloadCue(.92f, "Bolt_B")),
            new ReloadProfile("AssaultRifle", .52f, new ReloadCue(.16f, "Reload_A", .7f),
                new ReloadCue(.65f, "Reload_B"), new ReloadCue(.78f, "Bolt_A", .9f), new ReloadCue(.92f, "Bolt_B")),
            new ReloadProfile("Shotgun", .54f, new ReloadCue(.05f, "Bolt_A", .9f),
                new ReloadCue(.35f, "Reload_A", .7f), new ReloadCue(.59f, "Reload_B"), new ReloadCue(.82f, "Bolt_B")),
            new ReloadProfile("HuntingRifle", .52f, new ReloadCue(.12f, "Bolt_A", .9f),
                new ReloadCue(.41f, "Reload_A", .7f), new ReloadCue(.62f, "Reload_B"), new ReloadCue(.83f, "Bolt_B")),
            new ReloadProfile("SniperRifle", .54f, new ReloadCue(.12f, "Bolt_A", .9f),
                new ReloadCue(.17f, "Reload_A", .7f), new ReloadCue(.65f, "Reload_B"), new ReloadCue(.83f, "Bolt_B")),
            new ReloadProfile("MachineGun", .58f, new ReloadCue(.16f, "Reload_A", .8f),
                new ReloadCue(.65f, "Reload_B"), new ReloadCue(.78f, "Bolt_A", .9f), new ReloadCue(.92f, "Bolt_B"))
        };

        [MenuItem("Survive/Weapons/Integrar audio por tipo y recargas JU TPS")]
        public static void CreateAndIntegrate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Salir de Play Mode antes de integrar el audio de armas.");
            // Validar todos los recursos antes de modificar cualquier preset.
            foreach (ShotProfile profile in Profiles)
            {
                if (Definition(profile) == null)
                    throw new InvalidOperationException("Falta el preset de arma: " + profile.Id);
                Clips(profile, "OneShot"); Clips(profile, "Stop"); Clips(profile, "Tail");
            }
            foreach (string sound in new[] { "Reload_A", "Reload_B", "Bolt_A", "Bolt_B" }) ReloadClip(sound);
            int integrated = 0;
            foreach (ShotProfile profile in Profiles)
            {
                WeaponDefinition definition = Definition(profile);
                var data = new SerializedObject(definition);
                AudioClip current = Required(data, "shotSound").objectReferenceValue as AudioClip;
                string currentPath = current != null ? AssetDatabase.GetAssetPath(current) : "";
                // Un perfil personalizado ajeno al prototipo y al paquete conserva su configuración.
                if (current != null && !currentPath.StartsWith("Assets/Audio/Weapons/Shot_", StringComparison.Ordinal)
                    && !currentPath.StartsWith(SoundsFolder + "/", StringComparison.Ordinal))
                {
                    Debug.Log("Audio personalizado conservado: " + profile.Id);
                    continue;
                }
                AudioClip[] near = Clips(profile, "OneShot");
                AudioClip[] stop = Clips(profile, "Stop");
                AudioClip[] tail = Clips(profile, "Tail");
                foreach (AudioClip clip in near.Concat(stop).Concat(tail)) ConfigureImporter(clip);
                Required(data, "shotSound").objectReferenceValue = near[0];
                AssignClips(data, "shotVariations", near);
                AssignClips(data, "shotMechanismVariations", stop);
                AssignClips(data, "shotTailVariations", tail);
                Required(data, "shotVolume").floatValue = profile.Near;
                Required(data, "shotMechanismVolume").floatValue = profile.Stop;
                Required(data, "shotTailVolume").floatValue = profile.Tail;
                SerializedProperty reload = Required(data, "reloadSound");
                if (reload.objectReferenceValue != null
                    && AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(reload.objectReferenceValue)) == GeneratedReloadGuid)
                    reload.objectReferenceValue = null;
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(definition);
                integrated++;
                Debug.Log($"WEAPON_AUDIO_PROFILE {profile.Id} source={profile.Prefix} variants={profile.Variants} layers=3 near={profile.Near} stop={profile.Stop} tail={profile.Tail}");
            }
            IntegrateReloads();
            IntegratePlayer();
            AssetDatabase.SaveAssets();
            Debug.Log($"SURVIVE_WEAPON_AUDIO_SETUP=PASS library=ModernWeapons profiles={integrated} reloadLibrary=JUTPS reloadProfiles=8 reloadCues=32 syntheticPrototype=false reloadPlaceholder=false feedback=integrated");
        }

        static AudioClip ReloadClip(string sound)
        {
            string path = $"{ReloadSoundsFolder}/JU_TPS_{sound}.wav";
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            return clip != null ? clip : throw new InvalidOperationException("Falta el corte de audio real de recarga: " + path);
        }

        static void IntegrateReloads()
        {
            foreach (ReloadProfile profile in ReloadProfiles)
            {
                WeaponDefinition definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{DefinitionsFolder}/WD_{profile.Id}.asset");
                bool customCue = definition.ReloadAudioCues.Any(cue => cue != null && cue.Clip != null
                    && !AssetDatabase.GetAssetPath(cue.Clip).StartsWith(ReloadSoundsFolder + "/", StringComparison.Ordinal));
                AudioClip fallback = definition.ReloadSound;
                bool customFallback = fallback != null && AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(fallback)) != GeneratedReloadGuid;
                if (customCue || customFallback)
                {
                    Debug.Log("Recarga personalizada conservada: " + profile.Id);
                    continue;
                }
                var data = new SerializedObject(definition);
                SerializedProperty cues = Required(data, "reloadAudioCues");
                cues.arraySize = profile.Cues.Length;
                for (int index = 0; index < profile.Cues.Length; index++)
                {
                    ReloadCue cue = profile.Cues[index];
                    AudioClip clip = ReloadClip(cue.Sound);
                    ConfigureImporter(clip, true);
                    SerializedProperty entry = cues.GetArrayElementAtIndex(index);
                    entry.FindPropertyRelative("normalizedTime").floatValue = cue.Time;
                    entry.FindPropertyRelative("clip").objectReferenceValue = clip;
                    entry.FindPropertyRelative("volume").floatValue = cue.Volume;
                }
                Required(data, "reloadSound").objectReferenceValue = null;
                Required(data, "reloadVolume").floatValue = profile.Volume;
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(definition);
                Debug.Log($"WEAPON_RELOAD_AUDIO_PROFILE {profile.Id} source=JUTPS cues={profile.Cues.Length} gain={profile.Volume} pitch=1");
            }
        }

        static WeaponDefinition Definition(ShotProfile profile) =>
            AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{DefinitionsFolder}/WD_{profile.Id}.asset");

        static AudioClip[] Clips(ShotProfile profile, string layer)
        {
            var clips = new AudioClip[profile.Variants];
            for (int i = 0; i < clips.Length; i++)
            {
                string path = $"{SoundsFolder}/{profile.Folder}/{profile.Prefix}_{layer}_{i + 1}.wav";
                clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clips[i] == null) throw new InvalidOperationException("Importar el audio original del paquete: " + path);
            }
            return clips;
        }

        static void AssignClips(SerializedObject data, string name, AudioClip[] clips)
        {
            SerializedProperty property = Required(data, name);
            property.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
        }

        static void ConfigureImporter(AudioClip clip, bool shortMechanism = false)
        {
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip)) as AudioImporter;
            if (importer == null) throw new InvalidOperationException("Falta AudioImporter: " + clip.name);
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            // Las colas superpuestas no requieren decodificadores Vorbis por voz ni streaming.
            // ADPCM en memoria conserva la respuesta inmediata sin cargar toda la biblioteca.
            AudioClipLoadType loadType = shortMechanism ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.CompressedInMemory;
            AudioCompressionFormat format = shortMechanism ? AudioCompressionFormat.PCM : AudioCompressionFormat.ADPCM;
            if (settings.loadType == loadType
                && settings.compressionFormat == format && settings.preloadAudioData
                && !importer.forceToMono && !importer.loadInBackground) return;
            settings.loadType = loadType;
            settings.compressionFormat = format;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = false;
            importer.loadInBackground = false;
            importer.SaveAndReimport();
        }

        static void IntegratePlayer()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPath);
            try
            {
                PlayerWeaponController controller = root.GetComponent<PlayerWeaponController>();
                if (controller == null) throw new InvalidOperationException("Falta el controlador de armas del jugador.");
                Transform audioRoot = root.transform.Find("WeaponAudio");
                if (audioRoot == null)
                {
                    audioRoot = new GameObject("WeaponAudio").transform;
                    audioRoot.SetParent(root.transform, false);
                }
                AudioSource shot = EnsureSource(audioRoot, "Shots");
                AudioSource reload = EnsureSource(audioRoot, "Reload");
                PlayerWeaponFeedback feedback = root.GetComponent<PlayerWeaponFeedback>();
                if (feedback == null) feedback = root.AddComponent<PlayerWeaponFeedback>();
                var data = new SerializedObject(feedback);
                Required(data, "weaponController").objectReferenceValue = controller;
                Required(data, "shotSource").objectReferenceValue = shot;
                Required(data, "reloadSource").objectReferenceValue = reload;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static AudioSource EnsureSource(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(parent, false);
            }
            AudioSource source = child.GetComponent<AudioSource>();
            if (source == null) source = child.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = 1f;
            source.pitch = 1f;
            source.priority = 90;
            source.ignoreListenerPause = false;
            return source;
        }

        static SerializedProperty Required(SerializedObject data, string name) => data.FindProperty(name)
            ?? throw new InvalidOperationException("Falta el campo serializado: " + name);
    }
}
