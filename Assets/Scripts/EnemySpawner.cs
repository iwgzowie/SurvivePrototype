using UnityEngine;
using System.Collections.Generic;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private GameObject chasingPrefab;
    [SerializeField] private GameObject chasingRangedPrefab;
    [SerializeField] private GameObject patrolPrefab;

    [SerializeField] private List<EnemyWave> waves;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
   
    }

      private void SpawnEnemy(GameObject enemyPrefab)
      {
        Instantiate(enemyPrefab, transform.position, transform.rotation);
      }
}


