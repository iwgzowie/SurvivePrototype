using UnityEngine;

public class AcidDetector : MonoBehaviour
{
    [SerializeField] private GameObject acidPool;

    private bool activated = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !activated)
        {
            activated = true;

            acidPool.SetActive(true);
        }
    }
}