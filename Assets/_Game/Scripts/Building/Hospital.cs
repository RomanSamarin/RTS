using System.Collections.Generic;
using UnityEngine;

/// <summary>Использует тот же физический запас еды и рейсы носильщиков, что и таверна.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BuildingHealth))]
public class Hospital : Tavern
{
    public static readonly List<Hospital> AllHospitals=new List<Hospital>();
    [Min(1)] public int bedCount=4;
    [Min(.1f)] public float treatmentSeconds=5;
    [Min(1)] public int foodPerTreatment=1;
    [Min(1)] public float healingPerTreatment=20;
    public Transform exitPoint;
    public bool debugHospital=true;
    private readonly HospitalBeds<HospitalPatient> beds=new HospitalBeds<HospitalPatient>();
    private BuildingHealth health;
    public int OccupiedBeds=>beds.Count;
    public int FreeBeds=>Mathf.Max(0,bedCount-beds.Count);
    public bool Operational=>isActiveAndEnabled&&health!=null&&!health.IsDead&&BuildingRoadAccess.Allows(this);
    public override int RequestedMeals=>Operational?base.RequestedMeals:0;
    public IEnumerable<HospitalPatient> Patients=>beds.Patients;
    private void Awake(){health=GetComponent<BuildingHealth>();FactionMember.Ensure(gameObject);BuildingRoadAccess.Ensure(gameObject);}
    protected override void OnEnable(){base.OnEnable();if(!AllHospitals.Contains(this))AllHospitals.Add(this);}
    public bool Reserve(HospitalPatient patient)
    {
        if(!Operational||patient==null||!FactionMember.CanUse(patient.gameObject,gameObject))return false;
        bool result=beds.Reserve(patient,bedCount);
        if(result&&debugHospital)Debug.Log($"[Больница] {name}: койка зарезервирована для {patient.name}, занято {OccupiedBeds}/{bedCount}.",this);
        return result;
    }
    public bool HasBed(HospitalPatient patient)=>beds.Contains(patient);
    public void Release(HospitalPatient patient){beds.Release(patient);}
    public void DischargeAll(){foreach(var patient in new List<HospitalPatient>(beds.Patients))if(patient!=null)patient.RequestDischarge();}
    // Больница обслуживает пациентов, а не случайных посетителей таверны.
    public override bool TryServe(int portions)=>false;
    public bool ConsumeTreatment()=>Operational&&base.TryServe(Mathf.Max(1,foodPerTreatment));
    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        if(health!=null&&health.IsDead)Evacuate();
        foreach(var patient in new List<HospitalPatient>(beds.Patients))
            if(patient==null||!patient.isActiveAndEnabled)beds.Release(patient);
    }
    private void Evacuate(){foreach(var patient in new List<HospitalPatient>(beds.Patients))if(patient!=null)patient.HospitalUnavailable();}
    protected override void OnDisable(){Evacuate();AllHospitals.Remove(this);base.OnDisable();}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry(){AllHospitals.Clear();}
}
