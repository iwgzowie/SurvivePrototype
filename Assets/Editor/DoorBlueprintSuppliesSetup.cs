using System;
using System.IO;
using System.Linq;
using Survive.Economy;
using Survive.Items;
using Survive.Turrets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Survive.Editor
{
    public static class DoorBlueprintSuppliesSetup
    {
        public static void SetupAndValidate() { Run(); DoorBlueprintSuppliesValidation.Run(); }
        public const string Folder = "Logs/DoorBlueprintSupplies";
        public const string Blueprint = "Assets/Prefabs/Items/DoorBlueprint.prefab";
        public const string Ammo = "Assets/Data/Vendor/AD_CommonAmmoBox.asset";
        const string Neon = "Assets/Resources/Gameplay/DoorGuideNeon.mat";
        [MenuItem("Survive/Gameplay/Integrar planos puertas y suministros")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Salí de Play Mode.");
            Directory.CreateDirectory(Folder); Directory.CreateDirectory(".utmp/DoorBlueprintSuppliesBackup");
            Ensure("Assets/Prefabs/Items"); Ensure("Assets/Resources/Gameplay");
            var neon = Material(Neon, new Color(.02f,.85f,1), new Color(.02f,1,1)*3f);
            var paper = Material("Assets/Resources/Gameplay/DoorBlueprintPaper.mat",new Color(.025f,.22f,.5f),new Color(.02f,.15f,.4f));
            const string texturePath="Assets/Resources/Gameplay/DoorBlueprintGrid.asset";
            var grid=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if(grid==null)
            {
                grid=new Texture2D(256,256,TextureFormat.RGBA32,false); grid.name="DoorBlueprintGrid";
                for(int y=0;y<256;y++) for(int x=0;x<256;x++)
                {
                    bool line=x%24<1 || y%24<1;
                    bool doorLine=(x>=70 && x<=185 && (y>=45 && y<=50 || y>=205 && y<=210))
                        || (y>=45 && y<=210 && (x>=70 && x<=75 || x>=180 && x<=185))
                        || x>=157 && x<=163 && y>=119 && y<=125;
                    grid.SetPixel(x,y,doorLine?new Color(.8f,1,1):line?new Color(.12f,.4f,.7f):new Color(.02f,.12f,.3f));
                }
                grid.Apply(); AssetDatabase.CreateAsset(grid,texturePath);
            }
            paper.SetColor("_BaseColor",Color.white); paper.SetTexture("_BaseMap",grid); EditorUtility.SetDirty(paper);
            var root=new GameObject("DoorBlueprint");
            try
            {
                var visual=GameObject.CreatePrimitive(PrimitiveType.Cube); visual.name="BlueprintSheet"; visual.transform.SetParent(root.transform,false);
                visual.transform.localScale=new Vector3(.7f,.018f,.5f); Object.DestroyImmediate(visual.GetComponent<Collider>());
                visual.GetComponent<Renderer>().sharedMaterial=paper;
                // Dos bordes enrollados hacen que el plano se reconozca como objeto de papel.
                foreach(float x in new[]{-.35f,.35f})
                {
                    var roll=GameObject.CreatePrimitive(PrimitiveType.Cylinder); roll.name="RolledEdge"; roll.transform.SetParent(root.transform,false);
                    roll.transform.localPosition=new Vector3(x,.015f,0); roll.transform.localRotation=Quaternion.Euler(90,0,0); roll.transform.localScale=new Vector3(.045f,.27f,.045f);
                    Object.DestroyImmediate(roll.GetComponent<Collider>()); roll.GetComponent<Renderer>().sharedMaterial=paper;
                }
                var col=root.AddComponent<BoxCollider>(); col.size=new Vector3(.85f,.3f,.65f); col.isTrigger=true;
                root.AddComponent<DoorBlueprintPickup>(); PrefabUtility.SaveAsPrefabAsset(root,Blueprint);
            }
            finally { Object.DestroyImmediate(root); }
            string bossPath=ZombieRewardBossSetup.BossPath; var boss=PrefabUtility.LoadPrefabContents(bossPath);
            try { VendorTurretSetup.Set(boss.GetComponent<BossBase>(),"doorBlueprintPrefab",AssetDatabase.LoadAssetAtPath<GameObject>(Blueprint)); PrefabUtility.SaveAsPrefabAsset(boss,bossPath); }
            finally { PrefabUtility.UnloadPrefabContents(boss); }
            var ammo=AssetDatabase.LoadAssetAtPath<AmmoBoxDefinition>(Ammo);
            if(ammo==null) { ammo=ScriptableObject.CreateInstance<AmmoBoxDefinition>(); AssetDatabase.CreateAsset(ammo,Ammo); }
            ammo.rounds=60; ammo.icon=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Synty/InterfaceMilitaryCombatHUD/Sprites/Icons_Inventory/ICON_MilitaryCombat_Inventory_Ammo_Bullets_02_Clean.png"); EditorUtility.SetDirty(ammo);
            var catalog=AssetDatabase.LoadAssetAtPath<VendorCatalog>("Assets/Data/Vendor/VC_Armero.asset");
            var data=new SerializedObject(catalog); var offers=data.FindProperty("offers");
            bool exists=Enumerable.Range(0,catalog.Count).Any(i=>catalog.GetOffer(i).IsCommonAmmo);
            if(!exists)
            {
                int i=offers.arraySize; offers.arraySize++; var offer=offers.GetArrayElementAtIndex(i);
                foreach(string field in new[]{"weaponPrefab","turret","explosiveMagazine"}) offer.FindPropertyRelative(field).objectReferenceValue=null;
                offer.FindPropertyRelative("commonAmmo").objectReferenceValue=ammo; offer.FindPropertyRelative("price").intValue=30;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            var td=AssetDatabase.LoadAssetAtPath<TurretDefinition>("Assets/Data/Vendor/TD_AutoTurret.asset"); data=new SerializedObject(td);
            data.FindProperty("maxHealth").floatValue=150; data.FindProperty("healthCostPerShot").floatValue=10; data.FindProperty("shotsPerSecond").floatValue=2; data.ApplyModifiedPropertiesWithoutUndo();
            var turret=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Vendor/AutoTurret.prefab");
            try { if(turret.GetComponent<TurretHealthDisplay>()==null) turret.AddComponent<TurretHealthDisplay>(); PrefabUtility.SaveAsPrefabAsset(turret,"Assets/Prefabs/Vendor/AutoTurret.prefab"); }
            finally { PrefabUtility.UnloadPrefabContents(turret); }
            ConfigureDoors(); AssetDatabase.SaveAssets();
            Debug.Log("DOOR_BLUEPRINT_SUPPLIES_SETUP=PASS");
        }
        static void ConfigureDoors()
        {
            const string path="Assets/Scenes/Level 2.unity";
            var scene=SceneManager.GetSceneByPath(path); bool opened=!scene.IsValid() || !scene.isLoaded;
            if(opened) scene=EditorSceneManager.OpenScene(path,Application.isBatchMode?OpenSceneMode.Single:OpenSceneMode.Additive);
            if(!File.Exists(".utmp/DoorBlueprintSuppliesBackup/Level2-disk.unity")) File.Copy(path,".utmp/DoorBlueprintSuppliesBackup/Level2-disk.unity",false);
            const string copy="Assets/Scenes/Level2-BeforeBlueprint.temp.unity";
            EditorSceneManager.SaveScene(scene,copy,true); File.Copy(copy,".utmp/DoorBlueprintSuppliesBackup/Level2-editor.unity",true); AssetDatabase.DeleteAsset(copy);
            string report="";
            foreach(var door in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PuertaConLlave>(true)))
            {
                int order=Array.IndexOf(DoorKeyProgressionSetup.Names,door.name); if(order<0) continue;
                var bounds=new Bounds(door.transform.position,Vector3.zero); bool first=true;
                foreach(var renderer in door.GuideRenderers) { if(first) { bounds=renderer.bounds; first=false; } else bounds.Encapsulate(renderer.bounds); }
                foreach(var blocker in door.BlockingObjects.Where(b=>b!=null)) foreach(var col in blocker.GetComponentsInChildren<Collider>()) bounds.Encapsulate(col.bounds);
                var data=new SerializedObject(door); data.FindProperty("guideOrder").intValue=order;
                data.FindProperty("openingDuration").floatValue=1.4f;
                data.FindProperty("openingTranslation").vector3Value=Vector3.up*Mathf.Max(5,bounds.size.y+1);
                data.ApplyModifiedPropertiesWithoutUndo();
                foreach(var target in new[]{door.gameObject}.Concat(door.BlockingObjects).Where(b=>b!=null))
                    foreach(var child in target.GetComponentsInChildren<Transform>(true)) child.gameObject.isStatic=false;
                report+=door.name+" height="+bounds.size.y+" lift="+Mathf.Max(5,bounds.size.y+1)+" renderers="+door.GuideRenderers.Length+"\n";
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            File.WriteAllText(Folder+"/configuration.txt",report+"Turret: 150 HP, 10/shot, 2 shots/s. Ammo box: 60 rounds, 30 coins.\n");
            if(opened && !Application.isBatchMode) EditorSceneManager.CloseScene(scene,true);
        }
        static Material Material(string path,Color color,Color emission)
        {
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null) { mat=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat,path); }
            mat.SetColor("_BaseColor",color); mat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive; mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor",emission); mat.SetFloat("_Smoothness",.65f); EditorUtility.SetDirty(mat); return mat;
        }
        static void Ensure(string path)
        {
            if(AssetDatabase.IsValidFolder(path)) return;
            string parent=Path.GetDirectoryName(path).Replace('\\','/'); Ensure(parent); AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
        }
    }
}
