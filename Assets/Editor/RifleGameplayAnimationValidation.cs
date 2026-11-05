using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Survive.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UPP.ThirdPersonController;
using UPP.ThirdPersonController.CameraSystems;
using Object = UnityEngine.Object;

namespace Survive.Editor
{
    [InitializeOnLoad] public static class RifleGameplayAnimationValidation
    {
        const string Key = "Survive.RifleGameplayAnimationValidation";
        static readonly List<string> checks = new List<string>(), errors = new List<string>();
        static readonly HashSet<string> jumpClips = new HashSet<string>();
        static readonly Vector2[] directions = { Vector2.up, Vector2.down, Vector2.left, Vector2.right, new Vector2(1,1), new Vector2(-1,1), new Vector2(1,-1), new Vector2(-1,-1) };
        static readonly string[] suffixes = { "F", "B", "L", "R", "FR", "FL", "BR_BkPd", "BL_BkPd" };
        static int stage, caseIndex, failures, frame;
        static double start, next;
        static UPPCharacterMovementComponent movement;
        static Animator animator;
        static Rigidbody body;
        static PlayerWeaponController weapons;
        static Camera camera;
        static Vector3 origin;
        static float maxJump;
        [Serializable] class Result { public int passed, failed; public string scope = "Level 2 real con jugador y física UPP: caminar/correr/agachado en ocho direcciones, salto, prone, roll y recarga. Arena plana de prueba; sin build distribuido."; public string[] checks, errors, jumpClips; }
        static RifleGameplayAnimationValidation()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state => {
                if (!SessionState.GetBool(Key,false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode) { checks.Clear(); errors.Clear(); jumpClips.Clear(); stage=caseIndex=failures=0; start=EditorApplication.timeSinceStartup; next=start+3; Application.runInBackground=true; Application.logMessageReceived+=OnLog; }
                if (state == PlayModeStateChange.EnteredEditMode) { SessionState.SetBool(Key,false); if(Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(Key+".Exit",1)); }
            };
        }
        public static void Run()
        {
            Directory.CreateDirectory(RifleGameplayAnimationSetup.Folder);
            EditorSceneManager.OpenScene("Assets/Scenes/Level 2.unity");
            Prepare(); SessionState.SetBool(Key,true); EditorApplication.EnterPlaymode();
        }
        static void Prepare() => typeof(CharacterCustomizationValidation).GetMethod("PrepareBatchGameView",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying)return;
            Prepare();EditorApplication.QueuePlayerLoopUpdate();
            if(camera!=null&&movement!=null) { camera.transform.position=movement.transform.position+new Vector3(.5f,2,-4);camera.transform.rotation=Quaternion.Euler(12,0,0); }
            if(EditorApplication.timeSinceStartup-start>180){Check(false,"Timeout etapa "+stage);Finish();return;}
            if(stage==3 && movement!=null) { maxJump=Mathf.Max(maxJump,movement.transform.position.y-origin.y); foreach(var c in animator.GetCurrentAnimatorClipInfo(1)) if(c.weight>.05f)jumpClips.Add(c.clip.name); }
            if(EditorApplication.timeSinceStartup<next||Time.frameCount<frame+3)return;
            frame=Time.frameCount;next=EditorApplication.timeSinceStartup+.5;
            try
            {
                switch(stage)
                {
                    case 0:
                        movement=Object.FindFirstObjectByType<UPPCharacterMovementComponent>();animator=movement.GetComponent<Animator>();body=movement.GetComponent<Rigidbody>();weapons=movement.GetComponent<PlayerWeaponController>();
                        Check(movement.GetComponent<UPPAnimatorControllerComponent>().ValidateAnimatorContract(),"Contrato de parámetros y capas UPP válido");
                        Check(!movement.RootMotion && Mathf.Approximately(movement.JumpForce,1.9221364f),"Root motion desactivado y salto calibrado con aumento de altura del 10 %");
                        var sources=AssetDatabase.GetDependencies(RifleGameplayAnimationSetup.Controller,true).Where(p=>p.StartsWith("Assets/Rifle_27A2_Pro/Animation/")&&p.EndsWith(".fbx")).ToArray();
                        Check(sources.Length>=30,"Al menos 30 clips RIFLE PRO realmente conectados al Animator");
                        Check(sources.All(p=>AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>().Any(c=>c.humanMotion&&c.length>0)),"Todas las fuentes integradas tienen animación Humanoid válida");
                        var waves=Object.FindFirstObjectByType<EnemySpawner>();waves.CancelWaves();foreach(var e in Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))Object.Destroy(e.gameObject);
                        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="AnimationValidationFloor";floor.transform.position=new Vector3(-500,49.5f,-500);floor.transform.localScale=new Vector3(100,1,100);
                        origin=new Vector3(-500,50.05f,-500);body.position=origin;body.linearVelocity=Vector3.zero;movement.transform.rotation=Quaternion.identity;
                        movement.UseDefaultControllerInput=false;movement.FiringMode=true;movement.IsAiming=false;movement.LookAtPosition=origin+Vector3.forward*100;
                        var rig=Object.FindFirstObjectByType<UPPTPSCameraController>();rig.EnableAutoRotator=false;camera=rig.mCamera;rig.enabled=false;
                        camera.transform.SetParent(null);camera.transform.rotation=Quaternion.Euler(12,0,0);
                        VendorTurretSetup.Set(weapons,"requireLockedCursor",false);
                        stage++;next+=1;break;
                    case 1: BeginCase();stage++;next+=1.2;break;
                    case 2:
                        int kind=caseIndex/8,dir=caseIndex%8;
                        string prefix=kind==0?"W2_Walk_Aim_":kind==1?"W2_Jog_Aim_":"W2_CrouchWalk_Aim_";
                        var info=animator.GetCurrentAnimatorClipInfo(1);string expected=prefix+suffixes[dir]+"_Loop_IPC";
                        Check(info.Any(c=>c.clip.name==expected&&c.weight>.05f),"Clip activo "+expected+" | "+string.Join(",",info.Select(c=>c.clip.name+":"+c.weight.ToString("F2"))));
                        Check((body.position-origin).sqrMagnitude>.01f,"Movimiento físico "+kind+"/"+suffixes[dir]);
                        if(dir==0)Capture(kind==0?"01-walk.png":kind==1?"02-jog.png":"03-crouch.png");
                        caseIndex++;if(caseIndex<24){BeginCase();next+=1.2;}else{movement.SetMoveInput(0,0);movement.IsCrouched=false;movement.IsRunning=false;body.position=origin;body.linearVelocity=Vector3.zero;stage++;next+=1;}break;
                    case 3:
                        if(maxJump==0){movement._Jump();maxJump=.00001f;next+=2.5;break;}
                        Check(maxJump>.05f&&maxJump<3&&movement.IsGrounded,"Salto físico despega y vuelve al suelo: "+maxJump.ToString("F3")+" m");
                        Check(jumpClips.Any(n=>n.Contains("W2_Stand_Aim_Jump_")),"Salto armado reproduce fases RIFLE PRO");
                        movement._Prone();stage++;next+=2;break;
                    case 4:
                        Check(movement.IsProne&&animator.GetCurrentAnimatorClipInfo(0).Any(c=>c.clip.name.IndexOf("Prone",StringComparison.OrdinalIgnoreCase)>=0),"Reptar conserva animación y transición originales");
                        movement._GetUp();stage++;next+=2;break;
                    case 5:
                        movement._GetUp();movement._Roll();stage++;next+=.3;break;
                    case 6:
                        Check(movement.IsRolling,"Rodar sigue entrando por la acción existente");stage++;next+=2.8;break;
                    case 7:
                        Check(!movement.IsRolling&&!movement.DisableAllMove,"Evento de fin de roll libera al jugador");
                        movement.IsAiming=true;
                        Check(weapons.TryFire(),"Disparo real previo a recarga");Check(weapons.TryReload(),"Recarga real inicia con el Animator actualizado");
                        movement.SetMoveInput(0,1);stage++;next+=.6;break;
                    case 8:
                        Check(animator.GetCurrentAnimatorClipInfo(2).Any(c=>c.clip.name=="W2_Stand_Aim_Reload_IPC"),"Recarga superior RIFLE PRO conserva clip y máscara");
                        Check(animator.GetCurrentAnimatorClipInfo(1).Any(c=>c.clip.name.StartsWith("W2_Walk_Aim_")),"Piernas mantienen locomoción RIFLE PRO durante recarga");
                        Capture("04-reload-moving.png");stage++;next+=2;break;
                    case 9:
                        Check(!weapons.IsReloadAnimationActive,"Recarga finaliza y libera el arma");Check(errors.Count==0,"Sin errores nuevos de ejecución");Finish();break;
                }
            }
            catch(Exception e){Check(false,e.ToString());Finish();}
        }
        static void BeginCase()
        {
            body.position=origin;body.linearVelocity=Vector3.zero;movement.transform.rotation=Quaternion.identity;
            body.rotation=Quaternion.identity;
            movement.IsRunning=caseIndex/8==1;movement.IsCrouched=caseIndex/8==2;movement.FiringMode=true;
            movement.LookAtPosition=origin+Vector3.forward*100;var input=directions[caseIndex%8];movement.SetMoveInput(input.x,input.y);
        }
        static void Capture(string name)
        {
            var rt=(RenderTexture)typeof(CharacterCustomizationValidation).GetMethod("GameViewFramebuffer",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);if(rt==null)return;
            var old=RenderTexture.active;RenderTexture.active=rt;var texture=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);texture.Apply();
            if(SystemInfo.graphicsUVStartsAtTop){var p=texture.GetPixels32();var flipped=new Color32[p.Length];for(int y=0;y<rt.height;y++)Array.Copy(p,y*rt.width,flipped,(rt.height-y-1)*rt.width,rt.width);texture.SetPixels32(flipped);texture.Apply();}
            File.WriteAllBytes(RifleGameplayAnimationSetup.Folder+"/"+name,texture.EncodeToPNG());Object.DestroyImmediate(texture);RenderTexture.active=old;
        }
        static void Check(bool pass,string label){checks.Add((pass?"PASS ":"FAIL ")+label);if(!pass)failures++;}
        static void OnLog(string message,string trace,LogType type){if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;if(message.Contains("Assets/New Terrain 3.asset")||message.StartsWith("SocketException:")&&trace.Contains("System.Net."))return;errors.Add(message);}
        static void Finish(){Application.logMessageReceived-=OnLog;File.WriteAllText(RifleGameplayAnimationSetup.Folder+"/validation.json",JsonUtility.ToJson(new Result{passed=checks.Count-failures,failed=failures,checks=checks.ToArray(),errors=errors.ToArray(),jumpClips=jumpClips.ToArray()},true));Time.timeScale=1;SessionState.SetInt(Key+".Exit",failures==0?0:1);Debug.Log("RIFLE_GAMEPLAY_ANIMATIONS_VALIDATION="+(failures==0?"PASS":"FAIL")+" "+(checks.Count-failures)+"/"+checks.Count);EditorApplication.ExitPlaymode();}
    }
}
