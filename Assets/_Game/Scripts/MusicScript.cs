using UnityEngine;
using UnityEngine.UI;

public class VolumeSlider : MonoBehaviour
{
    [SerializeField] private AudioSource musicSource; // Источник музыки
    [SerializeField] private Slider volumeSlider;     // Слайдер громкости

    private const string VolumePrefKey = "MusicVolume"; // Ключ для сохранения громкости

    private void Start()
    {
        // Загружаем сохранённую громкость (или устанавливаем по умолчанию 1)
        float savedVolume = PlayerPrefs.GetFloat(VolumePrefKey, 1f);
        musicSource.volume = savedVolume;
        volumeSlider.value = savedVolume;

        // Подписываемся на событие изменения слайдера
        volumeSlider.onValueChanged.AddListener(SetVolume);
    }

    private void SetVolume(float volume)
    {
        musicSource.volume = volume;
        PlayerPrefs.SetFloat(VolumePrefKey, volume); // Сохраняем громкость
    }

    private void OnDestroy()
    {
        // Отписываемся от события, чтобы избежать утечек памяти
        volumeSlider.onValueChanged.RemoveListener(SetVolume);
    }
}