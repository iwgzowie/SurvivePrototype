using System;
using System.IO;
using System.Linq;
using Survive.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UPP.ThirdPersonController.CameraSystems;
using Object = UnityEngine.Object;

namespace Survive.Editor
{
    public static class SyntyResultsSetup
    {
        public const string Folder="Logs/SyntyResults", Prefab="Assets/Prefabs/UI/SyntyResultsOverlay.prefab";
        static VendorUITheme theme;
        [MenuItem("Survive/UI/Integrar derrota clasificación y puntaje")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Salí de Play Mode.");
            Directory.CreateDirectory(Folder); Directory.CreateDirectory(".utmp/SyntyResultsBackup"); Build();
            var rig=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Cameras/UPPThirdPersonCamera.prefab");
            try { rig.GetComponent<UPPTPSCameraController>().AimCameraState.RightCameraOffset=1f; PrefabUtility.SaveAsPrefabAsset(rig,"Assets/Prefabs/Cameras/UPPThirdPersonCamera.prefab"); }
            finally { PrefabUtility.UnloadPrefabContents(rig); }
            foreach(string path in new[]{"Assets/Scenes/Level 2.unity","Assets/Scenes/SceneAlejandro.unity"})
            {
                var scene=SceneManager.GetSceneByPath(path); bool opened=!scene.IsValid()||!scene.isLoaded;
                if(opened) scene=EditorSceneManager.OpenScene(path,Application.isBatchMode?OpenSceneMode.Single:OpenSceneMode.Additive);
                string backup=".utmp/SyntyResultsBackup/"+scene.name+".unity";
                if(!File.Exists(backup)) File.Copy(path,backup,false);
                var menu=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MenuBehaviour>(true)).Single();
                var ui=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SyntyResultsUI>(true)).FirstOrDefault();
                if(ui==null) ui=((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),scene)).GetComponent<SyntyResultsUI>();
                Set(ui,"menu",menu); Set(menu,"resultsUi",ui); Set(menu,"gameOverContainer",ui.transform.Find("DefeatPanel").gameObject);
                Wire(ui.transform.Find("DefeatPanel/DefeatCard/Restart").GetComponent<Button>(),menu.ReloadCurrentScene);
                Wire(ui.transform.Find("DefeatPanel/DefeatCard/MainMenu").GetComponent<Button>(),menu.BackToMainMenu);
                if(!menu.IsMainMenu && menu.container!=null)
                {
                    var pause=(RectTransform)menu.container.transform.Find("Menu");pause.sizeDelta=new Vector2(650,780);
                    foreach(var pair in new[]{("Heading",300f),("Subheading",245f),("Continue",150f),("Restart",60f),("MainMenu",-120f),("Exit",-210f),("Hint",-320f)})
                        ((RectTransform)pause.Find(pair.Item1)).anchoredPosition=new Vector2(0,pair.Item2);
                    var open=pause.Find("Leaderboard");
                    if(open==null) Button(pause,"Leaderboard","CLASIFICACIÓN",new Vector2(0,-30),new Vector2(490,70),ui.OpenLeaderboard);
                    else Wire(open.GetComponent<Button>(),ui.OpenLeaderboard);
                    MusicControlsSetup.ConfigurePause(menu.gameObject);
                }
                if(menu.IsMainMenu)
                {
                    var legacyButtons=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Button>(true))
                        .Where(b=>!b.transform.IsChildOf(ui.transform) && b.GetComponentsInChildren<TMP_Text>(true)
                            .Any(t=>t.text.ToUpperInvariant().Contains("LEADER") || t.text.ToUpperInvariant().Contains("CLASIF"))).ToArray();
                    foreach(var button in legacyButtons) Wire(button,ui.OpenLeaderboard);
                    if(legacyButtons.Length>0)
                    {
                        ui.transform.Find("MainLeaderboard").gameObject.SetActive(false);
                        Set(ui,"mainOpenButton",null);
                    }
                }
                if(!menu.IsMainMenu && !scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<GameStatsManager>(true)).Any())
                {
                    var stats=new GameObject("GameStatsManager"); SceneManager.MoveGameObjectToScene(stats,scene); stats.AddComponent<GameStatsManager>();
                }
                // Las cámaras guardadas en la escena también deben usar el nuevo ajuste.
                foreach(var camera in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<UPPTPSCameraController>(true)))
                { camera.AimCameraState.RightCameraOffset=1; EditorUtility.SetDirty(camera); }
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                if(opened&&!Application.isBatchMode) EditorSceneManager.CloseScene(scene,true);
            }
            AssetDatabase.SaveAssets(); Debug.Log("SYNTY_RESULTS_SETUP=PASS");
        }
        public static void SetupAndValidate() { Run(); SyntyResultsValidation.Run(); }
        static void Build()
        {
            theme=VendorUITheme.Load(); if(theme==null) throw new Exception("Falta tema Synty.");
            var scene=EditorSceneManager.NewPreviewScene();
            var root=new GameObject("SyntyResultsOverlay",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));SceneManager.MoveGameObjectToScene(root,scene);
            try
            {
                var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=350;
                var scale=root.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1920,1080);scale.matchWidthOrHeight=.5f;
                var ui=root.AddComponent<SyntyResultsUI>(); var manager=root.AddComponent<LeaderboardManager>();Set(ui,"leaderboard",manager);
                var defeat=Overlay("DefeatPanel",root.transform);Set(ui,"defeatPanel",defeat.gameObject);
                var card=Panel("DefeatCard",defeat,new Vector2(1440,850),Vector2.zero);
                Text(card,"Heading","MISIÓN FALLIDA",new Vector2(0,345),new Vector2(1280,70),52,theme.accent);
                Text(card,"Subheading","RESUMEN DE PARTIDA",new Vector2(0,285),new Vector2(1280,40),21,theme.muted);
                Metric(ui,card,"killsText","ENEMIGOS",new Vector2(-480,150));
                Metric(ui,card,"waveText","OLEADA",new Vector2(-160,150));
                Metric(ui,card,"timeText","TIEMPO",new Vector2(-480,10));
                Metric(ui,card,"accuracyText","PRECISIÓN",new Vector2(-160,10));
                var hero=Panel("FinalScore",card,new Vector2(550,250),new Vector2(350,100));
                Text(hero,"Label","PUNTAJE FINAL",new Vector2(0,73),new Vector2(490,40),25,theme.muted);
                var score=Text(hero,"Value","0",new Vector2(0,-5),new Vector2(490,120),76,theme.accent);score.enableAutoSizing=true;score.fontSizeMin=46;score.fontSizeMax=76;Set(ui,"scoreText",score);
                Text(card,"NameLabel","NOMBRE DEL AGENTE",new Vector2(0,-78),new Vector2(680,32),20,theme.muted);
                var input=Input(card);Set(ui,"nameInput",input);
                Set(ui,"saveButton",Button(card,"SaveScore","GUARDAR PUNTAJE",new Vector2(0,-230),new Vector2(460,62),ui.SaveScore));
                Set(ui,"saveStatus",Text(card,"SaveStatus","",new Vector2(0,-282),new Vector2(1260,36),20,theme.muted));
                // Las acciones de navegación se conectan a MenuBehaviour en la escena.
                Button(card,"Restart","REINTENTAR",new Vector2(-445,-365),new Vector2(395,65),null);
                Button(card,"Leaderboard","CLASIFICACIÓN",new Vector2(0,-365),new Vector2(395,65),ui.OpenLeaderboard);
                Button(card,"MainMenu","MENÚ PRINCIPAL",new Vector2(445,-365),new Vector2(395,65),null);
                var leaderboard=Overlay("LeaderboardPanel",root.transform);Set(ui,"leaderboardPanel",leaderboard.gameObject);
                var table=Panel("LeaderboardCard",leaderboard,new Vector2(1440,920),Vector2.zero);
                Text(table,"Heading","CLASIFICACIÓN",new Vector2(0,375),new Vector2(1260,72),50,theme.accent);
                Text(table,"Subtitle","MEJORES MARCAS LOCALES",new Vector2(0,315),new Vector2(1260,35),21,theme.muted);
                Text(table,"RankHeader","POSICIÓN",new Vector2(-530,253),new Vector2(200,35),20,theme.muted);
                Text(table,"NameHeader","AGENTE",new Vector2(-110,253),new Vector2(570,35),20,theme.muted);
                Text(table,"ScoreHeader","PUNTOS",new Vector2(500,253),new Vector2(240,35),20,theme.muted);
                var rows=Rect("Rows",table,new Vector2(1250,550),new Vector2(0,-50));Set(ui,"rows",rows);
                var row=Panel("ScoreRowTemplate",rows,new Vector2(1250,46),Vector2.zero);row.GetComponent<Image>().sprite=theme.card;row.GetComponent<Image>().color=theme.cardColor;
                var view=row.gameObject.AddComponent<ScoreRowUI>();
                Set(view,"rankText",Text(row,"Rank","#1",new Vector2(-530,0),new Vector2(180,42),26,theme.accent));
                Set(view,"nameText",Text(row,"Name","AGENTE",new Vector2(-110,0),new Vector2(570,42),25,Color.white));
                Set(view,"scoreText",Text(row,"Score","0",new Vector2(500,0),new Vector2(240,42),27,theme.accent));
                Set(ui,"rowTemplate",view);row.gameObject.SetActive(false);
                Set(ui,"leaderboardBack",Button(table,"Back","VOLVER  [ESC]",new Vector2(0,-390),new Vector2(460,66),ui.CloseLeaderboard));
                var open=Button(root.transform,"MainLeaderboard","CLASIFICACIÓN",new Vector2(-235,60),new Vector2(390,66),ui.OpenLeaderboard);
                var rect=(RectTransform)open.transform;rect.anchorMin=rect.anchorMax=new Vector2(1,0);Set(ui,"mainOpenButton",open.gameObject);
                defeat.gameObject.SetActive(false);leaderboard.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root,Prefab);
            }
            finally { Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene); }
        }
        static void Wire(Button button,UnityAction action)
        {
            button.onClick=new Button.ButtonClickedEvent();UnityEventTools.AddPersistentListener(button.onClick,action);
        }
        static TMP_InputField Input(Transform parent)
        {
            var box=Panel("NameInput",parent,new Vector2(680,66),new Vector2(0,-135));box.GetComponent<Image>().sprite=theme.card;
            var viewport=Rect("Viewport",box,new Vector2(630,58),Vector2.zero);viewport.gameObject.AddComponent<RectMask2D>();
            var text=Text(viewport,"Text","",Vector2.zero,new Vector2(620,54),28,Color.white);
            var placeholder=Text(viewport,"Placeholder","TU NOMBRE",Vector2.zero,new Vector2(620,54),26,theme.muted);
            var input=box.gameObject.AddComponent<TMP_InputField>();input.textViewport=viewport;input.textComponent=text;input.placeholder=placeholder;
            input.targetGraphic=box.GetComponent<Image>();input.characterLimit=24;input.lineType=TMP_InputField.LineType.SingleLine;input.customCaretColor=true;input.caretColor=theme.accent;return input;
        }
        static void Metric(SyntyResultsUI ui,Transform parent,string field,string title,Vector2 pos)
        {
            var card=Panel(field,parent,new Vector2(290,116),pos);card.GetComponent<Image>().sprite=theme.card;
            Text(card,"Label",title,new Vector2(0,32),new Vector2(260,30),20,theme.muted);
            Set(ui,field,Text(card,"Value","0",new Vector2(0,-16),new Vector2(260,62),39,Color.white));
        }
        static RectTransform Overlay(string name,Transform parent)
        {
            var rect=Rect(name,parent,Vector2.zero,Vector2.zero);rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            rect.gameObject.AddComponent<Image>().color=new Color(.012f,.022f,.035f,.88f);return rect;
        }
        static RectTransform Panel(string name,Transform parent,Vector2 size,Vector2 pos)
        {
            var rect=Rect(name,parent,size,pos);var image=rect.gameObject.AddComponent<Image>();image.sprite=theme.panel;image.type=Image.Type.Sliced;image.color=theme.background;return rect;
        }
        static RectTransform Rect(string name,Transform parent,Vector2 size,Vector2 pos)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.sizeDelta=size;rect.anchoredPosition=pos;return rect;
        }
        static TMP_Text Text(Transform parent,string name,string value,Vector2 pos,Vector2 size,float fontSize,Color color)
        {
            var text=Rect(name,parent,size,pos).gameObject.AddComponent<TextMeshProUGUI>();text.font=theme.font;text.text=value;text.fontSize=fontSize;text.color=color;text.alignment=TextAlignmentOptions.Center;
            text.textWrappingMode=TextWrappingModes.NoWrap;text.overflowMode=TextOverflowModes.Ellipsis;text.raycastTarget=false;return text;
        }
        static Button Button(Transform parent,string name,string label,Vector2 pos,Vector2 size,UnityAction action)
        {
            var rect=Rect(name,parent,size,pos);var image=rect.gameObject.AddComponent<Image>();image.sprite=theme.button;image.type=Image.Type.Sliced;
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;var colors=button.colors;colors.normalColor=theme.accent;colors.highlightedColor=Color.white;colors.pressedColor=theme.muted;button.colors=colors;
            Text(rect,"Label",label,Vector2.zero,size-new Vector2(16,6),24,new Color(.02f,.04f,.06f));if(action!=null) UnityEventTools.AddPersistentListener(button.onClick,action);return button;
        }
        static void Set(Object target,string name,Object value)=>VendorTurretSetup.Set(target,name,value);
    }
}
