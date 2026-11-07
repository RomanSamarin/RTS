using UnityEngine;
using UnityEngine.AI;

/// <summary>Корень секции — центр у земли, длина модели вдоль локальной оси X.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BuildingHealth), typeof(BoxCollider), typeof(NavMeshObstacle))]
public class WallSegment : MonoBehaviour
{
    [Min(0.1f)] public float length = 1f;
    [Min(0.1f)] public float thickness = 0.4f;
    [Min(0.1f)] public float height = 3f;
    [Min(0f)] public float maxGroundStep = 0.3f;
    [Range(0f, 60f)] public float maxSlope = 15f;
    [Tooltip("Слои зданий, камней, деревьев. Не включайте землю и юнитов.")]
    public LayerMask blockingLayers;
    [Header("Башни и боевой ход")]
    public WallTower towerPrefab;
    [Min(2)] public int towerInterval = 6;
    [Min(0.8f)] public float walkwayWidth = 1.6f;
    public int navigationAgentType;

    protected virtual void OnEnable()
    {
        if (!Application.isPlaying) return;
        var walkway = GetComponent<WallWalkway>();
        if (walkway == null) walkway = gameObject.AddComponent<WallWalkway>();
        walkway.Refresh();
    }

    protected virtual void Awake() { Configure(); }
    protected virtual void OnValidate() { Configure(); }
    protected virtual void Configure()
    {
        var size = new Vector3(Mathf.Max(0.1f, length), Mathf.Max(0.1f, height), Mathf.Max(0.1f, thickness));
        var box = GetComponent<BoxCollider>();
        if (box != null) { box.isTrigger = false; box.size = size; box.center = Vector3.up * size.y * 0.5f; }
        var obstacle = GetComponent<NavMeshObstacle>();
        if (obstacle != null)
        {
            // Carving блокирует землю, но не вырезает поверхность боевого хода.
            var obstacleSize = new Vector3(size.x, Mathf.Max(0.1f, size.y - 0.5f), size.z);
            obstacle.shape = NavMeshObstacleShape.Box; obstacle.size = obstacleSize;
            obstacle.center = Vector3.up * obstacleSize.y * 0.5f;
            obstacle.carving = true; obstacle.carveOnlyStationary = true;
        }
    }
}
