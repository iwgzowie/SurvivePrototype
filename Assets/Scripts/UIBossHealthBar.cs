using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIBossHealthBar : MonoBehaviour
{
    [Header("Elementos de UI")]
    [SerializeField] private GameObject healthBarContainer;
    [SerializeField] private Image healthFillImage;
    [SerializeField] private TMP_Text bossNameText;

    private EnemyHealth currentBossHealth;

    private void OnEnable()
    {
        BossBase.OnBossSpawnedUI += ShowBossHealthBar;
        BossBase.OnBossDefeatedUI += HideBossHealthBar;
    }

    private void OnDisable()
    {
        BossBase.OnBossSpawnedUI -= ShowBossHealthBar;
        BossBase.OnBossDefeatedUI -= HideBossHealthBar;
    }

    private void Start()
    {
        if (healthBarContainer != null)
        {
            healthBarContainer.SetActive(false);
        }
    }

    private void Update()
    {
        if (currentBossHealth != null && healthFillImage != null)
        {
            float fillAmount = currentBossHealth.CurrentHealth / currentBossHealth.MaxHealth;
            healthFillImage.fillAmount = fillAmount;
        }
    }

    private void ShowBossHealthBar(BossBase boss)
    {
        if (boss == null) return;

        currentBossHealth = boss.Health;
        if (bossNameText != null) bossNameText.text = boss.gameObject.name.Replace("(Clone)", "");
        if (healthBarContainer != null) healthBarContainer.SetActive(true);
    }

    private void HideBossHealthBar()
    {
        if (healthBarContainer != null) healthBarContainer.SetActive(false);
        currentBossHealth = null;
    }
}
