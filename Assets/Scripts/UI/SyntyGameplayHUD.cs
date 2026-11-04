using System;
using System.Collections.Generic;
using Survive.Combat;
using Survive.Economy;
using Survive.Items;
using Survive.Turrets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Survive.UI
{
    [DefaultExecutionOrder(1400), DisallowMultipleComponent]
    public sealed class SyntyGameplayHUD : MonoBehaviour
    {
        [Serializable] public sealed class Slot { public GameObject selected; public Image icon; public TMP_Text ammo; }
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Slider healthBar, reloadBar, bossBar;
        [SerializeField] private Image healthFill, weaponIcon;
        [SerializeField] private TMP_Text healthText, coinsText, turretText, keyText, waveText, enemiesText;
        [SerializeField] private TMP_Text phaseText, scoreText, timeText, weaponName, ammoText, reserveText, weaponStatus;
        [SerializeField] private TMP_Text promptText, bossName;
        [SerializeField] private GameObject promptRoot, bossRoot;
        [SerializeField] private Slot[] slots;
        private PlayerWeaponController weapons;
        private PlayerWeaponInventory inventory;
        private PlayerHealth health;
        private PlayerWallet wallet;
        private PlayerTurretController turrets;
        private PlayerVendorController vendor;
        private PlayerInteractionController interaction;
        private EnemySpawner spawner;
        private BossSpawner bossSpawner;
        private EnemyHealth boss;
        private float nextBind, nextCounters, messageUntil;
        private string waveMessage;
        private readonly Dictionary<Canvas, bool> previousCanvases = new Dictionary<Canvas, bool>();
        public bool IsBound => weapons != null && health != null;
        public PlayerHealth BoundHealth => health;
        public PlayerWallet BoundWallet => wallet;
        public PlayerWeaponInventory BoundInventory => inventory;
        private void Start() => Bind();
        private void OnDisable() => Unbind();
        private void OnEnable() { nextBind = 0; }

        private void LateUpdate()
        {
            if (!IsBound && Time.unscaledTime >= nextBind) { nextBind = Time.unscaledTime + .5f; Bind(); }
            if (!IsBound) return;
            canvasGroup.alpha = !health.IsDead && !(vendor != null && vendor.IsOpen) ? 1 : 0;
            if (Time.unscaledTime >= nextCounters)
            {
                nextCounters = Time.unscaledTime + .1f;
                RefreshWave(); RefreshEconomy(); RefreshInventory();
                GameStatsManager stats = GameStatsManager.Instance;
                Set(scoreText, "PUNTAJE  " + (stats != null ? stats.totalScore.ToString("N0") : "0"));
                int elapsed = stats != null ? Mathf.FloorToInt(stats.survivalTime) : 0;
                Set(timeText, $"{elapsed / 60:00}:{elapsed % 60:00}");
            }
            string prompt = turrets != null && turrets.IsPlacing ? turrets.PlacementMessage : interaction != null ? interaction.CurrentPrompt : "";
            promptRoot.SetActive(!string.IsNullOrEmpty(prompt)); Set(promptText, prompt);
            WeaponItem weapon = weapons.EquippedWeapon;
            reloadBar.gameObject.SetActive(weapon != null && weapon.IsReloading);
            if (weapon != null && weapon.IsReloading) reloadBar.SetValueWithoutNotify(weapon.ReloadProgress);
            bool bossAlive = boss != null && !boss.IsDead;
            bossRoot.SetActive(bossAlive);
            if (bossAlive) bossBar.SetValueWithoutNotify(boss.CurrentHealth / Mathf.Max(1, boss.MaxHealth));
        }

        public void Bind()
        {
            if (IsBound) return;
            foreach (PlayerWeaponController candidate in FindObjectsByType<PlayerWeaponController>(FindObjectsSortMode.None))
                if (candidate.gameObject.scene == gameObject.scene && candidate.GetComponent<PlayerHealth>() != null) { weapons = candidate; break; }
            if (weapons == null) return;
            health = weapons.GetComponent<PlayerHealth>(); inventory = weapons.GetComponent<PlayerWeaponInventory>();
            wallet = weapons.GetComponent<PlayerWallet>(); turrets = weapons.GetComponent<PlayerTurretController>();
            vendor = weapons.GetComponent<PlayerVendorController>(); interaction = weapons.GetComponent<PlayerInteractionController>();
            health.OnHealthChanged += OnHealth;
            if (wallet != null) wallet.Changed += RefreshEconomy;
            if (turrets != null) turrets.Changed += RefreshEconomy;
            if (inventory != null) inventory.Changed += RefreshInventory;
            weapons.WeaponChanged += OnWeapon; weapons.WeaponStateChanged += OnWeapon;
            if (vendor != null) vendor.ExternalGameplayHud = true;
            foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (canvas.gameObject.scene == gameObject.scene && (canvas.name == "PlayerInteractionHUD" || canvas.name == "WaveHUD"))
                { previousCanvases[canvas] = canvas.enabled; canvas.enabled = false; }
            foreach (EnemySpawner candidate in FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None))
                if (candidate.gameObject.scene == gameObject.scene) { spawner = candidate; break; }
            if (spawner != null)
            {
                spawner.OnWaveStarted += OnWave; spawner.OnWaveCompleted += OnWaveComplete;
                spawner.OnBossWaveStarted += OnBossWave; spawner.OnRestStarted += OnRest; spawner.OnRestFinished += OnRestFinished;
                bossSpawner = spawner.GetComponent<BossSpawner>();
                if (bossSpawner != null) bossSpawner.OnBossSpawned += OnBoss;
            }
            foreach (BossBase existing in FindObjectsByType<BossBase>(FindObjectsSortMode.None))
                if (existing.gameObject.scene == gameObject.scene && existing.Health != null) OnBoss(existing.Health);
            OnHealth(health.CurrentHealth, health.MaxHealth); RefreshEconomy(); RefreshInventory(); RefreshWave();
        }

        private void Unbind()
        {
            if (health != null) health.OnHealthChanged -= OnHealth;
            if (wallet != null) wallet.Changed -= RefreshEconomy;
            if (turrets != null) turrets.Changed -= RefreshEconomy;
            if (inventory != null) inventory.Changed -= RefreshInventory;
            if (weapons != null) { weapons.WeaponChanged -= OnWeapon; weapons.WeaponStateChanged -= OnWeapon; }
            if (vendor != null) vendor.ExternalGameplayHud = false;
            if (spawner != null)
            {
                spawner.OnWaveStarted -= OnWave; spawner.OnWaveCompleted -= OnWaveComplete;
                spawner.OnBossWaveStarted -= OnBossWave; spawner.OnRestStarted -= OnRest; spawner.OnRestFinished -= OnRestFinished;
            }
            if (bossSpawner != null) bossSpawner.OnBossSpawned -= OnBoss;
            foreach (var previous in previousCanvases) if (previous.Key != null) previous.Key.enabled = previous.Value;
            previousCanvases.Clear(); weapons = null; health = null; boss = null;
        }

        private void OnHealth(float current, float maximum)
        {
            float fraction = current / Mathf.Max(1, maximum); healthBar.SetValueWithoutNotify(fraction);
            healthFill.color = fraction <= .25f ? new Color(1, .25f, .22f) : fraction <= .5f ? new Color(1, .65f, .22f) : new Color(.45f, .86f, .63f);
            Set(healthText, $"{Mathf.CeilToInt(current)} <size=65%>/ {Mathf.CeilToInt(maximum)}</size>");
        }
        private void RefreshEconomy()
        {
            Set(coinsText, wallet != null ? wallet.Coins.ToString("N0") : "0");
            Set(turretText, turrets != null ? $"{turrets.Count} / {turrets.Capacity}" : "0 / 0");
            PlayerKeyInventory keys = weapons != null ? PlayerKeyInventory.ForPlayer(weapons.gameObject) : null;
            int count = keys != null ? keys.KeyCount : 0;
            Set(keyText, count == 0 ? "SIN LLAVE" : count == 1 ? "LLAVE DISPONIBLE" : "LLAVES  " + count);
            PlayerDoorGuide guide = weapons != null ? weapons.GetComponent<PlayerDoorGuide>() : null;
            if (guide != null && guide.Blueprints > 0)
                Set(keyText, guide.Target != null ? $"{count} {(count == 1 ? "LLAVE" : "LLAVES")} · {guide.Target.name.Replace("Puerta", "").ToUpperInvariant()}" : count == 0 ? "PLANO · NECESITÁS LLAVE" : "PLANO DISPONIBLE");
            keyText.color = count > 0 ? new Color(.45f, .9f, 1) : new Color(.56f, .65f, .7f);
        }
        private void OnWeapon(WeaponItem ignored) => RefreshInventory();
        private static Sprite Icon(WeaponItem weapon) => weapon != null && weapon.Definition != null ? weapon.Definition.Icon : null;
        private void RefreshInventory()
        {
            if (weapons == null) return;
            WeaponItem current = weapons.EquippedWeapon;
            HealingDefinition selectedHealing = inventory != null ? inventory.GetHealing(inventory.SelectedIndex) : null;
            bool explosive = current != null && current.LoadedExplosiveAmmo != null;
            Set(weaponName, selectedHealing != null ? selectedHealing.displayName.ToUpperInvariant() : current != null ? current.DisplayName.ToUpperInvariant() : "SIN ARMA");
            Set(ammoText, selectedHealing != null ? "x" + inventory.GetHealingCount(inventory.SelectedIndex) : current != null ? current.AmmoInMagazine.ToString("00") : "--");
            Set(reserveText, selectedHealing != null ? "+" + selectedHealing.healing.ToString("0") + " PV" : current != null ? "/ " + current.ReserveAmmo : "/ --");
            Set(weaponStatus, selectedHealing != null ? health.CurrentHealth >= health.MaxHealth ? "VIDA COMPLETA · G SOLTAR" : "CLIC USAR · G SOLTAR"
                : current == null ? "1–6 ELEGIR · E RECOGER" : current.IsReloading ? "RECARGANDO…"
                : explosive ? "MUNICIÓN EXPLOSIVA · G SOLTAR" : !current.HasAmmo ? "R RECARGAR · SIN MUNICIÓN" : "R RECARGAR · G SOLTAR");
            ammoText.color = explosive ? new Color(1, .7f, .25f) : Color.white;
            Sprite currentIcon = selectedHealing != null ? selectedHealing.icon : Icon(current); weaponIcon.sprite = currentIcon; weaponIcon.enabled = currentIcon != null;
            for (int i = 0; i < slots.Length; i++)
            {
                WeaponItem item = inventory != null ? inventory.GetWeapon(i) : null;
                HealingDefinition healing = inventory != null ? inventory.GetHealing(i) : null;
                slots[i].selected.SetActive(inventory != null && inventory.SelectedIndex == i);
                Sprite icon = healing != null ? healing.icon : Icon(item); slots[i].icon.sprite = icon; slots[i].icon.enabled = icon != null;
                Set(slots[i].ammo, healing != null ? "x" + inventory.GetHealingCount(i) : item != null ? item.IsReloading ? "RECARGA"
                    : item.LoadedExplosiveAmmo != null ? item.AmmoInMagazine + " EXP" : item.AmmoInMagazine + " / " + item.ReserveAmmo : "VACÍO");
            }
        }
        private void RefreshWave()
        {
            Set(waveText, "OLEADA " + (spawner != null ? Mathf.Max(1, spawner.CurrentWaveNumber).ToString("00") : "--"));
            Set(enemiesText, "ENEMIGOS  " + (spawner != null ? spawner.AliveEnemyCount : 0));
            string phase = "PREPARATE PARA SOBREVIVIR";
            if (spawner != null)
            {
                if (spawner.State == WaveRunState.Resting) { int seconds = Mathf.CeilToInt(spawner.RestTimeRemaining); phase = $"DESCANSO  {seconds / 60:00}:{seconds % 60:00}"; }
                else if (spawner.IsRunning) phase = spawner.WavesUntilBoss > 0 ? $"JEFE EN {spawner.WavesUntilBoss} OLEADAS" : "JEFE EN CAMINO";
                else if (spawner.State == WaveRunState.Failed) phase = "OLEADA DETENIDA";
            }
            Set(phaseText, Time.unscaledTime < messageUntil ? waveMessage : phase);
        }
        private void Message(string message) { waveMessage = message; messageUntil = Time.unscaledTime + 3; RefreshWave(); }
        private void OnWave(int index, EnemyWave wave) => RefreshWave();
        private void OnWaveComplete(int index) => Message("OLEADA DESPEJADA");
        private void OnBossWave() => Message("¡ALERTA DE JEFE!");
        private void OnRest() => Message("JEFE DERROTADO · DESCANSO");
        private void OnRestFinished() => Message("PREPARATE PARA LA SIGUIENTE OLEADA");
        private void OnBoss(EnemyHealth target) { boss = target; BossBase data = target.GetComponent<BossBase>(); Set(bossName, data != null ? data.BossName : target.name.Replace("(Clone)", "").ToUpperInvariant()); }
        private static void Set(TMP_Text label, string value) { if (label != null && label.text != value) label.text = value; }
    }
}
