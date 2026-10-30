using System;
using UnityEngine;

namespace Survive.Economy
{
    [DisallowMultipleComponent]
    public sealed class PlayerWallet : MonoBehaviour
    {
        [SerializeField, Min(0)] private int startingCoins = 250;
        public int Coins { get; private set; }
        public event Action Changed;

        private void Awake() => Coins = Mathf.Max(0, startingCoins);

        public bool CanAfford(int price) => price >= 0 && Coins >= price;

        public bool TrySpend(int amount)
        {
            if (!CanAfford(amount)) return false;
            Coins -= amount;
            Changed?.Invoke();
            return true;
        }

        public void AddCoins(int amount)
        {
            if (amount <= 0) return;
            Coins = (int)Math.Min(int.MaxValue, (long)Coins + amount);
            Changed?.Invoke();
        }
    }
}
