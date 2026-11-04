using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Survive.Combat;
using Survive.Economy;
using Survive.Items;
using Survive.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UPP.ThirdPersonController;
using UPP.ThirdPersonController.Internal;
using Object = UnityEngine.Object;

namespace Survive.Editor
{
    [InitializeOnLoad]
    public static class HealingExplosiveValidation
    {
        const string Key = "Survive.HealingExplosiveValidation", TestScene = "HealingExplosiveValidation";
        static readonly List<string> checks = new List<string>(), errors = new List<string>();
        static int stage, failures, frame, deathEvents, ammoIndex, beforeCoins, normalReserve;
        static double started, next;
        static UPPCharacterMovementComponent player;
        static PlayerWeaponInventory inventory;
        static PlayerWeaponController weapons;
        static PlayerInteractionController interaction;
        static PlayerVendorController shop;
        static PlayerWallet wallet;
        static PlayerHealth health;
        static NpcVendor vendor;
        static HealingPickup pickup;
        static HealingDefinition healing;
        static Quaternion initialRotation;
        static SyntyGameplayHUD hud;
        static EnemyHealth target, nearby, shielded;
        static Camera camera;
        static Mouse mouse;
        static Keyboard keyboard;
        static Gamepad gamepad;
        static InputSettings previousSettings, settings;
        [Serializable] sealed class Result { public string scope = "Prefabs reales en escena aislada: input hold, interacción E, hotbar/curación, compra por UI, disparo hitscan y ragdoll físico. Sin build ni partida completa."; public int passed, failed; public string[] checks, errors; }

        static HealingExplosiveValidation()
        {
            EditorApplication.update += Tick;
            SceneManager.sceneLoaded += (scene, mode) => { if (SessionState.GetBool(Key, false) && scene.name != TestScene) foreach (GameObject root in scene.GetRootGameObjects()) root.SetActive(false); };
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Key, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    stage = failures = frame = deathEvents = 0; checks.Clear(); errors.Clear(); started = EditorApplication.timeSinceStartup; next = started + 3;
                    previousSettings = InputSystem.settings; settings = Object.Instantiate(previousSettings);
                    settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    InputSystem.settings = settings; Application.runInBackground = true; Application.logMessageReceived += OnLog;
                }
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    SessionState.SetBool(Key, false);
                    if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(Key + ".Exit", 1));
                    else
                    {
                        Scene original = SceneManager.GetSceneByPath(SessionState.GetString(Key + ".Scene", ""));
                        if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                        for (int i = SceneManager.sceneCount - 1; i >= 0; i--) { Scene s = SceneManager.GetSceneAt(i); if (s.name == TestScene) EditorSceneManager.CloseScene(s, true); }
                    }
                }
            };
        }
        [MenuItem("Survive/Gameplay/Validar hold, curación y balas explosivas")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salí de Play Mode.");
            Directory.CreateDirectory(HealingExplosiveSetup.Folder);
            SessionState.SetString(Key + ".Scene", SceneManager.GetActiveScene().path);
            // Una escena Untitled no admite carga aditiva. En ese caso se prueba en su copia de Play Mode:
            // sceneLoaded desactiva los objetos originales y Unity los restaura al salir, sin guardar la escena.
            if (Application.isBatchMode || !string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
            {
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive); scene.name = TestScene;
                SceneManager.SetActiveScene(scene);
            }
            SessionState.SetBool(Key, true); Prepare(); EditorApplication.EnterPlaymode();
        }
        static void Prepare() => typeof(CharacterCustomizationValidation).GetMethod("PrepareBatchGameView", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            Prepare(); EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup - started > 180) { Check(false, "Timeout"); Finish(); return; }
            if (EditorApplication.timeSinceStartup < next || Time.frameCount <= frame + 3) return;
            frame = Time.frameCount; next = EditorApplication.timeSinceStartup + 1;
            try
            {
                switch (stage++)
                {
                    case 0: CreateScenario(); break;
                    case 1:
                        camera = player.MyPivotCamera.mCamera; player.MyPivotCamera.enabled = false;
                        player.UseDefaultControllerInput = true; player.Inputs.SetActiveInputs(true);
                        Check(player.AimMode == UPPCharacterMovementCore.PressAimMode.HoldToAim, "Prefab usa HoldToAim");
                        MouseInput(false, true); player.ControllerInputs(); Check(player.IsAiming, "Mantener botón derecho activa apuntado real UPP");
                        MouseInput(false, true); player.ControllerInputs(); Check(player.IsAiming, "Apuntado continúa mientras se mantiene");
                        MouseInput(); player.ControllerInputs(); Check(!player.IsAiming, "Soltar botón derecho termina apuntado sin segundo clic");
                        InputSystem.QueueStateEvent(gamepad, new GamepadState { leftTrigger = 1 }); InputSystem.Update(); player.ControllerInputs(); Check(player.IsAiming, "Gatillo izquierdo también funciona como hold");
                        InputSystem.QueueStateEvent(gamepad, new GamepadState()); InputSystem.Update(); player.ControllerInputs(); Check(!player.IsAiming, "Soltar gatillo termina apuntado");
                        player.UseDefaultControllerInput = false;
                        pickup = Object.Instantiate(healing.worldPrefab, new Vector3(0, 0, 1.5f), Quaternion.identity); pickup.PlaceInWorld(new Vector3(0, 0, 1.5f), Quaternion.identity);
                        initialRotation = pickup.transform.rotation;
                        break;
                    case 2:
                        Check(Quaternion.Angle(initialRotation, pickup.transform.rotation) > 5, "Objeto de curación rota sobre su eje en física");
                        Check(pickup.GetComponentsInChildren<Renderer>().All(r => r.sharedMaterials.All(m => m != null && m.shader.isSupported)), "Todos los renderers del consumible tienen material visible");
                        Check(inventory.GetHealing(1) == null && health.CurrentHealth == health.MaxHealth, "No recoge ni cura automáticamente");
                        AimAt(pickup.transform.position); interaction.RefreshTarget(); Require(interaction.CurrentInteractable == pickup, "Raycast real detecta vendas y muestra interacción");
                        stage = 20;
                        break;
                    case 20:
                        Capture("01-healing-pickup.png"); health.TakeDamage(60); Keys(UnityEngine.InputSystem.Key.E); stage = 3; break;
                    case 3:
                        Keys(); Require(inventory.GetHealing(1) == healing && inventory.GetHealingCount(1) == 1, "Tecla E guarda vendas en el primer espacio libre");
                        Check(Mathf.Approximately(health.CurrentHealth, 40), "Guardar curación no la consume");
                        Check(inventory.TryAddHealing(healing, 4) && inventory.GetHealingCount(1) == 5, "Vendas se apilan hasta cinco en un solo espacio");
                        Keys(UnityEngine.InputSystem.Key.Digit2); break;
                    case 4:
                        Keys(); Require(inventory.SelectedIndex == 1 && weapons.EquippedWeapon == null, "Tecla 2 selecciona consumible y guarda el arma");
                        var slots = Read<SyntyGameplayHUD.Slot[]>(hud, "slots"); Check(slots[1].icon.sprite == healing.icon && slots[1].ammo.text == "x5", "Hotbar muestra icono Synty de vendas y cantidad");
                        Capture("02-healing-hotbar.png"); InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left)); break;
                    case 5:
                        MouseInput(); Check(Mathf.Approximately(health.CurrentHealth, 75) && inventory.GetHealingCount(1) == 4, "Un clic cura 35 y consume una venda; mantener no repite");
                        Check(inventory.TryUseSelectedHealing() && health.CurrentHealth == health.MaxHealth && inventory.GetHealingCount(1) == 3, "Curación respeta vida máxima");
                        Check(!inventory.TryUseSelectedHealing() && inventory.GetHealingCount(1) == 3, "Vida completa no desperdicia vendas");
                        Require(inventory.TryDropSelected(new Vector3(0, 0, 1.5f), Quaternion.identity), "Puede soltar la pila de curación");
                        HealingPickup dropped = Object.FindFirstObjectByType<HealingPickup>();
                        Check(dropped != null && dropped.Quantity == 3 && dropped.Interact(player.gameObject) && inventory.GetHealingCount(1) == 3, "Soltar y recoger conserva la cantidad de vendas");
                        health.TakeDamage(10); Time.timeScale = 0;
                        Check(!inventory.TryUseSelectedHealing() && health.CurrentHealth == 90 && inventory.GetHealingCount(1) == 3, "Pausa bloquea uso aun con vida faltante"); Time.timeScale = 1;
                        Require(shop.TryOpen(vendor), "Abre catálogo real del mercader");
                        ammoIndex = Enumerable.Range(0, vendor.Catalog.Count).First(i => vendor.Catalog.GetOffer(i).IsExplosiveMagazine);
                        beforeCoins = wallet.Coins;
                        Check(!shop.TryBuy(ammoIndex) && wallet.Coins == beforeCoins, "Sin arma equipada no compra ni cobra cargador");
                        Check(!inventory.TryUseSelectedHealing(), "Mercader bloquea consumibles y selección de juego");
                        shop.Close(); break;
                    case 6:
                        Require(inventory.TrySelect(0), "Vuelve a la pistola tras la protección del clic de cierre");
                        Require(shop.TryOpen(vendor), "Abre mercader con arma equipada");
                        shop.transform.Find("VendorAndTurretUI/Shop/Window/ProductScroll").GetComponent<ScrollRect>().verticalNormalizedPosition = 0;
                        normalReserve = weapons.EquippedWeapon.ReserveAmmo + weapons.EquippedWeapon.AmmoInMagazine;
                        beforeCoins = wallet.Coins; break;
                    case 7:
                        Capture("03-vendor-explosive-magazine.png");
                        Click(Read<Button[]>(shop, "buyButtons")[ammoIndex], "Comprar cargador explosivo");
                        WeaponItem gun = weapons.EquippedWeapon;
                        Require(gun.LoadedExplosiveAmmo != null && gun.AmmoInMagazine == gun.MagazineSize, "Clic real carga un cargador explosivo completo");
                        Check(wallet.Coins == beforeCoins - vendor.Catalog.GetOffer(ammoIndex).price, "Cobra el precio del catálogo exactamente una vez");
                        Check(gun.ReserveAmmo == normalReserve, "Las balas normales restantes vuelven a la reserva");
                        beforeCoins = wallet.Coins; Check(!shop.TryBuy(ammoIndex) && wallet.Coins == beforeCoins, "Cargador ya activo impide compra duplicada");
                        shop.Close();
                        vendor.transform.position = new Vector3(-6, 0, 0);
                        target = Zombie(new Vector3(0, 0, 6)); nearby = Zombie(new Vector3(1.4f, 0, 6)); shielded = Zombie(new Vector3(-2.1f, 0, 6));
                        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "BlastOcclusionWall"; wall.transform.position = new Vector3(-1.05f, 1.4f, 6); wall.transform.localScale = new Vector3(.2f, 2.8f, 4);
                        break;
                    case 8:
                        Require(inventory.TrySelect(1) && inventory.TrySelect(0) && weapons.EquippedWeapon.LoadedExplosiveAmmo != null, "Cambiar hotbar conserva el cargador especial");
                        stage = 21; break;
                    case 21:
                        AimAt(target.GetComponent<Collider>().bounds.center); player.IsAiming = true; Physics.SyncTransforms();
                        int rounds = weapons.EquippedWeapon.AmmoInMagazine;
                        weapons.EquippedWeapon.ImpactPresented += (item, hit) => Debug.Log("HEALING_SHOT_IMPACT " + hit.collider.name + " point=" + hit.point);
                        Debug.Log("HEALING_SHOT_GEOMETRY muzzle=" + weapons.EquippedWeapon.Muzzle.position + " target=" + target.GetComponent<Collider>().bounds.center + " hp=" + target.CurrentHealth);
                        Require(weapons.TryFire(), "Disparo real respeta apuntado, cañón y raycast");
                        Check(weapons.EquippedWeapon.AmmoInMagazine == rounds - 1, "Explosión consume una bala");
                        Require(target.IsDead, "Impacto explosivo mata al zombi objetivo");
                        Check(nearby.IsDead, "Explosión daña al zombi cercano una sola vez");
                        Check(!shielded.IsDead && shielded.CurrentHealth == shielded.MaxHealth, "Pared bloquea daño de área");
                        Check(target.GetComponent<EnemyRagdoll>() != null && target.GetComponent<EnemyRagdoll>().IsActive && target.GetComponent<EnemyRagdoll>().BodyCount >= 8, "Zombi muerto activa cuerpos articulados reales");
                        Check(!target.GetComponentInChildren<Animator>().enabled && !target.GetComponent<NavMeshAgent>().enabled, "Ragdoll libera el Animator y desactiva navegación");
                        ExplosiveAmmoDefinition ammunition = AssetDatabase.LoadAssetAtPath<ExplosiveAmmoDefinition>(HealingExplosiveSetup.AmmoPath);
                        Check(ammunition.impactEffectPrefab != null && Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)
                            .Any(p => p.isPlaying && p.GetComponentInParent<WeaponVfxPool>() != null
                                && p.transform.root.GetComponentsInChildren<Transform>().Any(t => t.name.StartsWith(ammunition.impactEffectPrefab.name))),
                            "Impacto reproduce la mini explosión Gunslinger configurada");
                        camera.transform.SetPositionAndRotation(new Vector3(4, 3, -1), Quaternion.LookRotation(new Vector3(0, 1, 6) - new Vector3(4, 3, -1)));
                        next = EditorApplication.timeSinceStartup + .35; stage = 9; break;
                    case 9:
                        Capture("04-explosive-ragdoll.png");
                        Check(target.GetComponentsInChildren<Rigidbody>().Any(body => !body.isKinematic), "Cuerpo del zombi sigue bajo simulación física");
                        int deaths = deathEvents; target.TakeExplosiveDamage(1000, Vector3.zero, 6); Check(deathEvents == deaths && deaths == 2, "Cada zombi publica una sola muerte");
                        gun = weapons.EquippedWeapon;
                        while (gun.HasAmmo) Require(gun.TryConsumeRound(), "Munición especial se agota sin recargas gratis");
                        Check(gun.LoadedExplosiveAmmo == null && gun.TryStartReload() && gun.TickReload(gun.ReloadDuration), "Al terminar el cargador se recarga munición normal");
                        Check(gun.LoadedExplosiveAmmo == null && gun.AmmoInMagazine == gun.MagazineSize && gun.ReserveAmmo == normalReserve - gun.MagazineSize, "Recarga normal conserva contabilidad de balas");
                        FillAndCheck();
                        break;
                    case 10:
                        Check(Read<TMPro.TMP_Text>(shop, "inventoryStatus").text.Contains("6 / 6"), "Mercader cuenta consumibles junto a armas");
                        Check(!shop.TryBuy(0), "Inventario lleno impide comprar arma"); shop.Close();
                        break;
                    case 11:
                        Require(inventory.TrySelect(1), "Selecciona vendas antes de probar muerte del jugador"); health.TakeDamage(1000);
                        Check(!inventory.TryUseSelectedHealing() && inventory.GetHealingCount(1) == 5, "Muerte bloquea uso y conserva consumibles");
                        health.Heal(100); Check(health.CurrentHealth == 0 && health.IsDead, "Curación no resucita al jugador muerto");
                        Finish(); break;
                }
            }
            catch (Exception exception) { Check(false, exception.ToString()); Finish(); }
        }
        static void CreateScenario()
        {
            var light = new GameObject("Sun").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.6f; light.transform.rotation = Quaternion.Euler(45, -40, 0); RenderSettings.ambientLight = Color.gray;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.position = new Vector3(0, -.5f, 0); floor.transform.localScale = new Vector3(40, 1, 40);
            GameObject root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(VendorTurretSetup.PlayerPath), new Vector3(0, .05f, 0), Quaternion.identity);
            player = root.GetComponent<UPPCharacterMovementComponent>(); player.UseDefaultControllerInput = false; player.BlockFireModeOnCursorVisible = false;
            inventory = root.GetComponent<PlayerWeaponInventory>(); weapons = root.GetComponent<PlayerWeaponController>(); interaction = root.GetComponent<PlayerInteractionController>();
            health = root.GetComponent<PlayerHealth>(); shop = root.GetComponent<PlayerVendorController>(); wallet = root.GetComponent<PlayerWallet>(); wallet.AddCoins(5000);
            VendorTurretSetup.Set(weapons, "requireLockedCursor", false); VendorTurretSetup.Set(interaction, "requireLockedCursor", false); VendorTurretSetup.Set(shop, "requireLockedCursor", false);
            vendor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(VendorTurretSetup.VendorPath), new Vector3(1.4f, 0, 1), Quaternion.identity).GetComponent<NpcVendor>();
            hud = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SyntyGameplayHudSetup.PrefabPath)).GetComponent<SyntyGameplayHUD>();
            healing = AssetDatabase.LoadAssetAtPath<HealingDefinition>(HealingExplosiveSetup.HealingPath);
            mouse = InputSystem.AddDevice<Mouse>("HealingValidationMouse"); keyboard = InputSystem.AddDevice<Keyboard>("HealingValidationKeyboard"); gamepad = InputSystem.AddDevice<Gamepad>("HealingValidationGamepad");
        }
        static EnemyHealth Zombie(Vector3 position)
        {
            var holder = new GameObject("InactiveSpawn"); holder.SetActive(false);
            GameObject enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Players/EnemyChasing.prefab"), holder.transform);
            enemy.GetComponent<NavMeshAgent>().enabled = false; enemy.GetComponent<EnemyBaseController>().enabled = false;
            CapsuleCollider capsule = enemy.GetComponent<CapsuleCollider>();
            enemy.transform.position = position + Vector3.up * (capsule.height * .5f - capsule.center.y);
            enemy.transform.SetParent(null, true); Object.Destroy(holder);
            EnemyHealth hp = enemy.GetComponent<EnemyHealth>(); hp.OnDeath += () => deathEvents++; return hp;
        }
        static void FillAndCheck()
        {
            Require(inventory.TryAddHealing(healing, 2) && inventory.GetHealingCount(1) == 5, "Completa pila de vendas");
            for (int i = 0; i < 4; i++)
            {
                WeaponItem weapon = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Items/Weapons/Weapon_Pistol.prefab")).GetComponent<WeaponItem>();
                Require(inventory.TryStorePurchase(weapon), "Compra respeta espacios compartidos con consumibles");
            }
            Check(!inventory.HasFreeSlot && !inventory.CanAddHealing(healing, 1), "Seis espacios ocupados impiden agregar otra pila");
            HealingPickup extra = Object.Instantiate(healing.worldPrefab, new Vector3(2, 0, 1), Quaternion.identity);
            Check(!extra.Interact(player.gameObject) && extra.gameObject.activeSelf, "Inventario lleno deja el objeto en el mundo");
            EnemyHealth regular = Zombie(new Vector3(12, 0, 5)); regular.TakeDamage(regular.MaxHealth);
            Check(regular.IsDead && regular.GetComponent<EnemyRagdoll>() == null, "Munición normal conserva la muerte animada existente");
            vendor.transform.position = new Vector3(1.4f, 0, 1);
            Require(shop.TryOpen(vendor), "Reabre mercader con inventario lleno");
        }
        static void AimAt(Vector3 target) { Vector3 position = new Vector3(2.7f, 2.6f, -2); camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position)); Physics.SyncTransforms(); }
        static void MouseInput(bool left = false, bool right = false) { var state = new MouseState(); if (left) state = state.WithButton(MouseButton.Left); if (right) state = state.WithButton(MouseButton.Right); InputSystem.QueueStateEvent(mouse, state); InputSystem.Update(); mouse.MakeCurrent(); }
        static void Keys(params UnityEngine.InputSystem.Key[] keys) { keyboard.MakeCurrent(); InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); }
        static T Read<T>(object source, string field) => (T)source.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(source);
        static void Click(Button button, string label) => typeof(CharacterCustomizationValidation).GetMethod("ClickThroughEventSystem", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { button, label });
        static void Capture(string file)
        {
            var rt = (RenderTexture)typeof(CharacterCustomizationValidation).GetMethod("GameViewFramebuffer", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            if (rt == null) throw new InvalidOperationException("Framebuffer ausente");
            var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                if (SystemInfo.graphicsUVStartsAtTop)
                {
                    Color32[] pixels = image.GetPixels32(); for (int y = 0; y < rt.height / 2; y++) for (int x = 0; x < rt.width; x++)
                    { int a = y * rt.width + x, b = (rt.height - 1 - y) * rt.width + x; Color32 p = pixels[a]; pixels[a] = pixels[b]; pixels[b] = p; }
                    image.SetPixels32(pixels);
                }
                image.Apply(); File.WriteAllBytes(HealingExplosiveSetup.Folder + "/" + file, image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
        }
        static void OnLog(string text, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(text + "\n" + stack); }
        static void Check(bool ok, string text) { checks.Add((ok ? "PASS " : "FAIL ") + text); if (!ok) failures++; Debug.Log("HEALING_EXPLOSIVE_" + (ok ? "PASS " : "FAIL ") + text); }
        static void Require(bool ok, string text) { Check(ok, text); if (!ok) throw new InvalidOperationException(text); }
        static void Finish()
        {
            Application.logMessageReceived -= OnLog;
            if (mouse != null) InputSystem.RemoveDevice(mouse); if (keyboard != null) InputSystem.RemoveDevice(keyboard); if (gamepad != null) InputSystem.RemoveDevice(gamepad);
            if (previousSettings != null) InputSystem.settings = previousSettings; if (settings != null) Object.DestroyImmediate(settings);
            File.WriteAllText(HealingExplosiveSetup.Folder + "/result.json", JsonUtility.ToJson(new Result { passed = checks.Count - failures, failed = failures, checks = checks.ToArray(), errors = errors.ToArray() }, true));
            SessionState.SetInt(Key + ".Exit", failures == 0 && errors.Count == 0 ? 0 : 1); EditorApplication.ExitPlaymode();
        }
    }
}
