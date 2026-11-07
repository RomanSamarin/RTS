using TMPro;
using UnityEngine;

/// <summary>Подключение к пользовательскому Canvas без создания UI.</summary>
public class SaveGamePanel : MonoBehaviour
{
    public TMP_Text statusText;
    private void Update(){if(statusText!=null)statusText.text=SaveGameManager.Instance!=null?SaveGameManager.Instance.Status:"";}
    public void Save()=>SaveGameManager.Instance?.SaveGame();
    public void Load()=>SaveGameManager.Instance?.LoadGame();
    public void LoadAutoSave()=>SaveGameManager.Instance?.LoadLatestAutoSave();
    public void SetAutoSave(bool value){if(SaveGameManager.Instance!=null)SaveGameManager.Instance.autoSaveEnabled=value;}
}
