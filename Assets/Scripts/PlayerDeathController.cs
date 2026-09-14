using Survive.Combat;
using UPP.Runtime.Aiming;
using UPP.Runtime.Character;
using UPP.ThirdPersonController;
using UnityEngine;

/// <summary>Aplica la derrota del jugador sin agregar reglas de salud al controlador UPP.</summary>
[DefaultExecutionOrder(1250)]
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerHealth))]
public sealed class PlayerDeathController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private UPPCharacterMovementComponent movement;
    [SerializeField] private PlayerWeaponController weaponController;
    [SerializeField] private Rigidbody playerBody;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private MenuBehaviour menu;

    [Header("Otros controles")]
    [SerializeField, Tooltip("Interacciones u otros controles adicionales que deben detenerse al morir.")]
    private Behaviour[] behavioursToDisable = System.Array.Empty<Behaviour>();

    public bool DeathHandled { get; private set; }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        playerHealth.OnDeath += HandleDeath;
        if (playerHealth.IsDead)
        {
            HandleDeath();
        }
    }

    private void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnDeath -= HandleDeath;
        }
    }

    private void HandleDeath()
    {
        if (DeathHandled)
        {
            return;
        }
        DeathHandled = true;
        ResolveReferences();

        // Deshabilitar el receptor también impide equipar armas mediante la interacción.
        if (weaponController != null)
        {
            weaponController.enabled = false;
        }
        if (movement != null)
        {
            movement.DisableLocomotion();
            movement.StopAllCoroutines();
            movement.CancelInvoke();
            movement.enabled = false;

            if (movement.MyPivotCamera != null)
            {
                // La Camera y el AudioListener siguen dibujando la escena detrás del panel.
                movement.MyPivotCamera.enabled = false;
                foreach (UPPDebugCrosshair crosshair in movement.MyPivotCamera.GetComponentsInChildren<UPPDebugCrosshair>(true))
                {
                    crosshair.enabled = false;
                }
            }
        }

        DisableComponent<UPPPlayerIntegration>();
        DisableComponent<UPPAnimatorControllerComponent>();
        DisableComponent<UPPIKCharacterComponent>();
        DisableComponent<PlayerController>();
        foreach (Behaviour behaviour in behavioursToDisable)
        {
            if (behaviour != null && behaviour != this && behaviour != playerHealth && behaviour != menu)
            {
                behaviour.enabled = false;
            }
        }

        if (playerAnimator != null)
        {
            playerAnimator.applyRootMotion = false;
            playerAnimator.enabled = false;
        }
        if (playerBody != null)
        {
            if (!playerBody.isKinematic)
            {
                playerBody.linearVelocity = Vector3.zero;
                playerBody.angularVelocity = Vector3.zero;
            }
            playerBody.isKinematic = true;
        }

        if (menu != null)
        {
            menu.ShowGameOver();
        }
        else
        {
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Debug.LogWarning("El jugador murió, pero falta asignar el menú de derrota en la escena.", this);
        }
    }

    private void ResolveReferences()
    {
        playerHealth ??= GetComponent<PlayerHealth>();
        movement ??= GetComponent<UPPCharacterMovementComponent>();
        weaponController ??= GetComponent<PlayerWeaponController>();
        playerBody ??= GetComponent<Rigidbody>();
        playerAnimator ??= GetComponent<Animator>();
        if (menu == null)
        {
            foreach (MenuBehaviour candidate in FindObjectsByType<MenuBehaviour>(FindObjectsSortMode.None))
            {
                if (!candidate.IsMainMenu && candidate.gameObject.scene == gameObject.scene)
                {
                    menu = candidate;
                    break;
                }
            }
        }
    }

    private void DisableComponent<T>() where T : Behaviour
    {
        if (TryGetComponent<T>(out T component))
        {
            component.enabled = false;
        }
    }
}
