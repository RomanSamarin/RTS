using UnityEngine;

/// <summary>Модели этапов в системе координат готового здания.</summary>
[DisallowMultipleComponent]
public sealed class ConstructionAppearance : MonoBehaviour
{
    public GameObject foundationPrefab;
    public GameObject framePrefab;
    public GameObject wallsPrefab;
    public GameObject roofPrefab;
    public GameObject scaffoldingPrefab;
}
