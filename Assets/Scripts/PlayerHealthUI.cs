using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Presenta la salud del jugador mediante referencias de UI configuradas en el prefab.</summary>
[DisallowMultipleComponent]
public sealed class PlayerHealthUI : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Image healthFill;
    [SerializeField] private TMP_Text healthText;

    [Header("Colores por nivel de vida")]
    [SerializeField] private Color normalColor = new Color(0.25f, 0.82f, 0.62f, 1f);
    [SerializeField] private Color warningColor = new Color(1f, 0.68f, 0.2f, 1f);
    [SerializeField] private Color criticalColor = new Color(0.95f, 0.22f, 0.2f, 1f);
    [SerializeField, Range(0f, 1f), Tooltip("Fracción de vida a partir de la cual se muestra el color de advertencia.")]
    private float warningThreshold = 0.5f;
    [SerializeField, Range(0f, 1f), Tooltip("Fracción de vida a partir de la cual se muestra el color crítico.")]
    private float criticalThreshold = 0.25f;

    private PlayerHealth subscribedHealth;

    private void OnEnable()
    {
        SubscribeToHealth();
        Refresh();
    }

    private void Start()
    {
        // El HUD puede habilitarse antes del Awake de la salud: Start sincroniza el valor inicial.
        SubscribeToHealth();
        Refresh();
    }

    private void OnDisable()
    {
        UnsubscribeHealth();
    }

    private void OnValidate()
    {
        warningThreshold = Mathf.Clamp01(warningThreshold);
        criticalThreshold = Mathf.Clamp(criticalThreshold, 0f, warningThreshold);
    }

    private void ResolveHealth()
    {
        if (playerHealth == null)
        {
            playerHealth = GetComponentInParent<PlayerHealth>();
        }
    }

    private void SubscribeToHealth()
    {
        ResolveHealth();
        if (subscribedHealth == playerHealth)
        {
            return;
        }
        UnsubscribeHealth();
        subscribedHealth = playerHealth;
        if (subscribedHealth != null)
        {
            subscribedHealth.OnHealthChanged += UpdateDisplay;
        }
    }

    private void UnsubscribeHealth()
    {
        if (subscribedHealth != null)
        {
            subscribedHealth.OnHealthChanged -= UpdateDisplay;
        }
        subscribedHealth = null;
    }

    /// <summary>Sincroniza la presentación sin alterar la salud ni crear objetos de interfaz.</summary>
    public void Refresh()
    {
        ResolveHealth();
        if (playerHealth == null)
        {
            if (healthFill != null) healthFill.fillAmount = 0f;
            if (healthText != null) healthText.text = "-- / --";
            return;
        }
        UpdateDisplay(playerHealth.CurrentHealth, playerHealth.MaxHealth);
    }

    private void UpdateDisplay(float current, float maximum)
    {
        float fraction = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
        if (healthFill != null)
        {
            healthFill.fillAmount = fraction;
            healthFill.color = fraction <= criticalThreshold ? criticalColor
                : fraction <= warningThreshold ? warningColor : normalColor;
        }
        if (healthText != null)
        {
            // Una fracción positiva se muestra como 1, evitando indicar cero mientras sigue vivo.
            healthText.SetText("{0:0} / {1:0}", Mathf.Ceil(current), Mathf.Ceil(maximum));
        }
    }
}
