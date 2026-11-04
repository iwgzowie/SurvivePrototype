using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Survive.Economy;
using Survive.Combat;
using Survive.Items;
using Survive.Turrets;
using Object = UnityEngine.Object;

namespace Survive.Editor
{
    [InitializeOnLoad]
    public static class ZombieRewardBossValidation
    {
        const string Key = "Survive.ZombieRewardBossValidation";
        static readonly List<string> checks = new List<string>(), errors = new List<string>();
        static double start, next; static int stage, failures; static EnemySpawner waves; static BossBase boss;
        static PlayerWallet wallet; static int balance; static Vector3 bossStart; static AutoTurret turret;
        static EnemyHealth turretTarget; static PlayerWallet testWallet; static GameObject testRoot;
        [Serializable] class Result { public int passed, failed; public string scope = "Play Mode en Level 2 real: recompensas, sonido, explosión, torreta, oleadas hasta boss, modelo/materiales, NavMesh/ruta/movimiento, muerte y botín. Sin build distribuido ni escucha manual."; public string[] checks, errors; }
        static ZombieRewardBossValidation()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Key, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    start = EditorApplication.timeSinceStartup; next = start + 3; stage = failures = 0; checks.Clear(); errors.Clear();
                    Application.runInBackground = true; Application.logMessageReceived += OnLog;
                }
                if (state == PlayModeStateChange.EnteredEditMode) SessionState.SetBool(Key, false);
            };
        }
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Salí de Play Mode.");
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != ZombieRewardBossSetup.Level) throw new Exception("Abrí Level 2.");
            typeof(CharacterCustomizationValidation).GetMethod("PrepareBatchGameView", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).Invoke(null, null);
            SessionState.SetBool(Key, true); EditorApplication.EnterPlaymode();
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup - start > 140) { Check(false, "Tiempo máximo excedido"); Finish(); return; }
            if (EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + .3;
            try
            {
                switch (stage)
                {
                    case 0:
                        waves = Object.FindFirstObjectByType<EnemySpawner>();
                        var player = Object.FindFirstObjectByType<PlayerHealth>(); wallet = player.GetComponent<PlayerWallet>();
                        Check(waves != null && wallet != null, "Level 2 tiene spawner y billetera reales");
                        player.enabled = false; // El piloto de pruebas no muere mientras se aceleran las oleadas.
                        typeof(PlayerHealth).GetField("currentHealth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(player, 100000f);
                        TestRewards(); TestExplosion(); SetupTurret(); stage++; next += 2;
                        break;
                    case 1:
                        Check(turret.ShotsFired > 0 && turretTarget.IsDead, "Torreta real mata al objetivo");
                        Check(testWallet.Coins == 5 && testWallet.GetComponent<PlayerRewardFeedback>()?.RewardsPlayed == 1, "La baja de torreta paga y reproduce un premio para su dueño");
                        Object.Destroy(testRoot);
                        Check(waves.IsRunning, "Oleadas de Level 2 arrancan automáticamente"); stage++;
                        break;
                    case 2:
                        foreach (var enemy in waves.AliveEnemies.ToArray()) if (enemy != null && !enemy.IsDead) enemy.TakeDamage(999999);
                        boss = Object.FindFirstObjectByType<BossBase>();
                        if (waves.State == WaveRunState.Failed) { Check(false, "Oleadas fallaron: " + waves.LastError); Finish(); return; }
                        if (boss == null) break;
                        Check(waves.CurrentWaveNumber >= 2 && waves.CurrentWaveNumber <= 5, "El ciclo real de oleadas genera al boss entre las oleadas 2 y 5");
                        var agent = boss.GetComponent<NavMeshAgent>();
                        Check(agent.agentTypeID == -902729914 && agent.isOnNavMesh, "Boss vinculado al NavMesh de tipo Boss");
                        Check(boss.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r => r.sharedMesh != null && r.enabled), "Boss tiene mesh 3D visible");
                        Check(boss.GetComponentsInChildren<SkinnedMeshRenderer>().SelectMany(r => r.sharedMaterials).All(m => m != null && m.shader != null && m.shader.isSupported), "Todos los materiales del boss son válidos y soportados");
                        var anim = boss.GetComponentInChildren<Animator>();
                        Check(anim.avatar != null && anim.avatar.isValid && anim.runtimeAnimatorController != null, "Boss tiene avatar y animaciones válidas");
                        float expectedHealth = 700f * .3f * (1f + waves.CurrentWaveIndex * .15f) * 2.5f;
                        Check(Mathf.Approximately(boss.Health.MaxHealth, expectedHealth) && boss.Health.CurrentHealth == boss.Health.MaxHealth, "Boss recibe el multiplicador de vida configurado en Level 2: " + expectedHealth);
                        float expectedSpeed = 3.5f * Mathf.Min(2f, .3f * (1f + waves.CurrentWaveIndex * .04f));
                        float expectedDamage = 25f * .3f * (1f + waves.CurrentWaveIndex * .15f) * 1.5f;
                        float damage = (float)typeof(BossBase).GetField("areaAttackDamage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(boss);
                        Check(Mathf.Approximately(agent.speed, expectedSpeed) && Mathf.Approximately(damage, expectedDamage), "Boss recibe multiplicadores reales de daño y velocidad");
                        Check(!Object.FindFirstObjectByType<BossSpawner>().SpawnBoss(1,1,1), "Spawner impide dos bosses simultáneos");
                        balance = wallet.Coins; bossStart = boss.transform.position; stage++; next += 2;
                        break;
                    case 3:
                        var navigation = boss.GetComponent<NavMeshAgent>();
                        Check(navigation.hasPath && navigation.pathStatus == NavMeshPathStatus.PathComplete, "Boss calcula ruta completa hacia el jugador");
                        Check(Vector3.Distance(bossStart, boss.transform.position) > .2f, "Boss se desplaza realmente por Level 2");
                        Check(boss.GetComponentInChildren<Animator>().GetFloat("Speed") > .01f, "Animación recibe velocidad de navegación");
                        int keys = Object.FindObjectsByType<KeyPickup>(FindObjectsSortMode.None).Length;
                        boss.Health.TakeDamage(999999, default, wallet.gameObject);
                        Check(wallet.Coins == balance + 150, "Boss otorga 150 monedas directas al jugador");
                        Check(Object.FindObjectsByType<KeyPickup>(FindObjectsSortMode.None).Length == keys + 1, "Boss suelta una llave garantizada");
                        Check(Object.FindObjectsByType<CoinPickup>(FindObjectsSortMode.None).Length >= 5, "Boss conserva drops de monedas");
                        stage++; next += 1;
                        break;
                    case 4:
                        Check(waves.State == WaveRunState.Resting, "Derrotar al boss libera la oleada e inicia el descanso"); Finish();
                        break;
                }
            }
            catch (Exception e) { Check(false, e.ToString()); Finish(); }
        }
        static EnemyHealth Dummy(Vector3 position, Transform parent = null)
        {
            var root = new GameObject("RewardTestZombie"); root.transform.SetParent(parent); root.transform.position = position;
            root.AddComponent<CapsuleCollider>(); var h = root.AddComponent<EnemyHealth>();
            ZombieRewardBossSetup.Set(h, "rewardSound", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Rewards/ZombieKillReward.wav")); return h;
        }
        static void TestRewards()
        {
            foreach (string name in new[] { "EnemyChasing", "EnemyChasingRanged", "EnemyPatrol", "EnemyAttack" })
            {
                var h = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Players/" + name + ".prefab").GetComponent<EnemyHealth>();
                Check(h.KillCoins == 5 && h.RewardSound != null && h.RewardSound.length > .1f, name + " configurado con 5 monedas y sonido");
            }
            int coins = wallet.Coins; var enemy = Dummy(new Vector3(10000,100,10000));
            enemy.TakeDamage(30, default, wallet.gameObject); Check(wallet.Coins == coins, "Un impacto no letal no paga");
            enemy.TakeDamage(100, default, wallet.gameObject);
            Check(wallet.Coins == coins + 5, "El golpe letal acredita 5 monedas");
            var feedback = wallet.GetComponent<PlayerRewardFeedback>();
            Check(feedback != null && feedback.RewardsPlayed == 1 && feedback.GetComponents<AudioSource>().Any(a => a.isPlaying && a.spatialBlend == 0), "Premio sonoro se reproduce en 2D al matar");
            enemy.TakeDamage(100, default, wallet.gameObject);
            Check(wallet.Coins == coins + 5 && feedback.RewardsPlayed == 1, "Volver a golpear un cadáver no duplica recompensa ni sonido");
            var debug = Dummy(new Vector3(10000,100,10010)); debug.TakeDamage(100);
            Check(wallet.Coins == coins + 5, "Una muerte sin autor no paga al jugador equivocado");
        }
        static void TestExplosion()
        {
            int coins = wallet.Coins, sounds = wallet.GetComponent<PlayerRewardFeedback>().RewardsPlayed;
            var a = Dummy(new Vector3(11000,100,11000)); var b = Dummy(a.transform.position + Vector3.right);
            Physics.SyncTransforms(); var ammo = AssetDatabase.LoadAssetAtPath<ExplosiveAmmoDefinition>("Assets/Data/Vendor/AD_ExplosiveMagazine.asset");
            if (ammo == null) ammo = AssetDatabase.FindAssets("t:ExplosiveAmmoDefinition").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<ExplosiveAmmoDefinition>).First();
            Physics.Raycast(a.transform.position - Vector3.forward * 3, Vector3.forward, out var hit, 5);
            ExplosiveRoundImpact.Detonate(hit, ammo, wallet.transform);
            Check(a.IsDead && b.IsDead && wallet.Coins == coins + 10, "Explosión real paga una vez por cada zombi alcanzado");
            Check(wallet.GetComponent<PlayerRewardFeedback>().RewardsPlayed == sounds + 2, "Explosión reproduce una recompensa por cada baja");
        }
        static void SetupTurret()
        {
            testRoot = new GameObject("TurretRewardTest"); testRoot.transform.position = new Vector3(12000,100,12000);
            testWallet = new GameObject("TurretOwner").AddComponent<PlayerWallet>(); testWallet.transform.SetParent(testRoot.transform);
            turretTarget = Dummy(testRoot.transform.position + Vector3.forward * 4, testRoot.transform);
            turretTarget.TakeDamage(99);
            turret = Object.Instantiate(AssetDatabase.LoadAssetAtPath<AutoTurret>("Assets/Prefabs/Vendor/AutoTurret.prefab"), testRoot.transform.position, Quaternion.identity, testRoot.transform);
            turret.InitializeOwner(testWallet.gameObject); Physics.SyncTransforms(); turret.AcquireTarget();
        }
        static void OnLog(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); }
        static void Check(bool pass, string label) { checks.Add((pass ? "PASS " : "FAIL ") + label); if (!pass) failures++; }
        static void Finish()
        {
            Application.logMessageReceived -= OnLog;
            File.WriteAllText(ZombieRewardBossSetup.Folder + "/validation.json", JsonUtility.ToJson(new Result { passed = checks.Count - failures, failed = failures, checks = checks.ToArray(), errors = errors.ToArray() }, true));
            Debug.Log("ZOMBIE_REWARD_BOSS_VALIDATION=" + (failures == 0 ? "PASS" : "FAIL") + " " + (checks.Count - failures) + "/" + checks.Count);
            EditorApplication.ExitPlaymode();
        }
    }
}
