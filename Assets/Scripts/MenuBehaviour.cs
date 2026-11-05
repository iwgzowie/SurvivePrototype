using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Survive.Character;
using System.Collections;

/// <summary>
/// Coordina navegación, pausa y fin de partida
/// sin depender de un personaje concreto.
/// </summary>
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
    [SerializeField] private string gameplayScene = "Level 2";
    [SerializeField] private string mainMenuScene = "SceneAlejandro";

    [Header("Agente del escenario")]
    [SerializeField] private SceneCharacterPreview agentPreview;
    [SerializeField] private CameraSwitch agentCameraSwitch;

    [SerializeField] private Survive.UI.SyntyResultsUI resultsUi;

    // NUEVO: referencia al sistema de snapshots.
    [Header("Audio de pausa")]
    [SerializeField]
    private PauseSnapshotAudio pauseSnapshotAudio;

    private bool sceneLoading;
    private bool waitForPointerRelease;
    private float timeScaleBeforePause = 1f;
    private CharacterCreationMenu characterCreation;

    public bool IsPaused { get; private set; }
    public bool IsGameOver { get; private set; }
    public bool IsVictory { get; private set; }
    public bool IsFinished => IsGameOver || IsVictory;
    public bool IsMainMenu => isMainMenu;

    public bool IsCreatingCharacter =>
        characterCreation != null &&
        characterCreation.IsOpen;

    private void Start()
    {
        // NUEVO: encontrar el sistema de audio si no fue asignado.
        if (pauseSnapshotAudio == null)
        {
            pauseSnapshotAudio =
                FindFirstObjectByType<PauseSnapshotAudio>();
        }

        // Un menú principal no necesita un contenedor de pausa.
        SetPanel(container, IsPaused);
        SetPanel(gameOverContainer, IsGameOver);
        SetPanel(victoryContainer, IsVictory);

        Time.timeScale = IsPaused || IsFinished ? 0f : 1f;

        ApplyCursor();
    }

    private void Update()
    {
        if (sceneLoading) return;

        bool wantsPause =
            Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame;

        wantsPause |=
            Gamepad.current != null &&
            Gamepad.current.startButton.wasPressedThisFrame;

        if (resultsUi != null && resultsUi.IsLeaderboardOpen)
        {
            if (wantsPause)
                resultsUi.CloseLeaderboard();

            return;
        }

        if (isMainMenu || IsFinished || container == null)
            return;

        if (wantsPause)
        {
            foreach (
                Survive.Economy.PlayerVendorController shop
                in FindObjectsByType<
                    Survive.Economy.PlayerVendorController>(
                    FindObjectsSortMode.None))
            {
                if (shop.IsOpen)
                {
                    shop.Close();
                    return;
                }
            }

            foreach (
                Survive.Turrets.PlayerTurretController builder
                in FindObjectsByType<
                    Survive.Turrets.PlayerTurretController>(
                    FindObjectsSortMode.None))
            {
                if (builder.IsPlacing)
                {
                    builder.CancelPlacement();
                    return;
                }
            }

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
            // El clic que cierra el menú
            // no debe convertirse también en un disparo.
            if (Mouse.current != null &&
                Mouse.current.leftButton.isPressed)
            {
                return;
            }

            waitForPointerRelease = false;
            ApplyCursor();
        }

        if (isMainMenu || IsPaused || IsFinished)
        {
            // El rig UPP puede crearse en Awake/Start:
            // el menú conserva el cursor libre después.
            ApplyCursor();
        }
    }

    private void OnDisable()
    {
        if (characterCreation != null)
            characterCreation.Close();

        if (IsPaused || IsFinished)
        {
            Time.timeScale = 1f;

            // NUEVO: restaurar el sonido normal.
            pauseSnapshotAudio?.OnGameResumed();
        }
    }

    public void StartGame()
    {
        if (sceneLoading || IsCreatingCharacter)
            return;

        if (isMainMenu)
        {
            SidekickCharacterCatalog catalog =
                SidekickCharacterCatalog.Load();

            if (catalog == null)
            {
                Debug.LogError(
                    "Falta el catálogo Sidekick. Ejecutá Survive/Character/Crear e integrar catálogo Sidekick en el Editor.",
                    this);

                return;
            }

            if (!CharacterProfileStore.TryLoad(
                    out CharacterProfile profile) ||
                !catalog.Validate(profile, out _))
            {
                OpenCharacterCreator(catalog, true);
                return;
            }
        }

        ChangeScene(gameplayScene);
    }

    public void OpenAgentCreator()
    {
        if (!isMainMenu ||
            sceneLoading ||
            IsCreatingCharacter)
        {
            return;
        }

        SidekickCharacterCatalog catalog =
            SidekickCharacterCatalog.Load();

        if (catalog == null)
        {
            Debug.LogError(
                "Falta el catálogo Sidekick del agente.",
                this);

            return;
        }

        OpenCharacterCreator(catalog, false);
    }

    private void OpenCharacterCreator(
        SidekickCharacterCatalog catalog,
        bool playAfterSave)
    {
        agentCameraSwitch?.FocusAgent(
            agentPreview != null
                ? agentPreview.transform
                : null);

        characterCreation = CharacterCreationMenu.Open(
            this,
            catalog,
            profile => SaveCharacter(profile, playAfterSave),
            agentPreview,
            () =>
            {
                if (agentCameraSwitch != null)
                    agentCameraSwitch.ShowMainMenu();
            },
            playAfterSave
                ? "CREAR Y JUGAR"
                : "GUARDAR AGENTE");
    }

    private string SaveCharacter(
        CharacterProfile profile,
        bool playAfterSave)
    {
        SidekickCharacterCatalog catalog =
            SidekickCharacterCatalog.Load();

        if (catalog == null)
            return "No se pudo cargar el catálogo de personajes.";

        if (!catalog.Validate(profile, out string error))
            return error;

        if (!CharacterProfileStore.TrySave(
                profile, out error))
        {
            return error;
        }

        if (playAfterSave)
            StartCoroutine(EnterGameAfterCreation());

        return null;
    }

    private IEnumerator EnterGameAfterCreation()
    {
        // La UI libera el estudio 3D y restaura
        // las cámaras del menú antes de cargar la partida.
        yield return null;

        ChangeScene(gameplayScene);
    }

    public void PauseGame()
    {
        if (sceneLoading ||
            isMainMenu ||
            IsFinished ||
            IsPaused ||
            container == null)
        {
            return;
        }

        timeScaleBeforePause =
            Time.timeScale > 0f
                ? Time.timeScale
                : 1f;

        IsPaused = true;
        Time.timeScale = 0f;

        // NUEVO: activar snapshot Pause.
        pauseSnapshotAudio?.OnGamePaused();

        SetPanel(container, true);
        ApplyCursor();
    }

    public void ResumeGame()
    {
        if (sceneLoading ||
            isMainMenu ||
            IsFinished ||
            !IsPaused)
        {
            return;
        }

        IsPaused = false;
        SetPanel(container, false);

        Time.timeScale = timeScaleBeforePause;

        // NUEVO: volver al snapshot Gameplay.
        pauseSnapshotAudio?.OnGameResumed();

        waitForPointerRelease = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ShowGameOver()
    {
        if (sceneLoading ||
            isMainMenu ||
            IsFinished)
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

        resultsUi?.ShowDefeat();
    }

    /// <summary>
    /// Cierra la partida al completar el objetivo,
    /// sin reemplazar una derrota previa.
    /// </summary>
    public void ShowVictory()
    {
        if (sceneLoading ||
            isMainMenu ||
            IsFinished)
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
            return;

        if (string.IsNullOrWhiteSpace(sceneName) ||
            !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning(
                $"No se puede cargar la escena '{sceneName}'. Revisá la referencia y la lista de escenas de Build Settings.",
                this);

            return;
        }

        GameStatsManager.Instance?.FinalizeGameStats();

        sceneLoading = true;

        // NUEVO: restaurar snapshot Gameplay
        // antes de abandonar la escena.
        pauseSnapshotAudio?.OnGameResumed();

        // La nueva escena siempre comienza fuera
        // de la pausa o del final de partida anterior.
        Time.timeScale = 1f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SceneManager.LoadScene(sceneName);
    }

    public void ExitGame()
    {
        // NUEVO: restaurar el audio antes de salir.
        pauseSnapshotAudio?.OnGameResumed();

        Time.timeScale = 1f;
        Application.Quit();
    }

    private void ApplyCursor()
    {
        bool showCursor =
            isMainMenu ||
            IsPaused ||
            IsFinished ||
            sceneLoading;

        Cursor.lockState = showCursor
            ? CursorLockMode.None
            : CursorLockMode.Locked;

        Cursor.visible = showCursor;
    }

    private static void SetPanel(
        GameObject panel,
        bool active)
    {
        if (panel != null)
        {
            panel.SetActive(active);
        }
    }
}
