using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Text;

namespace _Game.Scripts.UI
{
    /// <summary>Подключается к созданной вручную панели больницы.</summary>
    public class HospitalPanel : MonoBehaviour
    {
        public TMP_Text bedsText,foodText,patientsText,statusText;
        public Button dischargeAllButton;
        [SerializeField] private Hospital target;
        private float nextRefresh;
        private void Awake(){if(dischargeAllButton!=null)dischargeAllButton.onClick.AddListener(DischargeAll);}
        public void Bind(Hospital hospital){target=hospital;Refresh();}
        private void OnEnable(){Refresh();}
        private void Update(){if(Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+.2f;Refresh();}}
        public void DischargeAll(){if(target!=null)target.DischargeAll();Refresh();}
        private void Refresh()
        {
            if(bedsText!=null)bedsText.text=target!=null?$"Койки: {target.OccupiedBeds}/{target.bedCount}":"Койки: —";
            if(foodText!=null)foodText.text=target!=null?$"Еда: {target.StoredMeals}/{target.capacity} • {target.foodPerTreatment} за цикл":"Еда: —";
            if(statusText!=null)statusText.text=target==null?"Выберите больницу":!target.Operational?"Нет подъезда или больница недоступна"
                :target.AvailableMeals<target.foodPerTreatment?"Нужна доставка еды":"Больница работает";
            if(patientsText!=null)
            {
                var lines=new StringBuilder();
                if(target!=null)foreach(var patient in target.Patients)
                {
                    if(patient==null)continue;var unit=patient.GetComponent<Unit>();
                    lines.AppendLine($"{patient.name}: {patient.Status} • {(unit!=null?unit.unitHealth:0):0} HP • цикл {patient.Progress:P0}");
                }
                patientsText.text=lines.Length>0?lines.ToString():"Нет пациентов";
            }
            if(dischargeAllButton!=null)dischargeAllButton.interactable=target!=null&&target.OccupiedBeds>0;
        }
        private void OnDestroy(){if(dischargeAllButton!=null)dischargeAllButton.onClick.RemoveListener(DischargeAll);}
    }
}
