using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ContinueScriot : MonoBehaviour
{
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button autoSaveButton;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private string gameScene = "Assets/Scenes/LordsGame.unity";

    private SaveGameManager saves;
    private bool starting;

    private void Start()
    {
        saves = SaveGameManager.Instance;
        if (statusText != null) statusText.text = saves != null ? saves.Status : "";
        if (saves != null) saves.StatusChanged += ShowStatus;
        if (newGameButton != null) newGameButton.onClick.AddListener(NewGame);
        if (continueButton != null) continueButton.onClick.AddListener(ContinueButton);
        if (autoSaveButton != null) autoSaveButton.onClick.AddListener(LoadSave);
        RefreshButtons();
    }

    private void OnDestroy()
    {
        if (saves != null) saves.StatusChanged -= ShowStatus;
        if (newGameButton != null) newGameButton.onClick.RemoveListener(NewGame);
        if (continueButton != null) continueButton.onClick.RemoveListener(ContinueButton);
        if (autoSaveButton != null) autoSaveButton.onClick.RemoveListener(LoadSave);
    }

    public void NewGame()
    {
        if (starting || SaveGameManager.IsLoading || SettingsMenu.IsOpen) return;

        starting = true;
        ShowStatus("Открываем песочницу…");
        float previousTimeScale = Time.timeScale;
        Time.timeScale = 1;
        try
        {
            if (SceneManager.LoadSceneAsync(gameScene, LoadSceneMode.Single) == null)
                throw new InvalidOperationException("Сцена недоступна");
        }
        catch (Exception ex)
        {
            starting = false;
            Time.timeScale = previousTimeScale;
            Debug.LogWarning("[Меню] " + ex.Message, this);
            ShowStatus("Не удалось открыть песочницу");
        }
    }

    public void ContinueButton()
    {
        if (!starting && !SettingsMenu.IsOpen) saves?.ContinueLatest();
    }

    private void LoadSave()
    {
        if (!starting && !SettingsMenu.IsOpen) saves?.LoadFromMenu();
    }

    private void ShowStatus(string message)
    {
        if (statusText != null) statusText.text = message;
        RefreshButtons();
    }

    private void RefreshButtons()
    {
        bool canStart = !starting && !SaveGameManager.IsLoading;
        if (newGameButton != null) newGameButton.interactable = canStart;
        if (continueButton != null)
            continueButton.interactable = canStart && saves != null && saves.HasSave(true);
        if (autoSaveButton != null)
            autoSaveButton.interactable = canStart && saves != null && saves.HasSave(true);
    }
}
