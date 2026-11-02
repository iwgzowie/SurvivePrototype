using UnityEngine;


[RequireComponent(typeof(Collider))]
public class PuertaConLlave : MonoBehaviour
{
    [Header("Efectos al Desaparecer")]
    [SerializeField] private GameObject objectdisappearingeffect;
    [SerializeField] private AudioClip openSound;

    [Header("Reglas")]
    [SerializeField] private bool keyConsume = true;
    [SerializeField] private bool destroyObject = false;

    private bool isOpen = false;

    private void Awake()
    {
       
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        
        if (isOpen) return;

        
        if (other.CompareTag("Player") )
        {
            
            if (KeyPickup.getBossKey)
            {
                AbrirYDesaparecer();
            }
        }
    }

   
    private void AbrirYDesaparecer()
    {
        isOpen = true;

        
        if (keyConsume)
        {
            KeyPickup.getBossKey = false;
        }

        
        if (openSound != null)
        {
            AudioSource.PlayClipAtPoint(openSound, transform.position);
        }

        
        if (objectdisappearingeffect != null)
        {
            Instantiate(objectdisappearingeffect, transform.position, Quaternion.identity);
        }

        
        if (destroyObject)
        {
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}