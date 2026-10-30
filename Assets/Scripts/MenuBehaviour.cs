using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Survive.Character;
using System.Collections;

/// <summary>Coordina navegación, pausa y fin de partida sin depender de un personaje concreto.</summary>
[DefaultExecutionOrder(1200)]
[DisallowMultipleComponent]
public class MenuBehaviour : MonoBehaviour
{
    [Header("Paneles")]
    [Tooltip("Panel de pausa. Se conserva el nombre para mantener las referencias existentes.")]
    public GameObject container;
    public GameObject gameOverContainer;
    public GameObject victoryContainer;
    [SerializeField] private bool isMainMenu;

    [Header("Escenas")]
    [SerializeField] private string gameplayScene = "Level 1";
    [SerializeField] private string mainMenuScene = "SceneAlejandro";

    private bool sceneLoading;
    private bool waitForPointerRelease;
    private float timeScaleBeforePause = 1f;
    private CharacterCreationMenu characterCreation;

    public bool IsPaused { get; private set; }
    public bool IsGameOver { get; private set; }
    public bool IsVictory { get; private set; }
    public bool IsFinished => IsGameOver || IsVictory;
    public bool IsMainMenu => isMainMenu;
    public bool IsCreatingCharacter => characterCreation != null && characterCreation.IsOpen;

    private void Start()
    {
        // Un menú principal no necesita un contenedor de pausa.
        SetPanel(container, IsPaused);
        SetPanel(gameOverContainer, IsGameOver);
        SetPanel(victoryContainer, IsVictory);
        Time.timeScale = IsPaused || IsFinished ? 0f : 1f;
        ApplyCursor();
    }

    private void Update()
    {
        if (sceneLoading || isMainMenu || IsFinished || container == null)
        {
            return;
        }

        bool wantsPause = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        wantsPause |= Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame;
        if (wantsPause)
        {
            foreach (Survive.Economy.PlayerVendorController shop in FindObjectsByType<Survive.Economy.PlayerVendorController>(FindObjectsSortMode.None))
                if (shop.IsOpen) { shop.Close(); return; }
            foreach (Survive.Turrets.PlayerTurretController builder in FindObjectsByType<Survive.Turrets.PlayerTurretController>(FindObjectsSortMode.None))
                if (builder.IsPlacing) { builder.CancelPlacement(); return; }
            if (IsPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    private void LateUpdate()
    {
        if (waitForPointerRelease)
        {
            // El clic que cierra el menú no debe convertirse también en un disparo.
            if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                return;
            }
            waitForPointerRelease = false;
            ApplyCursor();
        }
        if (isMainMenu || IsPaused || IsFinished)
        {
            // El rig UPP puede crearse en Awake/Start: el menú conserva el cursor libre después.
            ApplyCursor();
        }
    }

    private void OnDisable()
    {
        if (characterCreation != null) characterCreation.Close();
        if (IsPaused || IsFinished)
        {
            Time.timeScale = 1f;
        }
    }

    public void StartGame()
    {
        if (sceneLoading || IsCreatingCharacter) return;
        if (isMainMenu)
        {
            SidekickCharacterCatalog catalog = SidekickCharacterCatalog.Load();
            if (catalog == null)
            {
                Debug.LogError("Falta el catálogo Sidekick. Ejecutá Survive/Character/Crear e integrar catálogo Sidekick en el Editor.", this);
                return;
            }
            if (!CharacterProfileStore.TryLoad(out CharacterProfile profile) || !catalog.Validate(profile, out _))
            {
                characterCreation = CharacterCreationMenu.Open(this, catalog, ConfirmCharacter);
                return;
            }
        }
        ChangeScene(gameplayScene);
    }

    private string ConfirmCharacter(CharacterProfile profile)
    {
        SidekickCharacterCatalog catalog = SidekickCharacterCatalog.Load();
        if (catalog == null) return "No se pudo cargar el catálogo de personajes.";
        if (!catalog.Validate(profile, out string error)) return error;
        if (!CharacterProfileStore.TrySave(profile, out error)) return error;
        StartCoroutine(EnterGameAfterCreation());
        return null;
    }

    private IEnumerator EnterGameAfterCreation()
    {
        // La UI libera el estudio 3D y restaura las cámaras del menú antes de cargar la partida.
        yield return null;
        ChangeScene(gameplayScene);
    }

    public void PauseGame()
    {
        if (sceneLoading || isMainMenu || IsFinished || IsPaused || container == null)
        {
            return;
        }

        timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : 1f;
        IsPaused = true;
        Time.timeScale = 0f;
        SetPanel(container, true);
        ApplyCursor();
    }

    public void ResumeGame()
    {
        if (sceneLoading || isMainMenu || IsFinished || !IsPaused)
        {
            return;
        }

        IsPaused = false;
        SetPanel(container, false);
        Time.timeScale = timeScaleBeforePause;
        waitForPointerRelease = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ShowGameOver()
    {
        if (sceneLoading || isMainMenu || IsFinished)
        {
            return;
        }

        IsGameOver = true;
        IsPaused = false;
        waitForPointerRelease = false;
        SetPanel(container, false);
        SetPanel(victoryContainer, false);
        SetPanel(gameOverContainer, true);
        Time.timeScale = 0f;
        ApplyCursor();
    }

    /// <summary>Cierra la partida al completar el objetivo, sin reemplazar una derrota previa.</summary>
    public void ShowVictory()
    {
        if (sceneLoading || isMainMenu || IsFinished)
        {
            return;
        }

        IsVictory = true;
        IsPaused = false;
        waitForPointerRelease = false;
        SetPanel(container, false);
        SetPanel(gameOverContainer, false);
        SetPanel(victoryContainer, true);
        Time.timeScale = 0f;
        ApplyCursor();
    }

    public void ReloadCurrentScene()
    {
        ChangeScene(SceneManager.GetActiveScene().path);
    }

    public void BackToMainMenu()
    {
        ChangeScene(mainMenuScene);
    }

    public void ChangeScene(string sceneName)
    {
        if (sceneLoading)
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning($"No se puede cargar la escena '{sceneName}'. Revisá la referencia y la lista de escenas de Build Settings.", this);
            return;
        }

        sceneLoading = true;
        // La nueva escena siempre comienza fuera de la pausa o del final de partida anterior.
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene(sceneName);
    }

    public void ExitGame()
    {
        Time.timeScale = 1f;
        Application.Quit();
    }

    private void ApplyCursor()
    {
        bool showCursor = isMainMenu || IsPaused || IsFinished || sceneLoading;
        Cursor.lockState = showCursor ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = showCursor;
    }

    private static void SetPanel(GameObject panel, bool active)
    {
        if (panel != null)
        {
            panel.SetActive(active);
        }
    }
}
