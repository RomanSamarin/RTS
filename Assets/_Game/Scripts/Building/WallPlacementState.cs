using System;
using System.Collections.Generic;
using UnityEngine;
using _Game.Scripts.UI;

/// <summary>Два клика → ряд независимых строек с атомарным резервированием всей цены.</summary>
public sealed class WallPlacementState : IBuildingState
{
    private readonly ObjectData data;
    private readonly Grid grid;
    private readonly GridData occupied;
    private readonly ObjectPlacer placer;
    private readonly InputManager input;
    private readonly WallSegment settings;
    private readonly float buildTime;
    private readonly GameObject sitePrefab;
    private readonly Dictionary<ResourceType, int> cost;
    private readonly List<GameObject> ghosts = new List<GameObject>();
    private readonly List<Vector3Int> cells = new List<Vector3Int>();
    private readonly List<Vector3> positions = new List<Vector3>();
    private readonly List<bool> reused = new List<bool>();
    private readonly List<WallDraftPlan.Node> draft = new List<WallDraftPlan.Node>();
    private readonly List<WallDraftPlan.Node> previewPlan = new List<WallDraftPlan.Node>();
    private readonly List<Quaternion> rotations = new List<Quaternion>();
    private bool[] towerLayout = Array.Empty<bool>();
    private readonly Material previewMaterial;
    private readonly MaterialPropertyBlock previewProperties = new MaterialPropertyBlock();
    private Vector3Int? anchor;
    private int? manualRotation;
    private int currentRotation;
    public void Rotate(int steps)
    {
        manualRotation = WallLinePlan.NormalizeRotation((manualRotation ?? currentRotation) + steps);
        Debug.Log($"[Стены] Поворот {manualRotation.Value * 90}°. R — повернуть, Shift+R — в обратную сторону.");
    }
    private string error;
    private WallSegment SegmentAt(int index, int count) => settings.towerPrefab != null
        && index >= 0 && index < towerLayout.Length && towerLayout[index] ? settings.towerPrefab : settings;
    private Dictionary<ResourceType, int> CostAt(int index, int count)
        => SegmentAt(index, count) is WallTower tower ? tower.GetCost() : cost;

    public WallPlacementState(ObjectData data, Grid grid, GridData occupied, ObjectPlacer placer,
        InputManager input, float buildTime, GameObject sitePrefab)
    {
        this.data = data; this.grid = grid; this.occupied = occupied; this.placer = placer;
        this.input = input; this.buildTime = buildTime; this.sitePrefab = sitePrefab;
        settings = data.Prefab.GetComponent<WallSegment>(); cost = BuildCost.GetMaterials(data);
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
        previewMaterial = new Material(shader);
    }

    private bool Evaluate(Vector3Int end)
    {
        cells.Clear(); positions.Clear(); reused.Clear(); rotations.Clear(); previewPlan.Clear(); error = "";
        end.y = 0;
        var start = anchor ?? end;
        var plan = WallDraftPlan.Preview(draft, start.x, start.z, end.x, end.z, manualRotation, out error);
        if (plan.Count == 0) return false;
        previewPlan.AddRange(plan);
        towerLayout = WallDraftPlan.TowerLayout(previewPlan, settings.towerInterval);
        // Квадратная сетка обеспечивает одинаковую длину секции при повороте.
        Vector3 x = grid.CellToWorld(Vector3Int.right) - grid.CellToWorld(Vector3Int.zero);
        Vector3 z = grid.CellToWorld(new Vector3Int(0, 0, 1)) - grid.CellToWorld(Vector3Int.zero);
        if (Mathf.Abs(x.magnitude - settings.length) > 0.01f || Mathf.Abs(z.magnitude - settings.length) > 0.01f
            || grid.transform.rotation != Quaternion.identity || data.Prefab.transform.localScale != Vector3.one)
        { error = $"Размеры не совпадают: клетка X={x.magnitude:0.##}, Z={z.magnitude:0.##}; Length={settings.length:0.##}, Scale={data.Prefab.transform.localScale}. Установите Length по размеру клетки и Scale корня (1,1,1); размер модели задавайте дочернему объекту. Grid должен быть без поворота."; return false; }
        currentRotation = WallDraftPlan.ActiveRotation(start.x, start.z, end.x, end.z, manualRotation);
        float? previousY = null;
        int planIndex = 0;
        foreach (var cell in plan)
        {
            Quaternion rotation = Quaternion.Euler(0, cell.rotation * 90, 0);
            rotations.Add(rotation);
            var part = SegmentAt(planIndex++, plan.Count);
            if (Mathf.Abs(part.length - settings.length) > 0.01f || part.transform.localScale != Vector3.one
                || Mathf.Abs(part.height - settings.height) > 0.1f || part.navigationAgentType != settings.navigationAgentType)
                error = "У башни и стены должны совпадать длина клетки, высота площадки, Agent Type и масштаб 1.";
            var coord = new Vector3Int(cell.x, 0, cell.z);
            Vector3 center = grid.CellToWorld(coord) + (x + z) * 0.5f;
            bool ground = input.TryProjectWallGround(center, out var hit);
            if (ground)
            {
                center.y = hit.point.y;
                // В этой сцене земля ниже Y=0. Занятость должна храниться на том
                // же уровне клетки, который используют обычные здания и снос.
                coord.y = grid.WorldToCell(hit.point).y;
            }
            cells.Add(coord); positions.Add(center);
            if (!ground || Vector3.Angle(hit.normal, Vector3.up) > settings.maxSlope)
                error = "Нет подходящей поверхности или слишком крутой склон.";
            if (previousY.HasValue && Mathf.Abs(center.y - previousY.Value) > settings.maxGroundStep)
                error = "Слишком большой перепад высоты между секциями.";
            previousY = center.y;
            int existingIndex = occupied.GetRepresentationIndex(coord);
            var existingObject = existingIndex >= 0 ? placer.GetPlacedObject(existingIndex) : null;
            var existingWall = existingObject != null ? existingObject.GetComponent<WallSegment>() : null;
            bool compatible = existingWall != null && Mathf.Abs(existingWall.length - settings.length) < 0.01f
                && Mathf.Abs(existingWall.height - settings.height) < 0.1f && existingWall.navigationAgentType == settings.navigationAgentType
                && existingWall.GetComponent<BuildingHealth>() != null && !existingWall.GetComponent<BuildingHealth>().IsDead;
            if (existingWall is WallGate && plan.Count > 1
                && Mathf.Abs(Vector3.Dot(existingWall.transform.right, rotation * Vector3.right)) < 0.95f) compatible = false;
            bool reuse = WallFortificationPlan.CanReuseEndpoint(planIndex - 1, plan.Count, compatible);
            reused.Add(reuse);
            if (!occupied.CanPlaceObjectAt(coord, Vector2Int.one) && !reuse)
                error = "Клетка занята. Существующую стену можно использовать в начале или конце участка, остальные клетки должны быть свободны.";
            Vector3 half = new Vector3(part.length * 0.48f, part.height * 0.48f, Mathf.Max(part.thickness, part.walkwayWidth) * 0.48f);
            if (!reuse && Physics.CheckBox(center + Vector3.up * (part.height * 0.5f + 0.05f), half, rotation,
                part.blockingLayers | settings.blockingLayers, QueryTriggerInteraction.Ignore)) error = "Стена или башня пересекает препятствие.";
        }
        var totalCost = new Dictionary<ResourceType, long>();
        for (int i = 0; i < cells.Count; i++)
        {
            if (reused[i]) continue;
            foreach (var item in CostAt(i, cells.Count))
            { totalCost.TryGetValue(item.Key, out long amount); totalCost[item.Key] = amount + item.Value; }
        }
        foreach (var item in totalCost)
        {
            long available = 0;
            foreach (var store in WarehouseStorage.All)
                if (store != null && store.isActiveAndEnabled) available += store.AvailableMaterial(item.Key);
            if (available < item.Value) error = "Недостаточно свободных материалов на стены и башни.";
        }
        return error.Length == 0;
    }

    public bool OnAction(Vector3Int position)
    {
        position.y = 0;
        if (!Evaluate(position)) { Debug.LogWarning("[Стены] " + error); return false; }
        if (!anchor.HasValue)
        {
            bool fixFirstTower = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (fixFirstTower && settings.towerPrefab == null)
            { Debug.LogWarning("[Стены] Для фиксации башни назначьте Tower Prefab на префабе стены."); return false; }
            anchor = position;
            if (fixFirstTower)
            {
                draft.Clear(); draft.AddRange(previewPlan);
                WallDraftPlan.FixTower(draft, position.x, position.z);
                manualRotation = null; currentRotation = 0;
            }
            UpdateState(position);
            Debug.Log(fixFirstTower ? "[Стены] Первая башня зафиксирована в превью. Ведите стену от неё."
                : "[Стены] Начало выбрано. Укажите конец участка вторым кликом по земле.");
            return false;
        }
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            if (settings.towerPrefab == null)
            { Debug.LogWarning("[Стены] Для фиксации башни назначьте Tower Prefab на префабе стены."); return false; }
            draft.Clear(); draft.AddRange(previewPlan);
            var start = anchor.Value;
            anchor = currentRotation % 2 == 0 ? new Vector3Int(position.x, 0, start.z) : new Vector3Int(start.x, 0, position.z);
            WallDraftPlan.FixTower(draft, anchor.Value.x, anchor.Value.z);
            manualRotation = null; currentRotation = 0;
            Debug.Log("[Стены] Башня зафиксирована в превью. Следующая стена идёт от неё; ЛКМ без Shift — построить весь чертёж.");
            UpdateState(anchor.Value);
            return false;
        }
        if (!reused.Contains(false))
        { Debug.LogWarning("[Стены] Участок уже построен. Выберите конец в свободной клетке."); return false; }
        var reservations = new List<ConstructionMaterials>();
        var created = new List<int>();
        var createdCells = new List<Vector3Int>();
        var sites = new List<ConstructionSite>();
        bool committed = false;
        try
        {
            // Сначала резервируем ВСЕ секции; до этого никаких построек на карте нет.
            for (int i = 0; i < cells.Count; i++)
            {
                if (reused[i]) { reservations.Add(null); continue; }
                var materials = new GameObject("WallMaterials").AddComponent<ConstructionMaterials>();
                FactionMember.Set(materials.gameObject, FactionMember.LocalPlayer);
                reservations.Add(materials);
                if (!materials.Initialize(CostAt(i, cells.Count))) { Debug.LogWarning("[Стены] Материалы уже зарезервированы другой стройкой."); return false; }
            }
            for (int i = 0; i < cells.Count; i++)
            {
                if (reused[i]) continue;
                var part = SegmentAt(i, cells.Count);
                int index = placer.PlaceObject(part.gameObject, positions[i], rotations[i]);
                created.Add(index);
                createdCells.Add(cells[i]);
                var building = placer.GetPlacedObject(index);
                FactionMember.Set(building,FactionMember.LocalPlayer);
                var cell = cells[i];
                occupied.AddObjectAt(cell, Vector2Int.one, data.ID, index);
                building.GetComponent<BuildingHealth>().Destroyed += _ =>
                { if (occupied.GetRepresentationIndex(cell) == index) occupied.RemoveObjectAt(cell); };
                var site = ConstructionSite.Begin(building, part is WallTower tower ? tower.towerBuildTime : buildTime, Vector2Int.one, sitePrefab);
                if (site == null) throw new InvalidOperationException("Не удалось создать стройплощадку стены.");
                sites.Add(site);
                site.Materials = reservations[i];
                reservations[i].transform.SetParent(site.transform, false);
            }
            committed = true;
            Debug.Log($"[Стены] Новых секций: {created.Count}. Существующие стыки сохранены; материалы доставят строители.");
            anchor = null; draft.Clear();
            manualRotation = null; currentRotation = 0;
            HidePreview();
            return true;
        }
        catch (Exception exception) { Debug.LogException(exception); return false; }
        finally
        {
            if (!committed)
            {
                foreach (var materials in reservations)
                    if (materials != null) { materials.ReleaseAll(); UnityEngine.Object.Destroy(materials.gameObject); }
                foreach (var site in sites) if (site != null) UnityEngine.Object.Destroy(site.gameObject);
                for (int i = 0; i < created.Count; i++)
                {
                    if (occupied.GetRepresentationIndex(createdCells[i]) == created[i]) occupied.RemoveObjectAt(createdCells[i]);
                    placer.RemoveObjectAt(created[i]);
                }
            }
        }
    }

    public void UpdateState(Vector3Int position)
    {
        bool valid = Evaluate(position);
        Color color = valid ? new Color(0.2f, 0.8f, 0.35f) : new Color(0.9f, 0.2f, 0.15f);
        previewMaterial.color = color;
        if (previewMaterial.HasProperty("_BaseColor")) previewMaterial.SetColor("_BaseColor", color);
        while (ghosts.Count < positions.Count)
        {
            // Кубы вместо клона игрового префаба: превью не регистрирует здания и препятствия.
            var ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ghost.name = "WallPreview"; ghost.layer = 2;
            ghost.GetComponent<Collider>().enabled = false;
            ghost.GetComponent<Renderer>().sharedMaterial = previewMaterial;
            ghosts.Add(ghost);
        }
        for (int i = 0; i < ghosts.Count; i++)
        {
            ghosts[i].SetActive(i < positions.Count);
            if (i >= positions.Count) continue;
            var part = SegmentAt(i, positions.Count);
            bool fixedTower = previewPlan[i].fixedTower && settings.towerPrefab != null;
            ghosts[i].name = fixedTower ? "FixedTowerPreview" : part is WallTower ? "TowerPreview" : "WallPreview";
            Color pieceColor = !valid ? color : fixedTower ? new Color(1f, 0.72f, 0.2f)
                : part is WallTower ? new Color(0.2f, 0.7f, 1f) : color;
            previewProperties.Clear();
            previewProperties.SetColor("_BaseColor", pieceColor);
            previewProperties.SetColor("_Color", pieceColor);
            ghosts[i].GetComponent<Renderer>().SetPropertyBlock(previewProperties);
            ghosts[i].transform.SetPositionAndRotation(positions[i] + Vector3.up * part.height * 0.5f, rotations[i]);
            ghosts[i].transform.localScale = new Vector3(part.length, part.height, Mathf.Max(part.thickness, part.walkwayWidth));
        }
    }
    public void HidePreview() { foreach (var ghost in ghosts) ghost.SetActive(false); }
    public void EndState()
    {
        foreach (var ghost in ghosts) UnityEngine.Object.Destroy(ghost);
        ghosts.Clear(); UnityEngine.Object.Destroy(previewMaterial);
        draft.Clear(); previewPlan.Clear();
    }
}
