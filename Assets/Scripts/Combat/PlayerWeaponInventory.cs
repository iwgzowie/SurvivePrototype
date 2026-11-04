using System;
using Survive.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Survive.Combat
{
    /// <summary>Seis posiciones compartidas por armas y pilas de consumibles de curación.</summary>
    [DefaultExecutionOrder(1025)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerWeaponController))]
    [AddComponentMenu("Survive/Combate/Inventario de armas")]
    public sealed class PlayerWeaponInventory : MonoBehaviour
    {
        public const int Capacity = 6;

        [SerializeField] private PlayerWeaponController weaponController;
        [SerializeField] private Transform inventoryStorage;
        [SerializeField, Tooltip("Arma entregada una sola vez al iniciar esta instancia del jugador, en el primer espacio libre.")]
        private WeaponItem startingWeaponPrefab;
        [SerializeField] private InputAction[] slotActions = CreateSlotActions();
        [SerializeField] private InputAction scrollAction = new InputAction("WeaponSlotScroll", InputActionType.Value, "<Mouse>/scroll/y");
        [SerializeField] private InputAction previousSlotAction = new InputAction("PreviousWeaponSlot", InputActionType.Button, "<Gamepad>/dpad/left");
        [SerializeField] private InputAction nextSlotAction = new InputAction("NextWeaponSlot", InputActionType.Button, "<Gamepad>/dpad/right");

        private readonly WeaponItem[] slots = new WeaponItem[Capacity];
        private readonly HealingDefinition[] healing = new HealingDefinition[Capacity];
        private readonly int[] healingCounts = new int[Capacity];
        private InputAction useHealingAction, dropHealingAction;
        private int selectedIndex;
        private bool changingSelection;
        private WeaponItem startingWeaponToEquip;

        public event Action Changed;
        public int SlotCount => Capacity;
        public int SelectedIndex => selectedIndex;
        public Transform StorageRoot => inventoryStorage;
        public HealingDefinition GetHealing(int index) => index >= 0 && index < Capacity && healingCounts[index] > 0 ? healing[index] : null;
        public int GetHealingCount(int index) => GetHealing(index) != null ? healingCounts[index] : 0;
        public bool IsOccupied(int index) => GetWeapon(index) != null || GetHealing(index) != null;
        public bool CanManageInventory => isActiveAndEnabled && !changingSelection
            && weaponController != null && weaponController.CanChangeWeaponSelection;
        public bool HasFreeSlot
        {
            get
            {
                for (int index = 0; index < Capacity; index++)
                    if (!IsOccupied(index)) return true;
                return false;
            }
        }

        private static InputAction[] CreateSlotActions()
        {
            var actions = new InputAction[Capacity];
            for (int index = 0; index < Capacity; index++)
            {
                actions[index] = new InputAction($"WeaponSlot{index + 1}", InputActionType.Button);
                actions[index].AddBinding($"<Keyboard>/{index + 1}");
                actions[index].AddBinding($"<Keyboard>/numpad{index + 1}");
            }
            return actions;
        }

        private void Awake()
        {
            ResolveController();
            // Permite agregar el componente a un jugador que ya tenía un arma.
            WeaponItem current = weaponController != null ? weaponController.EquippedWeapon : null;
            if (current != null && current.IsOwnedBy(weaponController.WeaponOwnerRoot)) slots[0] = current;
        }

        private void Start()
        {
            ResolveController();
            if (startingWeaponPrefab == null || weaponController == null || !HasFreeSlot) return;
            // No repetir la entrega si otro inicializador ya dejó esa arma en el inventario.
            for (int index = 0; index < Capacity; index++)
            {
                WeaponItem current = GetWeapon(index);
                if (current != null && current.Definition == startingWeaponPrefab.Definition) return;
            }
            WeaponItem weapon = Instantiate(startingWeaponPrefab, transform.position, Quaternion.identity);
            if (TryStoreInFreeSlot(weapon)) startingWeaponToEquip = weapon;
            else { weapon.gameObject.SetActive(false); Destroy(weapon.gameObject); }
        }

        private void OnEnable()
        {
            ResolveController();
            if (slotActions == null || slotActions.Length != Capacity)
            {
                if (slotActions != null)
                    foreach (InputAction action in slotActions) action?.Dispose();
                slotActions = CreateSlotActions();
            }
            for (int index = 0; index < Capacity; index++)
            {
                if (slotActions[index] == null) slotActions[index] = CreateSlotActions()[index];
                // El layout nombra las teclas superiores "1".."6", no "digit1".
                // Corrige únicamente esa ruta antigua sin pisar bindings del usuario.
                for (int binding = 0; binding < slotActions[index].bindings.Count; binding++)
                    if (slotActions[index].bindings[binding].path == $"<Keyboard>/digit{index + 1}")
                        slotActions[index].ChangeBinding(binding).WithPath($"<Keyboard>/{index + 1}");
                slotActions[index].Enable();
            }
            scrollAction ??= new InputAction("WeaponSlotScroll", InputActionType.Value, "<Mouse>/scroll/y");
            previousSlotAction ??= new InputAction("PreviousWeaponSlot", InputActionType.Button, "<Gamepad>/dpad/left");
            nextSlotAction ??= new InputAction("NextWeaponSlot", InputActionType.Button, "<Gamepad>/dpad/right");
            scrollAction.Enable();
            previousSlotAction.Enable();
            nextSlotAction.Enable();
            useHealingAction ??= new InputAction("UseHealing", InputActionType.Button, "<Mouse>/leftButton");
            if (useHealingAction.bindings.Count == 1) useHealingAction.AddBinding("<Gamepad>/rightTrigger");
            dropHealingAction ??= new InputAction("DropHealing", InputActionType.Button, "<Keyboard>/g");
            useHealingAction.Enable(); dropHealingAction.Enable();
            Changed?.Invoke();
        }

        private void OnDisable()
        {
            if (slotActions != null)
                foreach (InputAction action in slotActions) action?.Disable();
            scrollAction?.Disable();
            previousSlotAction?.Disable();
            nextSlotAction?.Disable();
            useHealingAction?.Disable(); dropHealingAction?.Disable();
            Changed?.Invoke();
        }

        private void OnDestroy()
        {
            if (slotActions != null)
                foreach (InputAction action in slotActions) action?.Dispose();
            scrollAction?.Dispose();
            previousSlotAction?.Dispose();
            nextSlotAction?.Dispose();
            useHealingAction?.Dispose(); dropHealingAction?.Dispose();
        }

        private void LateUpdate()
        {
            // Los menús ya resolvieron pausa y cursor en Update. La pose y el
            // disparo reciben la nueva selección más adelante en LateUpdate.
            if (PruneInvalidSlots()) Changed?.Invoke();
            if (!CanManageInventory) return;
            if (startingWeaponToEquip != null)
            {
                // Esperar al cursor/control del juego permite iniciar también durante una pausa.
                int index = Array.IndexOf(slots, startingWeaponToEquip);
                if (weaponController.EquippedWeapon == null && index >= 0) TrySelect(index);
                startingWeaponToEquip = null;
            }
            for (int index = 0; index < Capacity; index++)
                if (slotActions[index].WasPressedThisFrame()) { TrySelect(index); return; }
            if (previousSlotAction.WasPressedThisFrame()) { TryCycle(-1); return; }
            if (nextSlotAction.WasPressedThisFrame()) { TryCycle(1); return; }
            if (GetHealing(selectedIndex) != null)
            {
                if (useHealingAction.WasPressedThisFrame()) TryUseSelectedHealing();
                if (dropHealingAction.WasPressedThisFrame()) TryDropSelected(transform.position + transform.forward * 1.2f, Quaternion.identity);
            }
            float scroll = scrollAction.ReadValue<float>();
            if (Mathf.Abs(scroll) > .01f) TryCycle(scroll > 0f ? -1 : 1);
        }

        private void ResolveController()
        {
            if (weaponController == null) weaponController = GetComponent<PlayerWeaponController>();
        }

        public WeaponItem GetWeapon(int index)
        {
            if (index < 0 || index >= Capacity || weaponController == null) return null;
            WeaponItem weapon = slots[index];
            return weapon != null && weapon.IsOwnedBy(weaponController.WeaponOwnerRoot) ? weapon : null;
        }

        /// <summary>Entrega del vendedor: guarda en un hueco libre sin reemplazar ni cambiar el arma en mano.</summary>
        public bool TryStorePurchase(WeaponItem weapon)
        {
            ResolveController();
            PlayerHealth health = GetComponent<PlayerHealth>();
            if (!isActiveAndEnabled || changingSelection || weaponController == null
                || !weaponController.isActiveAndEnabled || Time.timeScale <= 0f
                || health != null && health.IsDead || weapon == null || !weapon.isActiveAndEnabled
                || weapon.IsEquipped) return false;
            return TryStoreInFreeSlot(weapon);
        }

        private bool TryStoreInFreeSlot(WeaponItem weapon)
        {
            PruneInvalidSlots();
            int destination = -1;
            for (int index = 0; index < Capacity; index++)
                if (!IsOccupied(index)) { destination = index; break; }
            if (destination < 0 || !weapon.TryAttach(weaponController.WeaponOwnerRoot,
                    EnsureStorage(), Vector3.zero, Vector3.zero)) return false;
            slots[destination] = weapon;
            weapon.gameObject.SetActive(false);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Recoge en el primer lugar libre; con seis armas reemplaza la seleccionada.</summary>
        public bool TryCollect(WeaponItem weapon)
        {
            ResolveController();
            if (!CanManageInventory || weapon == null || !weapon.isActiveAndEnabled || weapon.IsEquipped) return false;
            PruneInvalidSlots();
            int destination = -1;
            for (int index = 0; index < Capacity; index++)
                if (!IsOccupied(index)) { destination = index; break; }
            bool replacing = destination < 0;
            if (replacing) destination = selectedIndex;
            // Para reemplazar un consumible se debe seleccionar otro espacio: nunca destruir curación por una recogida.
            if (replacing && GetHealing(destination) != null) return false;
            WeaponItem replaced = slots[destination];
            int previousIndex = selectedIndex;
            Vector3 pickupPosition = weapon.transform.position;
            Quaternion pickupRotation = weapon.transform.rotation;
            changingSelection = true;
            bool collected;
            try
            {
                slots[destination] = weapon;
                selectedIndex = destination;
                collected = weaponController.TrySetInventorySelection(weapon, EnsureStorage(), replacing,
                    pickupPosition, pickupRotation);
                if (!collected) { slots[destination] = replaced; selectedIndex = previousIndex; }
            }
            finally { changingSelection = false; }
            if (collected) Changed?.Invoke();
            return collected;
        }

        /// <summary>Los números también seleccionan lugares vacíos, dejando al jugador sin arma en mano.</summary>
        public bool TrySelect(int index)
        {
            ResolveController();
            if (!CanManageInventory || index < 0 || index >= Capacity) return false;
            PruneInvalidSlots();
            WeaponItem weapon = slots[index];
            if (index == selectedIndex && weaponController.EquippedWeapon == weapon) return true;
            int previousIndex = selectedIndex;
            changingSelection = true;
            bool selected;
            try
            {
                selectedIndex = index;
                selected = weaponController.TrySetInventorySelection(weapon, EnsureStorage(), false,
                    Vector3.zero, Quaternion.identity);
                if (!selected) selectedIndex = previousIndex;
            }
            finally { changingSelection = false; }
            if (selected) Changed?.Invoke();
            return selected;
        }

        /// <summary>Avanza o retrocede una posición ocupada, saltando los huecos sin compactar los slots.</summary>
        public bool TryCycle(int direction)
        {
            if (!CanManageInventory || direction == 0) return false;
            int step = direction > 0 ? 1 : -1;
            for (int offset = 1; offset <= Capacity; offset++)
            {
                int candidate = (selectedIndex + step * offset + Capacity * 2) % Capacity;
                if (!IsOccupied(candidate)) continue;
                return candidate != selectedIndex && TrySelect(candidate);
            }
            return false;
        }

        public bool TryDropSelected(Vector3 position, Quaternion rotation)
        {
            ResolveController();
            if (CanManageInventory && GetHealing(selectedIndex) != null)
            {
                HealingDefinition definition = GetHealing(selectedIndex);
                if (definition.worldPrefab == null) return false;
                HealingPickup pickup = Instantiate(definition.worldPrefab, position, rotation);
                pickup.Initialize(definition, healingCounts[selectedIndex]); pickup.PlaceInWorld(position, rotation);
                healing[selectedIndex] = null; healingCounts[selectedIndex] = 0; Changed?.Invoke(); return true;
            }
            if (!CanManageInventory || GetWeapon(selectedIndex) == null) return false;
            changingSelection = true;
            bool dropped;
            try
            {
                dropped = weaponController.TryDropInventorySelection(position, rotation);
                if (dropped) slots[selectedIndex] = null;
            }
            finally { changingSelection = false; }
            if (dropped) Changed?.Invoke();
            return dropped;
        }

        private Transform EnsureStorage()
        {
            Transform owner = weaponController.WeaponOwnerRoot;
            if (inventoryStorage != null && inventoryStorage.IsChildOf(owner)) return inventoryStorage;
            inventoryStorage = owner.Find("InventoryStorage");
            if (inventoryStorage == null)
            {
                inventoryStorage = new GameObject("InventoryStorage").transform;
                inventoryStorage.SetParent(owner, false);
            }
            return inventoryStorage;
        }

        public bool CanAddHealing(HealingDefinition definition, int count)
        {
            if (definition == null || count <= 0) return false;
            long capacity = 0;
            for (int i = 0; i < Capacity; i++)
                if (GetHealing(i) == definition) capacity += Mathf.Max(0, definition.stackLimit - healingCounts[i]);
                else if (!IsOccupied(i)) capacity += Mathf.Max(1, definition.stackLimit);
            return count <= capacity;
        }

        public bool TryAddHealing(HealingDefinition definition, int count)
        {
            if (!CanManageInventory || !CanAddHealing(definition, count)) return false;
            for (int pass = 0; pass < 2 && count > 0; pass++)
                for (int i = 0; i < Capacity && count > 0; i++)
                {
                    if (pass == 0 ? GetHealing(i) != definition : IsOccupied(i)) continue;
                    int added = Mathf.Min(count, Mathf.Max(1, definition.stackLimit) - healingCounts[i]);
                    healing[i] = definition; healingCounts[i] += added; count -= added;
                }
            Changed?.Invoke(); return true;
        }

        public bool TryUseSelectedHealing()
        {
            HealingDefinition definition = GetHealing(selectedIndex); PlayerHealth health = GetComponent<PlayerHealth>();
            if (!CanManageInventory || definition == null || health == null || health.IsDead
                || health.CurrentHealth >= health.MaxHealth || definition.healing <= 0) return false;
            health.Heal(definition.healing);
            if (--healingCounts[selectedIndex] <= 0) healing[selectedIndex] = null;
            Changed?.Invoke(); return true;
        }

        private bool PruneInvalidSlots()
        {
            bool changed = false;
            for (int index = 0; index < Capacity; index++)
            {
                if (ReferenceEquals(slots[index], null) || GetWeapon(index) != null) continue;
                slots[index] = null;
                changed = true;
            }
            return changed;
        }
    }
}
