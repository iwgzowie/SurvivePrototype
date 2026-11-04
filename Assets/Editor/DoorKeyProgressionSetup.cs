using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Survive.Editor
{
    public static class DoorKeyProgressionSetup
    {
        public const string Level = "Assets/Scenes/Level 2.unity", Folder = "Logs/DoorProgression";
        public static readonly string[] Names = { "PuertaZona2", "PuertaZona3", "PuertaZona4", "PuertaZona4.1", "PuertaZona5" };
        [MenuItem("Survive/Boss/Configurar secuencia de puertas y llaves")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salí de Play Mode.");
            Directory.CreateDirectory(Folder);
            Scene scene = SceneManager.GetSceneByPath(Level); bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(Level, Application.isBatchMode ? OpenSceneMode.Single : OpenSceneMode.Additive);
            PuertaConLlave[] all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PuertaConLlave>(true)).ToArray();
            PuertaConLlave[] doors = Names.Select(name => all.Single(d => d.name == name)).ToArray();
            Set(doors[0], Array.Empty<PuertaConLlave>(), Array.Empty<PuertaConLlave>());
            Set(doors[1], new[] { doors[0] }, Array.Empty<PuertaConLlave>());
            Set(doors[2], new[] { doors[1] }, new[] { doors[3] });
            Set(doors[3], new[] { doors[1] }, new[] { doors[2] });
            Set(doors[4], new[] { doors[2], doors[3] }, Array.Empty<PuertaConLlave>());
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            if (opened && !Application.isBatchMode) EditorSceneManager.CloseScene(scene, true);
            File.WriteAllText(Folder + "/configuration.txt", "PuertaZona2: primera apertura, una llave.\nPuertaZona3: requiere PuertaZona2 abierta y otra llave.\nPuertaZona4 / PuertaZona4.1: requieren PuertaZona3; abrir cualquiera abre ambas con una llave.\nPuertaZona5: requiere las dos PuertaZona4 abiertas y otra llave.\n");
            Debug.Log("DOOR_PROGRESSION_SETUP_PASS");
        }
        static void Set(PuertaConLlave door, PuertaConLlave[] requirements, PuertaConLlave[] peers)
        {
            var data = new SerializedObject(door); data.FindProperty("keyConsume").boolValue = true;
            References(data.FindProperty("prerequisiteDoors"), requirements); References(data.FindProperty("linkedDoors"), peers);
            data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(door);
        }
        static void References(SerializedProperty property, PuertaConLlave[] values)
        { property.arraySize = values.Length; for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; }
    }
}
