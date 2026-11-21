using System.IO;
using Unity.AI.Navigation;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using System.Linq;

public static class MultiplayerPrototypeSetup
{
    public const string ScenePath = "Assets/Scenes/MultiplayerMovement.unity";
    private const string ReservePrefabPath = "Assets/MultiplayerTest/ReserveUnit.prefab";
    [MenuItem("RTS/Multiplayer/Create movement test scene (no UI)")]
    public static void CreateScene()
    {
        if (File.Exists(ScenePath)) { Debug.LogWarning("Test scene already exists: " + ScenePath); return; }
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0, -.5f, 0);
        ground.transform.localScale = new Vector3(60, 1, 60);
        ground.layer = 0;
        var surface = ground.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();
        Directory.CreateDirectory("Assets/MultiplayerTest");
        AssetDatabase.CreateAsset(surface.navMeshData, "Assets/MultiplayerTest/MovementNavMesh.asset");

        var camera = new GameObject("Main Camera", typeof(Camera));
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(0, 32, -25);
        camera.transform.rotation = Quaternion.Euler(52, 0, 0);
        camera.GetComponent<Camera>().farClipPlane = 200;
        var light = new GameObject("Sun", typeof(Light)).GetComponent<Light>();
        light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(50, -30, 0);
        var input = new GameObject("Unit commands", typeof(ManagerGO)).GetComponent<ManagerGO>();
        input.Ground = 1; input.clickable = 1 << 2;
        var managerObject = new GameObject("NetworkManager", typeof(NetworkManager), typeof(UnityTransport));
        var sessionObject = new GameObject("Session", typeof(NetworkObject), typeof(RtsNetworkSession));
        var session = sessionObject.GetComponent<RtsNetworkSession>();
        session.manager = managerObject.GetComponent<NetworkManager>();
        session.transport = managerObject.GetComponent<UnityTransport>();
        session.manager.NetworkConfig.NetworkTransport = session.transport;
        sessionObject.AddComponent<RtsNetworkSmokeProbe>();
        for (int slot = 1; slot <= 4; slot++)
        {
            var unit = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            unit.name = "Player " + slot + " test unit"; unit.layer = 2;
            unit.transform.position = new Vector3((slot - 2.5f) * 6, 1, 0);
            unit.AddComponent<FactionMember>().SetFaction(NetworkPlayerSlots.Side(slot));
            var agent = unit.AddComponent<NavMeshAgent>();
            agent.speed = 4; agent.baseOffset = 1; agent.stoppingDistance = .15f;
            agent.enabled = false;
            unit.AddComponent<RtsNetworkUnit>();
            var indicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            indicator.name = "Indicator";
            indicator.transform.SetParent(unit.transform, false);
            indicator.transform.localPosition = new Vector3(0, -.9f, 0);
            indicator.transform.localScale = new Vector3(1.4f, .025f, 1.4f);
            Object.DestroyImmediate(indicator.GetComponent<Collider>());
            indicator.SetActive(false);
        }
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        ConfigureReserve();
        AssetDatabase.SaveAssets();
        Debug.Log("Movement test scene created without UI: " + ScenePath);
    }

    [MenuItem("RTS/Multiplayer/Configure reserve on movement test scene")]
    public static void ConfigureReserve()
    {
        var scene = EditorSceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded)
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(ScenePath);
        }
        var roots = scene.GetRootGameObjects();
        var session = roots.Select(root => root.GetComponent<RtsNetworkSession>()).First(item => item != null);
        var settings = new SerializedObject(session);
        var property = settings.FindProperty("reserveUnitPrefab");
        if (property.objectReferenceValue != null) return;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ReservePrefabPath);
        if (prefab == null)
        {
            var source = roots.Select(root => root.GetComponent<RtsNetworkUnit>()).First(unit => unit != null && unit.GetComponent<FactionMember>().Side == Faction.Player);
            var copy = Object.Instantiate(source.gameObject);
            try
            {
                copy.name = "ReserveUnit";
                copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                prefab = PrefabUtility.SaveAsPrefabAsset(copy, ReservePrefabPath);
            }
            finally { Object.DestroyImmediate(copy); }
        }
        property.objectReferenceValue = prefab.GetComponent<RtsNetworkUnit>();
        settings.ApplyModifiedProperties();
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    // Run in an isolated validation project, or explicitly from Unity's command line.
    public static void BuildSmokePlayer()
    {
        CreateScene();
        ConfigureReserve();
        ConfigureTraining();
        ConfigureConstruction();
        Directory.CreateDirectory("Builds/NetworkSmoke");
        var report = BuildPipeline.BuildPlayer(new[] { ScenePath }, "Builds/NetworkSmoke/NetworkSmoke.exe",
            BuildTarget.StandaloneWindows64, BuildOptions.Development);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new System.Exception("Network smoke player build failed: " + report.summary.result);
    }

    [MenuItem("RTS/Multiplayer/Configure construction on movement test scene")]
    public static void ConfigureConstruction()
    {
        var scene = EditorSceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded)
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(ScenePath);
        }
        var builder = CreateTrainingUnit("Villager (1) (1).prefab", "NetworkBuilder", RtsNetworkUnit.UnitKind.Builder);
        var lumberjack = CreateTrainingUnit("Villager (1) (1).prefab", "NetworkLumberjack", RtsNetworkUnit.UnitKind.Lumberjack);
        var carrier = CreateTrainingUnit("Villager (1) (1).prefab", "NetworkCarrier", RtsNetworkUnit.UnitKind.Carrier);
        var mason = CreateTrainingUnit("Villager (1) (1).prefab", "NetworkStoneMason", RtsNetworkUnit.UnitKind.StoneMason);
        var producer = CreateTrainingUnit("Villager (1) (1).prefab", "NetworkProducer", RtsNetworkUnit.UnitKind.Producer);
        var farmer = CreateTrainingUnit("Villager (1) (1).prefab", "NetworkFarmer", RtsNetworkUnit.UnitKind.Farmer);
        var miner = CreateTrainingUnit("Villager (1) (1).prefab", "NetworkMiner", RtsNetworkUnit.UnitKind.Miner);
        var horse = CreateTrainingUnit("StableHorse.prefab", "NetworkHorse", RtsNetworkUnit.UnitKind.Horse,
            "Assets/Learnig/Episode11_Assets_RTS/");
        var resident = CreateTrainingUnit("Villager (1) (1).prefab", "NetworkVillager", RtsNetworkUnit.UnitKind.Villager);
        var field = CreateFarmField();
        var tree = CreateDeposit(false);
        var rock = CreateDeposit(true);
        var ironVein = CreateIronVein();
        var site = CreateBuildingPrefab();
        var database = AssetDatabase.LoadAssetAtPath<ObjectsDatabseSO>("Assets/Learnig/Episode11_Assets_RTS/Database.asset");
        EnsureRecipeEntry<Mill>(database, RtsNetworkConstruction.MillId, "Мельница", "Mill");
        EnsureRecipeEntry<Bakery>(database, RtsNetworkConstruction.BakeryId, "Пекарня", "Bakery");
        EnsureFarmEntry(database);
        EnsureMineEntry(database);
        EnsureArcheryRangeEntry(database);
        EnsureStableEntry(database);
        EnsureRecipeEntry<Blacksmith>(database, RtsNetworkConstruction.BlacksmithId, "Кузница", "Blacksmith", 25, 10);
        EnsureRecipeEntry<Workshop>(database, RtsNetworkConstruction.WorkshopId, "Мастерская", "Workshop", 30, 10);
        int[] buildingIds = { RtsNetworkConstruction.HouseId, RtsNetworkConstruction.GranaryId,
            RtsNetworkConstruction.BarracksId, RtsNetworkConstruction.SawmillId, RtsNetworkConstruction.StoneQuarryId,
            RtsNetworkConstruction.MillId, RtsNetworkConstruction.BakeryId, RtsNetworkConstruction.FarmId,
            RtsNetworkConstruction.BlacksmithId, RtsNetworkConstruction.WorkshopId, RtsNetworkConstruction.MineId,
            RtsNetworkConstruction.ArcheryRangeId, RtsNetworkConstruction.StableId };
        var prefabs = buildingIds.Select(id => CreateCatalogBuilding(database.objectsData.Single(item => item.ID == id))).ToArray();
        for (var slot = 1; slot <= 4; slot++)
        {
            var side = NetworkPlayerSlots.Side(slot);
            var network = scene.GetRootGameObjects().Select(root => root.GetComponent<RtsNetworkConstruction>())
                .FirstOrDefault(station => station != null && station.GetComponent<FactionMember>().Side == side);
            if (network == null)
            {
                var plot = new GameObject("Player " + slot + " construction");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(plot, scene);
                plot.transform.position = new Vector3((slot - 2.5f) * 6f, 0, 14f);
                plot.AddComponent<FactionMember>().SetFaction(side);
                network = plot.AddComponent<RtsNetworkConstruction>();
                BoxVisual(plot.transform, "Warehouse", new Vector3(0, 1, 1.2f), new Vector3(2.4f, 2f, 2f), true);
                BoxVisual(plot.transform, "Build plot", Vector3.zero, Vector3.one, false);
            }
            var marker = network.transform.Find("Build plot");
            if (marker != null)
            {
                marker.localPosition = new Vector3(0, .035f, 9f);
                marker.localScale = new Vector3(5f, .04f, 10f);
            }
            var settings = new SerializedObject(network);
            settings.FindProperty("builderPrefab").objectReferenceValue = builder;
            settings.FindProperty("lumberjackPrefab").objectReferenceValue = lumberjack;
            settings.FindProperty("carrierPrefab").objectReferenceValue = carrier;
            settings.FindProperty("stoneMasonPrefab").objectReferenceValue = mason;
            settings.FindProperty("producerPrefab").objectReferenceValue = producer;
            settings.FindProperty("farmerPrefab").objectReferenceValue = farmer;
            settings.FindProperty("minerPrefab").objectReferenceValue = miner;
            settings.FindProperty("horsePrefab").objectReferenceValue = horse;
            settings.FindProperty("residentPrefab").objectReferenceValue = resident;
            settings.FindProperty("fieldPrefab").objectReferenceValue = field;
            settings.FindProperty("timberTreePrefab").objectReferenceValue = tree;
            settings.FindProperty("stoneDepositPrefab").objectReferenceValue = rock;
            settings.FindProperty("ironVeinPrefab").objectReferenceValue = ironVein;
            settings.FindProperty("buildingPrefab").objectReferenceValue = site;
            settings.FindProperty("database").objectReferenceValue = database;
            var catalog = settings.FindProperty("catalog");
            catalog.arraySize = buildingIds.Length;
            for (int index = 0; index < buildingIds.Length; index++)
            {
                var entry = catalog.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("id").intValue = buildingIds[index];
                entry.FindPropertyRelative("prefab").objectReferenceValue = prefabs[index];
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
        if (!scene.GetRootGameObjects().Any(root => root.GetComponentInChildren<SoldiersFormation>(true) != null))
        {
            var root = new GameObject("Formation placement");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
            var placement = root.AddComponent<SoldiersFormation>();
            placement.formationRoot = new GameObject("Formation points").transform;
            placement.formationRoot.SetParent(root.transform, false);
            var marker = BoxVisual(root.transform, "Formation point template", Vector3.zero, new Vector3(.48f, .04f, .7f), false);
            marker.SetActive(false);
            placement.pointPrefab = marker.transform;
        }
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    private static void EnsureRecipeEntry<T>(ObjectsDatabseSO database, int id, string label, string assetName,
        int wood = 20, int stone = 5) where T : RecipeBuilding
    {
        if (database.objectsData.Any(entry => entry.ID == id)) return;
        string path = $"Assets/_Game/Prefab/{assetName}.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            var root = new GameObject(assetName);
            try
            {
                var recipe = root.AddComponent<T>();
                if (!(recipe is EquipmentProductionBuilding)) recipe.secondsPerBatch = 12f;
                root.GetComponent<WorkplaceHiring>().profession = recipe.Profession;
                bool mill = recipe is Mill;
                BoxVisual(root.transform, "Workshop", Vector3.up * .8f, new Vector3(1.7f, 1.6f, 1.7f), true);
                var roof = BoxVisual(root.transform, "Roof", Vector3.up * 1.65f, new Vector3(1.9f, .28f, 1.9f), false);
                roof.transform.localRotation = Quaternion.Euler(0, 0, mill ? 0 : 9);
                if (mill)
                {
                    BoxVisual(root.transform, "Vertical sail", new Vector3(0, 1.42f, -.94f), new Vector3(.13f, 1.7f, .13f), false);
                    BoxVisual(root.transform, "Horizontal sail", new Vector3(0, 1.42f, -.94f), new Vector3(1.7f, .13f, .13f), false);
                }
                else BoxVisual(root.transform, "Chimney", new Vector3(.45f, 1.97f, .45f), new Vector3(.32f, .72f, .32f), false);
                var entrance = new GameObject("Entrance");
                entrance.transform.SetParent(root.transform, false);
                entrance.transform.localPosition = Vector3.back * .95f;
                var access = root.AddComponent<BuildingRoadAccess>();
                access.connectionPoint = entrance.transform;
                prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
        AddCatalogEntry(database, id, label, prefab, wood, stone);
    }

    private static void EnsureFarmEntry(ObjectsDatabseSO database)
    {
        if (database.objectsData.Any(entry => entry.ID == RtsNetworkConstruction.FarmId)) return;
        const string path = "Assets/_Game/Prefab/FarmHouse.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            var root = new GameObject("FarmHouse");
            try
            {
                root.AddComponent<FarmHouse>();
                root.AddComponent<WorkplaceHiring>().profession = ResourcesMove.UnitRole.Farmer;
                BoxVisual(root.transform, "Farm shed", Vector3.up * .65f, new Vector3(1.7f, 1.3f, 1.7f), true);
                BoxVisual(root.transform, "Roof", Vector3.up * 1.35f, new Vector3(1.9f, .22f, 1.9f), false);
                var entrance = new GameObject("Entrance");
                entrance.transform.SetParent(root.transform, false);
                entrance.transform.localPosition = Vector3.back * .95f;
                root.AddComponent<BuildingRoadAccess>().connectionPoint = entrance.transform;
                prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
        AddCatalogEntry(database, RtsNetworkConstruction.FarmId, "Ферма", prefab, 15, 0);
    }

    private static void EnsureMineEntry(ObjectsDatabseSO database)
    {
        if (database.objectsData.Any(entry => entry.ID == RtsNetworkConstruction.MineId)) return;
        const string path = "Assets/_Game/Prefab/MiningHouse.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            var root = new GameObject("MiningHouse");
            try
            {
                root.AddComponent<MiningHouse>();
                root.AddComponent<WorkplaceHiring>().profession = ResourcesMove.UnitRole.Miner;
                BoxVisual(root.transform, "Mine shed", Vector3.up * .6f, new Vector3(1.65f, 1.2f, 1.6f), true);
                BoxVisual(root.transform, "Roof", Vector3.up * 1.39f, new Vector3(1.9f, .22f, 1.9f), false);
                BoxVisual(root.transform, "Entrance beam", new Vector3(0, .95f, -.87f), new Vector3(1f, .25f, .18f), false);
                var entrance = new GameObject("Entrance");
                entrance.transform.SetParent(root.transform, false);
                entrance.transform.localPosition = Vector3.back * .95f;
                root.AddComponent<BuildingRoadAccess>().connectionPoint = entrance.transform;
                prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
        AddCatalogEntry(database, RtsNetworkConstruction.MineId, "Шахта", prefab, 20, 10);
    }

    private const string ArcherSource = "Archery (3) (3).prefab";

    private static void EnsureStableEntry(ObjectsDatabseSO database)
    {
        int id = RtsNetworkConstruction.StableId;
        if (database.objectsData.Any(entry => entry.ID == id)) return;
        const string path = "Assets/_Game/Prefab/Stable.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            var root = new GameObject("Stable");
            try
            {
                var stable = root.AddComponent<Stable>();
                stable.horsePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Learnig/Episode11_Assets_RTS/StableHorse.prefab");
                var entrance = new GameObject("Entrance");
                entrance.transform.SetParent(root.transform, false);
                entrance.transform.localPosition = Vector3.back;
                root.AddComponent<BuildingRoadAccess>().connectionPoint = entrance.transform;
                var exit = new GameObject("Horse Exit");
                exit.transform.SetParent(root.transform, false);
                exit.transform.localPosition = Vector3.forward * 2.4f;
                stable.spawnPoint = exit.transform;
                BoxVisual(root.transform, "Stall", new Vector3(0, .62f, .24f), new Vector3(1.65f, 1.23f, 1.35f), true);
                BoxVisual(root.transform, "Roof", new Vector3(0, 1.42f, .2f), new Vector3(1.9f, .25f, 1.75f), false);
                BoxVisual(root.transform, "Trough", new Vector3(.48f, .28f, -.66f), new Vector3(.5f, .32f, .42f), false);
                prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
        AddCatalogEntry(database, id, "Конюшня", prefab, 40, 20, 10);
    }

    private static void EnsureArcheryRangeEntry(ObjectsDatabseSO database)
    {
        int id = RtsNetworkConstruction.ArcheryRangeId;
        if (database.objectsData.Any(entry => entry.ID == id)) return;
        const string path = "Assets/_Game/Prefab/ArcheryRange.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            var root = new GameObject("ArcheryRange");
            try
            {
                var barracks = root.AddComponent<Barracks>();
                barracks.unitTypes.Add(ArcherRecipe(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefab/" + ArcherSource)));
                var entrance = new GameObject("Entrance");
                entrance.transform.SetParent(root.transform, false);
                entrance.transform.localPosition = Vector3.back * 1.3f;
                barracks.SpawnPoint = entrance;
                BoxVisual(root.transform, "Range shelter", new Vector3(-.39f, .63f, .35f), new Vector3(.89f, 1.26f, 1.25f), true);
                BoxVisual(root.transform, "Roof", new Vector3(-.39f, 1.4f, .35f), new Vector3(1.16f, .24f, 1.55f), false);
                BoxVisual(root.transform, "Practice target", new Vector3(.53f, .76f, .45f), new Vector3(.59f, .77f, .16f), true);
                prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
        AddCatalogEntry(database, id, "Стрельбище", prefab, 30, 10);
    }

    private static Barracks.UnitType ArcherRecipe(GameObject prefab)
        => new Barracks.UnitType { unitName = "Лучник", unitPrefab = prefab, troopType = TroopType.Archer, spawnDelay = 35f };

    private static Arrow CreateNetworkArrow(Arrow source)
    {
        const string path = "Assets/MultiplayerTest/NetworkArrow.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            var copy = Object.Instantiate(source.gameObject);
            try
            {
                copy.name = "NetworkArrow";
                copy.AddComponent<NetworkObject>();
                copy.AddComponent<NetworkTransform>();
                prefab = PrefabUtility.SaveAsPrefabAsset(copy, path);
            }
            finally { Object.DestroyImmediate(copy); }
        }
        RefreshNetworkObjectIdentity(prefab);
        return prefab.GetComponent<Arrow>();
    }

    private static RtsNetworkIronVein CreateIronVein()
    {
        const string path = "Assets/MultiplayerTest/NetworkIronVein.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            var root = new GameObject("Iron vein");
            try
            {
                var vein = root.AddComponent<RtsNetworkIronVein>();
                var ore = BoxVisual(root.transform, "Ore outcrop", Vector3.up * .28f, new Vector3(1.7f, .55f, 1.15f), false);
                var seam = BoxVisual(ore.transform, "Iron seam", new Vector3(0, .49f, 0), new Vector3(.8f, .18f, .24f), false);
                seam.transform.localRotation = Quaternion.Euler(0, 33, 0);
                var settings = new SerializedObject(vein);
                settings.FindProperty("ore").objectReferenceValue = ore;
                settings.ApplyModifiedPropertiesWithoutUndo();
                prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
        RefreshNetworkObjectIdentity(prefab);
        return prefab.GetComponent<RtsNetworkIronVein>();
    }

    private static RtsNetworkFarmField CreateFarmField()
    {
        const string path = "Assets/MultiplayerTest/NetworkFarmField.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            var root = new GameObject("Wheat field");
            try
            {
                var field = root.AddComponent<RtsNetworkFarmField>();
                BoxVisual(root.transform, "Soil", Vector3.up * .025f, new Vector3(1.7f, .04f, 2f), false);
                var crop = new GameObject("Wheat");
                crop.transform.SetParent(root.transform, false);
                for (int row = -1; row <= 1; row++)
                    BoxVisual(crop.transform, "Crop row", new Vector3(row * .45f, .13f, 0), new Vector3(.24f, .18f, 1.8f), false);
                var settings = new SerializedObject(field);
                settings.FindProperty("crop").objectReferenceValue = crop;
                settings.ApplyModifiedPropertiesWithoutUndo();
                prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
        RefreshNetworkObjectIdentity(prefab);
        return prefab.GetComponent<RtsNetworkFarmField>();
    }

    private static void AddCatalogEntry(ObjectsDatabseSO database, int id, string label, GameObject prefab, int wood, int stone, int gold = 0)
    {
        var settings = new SerializedObject(database);
        var entries = settings.FindProperty("objectsData");
        var index = entries.arraySize;
        entries.InsertArrayElementAtIndex(index);
        var data = entries.GetArrayElementAtIndex(index);
        data.FindPropertyRelative("<ID>k__BackingField").intValue = id;
        data.FindPropertyRelative("<Name>k__BackingField").stringValue = label;
        data.FindPropertyRelative("description").stringValue = "";
        data.FindPropertyRelative("<Size>k__BackingField").vector2IntValue = Vector2Int.one;
        data.FindPropertyRelative("<Prefab>k__BackingField").objectReferenceValue = prefab;
        data.FindPropertyRelative("<benefits>k__BackingField").arraySize = 0;
        var costs = data.FindPropertyRelative("<requirements>k__BackingField");
        int goldIndex = stone > 0 ? 2 : 1;
        costs.arraySize = goldIndex + (gold > 0 ? 1 : 0);
        costs.GetArrayElementAtIndex(0).FindPropertyRelative("resource").stringValue = "Wood";
        costs.GetArrayElementAtIndex(0).FindPropertyRelative("amount").intValue = wood;
        if (stone > 0)
        {
            costs.GetArrayElementAtIndex(1).FindPropertyRelative("resource").stringValue = "Stone";
            costs.GetArrayElementAtIndex(1).FindPropertyRelative("amount").intValue = stone;
        }
        if (gold > 0)
        {
            costs.GetArrayElementAtIndex(goldIndex).FindPropertyRelative("resource").stringValue = "Gold";
            costs.GetArrayElementAtIndex(goldIndex).FindPropertyRelative("amount").intValue = gold;
        }
        settings.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }

    private static NetworkObject CreateDeposit(bool stone)
    {
        string path = stone ? "Assets/MultiplayerTest/NetworkStoneDeposit.prefab" : "Assets/MultiplayerTest/NetworkTimberTree.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            var root = new GameObject(stone ? "Stone Deposit" : "Timber Tree");
            try
            {
                root.AddComponent<NetworkObject>();
                if (!stone) BoxVisual(root.transform, "Trunk", Vector3.up * .65f, new Vector3(.25f, 1.3f, .25f), false);
                var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                crown.name = stone ? "Rock" : "Crown";
                crown.transform.SetParent(root.transform, false);
                crown.transform.localPosition = Vector3.up * (stone ? .35f : 1.4f);
                crown.transform.localScale = stone ? new Vector3(1.5f, .9f, 1.15f) : new Vector3(.9f, 1.1f, .9f);
                Object.DestroyImmediate(crown.GetComponent<Collider>());
                prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
        RefreshNetworkObjectIdentity(prefab);
        return prefab.GetComponent<NetworkObject>();
    }

    private static RtsNetworkBuildSite CreateCatalogBuilding(ObjectData entry)
    {
        string path = $"Assets/MultiplayerTest/NetworkBuilding{entry.ID}.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab != null)
        {
            if (entry.ID == RtsNetworkConstruction.BarracksId
                && (prefab.GetComponent<Barracks>().SpawnPoint?.name == "Entrance"
                    || !HasTrainingRecipe(prefab.GetComponent<Barracks>(), TroopType.Spearman)
                    || !HasTrainingRecipe(prefab.GetComponent<Barracks>(), TroopType.Cavalry)))
            {
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var barracks = contents.GetComponent<Barracks>();
                    if (barracks.SpawnPoint?.name == "Entrance") CreateCatalogBarracksExit(barracks, (Vector2)entry.Size * 2);
                    EnsureSpearmanRecipe(barracks);
                    EnsureCavalryRecipe(barracks);
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            RefreshNetworkObjectIdentity(prefab);
            return prefab.GetComponent<RtsNetworkBuildSite>();
        }
        var root = new GameObject("Network " + entry.Name);
        try
        {
            var site = root.AddComponent<RtsNetworkBuildSite>();
            var size = (Vector2)entry.Size * 2;
            var foundation = BoxVisual(root.transform, "Foundation", new Vector3(0, .1f, 0), new Vector3(size.x, .16f, size.y), false);
            var finished = new GameObject("Finished building");
            finished.transform.SetParent(root.transform, false);
            var art = new GameObject("Catalog model");
            art.transform.SetParent(finished.transform, false);
            CopyMeshes(entry.Prefab.transform, art.transform);
            var meshes = art.GetComponentsInChildren<MeshRenderer>();
            if (meshes.Length == 0) throw new System.InvalidOperationException("No visual meshes for " + entry.Name);
            Bounds bounds = meshes[0].bounds;
            foreach (var mesh in meshes) bounds.Encapsulate(mesh.bounds);
            float scale = Mathf.Min(size.x / Mathf.Max(.1f, bounds.size.x), size.y / Mathf.Max(.1f, bounds.size.z));
            art.transform.localScale = Vector3.one * scale * .96f;
            art.transform.localPosition = -new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale * .96f;
            var box = finished.AddComponent<BoxCollider>();
            box.center = Vector3.up * .75f;
            box.size = new Vector3(size.x * .96f, 1.5f, size.y * .96f);
            finished.SetActive(false);
            var settings = new SerializedObject(site);
            settings.FindProperty("foundation").objectReferenceValue = foundation;
            settings.FindProperty("finishedBuilding").objectReferenceValue = finished;
            settings.ApplyModifiedPropertiesWithoutUndo();

            if (entry.ID == RtsNetworkConstruction.BarracksId || entry.ID == RtsNetworkConstruction.ArcheryRangeId)
            {
                var network = root.AddComponent<RtsNetworkBarracks>();
                var barracks = root.GetComponent<Barracks>();
                var resident = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MultiplayerTest/NetworkVillager.prefab");
                if (entry.ID == RtsNetworkConstruction.ArcheryRangeId)
                {
                    var archer = CreateTrainingUnit(ArcherSource, "NetworkArcher", RtsNetworkUnit.UnitKind.Archer);
                    barracks.unitTypes.Add(ArcherRecipe(archer.gameObject));
                }
                else
                {
                    var soldier = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MultiplayerTest/NetworkSwordsman.prefab");
                    barracks.unitTypes.Add(new Barracks.UnitType { unitName = "Мечник", unitPrefab = soldier, troopType = TroopType.Swordsman });
                    EnsureSpearmanRecipe(barracks);
                    EnsureCavalryRecipe(barracks);
                }
                var entrance = new GameObject("Entrance");
                entrance.transform.SetParent(root.transform, false);
                entrance.transform.localPosition = Vector3.back * (size.y * .5f + 1.1f);
                CreateCatalogBarracksExit(barracks, size);
                var bridge = new SerializedObject(network);
                bridge.FindProperty("villagerPrefab").objectReferenceValue = resident.GetComponent<RtsNetworkUnit>();
                bridge.FindProperty("spawnStarter").boolValue = false;
                bridge.ApplyModifiedPropertiesWithoutUndo();
            }
            prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            RefreshNetworkObjectIdentity(prefab);
            return prefab.GetComponent<RtsNetworkBuildSite>();
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void CreateCatalogBarracksExit(Barracks barracks, Vector2 size)
    {
        var exit = new GameObject("Unit Exit");
        exit.transform.SetParent(barracks.transform, false);
        exit.transform.localPosition = Vector3.forward * (size.y * .5f + 1.1f);
        barracks.SpawnPoint = exit;
    }

    private static void CopyMeshes(Transform source, Transform target)
    {
        if (source.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null
            && source.TryGetComponent<MeshRenderer>(out var renderer) && renderer.enabled)
        {
            target.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            target.gameObject.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
        }
        foreach (Transform child in source)
        {
            if (!child.gameObject.activeSelf) continue;
            var copy = new GameObject(child.name).transform;
            copy.SetParent(target, false);
            copy.SetLocalPositionAndRotation(child.localPosition, child.localRotation);
            copy.localScale = child.localScale;
            CopyMeshes(child, copy);
        }
    }

    private static RtsNetworkBuildSite CreateBuildingPrefab()
    {
        const string path = "Assets/MultiplayerTest/NetworkBuildSite.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            var root = new GameObject("NetworkBuildSite");
            try
            {
                var model = root.AddComponent<RtsNetworkBuildSite>();
                var foundation = BoxVisual(root.transform, "Foundation", new Vector3(0, .12f, 0), new Vector3(2.4f, .22f, 2.4f), false);
                var finished = BoxVisual(root.transform, "Storage shed", new Vector3(0, .9f, 0), new Vector3(2.4f, 1.8f, 2.4f), true);
                BoxVisual(finished.transform, "Roof", new Vector3(0, .56f, 0), new Vector3(1.13f, .16f, 1.13f), false);
                finished.SetActive(false);
                var settings = new SerializedObject(model);
                settings.FindProperty("foundation").objectReferenceValue = foundation;
                settings.FindProperty("finishedBuilding").objectReferenceValue = finished;
                settings.ApplyModifiedPropertiesWithoutUndo();
                prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }
        RefreshNetworkObjectIdentity(prefab);
        return prefab.GetComponent<RtsNetworkBuildSite>();
    }

    private static GameObject BoxVisual(Transform parent, string name, Vector3 position, Vector3 scale, bool collides)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = position;
        box.transform.localScale = scale;
        if (!collides) Object.DestroyImmediate(box.GetComponent<Collider>());
        return box;
    }

    [MenuItem("RTS/Multiplayer/Configure training on movement test scene")]
    public static void ConfigureTraining()
    {
        var scene = EditorSceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded)
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(ScenePath);
        }
        var resident = CreateTrainingUnit("Villager (1) (1).prefab", "NetworkVillager", RtsNetworkUnit.UnitKind.Villager);
        var swordsman = CreateTrainingUnit("SwordManL (2) (1).prefab", "NetworkSwordsman", RtsNetworkUnit.UnitKind.Swordsman);
        if (resident.GetComponent<NetworkObject>().PrefabIdHash == swordsman.GetComponent<NetworkObject>().PrefabIdHash)
            throw new System.InvalidOperationException("Training units have duplicate network prefab IDs.");
        var existingBarracks = scene.GetRootGameObjects().Where(root => root.TryGetComponent<RtsNetworkBarracks>(out _)).ToArray();
        if (existingBarracks.Length != 0)
        {
            bool changed = false;
            foreach (var root in existingBarracks)
            {
                var barracks = root.GetComponent<Barracks>();
                if (HasTrainingRecipe(barracks, TroopType.Spearman) && HasTrainingRecipe(barracks, TroopType.Cavalry)) continue;
                EnsureSpearmanRecipe(barracks);
                EnsureCavalryRecipe(barracks);
                changed = true;
            }
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            return;
        }
        for (int slot = 1; slot <= 4; slot++)
        {
            var building = new GameObject("Player " + slot + " barracks");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(building, scene);
            building.transform.position = new Vector3((slot - 2.5f) * 6f, 0, -19f);
            building.AddComponent<FactionMember>().SetFaction(NetworkPlayerSlots.Side(slot));
            var network = building.AddComponent<RtsNetworkBarracks>();
            var barracks = building.GetComponent<Barracks>();
            barracks.unitTypes.Add(new Barracks.UnitType
            {
                unitName = "Мечник", unitPrefab = swordsman.gameObject, troopType = TroopType.Swordsman, spawnDelay = 4
            });
            EnsureSpearmanRecipe(barracks);
            EnsureCavalryRecipe(barracks);
            var exit = new GameObject("Exit");
            exit.transform.SetParent(building.transform, false);
            exit.transform.localPosition = Vector3.forward * 3;
            barracks.SpawnPoint = exit;
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Barracks building";
            visual.transform.SetParent(building.transform, false);
            visual.transform.localPosition = new Vector3(0, 1.2f, -1.2f);
            visual.transform.localScale = new Vector3(3.4f, 2.4f, 2.2f);
            var settings = new SerializedObject(network);
            settings.FindProperty("villagerPrefab").objectReferenceValue = resident;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }

    private static bool HasTrainingRecipe(Barracks barracks, TroopType type)
        => barracks.unitTypes.Any(recipe => recipe != null && recipe.troopType == type);

    private static void EnsureSpearmanRecipe(Barracks barracks)
    {
        if (HasTrainingRecipe(barracks, TroopType.Spearman)) return;
        var spearman = CreateTrainingUnit("SwordManL (2) (1).prefab", "NetworkSpearman", RtsNetworkUnit.UnitKind.Spearman);
        barracks.unitTypes.Add(new Barracks.UnitType
        {
            unitName = "Копейщик", unitPrefab = spearman.gameObject, troopType = TroopType.Spearman, spawnDelay = 30f
        });
        EditorUtility.SetDirty(barracks);
    }

    private static void EnsureCavalryRecipe(Barracks barracks)
    {
        if (HasTrainingRecipe(barracks, TroopType.Cavalry)) return;
        var cavalry = CreateTrainingUnit("SwordManL (2) (1).prefab", "NetworkCavalry", RtsNetworkUnit.UnitKind.Cavalry);
        barracks.unitTypes.Add(new Barracks.UnitType
        {
            unitName = "Кавалерист", unitPrefab = cavalry.gameObject, troopType = TroopType.Cavalry, spawnDelay = 50f
        });
        EditorUtility.SetDirty(barracks);
    }

    private static RtsNetworkUnit CreateTrainingUnit(string sourceName, string prefabName, RtsNetworkUnit.UnitKind kind,
        string sourceFolder = "Assets/_Game/Prefab/")
    {
        var path = "Assets/MultiplayerTest/" + prefabName + ".prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return RefreshPrefabIdentity(existing);
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourceFolder + sourceName);
        var copy = Object.Instantiate(source);
        try
        {
            copy.name = prefabName;
            copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var script in copy.GetComponentsInChildren<MonoBehaviour>(true).Reverse())
            {
                if (script == null || (script.gameObject == copy && (script is Unit || script is FactionMember))) continue;
                if (kind == RtsNetworkUnit.UnitKind.Archer && script.gameObject == copy
                    && (script is Archer || script is AttackController)) continue;
                if (kind == RtsNetworkUnit.UnitKind.Horse && script.gameObject == copy
                    && (script is CartHorse || script is GOScript || script is AttackController)) continue;
                Object.DestroyImmediate(script);
            }
            foreach (var collider in copy.GetComponentsInChildren<Collider>(true))
            {
                if (collider.isTrigger) Object.DestroyImmediate(collider);
                else collider.gameObject.layer = 2;
            }
            var agent = copy.GetComponent<NavMeshAgent>();
            agent.enabled = false;
            if (kind == RtsNetworkUnit.UnitKind.Spearman) ConfigureSpearmanVisual(copy);
            if (kind == RtsNetworkUnit.UnitKind.Cavalry) ConfigureCavalryVisual(copy, agent);
            if (kind == RtsNetworkUnit.UnitKind.Archer)
            {
                var archer = copy.GetComponent<Archer>();
                archer.arrowPrefab = CreateNetworkArrow(archer.arrowPrefab);
                archer.enabled = false;
                copy.GetComponent<AttackController>().enabled = false;
            }
            if (kind == RtsNetworkUnit.UnitKind.Horse)
            {
                copy.GetComponent<CartHorse>().enabled = false;
                copy.GetComponent<GOScript>().enabled = false;
                copy.GetComponent<AttackController>().enabled = false;
            }
            if (kind == RtsNetworkUnit.UnitKind.Villager || kind == RtsNetworkUnit.UnitKind.Lumberjack
                || kind == RtsNetworkUnit.UnitKind.Carrier || kind == RtsNetworkUnit.UnitKind.StoneMason
                || kind == RtsNetworkUnit.UnitKind.Producer || kind == RtsNetworkUnit.UnitKind.Farmer
                || kind == RtsNetworkUnit.UnitKind.Miner)
            {
                copy.AddComponent<ResourcesMove>().role = kind == RtsNetworkUnit.UnitKind.Lumberjack ? ResourcesMove.UnitRole.Lumberjack
                    : kind == RtsNetworkUnit.UnitKind.StoneMason ? ResourcesMove.UnitRole.StoneMason
                    : kind == RtsNetworkUnit.UnitKind.Producer ? ResourcesMove.UnitRole.Miller
                    : kind == RtsNetworkUnit.UnitKind.Farmer ? ResourcesMove.UnitRole.Farmer
                    : kind == RtsNetworkUnit.UnitKind.Miner ? ResourcesMove.UnitRole.Miner
                    : kind == RtsNetworkUnit.UnitKind.Carrier ? ResourcesMove.UnitRole.Carrier : ResourcesMove.UnitRole.Villager;
            }
            if (copy.transform.Find("Indicator") == null)
            {
                var indicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                indicator.name = "Indicator";
                indicator.transform.SetParent(copy.transform, false);
                indicator.transform.localPosition = new Vector3(0, .05f, 0);
                indicator.transform.localScale = new Vector3(1.4f, .025f, 1.4f);
                Object.DestroyImmediate(indicator.GetComponent<Collider>());
                indicator.SetActive(false);
            }
            var network = copy.AddComponent<RtsNetworkUnit>();
            copy.GetComponent<FactionMember>().SetFaction(Faction.Player);
            var settings = new SerializedObject(network);
            settings.FindProperty("kind").enumValueIndex = (int)kind;
            settings.ApplyModifiedPropertiesWithoutUndo();
            return RefreshPrefabIdentity(PrefabUtility.SaveAsPrefabAsset(copy, path));
        }
        finally { Object.DestroyImmediate(copy); }
    }

    private static void ConfigureSpearmanVisual(GameObject soldier)
    {
        var bones = soldier.GetComponentsInChildren<Transform>(true);
        var sword = bones.FirstOrDefault(bone => bone.name == "Sword");
        var leftHand = bones.FirstOrDefault(bone => bone.name.EndsWith(":LeftHand"));
        if (sword == null || leftHand == null) throw new System.InvalidOperationException("The prototype rig has no weapon mounts.");
        var grip = CreateWeaponMount("Spear", sword.parent, sword.position, soldier.transform.rotation);
        BoxVisual(grip, "Shaft", Vector3.up * .43f, new Vector3(.04f, 1.85f, .04f), false);
        BoxVisual(grip, "Point", Vector3.up * 1.39f, new Vector3(.085f, .24f, .045f), false)
            .transform.localRotation = Quaternion.Euler(0, 0, 20);
        Object.DestroyImmediate(sword.gameObject);

        var shieldMount = CreateWeaponMount("Shield", leftHand, leftHand.position, soldier.transform.rotation);
        var shield = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shield.name = "Shield face";
        shield.transform.SetParent(shieldMount, false);
        shield.transform.localPosition = Vector3.forward * .09f;
        shield.transform.localRotation = Quaternion.Euler(90, 0, 0);
        shield.transform.localScale = new Vector3(.58f, .025f, .58f);
        Object.DestroyImmediate(shield.GetComponent<Collider>());
    }

    private static void ConfigureCavalryVisual(GameObject rider, NavMeshAgent agent)
    {
        foreach (Transform child in rider.transform)
            if (child.name != "Indicator") child.localPosition += Vector3.up * 1.15f;
        var horse = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Learnig/Episode11_Assets_RTS/StableHorse.prefab");
        var mountedModel = new GameObject("Mounted horse model").transform;
        mountedModel.SetParent(rider.transform, false);
        CopyMeshes(horse.transform, mountedModel);
        agent.radius = Mathf.Max(agent.radius, .65f);
        if (rider.TryGetComponent<CapsuleCollider>(out var capsule))
        {
            capsule.radius = Mathf.Max(capsule.radius, .55f);
            capsule.height = Mathf.Max(capsule.height, 2.8f);
            capsule.center = Vector3.up * (capsule.height * .5f);
        }
    }

    private static Transform CreateWeaponMount(string name, Transform parent, Vector3 position, Quaternion rotation)
    {
        var mount = new GameObject(name).transform;
        mount.SetPositionAndRotation(position, rotation);
        mount.SetParent(parent, worldPositionStays: true);
        return mount;
    }

    private static RtsNetworkUnit RefreshPrefabIdentity(GameObject prefab)
    {
        RefreshNetworkObjectIdentity(prefab);
        return prefab.GetComponent<RtsNetworkUnit>();
    }

    private static void RefreshNetworkObjectIdentity(GameObject prefab)
    {
        // NGO 1.12 validates new components before the prefab has its own asset GUID.
        // Run its editor validation once more on the saved asset.
        var networkObject = prefab.GetComponent<NetworkObject>();
        var validate = typeof(NetworkObject).GetMethod("OnValidate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (validate == null) throw new System.InvalidOperationException("NGO prefab validation API is unavailable.");
        validate.Invoke(networkObject, null);
        PrefabUtility.SavePrefabAsset(prefab);
    }
}
