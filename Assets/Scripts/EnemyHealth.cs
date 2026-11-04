using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class EnemyHealth : MonoBehaviour
{
    //Helth Settings
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    [Header("Recompensa por baja")]
    [SerializeField, Min(0), InspectorName("Monedas por baja"), Tooltip("Monedas directas al jugador que mata este enemigo. El boss usa BossBase > Direct Gold Reward.")] private int killCoins = 5;
    [SerializeField] private AudioClip rewardSound;
    public int KillCoins => killCoins;
    public AudioClip RewardSound => rewardSound;

    //Death Settings
    [SerializeField] private float destroyDelay = 2.5f;

    //Visual Feedback (Flash)
    [SerializeField] private Renderer meshRenderer; // Puede ser MeshRenderer o SkinnedMeshRenderer
    [SerializeField] private Color hitColor = Color.red;
    [SerializeField] private float flashDuration = 0.12f;
    private Color[] originalColors;
    private Coroutine flashCoroutine;

    //Particle Feedback
    [SerializeField] private GameObject hitParticlePrefab;

    //Audio Feedback
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] hitSounds;
    [SerializeField] private AudioClip deathSound;

    // Eventos para conectar interfaces (UI) u otros sistemas
    public event Action<float, float> OnHealthChanged;
    public event Action OnDeath;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;

    private bool isDead = false;
    private bool explosiveDeath;
    private Vector3 explosionOrigin;
    private float explosionImpulse;
    private bool bossMinion;
    public void MarkAsBossMinion() => bossMinion = true;
    public bool IsDead => isDead;
    public bool HasHitParticleEffect => hitParticlePrefab != null;

    private void Awake()
    {
        currentHealth = maxHealth;

        // Auto-detección si no se asignaron en el Inspector
        if (meshRenderer == null) meshRenderer = GetComponentInChildren<Renderer>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        // Cache de los materiales originales
        if (meshRenderer != null)
        {
            Material[] materials = meshRenderer.materials;
            originalColors = new Color[materials.Length];
            for (int i = 0; i < materials.Length; i++)
                originalColors[i] = materials[i].color;
        }
    }

    public void Update()
    {
        
    }

    public void TakeDamage(float amount, Vector3 hitPosition = default, GameObject instigator = null)
    {
        if (isDead || amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount)) return;

        currentHealth -= amount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        // Feedback de impacto
        TriggerHitFlash();
        PlayHitSound();
        SpawnHitParticle(hitPosition);

        if (currentHealth <= 0f)
        {
            Die(instigator);
        }
    }

    public void TakeExplosiveDamage(float amount, Vector3 origin, float impulse, GameObject instigator = null)
    {
        if (isDead || amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount)) return;
        explosiveDeath = amount >= currentHealth; explosionOrigin = origin; explosionImpulse = impulse;
        TakeDamage(amount, origin, instigator);
        if (!isDead) explosiveDeath = false;
    }

    private void TriggerHitFlash()
    {
        if (meshRenderer == null) return;

        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
        }

        flashCoroutine = StartCoroutine(HitFlashRoutine());
    }

    private IEnumerator HitFlashRoutine()
    {
        Material[] mats = meshRenderer.sharedMaterials;

        for (int i = 0; i < mats.Length; i++)
        {
            mats[i].color = hitColor;
        }

        yield return new WaitForSeconds(flashDuration);

        for (int i = 0; i < mats.Length; i++)
        {
            if (i < originalColors.Length)
            {
                mats[i].color = originalColors[i];
            }
        }

        flashCoroutine = null;
    }

    private void PlayHitSound()
    {
        if (audioSource == null || hitSounds == null || hitSounds.Length == 0) return;

        AudioClip clip = hitSounds[UnityEngine.Random.Range(0, hitSounds.Length)];
        audioSource.pitch = UnityEngine.Random.Range(0.85f, 1.15f); // Variación de tono?
        audioSource.PlayOneShot(clip);
    }

    private void SpawnHitParticle(Vector3 position)
    {
        if (hitParticlePrefab == null) return;

        Vector3 spawnPos = position != default ? position : transform.position + Vector3.up;
        GameObject effect = Instantiate(hitParticlePrefab, spawnPos, Quaternion.identity);
        Destroy(effect, 1.5f);
    }

    private void Die(GameObject instigator)
    {
        if (isDead) return;
        isDead = true;
        // Sólo la fuente del golpe letal cobra; las bajas de depuración no generan dinero.
        var wallet = instigator != null ? instigator.GetComponentInParent<Survive.Economy.PlayerWallet>() : null;
        if (wallet != null)
        {
            int reward = TryGetComponent<BossBase>(out var boss) ? boss.DirectGoldReward : killCoins;
            if (reward > 0)
            {
                wallet.AddCoins(reward);
                var feedback = wallet.GetComponent<Survive.Economy.PlayerRewardFeedback>();
                if (feedback == null) feedback = wallet.gameObject.AddComponent<Survive.Economy.PlayerRewardFeedback>();
                feedback.Play(rewardSound);
            }
        }
        OnDeath?.Invoke();

        if (deathSound != null)
        {
            AudioSource.PlayClipAtPoint(deathSound, transform.position);
        }

        // Desactivar componentes para evitar bloqueos y acciones residuales
        if(TryGetComponent<NavMeshAgent>(out var agent)) agent.enabled = false;
        if (TryGetComponent<EnemyBaseController>(out var controller)) controller.enabled = false;
        if (TryGetComponent<Collider>(out var col)) col.enabled = false;

        // El Animator puede vivir en el modelo visual hijo del enemigo.
        Animator anim = GetComponentInChildren<Animator>();
        bool ragdollActive = false;
        if (explosiveDeath)
        {
            Survive.Combat.EnemyRagdoll ragdoll = GetComponent<Survive.Combat.EnemyRagdoll>();
            if (ragdoll == null) ragdoll = gameObject.AddComponent<Survive.Combat.EnemyRagdoll>();
            ragdollActive = ragdoll.TryActivate(explosionOrigin, explosionImpulse);
        }
        if (!ragdollActive && anim != null && anim.runtimeAnimatorController != null)
        {
            anim.ResetTrigger("Attack");
            anim.SetFloat("Speed", 0f);
            anim.SetTrigger("Die");
        }

        //  Destruir tras el retardo
        Destroy(gameObject, ragdollActive ? Mathf.Max(8, destroyDelay) : destroyDelay);

        if (GameStatsManager.Instance != null)
        {
            if (GetComponent<BossBase>() != null) GameStatsManager.Instance.AddBossKill();
            else if (bossMinion) GameStatsManager.Instance.AddMinionKill();
            else GameStatsManager.Instance.AddEnemyKill();
        }
    }
    public void ApplyWaveMultipliers(float healthMult)
    {
        maxHealth *= healthMult;
        currentHealth = maxHealth;
    }

}
