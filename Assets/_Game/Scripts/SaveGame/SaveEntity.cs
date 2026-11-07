using UnityEngine;

/// <summary>Постоянный идентификатор; Prefab нужен только при неоднозначном имени динамического объекта.</summary>
[DisallowMultipleComponent]
public class SaveEntity : MonoBehaviour
{[HideInInspector] public string id,sceneKey;public GameObject prefab;}
