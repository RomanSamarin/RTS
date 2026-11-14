using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(BuildingHealth), typeof(UiSelectBuilding))]
public sealed class TownHall : MonoBehaviour
{
    [SerializeField] private GameObject model;
    public BuildingHealth Health => GetComponent<BuildingHealth>();

    private void OnEnable()
    {
        Health.Died += OnDestroyed;
        RestoreAppearance();
    }

    private void OnDisable() => Health.Died -= OnDestroyed;

    private void OnDestroyed(BuildingHealth health)
    {
        RestoreAppearance();
        if (!SaveGameManager.IsLoading && FactionMember.IsPlayer(gameObject))
            RaidManager.Instance?.Warn("Ратуша разрушена. Поселение может продолжить развитие.");
    }

    internal void RestoreAppearance()
    {
        bool intact = !Health.IsDead;
        if (model != null && model.transform != transform && model.transform.IsChildOf(transform))
            model.SetActive(intact);
        if (TryGetComponent<BoxCollider>(out var body)) body.enabled = intact;
        if (TryGetComponent<NavMeshObstacle>(out var obstacle)) obstacle.enabled = intact;
        if (!intact) GetComponent<UiSelectBuilding>().Deselect();
    }
}
