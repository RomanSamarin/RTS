using System;
public static class HospitalChecks
{
    private static void Check(bool ok,string why){if(!ok)throw new Exception("Hospital: "+why);}
    public static void Run()
    {
        var beds=new HospitalBeds<object>();var first=new object();var second=new object();
        Check(beds.Reserve(first,1),"reserve before travel");
        Check(!beds.Reserve(first,2),"same patient cannot reserve twice");
        Check(!beds.Reserve(second,1)&&beds.Count==1,"en-route reservation prevents overbooking");
        Check(beds.Contains(first)&&beds.Count==1,"admission preserves existing reservation");
        beds.Release(first);beds.Release(first);
        Check(beds.Count==0&&beds.Reserve(second,1),"cancel or discharge releases exactly one bed");
        Check(!beds.Reserve(new object(),0),"zero beds accepts nobody");
        var cycle=new HospitalTreatment();int stock=0,spent=0;
        Func<bool> pay=()=>{if(stock<=0)return false;stock--;spent++;return true;};
        Check(cycle.Advance(10,5,20,pay)==0&&cycle.Progress(5)==0&&cycle.WaitingFood,"no healing without delivered food");
        stock=2;
        Check(cycle.Advance(0,5,20,pay)==0&&stock==2,"pause cannot consume food");
        Check(cycle.Advance(2.5f,5,20,pay)==0&&stock==1&&spent==1,"physical food charged once at start");
        Check(cycle.Advance(2.5f,5,20,pay)==20&&spent==1,"one completed batch heals once");
        Check(cycle.Advance(5,5,20,pay)==20&&stock==0&&spent==2,"next batch requires new real portion");
        Check(cycle.Advance(100,5,20,pay)==0&&spent==2,"empty hospital cannot continue free treatment");
        Console.WriteLine("PASS: hospital bed reservations, competing patients, release, physical food, paused time and paid healing cycles");
    }
}
