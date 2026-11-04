using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Survive.Combat;
using Survive.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UPP.ThirdPersonController;
using UPP.ThirdPersonController.CameraSystems;
using Object = UnityEngine.Object;

namespace Survive.Editor
{
    [InitializeOnLoad] public static class SyntyResultsValidation
    {
        const string Key="Survive.SyntyResultsValidation";
        static readonly List<string> checks=new List<string>(),errors=new List<string>(),baseline=new List<string>();
        static int stage,failures,frame,frozenScore; static double start,next;static float normalOffset;
        static MenuBehaviour menu;static SyntyResultsUI ui;static LeaderboardManager leaderboard;static GameStatsManager stats;
        static GameObject player,target;static UPPCharacterMovementComponent movement;static UPPTPSCameraController rig;static PlayerWeaponController weapons;static SyntyGameplayHUD hud;
        static Keyboard keyboard;static InputSettings originalSettings,settings;
        [Serializable] class Result { public int passed,failed;public string scope="Level 2 real: desplazamiento de cámara normal/apuntado, disparo e impacto reales, bajas normal/esbirro/boss, HUD y resultado final iguales, derrota, guardado/reinicio/persistencia/ESC y clasificación desde menú principal. PlayerPrefs de prueba aislados. Sin build distribuido.";public string[] checks,errors,baselineErrors; }
        static SyntyResultsValidation()
        {
            if(SessionState.GetBool(Key,false)) LeaderboardManager.StoragePrefixOverride=SessionState.GetString(Key+".Prefix","");
            EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false)) return;
                if(state==PlayModeStateChange.EnteredPlayMode)
                {
                    checks.Clear();errors.Clear();baseline.Clear();stage=failures=0;start=EditorApplication.timeSinceStartup;next=start+3;Application.runInBackground=true;
                    originalSettings=InputSystem.settings;settings=Object.Instantiate(originalSettings);settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                    settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings=settings;
                    keyboard=InputSystem.AddDevice<Keyboard>("ResultsKeyboard");Application.logMessageReceived+=OnLog;
                }
                if(state==PlayModeStateChange.EnteredEditMode)
                { SessionState.SetBool(Key,false);LeaderboardManager.StoragePrefixOverride=null;if(Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(Key+".Exit",1)); }
            };
        }
        [MenuItem("Survive/UI/Validar cámara derrota HUD y clasificación")]
        public static void Run()
        {
            if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(SyntyResultsSetup.Folder);EditorSceneManager.OpenScene(DoorKeyProgressionSetup.Level,OpenSceneMode.Single);
            SessionState.SetString(Key+".Prefix","CodexResults_"+Guid.NewGuid().ToString("N")+"_");
            LeaderboardManager.StoragePrefixOverride=SessionState.GetString(Key+".Prefix","");
            Prepare();SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void Prepare()=>typeof(CharacterCustomizationValidation).GetMethod("PrepareBatchGameView",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying) return;
            Prepare();EditorApplication.QueuePlayerLoopUpdate();if(EditorApplication.timeSinceStartup-start>180){Check(false,"Timeout etapa "+stage);Finish();return;}
            if(EditorApplication.timeSinceStartup<next||Time.frameCount<=frame+3) return;frame=Time.frameCount;next=EditorApplication.timeSinceStartup+.5;
            try
            {
                switch(stage)
                {
                    case 0:
                        stats=GameStatsManager.Instance;Require(stats!=null&&stats.IsGameActive,"Level 2 inicia estadísticas activas");
                        Require(LeaderboardManager.StoragePrefixOverride==SessionState.GetString(Key+".Prefix",""),"Persistencia de prueba aislada antes de guardar");
                        var waves=Object.FindFirstObjectByType<EnemySpawner>();foreach(var h in waves.AliveEnemies.ToArray()) if(h!=null)h.TakeDamage(999999);waves.CancelWaves();stats.ResetStats();
                        player=Object.FindFirstObjectByType<PlayerHealth>().gameObject;movement=player.GetComponent<UPPCharacterMovementComponent>();movement.UseDefaultControllerInput=false;movement.IsAiming=false;movement.FiringMode=false;
                        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(-500,49.5f,-500);floor.transform.localScale=new Vector3(80,1,80);
                        player.transform.position=new Vector3(-500,50.05f,-500);var body=player.GetComponent<Rigidbody>();body.position=player.transform.position;body.constraints=RigidbodyConstraints.FreezeAll;
                        rig=Object.FindFirstObjectByType<UPPTPSCameraController>();rig.EnableAutoRotator=false;
                        weapons=player.GetComponent<PlayerWeaponController>();VendorTurretSetup.Set(weapons,"requireLockedCursor",false);
                        hud=Object.FindFirstObjectByType<SyntyGameplayHUD>();BindUi();stage++;next+=1.2;break;
                    case 1:
                        normalOffset=Lateral();Check(Mathf.Abs(normalOffset-.5f)<.08f,"Cámara normal conserva offset de 0.5 m");Capture("01-normal-camera.png");
                        movement.IsAiming=true;movement.FiringMode=true;stage++;next+=1.2;break;
                    case 2:
                        Check(rig.AimCameraState.RightCameraOffset==1 && Lateral()>normalOffset+.35f,"Apuntado desplaza cámara a la derecha hasta 1 m");Capture("02-aim-camera-right.png");
                        target=GameObject.CreatePrimitive(PrimitiveType.Cube);target.name="ScoreHitTarget";target.transform.position=rig.mCamera.transform.position+rig.mCamera.transform.forward*8;target.transform.localScale=Vector3.one*2;
                        target.AddComponent<EnemyHealth>().ApplyWaveMultipliers(50);Physics.SyncTransforms();stage++;break;
                    case 3:
                        Require(weapons.TryFire(),"Disparo real del arma equipada");
                        Check(stats.shotsFired==1&&stats.shotsHit==1,"Disparo e impacto cuentan una vez");
                        target.GetComponent<EnemyHealth>().TakeDamage(999999,default,player);
                        var minion=new GameObject("ScoreMinion").AddComponent<EnemyHealth>();minion.MarkAsBossMinion();minion.TakeDamage(999999,default,player);
                        var boss=new GameObject("ScoreBoss");boss.AddComponent<EnemyHealth>();boss.AddComponent<BossBase>();boss.GetComponent<EnemyHealth>().TakeDamage(999999,default,player);
                        Check(stats.enemiesKilled==3&&stats.combatScore==1150,"Zombi 100, esbirro 50 y boss 1000 puntos");
                        target.GetComponent<EnemyHealth>().TakeDamage(999999,default,player);Check(stats.enemiesKilled==3,"Golpear un cadáver no duplica puntos");
                        Object.Destroy(target);stage++;next+=.7;break;
                    case 4:
                        Require(weapons.TryFire(),"Segundo disparo real sin objetivo");Check(stats.shotsFired==2&&stats.shotsHit==1&&Mathf.Approximately(stats.Accuracy,50),"Fallo no aumenta impactos: precisión 50 %");
                        movement.IsAiming=false;movement.FiringMode=false;stage++;next+=1;break;
                    case 5:
                        Check(Mathf.Abs(Lateral()-.5f)<.08f,"Soltar apuntado devuelve offset normal");
                        stats.waveReached=3;stage++;break;
                    case 6:
                        Time.timeScale=0;frozenScore=stats.totalScore;stage++;break;
                    case 7:
                        Check(Read<TMP_Text>(hud,"scoreText").text=="PUNTAJE  "+frozenScore.ToString("N0"),"HUD muestra puntaje vivo de la misma fuente");
                        Check(frozenScore==stats.CalculateScore()&&frozenScore>1900,"Puntaje vivo incluye combate, oleada y tiempo");Capture("03-live-score-hud.png");
                        Time.timeScale=1;player.GetComponent<PlayerHealth>().TakeDamage(999999);stage++;break;
                    case 8:
                        Require(menu.IsGameOver&&menu.gameOverContainer.activeInHierarchy,"Muerte real abre menú de derrota");
                        Check(Time.timeScale==0&&Cursor.visible&&Cursor.lockState==CursorLockMode.None,"Derrota detiene partida y libera cursor");
                        Check(!stats.IsGameActive&&ui.PendingScore==frozenScore&&stats.totalScore==frozenScore,"Derrota congela exactamente el puntaje del HUD");
                        Check(Read<TMP_Text>(ui,"scoreText").text==frozenScore.ToString("N0"),"Resultado final coincide con estadísticas");
                        stats.AddBossKill();stats.FinalizeGameStats();Check(stats.totalScore==frozenScore,"Bajas tardías y finalización repetida no alteran resultado");
                        Read<TMP_InputField>(ui,"nameInput").text="CODEX_VALIDACION";Capture("04-defeat-synty.png");
                        Read<Button>(ui,"saveButton").onClick.Invoke();stage++;break;
                    case 9:
                        Require(ui.IsLeaderboardOpen&&ui.ScoreSubmitted,"Guardar abre clasificación Synty");
                        Check(leaderboard.GetScores().Count(s=>s.playerName=="CODEX_VALIDACION"&&s.score==frozenScore)==1,"Tabla recibe nombre y puntaje exactos");
                        ui.SaveScore();Check(leaderboard.GetScores().Count(s=>s.playerName=="CODEX_VALIDACION")==1,"Guardar repetidamente no duplica entrada");
                        Check(leaderboard.GetScores().Count<=10&&leaderboard.GetScores().Zip(leaderboard.GetScores().Skip(1),(a,b)=>a.score>=b.score).All(v=>v),"Tabla ordenada de mayor a menor y limitada a 10");
                        Check(Read<Transform>(ui,"rows").GetComponentsInChildren<ScoreRowUI>().Length==leaderboard.GetScores().Count,"Filas visibles conectadas a datos persistidos");Capture("05-leaderboard-synty.png");PressEsc();stage++;break;
                    case 10:
                        Release();Require(!ui.IsLeaderboardOpen&&menu.gameOverContainer.activeInHierarchy&&Time.timeScale==0,"ESC vuelve de clasificación a derrota sin reanudar");
                        ui.transform.Find("DefeatPanel/DefeatCard/Restart").GetComponent<Button>().onClick.Invoke();stage++;next+=3;break;
                    case 11:
                        Require(SceneManager.GetActiveScene().name=="Level 2","Reintentar carga Level 2");BindUi();stats=GameStatsManager.Instance;
                        Check(!menu.IsFinished&&Time.timeScale==1&&stats.IsGameActive&&stats.enemiesKilled==0,"Reintentar inicia partida y estadísticas nuevas");
                        Check(leaderboard.GetScores().Count(s=>s.playerName=="CODEX_VALIDACION"&&s.score==frozenScore)==1,"Leaderboard persiste al recargar escena");
                        menu.PauseGame();menu.container.transform.Find("Menu/Leaderboard").GetComponent<Button>().onClick.Invoke();stage++;break;
                    case 12:
                        Require(ui.IsLeaderboardOpen&&menu.IsPaused,"Clasificación accesible desde pausa");PressEsc();stage++;break;
                    case 13:
                        Release();Check(!ui.IsLeaderboardOpen&&menu.IsPaused&&menu.container.activeInHierarchy&&Time.timeScale==0,"ESC vuelve a pausa sin perder su estado");
                        menu.container.transform.Find("Menu/MainMenu").GetComponent<Button>().onClick.Invoke();stage++;next+=3;break;
                    case 14:
                        Require(SceneManager.GetActiveScene().name=="SceneAlejandro","Menú principal disponible después de partida");BindUi();
                        Check(!GameStatsManager.Instance.IsGameActive,"Tiempo de partida no sigue sumando en menú principal");
                        Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.gameObject.activeInHierarchy
                            &&Enumerable.Range(0,b.onClick.GetPersistentEventCount()).Any(i=>b.onClick.GetPersistentMethodName(i)=="OpenLeaderboard")).onClick.Invoke();stage++;break;
                    case 15:
                        Check(ui.IsLeaderboardOpen&&leaderboard.GetScores().Any(s=>s.playerName=="CODEX_VALIDACION"),"Clasificación accesible desde menú principal con marca guardada");Capture("06-main-menu-leaderboard.png");
                        string sentinel=SessionState.GetString(Key+".Prefix","")+"OtherSetting";PlayerPrefs.SetInt(sentinel,17);leaderboard.ClearLeaderboard();
                        Check(PlayerPrefs.GetInt(sentinel)==17,"Limpiar leaderboard conserva otras preferencias");PlayerPrefs.DeleteKey(sentinel);
                        Check(errors.Count==0,"Sin errores nuevos de ejecución");Finish();break;
                }
            }
            catch(Exception e){Check(false,e.ToString());Finish();}
        }
        static void BindUi(){menu=Object.FindFirstObjectByType<MenuBehaviour>();ui=Object.FindFirstObjectByType<SyntyResultsUI>();leaderboard=Object.FindFirstObjectByType<LeaderboardManager>();}
        static float Lateral()=>Vector3.Dot(rig.mCamera.transform.position-rig.mCamera.transform.parent.position,rig.mCamera.transform.right);
        static void PressEsc(){keyboard.MakeCurrent();InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.Escape));}
        static void Release()=>InputSystem.QueueStateEvent(keyboard,new KeyboardState());
        static T Read<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(target);
        static void Capture(string name)
        {
            var rt=(RenderTexture)typeof(CharacterCustomizationValidation).GetMethod("GameViewFramebuffer",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);if(rt==null)return;
            var old=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();
            if(SystemInfo.graphicsUVStartsAtTop){var p=image.GetPixels32();var f=new Color32[p.Length];for(int y=0;y<rt.height;y++)Array.Copy(p,y*rt.width,f,(rt.height-y-1)*rt.width,rt.width);image.SetPixels32(f);image.Apply();}
            File.WriteAllBytes(SyntyResultsSetup.Folder+"/"+name,image.EncodeToPNG());Object.DestroyImmediate(image);RenderTexture.active=old;
        }
        static void Check(bool pass,string label){checks.Add((pass?"PASS ":"FAIL ")+label);if(!pass)failures++;}
        static void Require(bool pass,string label){Check(pass,label);if(!pass)throw new Exception(label);}
        static void OnLog(string message,string trace,LogType type)
        {
            if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
            if(message=="Unknown error occurred while loading 'Assets/New Terrain 3.asset'."||message.Contains("e03da6a7ced7b0f45b5982deba906d5c")||message.StartsWith("Problem detected while opening the Scene file: 'Assets/Scenes/SceneAlejandro.unity'.")||message.StartsWith("SocketException:")&&trace.Contains("System.Net."))baseline.Add(message);else errors.Add(message);
        }
        static void Finish()
        {
            Application.logMessageReceived-=OnLog;
            string prefix=SessionState.GetString(Key+".Prefix","");
            if(prefix.StartsWith("CodexResults_")&&LeaderboardManager.StoragePrefixOverride==prefix)
            {
                Object.FindFirstObjectByType<LeaderboardManager>()?.ClearLeaderboard();PlayerPrefs.DeleteKey(prefix+"Count");PlayerPrefs.Save();
            }
            File.WriteAllText(SyntyResultsSetup.Folder+"/validation.json",JsonUtility.ToJson(new Result{passed=checks.Count-failures,failed=failures,checks=checks.ToArray(),errors=errors.ToArray(),baselineErrors=baseline.ToArray()},true));
            if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);if(originalSettings!=null)InputSystem.settings=originalSettings;if(settings!=null)Object.DestroyImmediate(settings);
            Time.timeScale=1;SessionState.SetInt(Key+".Exit",failures==0?0:1);Debug.Log("SYNTY_RESULTS_VALIDATION="+(failures==0?"PASS":"FAIL")+" "+(checks.Count-failures)+"/"+checks.Count);EditorApplication.ExitPlaymode();
        }
    }
}
