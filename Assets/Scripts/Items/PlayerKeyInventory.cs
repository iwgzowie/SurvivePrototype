using System;
using UnityEngine;

namespace Survive.Items
{
    /// <summary>Llaves de esta instancia de jugador; una recogida equivale a una apertura lógica.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerHealth))]
    public sealed class PlayerKeyInventory : MonoBehaviour
    {
        private int keyCount;
        private PlayerHealth health;
        public int KeyCount => keyCount;
        public bool HasKey => keyCount > 0;
        public event Action Changed;
        private void Awake() => health = GetComponent<PlayerHealth>();
        private bool CanUse => isActiveAndEnabled && health != null && !health.IsDead && Time.timeScale > 0;
        public bool CanReceiveKey => CanUse && keyCount < int.MaxValue;
        public bool TryAddKey()
        {
            if (!CanReceiveKey) return false;
            keyCount++; Changed?.Invoke(); return true;
        }
        public bool TryConsumeKey()
        {
            if (!CanUse || keyCount <= 0) return false;
            keyCount--; Changed?.Invoke(); return true;
        }
        public void ResetKeys() { if (keyCount == 0) return; keyCount = 0; Changed?.Invoke(); }
        public static PlayerKeyInventory ForPlayer(GameObject player, bool create = false)
        {
            PlayerHealth health = player != null ? player.GetComponentInParent<PlayerHealth>() : null;
            if (health == null) return null;
            PlayerKeyInventory inventory = health.GetComponent<PlayerKeyInventory>();
            return inventory != null || !create ? inventory : health.gameObject.AddComponent<PlayerKeyInventory>();
        }
    }
}
