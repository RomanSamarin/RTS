using System;
using System.Collections.Generic;

/// <summary>Резерв койки сохраняется при приёме: свободное место нельзя выдать второму пациенту.</summary>
public sealed class HospitalBeds<T> where T:class
{
    private readonly HashSet<T> patients=new HashSet<T>();
    public int Count=>patients.Count;
    public IEnumerable<T> Patients=>patients;
    public bool Contains(T patient)=>patient!=null&&patients.Contains(patient);
    public bool Reserve(T patient,int capacity)
        =>patient!=null&&patients.Count<Math.Max(0,capacity)&&patients.Add(patient);
    public void Release(T patient){if(patient!=null)patients.Remove(patient);}
}

/// <summary>Одна реальная порция оплачивает один цикл лечения. Без оплаты цикл не начинается.</summary>
public sealed class HospitalTreatment
{
    private bool paid;
    private float elapsed;
    public float Progress(float seconds)=>paid?Math.Min(1,elapsed/Math.Max(.1f,seconds)):0;
    public bool WaitingFood { get; private set; }
    public float Advance(float delta,float seconds,float healing,Func<bool> consume)
    {
        if(delta<=0)return 0;
        if(!paid)
        {
            if(!consume()){WaitingFood=true;return 0;}
            paid=true;elapsed=0;WaitingFood=false;
        }
        elapsed+=Math.Max(0,delta);
        if(elapsed<Math.Max(.1f,seconds))return 0;
        paid=false;elapsed=0;return Math.Max(0,healing);
    }
}
