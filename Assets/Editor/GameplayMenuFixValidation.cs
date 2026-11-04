using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Survive.Character;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Survive.Editor
{
    [InitializeOnLoad]
    public static class GameplayMenuFixValidation
    {
        const string Key = "Survive.GameplayMenuFixValidation";
        static readonly List<string> checks = new List<string>(), errors = new List<string>(), baselineErrors = new List<string>();
        static readonly HashSet<int> existing = new HashSet<int>();
        static int stage, failures, frame; static double started, next;
        static MenuBehaviour menu; static EnemySpawner waves; static BossBase boss;
        static float waveSpeed; static Keyboard keyboard; static Mouse mouse; static Vector2 buttonPoint;
        static InputSettings previousSettings, settings;
        static InputActionAsset isolatedUiActions;
        [Serializable] class Result
        {
            public int passed, failed; public string scope = "Play real de SceneAlejandro a Level 2, ESC, botón Continuar con mouse virtual, reinicio y vuelta al menú; boss generado por oleadas e invocación real de esbirros, antes y después de furia. Sin build distribuido.";
            public string[] checks, errors, baselineErrors;
        }
        static GameplayMenuFixValidation()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Key, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    checks.Clear(); errors.Clear(); baselineErrors.Clear(); existing.Clear(); stage = failures = 0;
                    started = EditorApplication.timeSinceStartup; next = started + 3; Application.runInBackground = true;
                    CharacterProfileStore.ProfilePathOverride = Path.GetFullPath(".utmp/GameplayMenuFixValidation/character-profile.json");
                    previousSettings = InputSystem.settings; settings = Object.Instantiate(previousSettings);
                    settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    InputSystem.settings = settings; keyboard = InputSystem.AddDevice<Keyboard>("MenuFixKeyboard"); mouse = InputSystem.AddDevice<Mouse>("MenuFixMouse");
                    Application.logMessageReceived += OnLog;
                }
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    SessionState.SetBool(Key,false);
                    string original = SessionState.GetString(Key + ".Scene", "");
                    if (!Application.isBatchMode && File.Exists(original)) EditorSceneManager.OpenScene(original,OpenSceneMode.Single);
                    if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(Key + ".Exit",1));
                }
            };
        }
        [MenuItem("Survive/Gameplay/Validar Play pausa y velocidad de esbirros")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Salí de Play Mode.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(GameplayMenuFixSetup.Folder);
            SessionState.SetString(Key + ".Scene",SceneManager.GetActiveScene().path);
            EditorSceneManager.OpenScene("Assets/Scenes/SceneAlejandro.unity",OpenSceneMode.Single);
            Prepare(); SessionState.SetBool(Key,true); EditorApplication.EnterPlaymode();
        }
        static void Prepare() => typeof(CharacterCustomizationValidation).GetMethod("PrepareBatchGameView",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
        static void Tick()
        {
            if (!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying) return;
            Prepare(); EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup - started > 180) { Check(false,"Tiempo máximo, etapa " + stage); Finish(); return; }
            if (EditorApplication.timeSinceStartup < next || Time.frameCount <= frame + 3) return;
            frame = Time.frameCount; next = EditorApplication.timeSinceStartup + .5;
            try
            {
                switch(stage)
                {
                    case 0:
                        var catalog = SidekickCharacterCatalog.Load(); var profile = catalog.CreateDefault();
                        Check(CharacterProfileStore.TrySave(profile,out _),"Perfil de prueba aislado del perfil real");
                        menu = Object.FindObjectsByType<MenuBehaviour>(FindObjectsSortMode.None).Single(m=>m.IsMainMenu);
                        Check(new SerializedObject(menu).FindProperty("gameplayScene").stringValue == "Level 2","Play apunta a Level 2");
                        Check(Application.CanStreamedLevelBeLoaded("Level 2"),"Level 2 habilitada en Build Settings");
                        Object.FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                            .Single(b=>Enumerable.Range(0,b.onClick.GetPersistentEventCount()).Any(i=>b.onClick.GetPersistentMethodName(i)=="StartGame")).onClick.Invoke();
                        stage++; next += 3; break;
                    case 1:
                        Check(SceneManager.GetActiveScene().name == "Level 2","Botón Play real carga Level 2");
                        menu = Object.FindFirstObjectByType<MenuBehaviour>();
                        Require(menu != null && !menu.IsMainMenu && menu.container != null,"Level 2 tiene gestor y panel de pausa");
                        Check(!menu.IsPaused && !menu.container.activeSelf && Time.timeScale == 1,"Partida comienza sin pausa");
                        Check(Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length == 1,"Un único EventSystem activo");
                        var module = Object.FindFirstObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                        isolatedUiActions = Object.Instantiate(module.actionsAsset);
                        isolatedUiActions.devices = new InputDevice[] { keyboard, mouse };
                        module.actionsAsset = isolatedUiActions;
                        InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.Escape)); stage++; break;
                    case 2:
                        Require(menu.IsPaused && menu.container.activeInHierarchy && Time.timeScale == 0,"ESC abre pausa y detiene el juego");
                        Check(menu.GetComponent<Canvas>().enabled && menu.GetComponent<Canvas>().sortingOrder > 95,"Pausa visible por encima del HUD Synty");
                        Check(Cursor.visible && Cursor.lockState == CursorLockMode.None,"Pausa libera cursor");
                        Capture(); InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                        Canvas.ForceUpdateCanvases();
                        buttonPoint = RectTransformUtility.WorldToScreenPoint(null,menu.container.transform.Find("Menu/Continue").position);
                        var hits = new List<UnityEngine.EventSystems.RaycastResult>();
                        UnityEngine.EventSystems.EventSystem.current.RaycastAll(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = buttonPoint },hits);
                        Require(hits.Count > 0 && hits[0].gameObject.name == "Continue","Raycast de UI apunta al botón Continuar");
                        InputSystem.QueueStateEvent(mouse,new MouseState { position = buttonPoint }); stage++; break;
                    case 3:
                        InputSystem.QueueStateEvent(mouse,new MouseState { position = buttonPoint }.WithButton(MouseButton.Left)); stage++; break;
                    case 4:
                        InputSystem.QueueStateEvent(mouse,new MouseState { position = buttonPoint }); stage++; break;
                    case 5:
                        Require(!menu.IsPaused && !menu.container.activeSelf && Time.timeScale == 1,"Click real en Continuar reanuda la partida");
                        InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.Escape)); stage++; break;
                    case 6:
                        Check(menu.IsPaused,"ESC vuelve a abrir pausa"); InputSystem.QueueStateEvent(keyboard,new KeyboardState()); stage++; break;
                    case 7:
                        InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.Escape)); stage++; break;
                    case 8:
                        Require(!menu.IsPaused && Time.timeScale == 1,"ESC también reanuda"); InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                        var player = Object.FindFirstObjectByType<PlayerHealth>();
                        typeof(PlayerHealth).GetField("currentHealth",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(player,100000f);
                        waves = Object.FindFirstObjectByType<EnemySpawner>();
                        foreach(var h in waves.AliveEnemies.ToArray()) if(h != null) h.TakeDamage(999999);
                        waves.CancelWaves(); var data = new SerializedObject(waves);
                        data.FindProperty("minWavesBeforeBoss").intValue = data.FindProperty("maxWavesBeforeBoss").intValue = 2;
                        data.ApplyModifiedPropertiesWithoutUndo(); waves.OnWaveStarted += (i,w)=>waveSpeed=w.speedMultiplier;
                        waves.ResetAndRestartWaves(); stage++; break;
                    case 9:
                        foreach(var h in waves.AliveEnemies.ToArray()) if(h != null && !h.IsDead) h.TakeDamage(999999);
                        if (waves.State == WaveRunState.Failed) throw new Exception(waves.LastError);
                        boss = Object.FindFirstObjectByType<BossBase>(); if(boss == null) break;
                        foreach(var h in Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None)) existing.Add(h.GetInstanceID());
                        stage++; next += 6; break;
                    case 10:
                        var minions = NewMinions(); if(minions.Length == 0) break;
                        CheckMinions(minions,"Invocación temporizada");
                        boss.Health.TakeDamage(boss.Health.CurrentHealth * .6f); stage++; break;
                    case 11:
                        foreach(var h in NewMinions()) { existing.Add(h.GetInstanceID()); Object.Destroy(h.gameObject); }
                        next += .6; stage++; break;
                    case 12:
                        typeof(BossBase).GetMethod("SummonMinions",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(boss,null);
                        stage++; next += 1; break;
                    case 13:
                        var rageMinions = NewMinions(); Require(rageMinions.Length > 0,"Boss en furia sigue invocando esbirros");
                        CheckMinions(rageMinions,"Invocación durante furia");
                        menu.PauseGame(); menu.container.transform.Find("Menu/Restart").GetComponent<Button>().onClick.Invoke();
                        stage++; next += 3; break;
                    case 14:
                        Check(SceneManager.GetActiveScene().name == "Level 2" && Time.timeScale == 1,"Reiniciar conserva Level 2 y sale de pausa");
                        menu = Object.FindFirstObjectByType<MenuBehaviour>(); menu.PauseGame();
                        menu.container.transform.Find("Menu/MainMenu").GetComponent<Button>().onClick.Invoke(); stage++; next += 3; break;
                    case 15:
                        Check(SceneManager.GetActiveScene().name == "SceneAlejandro" && Time.timeScale == 1,"Botón Menú principal vuelve a SceneAlejandro");
                        Check(errors.Count == 0,"Sin errores nuevos de ejecución"); Finish(); break;
                }
            }
            catch(Exception e) { Check(false,e.ToString()); Finish(); }
        }
        static EnemyHealth[] NewMinions() => Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None).Where(h=>!h.IsDead && h.GetComponent<BossBase>()==null && !existing.Contains(h.GetInstanceID())).ToArray();
        static void CheckMinions(EnemyHealth[] minions,string label)
        {
            var prefab = (GameObject)new SerializedObject(boss).FindProperty("minionPrefab").objectReferenceValue;
            float expected = prefab.GetComponent<EnemyBaseController>().MoveSpeed * waveSpeed;
            Check(waveSpeed < 1 && expected > 0,label + ": multiplicador de la oleada " + waveSpeed);
            foreach(var h in minions)
            {
                var a=h.GetComponent<NavMeshAgent>(); var c=h.GetComponent<EnemyBaseController>();
                Check(Mathf.Approximately(c.MoveSpeed,expected) && Mathf.Approximately(a.speed,expected),label + ": IA y agente a " + expected + " m/s");
                Check(a.isOnNavMesh && h.GetComponent<EnemyReachability>()!=null,label + ": navegación y recuperación configuradas");
            }
        }
        static void Capture()
        {
            var rt=(RenderTexture)typeof(CharacterCustomizationValidation).GetMethod("GameViewFramebuffer",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
            if(rt==null) throw new Exception("No hay framebuffer de GameView.");
            var old=RenderTexture.active; RenderTexture.active=rt; var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); image.Apply();
            if(SystemInfo.graphicsUVStartsAtTop)
            {
                var pixels=image.GetPixels32(); var flip=new Color32[pixels.Length];
                for(int y=0;y<rt.height;y++) Array.Copy(pixels,y*rt.width,flip,(rt.height-y-1)*rt.width,rt.width);
                image.SetPixels32(flip); image.Apply();
            }
            File.WriteAllBytes(GameplayMenuFixSetup.Folder + "/pause-level2.png",image.EncodeToPNG()); Object.DestroyImmediate(image); RenderTexture.active=old;
        }
        static void Check(bool pass,string label) { checks.Add((pass?"PASS ":"FAIL ")+label); if(!pass) failures++; }
        static void Require(bool pass,string label) { Check(pass,label); if(!pass) throw new Exception(label); }
        static void OnLog(string message,string trace,LogType type)
        {
            if(type!=LogType.Error && type!=LogType.Exception && type!=LogType.Assert) return;
            // Los dos assets rotos ya emiten estos errores al abrir las escenas antes de la configuración.
            bool knownAsset = message=="Unknown error occurred while loading 'Assets/New Terrain 3.asset'."
                || message=="Problem detected while opening the Scene file: 'Assets/Scenes/SceneAlejandro.unity'.\nCheck the following logs for more details."
                || message=="Prefab instance problem. Missing Prefab Asset: 'LevelLoader (Missing Prefab with guid: e03da6a7ced7b0f45b5982deba906d5c)'";
            bool editorNetwork = message.StartsWith("SocketException:") && trace.Contains("System.Net.");
            if(knownAsset || editorNetwork) baselineErrors.Add(message); else errors.Add(message);
        }
        static void Finish()
        {
            Application.logMessageReceived-=OnLog;
            File.WriteAllText(GameplayMenuFixSetup.Folder + "/validation.json",JsonUtility.ToJson(new Result { passed=checks.Count-failures,failed=failures,checks=checks.ToArray(),errors=errors.ToArray(),baselineErrors=baselineErrors.ToArray() },true));
            CharacterProfileStore.ProfilePathOverride=null;
            if(keyboard!=null && keyboard.added) InputSystem.RemoveDevice(keyboard); if(mouse!=null && mouse.added) InputSystem.RemoveDevice(mouse);
            if(previousSettings!=null) InputSystem.settings=previousSettings; if(settings!=null) Object.DestroyImmediate(settings);
            if(isolatedUiActions!=null) Object.DestroyImmediate(isolatedUiActions);
            Time.timeScale=1; SessionState.SetInt(Key+".Exit",failures==0?0:1);
            Debug.Log("GAMEPLAY_MENU_FIX_VALIDATION="+(failures==0?"PASS":"FAIL")+" "+(checks.Count-failures)+"/"+checks.Count);
            EditorApplication.ExitPlaymode();
        }
    }
}
