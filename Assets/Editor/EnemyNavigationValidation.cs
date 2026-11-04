using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Survive.Economy;
using Survive.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace Survive.Editor
{
    [InitializeOnLoad]
    public static class EnemyNavigationValidation
    {
        const string Key = "Survive.EnemyNavigationValidation", Folder = "Logs/EnemyNavigation";
        static readonly List<string> checks = new List<string>(), errors = new List<string>();
        static readonly List<string> baselineErrors = new List<string>();
        static readonly List<NavMeshDataInstance> nav = new List<NavMeshDataInstance>();
        static readonly List<GameObject> probes = new List<GameObject>();
        static double started, next; static int stage, failures, spawns;
        static Transform target, wrongPoint; static EnemySpawner waves;
        static PuertaConLlave door; static GameObject blocker;
        static EnemyHealth recovered; static int coins;
        static bool bossSeen;
        [Serializable] class Result
        {
            public int passed, failed, spawned;
            public string scope = "Play Mode: Level 2 real con puntos forzados a otra isla, fallback de zombis y boss al jugador; puerta física/corte/restauración de tres tipos de agente; recuperación de aislado; progresión de velocidad. Sin build ni partida manual completa.";
            public string[] checks, errors, baselineErrors;
        }
        static EnemyNavigationValidation()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Key, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                {
                    started = EditorApplication.timeSinceStartup; next = started + 3;
                    stage = failures = spawns = 0; bossSeen = false; checks.Clear(); errors.Clear(); baselineErrors.Clear(); probes.Clear(); nav.Clear();
                    Application.runInBackground = true; Application.logMessageReceived += OnLog;
                }
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    SessionState.SetBool(Key, false);
                    if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(Key + ".Exit", 1));
                }
            };
        }
        [MenuItem("Survive/Gameplay/Validar navegación de enemigos y balance")]
        public static void Run()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Folder);
            EditorSceneManager.OpenScene(ZombieRewardBossSetup.Level, OpenSceneMode.Single);
            typeof(CharacterCustomizationValidation).GetMethod("PrepareBatchGameView", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            SessionState.SetBool(Key, true); EditorApplication.EnterPlaymode();
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup - started > 130) { Check(false, "Tiempo máximo excedido"); Finish(); return; }
            if (EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + .25;
            try
            {
                if (stage > 0)
                {
                    foreach (var health in waves.AliveEnemies.ToArray()) if (health != null && !health.IsDead) health.TakeDamage(999999);
                    var boss = Object.FindFirstObjectByType<BossBase>();
                    if (boss != null && !bossSeen)
                    {
                        bossSeen = true; var a = boss.GetComponent<NavMeshAgent>();
                        Check(a.isOnNavMesh && EnemyNavigation.CanReach(a, boss.transform.position - Vector3.up * a.baseOffset, GameObject.FindGameObjectWithTag("Player").transform), "Boss usa alternativa conectada al jugador cuando su punto está en otra isla");
                        boss.Health.TakeDamage(999999);
                    }
                }
                switch (stage)
                {
                    case 0:
                        waves = Object.FindFirstObjectByType<EnemySpawner>();
                        var player = Object.FindFirstObjectByType<PlayerHealth>();
                        typeof(PlayerHealth).GetField("currentHealth", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(player, 100000f);
                        foreach (var health in waves.AliveEnemies.ToArray()) if (health != null) health.TakeDamage(999999);
                        waves.CancelWaves(); BuildCourse();
                        var waveData = new SerializedObject(waves);
                        waveData.FindProperty("defaultSpawnPoints").arraySize = 1;
                        waveData.FindProperty("defaultSpawnPoints").GetArrayElementAtIndex(0).objectReferenceValue = wrongPoint;
                        waveData.FindProperty("minWavesBeforeBoss").intValue = 2; waveData.FindProperty("maxWavesBeforeBoss").intValue = 2;
                        waveData.ApplyModifiedPropertiesWithoutUndo();
                        VendorTurretSetup.Set(Object.FindFirstObjectByType<BossSpawner>(), "bossSpawnPoint", wrongPoint);
                        waves.OnEnemySpawned += health =>
                        {
                            spawns++; var a = health.GetComponent<NavMeshAgent>();
                            Check(EnemyNavigation.CanReach(a, health.transform.position - Vector3.up * a.baseOffset, player.transform), "Spawn de Level 2 conectado al jugador #" + spawns);
                            Check(health.GetComponent<EnemyReachability>() != null, "Spawn incorpora recuperación #" + spawns);
                            var controller = health.GetComponent<EnemyBaseController>();
                            Check(controller != null && Mathf.Approximately(controller.MoveSpeed, a.speed) && a.speed > 0, "Velocidad de IA aplicada al agente #" + spawns);
                        };
                        Check(Object.FindObjectsByType<PuertaConLlave>(FindObjectsSortMode.None).All(d => d.GetComponentsInChildren<Collider>().Where(c => !c.isTrigger && c.enabled).All(c => c.GetComponent<NavMeshObstacle>()?.carving == true)), "Puertas reales de Level 2 tienen obstáculos con carving");
                        TestAlternatingTemplates();
                        waves.ResetAndRestartWaves();
                        Check(waves.SpeedMultiplierForWave(.3f, 1) > waves.SpeedMultiplierForWave(.3f, 0), "Velocidad aumenta de primera a segunda oleada");
                        Check(waves.SpeedMultiplierForWave(.3f, 300) > waves.SpeedMultiplierForWave(.3f, 299), "Velocidad continúa aumentando después del antiguo tope");
                        Check(Mathf.Approximately(waves.SpeedMultiplierForWave(.3f, 10), .42f), "Progresión por defecto +4 % de la base por oleada");
                        Physics.SyncTransforms();
                        foreach (var probe in probes)
                        {
                            var a = probe.GetComponent<NavMeshAgent>();
                            Check(NavMesh.CalculatePath(new Vector3(1010,100,1000), new Vector3(990,100,1000), EnemyNavigation.Filter(a), new NavMeshPath()), "El NavMesh sin corte contiene conexión: tipo " + a.agentTypeID);
                            Check(!EnemyNavigation.CanReach(a, new Vector3(1010,100,1000), target), "La barrera física invalida una ruta horneada que la atraviesa: " + a.agentTypeID);
                        }
                        door = blocker.AddComponent<PuertaConLlave>(); stage++; next += 1;
                        break;
                    case 1:
                        foreach (var probe in probes)
                        {
                            var a = probe.GetComponent<NavMeshAgent>(); var path = new NavMeshPath();
                            NavMesh.CalculatePath(new Vector3(1010,100,1000), new Vector3(990,100,1000), EnemyNavigation.Filter(a), path);
                            Check(path.status != NavMeshPathStatus.PathComplete, "La puerta cerrada corta el NavMesh: " + a.agentTypeID);
                            Check(!EnemyNavigation.TryPosition(a, wrongPoint.position, target, 2, out _), "Se rechaza spawn del lado aislado: " + a.agentTypeID);
                            Check(EnemyNavigation.TryNearPlayer(a, target, out var p) && p.x < 1000, "Alternativa en el lado del jugador: " + a.agentTypeID);
                        }
                        PlayerKeyInventory.ForPlayer(target.gameObject, true).TryAddKey();
                        Check(door.Interact(target.gameObject), "Interacción con llave abre la puerta"); stage++; next += 1.8;
                        break;
                    case 2:
                        foreach (var probe in probes)
                            Check(EnemyNavigation.CanReach(probe.GetComponent<NavMeshAgent>(), wrongPoint.position, target), "Abrir puerta restaura ruta: " + probe.GetComponent<NavMeshAgent>().agentTypeID);
                        blocker = Wall("SecondClosedDoor"); blocker.AddComponent<PuertaConLlave>();
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Players/EnemyChasing.prefab");
                        recovered = Object.Instantiate(prefab, wrongPoint.position + Vector3.up, Quaternion.identity).GetComponent<EnemyHealth>();
                        recovered.GetComponent<EnemyBaseController>().enabled = false;
                        EnemyReachability.Track(recovered.gameObject, target);
                        coins = target.GetComponent<PlayerWallet>().Coins;
                        stage++; next += 8;
                        break;
                    case 3:
                        Check(recovered.GetComponent<EnemyReachability>().Recoveries > 0 && recovered.transform.position.x < 1000, "Enemigo aislado se reubica en zona accesible");
                        Check(!recovered.IsDead && recovered.CurrentHealth == recovered.MaxHealth && target.GetComponent<PlayerWallet>().Coins == coins, "Recuperación conserva enemigo y vida sin regalar monedas");
                        Check(EnemyNavigation.CanReach(recovered.GetComponent<NavMeshAgent>(), recovered.transform.position - Vector3.up, target), "Enemigo recuperado puede llegar al jugador");
                        stage++;
                        break;
                    case 4:
                        if (!bossSeen && waves.State != WaveRunState.Failed) break;
                        if (bossSeen && waves.State == WaveRunState.WaitingForEnemies) break;
                        Check(spawns >= 5 && bossSeen && waves.State == WaveRunState.Resting, "Oleada real completa sus enemigos y boss usando puntos alternativos");
                        Check(waves.State != WaveRunState.Failed, "Ninguna oleada queda bloqueada por un punto aislado");
                        Check(errors.Count == 0, "Sin errores de ejecución durante las pruebas"); Finish();
                        break;
                }
            }
            catch (Exception e) { Check(false, e.ToString()); Finish(); }
        }
        static void BuildCourse()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "NavigationTestFloor";
            floor.transform.position = new Vector3(1000,99.9f,1000); floor.transform.localScale = new Vector3(40,.2f,14);
            foreach (int type in new[] { -1923039037, 287145453, -902729914 })
            {
                var source = new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, size = new Vector3(40,.2f,14), transform = Matrix4x4.TRS(floor.transform.position, Quaternion.identity, Vector3.one), area = 0 };
                var data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(type), new List<NavMeshBuildSource> { source }, new Bounds(new Vector3(1000,100,1000), new Vector3(44,8,18)), Vector3.zero, Quaternion.identity);
                nav.Add(NavMesh.AddNavMeshData(data));
                var probe = new GameObject("Probe" + type); probe.SetActive(false); var agent = probe.AddComponent<NavMeshAgent>();
                agent.agentTypeID = type; agent.radius = type == -902729914 ? .8f : .5f; agent.height = type == -902729914 ? 3 : 2.3f; agent.baseOffset = 1;
                probes.Add(probe);
            }
            target = new GameObject("NavigationTestPlayer").transform; target.position = new Vector3(990,100,1000);
            target.gameObject.AddComponent<PlayerHealth>(); target.gameObject.AddComponent<PlayerWallet>();
            wrongPoint = new GameObject("IsolatedSpawnPoint").transform; wrongPoint.position = new Vector3(1010,100,1000);
            blocker = Wall("ClosedDoorWithoutCarving");
        }
        static GameObject Wall(string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.position = new Vector3(1000,102,1000); go.transform.localScale = new Vector3(.5f,4,18); return go;
        }
        static void Check(bool pass, string label) { checks.Add((pass ? "PASS " : "FAIL ") + label); if (!pass) failures++; }
        static void OnLog(string message, string trace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (message == "Unknown error occurred while loading 'Assets/New Terrain 3.asset'.") baselineErrors.Add(message);
            else errors.Add(message);
        }
        static void TestAlternatingTemplates()
        {
            var field = typeof(EnemySpawner).GetField("baseWaveTemplates", BindingFlags.NonPublic | BindingFlags.Instance);
            var original = field.GetValue(waves);
            field.SetValue(waves, new List<EnemyWave> { new EnemyWave { speedMultiplier = 1f }, new EnemyWave { speedMultiplier = .1f } });
            var generate = typeof(EnemySpawner).GetMethod("GenerateProceduralWave", BindingFlags.NonPublic | BindingFlags.Instance);
            try
            {
                var first = (EnemyWave)generate.Invoke(waves, new object[] { 0, false });
                var second = (EnemyWave)generate.Invoke(waves, new object[] { 1, true });
                var third = (EnemyWave)generate.Invoke(waves, new object[] { 2, false });
                Check(second.speedMultiplier > first.speedMultiplier && third.speedMultiplier > second.speedMultiplier, "Alternar plantillas y pasar por un boss nunca reduce la velocidad");
            }
            finally { field.SetValue(waves, original); }
        }
        static void Finish()
        {
            Application.logMessageReceived -= OnLog;
            foreach (var item in nav) if (item.valid) item.Remove();
            File.WriteAllText(Folder + "/validation.json", JsonUtility.ToJson(new Result { passed = checks.Count - failures, failed = failures, spawned = spawns, checks = checks.ToArray(), errors = errors.ToArray(), baselineErrors = baselineErrors.ToArray() }, true));
            SessionState.SetInt(Key + ".Exit", failures == 0 ? 0 : 1);
            Debug.Log("ENEMY_NAVIGATION_VALIDATION=" + (failures == 0 ? "PASS" : "FAIL") + " " + (checks.Count - failures) + "/" + checks.Count);
            EditorApplication.ExitPlaymode();
        }
    }
}
