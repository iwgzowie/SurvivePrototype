using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BossHealthBarUI : MonoBehaviour
{
    [Header("Elementos de UI")]
    [SerializeField] private GameObject rootContainer;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TextMeshProUGUI bossNameText;

    private EnemyHealth currentBossHealth;

    private void Awake()
    {
        if (rootContainer == null) rootContainer = gameObject;
        rootContainer.SetActive(false);
    }

    private void OnEnable()
    {
        BossBase.OnBossSpawnedUI += ShowBossBar;
        BossBase.OnBossDefeatedUI += HideBossBar;
    }

    private void OnDisable()
    {
        BossBase.OnBossSpawnedUI -= ShowBossBar;
        BossBase.OnBossDefeatedUI -= HideBossBar;

        UnsubscribeHealthEvents();
    }

    private void ShowBossBar(BossBase boss)
    {
        if (boss == null || boss.Health == null) return;

        currentBossHealth = boss.Health;

        if (bossNameText != null)
        {
            bossNameText.text = boss.BossName;
        }

        if (healthSlider != null)
        {
            healthSlider.minValue = 0f;
            healthSlider.maxValue = currentBossHealth.MaxHealth;
            healthSlider.value = currentBossHealth.CurrentHealth;
        }

        // Si EnemyHealth tiene un evento OnHealthChanged o similar, suscríbete aquí.
        // Como respaldo, el valor también se sincroniza en Update si cambia.
        rootContainer.SetActive(true);
    }

    private void Update()
    {
        if (rootContainer.activeSelf && currentBossHealth != null && healthSlider != null)
        {
            healthSlider.value = Mathf.Lerp(healthSlider.value, currentBossHealth.CurrentHealth, Time.deltaTime * 10f);
        }
    }

    private void HideBossBar()
    {
        UnsubscribeHealthEvents();
        if (rootContainer != null)
        {
            rootContainer.SetActive(false);
        }
    }

    private void UnsubscribeHealthEvents()
    {
        currentBossHealth = null;
    }
}