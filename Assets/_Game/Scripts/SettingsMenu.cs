using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class SettingsMenu : MonoBehaviour
{
    [Header("Audio")]
    public Slider masterVolumeSlider;
    public Slider musicVolumeSlider;
    public AudioSource musicSource;
    public AudioSource sfxSource;

    [Header("Display")]
    public Dropdown resolutionDropdown;
    public Dropdown graphicsDropdown;

    [Header("Buttons")]
    public Button applyButton;
    public Button cancelButton;

    [Header("Menu Panel")]
    public GameObject settingsPanel;

    private Resolution[] resolutions;

    // Для отмены изменений
    private float initMaster;
    private float initMusic;
    private int initResolutionIndex;
    private int initGraphicsIndex;

    void Start()
    {
        SetupResolutionDropdown();
        SetupGraphicsDropdown();
        LoadSettings();

        // Слушатели UI
        masterVolumeSlider.onValueChanged.AddListener(OnMasterChanged);
        musicVolumeSlider.onValueChanged.AddListener(OnMusicChanged);
        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        graphicsDropdown.onValueChanged.AddListener(OnGraphicsChanged);

        applyButton.onClick.AddListener(ApplySettings);
        cancelButton.onClick.AddListener(CancelSettings);
    }

    void SetupResolutionDropdown()
    {
        resolutions = Screen.resolutions
            .Select(r => new Resolution { width = r.width, height = r.height, refreshRate = r.refreshRate })
            .Distinct()
            .ToArray();

        resolutionDropdown.ClearOptions();
        var options = resolutions.Select(r => $"{r.width} x {r.height}").ToList();
        resolutionDropdown.AddOptions(options);

        int currentResIndex = System.Array.FindIndex(resolutions, r =>
            r.width == Screen.currentResolution.width &&
            r.height == Screen.currentResolution.height);

        resolutionDropdown.value = currentResIndex >= 0 ? currentResIndex : 0;

        // Live preview при смене
        resolutionDropdown.onValueChanged.AddListener(index =>
            ApplyResolutionImmediate(index));
    }

    void SetupGraphicsDropdown()
    {
        graphicsDropdown.ClearOptions();
        graphicsDropdown.AddOptions(QualitySettings.names.ToList());
        graphicsDropdown.value = QualitySettings.GetQualityLevel();
    }

    void LoadSettings()
    {
        masterVolumeSlider.value = PlayerPrefs.GetFloat("MasterVolume", 0.8f);
        musicVolumeSlider.value = PlayerPrefs.GetFloat("MusicVolume", 0.6f);
        int savedResolution = PlayerPrefs.GetInt("ResolutionIndex", resolutionDropdown.value);
        int savedGraphics = PlayerPrefs.GetInt("GraphicsIndex", graphicsDropdown.value);

        resolutionDropdown.value = Mathf.Clamp(savedResolution, 0, resolutions.Length - 1);
        graphicsDropdown.value = Mathf.Clamp(savedGraphics, 0, QualitySettings.names.Length - 1);

        ApplyAudioVolumes();
        ApplyResolutionImmediate(resolutionDropdown.value);
        QualitySettings.SetQualityLevel(graphicsDropdown.value);

        // сохраняем первоначальное состояние
        initMaster = masterVolumeSlider.value;
        initMusic = musicVolumeSlider.value;
        initResolutionIndex = resolutionDropdown.value;
        initGraphicsIndex = graphicsDropdown.value;
    }

    // ======= Слушатели изменений UI =======
    void OnMasterChanged(float value) => ApplyAudioVolumes();
    void OnMusicChanged(float value) => ApplyAudioVolumes();
    void OnResolutionChanged(int index) => ApplyResolutionImmediate(index);
    void OnGraphicsChanged(int index) => QualitySettings.SetQualityLevel(index);

    void ApplyAudioVolumes()
    {
        if (musicSource != null) musicSource.volume = musicVolumeSlider.value;
        if (sfxSource != null) sfxSource.volume = masterVolumeSlider.value;
        AudioListener.volume = masterVolumeSlider.value;
    }

    // ======= Кнопки Apply и Cancel =======
    void ApplySettings()
    {
        // Сохраняем настройки
        PlayerPrefs.SetFloat("MasterVolume", masterVolumeSlider.value);
        PlayerPrefs.SetFloat("MusicVolume", musicVolumeSlider.value);
        PlayerPrefs.SetInt("ResolutionIndex", resolutionDropdown.value);
        PlayerPrefs.SetInt("GraphicsIndex", graphicsDropdown.value);
        PlayerPrefs.Save();
        

        // Применяем
        ApplyResolutionImmediate(resolutionDropdown.value);
        QualitySettings.SetQualityLevel(graphicsDropdown.value);
        ApplyAudioVolumes();

        // Закрываем меню
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        Debug.Log("✅ Settings applied and saved.");
    }

    void CancelSettings()
    {
        // Восстанавливаем первоначальные значения
        masterVolumeSlider.value = initMaster;
        musicVolumeSlider.value = initMusic;
        resolutionDropdown.value = initResolutionIndex;
        graphicsDropdown.value = initGraphicsIndex;

        ApplyResolutionImmediate(initResolutionIndex);
        QualitySettings.SetQualityLevel(initGraphicsIndex);
        ApplyAudioVolumes();

        // Закрываем меню
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        Debug.Log("❎ Changes reverted.");
    }

    // ======= Применение разрешения =======
    void ApplyResolutionImmediate(int index)
    {
        if (index < 0 || index >= resolutions.Length) return;
        var r = resolutions[index];
        Screen.SetResolution(r.width, r.height, Screen.fullScreen, r.refreshRate);
    }
}