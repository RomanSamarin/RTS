using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class SettingsMenu : MonoBehaviour
{
    [Header("Audio")]
    public Slider masterVolumeSlider;
    public Slider musicVolumeSlider;
    public AudioSource musicSource;
    public Text masterValueText, musicValueText;

    [Header("Display")]
    public Dropdown resolutionDropdown;
    public Dropdown graphicsDropdown;
    public Toggle fullscreenToggle;

    [Header("Menu")]
    public Button openButton, applyButton, cancelButton, closeButton;
    public GameObject settingsPanel;

    private readonly List<Vector2Int> resolutions = new List<Vector2Int>();
    private static SettingsMenu openedMenu;
    private static int closedFrame = -1;
    public static bool IsOpen => openedMenu != null && openedMenu.isActiveAndEnabled;
    internal static bool HandlesEscape => IsOpen || closedFrame == Time.frameCount;
    public static bool BlocksWorldInput => HandlesEscape || PauseMenu.BlocksWorldInput;
    private float master, music;
    private Vector2Int resolution;
    private int graphics;
    private bool fullscreen;
    private bool initialized;
    private UniversalRenderPipelineAsset qualityProfile;
    private RenderPipelineAsset previousQualityProfile;
    private int profileQuality;

    private bool HasSettingsPanel => settingsPanel != null && !transform.IsChildOf(settingsPanel.transform);

    private void Start()
    {
        Initialize();
        if (musicVolumeSlider != null) musicVolumeSlider.interactable = musicSource != null;
        if (HasSettingsPanel) settingsPanel.SetActive(false);
        else if (settingsPanel != null)
            Debug.LogWarning("SettingsMenu должен находиться вне скрываемой панели настроек.", this);
        if (openButton != null) openButton.onClick.AddListener(OpenSettings);
        if (applyButton != null) applyButton.onClick.AddListener(ApplySettings);
        if (cancelButton != null) cancelButton.onClick.AddListener(CancelSettings);
        if (closeButton != null) closeButton.onClick.AddListener(CancelSettings);
        if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(PreviewAudio);
        if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.AddListener(PreviewAudio);
    }

    private void Initialize()
    {
        if (initialized) return;
        initialized = true;
        master = Mathf.Clamp01(PlayerPrefs.GetFloat("MasterVolume", .8f));
        music = Mathf.Clamp01(PlayerPrefs.GetFloat("MusicVolume", .6f));
        graphics = Mathf.Clamp(PlayerPrefs.GetInt("GraphicsIndex", QualitySettings.GetQualityLevel()),
            0, Mathf.Max(0, QualitySettings.names.Length - 1));
        fullscreen = PlayerPrefs.GetInt("DisplayFullscreen", Screen.fullScreen ? 1 : 0) != 0;
        var current = new Vector2Int(Screen.width, Screen.height);
        foreach (var mode in Screen.resolutions) AddResolution(new Vector2Int(mode.width, mode.height));
        AddResolution(current);
        resolutions.Sort((a, b) => a.x == b.x ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
        var saved = new Vector2Int(PlayerPrefs.GetInt("DisplayWidth", current.x), PlayerPrefs.GetInt("DisplayHeight", current.y));
        resolution = resolutions.Contains(saved) ? saved : current;

        if (resolutionDropdown != null)
        {
            var options = new List<string>();
            foreach (var size in resolutions) options.Add($"{size.x} × {size.y}");
            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(options);
        }
        if (graphicsDropdown != null)
        {
            graphicsDropdown.ClearOptions();
            graphicsDropdown.AddOptions(new List<string>(QualitySettings.names));
        }
        SyncControls();
        SetAudio(master, music);
        ApplyDisplay();
    }

    private void AddResolution(Vector2Int size)
    {
        if (size.x > 0 && size.y > 0 && !resolutions.Contains(size)) resolutions.Add(size);
    }

    private void SyncControls()
    {
        if (masterVolumeSlider != null) masterVolumeSlider.SetValueWithoutNotify(master);
        if (musicVolumeSlider != null) musicVolumeSlider.SetValueWithoutNotify(music);
        if (resolutionDropdown != null) resolutionDropdown.SetValueWithoutNotify(Mathf.Max(0, resolutions.IndexOf(resolution)));
        if (graphicsDropdown != null) graphicsDropdown.SetValueWithoutNotify(graphics);
        if (fullscreenToggle != null) fullscreenToggle.SetIsOnWithoutNotify(fullscreen);
        UpdateVolumeLabels(master, music);
    }

    public void OpenSettings()
    {
        if (!isActiveAndEnabled || !HasSettingsPanel || SaveGameManager.IsLoading || openedMenu == this) return;
        if (openedMenu != null) openedMenu.CancelSettings();
        Initialize();
        SyncControls();
        openedMenu = this;
        settingsPanel.SetActive(true);
    }

    public void ApplySettings()
    {
        if (openedMenu != this) return;
        if (masterVolumeSlider != null) master = Mathf.Clamp01(masterVolumeSlider.value);
        if (musicVolumeSlider != null) music = Mathf.Clamp01(musicVolumeSlider.value);
        if (resolutionDropdown != null && resolutionDropdown.value < resolutions.Count)
            resolution = resolutions[resolutionDropdown.value];
        if (graphicsDropdown != null && graphicsDropdown.options.Count > 0) graphics = graphicsDropdown.value;
        if (fullscreenToggle != null) fullscreen = fullscreenToggle.isOn;

        SetAudio(master, music);
        ApplyDisplay();
        PlayerPrefs.SetFloat("MasterVolume", master);
        PlayerPrefs.SetFloat("MusicVolume", music);
        PlayerPrefs.SetInt("DisplayWidth", resolution.x);
        PlayerPrefs.SetInt("DisplayHeight", resolution.y);
        PlayerPrefs.SetInt("DisplayFullscreen", fullscreen ? 1 : 0);
        PlayerPrefs.SetInt("GraphicsIndex", graphics);
        PlayerPrefs.Save();
        Close();
    }

    public void CancelSettings()
    {
        if (openedMenu != this) return;
        SetAudio(master, music);
        Close();
    }

    private void Close()
    {
        openedMenu = null;
        closedFrame = Time.frameCount;
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    private void PreviewAudio(float value)
    {
        if (openedMenu != this) return;
        SetAudio(masterVolumeSlider != null ? masterVolumeSlider.value : master,
            musicVolumeSlider != null ? musicVolumeSlider.value : music);
    }

    private void SetAudio(float masterValue, float musicValue)
    {
        AudioListener.volume = masterValue;
        if (musicSource != null) musicSource.volume = musicValue;
        UpdateVolumeLabels(masterValue, musicValue);
    }

    private void UpdateVolumeLabels(float masterValue, float musicValue)
    {
        if (masterValueText != null) masterValueText.text = $"{Mathf.RoundToInt(masterValue * 100)}%";
        if (musicValueText != null) musicValueText.text = musicSource != null ? $"{Mathf.RoundToInt(musicValue * 100)}%" : "—";
    }

    private void ApplyDisplay()
    {
        if (!Application.isEditor && resolution.x > 0 && resolution.y > 0
            && (resolution.x != Screen.width || resolution.y != Screen.height || fullscreen != Screen.fullScreen))
            Screen.SetResolution(resolution.x, resolution.y, fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        if (graphics >= QualitySettings.names.Length) return;
        if (qualityProfile != null && graphics == QualitySettings.GetQualityLevel()
            && QualitySettings.renderPipeline == qualityProfile) return;
        ReleaseQualityProfile();
        if (graphics != QualitySettings.GetQualityLevel())
            QualitySettings.SetQualityLevel(graphics);
        if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset source)) return;

        float tier = graphics / (float)Mathf.Max(1, QualitySettings.names.Length - 1);
        previousQualityProfile = QualitySettings.renderPipeline;
        profileQuality = graphics;
        qualityProfile = Instantiate(source);
        qualityProfile.hideFlags = HideFlags.DontSave;
        qualityProfile.renderScale = source.renderScale * Mathf.Lerp(.65f, 1f, tier);
        qualityProfile.shadowDistance = Mathf.Min(source.shadowDistance, QualitySettings.shadowDistance);
        QualitySettings.renderPipeline = qualityProfile;
    }

    private void ReleaseQualityProfile()
    {
        if (qualityProfile == null) return;
        if (QualitySettings.GetQualityLevel() == profileQuality && QualitySettings.renderPipeline == qualityProfile)
            QualitySettings.renderPipeline = previousQualityProfile;
        Destroy(qualityProfile);
        qualityProfile = null;
    }

    private void Update()
    {
        if (openedMenu == this && (settingsPanel == null || !settingsPanel.activeInHierarchy || SaveGameManager.IsLoading))
            CancelSettings();
        else if (openedMenu == this && Input.GetKeyDown(KeyCode.Escape))
            CancelSettings();
    }

    private void OnDisable()
    {
        if (openedMenu == this) CancelSettings();
    }

    private void OnDestroy()
    {
        ReleaseQualityProfile();
        if (openButton != null) openButton.onClick.RemoveListener(OpenSettings);
        if (applyButton != null) applyButton.onClick.RemoveListener(ApplySettings);
        if (cancelButton != null) cancelButton.onClick.RemoveListener(CancelSettings);
        if (closeButton != null) closeButton.onClick.RemoveListener(CancelSettings);
        if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.RemoveListener(PreviewAudio);
        if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.RemoveListener(PreviewAudio);
    }
}
