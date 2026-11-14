using UnityEngine;
using UnityEngine.AI;

public static class CatapultFactory
{
    public static bool Valid(GameObject prefab)=>prefab==null||(prefab.GetComponent<Catapult>()!=null&&prefab.GetComponent<Unit>()!=null&&prefab.GetComponent<NavMeshAgent>()!=null);
    public static bool TrySpawn(GameObject prefab,GameObject workshop,Transform spawn)
    {
        if(!Valid(prefab))return false;
        if (!TryFindExit(prefab, workshop, spawn, 1.3f, 2.5f, out var position)) return false;
        var machine=prefab!=null?Object.Instantiate(prefab,position,workshop.transform.rotation):Catapult.CreatePlaceholder(position,workshop.transform.rotation);
        SaveGameManager.Stamp(machine,prefab);
        FactionMember.Set(machine,FactionMember.Of(workshop));machine.SetActive(true);
        return true;
    }

    internal static bool TryFindExit(GameObject prefab, GameObject workshop, Transform spawn,
        float defaultRadius, float defaultHeight, out Vector3 position)
    {
        position = default;
        var template=prefab!=null?prefab.GetComponent<NavMeshAgent>():null;
        float radius=template!=null?template.radius:defaultRadius;
        var filter=new NavMeshQueryFilter{agentTypeID=template!=null?template.agentTypeID:0,areaMask=(template!=null?template.areaMask:NavMesh.AllAreas)&~WallNavigation.AreaMask};
        Vector3 exit=spawn!=null?spawn.position:workshop.transform.position+workshop.transform.forward*6;
        if(spawn==null&&BuildingEntrance.TryGetWorldPoint(workshop,out var door))
        {
            var body=BuildingRoadAccess.SolidBody(workshop);
            var outward=door-(body!=null?body.bounds.center:workshop.transform.position);outward.y=0;
            if(outward.sqrMagnitude<.01f)outward=workshop.transform.forward;
            exit=door+outward.normalized*(radius+.5f);
        }
        if(!NavMesh.SamplePosition(exit,out var hit,2,filter))return false;
        float height=template!=null?template.height:defaultHeight;
        if(Physics.CheckCapsule(hit.position+Vector3.up*(radius+.1f),hit.position+Vector3.up*Mathf.Max(radius+.1f,height-radius),radius,~0,QueryTriggerInteraction.Ignore))return false;
        position = hit.position;
        return true;
    }
}

public static class RamFactory
{
    public static bool Valid(GameObject prefab) => prefab == null || prefab.GetComponent<BatteringRam>() != null;

    public static bool TrySpawn(GameObject prefab, GameObject workshop, Transform spawn)
    {
        if (!Valid(prefab) || !CatapultFactory.TryFindExit(prefab, workshop, spawn, 1.05f, 2.2f, out var position))
            return false;
        var ram = prefab != null
            ? Object.Instantiate(prefab, position, workshop.transform.rotation)
            : BatteringRam.CreatePlaceholder(position, workshop.transform.rotation);
        SaveGameManager.Stamp(ram, prefab);
        FactionMember.Set(ram, FactionMember.Of(workshop));
        ram.SetActive(true);
        return true;
    }
}
