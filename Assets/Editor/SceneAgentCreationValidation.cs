using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cinemachine;
using Survive.Character;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Survive.Editor
{
    /// <summary>Recorre el botón real de Agente con un perfil aislado del usuario.</summary>
    [InitializeOnLoad]
    public static class SceneAgentCreationValidation
    {
        const string Key = "Survive.SceneAgentCreationValidation";
        static readonly string Folder = Path.GetFullPath("Logs/SceneAgentCreation");
        static readonly string ProfilePath = Path.Combine(Folder, "validation-profile.json");
        static readonly List<string> checks = new List<string>();
        static readonly List<string> errors = new List<string>();
        static int stage, failures, lastFrame;
        static double started, next;
        static string original, saved;
        static Canvas[] canvases;
        static bool[] canvasStates;
        static Vector3 agentCameraPosition;

        [Serializable] sealed class Result
        {
            public int passed, failed;
            public string[] checks, runtimeErrors;
        }

        static SceneAgentCreationValidation()
        {
            if (SessionState.GetBool(Key, false)) CharacterProfileStore.ProfilePathOverride = ProfilePath;
            EditorApplication.playModeStateChanged += OnPlay;
            EditorApplication.update += Tick;
        }

        [MenuItem("Survive/Characters/Validar Agente en SceneAlejandro")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salí de Play Mode.");
            Directory.CreateDirectory(Folder);
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
                if (File.Exists(ProfilePath + suffix)) File.Delete(ProfilePath + suffix);
            SessionState.SetBool(Key, true);
            CharacterProfileStore.ProfilePathOverride = ProfilePath;
            EditorSceneManager.OpenScene("Assets/Scenes/SceneAlejandro.unity");
            Helper("PrepareBatchGameView");
            EditorApplication.EnterPlaymode();
        }

        static void OnPlay(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                CharacterProfileStore.ProfilePathOverride = ProfilePath;
                checks.Clear(); errors.Clear(); failures = stage = 0;
                started = EditorApplication.timeSinceStartup;
                next = started + 3;
                Application.logMessageReceived += CaptureError;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                CharacterProfileStore.ProfilePathOverride = null;
                SessionState.SetBool(Key, false);
                if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(Key + ".Exit", 1));
            }
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            Helper("PrepareBatchGameView");
            EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup - started > 180) { Check(false, "Timeout"); Finish(); return; }
            if (EditorApplication.timeSinceStartup < next || Time.frameCount <= lastFrame + 2) return;
            next = EditorApplication.timeSinceStartup + 3;
            lastFrame = Time.frameCount;
            try
            {
                SceneCharacterPreview preview = Object.FindFirstObjectByType<SceneCharacterPreview>();
                CharacterCreationMenu creator = Object.FindFirstObjectByType<CharacterCreationMenu>();
                switch (stage++)
                {
                    case 0:
                        Require(preview != null && preview.Appearance != null, "Sidekick presente en el escenario al iniciar");
                        Check(GameObject.Find("RobotPlayerNoRig") == null, "Prop RobotPlayerNoRig reemplazado");
                        Check(Vector3.Distance(preview.transform.position, new Vector3(-16.309f, -.5f, 6.001f)) < .001f, "Ubicación del prop conservada");
                        Check(preview.Rig.GetComponentsInChildren<Renderer>().Any(r => r.enabled), "Módulos visibles");
                        original = JsonUtility.ToJson(preview.Appearance.AppliedProfile);
                        canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
                        canvasStates = canvases.Select(c => c.enabled).ToArray();
                        Capture("01-menu.png");
                        OpenAgent();
                        break;
                    case 1:
                        Require(creator != null && creator.IsOpen, "Botón Agente abre el creador");
                        Check(SceneManager.GetActiveScene().name == "SceneAlejandro", "Creador permanece en SceneAlejandro");
                        Check(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).All(c => c.name != "CharacterPreviewCamera"), "No se crea cámara de estudio externo");
                        Check(GameObject.Find("CharacterCreationPreview") == null, "No se crea estudio externo");
                        Check(Object.FindObjectsByType<SceneCharacterPreview>(FindObjectsSortMode.None).Length == 1, "Un solo personaje de escenario");
                        Check(CameraPriority("CM_CharacterSelectionCamera") == 10, "Cámara del agente activa");
                        Check(canvases.All(c => !c.enabled), "UI antigua oculta durante creación");
                        Check(Cursor.visible && Cursor.lockState == CursorLockMode.None, "Cursor libre en creador");
                        ValidateFraming(preview);
                        agentCameraPosition = Camera.main.transform.position;
                        Capture("02-agent.png");
                        creator.GetComponentInChildren<TMP_InputField>().text = "Borrador cancelado";
                        FindDropdown(creator, "Género").value = 1;
                        Click(creator, "→");
                        break;
                    case 2:
                        Check(creator.Draft.gender == CharacterGender.Feminine, "Control de género actualiza borrador");
                        Check(preview.Appearance.AppliedProfile.gender == CharacterGender.Feminine, "Género visible en el personaje del escenario");
                        Check(Quaternion.Angle(preview.Rig.transform.localRotation, Quaternion.identity) > 1, "Control de giro rota al agente");
                        ValidateFraming(preview);
                        Check(Vector3.Distance(agentCameraPosition, Camera.main.transform.position) < .0001f, "Cambiar apariencia y girar conserva cámara");
                        Capture("03-customized.png");
                        Click(creator, "CUERPO");
                        creator.GetComponentInChildren<Slider>().value = 60;
                        Check(Mathf.Approximately(preview.Appearance.AppliedProfile.bodySize, 60), "Proporciones de cuerpo actualizan personaje del escenario");
                        Click(creator, "ROPA");
                        TMP_Dropdown outfit = creator.GetComponentsInChildren<TMP_Dropdown>().First(d => d.options.Count > 10);
                        string beforeOutfit = JsonUtility.ToJson(creator.Draft);
                        outfit.value = outfit.value == 1 ? 2 : 1;
                        Check(JsonUtility.ToJson(creator.Draft) != beforeOutfit, "Selector de ropa modifica borrador");
                        Check(JsonUtility.ToJson(preview.Appearance.AppliedProfile) == JsonUtility.ToJson(creator.Draft), "Ropa del borrador aplicada en el escenario");
                        Click(creator, "Volver");
                        break;
                    case 3:
                        Check(creator == null, "Volver cierra el creador");
                        Check(!File.Exists(ProfilePath), "Cancelar no guarda un perfil");
                        Check(JsonUtility.ToJson(preview.Appearance.AppliedProfile) == original, "Cancelar restaura apariencia anterior");
                        Check(Quaternion.Angle(preview.Rig.transform.localRotation, Quaternion.identity) < .01f, "Cancelar restaura giro");
                        Check(CameraPriority("CM_CharacterSelectionCamera") == 0, "Volver restaura cámara principal");
                        Check(canvases.Select((c, i) => c.enabled == canvasStates[i]).All(v => v), "Volver restaura los Canvas");
                        OpenAgent();
                        break;
                    case 4:
                        Require(creator != null, "Agente puede reabrirse");
                        creator.GetComponentInChildren<TMP_InputField>().text = "Agente de prueba";
                        FindDropdown(creator, "Género").value = 1;
                        Click(creator, "GUARDAR AGENTE");
                        break;
                    case 5:
                        Check(creator == null, "Guardar cierra creador");
                        Check(SceneManager.GetActiveScene().name == "SceneAlejandro", "Guardar agente vuelve al menú sin entrar a partida");
                        Require(CharacterProfileStore.TryLoad(out CharacterProfile profile), "Guardar persiste perfil");
                        saved = JsonUtility.ToJson(profile);
                        Check(profile.displayName == "Agente de prueba" && profile.gender == CharacterGender.Feminine, "Nombre y género persistidos");
                        Check(JsonUtility.ToJson(preview.Appearance.AppliedProfile) == saved, "Escenario muestra perfil guardado");
                        OpenAgent();
                        break;
                    case 6:
                        Require(creator != null, "Agente abre aun teniendo perfil guardado");
                        Check(JsonUtility.ToJson(creator.Draft) == saved, "Reabrir carga el perfil existente");
                        creator.GetComponentInChildren<TMP_InputField>().text = "Cambio descartado";
                        Click(creator, "Volver");
                        break;
                    case 7:
                        Check(CharacterProfileStore.TryLoad(out CharacterProfile unchanged) && JsonUtility.ToJson(unchanged) == saved, "Cancelar edición conserva archivo guardado");
                        Check(JsonUtility.ToJson(preview.Appearance.AppliedProfile) == saved, "Cancelar edición restaura apariencia guardada");
                        Check(Object.FindObjectsByType<CharacterCreationMenu>(FindObjectsSortMode.None).Length == 0, "Sin creadores duplicados al cerrar");
                        SceneManager.LoadScene("SceneAlejandro");
                        break;
                    case 8:
                        Require(preview != null && preview.Appearance != null, "Vista previa se recrea al recargar escena");
                        Check(JsonUtility.ToJson(preview.Appearance.AppliedProfile) == saved, "Recargar escena recupera perfil persistido");
                        OpenAgent();
                        break;
                    case 9:
                        Check(JsonUtility.ToJson(creator.Draft) == saved, "Agente recupera perfil después de recargar");
                        Click(creator, "Volver");
                        File.Delete(ProfilePath);
                        if (File.Exists(ProfilePath + ".bak")) File.Delete(ProfilePath + ".bak");
                        break;
                    case 10:
                        Button play = FindButton("StartGame");
                        Require(play != null, "Botón Jugar conectado");
                        Helper("ClickThroughEventSystem", play, "Jugar sin perfil");
                        break;
                    case 11:
                        Require(creator != null && creator.IsOpen, "Jugar sin perfil abre creador integrado");
                        Check(GameObject.Find("CharacterCreationPreview") == null, "Jugar usa el escenario existente");
                        Click(creator, "CREAR Y JUGAR");
                        break;
                    case 12:
                        Check(SceneManager.GetActiveScene().name == "Level 1", "Crear y jugar carga Level 1");
                        Check(CharacterProfileStore.TryLoad(out _), "Crear y jugar guarda perfil");
                        Check(Object.FindFirstObjectByType<SceneCharacterPreview>() == null, "Vista previa se descarga con el menú");
                        Check(Object.FindFirstObjectByType<CharacterCreationMenu>() == null, "Creador se descarga al jugar");
                        Finish();
                        break;
                }
            }
            catch (Exception exception) { Check(false, exception.ToString()); Finish(); }
        }

        static Button FindButton(string method) => Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
            .FirstOrDefault(b => Enumerable.Range(0, b.onClick.GetPersistentEventCount()).Any(i => b.onClick.GetPersistentMethodName(i) == method));
        static void OpenAgent()
        {
            Button button = FindButton("ShowCharacterSelect");
            Require(button != null, "Botón Agente enlazado a ShowCharacterSelect");
            Helper("ClickThroughEventSystem", button, "Agente");
        }
        static int CameraPriority(string name) => GameObject.Find(name).GetComponent<CinemachineVirtualCamera>().Priority;
        static TMP_Dropdown FindDropdown(CharacterCreationMenu creator, string row) => creator.GetComponentsInChildren<TMP_Dropdown>()
            .First(d => d.transform.parent.name == row);
        static void Click(CharacterCreationMenu creator, string title)
        {
            Button button = creator.GetComponentsInChildren<Button>().First(b => b.GetComponentInChildren<TMP_Text>()?.text == title);
            Helper("ClickThroughEventSystem", button, title);
        }
        static void Capture(string filename)
        {
            RenderTexture frame = (RenderTexture)Helper("GameViewFramebuffer");
            if (frame == null) throw new InvalidOperationException("Framebuffer de GameView ausente.");
            RenderTexture previous = RenderTexture.active;
            var image = new Texture2D(frame.width, frame.height, TextureFormat.RGB24, false);
            try
            {
                RenderTexture.active = frame;
                image.ReadPixels(new Rect(0, 0, frame.width, frame.height), 0, 0);
                // El readback D3D del framebuffer usa el origen vertical superior.
                if (SystemInfo.graphicsUVStartsAtTop)
                {
                    Color32[] pixels = image.GetPixels32();
                    for (int row = 0; row < image.height / 2; row++)
                    {
                        int upper = row * image.width, lower = (image.height - 1 - row) * image.width;
                        for (int column = 0; column < image.width; column++)
                        {
                            Color32 pixel = pixels[upper + column];
                            pixels[upper + column] = pixels[lower + column];
                            pixels[lower + column] = pixel;
                        }
                    }
                    image.SetPixels32(pixels);
                }
                image.Apply();
                File.WriteAllBytes(Path.Combine(Folder, filename), image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
        }
        static void ValidateFraming(SceneCharacterPreview preview)
        {
            Camera camera = Camera.main;
            var points = new List<Vector3>();
            var mesh = new Mesh();
            try
            {
                foreach (SkinnedMeshRenderer renderer in preview.Rig.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (!renderer.enabled || renderer.sharedMesh == null) continue;
                    mesh.Clear(); renderer.BakeMesh(mesh);
                    points.AddRange(mesh.vertices.Select(v => camera.WorldToViewportPoint(renderer.transform.TransformPoint(v))));
                }
            }
            finally { Object.DestroyImmediate(mesh); }
            Require(points.Count > 0, "Geometría real del agente presente para comprobar encuadre");
            float minX = points.Min(p => p.x), maxX = points.Max(p => p.x);
            float minY = points.Min(p => p.y), maxY = points.Max(p => p.y);
            Check(points.All(p => p.z > camera.nearClipPlane) && minX >= .01f && maxX <= .64f && minY >= .11f && maxY <= .99f,
                $"Cuerpo completo fuera del panel lateral: x={minX:F3}..{maxX:F3} y={minY:F3}..{maxY:F3}");
            Check(maxY - minY >= .4f, "Agente ocupa al menos 40% de la altura de pantalla");
        }
        static object Helper(string name, params object[] args) => typeof(CharacterCustomizationValidation)
            .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
        static void Check(bool ok, string text)
        {
            checks.Add((ok ? "PASS " : "FAIL ") + text);
            if (ok) Debug.Log("SCENE_AGENT_PASS " + text);
            else { failures++; Debug.LogWarning("SCENE_AGENT_FAIL " + text); }
        }
        static void Require(bool ok, string text) { Check(ok, text); if (!ok) throw new InvalidOperationException(text); }
        static void CaptureError(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message + "\n" + stack);
        }
        static void Finish()
        {
            Application.logMessageReceived -= CaptureError;
            File.WriteAllText(Path.Combine(Folder, "result.json"), JsonUtility.ToJson(new Result
            { passed = checks.Count - failures, failed = failures, checks = checks.ToArray(), runtimeErrors = errors.ToArray() }, true));
            SessionState.SetInt(Key + ".Exit", failures == 0 && errors.Count == 0 ? 0 : 1);
            CharacterProfileStore.ProfilePathOverride = null;
            EditorApplication.ExitPlaymode();
        }
    }
}
