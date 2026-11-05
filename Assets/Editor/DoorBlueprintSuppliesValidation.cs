using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Survive.Combat;
using Survive.Economy;
using Survive.Items;
using Survive.Turrets;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UPP.ThirdPersonController;
using Object = UnityEngine.Object;

namespace Survive.Editor
{
    [InitializeOnLoad] public static class DoorBlueprintSuppliesValidation
    {
        const string Key="Survive.DoorBlueprintSuppliesValidation";
        static bool MapPreview=>SessionState.GetBool(Key+".Map",false);
        static string ReportFolder=>MapPreview?"Logs/DoorBossFix/Map":DoorBlueprintSuppliesSetup.Folder;
        static readonly List<string> checks=new List<string>(),errors=new List<string>(),baselineErrors=new List<string>();
        static readonly List<float> shotTimes=new List<float>();
        static readonly Dictionary<Renderer,Material[]> materials=new Dictionary<Renderer,Material[]>();
        static readonly Dictionary<Material,Color> emissionBefore=new Dictionary<Material,Color>();
        static PuertaConLlave[] doors; static GameObject player,wall; static Camera camera; static BossBase boss;
        static DoorBlueprintPickup blueprint; static KeyPickup key; static PlayerDoorGuide guide; static PlayerKeyInventory keys;
        static PlayerInteractionController interaction; static PlayerVendorController shop; static PlayerWallet wallet; static NpcVendor vendor;
        static AutoTurret turret; static Vector3 doorStart, pausedPosition; static bool stoppedAtZero; static int finalShots;
        static NavMeshDataInstance nav; static int stage,failures,frame,ammoIndex; static double start,next; static float lastHealth;
        static Keyboard keyboard; static InputSettings previousSettings,settings;
        [Serializable] class Result { public int passed,failed; public string scope="Puertas/barreras reales copiadas de Level 2, prefabs reales de jugador, boss, vendedor y torreta en escena aislada: drop, E, neón, orden, animación y pausa, munición común/explosiva, 15 disparos y desgaste. Sin build ni partida completa."; public string[] checks,errors,baselineErrors; }
        static DoorBlueprintSuppliesValidation()
        {
            EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged+=state=>
            {
                if(!SessionState.GetBool(Key,false)) return;
                if(state==PlayModeStateChange.EnteredPlayMode)
                {
                    checks.Clear();errors.Clear();baselineErrors.Clear();materials.Clear();emissionBefore.Clear();shotTimes.Clear(); stage=failures=finalShots=0; stoppedAtZero=false;
                    start=EditorApplication.timeSinceStartup;next=start+3; Application.runInBackground=true;
                    previousSettings=InputSystem.settings; settings=Object.Instantiate(previousSettings);
                    settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus; settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    InputSystem.settings=settings;keyboard=InputSystem.AddDevice<Keyboard>("BlueprintValidationKeyboard");Application.logMessageReceived+=OnLog;
                }
                if(state==PlayModeStateChange.EnteredEditMode) { SessionState.SetBool(Key,false); if(Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(Key+".Exit",1)); }
            };
        }
        [MenuItem("Survive/Gameplay/Validar planos animación munición y desgaste")]
        public static void Run()
        {
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SessionState.SetBool(Key+".Map",false);
            Directory.CreateDirectory(DoorBlueprintSuppliesSetup.Folder);
            var source=EditorSceneManager.OpenScene(DoorKeyProgressionSetup.Level,OpenSceneMode.Single);
            var originals=DoorKeyProgressionSetup.Names.Select(n=>source.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PuertaConLlave>(true)).Single(d=>d.name==n)).ToArray();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);scene.name="DoorBlueprintSuppliesValidation";SceneManager.SetActiveScene(scene);
            var map=new Dictionary<PuertaConLlave,PuertaConLlave>();
            for(int i=0;i<originals.Length;i++)
            {
                var original=originals[i]; var clone=Object.Instantiate(original.gameObject);clone.name=original.name;
                clone.transform.SetPositionAndRotation(original.transform.position,original.transform.rotation);clone.transform.localScale=original.transform.lossyScale;
                var bounds=clone.GetComponent<Collider>().bounds;
                Vector3 offset=new Vector3(i*40-bounds.center.x,-bounds.min.y,6-bounds.center.z);clone.transform.position+=offset;
                var door=clone.GetComponent<PuertaConLlave>();map[original]=door;
                var data=new SerializedObject(door); var array=data.FindProperty("blockingObjects");
                for(int j=0;j<array.arraySize;j++)
                {
                    var originalBarrier=(GameObject)array.GetArrayElementAtIndex(j).objectReferenceValue;
                    var copy=Object.Instantiate(originalBarrier);copy.transform.SetPositionAndRotation(originalBarrier.transform.position+offset,originalBarrier.transform.rotation);copy.transform.localScale=originalBarrier.transform.lossyScale;
                    array.GetArrayElementAtIndex(j).objectReferenceValue=copy;
                    foreach(var proxy in copy.GetComponentsInChildren<DoorBarrierInteraction>(true)) VendorTurretSetup.Set(proxy,"door",door);
                }
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach(var pair in map)
            {
                var data=new SerializedObject(pair.Value);
                foreach(string field in new[]{"prerequisiteDoors","linkedDoors"})
                {
                    var array=data.FindProperty(field);
                    for(int j=0;j<array.arraySize;j++) array.GetArrayElementAtIndex(j).objectReferenceValue=map[(PuertaConLlave)array.GetArrayElementAtIndex(j).objectReferenceValue];
                }
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.CloseScene(source,true); Prepare();SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        [MenuItem("Survive/Gameplay/Validar guía roja de día y noche en Level 2")]
        public static void PreviewInLevelTwo()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Salí de Play Mode.");
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SessionState.SetBool(Key+".Map",true);Directory.CreateDirectory(ReportFolder);
            EditorSceneManager.OpenScene(DoorKeyProgressionSetup.Level,OpenSceneMode.Single);
            Prepare();SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void Prepare()=>typeof(CharacterCustomizationValidation).GetMethod("PrepareBatchGameView",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying) return;
            Prepare();EditorApplication.QueuePlayerLoopUpdate();
            if(EditorApplication.timeSinceStartup-start>180) { Check(false,"Timeout etapa "+stage);Finish();return; }
            if(EditorApplication.timeSinceStartup<next || Time.frameCount<=frame+3) return;
            frame=Time.frameCount;next=EditorApplication.timeSinceStartup+.35;
            if(MapPreview) { TickMap();return; }
            try
            {
                switch(stage)
                {
                    case 0:
                        CreateRuntime(); stage++;next+=2;break;
                    case 1:
                        boss.Health.TakeDamage(999999);
                        blueprint=Object.FindFirstObjectByType<DoorBlueprintPickup>();key=Object.FindFirstObjectByType<KeyPickup>();
                        Require(blueprint!=null && key!=null,"Boss real suelta plano y llave garantizados");
                        foreach(var d in doors) foreach(var r in d.GuideRenderers) materials[r]=r.sharedMaterials;
                        foreach(var material in materials.Values.SelectMany(m=>m).Where(m=>m!=null && m.HasProperty("_EmissionColor"))) emissionBefore[material]=material.GetColor("_EmissionColor");
                        ViewPickup(blueprint.transform);interaction.RefreshTarget();
                        Require(interaction.CurrentInteractable==blueprint,"Mira selecciona plano 3D");
                        Check(player.GetComponent<PlayerDoorGuide>()==null,"Acercarse no recoge automáticamente el plano");Press();stage++;break;
                    case 2:
                        Release();guide=player.GetComponent<PlayerDoorGuide>();
                        Require(guide!=null && guide.Blueprints==1 && blueprint==null,"E recoge un plano una sola vez (planos="+(guide!=null?guide.Blueprints:-1)+", objeto="+(blueprint!=null)+", target="+interaction.CurrentInteractable+")");
                        Check(guide.Target==null && doors.All(d=>Original(d)),"Sin llave el plano espera sin resaltar una puerta");
                        ViewDoor(doors[0],true);stage=20;break;
                    case 20:
                        Capture("00-next-door-original.png");
                        ViewPickup(key.transform);interaction.RefreshTarget();Require(interaction.CurrentInteractable==key,"Mira selecciona llave");Press();stage=3;break;
                    case 3:
                        Release();Require(keys.KeyCount==1 && guide.Target==doors[0],"Recoger llave activa guía de Zona2");
                        Check(Neon(doors[0]) && doors.Skip(1).All(Original),"Neón sólo en primera puerta habilitada");
                        var neon=Resources.Load<Material>("Gameplay/DoorGuideNeon"); Check(neon.IsKeywordEnabled("_EMISSION") && neon.GetColor("_EmissionColor").maxColorComponent>1,"Material neón conserva emisión HDR activa");
                        Check(doors[0].GuideRenderers.All(PreservesSurface),"Neón conserva shader, texturas, UV, color base, normales y propiedades de superficie");
                        Check(doors[0].GuideRenderers.All(EmissionFollowsAlbedo),"Emisión roja HDR sigue la textura y UV originales de la puerta");
                        Check(emissionBefore.All(pair=>pair.Key.GetColor("_EmissionColor")==pair.Value),"Resaltar no modifica los materiales originales compartidos");
                        Check(!doors[1].Interact(player)&&keys.KeyCount==1,"Plano no permite saltear Zona2");
                        ViewDoor(doors[0],true);stage++;break;
                    case 4:
                        Capture("01-next-door-neon.png");ViewDoor(doors[0],false);interaction.RefreshTarget();
                        Require(interaction.CurrentInteractable==doors[0] || interaction.CurrentInteractable is DoorBarrierInteraction p && p.Door==doors[0],"Puerta resaltada se selecciona para interactuar");
                        doorStart=doors[0].transform.position;Press();stage++;break;
                    case 5:
                        Release();Require(doors[0].IsOpen && doors[0].IsAnimating,"E inicia animación de apertura");
                        Check(doors[0].gameObject.activeInHierarchy && doors[0].transform.position.y>doorStart.y && doors[0].transform.position.y<doorStart.y+5,"Puerta sube progresivamente sin desaparecer");
                        Check(Original(doors[0]) && guide.Blueprints==0 && keys.KeyCount==0,"Abrir restaura materiales y consume una llave y un plano");
                        pausedPosition=doors[0].transform.position;Time.timeScale=0;stage++;next+=.6;break;
                    case 6:
                        Check(Vector3.Distance(pausedPosition,doors[0].transform.position)<.001f,"Pausa detiene animación de puerta");Time.timeScale=1;stage++;next+=2;break;
                    case 7:
                        Check(!doors[0].IsAnimating && doors[0].gameObject.activeInHierarchy && Mathf.Approximately(doors[0].transform.position.y-doorStart.y,5),"Puerta termina elevada y visible");
                        Check(doors[0].BlockingObjects.All(b=>b!=null&&b.activeInHierarchy),"Barreras acompañan la apertura sin borrarse");
                        Check(doors[0].GetComponentsInChildren<NavMeshObstacle>().All(o=>!o.enabled),"Apertura libera carving de navegación");
                        ViewDoor(doors[0],true);
                        keys.TryAddKey();guide.TryAddBlueprint();Check(guide.Target==doors[1] && Neon(doors[1]),"Siguiente plano señala Zona3");
                        Require(doors[1].Interact(player),"Segunda llave abre Zona3");stage++;next+=1.8;break;
                    case 8:
                        Capture("02-open-door-original-material.png"); keys.TryAddKey();guide.TryAddBlueprint();
                        Check(guide.Target.OpensTogether(doors[2]) && Neon(doors[2]) && Neon(doors[3]),"Plano de Zona4 resalta ambas puertas enlazadas");
                        Require(doors[3].Interact(player),"Se abre el par desde Zona4.1");
                        Check(doors[2].IsOpen && doors[3].IsOpen && Original(doors[2]) && Original(doors[3]) && keys.KeyCount==0 && guide.Blueprints==0,"Par restaura materiales y consume un solo plano y llave");
                        stage++;next+=1.8;break;
                    case 9:
                        Check(doors[2].gameObject.activeInHierarchy && doors[3].gameObject.activeInHierarchy && !doors[2].IsAnimating && !doors[3].IsAnimating,"Ambas puertas terminan animación visibles");
                        keys.TryAddKey();guide.TryAddBlueprint();Check(guide.Target==doors[4] && Neon(doors[4]),"Último plano guía Zona5 después del par");
                        Require(doors[4].Interact(player),"Última puerta conserva progresión");
                        PrepareShop();stage++;next+=1;break;
                    case 10:
                        Require(shop.TryOpen(vendor),"Abre mercader real");ammoIndex=Enumerable.Range(0,vendor.Catalog.Count).Single(i=>vendor.Catalog.GetOffer(i).IsCommonAmmo);
                        Check(vendor.Catalog.GetOffer(8).IsTurret && vendor.Catalog.GetOffer(9).IsExplosiveMagazine,"Ofertas anteriores conservan sus posiciones");
                        var weapon=player.GetComponent<PlayerWeaponController>().EquippedWeapon;Require(weapon!=null,"Arma equipada para recibir munición");
                        var inventory=player.GetComponent<PlayerWeaponInventory>();
                        for(int i=0;i<6 && inventory.HasFreeSlot;i++) inventory.TryStorePurchase(Object.Instantiate(vendor.Catalog.GetOffer(0).weaponPrefab));
                        Check(!inventory.HasFreeSlot,"Munición disponible con hotbar llena");
                        var explosive=AssetDatabase.FindAssets("t:ExplosiveAmmoDefinition").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<ExplosiveAmmoDefinition>).First();
                        Require(weapon.TryLoadExplosiveMagazine(explosive),"Cargador explosivo activo antes de comprar munición común");
                        int reserve=weapon.ReserveAmmo,magazine=weapon.AmmoInMagazine,balance=wallet.Coins;
                        var buttons=Read<Button[]>(shop,"buyButtons");buttons[ammoIndex].onClick.Invoke();
                        Check(weapon.ReserveAmmo==reserve+60 && wallet.Coins==balance-30,"Caja común entrega 60 balas y cobra 30 monedas");
                        Check(weapon.LoadedExplosiveAmmo==explosive && weapon.AmmoInMagazine==magazine,"Munición común no reemplaza cargador explosivo ni balas cargadas");
                        var scroll=Read<RectTransform>(shop,"rows").GetComponentInParent<ScrollRect>();Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=0;
                        stage++;break;
                    case 11:
                        Capture("03-vendor-common-ammo.png");
                        var w=player.GetComponent<PlayerWeaponController>().EquippedWeapon;int coins=wallet.Coins,reserveBefore=w.ReserveAmmo;
                        Time.timeScale=0;Check(!shop.TryBuy(ammoIndex)&&wallet.Coins==coins&&w.ReserveAmmo==reserveBefore,"Pausa impide compra y cobro de munición");Time.timeScale=1;
                        wallet.TrySpend(wallet.Coins);Check(!shop.TryBuy(ammoIndex)&&w.ReserveAmmo==reserveBefore,"Sin dinero no entrega munición");
                        wallet.AddCoins(100);SetPrivate(w,"reserveAmmo",int.MaxValue-1);
                        Check(!shop.TryBuy(ammoIndex)&&wallet.Coins==100,"Reserva llena no cobra ni desborda");
                        SetPrivate(w,"reserveAmmo",reserveBefore); shop.Close();CreateTurret();stage++;next+=1;break;
                    case 12:
                        Check(turret.ShotsFired==0 && turret.CurrentHealth==150,"Pared bloquea disparo sin gastar vida");
                        Object.Destroy(wall);stage++;next+=1.2;break;
                    case 13:
                        Require(turret!=null && turret.ShotsFired>=2,"Torreta real dispara automáticamente");
                        Check(Mathf.Approximately(turret.CurrentHealth,150-10*turret.ShotsFired),"Cada disparo descuenta exactamente 10 de vida");
                        Check(turret.GetComponentInChildren<TMP_Text>().text.Contains("/ 150"),"Barra de vida visible sobre torreta");Capture("04-turret-health.png");
                        finalShots=turret.ShotsFired;lastHealth=turret.CurrentHealth;Time.timeScale=0;stage++;next+=.6;break;
                    case 14:
                        Check(turret.ShotsFired==finalShots && turret.CurrentHealth==lastHealth,"Pausa detiene disparos y desgaste de torreta");Time.timeScale=1;stage++;next+=8;break;
                    case 15:
                        Check(turret==null && finalShots==15 && lastHealth==0 && stoppedAtZero,"Torreta completa 15 disparos, se agota y desaparece sin disparo 16");
                        Check(shotTimes.Count==15 && shotTimes.Zip(shotTimes.Skip(1),(a,b)=>b-a).All(dt=>dt>=.49f),"Cadencia limitada a 2 disparos por segundo");
                        Check(errors.Count==0,"Sin errores de ejecución");Finish();break;
                }
            }
            catch(Exception e) { Check(false,e.ToString());Finish(); }
        }
        static void TickMap()
        {
            try
            {
                switch(stage)
                {
                    case 0:
                        Object.FindFirstObjectByType<EnemySpawner>().CancelWaves();
                        foreach(var enemy in Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))Object.Destroy(enemy.gameObject);
                        doors=DoorKeyProgressionSetup.Names.Select(n=>Object.FindObjectsByType<PuertaConLlave>(FindObjectsSortMode.None).Single(d=>d.name==n)).ToArray();
                        foreach(var d in doors)foreach(var r in d.GuideRenderers)materials[r]=r.sharedMaterials;
                        player=Object.FindFirstObjectByType<PlayerHealth>().gameObject;keys=PlayerKeyInventory.ForPlayer(player,true);
                        player.GetComponent<UPPCharacterMovementComponent>().UseDefaultControllerInput=false;
                        player.GetComponent<Rigidbody>().constraints=RigidbodyConstraints.FreezeAll;
                        camera=Camera.main;camera.transform.SetParent(null,true);
                        foreach(var rig in Object.FindObjectsByType<UPP.ThirdPersonController.CameraSystems.UPPTPSCameraController>(FindObjectsSortMode.None))rig.enabled=false;
                        Check(camera.allowHDR && camera.GetUniversalAdditionalCameraData().renderPostProcessing,"Capturas con HDR y postproceso reales de Level 2");
                        ViewMapDoor(doors[0]);stage++;next+=.5;break;
                    case 1:
                        Capture("01-day-original.png");keys.TryAddKey();guide=player.GetComponent<PlayerDoorGuide>();if(guide==null)guide=player.AddComponent<PlayerDoorGuide>();
                        File.WriteAllLines(ReportFolder+"/source-materials.txt",materials.Values.SelectMany(m=>m).Where(m=>m!=null).Distinct().Select(m=>AssetDatabase.GetAssetPath(m)+" | shader="+m.shader.name+" | color="+m.color+" | texture="+AssetDatabase.GetAssetPath(m.mainTexture)));
                        Check(guide.TryAddBlueprint() && guide.Target==doors[0],"Plano real señala Zona2 en el mapa");
                        Check(Neon(doors[0]) && doors[0].GuideRenderers.All(PreservesSurface) && doors[0].GuideRenderers.All(EmissionFollowsAlbedo),"Guía roja conserva textura y superficie en Level 2");
                        Check(materials.Values.SelectMany(m=>m).Any(m=>m!=null && m.mainTexture!=null),"Puertas reales conservan sus texturas asignadas");
                        stage++;break;
                    case 2:
                        Capture("02-day-red-guide.png");guide.enabled=false;Check(Original(doors[0]),"Desactivar guía restaura el material diurno original");
                        Object.FindFirstObjectByType<StormLightingSkybox>().ActivarNoche();Time.timeScale=4;stage++;break;
                    case 3:
                        if(Object.FindFirstObjectByType<StormLightingSkybox>().NightProgress<1)break;
                        Time.timeScale=1;
                        var lightning=Object.FindFirstObjectByType<LightningController>();
                        if(lightning!=null)
                        {
                            lightning.enabled=false;lightning.CancelInvoke();
                            foreach(string field in new[]{"luzRelampago1","luzRelampago2"})
                            { var light=(Light)typeof(LightningController).GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(lightning);if(light!=null)light.intensity=0; }
                        }
                        stage++;next+=.6;break;
                    case 4:
                        Capture("03-night-original.png");guide.enabled=true;
                        Check(Neon(doors[0]) && doors[0].GuideRenderers.All(PreservesSurface),"Guía roja vuelve a activarse con la textura original de noche");stage++;break;
                    case 5:
                        Capture("04-night-red-guide.png");guide.enabled=false;
                        Check(doors.All(Original),"Materiales originales restaurados después de la vista diurna/nocturna");
                        Check(keys.KeyCount==1 && guide.Blueprints==1,"Activar y desactivar el resaltado no consume llave ni plano");
                        Check(errors.Count==0,"Sin errores nuevos en Level 2 durante las capturas");Finish();break;
                }
            }
            catch(Exception e) { Check(false,e.ToString());Finish(); }
        }
        static void CreateRuntime()
        {
            doors=DoorKeyProgressionSetup.Names.Select(n=>Object.FindObjectsByType<PuertaConLlave>(FindObjectsSortMode.None).Single(d=>d.name==n)).ToArray();
            var light=new GameObject("Sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(45,-30,0);RenderSettings.ambientLight=new Color(.4f,.45f,.5f);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Floor";floor.transform.position=new Vector3(80,-.15f,0);floor.transform.localScale=new Vector3(240,.3f,70);
            var source=new NavMeshBuildSource { shape=NavMeshBuildSourceShape.Box,size=new Vector3(240,.3f,70),transform=Matrix4x4.TRS(floor.transform.position,Quaternion.identity,Vector3.one),area=0 };
            var data=NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(-902729914),new List<NavMeshBuildSource>{source},new Bounds(new Vector3(80,0,0),new Vector3(250,8,80)),Vector3.zero,Quaternion.identity);nav=NavMesh.AddNavMeshData(data);
            camera=new GameObject("ValidationCamera").AddComponent<Camera>();camera.tag="MainCamera";camera.fieldOfView=60;
            player=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(VendorTurretSetup.PlayerPath),new Vector3(0,.05f,-5),Quaternion.identity);
            player.GetComponent<UPPCharacterMovementComponent>().UseDefaultControllerInput=false;player.GetComponent<Rigidbody>().constraints=RigidbodyConstraints.FreezeAll;
            interaction=player.GetComponent<PlayerInteractionController>();VendorTurretSetup.Set(interaction,"requireLockedCursor",false);
            VendorTurretSetup.Set(interaction,"sourceCamera",camera);
            foreach(var other in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) if(other!=camera) other.enabled=false;
            foreach(var rig in Object.FindObjectsByType<UPP.ThirdPersonController.CameraSystems.UPPTPSCameraController>(FindObjectsSortMode.None)) rig.enabled=false;
            VendorTurretSetup.Set(player.GetComponent<PlayerWeaponController>(),"requireLockedCursor",false);
            shop=player.GetComponent<PlayerVendorController>();VendorTurretSetup.Set(shop,"requireLockedCursor",false);wallet=player.GetComponent<PlayerWallet>();keys=PlayerKeyInventory.ForPlayer(player,true);
            Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SyntyGameplayHudSetup.PrefabPath));
            boss=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ZombieRewardBossSetup.BossPath),new Vector3(-20,1.5f,-15),Quaternion.identity).GetComponent<BossBase>();VendorTurretSetup.Set(boss,"stayStaticForTesting",true);
        }
        static void Move(Vector3 p) { player.transform.position=p;player.GetComponent<Rigidbody>().position=p;Physics.SyncTransforms(); }
        static void ViewPickup(Transform item)
        {
            Move(item.position-Vector3.forward*1.5f+Vector3.down*item.position.y+Vector3.up*.05f);
            camera.transform.SetPositionAndRotation(item.position+new Vector3(.25f,1.1f,-2),Quaternion.LookRotation(item.position-(item.position+new Vector3(.25f,1.1f,-2))));Physics.SyncTransforms();
        }
        static void ViewDoor(PuertaConLlave door,bool far)
        {
            var b=door.GetComponent<Collider>().bounds;Vector3 normal=b.size.x<b.size.z?Vector3.right:Vector3.forward;
            Vector3 point=b.center-normal*((normal.x!=0?b.extents.x:b.extents.z)+1.3f);point.y=b.min.y+.05f;Move(point);
            Vector3 eye=point+Vector3.up*1.5f-normal*(far?7:1);Vector3 aim=new Vector3(b.center.x,b.min.y+(far?b.size.y*.5f:1.2f),b.center.z);
            camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(aim-eye));Physics.SyncTransforms();
        }
        static void ViewMapDoor(PuertaConLlave door)
        {
            var bounds=door.GetComponent<Collider>().bounds;
            Vector3 normal=bounds.size.x<bounds.size.z?Vector3.right:Vector3.forward;
            Vector3 playerPosition=bounds.center+Vector3.Cross(normal,Vector3.up)*4;playerPosition.y=bounds.min.y+.05f;Move(playerPosition);
            camera.fieldOfView=80;bool visible=false;
            foreach(float side in new[]{1f,-1f})
            {
                Vector3 eye=bounds.center-normal*side*((normal.x!=0?bounds.extents.x:bounds.extents.z)+2.8f)+Vector3.up*.2f;
                Vector3 delta=bounds.center-eye;
                var hit=Physics.RaycastAll(eye,delta.normalized,delta.magnitude+.2f,~0,QueryTriggerInteraction.Ignore)
                    .Where(h=>!h.collider.transform.IsChildOf(player.transform)).OrderBy(h=>h.distance).FirstOrDefault();
                if(hit.collider!=null && !door.OwnsCollider(hit.collider))continue;
                camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(delta));visible=true;break;
            }
            Require(visible,"Cámara de revisión ve la puerta sin paredes delante");Physics.SyncTransforms();
        }
        static bool Neon(PuertaConLlave door)=>door.GuideRenderers.All(r=>r.sharedMaterials.Length==materials[r].Length && r.sharedMaterials.Select((m,i)=>
            materials[r][i]==null ? m==null : m!=null && m!=materials[r][i] && m.IsKeywordEnabled("_EMISSION")
            && m.GetColor("_EmissionColor").r>0 && m.GetColor("_EmissionColor").g<m.GetColor("_EmissionColor").r*.05f
            && m.GetColor("_EmissionColor").b<m.GetColor("_EmissionColor").r*.05f).All(pass=>pass));
        static bool PreservesSurface(Renderer renderer)
        {
            var active=renderer.sharedMaterials; var source=materials[renderer];
            if(active.Length!=source.Length) return false;
            for(int i=0;i<source.Length;i++)
            {
                var a=source[i];var b=active[i]; if(a==null) {if(b!=null)return false;continue;}
                if(b==null || a.shader!=b.shader)return false;
                foreach(string property in a.GetTexturePropertyNames().Where(p=>p!="_EmissionMap"))
                    if(a.GetTexture(property)!=b.GetTexture(property) || a.GetTextureScale(property)!=b.GetTextureScale(property) || a.GetTextureOffset(property)!=b.GetTextureOffset(property)) return false;
                foreach(string property in new[]{"_BaseColor","_Color"}) if(a.HasProperty(property) && a.GetColor(property)!=b.GetColor(property))return false;
                foreach(string property in new[]{"_Metallic","_Smoothness","_BumpScale","_OcclusionStrength","_Cull","_AlphaClip","_Cutoff"})
                    if(a.HasProperty(property) && !Mathf.Approximately(a.GetFloat(property),b.GetFloat(property)))return false;
            }
            return true;
        }
        static bool EmissionFollowsAlbedo(Renderer renderer)
        {
            var active=renderer.sharedMaterials;var source=materials[renderer];
            for(int i=0;i<source.Length;i++)
            {
                var a=source[i]; if(a==null)continue;var b=active[i];
                string property=a.HasProperty("_BaseMap")?"_BaseMap":a.HasProperty("_MainTex")?"_MainTex":null;
                if(property!=null && (b.GetTexture("_EmissionMap")!=a.GetTexture(property) || b.GetTextureScale("_EmissionMap")!=a.GetTextureScale(property) || b.GetTextureOffset("_EmissionMap")!=a.GetTextureOffset(property)))return false;
            }
            return true;
        }
        static bool Original(PuertaConLlave door)=>door.GuideRenderers.All(r=>r.sharedMaterials.SequenceEqual(materials[r]));
        static void PrepareShop()
        {
            Move(new Vector3(0,.05f,-5));vendor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(VendorTurretSetup.VendorPath),new Vector3(0,0,-3.3f),Quaternion.identity).GetComponent<NpcVendor>();wallet.AddCoins(1000);
        }
        static void CreateTurret()
        {
            var enemy=GameObject.CreatePrimitive(PrimitiveType.Capsule);enemy.name="DurabilityTarget";enemy.transform.position=new Vector3(30,1,-8);
            enemy.AddComponent<EnemyHealth>().ApplyWaveMultipliers(1000);
            turret=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Vendor/AutoTurret.prefab"),new Vector3(30,0,-15),Quaternion.identity).GetComponent<AutoTurret>();turret.InitializeOwner(player);
            turret.HealthChanged+=()=>
            {
                shotTimes.Add(Time.time);finalShots=turret.ShotsFired;lastHealth=turret.CurrentHealth;
                if(turret.IsDepleted) stoppedAtZero=!turret.TryFire();
            };
            wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(30,1,-11);wall.transform.localScale=new Vector3(3,3,.3f);
            camera.transform.SetPositionAndRotation(new Vector3(34,2.6f,-19),Quaternion.LookRotation(new Vector3(30,1,-14)-new Vector3(34,2.6f,-19)));Physics.SyncTransforms();
        }
        static T Read<T>(object o,string name)=>(T)o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
        static void SetPrivate(object o,string name,object v)=>o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(o,v);
        static void Press() { keyboard.MakeCurrent();InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.E)); }
        static void Release()=>InputSystem.QueueStateEvent(keyboard,new KeyboardState());
        static void Capture(string name)
        {
            var rt=(RenderTexture)typeof(CharacterCustomizationValidation).GetMethod("GameViewFramebuffer",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
            if(rt==null) return;var previous=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();
            if(SystemInfo.graphicsUVStartsAtTop) { var p=image.GetPixels32();var f=new Color32[p.Length];for(int y=0;y<rt.height;y++) Array.Copy(p,y*rt.width,f,(rt.height-y-1)*rt.width,rt.width);image.SetPixels32(f);image.Apply(); }
            File.WriteAllBytes(ReportFolder+"/"+name,image.EncodeToPNG());Object.DestroyImmediate(image);RenderTexture.active=previous;
        }
        static void Check(bool pass,string label) { checks.Add((pass?"PASS ":"FAIL ")+label);if(!pass) failures++; }
        static void Require(bool pass,string label) { Check(pass,label);if(!pass) throw new Exception(label); }
        static void OnLog(string message,string trace,LogType type)
        {
            if(type!=LogType.Error && type!=LogType.Exception && type!=LogType.Assert) return;
            if(message.StartsWith("SocketException:") && trace.Contains("System.Net.")) baselineErrors.Add(message); else errors.Add(message);
        }
        static void Finish()
        {
            Application.logMessageReceived-=OnLog;Time.timeScale=1;
            var result=new Result { passed=checks.Count-failures,failed=failures,checks=checks.ToArray(),errors=errors.ToArray(),baselineErrors=baselineErrors.ToArray() };
            if(MapPreview)result.scope="Level 2 real con HDR/postproceso y transición nocturna originales: guía roja sobre puerta real, textura/UV conservadas, restauración al desactivar y capturas comparables de día y noche. Sin build ni recorrido manual completo.";
            File.WriteAllText(ReportFolder+"/validation.json",JsonUtility.ToJson(result,true));
            if(keyboard!=null&&keyboard.added) InputSystem.RemoveDevice(keyboard);if(previousSettings!=null) InputSystem.settings=previousSettings;if(settings!=null) Object.DestroyImmediate(settings);if(nav.valid) nav.Remove();
            SessionState.SetInt(Key+".Exit",failures==0?0:1);Debug.Log("DOOR_BLUEPRINT_SUPPLIES_VALIDATION="+(failures==0?"PASS":"FAIL")+" "+(checks.Count-failures)+"/"+checks.Count);EditorApplication.ExitPlaymode();
        }
    }
}
