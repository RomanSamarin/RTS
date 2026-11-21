using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public struct RtsNetworkRoadData : INetworkSerializable, IEquatable<RtsNetworkRoadData>
{
    public const int MaxPoints = 96;
    public FixedList4096Bytes<Vector3> Points;
    public byte Owner;
    public bool RoundStart, RoundEnd;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Owner);
        serializer.SerializeValue(ref RoundStart);
        serializer.SerializeValue(ref RoundEnd);
        byte count = (byte)Points.Length;
        serializer.SerializeValue(ref count);
        if (serializer.IsReader) Points.Clear();
        for (int i = 0; i < count; i++)
        {
            var point = serializer.IsReader ? default : Points[i];
            serializer.SerializeValue(ref point);
            if (serializer.IsReader) Points.Add(point);
        }
    }

    public bool Equals(RtsNetworkRoadData other) => Owner == other.Owner && RoundStart == other.RoundStart
        && RoundEnd == other.RoundEnd && Points.Equals(other.Points);
}

public sealed partial class RtsNetworkSession
{
    private NetworkList<RtsNetworkRoadData> networkRoads;
    private readonly List<RoadStroke> replicatedRoads = new List<RoadStroke>();
    private readonly Dictionary<int, float> nextRoadRequest = new Dictionary<int, float>();
    private NavMeshSurface testGround;
    private int oldLayer;
    private LayerMask oldRoadMask, oldCommandMask, oldNavigationMask;
    private NavMeshSurface oldNavigationSurface;
    public int BuiltRoadCount => replicatedRoads.Count;

    [ContextMenu("RTS/Build road (Play Mode)")]
    public void BeginRoadPlacement()
    {
        if (Ready) RoadBuilder.Instance?.StartRoadPlacement();
    }

    public void RequestRoad(Vector3[] route, bool roundStart = false, bool roundEnd = false)
    {
        if (!Ready || route == null || route.Length < 2 || route.Length > RtsNetworkRoadData.MaxPoints) return;
        var data = new RtsNetworkRoadData { RoundStart = roundStart, RoundEnd = roundEnd };
        foreach (var point in route) data.Points.Add(point);
        BuildRoadServerRpc(data);
    }

    [ServerRpc(RequireOwnership = false)]
    private void BuildRoadServerRpc(RtsNetworkRoadData request, ServerRpcParams parameters = default)
    {
        ulong sender = parameters.Receive.SenderClientId;
        int slot = slots.Get(sender);
        if (slot == 0 || RoadBuilder.Instance == null) return;
        if (nextRoadRequest.TryGetValue(slot, out var next) && Time.unscaledTime < next) return;
        nextRoadRequest[slot] = Time.unscaledTime + .4f;
        int count = 0;
        foreach (var road in networkRoads) if (road.Owner == slot) count++;
        var points = new List<Vector3>(request.Points.Length);
        foreach (var point in request.Points) points.Add(point);
        if (count >= 12 || !CanBuildRoad(slot, points)
            || !RoadBuilder.Instance.ValidateNetworkRoute(points, request.RoundStart, request.RoundEnd))
        { Reply(sender, "Дорога пересекает постройку, выходит за участок или недоступна по рельефу"); return; }
        request.Points.Clear();
        foreach (var point in points) request.Points.Add(point);
        request.Owner = (byte)slot;
        if (RoadNavigation.Instance != null) RoadNavigation.Instance.enabled = true;
        networkRoads.Add(request);
        Reply(sender, "Дорога построена");
    }

    internal bool CanPreviewRoad(List<Vector3> points) => CanBuildRoad(LocalPlayerId, points);

    private static bool CanBuildRoad(int slot, List<Vector3> points)
    {
        if (points.Count < 2 || points.Count > RtsNetworkRoadData.MaxPoints || slot < 1 || slot > 4) return false;
        float plotX = (slot - 2.5f) * 6f;
        float width = RoadBuilder.Instance.roadWidth * .5f;
        float length = 0;
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 point = points[i];
            if (!NetworkPlayerSlots.ValidPoint(point.x, point.y, point.z)
                || Mathf.Abs(point.x - plotX) + width > 3f || point.z < 16f || point.z > 29f) return false;
            if (i == 0) continue;
            var delta = point - points[i - 1];
            float segment = new Vector2(delta.x, delta.z).magnitude;
            if (segment > 1.1f) return false;
            length += segment;
        }
        if (length < .5f || length > 25f) return false;
        foreach (var site in FindObjectsOfType<RtsNetworkBuildSite>())
        {
            if (!site.IsSpawned) continue;
            for (int i = 1; i < points.Count; i++)
                if (RoadCurve.RibbonIntersectsBox(points[i - 1], points[i], width, site.Footprint)) return false;
        }
        return true;
    }

    private void NetworkRoadChanged(NetworkListEvent<RtsNetworkRoadData> _) => RefreshNetworkRoads();

    private void RefreshNetworkRoads()
    {
        var builder = RoadBuilder.Instance;
        if (builder == null) return;
        while (replicatedRoads.Count < networkRoads.Count)
        {
            var data = networkRoads[replicatedRoads.Count];
            var points = new List<Vector3>(data.Points.Length);
            foreach (var point in data.Points) points.Add(point);
            var road = new GameObject("Player " + data.Owner + " road").AddComponent<RoadStroke>();
            road.transform.SetParent(transform, false);
            builder.RestoreSavedRoad(road, points, builder.roadWidth, data.RoundStart, data.RoundEnd);
            replicatedRoads.Add(road);
        }
    }

    private void ConfigureRoadGround()
    {
        int layer = LayerMask.NameToLayer("Ground");
        testGround = FindObjectOfType<NavMeshSurface>();
        if (testGround == null || layer < 0) return;
        oldLayer = testGround.gameObject.layer;
        testGround.gameObject.layer = layer;
        if (RoadBuilder.Instance != null)
        {
            oldRoadMask = RoadBuilder.Instance.groundLayer;
            RoadBuilder.Instance.groundLayer = 1 << layer;
        }
        if (ManagerGO.Instance != null)
        {
            oldCommandMask = ManagerGO.Instance.Ground;
            ManagerGO.Instance.Ground = 1 << layer;
        }
        if (RoadNavigation.Instance != null)
        {
            var navigation = RoadNavigation.Instance;
            oldNavigationMask = navigation.groundLayers;
            oldNavigationSurface = navigation.groundSurface;
            navigation.groundLayers = 1 << layer;
            navigation.groundSurface = testGround;
        }
    }

    private void TearDownNetworkRoads()
    {
        networkRoads.OnListChanged -= NetworkRoadChanged;
        foreach (var road in replicatedRoads)
            if (road != null) { road.gameObject.SetActive(false); Destroy(road.gameObject); }
        replicatedRoads.Clear();
        if (testGround == null) return;
        if (RoadNavigation.Instance != null)
        {
            RoadNavigation.Instance.enabled = false;
            RoadNavigation.Instance.groundLayers = oldNavigationMask;
            RoadNavigation.Instance.groundSurface = oldNavigationSurface;
        }
        testGround.gameObject.layer = oldLayer;
        if (RoadBuilder.Instance != null) RoadBuilder.Instance.groundLayer = oldRoadMask;
        if (ManagerGO.Instance != null) ManagerGO.Instance.Ground = oldCommandMask;
    }
}
