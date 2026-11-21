using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using _Game.Scripts.UI;

/// <summary>Inert unless explicitly started with -rtsSmokeHost or -rtsSmokeClient.</summary>
public sealed class RtsNetworkSmokeProbe : MonoBehaviour
{
    private IEnumerator Start()
    {
        var args = Environment.GetCommandLineArgs();
        bool host = Array.IndexOf(args, "-rtsSmokeHost") >= 0;
        bool client = Array.IndexOf(args, "-rtsSmokeClient") >= 0;
        if (!host && !client) yield break;
        yield return null;
        var session = GetComponent<RtsNetworkSession>();
        int smokePort = Array.IndexOf(args, "-rtsSmokePort");
        if (smokePort >= 0 && smokePort + 1 < args.Length) session.SetPort(args[smokePort + 1]);
        bool timber = Array.IndexOf(args, "-rtsTimberSmoke") >= 0;
        bool stone = Array.IndexOf(args, "-rtsStoneSmoke") >= 0;
        bool bread = Array.IndexOf(args, "-rtsBreadSmoke") >= 0;
        bool farm = Array.IndexOf(args, "-rtsFarmSmoke") >= 0;
        bool roads = Array.IndexOf(args, "-rtsRoadSmoke") >= 0;
        bool spearman = Array.IndexOf(args, "-rtsSpearmanSmoke") >= 0;
        bool archer = Array.IndexOf(args, "-rtsArcherSmoke") >= 0;
        bool cavalry = Array.IndexOf(args, "-rtsCavalrySmoke") >= 0;
        bool buildingAttack = Array.IndexOf(args, "-rtsBuildingCombatSmoke") >= 0;
        bool stable = cavalry || Array.IndexOf(args, "-rtsStableSmoke") >= 0;
        bool equipment = spearman || Array.IndexOf(args, "-rtsEquipmentSmoke") >= 0;
        bool iron = Array.IndexOf(args, "-rtsIronSmoke") >= 0;
        bool combat = Array.IndexOf(args, "-rtsCombatSmoke") >= 0;
        bool retaliation = Array.IndexOf(args, "-rtsRetaliationSmoke") >= 0;
        bool holdStop = Array.IndexOf(args, "-rtsHoldStopSmoke") >= 0;
        bool attackMove = Array.IndexOf(args, "-rtsAttackMoveSmoke") >= 0;
        bool patrol = Array.IndexOf(args, "-rtsPatrolSmoke") >= 0;
        bool retreat = Array.IndexOf(args, "-rtsRetreatSmoke") >= 0;
        bool formation = Array.IndexOf(args, "-rtsFormationSmoke") >= 0;
        if (formation) session.SmokeExtraTrainingKits = 1;
        session.SmokeTrainingKits = combat || retaliation || holdStop || attackMove || patrol || retreat
            || (!timber && !stone && !bread && !farm && !roads && !equipment && !iron && !archer && !stable);
        if (host && equipment)
            foreach (var station in FindObjectsOfType<RtsNetworkConstruction>()) station.UseEquipmentSmokeStock(spearman);
        if (host && stable)
            foreach (var station in FindObjectsOfType<RtsNetworkConstruction>())
            {
                station.UseStableSmokeStock();
                if (cavalry) station.UseEquipmentSmokeStock();
            }
        if (host && farm)
            foreach (var station in FindObjectsOfType<RtsNetworkConstruction>()) station.UseFarmSmokeStock();
        if (host && bread)
            foreach (var station in FindObjectsOfType<RtsNetworkConstruction>()) station.UseBreadSmokeStock();
        if (host && (timber || stone))
            foreach (var station in FindObjectsOfType<RtsNetworkConstruction>()) station.UseSupplySmokeStock(stone);
        if (host) session.StartHost(); else session.StartClient();
        float deadline = Time.realtimeSinceStartup + 40;
        while (!session.Ready && Time.realtimeSinceStartup < deadline) yield return null;
        if (!session.Ready) { Fail("connection timeout: " + session.Status); yield break; }
        if (buildingAttack) { yield return RunBuildingCombat(session, host); yield break; }
        if (formation) { yield return RunFormation(session, host); yield break; }
        if (archer) { yield return RunArcher(session, host); yield break; }
        if (stable) { yield return RunStable(session, host, cavalry); yield break; }
        if (combat || retaliation || holdStop || attackMove || patrol || retreat)
        { yield return RunCombat(session, host, retaliation, holdStop, attackMove, patrol, retreat); yield break; }
        if (iron) { yield return RunIron(session, host); yield break; }
        if (equipment) { yield return RunEquipment(session, host, spearman); yield break; }
        if (roads) { yield return RunRoads(session, host); yield break; }
        if (bread || farm) { yield return RunBread(session, host, farm); yield break; }
        if (timber || stone) { yield return RunSupply(session, host, stone); yield break; }
        while (FindObjectsOfType<RtsNetworkUnit>().Length < 4 && Time.realtimeSinceStartup < deadline) yield return null;
        yield return new WaitForSecondsRealtime(2);
        var units = FindObjectsOfType<RtsNetworkUnit>()
            .Where(unit => !unit.IsVillager && !unit.IsSwordsman && !unit.IsBuilder)
            .ToArray();
        RtsNetworkUnit own = null;
        foreach (var unit in units)
        {
            if (unit.PlayerId == session.LocalPlayerId) own = unit;
            else
            {
                ManagerGO.Instance.DeselectAll();
                ManagerGO.Instance.DragSelect(unit.gameObject);
                if (ManagerGO.Instance.unitsSelected.Count != 0) { Fail("foreign unit selected"); yield break; }
            }
        }
        if (own == null) { Fail("owner missing"); yield break; }
        var destination = own.transform.position + Vector3.forward * 8;
        ManagerGO.Instance.DragSelect(own.gameObject);
        Debug.Log($"RTS_SMOKE_ORDER player={session.LocalPlayerId} selected={ManagerGO.Instance.unitsSelected.Count} position={own.transform.position} target={destination} spawned={own.IsSpawned}");
        ManagerGO.Instance.IssueNetworkMove(destination);
        yield return new WaitForSecondsRealtime(host ? 11 : 7);
        if (Vector2.Distance(new Vector2(own.transform.position.x, own.transform.position.z), new Vector2(destination.x, destination.z)) > .6f)
        {
            var agent = own.GetComponent<UnityEngine.AI.NavMeshAgent>();
            Debug.Log($"RTS_SMOKE_AGENT position={own.transform.position} enabled={agent.enabled} onMesh={agent.isOnNavMesh} hasPath={agent.hasPath} remaining={agent.remainingDistance}");
            Fail("normal movement command did not arrive"); yield break;
        }
        foreach (var unit in units)
        {
            if (unit.PlayerId <= 2 && Mathf.Abs(unit.transform.position.z - 8) > .6f)
            { Fail("peer movement was not replicated"); yield break; }
            Debug.Log($"RTS_SMOKE_POSITION owner={unit.PlayerId} id={unit.EntityId} position={unit.transform.position:F3}");
        }
        if (!host) session.SpawnReserveUnit();
        deadline = Time.realtimeSinceStartup + 12;
        RtsNetworkUnit reserve;
        while ((reserve = FindReserve()) == null && Time.realtimeSinceStartup < deadline) yield return null;
        if (reserve == null) { Fail("client reserve was not replicated"); yield break; }
        Debug.Log($"RTS_SMOKE_SPAWN player={session.LocalPlayerId} owner={reserve.PlayerId} id={reserve.EntityId}");
        if (!host)
        {
            // Send a duplicate through the same UI command after the server cooldown.
            yield return new WaitForSecondsRealtime(.5f);
            session.SpawnReserveUnit();
            yield return new WaitForSecondsRealtime(5);
            if (FindObjectsOfType<RtsNetworkUnit>().Count(IsClientReserve) != 1)
            { Fail("duplicate reserve was spawned"); yield break; }
            ManagerGO.Instance.DeselectAll();
            ManagerGO.Instance.DragSelect(reserve.gameObject);
            session.RemoveSelectedReserveUnit();
        }
        deadline = Time.realtimeSinceStartup + 12;
        while (FindReserve() != null && Time.realtimeSinceStartup < deadline) yield return null;
        if (FindReserve() != null || ManagerGO.Instance.unitsSelected.Any(unit => unit == null))
        { Fail("reserve was not removed cleanly"); yield break; }
        Debug.Log("RTS_SMOKE_DESPAWN player=" + session.LocalPlayerId);

        var barracks = FindObjectsOfType<RtsNetworkBarracks>().SingleOrDefault(building => building.PlayerId == 2);
        var hostBarracks = FindObjectsOfType<RtsNetworkBarracks>().SingleOrDefault(building => building.PlayerId == 1);
        var villager = FindObjectsOfType<RtsNetworkUnit>().FirstOrDefault(unit => unit.IsVillager && unit.PlayerId == 2 && unit.transform.position.z < -8);
        if (barracks == null || hostBarracks == null || villager == null || !villager.IsSpawned)
        { Fail("training fixture is missing"); yield break; }
        if (barracks.QueueCount != 0 || barracks.SwordCount != 1 || barracks.ShieldCount != 1)
        { Fail("client barracks did not start with one complete kit"); yield break; }

        ulong villagerId = villager.EntityId;
        Vector3 villagerStart = villager.transform.position;
        int hostSwordCount = hostBarracks.SwordCount;
        int hostShieldCount = hostBarracks.ShieldCount;
        if (!host)
        {
            // Exercise the server-side ownership check, not just the local button guard.
            hostBarracks.TrainSwordsman();
            yield return new WaitForSecondsRealtime(1);
            barracks.TrainSwordsman();
        }
        deadline = Time.realtimeSinceStartup + 20;
        bool sawQueue = false;
        bool sawProgress = false;
        bool sawRecruitMove = false;
        RtsNetworkUnit swordsman = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            sawQueue |= barracks.QueueCount == 1;
            sawProgress |= barracks.Progress > 0f;
            sawRecruitMove |= villager != null && Vector3.Distance(villager.transform.position, villagerStart) > 1f;
            if (hostBarracks.QueueCount != 0 || hostBarracks.SwordCount != hostSwordCount
                || hostBarracks.ShieldCount != hostShieldCount)
            { Fail("client command changed the host's barracks"); yield break; }
            swordsman = FindObjectsOfType<RtsNetworkUnit>()
                .FirstOrDefault(unit => unit.IsSpawned && unit.IsSwordsman && unit.PlayerId == 2);
            if (swordsman != null && villager == null && barracks.QueueCount == 0
                && barracks.SwordCount == 0 && barracks.ShieldCount == 0) break;
            yield return null;
        }
        if (!sawQueue || !sawProgress || !sawRecruitMove || swordsman == null || swordsman.EntityId == villagerId
            || villager != null || barracks.QueueCount != 0
            || barracks.SwordCount != 0 || barracks.ShieldCount != 0
            || FindObjectsOfType<RtsNetworkUnit>().Count(unit => unit.IsSpawned && unit.IsSwordsman && unit.PlayerId == 2) != 1)
        { Fail($"training did not replace one villager and spend one kit (queue={sawQueue}, progress={sawProgress}, recruitMoved={sawRecruitMove})"); yield break; }
        if (host && (!swordsman.TryGetComponent<GettingResources>(out var gear)
            || gear.GetStoredAmount(ResourceType.Sword) != 1 || gear.GetStoredAmount(ResourceType.Shield) != 1))
        { Fail("host did not transfer the spent kit to the swordsman"); yield break; }
        Debug.Log($"RTS_SMOKE_TRAIN player={session.LocalPlayerId} villager={villagerId} swordsman={swordsman.EntityId} queue={barracks.QueueCount} sword={barracks.SwordCount} shield={barracks.ShieldCount}");

        var rally = new Vector3(-3f, 0f, -10f);
        if (!host)
        {
            ManagerGO.Instance.DeselectAll();
            ManagerGO.Instance.DragSelect(swordsman.gameObject);
            if (!ManagerGO.Instance.unitsSelected.Contains(swordsman.gameObject))
            { Fail("client could not select its trained swordsman"); yield break; }
            ManagerGO.Instance.IssueNetworkMove(rally);
        }
        deadline = Time.realtimeSinceStartup + 15;
        while (swordsman != null && !Reached(swordsman, rally) && Time.realtimeSinceStartup < deadline) yield return null;
        if (swordsman == null || !Reached(swordsman, rally))
        { Fail("trained swordsman's move was not replicated"); yield break; }
        Debug.Log($"RTS_SMOKE_TRAINED_MOVE player={session.LocalPlayerId} id={swordsman.EntityId} position={swordsman.transform.position:F3}");

        var construction = FindObjectsOfType<RtsNetworkConstruction>().SingleOrDefault(station => station.PlayerId == 2);
        var foreignConstruction = FindObjectsOfType<RtsNetworkConstruction>().SingleOrDefault(station => station.PlayerId == 1);
        var builder = FindObjectsOfType<RtsNetworkUnit>().SingleOrDefault(unit => unit.IsBuilder && unit.PlayerId == 2);
        if (construction == null || foreignConstruction == null || builder == null || !builder.IsSpawned
            || construction.WoodStock != RtsNetworkConstruction.StartingWood || foreignConstruction.WoodStock != RtsNetworkConstruction.StartingWood
            || construction.ReservedWood != 0 || construction.CargoWood != 0)
        { Fail("construction fixture did not start with separate stocked warehouses and a builder"); yield break; }

        if (!host)
        {
            foreignConstruction.RequestBuildAt(new Vector3(-9f, 0f, 22f));
            yield return new WaitForSecondsRealtime(1f);
            construction.RequestBuildAt(new Vector3(200f, 0f, 200f));
            yield return new WaitForSecondsRealtime(1f);
            if (FindObjectsOfType<RtsNetworkBuildSite>().Length != 0 || construction.ReservedWood != 0
                || construction.WoodStock != RtsNetworkConstruction.StartingWood)
            { Fail("unauthorized or out-of-zone construction changed world state"); yield break; }
            construction.RequestBuildAt(new Vector3(-3f, 0f, 22f));
        }

        deadline = Time.realtimeSinceStartup + 45;
        bool reservedBeforePickup = false;
        bool loadedAtWarehouse = false;
        bool physicallyCarried = false;
        bool sawBuildingProgress = false;
        Vector3 loadedPosition = default;
        RtsNetworkBuildSite site = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            var sites = FindObjectsOfType<RtsNetworkBuildSite>();
            if (foreignConstruction.WoodStock != RtsNetworkConstruction.StartingWood || foreignConstruction.ReservedWood != 0
                || sites.Any(building => building.PlayerId == 1))
            { Fail("client command changed the host's warehouse or construction"); yield break; }

            site = sites.SingleOrDefault(building => building.PlayerId == 2);
            if (site != null)
            {
                if (site.Progress > 0f && site.DeliveredWood != 6)
                { Fail("building progress began before unloading all wood"); yield break; }
                if (!reservedBeforePickup && construction.ReservedWood == 6 && construction.WoodStock == RtsNetworkConstruction.StartingWood
                    && construction.CargoWood == 0 && site.DeliveredWood == 0 && site.Progress == 0f)
                {
                    reservedBeforePickup = true;
                    Debug.Log($"RTS_SMOKE_BUILD_RESERVED player={session.LocalPlayerId} site={site.EntityId} stock={construction.WoodStock} reserved=6");
                }
                if (construction.WoodStock == RtsNetworkConstruction.StartingWood - 6 && construction.CargoWood == 6 && site.DeliveredWood == 0 && site.Progress == 0f)
                {
                    if (!loadedAtWarehouse)
                    {
                        loadedAtWarehouse = true;
                        loadedPosition = builder.transform.position;
                    }
                    if (!physicallyCarried && Vector3.Distance(builder.transform.position, loadedPosition) > 1f)
                    {
                        physicallyCarried = true;
                        Debug.Log($"RTS_SMOKE_BUILD_HAUL player={session.LocalPlayerId} site={site.EntityId} stock={construction.WoodStock} cargo=6");
                    }
                }
                sawBuildingProgress |= site.DeliveredWood == 6 && site.Progress > 0f && !site.IsComplete;
                if (site.IsComplete && construction.CargoWood == 0 && construction.ReservedWood == 0) break;
            }
            yield return null;
        }
        if (site == null || !site.IsComplete || !reservedBeforePickup || !loadedAtWarehouse || !physicallyCarried
            || !sawBuildingProgress || site.DeliveredWood != 6 || construction.WoodStock != RtsNetworkConstruction.StartingWood - 6
            || construction.CargoWood != 0 || construction.ReservedWood != 0
            || FindObjectsOfType<RtsNetworkBuildSite>().Length != 1)
        {
            Fail($"construction did not finish one paid, physically supplied build (reserved={reservedBeforePickup}, loaded={loadedAtWarehouse}, hauled={physicallyCarried}, built={sawBuildingProgress}, stock={construction.WoodStock}, cargo={construction.CargoWood}, progress={site?.Progress})");
            yield break;
        }
        Debug.Log($"RTS_SMOKE_BUILD_COMPLETE player={session.LocalPlayerId} site={site.EntityId} stock={construction.WoodStock} delivered={site.DeliveredWood} cargo={construction.CargoWood}");
        if (host)
        {
            deadline = Time.realtimeSinceStartup + 15;
            while (!FindObjectsOfType<RtsNetworkBuildSite>().Any(item => item.IsSpawned && item.BuildingId == RtsNetworkConstruction.HouseId)
                && Time.realtimeSinceStartup < deadline) yield return null;
            // Only the smoke player accelerates long deliveries and the standard training time.
            Time.timeScale = 4;
            foreignConstruction.RequestBuildAt(RtsNetworkConstruction.BarracksId, new Vector3(-10.5f, 0, 19), 1);
        }
        else
        {
            construction.RequestBuildAt(RtsNetworkConstruction.HouseId, new Vector3(-4.5f, 0, 19), 0);
            yield return new WaitForSecondsRealtime(.5f);
            construction.RequestBuildAt(RtsNetworkConstruction.GranaryId, new Vector3(-3.5f, 0, 26), 0);
            yield return new WaitForSecondsRealtime(.5f);
            construction.RequestBuildAt(RtsNetworkConstruction.HouseId, new Vector3(-4.5f, 0, 19), 1);
        }
        deadline = Time.realtimeSinceStartup + 100;
        RtsNetworkBuildSite house = null, granary = null, builtBarracks = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            var sites = FindObjectsOfType<RtsNetworkBuildSite>().Where(item => item.IsSpawned).ToArray();
            house = sites.FirstOrDefault(item => item.PlayerId == 2 && item.BuildingId == RtsNetworkConstruction.HouseId);
            granary = sites.FirstOrDefault(item => item.PlayerId == 2 && item.BuildingId == RtsNetworkConstruction.GranaryId);
            builtBarracks = sites.FirstOrDefault(item => item.PlayerId == 1 && item.BuildingId == RtsNetworkConstruction.BarracksId);
            if (sites.Count(item => item.PlayerId == 2 && item.BuildingId == RtsNetworkConstruction.HouseId) > 1)
            { Fail("occupied catalog cells accepted duplicate building"); yield break; }
            if (house != null && house.IsComplete && granary != null && granary.IsComplete && builtBarracks != null && builtBarracks.IsComplete) break;
            yield return null;
        }
        if (house == null || !house.IsComplete || house.HousingCapacity != 5 || house.DeliveredWood != 10
            || granary == null || !granary.IsComplete || granary.GranaryCapacity != 300 || granary.DeliveredWood != 30 || granary.DeliveredStone != 10
            || builtBarracks == null || !builtBarracks.IsComplete || builtBarracks.DeliveredWood != 50
            || builtBarracks.DeliveredStone != 50 || builtBarracks.DeliveredWheat != 40)
        { Fail($"catalog builds did not finish with expected costs and capacities (house={house?.Progress}, granary={granary?.Progress}, barracks={builtBarracks?.Progress})"); yield break; }
        var training = builtBarracks.GetComponent<RtsNetworkBarracks>();
        if (host) foreignConstruction.TrainAtBuiltBarracks();
        deadline = Time.realtimeSinceStartup + 35;
        while (Time.realtimeSinceStartup < deadline
            && (training.SwordCount != 0 || !FindObjectsOfType<RtsNetworkUnit>().Any(unit => unit.IsSpawned && unit.IsSwordsman && unit.PlayerId == 1))) yield return null;
        if (training.SwordCount != 0 || !FindObjectsOfType<RtsNetworkUnit>().Any(unit => unit.IsSpawned && unit.IsSwordsman && unit.PlayerId == 1))
        { Fail("new barracks did not train the host's resident"); yield break; }
        Debug.Log($"RTS_SMOKE_CATALOG player={session.LocalPlayerId} house={house.EntityId} granary={granary.EntityId} barracks={builtBarracks.EntityId}");
        Debug.Log("RTS_SMOKE_PASS player=" + session.LocalPlayerId);
        // Keep host alive while the client completes its observation.
        if (host) yield return new WaitForSecondsRealtime(2);
        session.Disconnect();
        Application.Quit(0);
    }

    private IEnumerator RunRoads(RtsNetworkSession session, bool host)
    {
        var station = FindObjectsOfType<RtsNetworkConstruction>().Single(item => item.PlayerId == 2);
        var millPoint = new Vector3(-4.5f, 0, 23f);
        var door = new Vector3(-4.5f, 0, 21.3f);
        var roadPoint = new Vector3(-4.5f, 0, 19f);
        if (RoadBuilder.Instance.HasConnection(door, 1.25f, 2f))
        { Fail("road fixture is already connected"); yield break; }
        if (host) Time.timeScale = 4;
        else
        {
            station.RequestBuildAt(RtsNetworkConstruction.MillId, millPoint, 0);
            yield return new WaitForSecondsRealtime(.7f);
            if (FindBuilding(RtsNetworkConstruction.MillId) != null)
            { Fail("disconnected mill was approved"); yield break; }
            session.RequestRoad(new[] { new Vector3(-9f, 0, 17f), new Vector3(-9f, 0, 18f) });
            yield return new WaitForSecondsRealtime(.7f);
            if (session.BuiltRoadCount != 0) { Fail("foreign road was approved"); yield break; }
            var points = new System.Collections.Generic.List<Vector3>();
            RoadCurve.Sample(new[] { new Vector3(-4.5f, 0, 17f), door }, .5f, points, Vector3.zero, Vector3.zero);
            session.RequestRoad(points.ToArray(), roundStart: true);
        }
        float deadline = Time.realtimeSinceStartup + 20;
        while (session.BuiltRoadCount != 1 && Time.realtimeSinceStartup < deadline) yield return null;
        if (session.BuiltRoadCount != 1 || !RoadBuilder.Instance.HasConnection(door, 1.25f, 2f)
            || !RoadBuilder.IsOnRoad(roadPoint))
        { Fail("new road was not registered on both processes"); yield break; }
        Debug.Log("RTS_SMOKE_ROAD_ACCEPTED player=" + session.LocalPlayerId);
        if (host)
        {
            bool roadAreaPresent = false;
            int area = RoadNavigation.RoadArea;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (area >= 0 && UnityEngine.AI.NavMesh.SamplePosition(roadPoint, out _, .4f, 1 << area))
                { roadAreaPresent = true; break; }
                yield return null;
            }
            if (!roadAreaPresent) { Fail("host NavMesh has no road area on the new segment"); yield break; }
        }
        else station.RequestBuildAt(RtsNetworkConstruction.MillId, millPoint, 0);
        deadline = Time.realtimeSinceStartup + 45;
        RtsNetworkBuildSite mill = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            mill = FindBuilding(RtsNetworkConstruction.MillId);
            if (mill != null && mill.IsComplete && mill.HasRoadAccess) break;
            yield return null;
        }
        if (mill == null || !mill.IsComplete || !mill.HasRoadAccess)
        { Fail("approved road did not allow the mill to open"); yield break; }
        Debug.Log($"RTS_SMOKE_ROAD_PASS player={session.LocalPlayerId} mill={mill.EntityId}");
        if (host) yield return new WaitForSecondsRealtime(2);
        session.Disconnect();
        Application.Quit(0);
    }

    private IEnumerator RunCombat(RtsNetworkSession session, bool host, bool retaliation, bool holdStop, bool attackMove, bool patrol, bool retreat)
    {
        const int fullHealth = 140;
        const int hitDamage = 18;
        RtsNetworkBarracks ownBarracks = null;
        bool starterReady = false;
        float deadline = Time.realtimeSinceStartup + 30f;
        while (Time.realtimeSinceStartup < deadline)
        {
            ownBarracks = FindObjectsOfType<RtsNetworkBarracks>()
                .SingleOrDefault(building => building.IsSpawned && building.PlayerId == session.LocalPlayerId);
            if (ownBarracks != null && ownBarracks.SwordCount == 1 && ownBarracks.ShieldCount == 1)
            {
                starterReady = FindObjectsOfType<RtsNetworkUnit>().Any(unit => unit.IsSpawned && unit.IsVillager
                    && unit.PlayerId == session.LocalPlayerId && unit.GetComponent<Unit>().HasStarted
                    && unit.GetComponent<Unit>().unitHealth > 0
                    && Vector3.Distance(unit.transform.position, ownBarracks.transform.position) < 9f
                    && (!host || unit.GetComponent<UnityEngine.AI.NavMeshAgent>().isOnNavMesh));
                if (starterReady && (!host || session.manager.ConnectedClientsList.Count >= 2)) break;
            }
            yield return null;
        }
        if (ownBarracks == null || !starterReady || (host && session.manager.ConnectedClientsList.Count < 2))
        { Fail("combat fixture needs a joined peer, living recruit and starting equipment"); yield break; }
        yield return new WaitForSecondsRealtime(.5f);
        ownBarracks.TrainSwordsman();

        deadline = Time.realtimeSinceStartup + (host ? 35f : 25f);
        RtsNetworkUnit attacker = null, defender = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            var swordsmen = FindObjectsOfType<RtsNetworkUnit>().Where(unit => unit.IsSpawned && unit.IsSwordsman).ToArray();
            attacker = swordsmen.SingleOrDefault(unit => unit.PlayerId == 2);
            defender = swordsmen.SingleOrDefault(unit => unit.PlayerId == 1);
            if (attacker != null && defender != null && attacker.Health == fullHealth && defender.Health == fullHealth) break;
            yield return null;
        }
        if (attacker == null || defender == null || attacker.Health != fullHealth || defender.Health != fullHealth
            || attacker.MaxHealth != fullHealth || defender.MaxHealth != fullHealth)
        { Fail($"combat fixture did not train two fresh swordsmen (attacker={attacker?.Health}, target={defender?.Health}, training={ownBarracks.Status})"); yield break; }

        if (holdStop)
        {
            yield return VerifyHoldStop(session, host, attacker, defender, fullHealth);
            yield break;
        }

        RtsNetworkUnit target = retaliation || retreat ? defender : FindObjectsOfType<RtsNetworkUnit>().SingleOrDefault(unit =>
            unit.IsSpawned && unit.PlayerId == 1 && !unit.IsVillager && !unit.IsSwordsman && !unit.IsBuilder && !unit.IsSupplyWorker);
        if (target == null || target.Health != target.MaxHealth || target.Health <= 0f)
        { Fail("combat fixture needs an undamaged target"); yield break; }
        int targetInitialHealth = Mathf.RoundToInt(target.Health);
        if (attackMove || patrol)
        {
            yield return VerifyCombatTravel(session, host, attacker, target, fullHealth, hitDamage, patrol);
            yield break;
        }

        var entry = attacker.transform.position;
        ulong attackerId = attacker.EntityId, targetId = target.EntityId;
        if (Vector3.Distance(entry, target.transform.position) < 4f)
        { Fail("combat fixture did not start outside melee range"); yield break; }
        if (!host)
        {
            defender.RequestAttack(attacker);
            attacker.RequestAttack(attacker);
            yield return new WaitForSecondsRealtime(3f);
            if (attacker == null || target == null || attacker.Health != fullHealth || target.Health != targetInitialHealth
                || Vector3.Distance(entry, attacker.transform.position) > .25f)
            { Fail("a foreign or friendly attack command changed combat state"); yield break; }
            ManagerGO.Instance.DeselectAll();
            ManagerGO.Instance.DragSelect(attacker.gameObject);
            if (ManagerGO.Instance.unitsSelected.Count != 1 || ManagerGO.Instance.unitsSelected[0] != attacker.gameObject)
            { Fail("client could not select its own swordsman for the normal attack command"); yield break; }
            ManagerGO.Instance.IssueNetworkAttack(target);
        }

        if (retaliation || retreat)
        {
            yield return VerifyRetaliation(session, host, attacker, defender, fullHealth, hitDamage, retreat);
            yield break;
        }

        int previousHealth = targetInitialHealth;
        var partialHealth = new System.Collections.Generic.List<int>();
        bool sawApproachBeforeDamage = false;
        deadline = Time.realtimeSinceStartup + 30f;
        while (target != null && Time.realtimeSinceStartup < deadline)
        {
            if (attacker == null || !attacker.IsSpawned || attacker.Health != fullHealth)
            { Fail("the defender received an unauthorized attack command"); yield break; }
            int health = Mathf.RoundToInt(target.Health);
            if (health == targetInitialHealth)
                sawApproachBeforeDamage |= Vector3.Distance(entry, attacker.transform.position) > 1f;
            else if (health > 0 && health != previousHealth)
            {
                if (health >= previousHealth || (targetInitialHealth - health) % hitDamage != 0)
                { Fail($"unexpected health after melee hit ({previousHealth} to {health})"); yield break; }
                partialHealth.Add(health);
                previousHealth = health;
            }
            yield return null;
        }
        if (target != null || session.manager.SpawnManager.SpawnedObjects.ContainsKey(targetId)
            || attacker == null || !attacker.IsSpawned || attacker.Health != fullHealth
            || partialHealth.Count < 3 || previousHealth != (targetInitialHealth - 1) % hitDamage + 1
            || (host && !sawApproachBeforeDamage))
        {
            Fail($"combat did not finish as expected (chased={sawApproachBeforeDamage}, health=[{string.Join(",", partialHealth)}], target={target != null}, attacker={attacker?.Health})");
            yield break;
        }
        Debug.Log($"RTS_SMOKE_COMBAT_PASS player={session.LocalPlayerId} attacker={attackerId} target={targetId} health=[{string.Join(",", partialHealth)}] despawn=true");
        if (host) yield return new WaitForSecondsRealtime(2f);
        session.Disconnect();
        Application.Quit(0);
    }

    private IEnumerator VerifyCombatTravel(RtsNetworkSession session, bool host, RtsNetworkUnit attacker,
        RtsNetworkUnit target, int fullHealth, int hitDamage, bool patrol)
    {
        bool IsOrdered() => attacker != null && (patrol ? attacker.IsPatrolling : attacker.IsAttackMoving);
        string smokeOrder = patrol ? "PATROL" : "ATTACK_MOVE";
        var input = ManagerGO.Instance;
        input.DeselectAll();
        var selected = host ? target : attacker;
        input.DragSelect(selected.gameObject);
        if (input.unitsSelected.Count != 1 || input.unitsSelected[0] != selected.gameObject)
        { Fail("could not select the local unit for attack-move"); yield break; }

        // Move the existing capsule away from other factions so the standard target search
        // stays enabled without a test-only target filter.
        var start = new Vector3(-28f, 0f, -10f);
        var crossing = new Vector3(-23f, 0f, -10f);
        var destination = new Vector3(-19.5f, 0f, -10f);
        int initialHealth = Mathf.RoundToInt(target.Health);
        ulong attackerId = attacker.EntityId, targetId = target.EntityId;
        input.IssueNetworkMove(host ? crossing : start);
        float deadline = Time.realtimeSinceStartup + 22f;
        while ((!Reached(attacker, start) || !Reached(target, crossing)) && Time.realtimeSinceStartup < deadline) yield return null;
        if (!Reached(attacker, start) || !Reached(target, crossing) || target.Health != initialHealth || attacker.Health != fullHealth)
        { Fail($"attack-move fixture did not stage (attacker={attacker.transform.position}, target={target.transform.position})"); yield break; }

        if (!host)
        {
            // Both peers need to observe the settled starting positions before contact.
            yield return new WaitForSecondsRealtime(.7f);
            if (patrol)
            {
                input.BeginPatrol();
                if (!input.IsChoosingPatrol)
                { Fail("patrol did not enter route point selection"); yield break; }
                input.IssueNetworkPatrol(new[] { destination, start });
            }
            else
            {
                input.BeginAttackMove();
                if (!input.IsChoosingAttackMove)
                { Fail("attack-move did not enter ground point selection"); yield break; }
                input.IssueNetworkAttackMove(destination);
            }
        }

        bool sawOrder = false;
        int lastHealth = initialHealth, numberOfUpdates = 0;
        deadline = Time.realtimeSinceStartup + 25f;
        while (attacker != null && target != null && Time.realtimeSinceStartup < deadline)
        {
            sawOrder |= IsOrdered();
            int targetHealth = Mathf.RoundToInt(target.Health);
            if (targetHealth < initialHealth && targetHealth > 0 && targetHealth != lastHealth)
            {
                if (targetHealth >= lastHealth || (initialHealth - targetHealth) % hitDamage != 0
                    || !IsOrdered() || Reached(attacker, destination))
                { Fail($"{smokeOrder} did not stay ordered and deal ordinary damage en route"); yield break; }
                lastHealth = targetHealth;
                numberOfUpdates++;
            }
            yield return null;
        }
        if (!sawOrder || numberOfUpdates < 3 || lastHealth != (initialHealth - 1) % hitDamage + 1 || target != null
            || session.manager.SpawnManager.SpawnedObjects.ContainsKey(targetId) || attacker == null
            || !IsOrdered() || Reached(attacker, destination))
        { Fail($"{smokeOrder} did not defeat the target before arriving (order={sawOrder}, updates={numberOfUpdates}, hp={lastHealth}, target={target != null})"); yield break; }
        var positionAfterFight = attacker.transform.position;
        Debug.Log($"RTS_SMOKE_{smokeOrder}_FIGHT player={session.LocalPlayerId} attacker={attackerId} target={targetId} lastHp={lastHealth} position={positionAfterFight:F2}");

        if (patrol)
        {
            yield return VerifyPatrolCycle(session, host, attacker, start, destination, fullHealth);
            yield break;
        }
        deadline = Time.realtimeSinceStartup + 14f;
        while (attacker != null && (!Reached(attacker, destination) || attacker.IsAttackMoving)
            && Time.realtimeSinceStartup < deadline) yield return null;
        if (attacker == null || !Reached(attacker, destination) || attacker.IsAttackMoving || attacker.Health != fullHealth
            || Vector3.Distance(positionAfterFight, attacker.transform.position) < 1.5f)
        { Fail($"attack-move did not resume and finish the original route (position={attacker?.transform.position}, active={attacker?.IsAttackMoving})"); yield break; }
        Debug.Log($"RTS_SMOKE_ATTACK_MOVE_PASS player={session.LocalPlayerId} attacker={attackerId} target={targetId} position={attacker.transform.position:F2}");
        if (host) yield return new WaitForSecondsRealtime(2f);
        session.Disconnect();
        Application.Quit(0);
    }

    private IEnumerator VerifyPatrolCycle(RtsNetworkSession session, bool host, RtsNetworkUnit swordsman,
        Vector3 first, Vector3 second, int fullHealth)
    {
        var expected = new[] { second, first, second };
        int visited = 0;
        float deadline = Time.realtimeSinceStartup + 26f;
        while (swordsman != null && swordsman.IsPatrolling && visited < expected.Length
            && Time.realtimeSinceStartup < deadline)
        {
            if (Vector3.Distance(swordsman.transform.position, expected[visited]) <= .95f)
                visited++;
            yield return null;
        }
        if (swordsman == null || swordsman.Health != fullHealth || !swordsman.IsPatrolling || visited != expected.Length)
        { Fail($"patrol did not resume its route and repeat the two waypoints (visited={visited}, active={swordsman?.IsPatrolling})"); yield break; }

        deadline = Time.realtimeSinceStartup + 10f;
        while (swordsman.IsPatrolling && Vector3.Distance(swordsman.transform.position, second) < 1.7f
            && Time.realtimeSinceStartup < deadline) yield return null;
        if (!swordsman.IsPatrolling || Vector3.Distance(swordsman.transform.position, second) < 1.7f)
        { Fail("patrol did not begin another leg after its return"); yield break; }
        if (!host) ManagerGO.Instance.StopSelected();
        deadline = Time.realtimeSinceStartup + 10f;
        while (swordsman.IsPatrolling && Time.realtimeSinceStartup < deadline) yield return null;
        if (swordsman.IsPatrolling)
        { Fail("Stop did not clear the patrol order"); yield break; }
        yield return new WaitForSecondsRealtime(.7f);
        Vector3 stopped = swordsman.transform.position;
        yield return new WaitForSecondsRealtime(1f);
        if (swordsman.IsPatrolling || Vector3.Distance(swordsman.transform.position, stopped) > .25f
            || Vector3.Distance(stopped, first) < 1f || Vector3.Distance(stopped, second) < 1f)
        { Fail($"Stop did not halt the swordsman between its patrol points (at={stopped:F2})"); yield break; }
        Debug.Log($"RTS_SMOKE_PATROL_PASS player={session.LocalPlayerId} swordsman={swordsman.EntityId} visits={visited} stopped={stopped:F2}");
        if (host) yield return new WaitForSecondsRealtime(2f);
        session.Disconnect();
        Application.Quit(0);
    }

    private IEnumerator VerifyHoldStop(RtsNetworkSession session, bool host, RtsNetworkUnit visitor,
        RtsNetworkUnit guard, int fullHealth)
    {
        var input = ManagerGO.Instance;
        input.DeselectAll();
        var selected = host ? guard : visitor;
        input.DragSelect(selected.gameObject);
        if (input.unitsSelected.Count != 1 || input.unitsSelected[0] != selected.gameObject)
        { Fail("could not select the local swordsman for Hold/Stop"); yield break; }

        Vector3 departure = guard.transform.position;
        var firstDestination = new Vector3(-18f, 0f, -16f);
        if (host)
        {
            input.IssueNetworkMove(firstDestination);
            float moveDeadline = Time.realtimeSinceStartup + 10f;
            while (Vector3.Distance(departure, guard.transform.position) < 1.5f && Time.realtimeSinceStartup < moveDeadline) yield return null;
            if (Vector3.Distance(departure, guard.transform.position) < 1.5f)
            { Fail("guard did not move before the Hold order"); yield break; }
            input.HoldSelected();
        }

        float deadline = Time.realtimeSinceStartup + 15f;
        while (!guard.IsHoldingPosition && Time.realtimeSinceStartup < deadline) yield return null;
        if (!guard.IsHoldingPosition)
        { Fail("Hold mode was not active on both peers"); yield break; }
        yield return new WaitForSecondsRealtime(.6f);
        Vector3 holdPoint = guard.transform.position;
        if (Vector3.Distance(holdPoint, departure) < 1f || Vector3.Distance(holdPoint, firstDestination) < 3f)
        { Fail("Hold did not interrupt the in-progress movement"); yield break; }
        yield return new WaitForSecondsRealtime(.9f);
        if (!guard.IsHoldingPosition || Vector3.Distance(holdPoint, guard.transform.position) > .25f || visitor.Health != fullHealth)
        { Fail("guard chased or hit a distant enemy while holding"); yield break; }

        var inReach = holdPoint + Vector3.right * 1.65f;
        if (!host) input.IssueNetworkMove(inReach);
        deadline = Time.realtimeSinceStartup + 16f;
        while (visitor != null && visitor.Health == fullHealth && Time.realtimeSinceStartup < deadline) yield return null;
        if (visitor == null || visitor.Health >= fullHealth || !guard.IsHoldingPosition
            || Vector3.Distance(holdPoint, guard.transform.position) > .25f)
        { Fail($"guard did not land a stationary melee hit (enemy={visitor?.Health}, holding={guard.IsHoldingPosition})"); yield break; }
        Debug.Log($"RTS_SMOKE_HOLD_HIT player={session.LocalPlayerId} guard={guard.EntityId} enemy={visitor.EntityId} hp={visitor.Health}");

        var retreat = new Vector3(-2.5f, 0f, -13f);
        if (!host) input.IssueNetworkMove(retreat);
        deadline = Time.realtimeSinceStartup + 15f;
        while (!Reached(visitor, retreat) && Time.realtimeSinceStartup < deadline) yield return null;
        if (!Reached(visitor, retreat) || !guard.IsHoldingPosition || Vector3.Distance(holdPoint, guard.transform.position) > .25f)
        { Fail("guard left its Hold position when the target moved away"); yield break; }

        if (host)
        {
            // The peer must see the completed retreat while hold is still active.
            yield return new WaitForSecondsRealtime(.8f);
            input.StopSelected();
        }
        deadline = Time.realtimeSinceStartup + 10f;
        while (guard.IsHoldingPosition && Time.realtimeSinceStartup < deadline) yield return null;
        if (guard.IsHoldingPosition)
        { Fail("Stop did not clear Hold on both peers"); yield break; }
        Vector3 chaseStart = guard.transform.position;
        float beforeChase = visitor.Health;
        if (host)
        {
            yield return new WaitForSecondsRealtime(.5f);
            input.IssueNetworkAttack(visitor);
        }

        float minimumTravel = host ? 2f : 1.25f;
        deadline = Time.realtimeSinceStartup + 12f;
        while (Vector3.Distance(chaseStart, guard.transform.position) < minimumTravel && Time.realtimeSinceStartup < deadline) yield return null;
        if (Vector3.Distance(chaseStart, guard.transform.position) < minimumTravel)
        { Fail("guard did not pursue its explicit target after Hold was cleared"); yield break; }
        if (host) input.StopSelected();
        yield return new WaitForSecondsRealtime(.9f);
        Vector3 stopPoint = guard.transform.position;
        yield return new WaitForSecondsRealtime(1.2f);
        if (guard.IsHoldingPosition || Vector3.Distance(stopPoint, guard.transform.position) > .25f || visitor.Health != beforeChase)
        { Fail("Stop did not cancel the current chase before it could deal damage"); yield break; }
        Debug.Log($"RTS_SMOKE_STOP_STILL player={session.LocalPlayerId} guard={guard.EntityId} position={stopPoint:F2}");

        float beforeDefence = guard.Health;
        if (!host)
        {
            // Give both peers a settled health snapshot before the new explicit attack.
            yield return new WaitForSecondsRealtime(.6f);
            input.IssueNetworkAttack(guard);
        }
        bool guardWasHit = false;
        deadline = Time.realtimeSinceStartup + 18f;
        while (guard != null && visitor != null && Time.realtimeSinceStartup < deadline)
        {
            guardWasHit |= guard.Health < beforeDefence;
            if (guardWasHit && visitor.Health < beforeChase) break;
            yield return null;
        }
        if (!guardWasHit || guard == null || visitor == null || visitor.Health >= beforeChase || guard.IsHoldingPosition)
        { Fail("the stopped guard did not retaliate on a new attack"); yield break; }
        Debug.Log($"RTS_SMOKE_HOLD_STOP_PASS player={session.LocalPlayerId} guard={guard.EntityId} enemy={visitor.EntityId} hp={guard.Health}/{visitor.Health}");
        if (host) yield return new WaitForSecondsRealtime(2f);
        session.Disconnect();
        Application.Quit(0);
    }

    private IEnumerator VerifyRetaliation(RtsNetworkSession session, bool host, RtsNetworkUnit attacker,
        RtsNetworkUnit defender, int fullHealth, int hitDamage, bool retreat)
    {
        int oneHit = fullHealth - hitDamage;
        bool sawFirstStrike = false;
        float deadline = Time.realtimeSinceStartup + 20f;
        while (attacker != null && defender != null && Time.realtimeSinceStartup < deadline)
        {
            if (defender.Health == oneHit && attacker.Health == fullHealth)
                sawFirstStrike = true;
            if (attacker.Health < fullHealth) break;
            yield return null;
        }
        if (!sawFirstStrike || attacker == null || defender == null || attacker.Health != oneHit || defender.Health > oneHit)
        { Fail($"swordsman did not respond after taking the first hit (first={sawFirstStrike}, initiator={attacker?.Health}, defender={defender?.Health})"); yield break; }
        string order = retreat ? "RETREAT" : "RETALIATION";
        Debug.Log($"RTS_SMOKE_{order}_EXCHANGE player={session.LocalPlayerId} attacker={attacker.EntityId} defender={defender.EntityId} health={attacker.Health}/{defender.Health}");

        ulong attackerId = attacker.EntityId, defenderId = defender.EntityId;
        var start = defender.transform.position;
        var escape = new Vector3(-19f, 0f, -9f);
        if (host)
        {
            ManagerGO.Instance.DeselectAll();
            ManagerGO.Instance.DragSelect(defender.gameObject);
            if (ManagerGO.Instance.unitsSelected.Count != 1 || ManagerGO.Instance.unitsSelected[0] != defender.gameObject)
            { Fail("host could not select its defender"); yield break; }
            if (retreat)
            {
                ManagerGO.Instance.BeginRetreat();
                if (!ManagerGO.Instance.IsChoosingRetreat)
                { Fail("Retreat did not enter ground point selection"); yield break; }
                ManagerGO.Instance.IssueNetworkRetreat(escape);
            }
            else ManagerGO.Instance.IssueNetworkMove(escape);
        }

        bool sawTravel = false;
        bool sawRetreating = false, hitOnRoute = false;
        float defenderHealthAtStart = defender.Health;
        deadline = Time.realtimeSinceStartup + 20f;
        while (attacker != null && defender != null && Time.realtimeSinceStartup < deadline)
        {
            float fromStart = Vector3.Distance(start, defender.transform.position);
            float fromEnd = Vector3.Distance(escape, defender.transform.position);
            if (retreat && defender.IsRetreating)
            {
                sawRetreating = true;
                hitOnRoute |= defender.Health < defenderHealthAtStart && fromEnd > 1.25f;
            }
            // Compare the middle of the route as network health and transforms arrive independently.
            if (fromStart > 2f && fromEnd > 2f)
            {
                sawTravel |= !retreat || defender.IsRetreating;
                if (attacker.Health < oneHit)
                { Fail("auto-retaliation delivered another hit during the explicit move"); yield break; }
            }
            if (Reached(defender, escape) && (!retreat || !defender.IsRetreating)) break;
            yield return null;
        }
        if (!sawTravel || attacker == null || defender == null || !Reached(defender, escape)
            || (retreat && (!sawRetreating || defender.IsRetreating)))
        { Fail($"manual move did not override retaliation (travel={sawTravel}, defender={defender?.transform.position})"); yield break; }
        if (retreat)
        {
            float defenderAtArrival = defender.Health;
            Debug.Log($"RTS_SMOKE_RETREAT_ARRIVAL player={session.LocalPlayerId} defender={defenderId} hitOnRoute={hitOnRoute} health={defenderAtArrival} position={defender.transform.position:F2}");
            deadline = Time.realtimeSinceStartup + 14f;
            while (attacker != null && defender != null && Time.realtimeSinceStartup < deadline
                && (defender.Health >= defenderAtArrival || attacker.Health >= oneHit)) yield return null;
            if (attacker == null || defender == null || defender.IsRetreating
                || defender.Health >= defenderAtArrival || attacker.Health >= oneHit)
            { Fail($"arrived defender did not respond to a new incoming hit (attacker={attacker?.Health}, defender={defender?.Health}, arrival={defenderAtArrival})"); yield break; }
        }
        Debug.Log($"RTS_SMOKE_{order}_PASS player={session.LocalPlayerId} attacker={attackerId} defender={defenderId} move=true");
        if (host) yield return new WaitForSecondsRealtime(2f);
        session.Disconnect();
        Application.Quit(0);
    }

    private IEnumerator RunFormation(RtsNetworkSession session, bool host)
    {
        yield return new WaitForSecondsRealtime(2);
        var barracks = FindObjectsOfType<RtsNetworkBarracks>().Single(unit => unit.PlayerId == 2);
        if (!host) barracks.TrainSwordsman();
        float deadline = Time.realtimeSinceStartup + 40f;
        while (FormationSwordsmen().Length == 0 && Time.realtimeSinceStartup < deadline) yield return null;
        var troop = FormationSwordsmen();
        if (troop.Length != 1) { Fail("formation could not train its first swordsman"); yield break; }
        var first = troop[0];
        var rally = new Vector3(-13f, 0, -14f);
        if (!host) first.RequestMove(rally);
        deadline = Time.realtimeSinceStartup + 15f;
        while (!Reached(first, rally) && Time.realtimeSinceStartup < deadline) yield return null;
        if (!Reached(first, rally)) { Fail("first swordsman did not clear the barracks exit"); yield break; }
        if (!host) barracks.TrainSwordsman();
        deadline = Time.realtimeSinceStartup + 45f;
        while (FormationSwordsmen().Length < 2 && Time.realtimeSinceStartup < deadline) yield return null;
        troop = FormationSwordsmen();
        if (troop.Length != 2) { Fail("formation could not train its second swordsman"); yield break; }

        var original = troop.Select(unit => unit.transform.position).ToArray();
        var line = new Vector3(-22f, 0, -16f);
        if (!host)
        {
            var foreign = FindObjectsOfType<RtsNetworkUnit>().First(unit => unit.PlayerId == 1);
            session.RequestFormation(new[] { troop[0].EntityId, foreign.EntityId },
                new[] { line, line + Vector3.right * 2f }, Vector3.forward);
        }
        yield return new WaitForSecondsRealtime(.8f);
        if (troop.Where((unit, i) => unit.IsHoldingPosition || Vector3.Distance(unit.transform.position, original[i]) > .35f).Any())
        { Fail("invalid mixed-owner formation changed movement"); yield break; }

        var placement = FindObjectOfType<SoldiersFormation>();
        if (placement == null) { Fail("formation placement was not configured on the test map"); yield break; }
        var wedge = new Vector3(-19f, 0, -22f);
        for (int stage = 0; stage < 2; stage++)
        {
            var center = stage == 0 ? line : wedge;
            var facing = stage == 0 ? Vector3.forward : Vector3.right;
            if (!host)
            {
                ManagerGO.Instance.DeselectAll();
                foreach (var unit in troop) ManagerGO.Instance.DragSelect(unit.gameObject);
                if (stage == 0) placement.ToggleLinePlacement();
                else placement.ToggleWedgePlacement();
                if (!placement.PlaceFormationAt(center, stage == 0 ? 0 : 90))
                { Fail("the formation input did not send a placement"); yield break; }
            }

            var expected = new[] { center + Vector3.right * .75f, center - Vector3.right * .75f };
            deadline = Time.realtimeSinceStartup + 25f;
            while (Time.realtimeSinceStartup < deadline && !InFormation(troop, expected, facing)) yield return null;
            if (!InFormation(troop, expected, facing))
            { Fail($"formation {stage} did not reach two distinct facing slots: {DescribeFormation(troop, facing)}"); yield break; }
            var settled = troop.Select(unit => unit.transform.position).ToArray();
            yield return new WaitForSecondsRealtime(.65f);
            if (!InFormation(troop, expected, facing) || troop.Where((unit, i) => Vector3.Distance(unit.transform.position, settled[i]) > .15f).Any())
            { Fail($"formation {stage} did not stay in its slots: {DescribeFormation(troop, facing)}; previously {string.Join("/", settled.Select(point => point.ToString("F2")))}"); yield break; }
            Debug.Log($"RTS_SMOKE_FORMATION_SETTLED player={session.LocalPlayerId} shape={(stage == 0 ? "line" : "wedge")} ids={troop[0].EntityId}/{troop[1].EntityId} slots={settled[0]:F2}/{settled[1]:F2}");
            if (!host) yield return new WaitForSecondsRealtime(.6f);
        }
        var offsets = troop.Select(unit => unit.transform.position).ToArray();
        var start = (offsets[0] + offsets[1]) * .5f;
        var marchCenter = new Vector3(-27f, 0, -25f);
        var marchSlots = offsets.Select(point => marchCenter + point - start).ToArray();
        if (host) troop[1].GetComponent<BattleCombatant>().SetBaseSpeed(1.6f);
        else ManagerGO.Instance.IssueNetworkMove(marchCenter);

        bool sawTravel = false, sawSharedPace = false;
        deadline = Time.realtimeSinceStartup + 22f;
        while (Time.realtimeSinceStartup < deadline && troop.All(unit => unit != null))
        {
            if (troop.All(unit => !unit.IsHoldingPosition)
                && troop.Where((unit, i) => Vector3.Distance(unit.transform.position, offsets[i]) > 1f).Count() == troop.Length)
            {
                sawTravel = true;
                if (host)
                {
                    var squad = troop[0].GetComponent<SquadMember>()?.Squad;
                    sawSharedPace |= squad != null && troop[1].GetComponent<SquadMember>()?.Squad == squad
                        && Mathf.Abs(squad.Speed - 1.6f) < .02f
                        && troop.All(unit => Mathf.Abs(unit.GetComponent<UnityEngine.AI.NavMeshAgent>().speed - 1.6f) < .02f);
                }
            }
            if (sawTravel && InFormation(troop, marchSlots, Vector3.right)) break;
            yield return null;
        }
        if (!sawTravel || (host && !sawSharedPace) || !InFormation(troop, marchSlots, Vector3.right)
            || troop[0].HeldFormationGroup != troop[1].HeldFormationGroup
            || troop.Where((unit, i) => Vector3.Distance(unit.transform.position, marchSlots[i]) >= .45f).Any())
        { Fail($"march did not keep its assigned slots (travel={sawTravel}, pace={sawSharedPace}): {DescribeFormation(troop, Vector3.right)}"); yield break; }
        Debug.Log($"RTS_SMOKE_MARCH_PASS player={session.LocalPlayerId} ids={troop[0].EntityId}/{troop[1].EntityId} slots={troop[0].transform.position:F2}/{troop[1].transform.position:F2}");
        var halt = troop[0].transform.position;
        var staying = troop[1].transform.position;
        var far = new Vector3(-24f, 0, -16f);
        var defensePoint = new Vector3(-26f, 0, -29f);
        if (!host)
        {
            ManagerGO.Instance.DeselectAll();
            ManagerGO.Instance.DragSelect(troop[0].gameObject);
            ManagerGO.Instance.IssueNetworkMove(far);
        }
        deadline = Time.realtimeSinceStartup + 12f;
        while (Time.realtimeSinceStartup < deadline && (troop[0].IsHoldingPosition
            || Vector3.Distance(halt, troop[0].transform.position) < 1.4f)) yield return null;
        if (troop[0].IsHoldingPosition || Vector3.Distance(halt, troop[0].transform.position) < 1.4f)
        { Fail("swordsman did not start moving before defense facing"); yield break; }
        if (!host)
        {
            ManagerGO.Instance.DragSelect(troop[1].gameObject);
            ManagerGO.Instance.BeginDefenseFacing();
            if (!ManagerGO.Instance.IsChoosingDefenseFacing) { Fail("defense facing did not enter point selection"); yield break; }
            ManagerGO.Instance.FaceDefenseAt(defensePoint);
        }
        deadline = Time.realtimeSinceStartup + 10f;
        while (Time.realtimeSinceStartup < deadline && !FacingTogether(troop, defensePoint)) yield return null;
        if (!FacingTogether(troop, defensePoint) || Vector3.Distance(troop[0].transform.position, far) < 2f
            || Vector3.Distance(troop[1].transform.position, staying) > .2f)
        { Fail($"defense facing did not interrupt motion and hold positions: {DescribeFormation(troop, Vector3.back)}"); yield break; }
        var guardPositions = troop.Select(unit => unit.transform.position).ToArray();
        yield return new WaitForSecondsRealtime(.7f);
        if (!FacingTogether(troop, defensePoint)
            || troop.Where((unit, i) => Vector3.Distance(unit.transform.position, guardPositions[i]) > .2f).Any())
        { Fail("the defenders did not keep their positions and new direction"); yield break; }
        Debug.Log($"RTS_SMOKE_DEFENSE_FACING_PASS player={session.LocalPlayerId} group={troop[0].HeldFormationGroup} facing={troop[0].transform.forward:F2}");
        Debug.Log($"RTS_SMOKE_FORMATION_PASS player={session.LocalPlayerId}");
        if (host) yield return new WaitForSecondsRealtime(2f);
        session.Disconnect();
        Application.Quit(0);
    }

    private static RtsNetworkUnit[] FormationSwordsmen() => FindObjectsOfType<RtsNetworkUnit>()
        .Where(unit => unit.IsSpawned && unit.IsSwordsman && unit.PlayerId == 2).OrderBy(unit => unit.EntityId).ToArray();

    private static bool InFormation(RtsNetworkUnit[] troop, Vector3[] slots, Vector3 facing)
        => troop.All(unit => unit != null && unit.IsHoldingPosition && Vector3.Dot(unit.transform.forward, facing) > .98f)
            && slots.All(slot => troop.Any(unit => Vector3.Distance(unit.transform.position, slot) < .45f));

    private static bool FacingTogether(RtsNetworkUnit[] troop, Vector3 point)
    {
        if (troop.Any(unit => unit == null || !unit.IsHoldingPosition)
            || troop[0].HeldFormationGroup != troop[1].HeldFormationGroup) return false;
        var direction = point - (troop[0].transform.position + troop[1].transform.position) * .5f;
        direction.y = 0;
        return troop.All(unit => Vector3.Dot(unit.transform.forward, direction.normalized) > .98f);
    }

    private static string DescribeFormation(RtsNetworkUnit[] troop, Vector3 facing)
        => string.Join("/", troop.Select(unit => unit == null ? "missing"
            : $"{unit.EntityId} {unit.transform.position:F2} held={unit.IsHoldingPosition} facing={Vector3.Dot(unit.transform.forward, facing):F2}"));

    private IEnumerator RunIron(RtsNetworkSession session, bool host)
    {
        var station = FindObjectsOfType<RtsNetworkConstruction>().Single(item => item.PlayerId == 2);
        var other = FindObjectsOfType<RtsNetworkConstruction>().Single(item => item.PlayerId == 1);
        if (station.IronStock != 0 || other.IronStock != 0 || station.SwordStock != 0)
        { Fail("iron fixture must start without ore or swords"); yield break; }

        if (host) Time.timeScale = 4;
        else
        {
            other.RequestBuildAt(RtsNetworkConstruction.MineId, new Vector3(-10.5f, 0, 19f), 0);
            station.HireCarrier();
            station.RequestBuildAt(RtsNetworkConstruction.MineId, new Vector3(-4.5f, 0, 25f), 0);
            yield return new WaitForSecondsRealtime(.75f);
            if (FindBuilding(RtsNetworkConstruction.MineId) != null)
            { Fail("a mine without a road was approved"); yield break; }
            station.RequestBuildAt(RtsNetworkConstruction.MineId, new Vector3(-4.5f, 0, 19f), 0);
            yield return new WaitForSecondsRealtime(.75f);
            station.RequestBuildAt(RtsNetworkConstruction.BlacksmithId, new Vector3(-2.5f, 0, 19f), 0);
        }

        float deadline = Time.realtimeSinceStartup + 90;
        RtsNetworkBuildSite mine = null, forge = null;
        RtsNetworkIronVein vein = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            mine = FindBuilding(RtsNetworkConstruction.MineId);
            forge = FindBuilding(RtsNetworkConstruction.BlacksmithId);
            vein = FindObjectsOfType<RtsNetworkIronVein>().SingleOrDefault(item => item.IsSpawned && item.PlayerId == 2);
            if (mine != null && forge != null && mine.IsComplete && forge.IsComplete
                && mine.HasRoadAccess && forge.HasRoadAccess && vein != null && station.CarrierCount == 1) break;
            yield return null;
        }
        if (mine == null || forge == null || !mine.IsComplete || !forge.IsComplete || !mine.HasRoadAccess
            || !forge.HasRoadAccess || vein == null || vein.Remaining != 10 || station.CarrierCount != 1
            || FindObjectsOfType<RtsNetworkBuildSite>().Any(site => site.PlayerId == 1 && site.BuildingId == RtsNetworkConstruction.MineId))
        { Fail($"iron fixtures missing or unauthorized mine placed (mine={mine?.Progress}, forge={forge?.Progress}, vein={vein?.Remaining}, carriers={station.CarrierCount})"); yield break; }
        if (!host)
        {
            station.HireWorker(RtsNetworkConstruction.MineId);
            station.HireWorker(RtsNetworkConstruction.BlacksmithId);
        }

        bool sawExtraction = false, sawMineStock = false, sawCarrierToWarehouse = false;
        bool minerMovedWithOre = false, carrierMovedToWarehouse = false;
        Vector3? minerLoadedAt = null, carrierLoadedAt = null;
        deadline = Time.realtimeSinceStartup + 100;
        while (Time.realtimeSinceStartup < deadline)
        {
            bool veinDecreased = vein == null || vein.Remaining < 10;
            foreach (var unit in FindObjectsOfType<RtsNetworkUnit>().Where(unit => unit.PlayerId == 2 && unit.CarriedIron > 0))
            {
                if (unit.IsMiner)
                {
                    sawExtraction |= veinDecreased;
                    if (unit.CarriedIron == 10)
                    {
                        if (!minerLoadedAt.HasValue) minerLoadedAt = unit.transform.position;
                        minerMovedWithOre |= Vector3.Distance(minerLoadedAt.Value, unit.transform.position) > 1f;
                    }
                }
                if (unit.IsCarrier && sawMineStock)
                {
                    sawCarrierToWarehouse = true;
                    if (!carrierLoadedAt.HasValue) carrierLoadedAt = unit.transform.position;
                    carrierMovedToWarehouse |= Vector3.Distance(carrierLoadedAt.Value, unit.transform.position) > 1f;
                }
            }
            sawMineStock |= mine.MineIron > 0;
            if (other.IronStock != 0 || other.SwordStock != 0 || forge.RecipeInput != 0 || station.SwordStock != 0
                || station.IronStock > 10)
            { Fail("iron bypassed the warehouse, entered foreign stock or duplicated"); yield break; }
            if (station.IronStock == 10 && mine.MineIron == 0) break;
            yield return null;
        }
        // The host advances simulation faster than the client here. Freight snapshots and interpolated
        // transforms reach the client on separate timelines, so verify simultaneous motion and cargo on the host.
        if (!sawExtraction || !sawMineStock || !sawCarrierToWarehouse
            || (host && (!minerMovedWithOre || !carrierMovedToWarehouse)) || station.IronStock != 10
            || mine.MineIron != 0 || (vein != null && vein.Remaining != 0) || mine.WorkerCount != 1)
        {
            Fail($"iron mining/warehouse flow incomplete (extracted={sawExtraction}, mineStock={sawMineStock}, carrierCargo={sawCarrierToWarehouse}, moved={minerMovedWithOre}/{carrierMovedToWarehouse}, remaining={vein?.Remaining}, iron={station.IronStock}, mine={mine.MineIron}, workers={mine.WorkerCount})");
            yield break;
        }
        Debug.Log($"RTS_SMOKE_IRON_WAREHOUSE player={session.LocalPlayerId} mine={mine.EntityId} forge={forge.EntityId} iron=10");
        if (!host)
        {
            yield return new WaitForSecondsRealtime(.75f);
            station.QueueSword();
        }

        bool sawCarrierToForge = false, sawForgeInput = false, sawForging = false, sawSwordCargo = false;
        deadline = Time.realtimeSinceStartup + 70;
        while (Time.realtimeSinceStartup < deadline)
        {
            sawForgeInput |= forge.RecipeInput >= 3;
            sawForging |= forge.EquipmentQueueCount == 1 && forge.WorkerCount == 1 && forge.RecipeProgress > 0;
            foreach (var unit in FindObjectsOfType<RtsNetworkUnit>().Where(unit => unit.PlayerId == 2 && unit.IsCarrier))
            {
                sawCarrierToForge |= unit.CarriedIron >= 3;
                sawSwordCargo |= unit.CarriedSword == 1;
            }
            if (other.IronStock != 0 || other.SwordStock != 0 || station.SwordStock > 1)
            { Fail("forge used foreign stock or duplicated swords"); yield break; }
            if (station.SwordStock == 1 && forge.EquipmentQueueCount == 0) break;
            yield return null;
        }
        if (!sawCarrierToForge || !sawForgeInput || !sawForging || !sawSwordCargo || station.SwordStock != 1
            || forge.EquipmentQueueCount != 0 || forge.RecipeOutput != 0 || station.IronStock + forge.RecipeInput != 7)
        {
            Fail($"iron forging/warehouse flow incomplete (cargo={sawCarrierToForge}/{sawSwordCargo}, input={sawForgeInput}, forged={sawForging}, iron={station.IronStock}+{forge.RecipeInput}, swords={station.SwordStock})");
            yield break;
        }
        Debug.Log($"RTS_SMOKE_IRON_PASS player={session.LocalPlayerId} mine={mine.EntityId} forge={forge.EntityId} sword=1 iron={station.IronStock}+{forge.RecipeInput}");
        if (host) yield return new WaitForSecondsRealtime(2f);
        session.Disconnect();
        Application.Quit(0);
    }

    private IEnumerator RunBuildingCombat(RtsNetworkSession session, bool host)
    {
        var station = FindObjectsOfType<RtsNetworkConstruction>().Single(item => item.PlayerId == session.LocalPlayerId);
        var enemyPlot = new Vector3(-10.5f, 0, 19f);
        var ownPlot = new Vector3(-4.5f, 0, 19f);
        yield return new WaitForSecondsRealtime(2f);
        if (host) Time.timeScale = 4;
        station.RequestBuildAt(RtsNetworkConstruction.HouseId, host ? enemyPlot : ownPlot, 0);
        if (!host)
            FindObjectsOfType<RtsNetworkBarracks>().Single(item => item.PlayerId == 2).TrainSwordsman();

        float deadline = Time.realtimeSinceStartup + 55f;
        RtsNetworkBuildSite target = null, friendly = null;
        RtsNetworkUnit attacker = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            target = FindObjectsOfType<RtsNetworkBuildSite>().FirstOrDefault(site => site.PlayerId == 1 && site.IsComplete);
            friendly = FindBuilding(RtsNetworkConstruction.HouseId);
            attacker = FindObjectsOfType<RtsNetworkUnit>().FirstOrDefault(unit => unit.IsSpawned && unit.IsSwordsman && unit.PlayerId == 2);
            if (target != null && friendly?.IsComplete == true && attacker != null) break;
            yield return null;
        }
        if (target == null || friendly?.IsComplete != true || attacker == null || target.Health != target.MaxHealth || target.Health <= 0)
        { Fail("building combat fixture is incomplete"); yield break; }
        float maxHealth = target.MaxHealth, friendlyHealth = friendly.Health;
        ulong targetId = target.EntityId;
        var start = attacker.transform.position;
        if (!host)
        {
            ManagerGO.Instance.DeselectAll();
            ManagerGO.Instance.DragSelect(attacker.gameObject);
            ManagerGO.Instance.IssueNetworkBuildingAttack(friendly);
        }
        yield return new WaitForSecondsRealtime(.65f);
        if (friendly.Health != friendlyHealth || Vector3.Distance(attacker.transform.position, start) > .3f)
        { Fail("building attack accepted a friendly target"); yield break; }
        if (!host) ManagerGO.Instance.IssueNetworkBuildingAttack(target);
        bool sawApproach = false, sawDamage = false;
        deadline = Time.realtimeSinceStartup + 55f;
        while (target != null && target.IsSpawned && Time.realtimeSinceStartup < deadline)
        {
            sawApproach |= target.Health == maxHealth && Vector3.Distance(attacker.transform.position, start) > 2f;
            if (target.Health > 0 && target.Health < maxHealth)
            {
                sawDamage = true;
                if ((Mathf.RoundToInt(maxHealth - target.Health) % 18) != 0
                    || (host && Vector3.Distance(attacker.transform.position, target.MeleeContact(attacker.transform.position)) > 2.15f))
                { Fail("building damage did not follow melee range or damage"); yield break; }
            }
            yield return null;
        }
        if (!sawDamage || (host && !sawApproach) || session.manager.SpawnManager.SpawnedObjects.ContainsKey(targetId)
            || friendly == null || friendly.Health != friendlyHealth)
        { Fail($"building destruction failed (approach={sawApproach}, damage={sawDamage}, target={target != null})"); yield break; }
        Debug.Log($"RTS_SMOKE_BUILDING_DESTROYED player={session.LocalPlayerId} target={targetId} health={maxHealth}");
        if (host)
        {
            yield return new WaitForSecondsRealtime(.65f);
            station.RequestBuildAt(RtsNetworkConstruction.HouseId, enemyPlot, 0);
        }
        deadline = Time.realtimeSinceStartup + 30f;
        RtsNetworkBuildSite rebuilt = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            rebuilt = FindObjectsOfType<RtsNetworkBuildSite>().FirstOrDefault(site => site.PlayerId == 1 && site.IsComplete);
            if (rebuilt != null) break;
            yield return null;
        }
        if (rebuilt == null || rebuilt.EntityId == targetId || rebuilt.Health != maxHealth)
        { Fail("the destroyed building did not free its placement and NavMesh footprint"); yield break; }
        Debug.Log($"RTS_SMOKE_BUILDING_COMBAT_PASS player={session.LocalPlayerId} target={targetId} rebuilt={rebuilt.EntityId}");
        if (host) yield return new WaitForSecondsRealtime(2f);
        session.Disconnect();
        Application.Quit(0);
    }

    private IEnumerator RunStable(RtsNetworkSession session, bool host, bool cavalry)
    {
        var station = FindObjectsOfType<RtsNetworkConstruction>().Single(item => item.PlayerId == 2);
        var other = FindObjectsOfType<RtsNetworkConstruction>().Single(item => item.PlayerId == 1);
        if (station.GoldStock != 15 || other.GoldStock != 15)
        { Fail("stable fixture needs 15 gold per settlement"); yield break; }
        if (host) Time.timeScale = 4;
        else
        {
            station.HireCarrier();
            station.RequestBuildAt(RtsNetworkConstruction.StableId, new Vector3(-4.5f, 0, 19f), 0);
            if (cavalry)
            {
                yield return new WaitForSecondsRealtime(.7f);
                station.RequestBuildAt(RtsNetworkConstruction.BlacksmithId, new Vector3(-2.5f, 0, 19f), 0);
                yield return new WaitForSecondsRealtime(.7f);
                station.RequestBuildAt(RtsNetworkConstruction.BarracksId, new Vector3(-4.5f, 0, 23f), 0);
            }
        }
        float deadline = Time.realtimeSinceStartup + 65f;
        RtsNetworkBuildSite stable = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            stable = FindBuilding(RtsNetworkConstruction.StableId);
            if (stable?.IsComplete == true && station.CarrierCount == 1 && station.GoldStock == 5) break;
            yield return null;
        }
        if (stable?.IsComplete != true || !stable.HasRoadAccess || station.CarrierCount != 1
            || stable.DeliveredGold != 10 || station.GoldStock != 5 || other.GoldStock != 15)
        { Fail($"stable build failed (progress={stable?.Progress}, gold={station.GoldStock}, delivered={stable?.DeliveredGold})"); yield break; }
        if (!host)
        {
            yield return new WaitForSecondsRealtime(.6f);
            station.QueueHorse();
        }

        bool sawFood = false, sawGold = false, sawProgress = false;
        RtsNetworkUnit horse = null;
        deadline = Time.realtimeSinceStartup + 55f;
        while (Time.realtimeSinceStartup < deadline)
        {
            foreach (var unit in FindObjectsOfType<RtsNetworkUnit>().Where(unit => unit.PlayerId == 2 && unit.IsCarrier))
            {
                sawFood |= unit.CarriedFood == 10;
                sawGold |= unit.CarriedGold == 5;
            }
            sawProgress |= stable.HorseQueueCount == 1 && stable.StableFood == 10 && stable.StableGold == 5
                && stable.RecipeProgress > 0;
            if (stable.RecipeProgress > 0 && (stable.StableFood != 10 || stable.StableGold != 5 || other.GoldStock != 15))
            { Fail("stable progress bypassed delivery or used foreign stock"); yield break; }
            horse = FindObjectsOfType<RtsNetworkUnit>().FirstOrDefault(unit => unit.IsSpawned && unit.IsHorse);
            if (horse != null && stable.HorseQueueCount == 0 && stable.RecipeProgress == 0) break;
            yield return null;
        }
        if (horse == null || horse.PlayerId != 2 || !sawFood || !sawGold || !sawProgress || station.GoldStock != 0
            || stable.StableFood != 0 || stable.StableGold != 0 || stable.HorseQueueCount != 0)
        { Fail($"horse production failed (food={sawFood}, gold={sawGold}, progress={sawProgress}, horse={horse != null}, stock={station.GoldStock})"); yield break; }
        Debug.Log($"RTS_SMOKE_STABLE_HORSE player={session.LocalPlayerId} stable={stable.EntityId} horse={horse.EntityId}");

        var destination = new Vector3(-4.5f, 0, cavalry ? 8f : 26f);
        if (!host)
        {
            yield return new WaitForSecondsRealtime(.65f);
            ManagerGO.Instance.DeselectAll();
            ManagerGO.Instance.DragSelect(horse.gameObject);
            if (ManagerGO.Instance.unitsSelected.Count != 1)
            { Fail("the owner could not select the horse"); yield break; }
            ManagerGO.Instance.IssueNetworkMove(destination);
        }
        deadline = Time.realtimeSinceStartup + 15f;
        while (!Reached(horse, destination) && Time.realtimeSinceStartup < deadline) yield return null;
        if (!Reached(horse, destination)) { Fail("horse movement did not reach both peers"); yield break; }
        Debug.Log($"RTS_SMOKE_STABLE_PASS player={session.LocalPlayerId} horse={horse.EntityId} position={horse.transform.position:F2}");
        if (cavalry)
        {
            yield return VerifyCavalry(session, host, station, horse);
            yield break;
        }
        if (host) yield return new WaitForSecondsRealtime(2f);
        session.Disconnect();
        Application.Quit(0);
    }

    private IEnumerator VerifyCavalry(RtsNetworkSession session, bool host, RtsNetworkConstruction station, RtsNetworkUnit horse)
    {
        float deadline = Time.realtimeSinceStartup + 85f;
        RtsNetworkBuildSite forge = null, site = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            forge = FindBuilding(RtsNetworkConstruction.BlacksmithId);
            site = FindBuilding(RtsNetworkConstruction.BarracksId);
            if (forge?.IsComplete == true && site?.IsComplete == true) break;
            yield return null;
        }
        if (forge?.IsComplete != true || site?.IsComplete != true || !site.TryGetComponent<RtsNetworkBarracks>(out var barracks))
        { Fail($"cavalry buildings missing (forge={forge?.Progress}, barracks={site?.Progress}, stone={station.StoneStock})"); yield break; }
        if (!host)
        {
            station.HireWorker(RtsNetworkConstruction.BlacksmithId);
            station.QueueSword();
        }
        deadline = Time.realtimeSinceStartup + 65f;
        while (station.SwordStock != 1 && Time.realtimeSinceStartup < deadline) yield return null;
        if (station.SwordStock != 1 || barracks.SwordCount != 0 || station.ShieldStock != 0)
        { Fail("cavalry training fixture requires a delivered sword and no shield"); yield break; }
        if (!host)
        {
            station.TrainCavalryAtBuiltBarracks();
            yield return new WaitForSecondsRealtime(.75f);
            if (barracks.QueueCount != 0 || horse.IsHorseReserved || !barracks.Status.Contains("свободную лошадь"))
            { Fail($"cavalry accepted a horse outside the recruitment radius: {barracks.Status}"); yield break; }
            horse.RequestMove(new Vector3(-3f, 0, 24f));
        }
        var near = new Vector3(-3f, 0, 24f);
        deadline = Time.realtimeSinceStartup + 18f;
        while (!Reached(horse, near) && Time.realtimeSinceStartup < deadline) yield return null;
        if (!Reached(horse, near)) { Fail("the owner did not lead the horse back to the barracks"); yield break; }
        ulong horseId = horse.EntityId;
        var originalMount = host ? horse.GetComponent<CartHorse>() : null;
        if (!host) station.TrainCavalryAtBuiltBarracks();
        bool sawReservation = false, sawTraining = false, repeatedOrder = false;
        RtsNetworkUnit rider = null;
        deadline = Time.realtimeSinceStartup + 45f;
        while (Time.realtimeSinceStartup < deadline)
        {
            sawReservation |= horse != null && horse.IsHorseReserved && barracks.QueueCount == 1;
            sawTraining |= barracks.SwordCount == 1 && barracks.Progress > 0f;
            if (!host && sawTraining && !repeatedOrder)
            {
                station.TrainCavalryAtBuiltBarracks();
                repeatedOrder = true;
            }
            if (barracks.QueueCount > 1 || (barracks.Progress > 0f && barracks.SwordCount != 1))
            { Fail("cavalry used a duplicate mount or started before sword delivery"); yield break; }
            rider = FindObjectsOfType<RtsNetworkUnit>().FirstOrDefault(unit => unit.IsSpawned && unit.IsCavalry && unit.PlayerId == 2);
            if (rider != null && barracks.QueueCount == 0 && !session.manager.SpawnManager.SpawnedObjects.ContainsKey(horseId)) break;
            yield return null;
        }
        if (rider == null || rider.MaxHealth != 160f || !sawReservation || !sawTraining || barracks.QueueCount != 0
            || station.SwordStock != 0 || barracks.SwordCount != 0 || session.manager.SpawnManager.SpawnedObjects.ContainsKey(horseId)
            || (host && (rider.GetComponent<TroopCombat>().MountedHorse != originalMount || originalMount.gameObject.activeSelf
                         || rider.GetComponent<GettingResources>()?.Sword != 1)))
        { Fail($"mounted soldier lifecycle failed (reserved={sawReservation}, training={sawTraining}, rider={rider != null})"); yield break; }
        var target = FindObjectsOfType<RtsNetworkUnit>().Single(unit => unit.PlayerId == 1 && !unit.IsVillager
            && !unit.IsBuilder && !unit.IsSupplyWorker && !unit.IsCombatUnit && !unit.IsHorse);
        float health = target.Health;
        if (!host)
        {
            ManagerGO.Instance.DeselectAll();
            ManagerGO.Instance.DragSelect(rider.gameObject);
            ManagerGO.Instance.IssueNetworkAttack(target);
        }
        deadline = Time.realtimeSinceStartup + 22f;
        while (target != null && target.Health == health && Time.realtimeSinceStartup < deadline) yield return null;
        if (target == null || target.Health != health - 22f)
        { Fail($"cavalry first melee strike did not deal 22 (health={target?.Health})"); yield break; }
        Debug.Log($"RTS_SMOKE_CAVALRY_PASS player={session.LocalPlayerId} horse={horseId} rider={rider.EntityId} health=160 damage=22");
        if (host) yield return new WaitForSecondsRealtime(2f);
        session.Disconnect();
        Application.Quit(0);
    }

    private IEnumerator RunArcher(RtsNetworkSession session, bool host)
    {
        var station = FindObjectsOfType<RtsNetworkConstruction>().Single(item => item.PlayerId == 2);
        var other = FindObjectsOfType<RtsNetworkConstruction>().Single(item => item.PlayerId == 1);
        if (station.BowStock != 0 || other.BowStock != 0) { Fail("archer fixture must start without bows"); yield break; }
        if (host) Time.timeScale = 4;
        else
        {
            station.HireCarrier();
            station.RequestBuildAt(RtsNetworkConstruction.WorkshopId, new Vector3(-2.5f, 0, 19f), 0);
            yield return new WaitForSecondsRealtime(.75f);
            station.RequestBuildAt(RtsNetworkConstruction.ArcheryRangeId, new Vector3(-4.5f, 0, 23f), 0);
        }
        float deadline = Time.realtimeSinceStartup + 85f;
        RtsNetworkBuildSite workshop = null, range = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            workshop = FindBuilding(RtsNetworkConstruction.WorkshopId);
            range = FindBuilding(RtsNetworkConstruction.ArcheryRangeId);
            if (workshop?.IsComplete == true && range?.IsComplete == true && station.CarrierCount == 1) break;
            yield return null;
        }
        if (workshop?.IsComplete != true || range?.IsComplete != true || !range.TryGetComponent<RtsNetworkBarracks>(out var training))
        { Fail($"archer buildings missing (workshop={workshop?.Progress}, range={range?.Progress}, carriers={station.CarrierCount})"); yield break; }
        if (!host)
        {
            training.TrainArcher();
            yield return new WaitForSecondsRealtime(.65f);
            if (training.QueueCount != 0) { Fail("training accepted an order without a bow"); yield break; }
            station.HireWorker(RtsNetworkConstruction.WorkshopId);
            station.QueueBow();
        }
        bool sawBowCargo = false, workshopWorked = false;
        deadline = Time.realtimeSinceStartup + 65f;
        while (station.BowStock == 0 && Time.realtimeSinceStartup < deadline)
        {
            workshopWorked |= workshop.RecipeProgress > 0 && workshop.WorkerCount == 1;
            sawBowCargo |= FindObjectsOfType<RtsNetworkUnit>().Any(unit => unit.PlayerId == 2 && unit.IsCarrier && unit.CarriedBow == 1);
            yield return null;
        }
        if (station.BowStock != 1 || other.BowStock != 0 || !sawBowCargo || !workshopWorked || training.BowCount != 0)
        { Fail($"workshop did not deliver one bow to warehouse (work={workshopWorked}, haul={sawBowCargo}, bows={station.BowStock})"); yield break; }

        if (!host) station.TrainArcherAtBuiltRange();
        bool sawDelivery = false, sawTraining = false;
        RtsNetworkUnit archer = null;
        deadline = Time.realtimeSinceStartup + 55f;
        while (Time.realtimeSinceStartup < deadline)
        {
            sawDelivery |= training.BowCount == 1;
            sawTraining |= training.Progress > 0 && training.BowCount == 1;
            archer = FindObjectsOfType<RtsNetworkUnit>().FirstOrDefault(unit => unit.IsSpawned && unit.IsArcher && unit.PlayerId == 2);
            if (archer != null && training.BowCount == 0 && training.QueueCount == 0) break;
            yield return null;
        }
        if (archer == null || archer.MaxHealth != 70f || !sawDelivery || !sawTraining || station.BowStock != 0
            || (host && archer.GetComponent<GettingResources>()?.Bow != 1))
        { Fail($"bow delivery or archer training failed (bow={sawDelivery}, trained={sawTraining}, unit={archer != null})"); yield break; }
        Debug.Log($"RTS_SMOKE_ARCHER_TRAINED player={session.LocalPlayerId} range={range.EntityId} archer={archer.EntityId} hp={archer.MaxHealth}");

        var target = FindObjectsOfType<RtsNetworkUnit>().Single(unit => unit.PlayerId == 1 && !unit.IsVillager
            && !unit.IsBuilder && !unit.IsSupplyWorker && !unit.IsCombatUnit);
        var firingPoint = new Vector3(-20, 0, 0);
        if (!host) archer.RequestMove(firingPoint);
        deadline = Time.realtimeSinceStartup + 18f;
        while (!Reached(archer, firingPoint) && Time.realtimeSinceStartup < deadline) yield return null;
        if (!Reached(archer, firingPoint)) { Fail("archer did not reach the firing point"); yield break; }
        if (host) Time.timeScale = 1;
        float before = target.Health;
        if (!host)
        {
            yield return new WaitForSecondsRealtime(.65f);
            ManagerGO.Instance.DeselectAll();
            ManagerGO.Instance.DragSelect(archer.gameObject);
            ManagerGO.Instance.IssueNetworkAttack(target);
        }
        bool sawArrow = false;
        ulong arrowId = 0;
        deadline = Time.realtimeSinceStartup + 10f;
        while (target != null && target.Health == before && Time.realtimeSinceStartup < deadline)
        {
            var arrow = FindObjectOfType<Arrow>();
            if (arrow != null && arrow.TryGetComponent<Unity.Netcode.NetworkObject>(out var net) && net.IsSpawned)
            {
                sawArrow = true;
                arrowId = net.NetworkObjectId;
            }
            yield return null;
        }
        if (!sawArrow || target == null || target.Health != before - 16f || Vector3.Distance(archer.transform.position, firingPoint) > 1f)
        { Fail($"archer shot result wrong (arrow={sawArrow}, before={before}, after={target?.Health}, archer={archer.transform.position:F2})"); yield break; }
        Debug.Log($"RTS_SMOKE_ARCHER_PASS player={session.LocalPlayerId} archer={archer.EntityId} arrow={arrowId} damage=16");
        if (host) yield return new WaitForSecondsRealtime(2f);
        session.Disconnect();
        Application.Quit(0);
    }

    private IEnumerator RunEquipment(RtsNetworkSession session, bool host, bool spear)
    {
        int ironCost = spear ? 1 : 3;
        ResourceType weapon = spear ? ResourceType.Spear : ResourceType.Sword;
        int WeaponStock(RtsNetworkConstruction source) => spear ? source.SpearStock : source.SwordStock;
        int WeaponCargo(RtsNetworkUnit carrier) => spear ? carrier.CarriedSpear : carrier.CarriedSword;
        RtsNetworkUnit Trainee() => FindObjectsOfType<RtsNetworkUnit>()
            .FirstOrDefault(unit => unit.IsSpawned && unit.PlayerId == 2 && (spear ? unit.IsSpearman : unit.IsSwordsman));
        var station = FindObjectsOfType<RtsNetworkConstruction>().Single(item => item.PlayerId == 2);
        var other = FindObjectsOfType<RtsNetworkConstruction>().Single(item => item.PlayerId == 1);
        if (station.IronStock != ironCost || other.IronStock != ironCost || WeaponStock(station) != 0 || station.ShieldStock != 0)
        { Fail("equipment fixture must start with iron and no finished equipment"); yield break; }

        if (host) Time.timeScale = 4;
        else
        {
            station.HireCarrier();
            station.HireCarrier();
            station.RequestBuildAt(RtsNetworkConstruction.BlacksmithId, new Vector3(-4.5f, 0, 19f), 0);
            yield return new WaitForSecondsRealtime(.75f);
            station.RequestBuildAt(RtsNetworkConstruction.WorkshopId, new Vector3(-2.5f, 0, 19f), 0);
            yield return new WaitForSecondsRealtime(.75f);
            station.RequestBuildAt(RtsNetworkConstruction.BarracksId, new Vector3(-4.5f, 0, 23f), 0);
        }

        RtsNetworkBuildSite forge = null, workshop = null, barracksSite = null;
        float deadline = Time.realtimeSinceStartup + 90;
        while (Time.realtimeSinceStartup < deadline)
        {
            forge = FindBuilding(RtsNetworkConstruction.BlacksmithId);
            workshop = FindBuilding(RtsNetworkConstruction.WorkshopId);
            barracksSite = FindBuilding(RtsNetworkConstruction.BarracksId);
            if (forge != null && workshop != null && barracksSite != null
                && forge.IsComplete && workshop.IsComplete && barracksSite.IsComplete
                && forge.HasRoadAccess && workshop.HasRoadAccess && station.CarrierCount == 2) break;
            yield return null;
        }
        if (forge == null || workshop == null || barracksSite == null || !forge.IsComplete || !workshop.IsComplete
            || !forge.HasRoadAccess || !workshop.HasRoadAccess || !barracksSite.IsComplete || station.CarrierCount != 2
            || !barracksSite.TryGetComponent<RtsNetworkBarracks>(out var barracks))
        { Fail($"equipment buildings or carriers missing (forge={forge?.Progress}, workshop={workshop?.Progress}, barracks={barracksSite?.Progress}, carriers={station.CarrierCount})"); yield break; }
        int BarracksWeapon() => spear ? barracks.SpearCount : barracks.SwordCount;
        if (BarracksWeapon() != 0 || barracks.ShieldCount != 0 || barracks.QueueCount != 0)
        { Fail("equipment scenario inherited a free barracks kit"); yield break; }

        if (!host)
        {
            if (spear) barracks.TrainSpearman();
            else barracks.TrainSwordsman();
            yield return new WaitForSecondsRealtime(.8f);
            if (barracks.QueueCount != 0 || barracks.Progress != 0f || Trainee() != null)
            { Fail("barracks accepted training without a kit"); yield break; }
            station.HireWorker(RtsNetworkConstruction.BlacksmithId);
            station.HireWorker(RtsNetworkConstruction.WorkshopId);
            if (spear) station.QueueSpear();
            else station.QueueSword();
            yield return new WaitForSecondsRealtime(.5f);
            station.QueueShield();
        }

        bool forgeWorked = false, workshopWorked = false, sawIronCargo = false;
        bool sawWeaponToWarehouse = false, sawShieldToWarehouse = false;
        deadline = Time.realtimeSinceStartup + 100;
        while (Time.realtimeSinceStartup < deadline)
        {
            forgeWorked |= forge.RecipeProgress > 0 && forge.EquipmentQueueCount == 1 && forge.WorkerCount == 1;
            workshopWorked |= workshop.RecipeProgress > 0 && workshop.EquipmentQueueCount == 1 && workshop.WorkerCount == 1;
            foreach (var carrier in FindObjectsOfType<RtsNetworkUnit>().Where(unit => unit.PlayerId == 2 && unit.IsCarrier))
            {
                sawIronCargo |= carrier.CarriedIron == ironCost;
                sawWeaponToWarehouse |= WeaponCargo(carrier) == 1;
                sawShieldToWarehouse |= carrier.CarriedShield == 1;
            }
            if (barracks.QueueCount != 0 || barracks.Progress > 0f || BarracksWeapon() != 0 || barracks.ShieldCount != 0
                || Trainee() != null || other.IronStock != ironCost || WeaponStock(other) != 0 || other.ShieldStock != 0
                || WeaponStock(station) > 1 || station.ShieldStock > 1 || (spear && station.SwordStock != 0))
            { Fail("production bypassed the warehouse, used foreign stock or duplicated equipment"); yield break; }
            if (WeaponStock(station) == 1 && station.ShieldStock == 1) break;
            yield return null;
        }
        if (!forgeWorked || !workshopWorked || !sawIronCargo || !sawWeaponToWarehouse || !sawShieldToWarehouse
            || station.IronStock != 0 || WeaponStock(station) != 1 || station.ShieldStock != 1
            || forge.EquipmentQueueCount != 0 || workshop.EquipmentQueueCount != 0)
        {
            Fail($"equipment production/warehouse flow incomplete (work={forgeWorked}/{workshopWorked}, cargo={sawIronCargo}/{sawWeaponToWarehouse}/{sawShieldToWarehouse}, stocks={station.IronStock}/{WeaponStock(station)}/{station.ShieldStock}, queues={forge.EquipmentQueueCount}/{workshop.EquipmentQueueCount})");
            yield break;
        }
        Debug.Log($"RTS_SMOKE_EQUIPMENT_WAREHOUSE player={session.LocalPlayerId} forge={forge.EntityId} workshop={workshop.EntityId} weapon={weapon} shield=1");
        if (!host)
        {
            // Leave time for both observers to see the complete kit physically deposited.
            yield return new WaitForSecondsRealtime(.75f);
            if (spear) station.TrainSpearmanAtBuiltBarracks();
            else station.TrainAtBuiltBarracks();
        }

        bool sawTrainingOrder = false, sawKitAtBarracks = false, sawTraining = false;
        bool sawWeaponToBarracks = false, sawShieldToBarracks = false;
        RtsNetworkUnit soldier = null;
        deadline = Time.realtimeSinceStartup + 55;
        while (Time.realtimeSinceStartup < deadline)
        {
            sawTrainingOrder |= barracks.QueueCount == 1;
            sawKitAtBarracks |= BarracksWeapon() == 1 && barracks.ShieldCount == 1;
            sawTraining |= barracks.Progress > 0f;
            if (barracks.Progress > 0f && (BarracksWeapon() != 1 || barracks.ShieldCount != 1))
            { Fail("training progressed before the two items arrived in barracks"); yield break; }
            if (sawTrainingOrder)
                foreach (var carrier in FindObjectsOfType<RtsNetworkUnit>().Where(unit => unit.PlayerId == 2 && unit.IsCarrier))
                {
                    sawWeaponToBarracks |= WeaponCargo(carrier) == 1;
                    sawShieldToBarracks |= carrier.CarriedShield == 1;
                }
            soldier = Trainee();
            if (soldier != null && barracks.QueueCount == 0 && BarracksWeapon() == 0 && barracks.ShieldCount == 0) break;
            yield return null;
        }
        if (!sawTrainingOrder || !sawWeaponToBarracks || !sawShieldToBarracks || !sawKitAtBarracks || !sawTraining
            || soldier == null || WeaponStock(station) != 0 || station.ShieldStock != 0
            || barracks.QueueCount != 0 || BarracksWeapon() != 0 || barracks.ShieldCount != 0
            || FindObjectsOfType<RtsNetworkUnit>().Count(unit => unit.IsSpawned && unit.IsMeleeFighter && unit.PlayerId == 2) != 1)
        {
            if (host && barracks.TryGetComponent<Barracks>(out var training) && training.SpawnPoint != null)
            {
                var exit = training.SpawnPoint.transform.position;
                var nearby = FindObjectsOfType<RtsNetworkUnit>()
                    .Where(unit => (unit.transform.position - exit).sqrMagnitude < 9f)
                    .Select(unit => $"{unit.name}#{unit.EntityId}@{unit.transform.position:F2}");
                Debug.Log($"RTS_SMOKE_EQUIPMENT_EXIT status={training.Status} exit={exit:F2} nearby={string.Join(", ", nearby)}");
            }
            Fail($"equipment training flow incomplete (queue={sawTrainingOrder}, cargo={sawWeaponToBarracks}/{sawShieldToBarracks}, kit={sawKitAtBarracks}, progress={sawTraining}, soldier={soldier != null}, stocks={WeaponStock(station)}/{station.ShieldStock}, barracks={BarracksWeapon()}/{barracks.ShieldCount})");
            yield break;
        }
        if (host && (!soldier.TryGetComponent<GettingResources>(out var gear)
            || gear.GetStoredAmount(weapon) != 1 || gear.GetStoredAmount(ResourceType.Shield) != 1))
        { Fail("the soldier did not receive the produced equipment"); yield break; }
        if (spear)
        {
            var target = FindObjectsOfType<RtsNetworkUnit>().Single(unit => unit.PlayerId == 1 && !unit.IsVillager
                && !unit.IsBuilder && !unit.IsSupplyWorker && !unit.IsMeleeFighter);
            float before = target.Health;
            if (!host)
            {
                ManagerGO.Instance.DeselectAll();
                ManagerGO.Instance.DragSelect(soldier.gameObject);
                ManagerGO.Instance.IssueNetworkAttack(target);
            }
            deadline = Time.realtimeSinceStartup + 22f;
            while (target != null && target.Health == before && Time.realtimeSinceStartup < deadline) yield return null;
            if (target == null || target.Health != before - 12f || soldier.MaxHealth != 100f)
            { Fail($"spearman attributes or first attack were incorrect (max={soldier.MaxHealth}, before={before}, after={target?.Health})"); yield break; }
            Debug.Log($"RTS_SMOKE_SPEARMAN_PASS player={session.LocalPlayerId} barracks={barracksSite.EntityId} spearman={soldier.EntityId} hp=100 damage=12");
        }
        else Debug.Log($"RTS_SMOKE_EQUIPMENT_PASS player={session.LocalPlayerId} barracks={barracksSite.EntityId} swordsman={soldier.EntityId}");
        if (host) yield return new WaitForSecondsRealtime(2);
        session.Disconnect();
        Application.Quit(0);
    }

    private IEnumerator RunBread(RtsNetworkSession session, bool host, bool fromField)
    {
        var station = FindObjectsOfType<RtsNetworkConstruction>().Single(item => item.PlayerId == 2);
        var other = FindObjectsOfType<RtsNetworkConstruction>().Single(item => item.PlayerId == 1);
        int startingWheat = fromField ? 0 : 6;
        if (station.WheatStock != startingWheat || other.WheatStock != startingWheat || station.BreadStock != 0)
        { Fail("incorrect initial wheat or bread in the fixture"); yield break; }
        if (host) Time.timeScale = fromField ? 5 : 4;
        else
        {
            other.HireCarrier();
            station.HireCarrier();
            if (!fromField) station.HireCarrier();
            station.RequestBuildAt(RtsNetworkConstruction.MillId, new Vector3(-4.5f, 0, 25), 0);
            yield return new WaitForSecondsRealtime(.75f);
            if (FindBuilding(RtsNetworkConstruction.MillId) != null)
            { Fail("mill without road access was accepted"); yield break; }
            station.RequestBuildAt(RtsNetworkConstruction.MillId, new Vector3(-4.5f, 0, 19), 0);
            yield return new WaitForSecondsRealtime(.75f);
            station.RequestBuildAt(RtsNetworkConstruction.BakeryId, new Vector3(-2.5f, 0, 19), 0);
            if (fromField)
            {
                yield return new WaitForSecondsRealtime(.75f);
                station.RequestBuildAt(RtsNetworkConstruction.FarmId, new Vector3(-4.5f, 0, 27), 2);
            }
        }
        bool wheat = false, flour = false, bread = false, millWorked = false, bakeryWorked = false;
        bool twoCarriers = false;
        bool ripeField = false, harvest = false, farmStock = false, deliveredWheat = false, farmerMovedWithWheat = false;
        var hired = new System.Collections.Generic.HashSet<int>();
        bool duplicateRequested = false;
        Vector3? harvestedAt = null;
        float deadline = Time.realtimeSinceStartup + (fromField ? 145 : 95);
        RtsNetworkBuildSite mill = null, bakery = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            mill = FindBuilding(RtsNetworkConstruction.MillId);
            bakery = FindBuilding(RtsNetworkConstruction.BakeryId);
            if (other.WheatStock != startingWheat || other.BreadStock != 0 || other.CarrierCount != 0 || station.BreadStock > 6)
            { Fail("production duplicated a shipment or changed foreign stock"); yield break; }
            if (mill != null && mill.IsComplete) millWorked |= mill.RecipeProgress > 0 && mill.HasRoadAccess;
            if (bakery != null && bakery.IsComplete) bakeryWorked |= bakery.RecipeProgress > 0 && bakery.HasRoadAccess;
            if (!host)
            {
                TryHireCompleted(station, mill, hired);
                TryHireCompleted(station, bakery, hired);
            }
            var carriers = FindObjectsOfType<RtsNetworkUnit>().Where(unit => unit.PlayerId == 2 && unit.IsCarrier).ToArray();
            twoCarriers |= carriers.Length == 2;
            wheat |= carriers.Any(unit => unit.CarriedWheat > 0);
            flour |= carriers.Any(unit => unit.CarriedFlour > 0);
            bread |= carriers.Any(unit => unit.CarriedBread > 0);
            if (fromField)
            {
                var field = FindObjectsOfType<RtsNetworkFarmField>().FirstOrDefault(item => item.IsSpawned);
                if (field != null)
                {
                    ripeField |= field.WheatRemaining == 6;
                    harvest |= ripeField && field.WheatRemaining == 0 && field.Growth < 1f;
                }
                var farm = FindBuilding(RtsNetworkConstruction.FarmId);
                farmStock |= farm != null && farm.FarmWheat == 6;
                if (!host)
                {
                    TryHireCompleted(station, farm, hired);
                    if (farm != null && farm.WorkerCount == 1 && !duplicateRequested)
                    {
                        duplicateRequested = true;
                        station.HireWorker(RtsNetworkConstruction.FarmId);
                    }
                }
                deliveredWheat |= station.WheatStock == 6;
                var farmer = FindObjectsOfType<RtsNetworkUnit>().FirstOrDefault(unit => unit.PlayerId == 2 && unit.IsFarmer);
                if (farmer != null && farmer.CarriedWheat == 6)
                {
                    if (!harvestedAt.HasValue) harvestedAt = farmer.transform.position;
                    farmerMovedWithWheat |= Vector3.Distance(harvestedAt.Value, farmer.transform.position) > 1f;
                }
            }
            if (station.BreadStock == 6) break;
            yield return null;
        }
        if (mill == null || bakery == null || !millWorked || !bakeryWorked || !wheat || !flour || !bread || (!fromField && !twoCarriers)
            || station.WheatStock != 0 || station.FlourStock != 0 || station.BreadStock != 6)
        {
            Fail($"bread flow incomplete (mill={mill?.Progress}/{mill?.RecipeInput}/{mill?.RecipeOutput}, bakery={bakery?.Progress}/{bakery?.RecipeInput}/{bakery?.RecipeOutput}, cargo={wheat}/{flour}/{bread}, worked={millWorked}/{bakeryWorked}, carriers={twoCarriers}, stock={station.WheatStock}/{station.FlourStock}/{station.BreadStock})");
            yield break;
        }
        if (fromField && (!ripeField || !harvest || !farmStock || !deliveredWheat || !farmerMovedWithWheat
            || station.CarrierCount != 1 || station.AvailableResidents != 3
            || FindObjectsOfType<RtsNetworkUnit>().Count(unit => unit.PlayerId == 2 && unit.IsFarmer) != 1))
        { Fail($"field flow incomplete (ripe={ripeField}, harvest={harvest}, farm={farmStock}, warehouse={deliveredWheat}, farm delivery={farmerMovedWithWheat})"); yield break; }
        Debug.Log($"RTS_SMOKE_{(fromField ? "FARM" : "BREAD")}_PASS player={session.LocalPlayerId} mill={mill.EntityId} bakery={bakery.EntityId} bread=6");
        if (host) yield return new WaitForSecondsRealtime(2);
        session.Disconnect();
        Application.Quit(0);
    }

    private static void TryHireCompleted(RtsNetworkConstruction station, RtsNetworkBuildSite building,
        System.Collections.Generic.HashSet<int> hired)
    {
        if (building != null && building.IsComplete && hired.Add(building.BuildingId))
            station.HireWorker(building.BuildingId);
    }

    private IEnumerator RunSupply(RtsNetworkSession session, bool host, bool stone)
    {
        var station = FindObjectsOfType<RtsNetworkConstruction>().Single(item => item.PlayerId == 2);
        var hostStation = FindObjectsOfType<RtsNetworkConstruction>().Single(item => item.PlayerId == 1);
        int Stock(RtsNetworkConstruction store) => stone ? store.StoneStock : store.WoodStock;
        int initialStock = Stock(station);
        int supplyBuilding = stone ? RtsNetworkConstruction.StoneQuarryId : RtsNetworkConstruction.SawmillId;
        int destinationBuilding = stone ? RtsNetworkConstruction.GranaryId : RtsNetworkConstruction.HouseId;
        string resource = stone ? "STONE" : "TIMBER";
        if (host) Time.timeScale = 3;
        else
        {
            station.HireCarrier();
            station.RequestBuildAt(supplyBuilding, new Vector3(-4.5f, 0, 25), 0);
            yield return new WaitForSecondsRealtime(.75f);
            if (FindBuilding(supplyBuilding) != null || station.ReservedWood != 0)
            { Fail("supply building without a road was accepted"); yield break; }
            station.RequestBuildAt(supplyBuilding, new Vector3(-4.5f, 0, 19), 0);
        }
        float deadline = Time.realtimeSinceStartup + 75;
        bool emptyAfterBuild = false, atMill = false, cut = false, haul = false;
        Vector3? loadedAt = null;
        RtsNetworkBuildSite mill = null;
        var hired = new System.Collections.Generic.HashSet<int>();
        while (Time.realtimeSinceStartup < deadline)
        {
            mill = FindBuilding(supplyBuilding);
            if (mill != null && mill.IsComplete)
            {
                if (!host) TryHireCompleted(station, mill, hired);
                if (!mill.HasRoadAccess) { Fail("supply building did not register road access"); yield break; }
                emptyAfterBuild |= Stock(station) == 0;
                atMill |= (stone ? mill.StoneHouseStock : mill.SawmillWood) >= 10;
                foreach (var unit in FindObjectsOfType<RtsNetworkUnit>())
                {
                    if (unit.PlayerId != 2 || (stone ? unit.CarriedStone : unit.CarriedWood) < 10) continue;
                    if (stone ? unit.IsStoneMason : unit.IsLumberjack) cut = true;
                    if (!unit.IsCarrier) continue;
                    if (!loadedAt.HasValue) loadedAt = unit.transform.position;
                    haul |= Vector3.Distance(loadedAt.Value, unit.transform.position) > .75f;
                }
                if (Stock(station) >= 10 && emptyAfterBuild && atMill && cut && haul) break;
            }
            if (Stock(hostStation) != initialStock) { Fail("delivery used a foreign warehouse"); yield break; }
            yield return null;
        }
        if (mill == null || !mill.IsComplete || !emptyAfterBuild || !atMill || !cut || !haul || Stock(station) < 10)
        { Fail($"{resource} flow incomplete (site={mill?.Progress}, exhausted={emptyAfterBuild}, gathered={cut}, atSite={atMill}, haul={haul}, warehouse={Stock(station)})"); yield break; }
        Debug.Log($"RTS_SMOKE_{resource}_STOCK player={session.LocalPlayerId} source={mill.EntityId} stock={Stock(station)}");
        deadline = Time.realtimeSinceStartup + 40;
        var house = FindBuilding(destinationBuilding);
        var houseCells = stone ? new[] { new Vector3(-3.5f, 0, 24), new Vector3(-3.5f, 0, 22) }
            : new[] { new Vector3(-4.5f, 0, 25), new Vector3(-2.5f, 0, 21), new Vector3(-2.5f, 0, 25) };
        float nextPlacement = 0;
        int cell = 0;
        while ((house == null || !house.IsComplete) && Time.realtimeSinceStartup < deadline)
        {
            // Workers can cross a cell between seeing available stock and sending the placement.
            if (!host && house == null && station.ReservedWood == 0 && Time.realtimeSinceStartup >= nextPlacement)
            {
                station.RequestBuildAt(destinationBuilding, houseCells[cell++ % houseCells.Length], 0);
                nextPlacement = Time.realtimeSinceStartup + 1f;
            }
            yield return null;
            house = FindBuilding(destinationBuilding);
        }
        if (house == null || !house.IsComplete || (stone ? house.DeliveredStone != 10 || house.GranaryCapacity != 300
            : house.DeliveredWood != 10 || house.HousingCapacity != 5))
        { Fail($"the finished building did not use delivered {resource}"); yield break; }
        Debug.Log($"RTS_SMOKE_{resource}_PASS player={session.LocalPlayerId} source={mill.EntityId} building={house.EntityId} delivered=10");
        if (host) yield return new WaitForSecondsRealtime(2);
        session.Disconnect();
        Application.Quit(0);
    }

    private static RtsNetworkBuildSite FindBuilding(int id) => FindObjectsOfType<RtsNetworkBuildSite>()
        .FirstOrDefault(item => item.IsSpawned && item.PlayerId == 2 && item.BuildingId == id);
    private static bool Reached(RtsNetworkUnit unit, Vector3 target)
    {
        var position = unit.transform.position;
        return new Vector2(position.x - target.x, position.z - target.z).sqrMagnitude <= .36f;
    }

    private static RtsNetworkUnit FindReserve() => FindObjectsOfType<RtsNetworkUnit>().FirstOrDefault(IsClientReserve);

    private static bool IsClientReserve(RtsNetworkUnit unit) => unit.IsSpawned && unit.PlayerId == 2
        && !unit.IsVillager && !unit.IsSwordsman && !unit.IsBuilder && unit.NetworkObject.IsSceneObject == false;
    private static void Fail(string reason) { Debug.LogError("RTS_SMOKE_FAIL " + reason); Application.Quit(2); }
}
