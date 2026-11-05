using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UPP.ThirdPersonController;
using UPP.ThirdPersonController.CameraSystems;
using Object=UnityEngine.Object;

namespace Survive.Editor
{
    [InitializeOnLoad] public static class LevelTwoTraversalValidation
    {
        const string Key="Survive.LevelTwoTraversalValidation",Folder="Logs/MovementFootsteps-20261105";
        [Serializable] class CrossingSample { public float time,progress,y;public bool grounded,wall,adjust;public string ground; }
        [Serializable] class Crossing { public string name;public Vector3 start,target,end;public float rise,peakY;public bool running,passed;public List<CrossingSample> samples=new List<CrossingSample>(); }
        [Serializable] class AudioResult { public string prefab;public int walkSteps,runSteps;public float maxRms;public string clip,mixer; }
        [Serializable] class Result { public int passed,failed;public string scope="Play real de Level 2: veredas distribuidas por el mapa con física UPP y cámara real, prueba de triggers/pared/cámara propia y pasos disparados por Animator en cuatro zombis y boss. Sin partida manual completa ni build.";public Crossing[] crossings;public AudioResult[] audio;public string[] checks,errors,baselineErrors; }
        static readonly List<string> checks=new List<string>(),errors=new List<string>(),baseline=new List<string>();
        static readonly List<Crossing> crossings=new List<Crossing>();static readonly List<ZombieFootstepAudio> footsteps=new List<ZombieFootstepAudio>();static readonly List<AudioResult> audioResults=new List<AudioResult>();
        static UPPCharacterMovementComponent player;static Rigidbody body;static UPPTPSCameraController rig;static Camera camera;static Crossing crossing;
        static int stage,index;static double start,next;static float phaseStart;static bool captured;static GameObject wall,trigger;static Vector3 arena=new Vector3(-450,100,-450),pivot;static int[] counts;static float[] rms;
        static RigidbodyConstraints originalConstraints;static float jumpStart,jumpPeak;static Vector3 rampStart;
        static LevelTwoTraversalValidation()
        {
            EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=s=>{
                if(!SessionState.GetBool(Key,false))return;
                if(s==PlayModeStateChange.EnteredPlayMode){stage=index=0;crossings.Clear();checks.Clear();errors.Clear();baseline.Clear();footsteps.Clear();audioResults.Clear();start=EditorApplication.timeSinceStartup;next=start+3;Application.runInBackground=true;Application.logMessageReceived+=OnLog;}
                if(s==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);if(Application.isBatchMode)EditorApplication.Exit(SessionState.GetInt(Key+".Exit",1));}
            };
        }
        [MenuItem("Survive/Gameplay/Validar recorridos veredas cámara y pasos")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Salí de Play Mode.");
            Directory.CreateDirectory(Folder);EditorSceneManager.OpenScene("Assets/Scenes/Level 2.unity");
            typeof(CharacterCustomizationValidation).GetMethod("PrepareBatchGameView",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying)return;
            typeof(CharacterCustomizationValidation).GetMethod("PrepareBatchGameView",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
            EditorApplication.QueuePlayerLoopUpdate();
            if(stage==1&&crossing!=null)
            {
                crossing.peakY=Mathf.Max(crossing.peakY,body.position.y);
                Vector3 delta=crossing.target-crossing.start;delta.y=0;float progress=Vector3.Dot(body.position-crossing.start,delta.normalized);
                if(progress>=delta.magnitude+.1f)player.SetMoveInput(0,0);
                var ground=Physics.RaycastAll(body.position+Vector3.up*.5f,Vector3.down,1.5f,~0,QueryTriggerInteraction.Ignore).Where(h=>!h.collider.transform.IsChildOf(player.transform)).OrderBy(h=>h.distance).FirstOrDefault();
                crossing.samples.Add(new CrossingSample{time=Time.time-phaseStart,progress=progress,y=body.position.y,grounded=player.IsGrounded,wall=player.WallAHead,ground=ground.collider!=null?ground.collider.name:null,adjust=(bool)player.GetType().BaseType.GetField("AdjustHeight",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(player)});
                if(!captured&&Time.time-phaseStart>1.1f&&index%4==0){Capture("route-"+index.ToString("00")+".png");captured=true;}
            }
            if(stage>=10&&stage<=12)SampleAudio();
            if(stage==15)jumpPeak=Mathf.Max(jumpPeak,body.position.y-jumpStart);
            if(EditorApplication.timeSinceStartup-start>280){Check(false,"Timeout etapa "+stage);Finish();return;}
            if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.15;
            try
            {
                switch(stage)
                {
                    case 0:
                        player=Object.FindFirstObjectByType<UPPCharacterMovementComponent>();body=player.GetComponent<Rigidbody>();originalConstraints=body.constraints;rig=Object.FindFirstObjectByType<UPPTPSCameraController>();camera=rig.mCamera;
                        Object.FindFirstObjectByType<EnemySpawner>().CancelWaves();foreach(var enemy in Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))Object.Destroy(enemy.gameObject);
                        player.UseDefaultControllerInput=false;player.SetMoveInput(0,0);rig.EnableAutoRotator=false;
                        Check(player.GetComponent<NavMeshAgent>()==null,"Jugador usa física UPP, sin agente NavMesh");
                        Check(Mathf.Approximately(rig.AimCameraState.RightCameraOffset,1f),"Offset de apuntado conservado");
                        stage=2;next+=.5;break;
                    case 2:
                        BuildCrossings();Check(crossings.Count>=8,"Se encontraron veredas transitables en distintas posiciones del mapa");
                        if(crossings.Count==0){stage=3;break;}BeginCrossing();stage=1;next+=2.1;break;
                    case 1:
                        crossing.end=body.position;Vector3 dir=crossing.target-crossing.start;dir.y=0;float needed=dir.magnitude;
                        Vector3 moved=body.position-crossing.start;moved.y=0;
                        crossing.passed=Vector3.Dot(moved,dir.normalized)>needed-.15f && crossing.peakY>=crossing.target.y-.045f && crossing.peakY<crossing.target.y+.7f;
                        Check(crossing.passed,crossing.name+" progreso="+Vector3.Dot(moved,dir.normalized).ToString("F2")+" altura="+crossing.peakY.ToString("F3"));
                        player.SetMoveInput(0,0);index++;
                        if(index<crossings.Count){BeginCrossing();next+=2.1;}else stage=3;break;
                    case 3:
                        player.SetMoveInput(0,0);player.DisableAllMove=true;body.constraints=RigidbodyConstraints.FreezeAll;body.position=arena+Vector3.right*6;body.linearVelocity=Vector3.zero;rig.enabled=false;pivot=arena+Vector3.up*1.5f;rig.transform.position=pivot;
                        trigger=GameObject.CreatePrimitive(PrimitiveType.Cube);trigger.name="AudioTriggerCameraProbe";trigger.transform.position=pivot+Vector3.back*1.5f;trigger.transform.localScale=new Vector3(2,2,.3f);trigger.GetComponent<Collider>().isTrigger=true;Physics.SyncTransforms();
                        ResetCollision();rig.SetCameraCollision(~0);Check(Vector3.Distance(camera.transform.position,pivot)>3.45f,"Cámara ignora volúmenes trigger como parlantes y pickups");
                        trigger.SetActive(false);wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=pivot+Vector3.back*2;wall.transform.localScale=new Vector3(3,3,.2f);Physics.SyncTransforms();
                        ResetCollision();rig.SetCameraCollision(~0);float distance=Vector3.Distance(camera.transform.position,pivot);
                        Check(distance<1.8f&&!Physics.CheckSphere(camera.transform.position,.14f,~0,QueryTriggerInteraction.Ignore),"Cámara respeta pared sólida con margen para la lente");
                        wall.SetActive(false);camera.transform.position=pivot+Vector3.back*3.5f;rig.SetCameraCollision(~0);
                        Check(Vector3.Distance(camera.transform.position,pivot)<3.4f,"Cámara recupera distancia progresivamente al salir de una esquina");
                        body.position=arena+Vector3.back*1.5f;Physics.SyncTransforms();ResetCollision();rig.SetCameraCollision(~0);
                        Check(Vector3.Distance(camera.transform.position,pivot)>3.45f,"Cámara ignora colliders del propio personaje");
                        stage=9;next+=.5;break;
                    case 9: SetupAudio();stage=10;next+=2.2;break;
                    case 10:
                        for(int i=0;i<footsteps.Count;i++)
                        {
                            var a=footsteps[i];audioResults[i].walkSteps=a.PlayedSteps;
                            Check(a.PlayedSteps>=2,"Eventos de caminar emiten pasos en "+audioResults[i].prefab);
                            Check(a.LastSurface==FootstepSurfaceType.Concrete&&a.LastClip!=null,"Superficie con valor antiguo usa clips válidos en "+audioResults[i].prefab);
                            Check(a.Source.outputAudioMixerGroup!=null&&a.Source.outputAudioMixerGroup.name=="SFX","Pasos 3D conectados a SFX en "+audioResults[i].prefab);
                            a.GetComponentInChildren<Animator>().SetFloat("Speed",2.32f);
                        }
                        counts=footsteps.Select(a=>a.PlayedSteps).ToArray();stage=11;next+=1.5;break;
                    case 11:
                        for(int i=0;i<footsteps.Count;i++)
                        {audioResults[i].runSteps=footsteps[i].PlayedSteps-counts[i];Check(audioResults[i].runSteps>=2,"Eventos de correr emiten pasos en "+audioResults[i].prefab);footsteps[i].GetComponentInChildren<Animator>().SetFloat("Speed",0);}
                        stage=12;next+=.5;break;
                    case 12: counts=footsteps.Select(a=>a.PlayedSteps).ToArray();stage=13;next+=.8;break;
                    case 13:
                        Check(footsteps.Select((a,i)=>a.PlayedSteps==counts[i]).All(x=>x),"No se emiten pasos en idle");
                        Check(audioResults.All(a=>a.maxRms>.00001f),"Señal PCM 3D no nula en pasos de los cuatro zombis y del boss");
                        Time.timeScale=0;foreach(var a in footsteps)a.Footstep();
                        Check(footsteps.Select((a,i)=>a.PlayedSteps==counts[i]).All(x=>x),"Pausa bloquea los pasos");Time.timeScale=1;
                        foreach(var a in footsteps)a.GetComponent<EnemyHealth>().TakeDamage(999999);counts=footsteps.Select(a=>a.PlayedSteps).ToArray();foreach(var a in footsteps)a.Footstep();
                        Check(footsteps.Select((a,i)=>a.PlayedSteps==counts[i]).All(x=>x),"Enemigos muertos no emiten pasos");
                        body.constraints=originalConstraints;player.DisableAllMove=false;body.position=arena+Vector3.right*9+Vector3.up*.03f;body.linearVelocity=Vector3.zero;player.IsRunning=false;player.IsSprinting=false;player.SetMoveInput(0,0);Physics.SyncTransforms();stage=14;next+=1;break;
                    case 14:
                        jumpStart=body.position.y;jumpPeak=0;player._Jump();stage=15;next+=2.8;break;
                    case 15:
                        Check(jumpPeak>.1f&&jumpPeak<2f&&player.IsGrounded,"Salto manual conserva despegue y aterrizaje: "+jumpPeak.ToString("F3")+" m");
                        Check(Mathf.Approximately(player.JumpForce,1.9221364f),"Salto configurado con aumento de altura del 10 %");
                        var ramp=GameObject.CreatePrimitive(PrimitiveType.Cube);ramp.name="WalkableSlopeRegression";ramp.transform.position=arena+Vector3.right*20;ramp.transform.localScale=new Vector3(4,.3f,8);ramp.transform.rotation=Quaternion.Euler(-20,0,0);
                        var probe=ramp.transform.position+Vector3.back*2.5f+Vector3.up*3;Physics.SyncTransforms();
                        var hit=Physics.RaycastAll(probe,Vector3.down,8,~0,QueryTriggerInteraction.Ignore).First(h=>h.collider.gameObject==ramp);
                        body.position=hit.point+Vector3.up*.03f;body.rotation=Quaternion.identity;body.linearVelocity=Vector3.zero;player.FiringMode=true;player.IsRunning=false;player.LookAtPosition=body.position+Vector3.forward*100;player.SetMoveInput(0,1);rampStart=body.position;stage=16;next+=2.5;break;
                    case 16:
                        Check(body.position.z-rampStart.z>1f&&body.position.y-rampStart.y>.25f,"Pendiente caminable de 20 grados se asciende sin atascarse");
                        Check(errors.Count==0,"Sin errores nuevos de ejecución");Finish();break;
                }
            }
            catch(Exception e){Check(false,e.ToString());Finish();}
        }
        static bool Ground(Vector3 p,out RaycastHit hit)
        {
            hit=Physics.RaycastAll(p+Vector3.up,Vector3.down,4,~0,QueryTriggerInteraction.Ignore)
                .Where(h=>!h.collider.transform.IsChildOf(player.transform)&&h.collider.GetComponentInParent<EnemyHealth>()==null).OrderBy(h=>h.distance).FirstOrDefault();
            return hit.collider!=null&&hit.normal.y>.85f;
        }
        static void BuildCrossings()
        {
            var candidates=new List<Crossing>();
            foreach(var collider in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Where(c=>c.enabled&&!c.isTrigger&&c.name.Contains("Sidewalk")))
            {
                var b=collider.bounds;
                foreach(var normal in new[]{Vector3.right,Vector3.left,Vector3.forward,Vector3.back})
                {
                    float extent=normal.x!=0?b.extents.x:b.extents.z;Vector3 edge=b.center+normal*extent;
                    if(!Ground(edge+normal*.7f,out var low)||!Ground(edge-normal*.7f,out var high))continue;
                    float rise=high.point.y-low.point.y;if(rise<.04f||rise>.35f||!high.collider.name.Contains("Sidewalk"))continue;
                    if(candidates.Any(c=>Vector3.Distance(c.start,low.point)<4))continue;
                    Vector3 delta=high.point-low.point;delta.y=0;
                    Vector3 bottom=low.point+Vector3.up*.85f;Vector3 top=bottom+Vector3.up;
                    if(Physics.CapsuleCastAll(bottom,top,.35f,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore).Any(h=>!h.collider.transform.IsChildOf(player.transform)&&h.normal.y<.5f))continue;
                    candidates.Add(new Crossing{name=collider.name,start=low.point+Vector3.up*.025f,target=high.point+Vector3.up*.025f,rise=rise});
                }
            }
            var selected=new List<Crossing>();
            if(candidates.Count>0){selected.Add(candidates[0]);candidates.RemoveAt(0);}
            while(selected.Count<8&&candidates.Count>0)
            {var best=candidates.OrderByDescending(c=>selected.Min(s=>(s.start-c.start).sqrMagnitude)).First();selected.Add(best);candidates.Remove(best);}
            foreach(bool run in new[]{false,true})foreach(var c in selected)crossings.Add(new Crossing{name=(run?"Correr ":"Caminar ")+c.name,start=c.start,target=c.target,rise=c.rise,running=run});
        }
        static void BeginCrossing()
        {
            crossing=crossings[index];crossing.peakY=crossing.start.y;body.position=crossing.start;body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;
            var dir=crossing.target-crossing.start;dir.y=0;dir.Normalize();body.rotation=Quaternion.LookRotation(dir);rig.SetCameraRotation(12,Quaternion.LookRotation(dir).eulerAngles.y,false);
            player.LookAtPosition=crossing.start+dir*100;player.IsRunning=crossing.running;player.IsCrouched=false;player.SetMoveInput(0,1);Physics.SyncTransforms();phaseStart=Time.time;captured=false;
        }
        static void ResetCollision(){rig.SetCameraCollision(~0,false);camera.transform.position=pivot+Vector3.back*3.5f;}
        static void SetupAudio()
        {
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=arena+Vector3.down*.1f;floor.transform.localScale=new Vector3(25,.2f,20);floor.AddComponent<FootstepSurface>().surfaceType=(FootstepSurfaceType)3;
            camera.transform.position=arena+new Vector3(0,3,-5);camera.transform.rotation=Quaternion.LookRotation(arena+Vector3.up-camera.transform.position);
            var paths=new[]{"Assets/Players/EnemyChasing.prefab","Assets/Players/EnemyPatrol.prefab","Assets/Players/EnemyChasingRanged.prefab","Assets/Players/EnemyAttack.prefab","Assets/Prefabs/Enemies/Boss_Zombie.prefab"};
            for(int i=0;i<paths.Length;i++)
            {
                var template=AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);var agent=template.GetComponent<NavMeshAgent>();
                var go=Object.Instantiate(template,arena+Vector3.right*((i-2)*2.6f)+Vector3.up*agent.baseOffset,Quaternion.identity);
                go.GetComponent<NavMeshAgent>().enabled=false;var ai=go.GetComponent<EnemyBaseController>();if(ai!=null)ai.enabled=false;var boss=go.GetComponent<BossBase>();if(boss!=null)boss.enabled=false;
                var a=go.GetComponent<ZombieFootstepAudio>();if(a==null)throw new Exception("Faltan pasos en "+paths[i]);footsteps.Add(a);
                go.GetComponentInChildren<Animator>().SetFloat("Speed",.36f);
                audioResults.Add(new AudioResult{prefab=Path.GetFileNameWithoutExtension(paths[i])});
            }
            rms=new float[footsteps.Count];Physics.SyncTransforms();
        }
        static void SampleAudio()
        {
            var buffer=new float[1024];for(int i=0;i<footsteps.Count;i++)
            {if(footsteps[i]==null)continue;footsteps[i].Source.GetOutputData(buffer,0);float sum=0;foreach(float v in buffer)sum+=v*v;rms[i]=Mathf.Max(rms[i],Mathf.Sqrt(sum/buffer.Length));audioResults[i].maxRms=rms[i];audioResults[i].clip=footsteps[i].LastClip!=null?footsteps[i].LastClip.name:null;audioResults[i].mixer=footsteps[i].Source.outputAudioMixerGroup!=null?footsteps[i].Source.outputAudioMixerGroup.name:null;}
        }
        static void Capture(string name)
        {
            var rt=(RenderTexture)typeof(CharacterCustomizationValidation).GetMethod("GameViewFramebuffer",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);if(rt==null)return;
            var previous=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();
            if(SystemInfo.graphicsUVStartsAtTop){var p=image.GetPixels32();var f=new Color32[p.Length];for(int y=0;y<rt.height;y++)Array.Copy(p,y*rt.width,f,(rt.height-y-1)*rt.width,rt.width);image.SetPixels32(f);image.Apply();}
            File.WriteAllBytes(Folder+"/"+name,image.EncodeToPNG());Object.DestroyImmediate(image);RenderTexture.active=previous;
        }
        static void Check(bool pass,string label)=>checks.Add((pass?"PASS ":"FAIL ")+label);
        static void OnLog(string m,string trace,LogType type){if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;if(m.StartsWith("SocketException:")&&trace.Contains("System.Net."))baseline.Add(m);else errors.Add(m);}
        static void Finish()
        {
            Application.logMessageReceived-=OnLog;Time.timeScale=1;if(player!=null)player.SetMoveInput(0,0);int failed=checks.Count(c=>c.StartsWith("FAIL "));
            File.WriteAllText(Folder+"/traversal-validation.json",JsonUtility.ToJson(new Result{passed=checks.Count-failed,failed=failed,crossings=crossings.ToArray(),audio=audioResults.ToArray(),checks=checks.ToArray(),errors=errors.ToArray(),baselineErrors=baseline.ToArray()},true));
            SessionState.SetInt(Key+".Exit",failed==0?0:1);Debug.Log("LEVEL_TWO_TRAVERSAL_VALIDATION="+(failed==0?"PASS":"FAIL")+" "+(checks.Count-failed)+"/"+checks.Count);EditorApplication.ExitPlaymode();
        }
    }
}
