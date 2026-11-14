using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Esc first reaches the world tools so they can cancel placement and targeting.
[DefaultExecutionOrder(100)]
public sealed class PauseMenu : MonoBehaviour
{
    private const string MainMenuScene = "Assets/Scenes/LordsMenu.unity";
    private static PauseMenu instance;
    private static int closedFrame = -1;

    public static bool BlocksWorldInput => (instance != null && instance.paused) || closedFrame == Time.frameCount;

    [Header("Пауза")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private SettingsMenu settingsMenu;

    [Header("Подтверждение выхода")]
    [SerializeField] private GameObject exitPanel;
    [SerializeField] private Button saveAndExitButton;
    [SerializeField] private Button exitWithoutSavingButton;
    [SerializeField] private Button cancelExitButton;
    [SerializeField] private TMP_Text exitStatus;
    private bool paused;
    private bool showingSettings;
    private bool leaving;
    private float resumeTimeScale;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        closedFrame = -1;
    }

    private void OnEnable()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("В игровой сцене нужен один компонент PauseMenu.", this);
            enabled = false;
            return;
        }
        if (pausePanel == null || transform.IsChildOf(pausePanel.transform) ||
            (exitPanel != null && (transform.IsChildOf(exitPanel.transform) ||
             exitPanel.transform.IsChildOf(pausePanel.transform) ||
             pausePanel.transform.IsChildOf(exitPanel.transform))))
        {
            Debug.LogWarning("PauseMenu: назначь Pause Panel, а контроллер и панели размести на отдельных объектах.", this);
            enabled = false;
            return;
        }

        instance = this;
        pausePanel.SetActive(false);
        if (exitPanel != null) exitPanel.SetActive(false);
        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (settingsButton != null)
        {
            settingsButton.interactable = settingsMenu != null;
            settingsButton.onClick.AddListener(OpenSettings);
        }
        if (exitButton != null)
        {
            exitButton.interactable = exitPanel != null && (saveAndExitButton != null || exitWithoutSavingButton != null);
            exitButton.onClick.AddListener(ShowExitConfirmation);
        }
        if (saveAndExitButton != null) saveAndExitButton.onClick.AddListener(SaveAndExit);
        if (exitWithoutSavingButton != null) exitWithoutSavingButton.onClick.AddListener(ExitToMainMenu);
        if (cancelExitButton != null) cancelExitButton.onClick.AddListener(ShowActions);
    }

    private void LateUpdate()
    {
        if (leaving || SaveGameManager.IsLoading) return;
        if (showingSettings && !SettingsMenu.IsOpen)
        {
            showingSettings = false;
            ShowActions();
            return;
        }

        if (SettingsMenu.HandlesEscape || !Input.GetKeyDown(KeyCode.Escape)) return;
        if (!paused)
        {
            resumeTimeScale = Time.timeScale;
            Time.timeScale = 0;
            paused = true;
            Cursor.visible = true;
            ShowActions();
        }
        else if (exitPanel != null && exitPanel.activeSelf)
            ShowActions();
        else
            Resume();
    }

    private void ShowActions()
    {
        if (leaving) return;
        if (exitPanel != null) exitPanel.SetActive(false);
        pausePanel.SetActive(true);
        if (resumeButton != null) resumeButton.Select();
    }

    private void Resume()
    {
        if (!paused || leaving) return;
        pausePanel.SetActive(false);
        Time.timeScale = resumeTimeScale;
        paused = false;
        closedFrame = Time.frameCount;
    }

    private void OpenSettings()
    {
        if (settingsMenu == null || leaving) return;

        settingsMenu.OpenSettings();
        showingSettings = SettingsMenu.IsOpen;
        if (showingSettings) pausePanel.SetActive(false);
    }

    private void ShowExitConfirmation()
    {
        if (exitPanel == null || leaving) return;
        if (exitStatus != null) exitStatus.text = "";
        pausePanel.SetActive(false);
        exitPanel.SetActive(true);
        if (cancelExitButton != null) cancelExitButton.Select();
    }

    private void SaveAndExit()
    {
        if (leaving) return;
        var saves = SaveGameManager.Instance;
        if (saves != null && saves.TrySaveBeforeExit(resumeTimeScale))
            ExitToMainMenu();
        else if (exitStatus != null)
            exitStatus.text = "Не удалось сохранить игру";
    }

    private void ExitToMainMenu()
    {
        if (leaving) return;
        leaving = true;
        if (exitStatus != null) exitStatus.text = "Открываем главное меню…";
        try
        {
            if (SceneManager.LoadSceneAsync(MainMenuScene, LoadSceneMode.Single) == null)
                throw new InvalidOperationException("Сцена главного меню недоступна");
            if (saveAndExitButton != null) saveAndExitButton.interactable = false;
            if (exitWithoutSavingButton != null) exitWithoutSavingButton.interactable = false;
            if (cancelExitButton != null) cancelExitButton.interactable = false;
        }
        catch (Exception ex)
        {
            leaving = false;
            if (exitStatus != null) exitStatus.text = "Не удалось открыть главное меню";
            Debug.LogWarning("[Пауза] " + ex.Message, this);
        }
    }

    private void OnDisable()
    {
        if (instance != this) return;
        instance = null;
        if (resumeButton != null) resumeButton.onClick.RemoveListener(Resume);
        if (settingsButton != null) settingsButton.onClick.RemoveListener(OpenSettings);
        if (exitButton != null) exitButton.onClick.RemoveListener(ShowExitConfirmation);
        if (saveAndExitButton != null) saveAndExitButton.onClick.RemoveListener(SaveAndExit);
        if (exitWithoutSavingButton != null) exitWithoutSavingButton.onClick.RemoveListener(ExitToMainMenu);
        if (cancelExitButton != null) cancelExitButton.onClick.RemoveListener(ShowActions);
        if (showingSettings && settingsMenu != null) settingsMenu.CancelSettings();
        if (pausePanel != null) pausePanel.SetActive(false);
        if (exitPanel != null) exitPanel.SetActive(false);
        if (paused && !SaveGameManager.IsLoading)
        {
            Time.timeScale = leaving ? 1 : resumeTimeScale;
            closedFrame = Time.frameCount;
        }
        paused = false;
        showingSettings = false;
        leaving = false;
    }
}
