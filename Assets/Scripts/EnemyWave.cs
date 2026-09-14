using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class EnemyWave
{
    [Tooltip("Nombre identificable de esta oleada en el Inspector.")]
    public string waveName = "Nueva oleada";

    [Header("Enemigos melee")]
    [Min(0), InspectorName("Cantidad melee")]
    public int chasingCount;
    [Tooltip("Opcional: reemplaza el prefab melee del spawner solamente en esta oleada.")]
    public GameObject chasingPrefab;

    [Header("Enemigos a distancia")]
    [Min(0), InspectorName("Cantidad a distancia")]
    public int chasingRangedCount;
    [Tooltip("Opcional: reemplaza el prefab a distancia solamente en esta oleada.")]
    public GameObject chasingRangedPrefab;

    [Header("Patrulleros adicionales (opcional)")]
    [Min(0), InspectorName("Cantidad de patrulleros")]
    public int patrolCount;
    public GameObject patrolPrefab;

    [Header("Tiempos")]
    [Min(0f), Tooltip("Segundos antes de comenzar esta oleada; el tiempo se detiene durante la pausa.")]
    public float delayBeforeWave = 5f;
    [Min(0f), Tooltip("Segundos entre apariciones. Cero genera toda la oleada en el mismo cuadro.")]
    public float spawnInterval = 0.5f;

    [Header("Ubicación")]
    [Min(0f), Tooltip("Dispersión horizontal alrededor del punto elegido, en metros.")]
    public float spawnRadius = 3f;
    [Tooltip("Puntos de esta oleada. Si la lista está vacía se usan los del spawner; sin puntos se usa su Transform.")]
    public List<Transform> spawnPoints = new List<Transform>();

    public long TotalEnemies => (long)chasingCount + chasingRangedCount + patrolCount;
}
