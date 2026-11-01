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
using UPP.ThirdPersonController;
using Object = UnityEngine.Object;

namespace Survive.Editor
{
    /// <summary>Escenario aislado en Play Mode con los prefabs reales; no escribe perfiles ni guarda la escena de prueba.</summary>
    [InitializeOnLoad]
    public static class VendorTurretValidation
    {
        const string Key = "Survive.VendorTurretValidation";
        const string ResultPath = "Logs/VendorTurret-Validation.json";
        static readonly List<string> checks = new List<string>();
        static readonly List<string> errors = new List<string>();
        static int stage, failed;
        static double nextStep, started;
        static GameObject player;
        static PlayerWallet wallet;
        static PlayerVendorController shop;
        static PlayerTurretController builder;
        static PlayerWeaponInventory inventory;
        static NpcVendor vendor;
        static AutoTurret turret;
        static EnemyHealth enemy, secondEnemy;
        static int lastShotCount;
        static float lastHealth;

        [Serializable] sealed class Result
        {
            public string scope = "Play Mode aislado con prefabs reales y smoke de compra/colocación en Level 1; sin partida manual completa ni build";
            public int passed, failed;
            public string[] checks, errors;
        }

        static VendorTurretValidation()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
                checks.Clear(); errors.Clear(); failed = stage = 0;
                started = EditorApplication.timeSinceStartup; nextStep = started + 1.5;
                Application.logMessageReceived += CaptureError;
            };
        }

        [MenuItem("Survive/Vendedor/Validar compras y torretas en Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Salir de Play Mode.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory("Logs");
            VendorTurretSetup.CreateTurretIcon();
            SessionState.SetBool(Key, true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        private static void CaptureError(string message, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
                errors.Add(message + "\n" + stack);
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
            double now = EditorApplication.timeSinceStartup;
            if (now - started > 120) { errors.Add("Timeout 120s"); Finish(); return; }
            if (now < nextStep) return;
            nextStep = now + .25;
            try
            {
                switch (stage++)
                {
                    case 0: CreateScenario(); break;
                    case 1:
                        Cursor.lockState = CursorLockMode.Locked;
                        Debug.Log($"VENDOR_READY canOpen={shop.CanOpen} reach={vendor.IsInReach(player)} cursor={Cursor.lockState} health={player.GetComponent<PlayerHealth>().IsDead} timeScale={Time.timeScale} movement={player.GetComponent<UPPCharacterMovementComponent>().enabled} disabledMove={player.GetComponent<UPPCharacterMovementComponent>().DisableAllMove} playerPos={player.transform.position} vendorPos={vendor.transform.position}");
                        Check(shop.TryOpen(vendor), "Abre tienda para captura visual");
                        if (!shop.IsOpen) throw new InvalidOperationException("No se pudo abrir la tienda de prueba");
                        CaptureShopImage();
                        break;
                    case 2: TestEconomy(); break;
                    case 3: shop.Close(); nextStep = now + .4; break;
                    case 4: TestInteractionAndPlacement(); break;
                    case 5: TestCombat(); break;
                    case 6: TestAutomaticFire(); break;
                    case 7: TestDeathAndCleanup(); break;
                    case 8:
                        Time.timeScale = 1;
                        EditorSceneManager.LoadSceneInPlayMode(VendorTurretSetup.LevelPath,
                            new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
                        nextStep = now + 1.5;
                        break;
                    case 9: TestLevelOneShop(); break;
                    case 10: TestLevelOnePlacement(); break;
                    default: Finish(); break;
                }
            }
            catch (Exception exception) { errors.Add(exception.ToString()); Finish(); }
        }

        private static void CreateScenario()
        {
            Time.timeScale = 1;
            var light = new GameObject("ValidationSun").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(45, -35, 0);
            RenderSettings.ambientLight = Color.gray;
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "ValidationFloor"; floor.transform.position = new Vector3(0, -.5f, 0);
            floor.transform.localScale = new Vector3(100, 1, 100);
            player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(VendorTurretSetup.PlayerPath), new Vector3(0, .05f, 0), Quaternion.identity);
            player.GetComponent<UPPCharacterMovementComponent>().UseDefaultControllerInput = false;
            wallet = player.GetComponent<PlayerWallet>(); shop = player.GetComponent<PlayerVendorController>();
            builder = player.GetComponent<PlayerTurretController>(); inventory = player.GetComponent<PlayerWeaponInventory>();
            // Este escenario de transacciones entrega fondos explícitos y comienza sin equipo.
            VendorTurretSetup.Set(inventory, "startingWeaponPrefab", null);
            // El editor batch no conserva captura de mouse. Sólo cambia instancias de esta escena aislada.
            VendorTurretSetup.Set(shop, "requireLockedCursor", false);
            VendorTurretSetup.Set(builder, "requireLockedCursor", false);
            VendorTurretSetup.Set(player.GetComponent<PlayerWeaponController>(), "requireLockedCursor", false);
            VendorTurretSetup.Set(player.GetComponent<PlayerInteractionController>(), "requireLockedCursor", false);
            vendor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(VendorTurretSetup.VendorPath), new Vector3(0, 0, 2), Quaternion.Euler(0, 180, 0)).GetComponent<NpcVendor>();
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            Check(wallet != null && shop != null && builder != null && inventory != null, "Componentes integrados en el prefab real");
            Check(vendor.Catalog.Count == 9 && Enumerable.Range(0, 9).All(i => vendor.Catalog.GetOffer(i).IsValid), "Catálogo: ocho armas válidas y una torreta");
            Check(wallet.Coins == 0, "Saldo inicial por partida: cero");
            wallet.AddCoins(250);
            Check(builder.Count == 0, "Reserva inicial de torretas vacía");
        }

        private static void TestEconomy()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Physics.SyncTransforms();
            Check(shop.IsOpen || shop.TryOpen(vendor), "Abrir vendedor cercano");
            Check(Cursor.lockState == CursorLockMode.None && player.GetComponent<UPPCharacterMovementComponent>().DisableAllMove, "Tienda libera cursor y bloquea movimiento");
            Check(!builder.BeginPlacement(), "No coloca durante el comercio");
            Check(shop.TryBuy(0) && wallet.Coins == 200, "Compra pistola: descuenta exactamente 50");
            WeaponItem first = inventory.GetWeapon(0);
            Check(first != null && first.IsEquipped && !first.gameObject.activeSelf && first.AmmoInMagazine == first.MagazineSize, "Entrega arma guardada y munición original");
            Check(player.GetComponent<PlayerWeaponController>().EquippedWeapon == null, "Comprar no cambia el arma en mano");
            Check(shop.TryBuy(8) && wallet.Coins == 50 && builder.Count == 1, "Compra torreta: descuenta 150 y agrega un kit");
            Check(!shop.TryBuy(7) && wallet.Coins == 50, "Sin fondos: no cobra ni entrega");
            Check(!shop.TryBuy(-1) && !shop.TryBuy(99) && wallet.Coins == 50, "Índices inválidos sin cobro");
            wallet.AddCoins(10000);
            int beforeFailure = wallet.Coins;
            Action rejectDelivery = () => inventory.enabled = false;
            wallet.Changed += rejectDelivery;
            bool delivered = shop.TryBuy(0);
            wallet.Changed -= rejectDelivery;
            inventory.enabled = true;
            Check(!delivered && wallet.Coins == beforeFailure && inventory.GetWeapon(1) == null,
                "Entrega rechazada devuelve el dinero y no deja un arma comprada");
            for (int index = 1; index < inventory.SlotCount; index++) Check(shop.TryBuy(0), "Compra en hueco libre " + index);
            int coins = wallet.Coins;
            Check(!inventory.HasFreeSlot && !shop.TryBuy(0) && wallet.Coins == coins && inventory.GetWeapon(0) == first, "Seis slots: compra rechazada sin tirar ni reemplazar armas");
            for (int attempt = 0; attempt < builder.Capacity && builder.Count < builder.Capacity; attempt++)
            {
                bool received = shop.TryBuy(8);
                Check(received, "Compra kit hasta capacidad " + builder.Count);
                if (!received) break;
            }
            coins = wallet.Coins;
            Check(!shop.TryBuy(8) && wallet.Coins == coins, "Reserva llena: sin cobro");
            Check(!wallet.TrySpend(-1) && !wallet.TrySpend(int.MaxValue) && wallet.Coins == coins, "Wallet rechaza importes inválidos e insuficientes");
            wallet.AddCoins(-10); Check(wallet.Coins == coins, "Crédito negativo rechazado");
            shop.Close();
            Check(!shop.TryBuy(0) && wallet.Coins == coins, "No compra con tienda cerrada");
            Check(!shop.TryOpen(vendor), "Cerrar impide reapertura en el mismo cuadro");
        }

        private static void CaptureShopImage()
        {
            Canvas canvas = shop.GetComponentsInChildren<Canvas>(true).First(item => item.name == "VendorAndTurretUI");
            Camera camera = Camera.main;
            var texture = new RenderTexture(1280, 900, 24);
            RenderTexture previous = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            int previousMask = camera.cullingMask;
            float previousAspect = camera.aspect;
            Texture2D photo = null;
            try
            {
                texture.Create(); camera.targetTexture = texture;
                camera.cullingMask |= 1 << LayerMask.NameToLayer("UI"); camera.aspect = 1280f / 900;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .5f;
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = texture;
                photo = new Texture2D(1280, 900, TextureFormat.RGB24, false);
                photo.ReadPixels(new Rect(0, 0, 1280, 900), 0, 0); photo.Apply();
                File.WriteAllBytes("Logs/VendorTurret-Shop.png", photo.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
                camera.targetTexture = previousTarget; camera.cullingMask = previousMask; camera.aspect = previousAspect;
                RenderTexture.active = previous; texture.Release(); Object.DestroyImmediate(texture);
                if (photo != null) Object.DestroyImmediate(photo);
            }
        }

        private static void TestInteractionAndPlacement()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Camera camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("Falta cámara real UPP");
            foreach (var cameraController in Object.FindObjectsByType<UPP.ThirdPersonController.CameraSystems.UPPTPSCameraController>(FindObjectsSortMode.None))
                cameraController.enabled = false;
            camera.transform.SetPositionAndRotation(new Vector3(0, 1, -.8f), Quaternion.LookRotation(new Vector3(0, 1, 2) - new Vector3(0, 1, -.8f)));
            Physics.SyncTransforms();
            PlayerInteractionController interaction = player.GetComponent<PlayerInteractionController>();
            interaction.RefreshTarget();
            Check(interaction.CurrentInteractable == vendor, "Raycast existente detecta NPC y muestra acción genérica");
            GameObject wall = Cube("InteractionWall", new Vector3(0, 1, 1), new Vector3(2, 2, .2f));
            Physics.SyncTransforms(); interaction.RefreshTarget();
            Check(!interaction.HasTarget && !vendor.IsInReach(player), "Pared bloquea interacción y transacción");
            Object.DestroyImmediate(wall);
            camera.transform.SetPositionAndRotation(new Vector3(0, 2, -2), Quaternion.LookRotation(new Vector3(2.6f, 0, 1) - new Vector3(0, 2, -2)));
            Physics.SyncTransforms();
            Check(builder.BeginPlacement(), "Inicia colocación con kit");
            int kits = builder.Count;
            Check(!inventory.TrySelect(0) && !player.GetComponent<PlayerWeaponController>().TryReload(), "Colocación bloquea selección y arma");
            Check(builder.TryPlace() && builder.Count == kits - 1, "Colocar en suelo válido consume exactamente un kit");
            TurretItem pickup = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(VendorTurretSetup.ItemPath),
                new Vector3(-2, 1, 0), Quaternion.identity).GetComponent<TurretItem>();
            Check(pickup.Interact(player) && builder.Count == kits && !pickup.Interact(player),
                "Kit del mundo se recoge una vez y se integra a la misma reserva");
            AutoTurret placed = Object.FindObjectsByType<AutoTurret>(FindObjectsSortMode.None).First(t => t.enabled);
            Check(placed.GetComponent<UnityEngine.AI.NavMeshObstacle>().carving, "Torreta colocada tiene obstáculo de navegación");
            placed.enabled = false;
            Check(Physics.Raycast(new Vector3(4, 2, 0), Vector3.down, out RaycastHit ground, 3), "Suelo de validación disponible");
            Check(builder.ValidatePlacement(ground, out _), "Valida base apoyada");
            GameObject obstruction = Cube("PlacementObstruction", new Vector3(4, .7f, 0), new Vector3(.5f, 1.4f, .5f));
            Physics.SyncTransforms(); Check(!builder.ValidatePlacement(ground, out _), "Rechaza volumen ocupado");
            Object.DestroyImmediate(obstruction);
            GameObject lowObstacle = Cube("LowPlacementObstruction", new Vector3(4, .1f, 0), new Vector3(.4f, .2f, .4f));
            Physics.SyncTransforms(); Check(!builder.ValidatePlacement(ground, out _), "Rechaza objetos bajos debajo de la base");
            Object.DestroyImmediate(lowObstacle);
            Physics.Raycast(new Vector3(9, 2, 0), Vector3.down, out RaycastHit distant, 3);
            Check(!builder.ValidatePlacement(distant, out _), "Rechaza colocación fuera de alcance");
            GameObject slope = Cube("SteepSlope", new Vector3(-2, .5f, 0), new Vector3(2, .2f, 2));
            slope.transform.rotation = Quaternion.Euler(0, 0, 35); Physics.SyncTransforms();
            Physics.Raycast(new Vector3(-2, 3, 0), Vector3.down, out RaycastHit steep, 5);
            Check(!builder.ValidatePlacement(steep, out _), "Rechaza pendiente excesiva");
            Object.DestroyImmediate(slope);
            camera.transform.SetPositionAndRotation(new Vector3(0, 2, -2), Quaternion.LookRotation(new Vector3(0, 3, 0)));
            Check(builder.BeginPlacement(), "Reabre colocación");
            kits = builder.Count; Check(!builder.TryPlace() && builder.Count == kits, "Sin suelo: no consume kit");
            builder.CancelPlacement(); Check(!builder.IsPlacing && builder.Count == kits, "Cancelar no consume kit");
            Time.timeScale = 0; Check(!builder.BeginPlacement(), "Pausa impide colocar"); Time.timeScale = 1;
        }

        private static void TestCombat()
        {
            turret = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(VendorTurretSetup.TurretPath), new Vector3(15, 0, 0), Quaternion.identity).GetComponent<AutoTurret>();
            enemy = Enemy("NearEnemy", new Vector3(15, 0, 6));
            secondEnemy = Enemy("FarEnemy", new Vector3(15, 0, 10));
            Physics.SyncTransforms(); turret.AcquireTarget();
            Check(turret.CurrentTarget == enemy, "Adquiere al enemigo vivo más cercano");
            float hp = enemy.CurrentHealth;
            Check(turret.TryFire() && enemy.CurrentHealth == hp - turret.Definition.Damage, "Tiro aplica daño por EnemyHealth");
            Check(!turret.TryFire(), "Cadencia impide dos disparos en el mismo cuadro");
            GameObject wall = Cube("CombatWall", new Vector3(15, 1.5f, 3), new Vector3(3, 3, .3f));
            Physics.SyncTransforms(); turret.AcquireTarget();
            Check(turret.CurrentTarget == null && !turret.TryFire(), "Pared bloquea adquisición y disparo");
            Object.DestroyImmediate(wall);
            GameObject friendly = Cube("Friendly", new Vector3(15, 1, 3), new Vector3(1, 2, 1));
            PlayerHealth friendlyHealth = friendly.AddComponent<PlayerHealth>();
            Physics.SyncTransforms(); turret.AcquireTarget();
            Check(turret.CurrentTarget == null && friendlyHealth.CurrentHealth == friendlyHealth.MaxHealth, "Jugador bloquea tiro y nunca recibe daño de torreta");
            Object.DestroyImmediate(friendly);
            enemy.transform.position = new Vector3(15, 0, 25); secondEnemy.transform.position = new Vector3(15, 0, 30);
            Physics.SyncTransforms(); turret.AcquireTarget();
            Check(turret.CurrentTarget == null && !turret.TryFire(), "No dispara fuera del rango");
            enemy.transform.position = new Vector3(15, 0, 6); secondEnemy.transform.position = new Vector3(15, 0, 10);
            Physics.SyncTransforms(); turret.AcquireTarget();
            lastHealth = enemy.CurrentHealth; lastShotCount = turret.ShotsFired;
            nextStep = EditorApplication.timeSinceStartup + .85;
        }

        private static void TestAutomaticFire()
        {
            Check(turret.ShotsFired > lastShotCount && enemy.CurrentHealth < lastHealth, "Update dispara automáticamente al enemigo en rango");
            if (!enemy.IsDead) enemy.TakeDamage(enemy.CurrentHealth);
            Physics.SyncTransforms(); turret.AcquireTarget();
            Check(turret.CurrentTarget == secondEnemy, "Ignora muerto y cambia de objetivo");
            float hp = secondEnemy.CurrentHealth; Time.timeScale = 0;
            Check(!turret.TryFire() && secondEnemy.CurrentHealth == hp, "Pausa detiene daño automático"); Time.timeScale = 1;
            turret.enabled = false;
        }

        private static void TestDeathAndCleanup()
        {
            // Moneda: collider hijo, un solo crédito pese a llamadas de trigger repetidas.
            GameObject coinObject = new GameObject("Coin"); CoinPickup coin = coinObject.AddComponent<CoinPickup>();
            MethodInfo trigger = typeof(CoinPickup).GetMethod("OnTriggerEnter", BindingFlags.Instance | BindingFlags.NonPublic);
            int coins = wallet.Coins;
            var child = new GameObject("CoinChildCollider"); child.transform.SetParent(player.transform, false);
            Collider childCollider = child.AddComponent<SphereCollider>(); childCollider.isTrigger = true;
            trigger.Invoke(coin, new object[] { childCollider });
            trigger.Invoke(coin, new object[] { childCollider });
            Check(wallet.Coins == coins + 1, "Moneda acredita una vez aunque se repita el trigger");
            vendor.transform.position = player.transform.position + Vector3.forward * 2;
            Cursor.lockState = CursorLockMode.Locked; Physics.SyncTransforms();
            Check(shop.TryOpen(vendor), "Reabre comercio tras cooldown");
            player.GetComponent<PlayerHealth>().TakeDamage(10000);
            coins = wallet.Coins;
            Check(!shop.TryBuy(8) && wallet.Coins == coins && !builder.BeginPlacement(), "Muerte impide compra y colocación");
            shop.Close();
            Check(!shop.IsOpen && player.GetComponent<UPPCharacterMovementComponent>().DisableAllMove, "Cerrar tras muerte no reactiva movimiento");
            Check(!builder.IsPlacing, "No quedan vistas previas activas");
        }

        private static void TestLevelOneShop()
        {
            shop = Object.FindFirstObjectByType<PlayerVendorController>();
            vendor = Object.FindFirstObjectByType<NpcVendor>();
            Check(shop != null && vendor != null, "Level 1: jugador y vendedor integrados");
            EnemySpawner spawner = Object.FindFirstObjectByType<EnemySpawner>();
            Check(spawner != null && spawner.IsRunning && spawner.State != WaveRunState.Failed,
                "Level 1: oleadas originales cargan y arrancan tras renombrado de campo");
            if (shop == null || vendor == null) throw new InvalidOperationException("Falta integración en Level 1");
            player = shop.gameObject; wallet = player.GetComponent<PlayerWallet>();
            Check(wallet.Coins == 0, "Level 1: saldo inicial cero");
            wallet.AddCoins(250); // Fondos del escenario de compra, no del jugador de producción.
            builder = player.GetComponent<PlayerTurretController>();
            VendorTurretSetup.Set(shop, "requireLockedCursor", false);
            VendorTurretSetup.Set(builder, "requireLockedCursor", false);
            PlayerInteractionController interaction = player.GetComponent<PlayerInteractionController>();
            VendorTurretSetup.Set(interaction, "requireLockedCursor", false);
            foreach (var controller in Object.FindObjectsByType<UPP.ThirdPersonController.CameraSystems.UPPTPSCameraController>(FindObjectsSortMode.None))
                controller.enabled = false;
            Camera camera = Camera.main;
            camera.transform.SetPositionAndRotation(player.transform.position + new Vector3(0, 1.4f, -1),
                Quaternion.LookRotation(vendor.transform.position + Vector3.up - (player.transform.position + new Vector3(0, 1.4f, -1))));
            Physics.SyncTransforms(); interaction.RefreshTarget();
            Check(interaction.CurrentInteractable == vendor && interaction.TryInteract(), "Level 1: raycast y apertura desde el spawn real");
            Check(shop.IsOpen && shop.TryBuy(0) && shop.TryBuy(8) && wallet.Coins == 50 && builder.Count == 1,
                "Level 1: compra arma y kit con el saldo real");
            CaptureShopImage();
            shop.Close();
        }

        private static void TestLevelOnePlacement()
        {
            Camera camera = Camera.main;
            bool found = false;
            for (int radius = 2; radius <= 4 && !found; radius++)
                for (int index = 0; index < 16 && !found; index++)
                {
                    float angle = index * Mathf.PI / 8;
                    Vector3 sample = player.transform.position + new Vector3(Mathf.Sin(angle) * radius, 3, Mathf.Cos(angle) * radius);
                    if (!Physics.Raycast(sample, Vector3.down, out RaycastHit ground, 7, ~0, QueryTriggerInteraction.Ignore)
                        || !builder.ValidatePlacement(ground, out _)) continue;
                    Vector3 cameraOrigin = player.transform.position + Vector3.up * 1.7f;
                    camera.transform.SetPositionAndRotation(cameraOrigin, Quaternion.LookRotation(ground.point - cameraOrigin));
                    found = true;
                }
            Check(found, "Level 1: hay suelo con apoyo y espacio libre cerca del spawn");
            Check(found && builder.BeginPlacement() && builder.TryPlace() && builder.Count == 0,
                "Level 1: colocación real consume un kit y genera torreta");
            AutoTurret deployed = Object.FindObjectsByType<AutoTurret>(FindObjectsSortMode.None).FirstOrDefault(item => item.enabled);
            Check(deployed != null && deployed.Definition != null, "Level 1: torreta desplegada activa y configurada");
        }

        private static EnemyHealth Enemy(string name, Vector3 position)
        {
            GameObject root = new GameObject(name); root.transform.position = position;
            CapsuleCollider body = root.AddComponent<CapsuleCollider>(); body.center = Vector3.up; body.height = 2;
            return root.AddComponent<EnemyHealth>();
        }

        private static GameObject Cube(string name, Vector3 position, Vector3 scale)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = name;
            obj.transform.position = position; obj.transform.localScale = scale; return obj;
        }

        private static void Check(bool condition, string name)
        {
            checks.Add((condition ? "PASS " : "FAIL ") + name);
            if (!condition) failed++;
            Debug.Log("VENDOR_TURRET_CHECK " + checks[checks.Count - 1]);
        }

        private static void Finish()
        {
            SessionState.SetBool(Key, false);
            Application.logMessageReceived -= CaptureError;
            var result = new Result { passed = checks.Count - failed, failed = failed, checks = checks.ToArray(), errors = errors.ToArray() };
            File.WriteAllText(ResultPath, JsonUtility.ToJson(result, true));
            bool pass = failed == 0 && errors.Count == 0;
            Debug.Log($"SURVIVE_VENDOR_TURRET_VALIDATION={(pass ? "PASS" : "FAIL")} passed={result.passed} failed={failed} errors={errors.Count}");
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.delayCall += () => EditorApplication.Exit(pass ? 0 : 1);
        }
    }
}
