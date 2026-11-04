using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Survive.Combat;
using Survive.Economy;
using Survive.Items;
using Survive.Turrets;
using Survive.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UPP.ThirdPersonController;
using Object = UnityEngine.Object;

namespace Survive.Editor
{
    [InitializeOnLoad]
    public static class SyntyVendorPresentationValidation
    {
        const string Key = "Survive.SyntyVendorPresentationValidation";
        public const string Folder = "Logs/SyntyVendorUI";
        const string TestScene = "SyntyVendorValidation";
        static readonly List<string> checks = new List<string>(), errors = new List<string>();
        static readonly BindingFlags Fields = BindingFlags.NonPublic | BindingFlags.Instance;
        static int stage, failures, frame;
        static double started, next;
        static PlayerVendorController shop;
        static PlayerWallet wallet;
        static PlayerWeaponInventory inventory;
        static PlayerTurretController turrets;
        static NpcVendor vendor;
        static SyntyGameplayHUD hud;
        static GameObject gallery;
        static readonly Dictionary<Canvas, bool> hidden = new Dictionary<Canvas, bool>();
        [Serializable] sealed class Result { public int passed, failed; public string[] checks, runtimeErrors; }

        static SyntyVendorPresentationValidation()
        {
            EditorApplication.update += Tick;
            SceneManager.sceneLoaded += (scene, mode) =>
            {
                if (SessionState.GetBool(Key, false) && scene.name != TestScene)
                    foreach (GameObject root in scene.GetRootGameObjects()) root.SetActive(false);
            };
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Key, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                { checks.Clear(); errors.Clear(); failures = stage = 0; started = EditorApplication.timeSinceStartup; next = started + 3; Application.runInBackground = true; Application.logMessageReceived += OnLog; }
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    SessionState.SetBool(Key, false);
                    for (int i = SceneManager.sceneCount - 1; i >= 0; i--) { Scene scene = SceneManager.GetSceneAt(i); if (scene.name == TestScene) EditorSceneManager.CloseScene(scene, true); }
                    Scene original = SceneManager.GetSceneByPath(SessionState.GetString(Key + ".Scene", ""));
                    if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                    if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(Key + ".Exit", 1));
                }
            };
        }

        [MenuItem("Survive/UI/Validar vendedor Synty y mostrar todos los iconos")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salí de Play Mode.");
            Directory.CreateDirectory(Folder); SyntyWeaponIconSetup.Run();
            SessionState.SetString(Key + ".Scene", SceneManager.GetActiveScene().path);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive); scene.name = TestScene;
            SceneManager.SetActiveScene(scene); SessionState.SetBool(Key, true);
            Prepare(); EditorApplication.EnterPlaymode();
        }
        static void Prepare() => typeof(CharacterCustomizationValidation).GetMethod("PrepareBatchGameView", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            Prepare(); EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup - started > 160) { Check(false, "Timeout"); Finish(); return; }
            if (EditorApplication.timeSinceStartup < next || Time.frameCount <= frame + 3) return;
            frame = Time.frameCount; next = EditorApplication.timeSinceStartup + 1.5;
            try
            {
                switch (stage++)
                {
                    case 0: CreateScenario(); break;
                    case 1:
                        Require(hud.IsBound, "HUD conectado al jugador real");
                        Image pistol = Read<Image>(hud, "weaponIcon");
                        Check(pistol.sprite == Definition(0).Icon, "HUD usa el icono SM Pistol a color compartido");
                        Check(Read<SyntyGameplayHUD.Slot[]>(hud, "slots")[0].icon.sprite == Definition(0).Icon, "Inventario usa el mismo icono de pistola");
                        Capture("01-pistol-hud.png");
                        Require(shop.TryOpen(vendor), "Abre vendedor con los prefabs y catálogo existentes");
                        break;
                    case 2:
                        Check(vendor.Catalog.Count == 11 && Read<Button[]>(shop, "buyButtons").Length == 11, "Armas, torreta, cargador explosivo y caja común disponibles");
                        Transform products = shop.transform.Find("VendorAndTurretUI/Shop/Window/ProductScroll/Products");
                        for (int i = 0; i < vendor.Catalog.Count; i++)
                        {
                            Transform card = products.Find("Product_" + i); Image icon = card.Find("Icon").GetComponent<Image>();
                            Check(icon.sprite == vendor.Catalog.GetOffer(i).Icon, "Tarjeta usa icono compartido: " + vendor.Catalog.GetOffer(i).DisplayName);
                            Check(card.Find("SyntyFrame").GetComponent<Image>().sprite != null, "Marco Synty asignado en tarjeta " + (i + 1));
                        }
                        Canvas.ForceUpdateCanvases();
                        Check(products.GetComponentsInChildren<TMP_Text>().All(t => !t.isTextOverflowing), "Textos de productos completos sin recortes");
                        Check(shop.transform.Find("VendorAndTurretUI/Shop").GetComponentsInChildren<TMP_Text>().All(t => t.font == VendorUITheme.Load().font), "Tipografía Synty en la tienda");
                        Capture("02-vendor-synty.png");
                        int coins = wallet.Coins; int occupied = Occupied();
                        Click(Read<Button[]>(shop, "buyButtons")[0], "Comprar pistola");
                        Check(wallet.Coins == coins - vendor.Catalog.GetOffer(0).price && Occupied() == occupied + 1, "Clic real compra arma y cobra una vez");
                        Click(Read<Button[]>(shop, "buyButtons")[8], "Comprar torreta");
                        Check(turrets.Count == 1, "Botón de torreta entrega un kit real");
                        break;
                    case 3:
                        MakeGallery();
                        break;
                    case 4:
                        Capture("03-all-weapon-icons.png");
                        Object.DestroyImmediate(gallery);
                        foreach (var state in hidden) if (state.Key != null) state.Key.enabled = state.Value;
                        hidden.Clear();
                        wallet.TrySpend(wallet.Coins); int before = Occupied();
                        Check(!shop.TryBuy(0) && wallet.Coins == 0 && Occupied() == before, "Sin fondos no entrega ni cobra");
                        wallet.AddCoins(5000);
                        for (int i = 0; i < 8 && inventory.HasFreeSlot; i++) Require(shop.TryBuy(0), "Compra ocupa un espacio libre");
                        int balance = wallet.Coins;
                        Check(!inventory.HasFreeSlot && !shop.TryBuy(0) && wallet.Coins == balance, "Inventario lleno impide cobro y reemplazo");
                        while (turrets.Count < turrets.Capacity) turrets.TryAdd(turrets.Definition);
                        Check(!shop.TryBuy(8) && wallet.Coins == balance, "Reserva de torretas llena conserva monedas");
                        break;
                    case 5:
                        Check(Read<TMP_Text>(shop, "inventoryStatus").text.Contains("6 / 6"), "Contador de inventario actualizado");
                        Check(!Read<Button[]>(shop, "buyButtons")[0].interactable && !Read<Button[]>(shop, "buyButtons")[8].interactable, "Botones reflejan límites de inventario y torretas");
                        Canvas.ForceUpdateCanvases();
                        Check(Read<TMP_Text[]>(shop, "buyLabels").All(t => !t.isTextOverflowing), "Motivos de compra deshabilitada son legibles");
                        Capture("04-vendor-full.png");
                        Click(shop.transform.Find("VendorAndTurretUI/Shop/Window/CERRAR  [ESC]").GetComponent<Button>(), "Cerrar tienda");
                        Check(!shop.IsOpen && !shop.GetComponent<UPPCharacterMovementComponent>().DisableAllMove, "Cerrar restaura control del jugador");
                        Finish();
                        break;
                }
            }
            catch (Exception exception) { Check(false, exception.ToString()); Finish(); }
        }

        static void CreateScenario()
        {
            var sun = new GameObject("ValidationLight").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.4f; sun.transform.rotation = Quaternion.Euler(45, -35, 0);
            RenderSettings.ambientLight = Color.gray;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.position = new Vector3(0, -.5f, 0); floor.transform.localScale = new Vector3(40, 1, 40);
            GameObject player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/UPPPlayer.prefab"), new Vector3(0, .05f, 0), Quaternion.identity);
            player.GetComponent<UPPCharacterMovementComponent>().UseDefaultControllerInput = false;
            shop = player.GetComponent<PlayerVendorController>(); wallet = player.GetComponent<PlayerWallet>(); inventory = player.GetComponent<PlayerWeaponInventory>(); turrets = player.GetComponent<PlayerTurretController>();
            VendorTurretSetup.Set(shop, "requireLockedCursor", false); VendorTurretSetup.Set(player.GetComponent<PlayerWeaponController>(), "requireLockedCursor", false);
            wallet.AddCoins(5000);
            vendor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Vendor/NpcVendor.prefab"), new Vector3(0, 0, 2), Quaternion.Euler(0, 180, 0)).GetComponent<NpcVendor>();
            hud = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SyntyGameplayHudSetup.PrefabPath)).GetComponent<SyntyGameplayHUD>();
            for (int i = 0; i < SyntyWeaponIconSetup.WeaponIds.Length; i++)
                Check(Definition(i).Icon != null && Definition(i).Icon.name == SyntyWeaponIconSetup.SpriteNames[i] && !Definition(i).Icon.name.EndsWith("_Clean"), "Icono a color asignado: " + Definition(i).DisplayName);
        }

        static void MakeGallery()
        {
            hidden.Clear(); foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) { hidden[canvas] = canvas.enabled; canvas.enabled = false; }
            gallery = new GameObject("SyntyWeaponIconGallery", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            gallery.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; gallery.GetComponent<Canvas>().sortingOrder = 1000;
            CanvasScaler scaler = gallery.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0;
            RectTransform full = (RectTransform)gallery.transform; full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one; full.offsetMin = full.offsetMax = Vector2.zero;
            Image bg = gallery.AddComponent<Image>(); bg.color = new Color(.018f, .03f, .042f);
            Text(gallery.transform, "Title", "ARSENAL / ICONOS SYNTY A COLOR", new Vector2(0, 430), new Vector2(1700, 70), 40);
            for (int i = 0; i < SyntyWeaponIconSetup.WeaponIds.Length; i++)
            {
                RectTransform card = Rect(gallery.transform, "Icon_" + i, new Vector2((i % 4 - 1.5f) * 444, 190 - i / 4 * 368), new Vector2(424, 340));
                Image plate = card.gameObject.AddComponent<Image>(); plate.sprite = VendorUITheme.Load().panel; plate.type = Image.Type.Sliced; plate.color = VendorUITheme.Load().cardColor;
                Image image = Rect(card, "Image", new Vector2(0, 15), new Vector2(355, 225)).gameObject.AddComponent<Image>(); image.sprite = Definition(i).Icon; image.preserveAspect = true;
                Text(card, "Name", Definition(i).DisplayName.ToUpperInvariant(), new Vector2(0, 141), new Vector2(400, 40), 25);
                Text(card, "Source", SyntyWeaponIconSetup.SpriteNames[i].Replace("ICON_SM_Wep_", ""), new Vector2(0, -132), new Vector2(390, 42), 18);
            }
        }
        static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size) { var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.anchoredPosition = position; rect.sizeDelta = size; return rect; }
        static void Text(Transform parent, string name, string value, Vector2 position, Vector2 size, float fontSize) { TMP_Text label = Rect(parent, name, position, size).gameObject.AddComponent<TextMeshProUGUI>(); label.font = VendorUITheme.Load().font; label.text = value; label.fontSize = fontSize; label.alignment = TextAlignmentOptions.Center; label.color = VendorUITheme.Load().accent; }
        static WeaponDefinition Definition(int index) => AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Data/Weapons/WD_" + SyntyWeaponIconSetup.WeaponIds[index] + ".asset");
        static int Occupied() => Enumerable.Range(0, inventory.SlotCount).Count(i => inventory.GetWeapon(i) != null);
        static T Read<T>(object target, string name) => (T)target.GetType().GetField(name, Fields).GetValue(target);
        static void Click(Button button, string label) => typeof(CharacterCustomizationValidation).GetMethod("ClickThroughEventSystem", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { button, label });
        static void Capture(string file)
        {
            var rt = (RenderTexture)typeof(CharacterCustomizationValidation).GetMethod("GameViewFramebuffer", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            if (rt == null) throw new InvalidOperationException("Framebuffer ausente");
            var photo = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = rt; photo.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                if (SystemInfo.graphicsUVStartsAtTop)
                {
                    Color32[] pixels = photo.GetPixels32(); for (int y = 0; y < rt.height / 2; y++) for (int x = 0; x < rt.width; x++)
                    { int a = y * rt.width + x, b = (rt.height - 1 - y) * rt.width + x; Color32 p = pixels[a]; pixels[a] = pixels[b]; pixels[b] = p; }
                    photo.SetPixels32(pixels);
                }
                photo.Apply(); File.WriteAllBytes(Folder + "/" + file, photo.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(photo); }
        }
        static void Check(bool ok, string text) { checks.Add((ok ? "PASS " : "FAIL ") + text); if (!ok) failures++; Debug.Log("SYNTY_VENDOR_" + (ok ? "PASS " : "FAIL ") + text); }
        static void Require(bool ok, string text) { Check(ok, text); if (!ok) throw new InvalidOperationException(text); }
        static void OnLog(string text, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(text + "\n" + stack); }
        static void Finish() { Application.logMessageReceived -= OnLog; File.WriteAllText(Folder + "/result.json", JsonUtility.ToJson(new Result { passed = checks.Count - failures, failed = failures, checks = checks.ToArray(), runtimeErrors = errors.ToArray() }, true)); SessionState.SetInt(Key + ".Exit", failures == 0 && errors.Count == 0 ? 0 : 1); EditorApplication.ExitPlaymode(); }
    }
}
