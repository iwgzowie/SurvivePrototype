using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Survive.Character
{
    /// <summary>Editor de un borrador local: sólo el botón de confirmación entrega el perfil al menú.</summary>
    [DisallowMultipleComponent]
    public sealed class CharacterCreationMenu : MonoBehaviour
    {
        private static readonly Color Background = new Color(.018f, .027f, .036f, .12f);
        private static readonly Color Ink = new Color(.018f, .027f, .036f, 1f);
        private static readonly Color Panel = new Color(.035f, .052f, .066f, .86f);
        private static readonly Color Control = new Color(.075f, .105f, .128f, .94f);
        private static readonly Color Accent = new Color(.32f, .79f, .65f);
        private static readonly Color Muted = new Color(.62f, .69f, .76f);
        private const int PreviewLayer = 31;
        // Un único volumen de presentación para todas las piezas, géneros y proporciones.
        // Cambiar la apariencia no debe hacer zoom ni desplazar el estudio.
        private const float PreviewRadius = 1.25f;
        private const float PreviewHeight = 2.45f;
        private const float PlatformHeight = .102f;
        private static readonly Vector3 StageCenter = new Vector3(.68f, PlatformHeight, 0);
        private static readonly float[] PresetSizes = { -70, 0, 70, 0 };
        private static readonly float[] PresetMuscles = { 0, 0, 15, 100 };
        private static CharacterCreationMenu activeInstance;

        private SidekickCharacterCatalog catalog;
        private CharacterProfile draft;
        private Func<CharacterProfile, string> confirm;
        private MenuBehaviour owner;
        private Transform content;
        private TMP_Text errorText;
        private TMP_InputField nameInput;
        private ScrollRect optionsScroll;
        private Button confirmButton;
        private readonly List<Button> tabButtons = new List<Button>();
        private readonly List<Button> bodyPresetButtons = new List<Button>();
        private readonly Dictionary<Selectable, bool> previousControls = new Dictionary<Selectable, bool>();
        private readonly Dictionary<Canvas, bool> previousCanvases = new Dictionary<Canvas, bool>();
        private readonly Dictionary<Camera, int> previousCameraMasks = new Dictionary<Camera, int>();
        private readonly Dictionary<string, Material> studioMaterialLookup = new Dictionary<string, Material>();
        private readonly List<Material> studioMaterials = new List<Material>();
        private GameObject previewRoot;
        private GameObject previewRig;
        private Camera previewCamera;
        private SidekickCharacterAppearance appearance;
        private EventSystem createdEventSystem;
        private GameObject previouslySelected;
        private readonly string[] selectedPacks = { "", "", "" };
        private readonly float[] scrollPositions = { 1, 1, 1 };
        private readonly string[] selectedRecipes = { "", "" };
        private int selectedTab;
        private float rotation;
        private bool confirming;
        private bool draggingPreview;
        private int previewScreenWidth;
        private int previewScreenHeight;

        public bool IsOpen { get; private set; }
        public CharacterProfile Draft => draft;

        public static CharacterCreationMenu Open(MenuBehaviour owner, SidekickCharacterCatalog catalog,
            Func<CharacterProfile, string> onConfirm)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (activeInstance != null && activeInstance.IsOpen) return activeInstance;
            var root = new GameObject("CharacterCreationMenu", typeof(RectTransform));
            var menu = root.AddComponent<CharacterCreationMenu>();
            activeInstance = menu;
            menu.owner = owner;
            menu.catalog = catalog;
            menu.confirm = onConfirm;
            menu.IsOpen = true;
            try
            {
                menu.draft = catalog.CreateDefault();
                menu.Build();
                return menu;
            }
            catch
            {
                // Una apertura incompleta debe devolver el menú y permitir reintentar Play.
                menu.Close();
                throw;
            }
        }

        private void Build()
        {
            // La navegación automática de gamepad también debe quedar dentro del creador.
            foreach (Selectable control in FindObjectsByType<Selectable>(FindObjectsSortMode.None))
            {
                previousControls[control] = control.interactable;
                control.interactable = false;
            }
            if (EventSystem.current == null)
            {
                var events = new GameObject("CharacterCreationEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
                createdEventSystem = events.GetComponent<EventSystem>();
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            previouslySelected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

            // El estudio 3D se dibuja como una cámara de escena completa; se ocultan las
            // capas UI del menú original para que no queden visibles bajo el creador.
            foreach (Canvas existingCanvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                previousCanvases[existingCanvas] = existingCanvas.enabled;
                existingCanvas.enabled = false;
            }
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            // TMP reserva 30000 para la lista y 29999 para su bloqueador.
            // El formulario debe quedar debajo de ambos, también para los raycasts.
            canvas.sortingOrder = 1000;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 900);
            scaler.matchWidthOrHeight = .5f;
            gameObject.AddComponent<GraphicRaycaster>();
            var background = gameObject.AddComponent<Image>();
            background.color = Background;
            background.raycastTarget = false;
            Stretch((RectTransform)transform);

            RectTransform stageInput = Rect("StudioDragArea", transform, Vector2.zero, new Vector2(.645f, 1));
            var stageInputImage = stageInput.gameObject.AddComponent<Image>();
            stageInputImage.color = new Color(1, 1, 1, 0);
            stageInputImage.raycastTarget = true;
            var trigger = stageInput.gameObject.AddComponent<EventTrigger>();
            AddPointerEvent(trigger, EventTriggerType.BeginDrag, _ => draggingPreview = true);
            AddPointerEvent(trigger, EventTriggerType.EndDrag, _ => draggingPreview = false);
            AddPointerEvent(trigger, EventTriggerType.Drag, data => Rotate(-((PointerEventData)data).delta.x * .5f));

            RectTransform sidePanel = Rect("Customization", transform, new Vector2(.65f, .035f), new Vector2(.985f, .965f));
            var panelImage = sidePanel.gameObject.AddComponent<Image>();
            panelImage.color = Panel;
            panelImage.raycastTarget = true;
            var title = Label("CREÁ TU PERSONAJE", sidePanel, 26, Color.white, 42);
            Place(title.rectTransform, new Vector2(.045f, .925f), new Vector2(.96f, .99f));
            title.alignment = TextAlignmentOptions.MidlineLeft;
            var subtitle = Label("Elegí tu apariencia antes de sobrevivir.", sidePanel, 14, Muted, 32);
            Place(subtitle.rectTransform, new Vector2(.045f, .885f), new Vector2(.96f, .935f));

            RectTransform tabRow = Rect("Tabs", sidePanel, new Vector2(.035f, .815f), new Vector2(.965f, .875f));
            var tabs = tabRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabs.spacing = 6;
            tabs.childControlHeight = true;
            tabs.childControlWidth = true;
            tabs.childForceExpandWidth = true;
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                tabButtons.Add(Button(new[] { "CARA", "CUERPO", "ROPA" }[i], tabRow, () => SelectTab(index), 44));
            }

            RectTransform scrollRoot = Rect("Options", sidePanel, new Vector2(.035f, .16f), new Vector2(.965f, .80f));
            optionsScroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            optionsScroll.horizontal = false;
            optionsScroll.movementType = ScrollRect.MovementType.Clamped;
            optionsScroll.scrollSensitivity = 35;
            RectTransform viewport = Rect("Viewport", scrollRoot, Vector2.zero, Vector2.one,
                Vector2.zero, new Vector2(-16, 0));
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform list = Rect("Content", viewport, new Vector2(0, 1), Vector2.one);
            list.pivot = new Vector2(.5f, 1);
            list.sizeDelta = Vector2.zero;
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 10, 14);
            layout.spacing = 12;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            optionsScroll.viewport = viewport;
            optionsScroll.content = list;
            AddVerticalScrollbar(optionsScroll, scrollRoot);
            content = list;

            errorText = Label("", sidePanel, 14, new Color(1f, .59f, .51f), 38);
            Place(errorText.rectTransform, new Vector2(.045f, .105f), new Vector2(.955f, .155f));
            errorText.textWrappingMode = TextWrappingModes.Normal;
            Button("Volver", Rect("Cancel", sidePanel, new Vector2(.035f, .035f), new Vector2(.40f, .095f)), Close, 46);
            confirmButton = Button("CREAR Y JUGAR", Rect("Confirm", sidePanel, new Vector2(.425f, .035f), new Vector2(.965f, .095f)), Confirm, 46, true);

            TMP_Text stageCaption = Label("VISTA 3D  ·  ARRASTRÁ PARA GIRAR", transform, 15, Color.white, 32);
            Place(stageCaption.rectTransform, new Vector2(.035f, .045f), new Vector2(.35f, .095f));
            stageCaption.fontStyle = FontStyles.Bold;
            stageCaption.alignment = TextAlignmentOptions.Center;
            RectTransform rotateRow = Rect("Rotation", transform, new Vector2(.38f, .045f), new Vector2(.63f, .095f));
            var rotateLayout = rotateRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            rotateLayout.spacing = 6;
            rotateLayout.childControlWidth = true;
            rotateLayout.childControlHeight = true;
            rotateLayout.childForceExpandWidth = true;
            Button("←", rotateRow, () => Rotate(30), 40);
            Button("Frente", rotateRow, () => { rotation = 0; Rotate(0); }, 40);
            Button("→", rotateRow, () => Rotate(-30), 40);

            CreatePreview();
            SelectTab(0);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(tabButtons[0].gameObject);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void SelectTab(int index)
        {
            SaveScrollPosition();
            selectedTab = index;
            for (int i = 0; i < tabButtons.Count; i++)
            {
                tabButtons[i].GetComponent<Image>().color = i == index ? Accent : Control;
                tabButtons[i].GetComponentInChildren<TMP_Text>().color = i == index ? Ink : Color.white;
            }
            RebuildOptions(false);
        }

        private void SaveScrollPosition()
        {
            if (optionsScroll != null && content != null && content.childCount > 0)
                scrollPositions[selectedTab] = optionsScroll.verticalNormalizedPosition;
        }

        private void RebuildOptions(bool preserveScroll = true)
        {
            if (preserveScroll) SaveScrollPosition();
            bodyPresetButtons.Clear();
            foreach (Transform child in content)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            Label("PERFIL", content, 14, Accent, 23);
            nameInput = Input("Nombre del personaje", content, draft.displayName ?? "", 48);
            nameInput.characterLimit = 24;
            nameInput.onValueChanged.AddListener(value => draft.displayName = value);
            Dropdown("Género", content, new List<string> { "Masculino", "Femenino" },
                draft.gender == CharacterGender.Feminine ? 1 : 0, value =>
                {
                    draft.gender = value == 1 ? CharacterGender.Feminine : CharacterGender.Masculine;
                    RefreshPreview();
                    RebuildOptions();
                });
            if (selectedTab == 1)
            {
                BodyControls();
            }
            else
            {
                bool face = selectedTab == 0;
                Label(face ? "CARA Y PELO" : "ROPA Y COMPLEMENTOS", content, 14, Accent, 26);
                Label(face ? "Combiná caras, pelo y rasgos de los packs instalados."
                    : "Elegí un conjunto completo o combiná las piezas una por una.", content, 17, Muted, 42);
                RecipeControls(face);
                var slotParts = catalog.parts.Where(part => face == SidekickCharacterCatalog.IsFaceSlot(part.slot)).ToArray();
                var packs = slotParts.Select(part => part.pack).Distinct().OrderBy(pack => pack).ToList();
                packs.Insert(0, "");
                string selectedPack = selectedPacks[selectedTab];
                int selected = packs.IndexOf(selectedPack);
                var packLabels = packs.Select(pack => string.IsNullOrEmpty(pack) ? "Todas las categorías"
                    : SidekickCharacterLabels.Pack(pack)).ToList();
                Dropdown("Filtrar piezas", content, packLabels, Mathf.Max(0, selected), value =>
                {
                    selectedPacks[selectedTab] = packs[value];
                    RebuildOptions();
                });
                foreach (string slot in slotParts.Select(part => part.slot).Distinct().OrderBy(SlotOrder).ThenBy(SlotLabel))
                    PartControl(slot);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)content);
            Canvas.ForceUpdateCanvases();
            optionsScroll.StopMovement();
            optionsScroll.verticalNormalizedPosition = scrollPositions[selectedTab];
            if (EventSystem.current != null && (EventSystem.current.currentSelectedGameObject == null
                || !EventSystem.current.currentSelectedGameObject.activeInHierarchy))
                EventSystem.current.SetSelectedGameObject(tabButtons[selectedTab].gameObject);
        }

        private void BodyControls()
        {
            Label("CUERPO", content, 14, Accent, 28);
            Label("El género y las proporciones se aplican al cuerpo, la cara y la ropa compatibles.", content, 17, Muted, 52);
            RectTransform presetRow = Rect("BodyPresets", content, Vector2.zero, Vector2.one);
            Height(presetRow.gameObject, 46);
            var group = presetRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = 7;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = true;
            string[] labels = { "Delgado", "Normal", "Robusto", "Musculoso" };
            for (int i = 0; i < labels.Length; i++)
            {
                int preset = i;
                bodyPresetButtons.Add(Button(labels[i], presetRow, () =>
                {
                    draft.bodySize = PresetSizes[preset];
                    draft.muscle = PresetMuscles[preset];
                    RefreshPreview();
                    RebuildOptions();
                }, 44));
            }
            UpdateBodyPresetSelection();
            Slider("Complexión", draft.bodySize, -100, 100,
                value => { draft.bodySize = value; RefreshPreview(); UpdateBodyPresetSelection(); });
            Slider("Musculatura", draft.muscle, 0, 100,
                value => { draft.muscle = value; RefreshPreview(); UpdateBodyPresetSelection(); });
            Label("Las piezas robóticas conservan su forma original cuando no incluyen deformaciones corporales.",
                content, 16, Muted, 65);
        }

        private void UpdateBodyPresetSelection()
        {
            for (int i = 0; i < bodyPresetButtons.Count; i++)
            {
                bool selected = Mathf.Approximately(draft.bodySize, PresetSizes[i])
                    && Mathf.Approximately(draft.muscle, PresetMuscles[i]);
                bodyPresetButtons[i].GetComponent<Image>().color = selected ? Accent : Control;
                bodyPresetButtons[i].GetComponentInChildren<TMP_Text>().color = selected ? Ink : Color.white;
            }
        }

        private bool MatchesRecipe(SidekickCharacterCatalog.Recipe recipe, bool face)
        {
            var selections = draft.parts.Where(part => face == SidekickCharacterCatalog.IsFaceSlot(part.slot)).ToArray();
            var recipeParts = recipe.parts.Where(part => face == SidekickCharacterCatalog.IsFaceSlot(part.slot)).ToArray();
            return selections.Length == recipeParts.Length && recipeParts.All(part =>
                selections.Any(selection => selection.slot == part.slot && selection.partId == part.partId));
        }

        private void RecipeControls(bool face)
        {
            var recipes = catalog.recipes.Where(recipe => recipe.parts != null && recipe.parts.Any(selection =>
                face == SidekickCharacterCatalog.IsFaceSlot(selection.slot))).ToArray();
            int category = face ? 0 : 1;
            int selected = Array.FindIndex(recipes, recipe => recipe.id == selectedRecipes[category] && MatchesRecipe(recipe, face));
            if (selected < 0) selected = Array.FindIndex(recipes, recipe => MatchesRecipe(recipe, face));
            selectedRecipes[category] = selected < 0 ? "" : recipes[selected].id;
            var labels = new List<string> { face ? "Cara personalizada" : "Conjunto personalizado" };
            labels.AddRange(recipes.Select(recipe => SidekickCharacterLabels.Recipe(recipe, draft.gender)));
            Dropdown(face ? "Caras de los personajes Sidekick" : "Conjuntos Sidekick", content, labels, selected + 1, value =>
            {
                if (value <= 0 || value > recipes.Length) { RebuildOptions(); return; }
                selectedRecipes[category] = recipes[value - 1].id;
                if (face) catalog.ApplyFaceRecipe(draft, recipes[value - 1].id);
                else catalog.ApplyOutfitRecipe(draft, recipes[value - 1].id);
                RefreshPreview();
                RebuildOptions();
            });
        }

        private void PartControl(string slot)
        {
            string selectedPack = selectedPacks[selectedTab];
            var choices = catalog.GetParts(slot, draft.gender)
                .Where(part => string.IsNullOrEmpty(selectedPack) || part.pack == selectedPack)
                .OrderBy(part => part.pack).ThenBy(part => part.label).ToList();
            if (choices.Count == 0) return;
            CharacterPartSelection current = draft.parts.FirstOrDefault(part => part.slot == slot);
            var selectedPart = current != null ? catalog.FindPart(current.partId) : null;
            if (selectedPart != null && choices.All(part => part.id != selectedPart.id)) choices.Insert(0, selectedPart);
            bool optional = !SidekickCharacterCatalog.RequiredSlots.Contains(slot);
            var ids = new List<string>();
            var labels = new List<string>();
            if (optional) { ids.Add(""); labels.Add("Sin pieza"); }
            foreach (var part in choices)
            {
                ids.Add(part.id);
                labels.Add(SidekickCharacterLabels.Part(catalog, part, draft.gender));
            }
            int index = current != null ? ids.IndexOf(current.partId) : 0;
            Dropdown(SlotLabel(slot), content, labels, Mathf.Max(0, index), value =>
            {
                draft.parts.RemoveAll(selection => selection.slot == slot);
                if (value >= 0 && value < ids.Count && !string.IsNullOrEmpty(ids[value]))
                    draft.parts.Add(new CharacterPartSelection { slot = slot, partId = ids[value] });
                RefreshPreview();
                RebuildOptions();
            });
        }

        private void RefreshPreview()
        {
            errorText.text = "";
            if (appearance != null && !appearance.Apply(draft))
                errorText.text = "No se pudo mostrar esta combinación. Elegí otra pieza antes de crear el perfil.";
            AlignPreviewFeet();
            if (previewRig != null)
                foreach (Transform node in previewRig.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = PreviewLayer;
        }

        private void CreatePreview()
        {
            if (catalog.previewRig == null)
            {
                errorText.text = "Falta el rig de vista previa. Ejecutá la integración Sidekick desde el Editor.";
                confirmButton.interactable = false;
                return;
            }
            previewRoot = new GameObject("CharacterCreationPreview");
            previewRoot.transform.position = Vector3.zero;
            previewRoot.layer = PreviewLayer;
            CreateStudioEnvironment(previewRoot.transform);
            previewRig = Instantiate(catalog.previewRig, previewRoot.transform, false);
            previewRig.name = "CharacterPreviewRig";
            previewRig.transform.localPosition = StageCenter;
            foreach (var animator in previewRig.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            foreach (var listener in previewRig.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            foreach (var collider in previewRig.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (Transform node in previewRig.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = PreviewLayer;
            appearance = previewRig.GetComponent<SidekickCharacterAppearance>();
            if (appearance == null) appearance = previewRig.AddComponent<SidekickCharacterAppearance>();
            appearance.Configure(catalog, false);
            var cameraObject = new GameObject("CharacterPreviewCamera", typeof(Camera));
            cameraObject.transform.SetParent(previewRoot.transform, false);
            previewCamera = cameraObject.GetComponent<Camera>();
            previewCamera.cullingMask = 1 << PreviewLayer;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(.035f, .055f, .066f);
            previewCamera.orthographic = false;
            previewCamera.fieldOfView = 26f;
            previewCamera.nearClipPlane = .05f;
            previewCamera.farClipPlane = 40f;
            previewCamera.rect = new Rect(0, 0, 1, 1);
            previewCamera.depth = 100;
            previewCamera.allowHDR = false;
            previewCamera.allowMSAA = true;
            ExcludeStudioLayerFromOtherCameras();
            RefreshPreview();
            FramePreview();
        }

        private void CreateStudioEnvironment(Transform parent)
        {
            CreateStudioMaterial("StudioFloor", new Color(.12f, .16f, .17f), .18f, .42f);
            CreateStudioMaterial("StudioWall", new Color(.075f, .105f, .115f), .12f, .32f);
            CreateStudioMaterial("StudioMetal", new Color(.20f, .27f, .27f), .58f, .38f);
            CreateStudioMaterial("StudioAccent", new Color(.25f, .63f, .53f), .3f, .4f);
            CreateStudioMaterial("StudioWarm", new Color(.70f, .39f, .19f), .18f, .38f);

            CreateStudioBox(parent, "Floor", new Vector3(0, -.24f, 0), new Vector3(14, .42f, 14), "StudioFloor");
            // La cámara mira desde +Z: el fondo siempre queda detrás del personaje.
            CreateStudioBox(parent, "BackWall", new Vector3(0, 2.35f, -3.3f), new Vector3(14, 5.2f, .32f), "StudioWall");
            CreateStudioBox(parent, "BackWallHeader", new Vector3(0, 4.65f, -3.08f), new Vector3(14, .16f, .16f), "StudioMetal");
            CreateStudioBox(parent, "WallPanelLeft", new Vector3(-3.5f, 1.7f, -3.08f), new Vector3(.12f, 3.1f, .12f), "StudioMetal");
            CreateStudioBox(parent, "WallPanelRight", new Vector3(3.5f, 1.7f, -3.08f), new Vector3(.12f, 3.1f, .12f), "StudioAccent");
            CreateStudioBox(parent, "WallMark", new Vector3(-2.1f, 3.7f, -3.05f), new Vector3(1.25f, .08f, .1f), "StudioWarm");

            CreateStudioPrimitive(parent, "Turntable", PrimitiveType.Cylinder, new Vector3(.68f, -.015f, 0),
                new Vector3(1.18f, .10f, 1.18f), "StudioMetal");
            CreateStudioPrimitive(parent, "TurntableTop", PrimitiveType.Cylinder, new Vector3(.68f, .045f, 0),
                new Vector3(1.08f, .055f, 1.08f), "StudioFloor");
            CreateStudioBox(parent, "TurntableAccent", new Vector3(.68f, .08f, .53f), new Vector3(.72f, .025f, .025f), "StudioAccent");

            // Props and light strips add depth to the full-screen stage without crossing the figure.
            CreateStudioBox(parent, "CrateLeft", new Vector3(-2.55f, .32f, .55f), new Vector3(.78f, .62f, .72f), "StudioMetal");
            CreateStudioBox(parent, "CrateLeftTop", new Vector3(-2.55f, .65f, .55f), new Vector3(.62f, .035f, .78f), "StudioWarm");
            CreateStudioBox(parent, "CrateRight", new Vector3(2.35f, .25f, 1.05f), new Vector3(.68f, .48f, .65f), "StudioMetal");
            CreateStudioBox(parent, "FloorLightLeft", new Vector3(-2.6f, -.015f, -.3f), new Vector3(1.05f, .025f, .055f), "StudioAccent");
            CreateStudioBox(parent, "FloorLightRight", new Vector3(2.15f, -.015f, -.5f), new Vector3(.85f, .025f, .055f), "StudioWarm");

            CreateStudioLight(parent, "StudioKey", new Vector3(-3.4f, 4.2f, 2.2f), Quaternion.Euler(38, 155, 0),
                new Color(1f, .91f, .78f), 1.35f, true);
            CreateStudioLight(parent, "StudioFill", new Vector3(3.1f, 3.0f, 1.6f), Quaternion.Euler(25, -145, 0),
                new Color(.66f, .84f, 1f), .75f, false);
            CreateStudioLight(parent, "StudioRim", new Vector3(-.4f, 3.1f, -3.0f), Quaternion.Euler(35, 0, 0),
                new Color(.39f, .88f, .75f), 1.05f, false);
        }

        private Material BuildStudioMaterial(string name, Color color, float metallic, float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var material = new Material(shader) { name = name, color = color };
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            studioMaterials.Add(material);
            return material;
        }

        private void CreateStudioMaterial(string name, Color color, float metallic, float smoothness)
        {
            studioMaterialLookup[name] = BuildStudioMaterial(name, color, metallic, smoothness);
        }

        private void CreateStudioBox(Transform parent, string name, Vector3 position, Vector3 scale, string materialName)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = scale;
            SetStudioLayer(box.transform);
            var collider = box.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            box.GetComponent<Renderer>().sharedMaterial = studioMaterialLookup[materialName];
        }

        private void CreateStudioPrimitive(Transform parent, string name, PrimitiveType type, Vector3 position,
            Vector3 scale, string materialName)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = position;
            primitive.transform.localScale = scale;
            SetStudioLayer(primitive.transform);
            var collider = primitive.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            primitive.GetComponent<Renderer>().sharedMaterial = studioMaterialLookup[materialName];
        }

        private static void SetStudioLayer(Transform root)
        {
            foreach (Transform node in root.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = PreviewLayer;
        }

        private static void CreateStudioLight(Transform parent, string name, Vector3 position, Quaternion rotation,
            Color color, float intensity, bool shadows)
        {
            var lightObject = new GameObject(name, typeof(Light));
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = position;
            lightObject.transform.localRotation = rotation;
            lightObject.layer = PreviewLayer;
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.color = color;
            light.cullingMask = 1 << PreviewLayer;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            light.shadowStrength = .62f;
        }

        private void ExcludeStudioLayerFromOtherCameras()
        {
            foreach (Camera camera in Camera.allCameras)
            {
                if (camera == null || camera == previewCamera) continue;
                if (!previousCameraMasks.ContainsKey(camera)) previousCameraMasks.Add(camera, camera.cullingMask);
                camera.cullingMask &= ~(1 << PreviewLayer);
            }
        }

        private void FramePreview()
        {
            if (previewCamera == null || previewRig == null) return;
            previewScreenWidth = Screen.width;
            previewScreenHeight = Screen.height;
            // Se reserva el espacio izquierdo para TODO el cuerpo, incluso al girarlo.
            // El volumen fijo incluye los accesorios y el cuerpo girado; no depende
            // de los bounds de la opción seleccionada ni de los bounds de culling.
            var stageViewport = new Rect(.055f, .12f, .56f, .82f);
            float aspect = Mathf.Max(.1f, previewCamera.aspect);
            float tangent = Mathf.Tan(previewCamera.fieldOfView * .5f * Mathf.Deg2Rad);
            const float pitch = 5f * Mathf.Deg2Rad;
            float verticalExtent = PreviewHeight * .5f * Mathf.Cos(pitch) + PreviewRadius * Mathf.Sin(pitch);
            float depthExtent = PreviewRadius * Mathf.Cos(pitch) + PreviewHeight * .5f * Mathf.Sin(pitch);
            float distance = Mathf.Max(verticalExtent / (tangent * stageViewport.height),
                PreviewRadius / (tangent * aspect * stageViewport.width)) * 1.04f + depthExtent;
            previewCamera.transform.rotation = Quaternion.Euler(5, 180, 0);
            Vector3 focus = previewRoot.transform.TransformPoint(StageCenter + Vector3.up * (PreviewHeight * .5f));
            float viewHeight = 2 * distance * tangent;
            previewCamera.transform.position = focus - previewCamera.transform.forward * distance
                + previewCamera.transform.right * ((.5f - stageViewport.center.x) * viewHeight * aspect)
                + previewCamera.transform.up * ((.5f - stageViewport.center.y) * viewHeight);
            foreach (Transform node in previewRig.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = PreviewLayer;
            ExcludeStudioLayerFromOtherCameras();
        }

        private void AlignPreviewFeet()
        {
            if (previewRig == null) return;
            var baked = new Mesh();
            float minimumY = float.PositiveInfinity;
            try
            {
                foreach (SkinnedMeshRenderer renderer in previewRig.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (!renderer.enabled || renderer.sharedMesh == null) continue;
                    if (!renderer.name.StartsWith("FootLeft/", StringComparison.Ordinal)
                        && !renderer.name.StartsWith("FootRight/", StringComparison.Ordinal)) continue;
                    baked.Clear();
                    renderer.BakeMesh(baked);
                    foreach (Vector3 vertex in baked.vertices)
                    {
                        Vector3 relative = renderer.transform.TransformPoint(vertex) - previewRig.transform.position;
                        minimumY = Mathf.Min(minimumY, relative.y);
                    }
                }
                if (float.IsInfinity(minimumY)) return;
                // El calzado de cada módulo tiene una base diferente: apoyar su
                // geometría real sobre la plataforma evita enterrar los pies.
                Vector3 position = previewRig.transform.position;
                position.y = PlatformHeight - minimumY;
                previewRig.transform.position = position;
            }
            finally { Destroy(baked); }
        }

        private void Rotate(float degrees)
        {
            rotation += degrees;
            if (previewRig != null) previewRig.transform.localRotation = Quaternion.Euler(0, rotation, 0);
        }

        private void Confirm()
        {
            if (confirming || !IsOpen) return;
            draft.displayName = (nameInput != null ? nameInput.text : draft.displayName)?.Trim();
            if (string.IsNullOrWhiteSpace(draft.displayName))
            {
                errorText.text = "Ingresá un nombre para tu personaje.";
                nameInput?.Select();
                return;
            }
            if (!catalog.Validate(draft, out string validationError))
            {
                errorText.text = validationError;
                return;
            }
            confirming = true;
            confirmButton.interactable = false;
            try
            {
                string result = confirm != null ? confirm(draft.Clone()) : "No hay una acción de creación configurada.";
                if (string.IsNullOrEmpty(result)) Close();
                else errorText.text = result;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                errorText.text = "No se pudo guardar el personaje. Tus cambios siguen en este menú.";
            }
            finally
            {
                confirming = false;
                if (confirmButton != null) confirmButton.interactable = true;
            }
        }

        private void Update()
        {
            if (!IsOpen) return;
            if (Screen.width != previewScreenWidth || Screen.height != previewScreenHeight) FramePreview();
            if ((Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame))
            {
                bool popupOpen = false;
                foreach (TMP_Dropdown dropdown in GetComponentsInChildren<TMP_Dropdown>())
                    if (dropdown.IsExpanded) { popupOpen = true; dropdown.Hide(); }
                if (!popupOpen) Close();
                return;
            }
            if (!draggingPreview && Gamepad.current != null)
            {
                float turn = Gamepad.current.rightStick.x.ReadValue();
                if (Mathf.Abs(turn) > .2f) Rotate(-turn * Time.unscaledDeltaTime * 100f);
            }
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (activeInstance == this) activeInstance = null;
            gameObject.SetActive(false);
            RestoreControls();
            if (EventSystem.current != null && previouslySelected != null)
                EventSystem.current.SetSelectedGameObject(previouslySelected);
            CleanupPreview();
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            RestoreControls();
            CleanupPreview();
            if (activeInstance == this) activeInstance = null;
        }

        private void OnDisable()
        {
            if (IsOpen) Close();
        }

        private void RestoreControls()
        {
            foreach (var previous in previousControls)
                if (previous.Key != null) previous.Key.interactable = previous.Value;
            previousControls.Clear();
            foreach (var previous in previousCanvases)
                if (previous.Key != null) previous.Key.enabled = previous.Value;
            previousCanvases.Clear();
        }

        private void CleanupPreview()
        {
            foreach (var previous in previousCameraMasks)
                if (previous.Key != null) previous.Key.cullingMask = previous.Value;
            previousCameraMasks.Clear();
            if (previewRoot != null) { previewRoot.SetActive(false); Destroy(previewRoot); previewRoot = null; }
            previewCamera = null;
            previewRig = null;
            appearance = null;
            studioMaterialLookup.Clear();
            foreach (Material material in studioMaterials)
                if (material != null) Destroy(material);
            studioMaterials.Clear();
        }

        private TMP_Dropdown Dropdown(string title, Transform parent, List<string> options, int value, Action<int> changed)
        {
            RectTransform row = Rect(title, parent, Vector2.zero, Vector2.one);
            Height(row.gameObject, 82);
            TMP_Text heading = Label(title, row, 16, Muted, 22);
            Place(heading.rectTransform, new Vector2(0, .67f), Vector2.one);
            RectTransform box = Rect("Dropdown", row, Vector2.zero, new Vector2(1, .62f));
            box.gameObject.AddComponent<Image>().color = Control;
            var dropdown = box.gameObject.AddComponent<TMP_Dropdown>();
            dropdown.alphaFadeSpeed = 0;
            var caption = Label("", box, 17, Color.white, 48);
            Place(caption.rectTransform, Vector2.zero, Vector2.one, new Vector2(12, 3), new Vector2(-34, -3));
            caption.overflowMode = TextOverflowModes.Ellipsis;
            caption.textWrappingMode = TextWrappingModes.NoWrap;
            var arrow = Label("v", box, 17, Accent, 48);
            Place(arrow.rectTransform, new Vector2(1, 0), Vector2.one, new Vector2(-30, 0), Vector2.zero);
            arrow.alignment = TextAlignmentOptions.Center;

            RectTransform template = Rect("Template", box, Vector2.zero, new Vector2(1, 0));
            template.pivot = new Vector2(.5f, 1);
            template.sizeDelta = new Vector2(0, 240);
            template.anchoredPosition = new Vector2(0, -3);
            Color popupColor = new Color(Control.r, Control.g, Control.b, 1);
            template.gameObject.AddComponent<Image>().color = popupColor;
            var scroll = template.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32;
            RectTransform viewport = Rect("Viewport", template, Vector2.zero, Vector2.one,
                Vector2.zero, new Vector2(-16, 0));
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform list = Rect("Content", viewport, new Vector2(0, 1), Vector2.one);
            list.pivot = new Vector2(.5f, 1);
            list.sizeDelta = new Vector2(0, 40);
            RectTransform item = Rect("Item", list, new Vector2(0, 1), Vector2.one);
            item.pivot = new Vector2(.5f, .5f);
            item.sizeDelta = new Vector2(0, 40);
            item.anchoredPosition = new Vector2(0, -20);
            var itemImage = item.gameObject.AddComponent<Image>();
            itemImage.color = popupColor;
            var toggle = item.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = itemImage;
            var check = Rect("Selected", item, new Vector2(0, .25f), new Vector2(0, .75f));
            check.sizeDelta = new Vector2(4, 0);
            check.anchoredPosition = new Vector2(4, 0);
            toggle.graphic = check.gameObject.AddComponent<Image>();
            ((Image)toggle.graphic).color = Accent;
            var itemLabel = Label("Opción", item, 16, Color.white, 40);
            Place(itemLabel.rectTransform, Vector2.zero, Vector2.one, new Vector2(14, 1), new Vector2(-10, -1));
            itemLabel.overflowMode = TextOverflowModes.Ellipsis;
            itemLabel.textWrappingMode = TextWrappingModes.NoWrap;
            scroll.viewport = viewport;
            scroll.content = list;
            AddVerticalScrollbar(scroll, template);
            dropdown.captionText = caption;
            dropdown.itemText = itemLabel;
            dropdown.template = template;
            dropdown.targetGraphic = box.GetComponent<Image>();
            dropdown.AddOptions(options);
            dropdown.SetValueWithoutNotify(Mathf.Clamp(value, 0, Mathf.Max(0, options.Count - 1)));
            dropdown.RefreshShownValue();
            dropdown.onValueChanged.AddListener(index => changed(index));
            template.gameObject.SetActive(false);
            return dropdown;
        }

        private void Slider(string title, float value, float minimum, float maximum, Action<float> changed)
        {
            RectTransform row = Rect(title, content, Vector2.zero, Vector2.one);
            Height(row.gameObject, 88);
            var heading = Label(title, row, 17, Muted, 28);
            Place(heading.rectTransform, new Vector2(0, .65f), new Vector2(.82f, 1));
            var amount = Label(Mathf.RoundToInt(value).ToString(), row, 16, Accent, 28);
            Place(amount.rectTransform, new Vector2(.86f, .65f), Vector2.one);
            amount.alignment = TextAlignmentOptions.Right;
            RectTransform control = Rect("Slider", row, new Vector2(.02f, .12f), new Vector2(.98f, .48f));
            control.gameObject.AddComponent<Image>().color = Control;
            var slider = control.gameObject.AddComponent<UnityEngine.UI.Slider>();
            RectTransform fill = Rect("Fill", control, Vector2.zero, Vector2.one);
            fill.gameObject.AddComponent<Image>().color = new Color(Accent.r, Accent.g, Accent.b, .55f);
            RectTransform area = Rect("HandleArea", control, Vector2.zero, Vector2.one, new Vector2(6, 0), new Vector2(-6, 0));
            RectTransform handle = Rect("Handle", area, Vector2.zero, new Vector2(0, 1));
            handle.sizeDelta = new Vector2(16, 8);
            handle.gameObject.AddComponent<Image>().color = Accent;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = minimum;
            slider.maxValue = maximum;
            slider.wholeNumbers = true;
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(next =>
            {
                amount.text = Mathf.RoundToInt(next).ToString();
                changed(next);
            });
        }

        private static void AddVerticalScrollbar(ScrollRect scroll, RectTransform parent)
        {
            RectTransform track = Rect("Scrollbar", parent, new Vector2(1, 0), Vector2.one,
                new Vector2(-10, 0), Vector2.zero);
            track.gameObject.AddComponent<Image>().color = new Color(.02f, .03f, .04f, 1);
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            RectTransform area = Rect("SlidingArea", track, Vector2.zero, Vector2.one,
                new Vector2(2, 2), new Vector2(-2, -2));
            RectTransform handle = Rect("Handle", area, Vector2.zero, Vector2.one);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = Accent;
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        private static TMP_InputField Input(string placeholder, Transform parent, string value, int height)
        {
            RectTransform root = Rect("CharacterName", parent, Vector2.zero, Vector2.one);
            Height(root.gameObject, height);
            root.gameObject.AddComponent<Image>().color = Control;
            var input = root.gameObject.AddComponent<TMP_InputField>();
            RectTransform viewport = Rect("TextArea", root, Vector2.zero, Vector2.one, new Vector2(12, 3), new Vector2(-12, -3));
            viewport.gameObject.AddComponent<RectMask2D>();
            var text = Label("", viewport, 19, Color.white, height);
            Stretch(text.rectTransform);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            var hint = Label(placeholder, viewport, 19, Muted, height);
            Stretch(hint.rectTransform);
            input.textViewport = viewport;
            input.textComponent = text;
            input.placeholder = hint;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.contentType = TMP_InputField.ContentType.Standard;
            input.text = value;
            input.targetGraphic = root.GetComponent<Image>();
            return input;
        }

        private static Button Button(string title, Transform parent, Action action, int height, bool primary = false)
        {
            RectTransform root = Rect(title, parent, Vector2.zero, Vector2.one);
            Height(root.gameObject, height);
            var image = root.gameObject.AddComponent<Image>();
            image.color = primary ? Accent : Control;
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.16f, 1.16f, 1.16f);
            colors.pressedColor = new Color(.7f, .7f, .7f);
            colors.selectedColor = new Color(1.12f, 1.12f, 1.12f);
            button.colors = colors;
            var text = Label(title, root, 17, primary ? Ink : Color.white, height);
            Stretch(text.rectTransform);
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = primary ? FontStyles.Bold : FontStyles.Normal;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            button.onClick.AddListener(() => action());
            return button;
        }

        private static TMP_Text Label(string text, Transform parent, int size, Color color, int height)
        {
            RectTransform root = Rect("Label", parent, Vector2.zero, Vector2.one);
            Height(root.gameObject, height);
            var label = root.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;
            label.font = TMP_Settings.defaultFontAsset;
            return label;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 minimum, Vector2 maximum,
            Vector2 offsetMinimum = default, Vector2 offsetMaximum = default)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            Place(rect, minimum, maximum, offsetMinimum, offsetMaximum);
            return rect;
        }

        private static void Place(RectTransform rect, Vector2 minimum, Vector2 maximum,
            Vector2 offsetMinimum = default, Vector2 offsetMaximum = default)
        {
            rect.anchorMin = minimum;
            rect.anchorMax = maximum;
            rect.offsetMin = offsetMinimum;
            rect.offsetMax = offsetMaximum;
        }

        private static void Stretch(RectTransform rect) => Place(rect, Vector2.zero, Vector2.one);
        private static void Height(GameObject target, int height) => target.AddComponent<LayoutElement>().preferredHeight = height;

        private static void AddPointerEvent(EventTrigger trigger, EventTriggerType type, Action<BaseEventData> callback)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(data => callback(data));
            trigger.triggers.Add(entry);
        }

        private static int SlotOrder(string slot)
        {
            switch (slot)
            {
                case "Head": return 0;
                case "Hair": return 1;
                case "FacialHair": return 2;
                case "Torso": return 0;
                case "Hips": return 1;
                case "LegLeft": return 2;
                case "LegRight": return 3;
                case "FootLeft": return 4;
                case "FootRight": return 5;
                default: return 20;
            }
        }

        private static string SlotLabel(string slot)
        {
            switch (slot)
            {
                case "Head": return "Cara";
                case "Hair": return "Pelo";
                case "FacialHair": return "Barba y bigote";
                case "EyebrowLeft": return "Ceja izquierda";
                case "EyebrowRight": return "Ceja derecha";
                case "EyeLeft": return "Ojo izquierdo";
                case "EyeRight": return "Ojo derecho";
                case "EarLeft": return "Oreja izquierda";
                case "EarRight": return "Oreja derecha";
                case "Nose": return "Nariz";
                case "Teeth": return "Dientes";
                case "Tongue": return "Lengua";
                case "Torso": return "Torso";
                case "ArmUpperLeft": return "Brazo izquierdo";
                case "ArmUpperRight": return "Brazo derecho";
                case "ArmLowerLeft": return "Antebrazo izquierdo";
                case "ArmLowerRight": return "Antebrazo derecho";
                case "HandLeft": return "Mano izquierda";
                case "HandRight": return "Mano derecha";
                case "Hips": return "Cintura";
                case "LegLeft": return "Pierna izquierda";
                case "LegRight": return "Pierna derecha";
                case "FootLeft": return "Calzado izquierdo";
                case "FootRight": return "Calzado derecho";
                case "AttachmentHead": return "Accesorio de cabeza";
                case "AttachmentFace": return "Accesorio de cara";
                case "AttachmentBack": return "Accesorio de espalda";
                case "AttachmentHipsFront": return "Accesorio frontal de cintura";
                case "AttachmentHipsBack": return "Accesorio trasero de cintura";
                case "AttachmentHipsLeft": return "Accesorio izquierdo de cintura";
                case "AttachmentHipsRight": return "Accesorio derecho de cintura";
                case "AttachmentShoulderLeft": return "Hombrera izquierda";
                case "AttachmentShoulderRight": return "Hombrera derecha";
                case "AttachmentElbowLeft": return "Codera izquierda";
                case "AttachmentElbowRight": return "Codera derecha";
                case "AttachmentKneeLeft": return "Rodillera izquierda";
                case "AttachmentKneeRight": return "Rodillera derecha";
                case "Wrap": return "Vendajes";
                default: return slot;
            }
        }
    }
}
