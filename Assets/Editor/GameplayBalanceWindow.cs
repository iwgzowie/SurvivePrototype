using Survive.Economy;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Survive.Editor
{
    public sealed class GameplayBalanceWindow : EditorWindow
    {
        Vector2 scroll;
        [MenuItem("Survive/Gameplay/Balance de enemigos y vendedor")]
        public static void Open() => GetWindow<GameplayBalanceWindow>("Balance de gameplay");
        void OnGUI()
        {
            EditorGUILayout.HelpBox("Los valores de prefabs y catálogo se guardan como assets. La progresión de oleadas pertenece al EnemySpawner de la escena y se guarda con la escena. Editá fuera de Play Mode.", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                scroll = EditorGUILayout.BeginScrollView(scroll);
                EditorGUILayout.LabelField("Zombis: monedas y velocidad base", EditorStyles.boldLabel);
                foreach (string name in new[] { "EnemyChasing", "EnemyChasingRanged", "EnemyPatrol", "EnemyAttack" })
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Players/" + name + ".prefab");
                    if (prefab == null) continue;
                    EditorGUILayout.ObjectField(name, prefab, typeof(GameObject), false);
                    Field(prefab.GetComponent<EnemyHealth>(), "killCoins", "Monedas por baja");
                    Field(prefab.GetComponent<EnemyBaseController>(), "moveSpeed", "Velocidad base (m/s)");
                    EditorGUILayout.Space(5);
                }
                var boss = AssetDatabase.LoadAssetAtPath<GameObject>(ZombieRewardBossSetup.BossPath);
                if (boss != null)
                {
                    EditorGUILayout.LabelField("Boss", EditorStyles.boldLabel);
                    EditorGUILayout.ObjectField("Prefab", boss, typeof(GameObject), false);
                    Field(boss.GetComponent<BossBase>(), "directGoldReward", "Monedas directas");
                    Field(boss.GetComponent<NavMeshAgent>(), "m_Speed", "Velocidad base (m/s)");
                }
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Torreta", EditorStyles.boldLabel);
                var turret = AssetDatabase.LoadAssetAtPath<Survive.Turrets.TurretDefinition>("Assets/Data/Vendor/TD_AutoTurret.asset");
                Field(turret, "maxHealth", "Vida máxima");
                Field(turret, "healthCostPerShot", "Desgaste por disparo");
                Field(turret, "shotsPerSecond", "Disparos por segundo");
                var ammo = AssetDatabase.LoadAssetAtPath<Survive.Items.AmmoBoxDefinition>("Assets/Data/Vendor/AD_CommonAmmoBox.asset");
                Field(ammo, "rounds", "Balas por caja común");
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Velocidad por oleada", EditorStyles.boldLabel);
                var spawner = FindFirstObjectByType<EnemySpawner>();
                if (spawner == null) EditorGUILayout.HelpBox("Abrí Level 2 para editar su EnemySpawnerManager.", MessageType.Info);
                else
                {
                    EditorGUILayout.ObjectField("Spawner de la escena", spawner, typeof(EnemySpawner), true);
                    Field(spawner, "speedIncreasePerWave", "Aumento por oleada (0.04 = 4 %)");
                    var data = new SerializedObject(spawner); var templates = data.FindProperty("baseWaveTemplates");
                    for (int i = 0; i < templates.arraySize; i++)
                    {
                        var template = templates.GetArrayElementAtIndex(i);
                        EditorGUILayout.PropertyField(template.FindPropertyRelative("speedMultiplier"), new GUIContent("Velocidad plantilla " + (i + 1)));
                    }
                    data.ApplyModifiedProperties();
                    EditorGUILayout.HelpBox("Velocidad final = velocidad base × multiplicador de plantilla × (1 + índice de oleada × aumento). La primera oleada tiene índice 0. El aumento continúa después de cada boss, sin tope fijo.", MessageType.None);
                }
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Precios del vendedor", EditorStyles.boldLabel);
                var catalog = AssetDatabase.LoadAssetAtPath<VendorCatalog>("Assets/Data/Vendor/VC_Armero.asset");
                if (catalog != null)
                {
                    EditorGUILayout.ObjectField("Catálogo", catalog, typeof(VendorCatalog), false);
                    var data = new SerializedObject(catalog); var offers = data.FindProperty("offers");
                    for (int i = 0; i < offers.arraySize; i++)
                        EditorGUILayout.PropertyField(offers.GetArrayElementAtIndex(i).FindPropertyRelative("price"), new GUIContent(catalog.GetOffer(i).DisplayName));
                    if (data.ApplyModifiedProperties()) AssetDatabase.SaveAssetIfDirty(catalog);
                }
                EditorGUILayout.EndScrollView();
            }
        }
        static void Field(Object target, string name, string label)
        {
            if (target == null) return;
            var data = new SerializedObject(target); var property = data.FindProperty(name);
            if (property == null) return;
            EditorGUILayout.PropertyField(property, new GUIContent(label));
            if (data.ApplyModifiedProperties() && EditorUtility.IsPersistent(target)) AssetDatabase.SaveAssetIfDirty(target);
        }
    }
}
