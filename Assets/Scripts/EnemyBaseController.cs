using UnityEngine;

public class EnemyBaseController : MonoBehaviour

{
    private GameObject player;
    private Rigidbody enemyBaseRb;
    [SerializeField] private float speed = 5.0f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        enemyBaseRb = GetComponent<Rigidbody>();
        player = GameObject.FindGameObjectWithTag("Player");
    }

    // Update is called once per frame
    void Update()
    {
        if (player != null)
        {
            transform.LookAt(player.transform);
        }
    }

    private void FixedUpdate()
    {
        
    }
}
