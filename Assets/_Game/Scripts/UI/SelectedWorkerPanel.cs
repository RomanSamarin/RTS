using System.Text;
using UnityEngine;
using UnityEngine.AI;
using TMPro;

namespace _Game.Scripts.UI
{
    /// <summary>Данные для панели, созданной пользователем в Canvas.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class SelectedWorkerPanel : MonoBehaviour
    {
        public TMP_Text workerNameText;
        public TMP_Text taskText;
        public TMP_Text cargoText;
        public TMP_Text capacityText;
        public TMP_Text reservationText;
        private CanvasGroup group;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            SetVisible(false);
        }
        private void SetVisible(bool visible)
        {
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
        private static void SetText(TMP_Text label, string value)
        {
            if (label != null && label.text != value) label.text = value;
        }
        private static string ResourceName(ResourceType type) => type switch
        {
            ResourceType.Wood => "Дерево", ResourceType.Stone => "Камень",
            ResourceType.Iron => "Железо", ResourceType.Gold => "Золото",
            ResourceType.Wheat => "Пшеница", ResourceType.Flour => "Мука",
            ResourceType.Bread => "Хлеб", ResourceType.Food => "Еда",
            ResourceType.Fish => "Сырая рыба", ResourceType.Meat => "Сырое мясо",
            ResourceType.Berries => "Ягоды", ResourceType.Sword => "Мечи",
            ResourceType.Spear => "Копья", ResourceType.Bow => "Луки",
            ResourceType.Shield => "Щиты", ResourceType.SiegeLadder => "Осадные лестницы", _ => type.ToString()
        };
        private void Update()
        {
            if (ManagerGO.Instance == null) { SetVisible(false); return; }
            ResourcesMove worker = null;
            foreach (var selected in ManagerGO.Instance.unitsSelected)
            {
                if (selected == null) continue;
                if (worker != null) { SetVisible(false); return; }
                worker = selected.GetComponent<ResourcesMove>();
                if (worker == null) { SetVisible(false); return; }
            }
            if (worker == null) { SetVisible(false); return; }
            SetVisible(true);
            SetText(workerNameText, worker.name);
            var command = worker.GetComponent<GOScript>();
            var eating = worker.GetComponent<UnitEat>();
            string task = "Ожидает работу";
            var patient=worker.GetComponent<HospitalPatient>();
            var catapult=worker.GetComponent<Catapult>();
            var ram = worker.GetComponent<BatteringRam>();
            if(worker.GetComponent<ShipPassenger>()?.IsAssigned??false)task=worker.GetComponent<ShipPassenger>().Status;
            else if(worker.IsShipCrew)task=worker.GetComponent<ShipCrewMember>()?.Status??"На борту";
            else if(command!=null&&command.IsRetreating)task="Отступает";
            else if(catapult!=null)task=catapult.Status;
            else if(ram!=null)task=ram.Status;
            else if(worker.GetComponent<RamCrewMember>() is RamCrewMember crew && crew.IsAssigned)task=crew.Status;
            else if(patient!=null&&patient.IsBusy)task=patient.Status;
            else if (eating != null && eating.IsBusy) task = eating.CurrentTask;
            else if(ArmyRejoin.Returning(worker.gameObject))task=worker.GetComponent<ArmyRejoin>().Status;
            else if(worker.GetComponent<MilitaryOrders>()?.Current==MilitaryOrders.Order.Patrol)task=worker.GetComponent<MilitaryOrders>().Status;
            else if (command != null && command.isCommandToMove) task = "Выполняет приказ движения";
            else if(worker.AssignedWorkplace!=null&&!BuildingRoadAccess.Allows(worker.AssignedWorkplace))task="Нет дороги к зданию";
            else if (worker.IsTrainingRecruit) task = worker.GetComponent<TrainingRecruit>().CurrentTask;
            else if (worker.IsReturningToWorkplace) task = "Идёт к месту работы";
            else if (worker.role == ResourcesMove.UnitRole.Watchman)
                task = worker.IsAtAssignedWorkplace ? "Ведёт дозор" : "Идёт на пост";
            else if (worker.role == ResourcesMove.UnitRole.Carrier) task = worker.GetComponent<CarrierTask>()?.Status ?? task;
            else if (worker.role == ResourcesMove.UnitRole.Farmer) task = worker.GetComponent<Farmer>()?.CurrentTask ?? task;
            else if (worker.role == ResourcesMove.UnitRole.Miner) task = worker.GetComponent<Miner>()?.CurrentTask ?? task;
            else if (worker.IsProductionWorker) task = worker.GetComponent<ProductionWorker>()?.CurrentTask ?? task;
            else if (worker.IsFoodGatherer) task = worker.GetComponent<FoodGatheringWorker>()?.CurrentTask ?? task;
            else if (worker.role == ResourcesMove.UnitRole.Lumberjack
                || worker.role == ResourcesMove.UnitRole.StoneMason
                || worker.role == ResourcesMove.UnitRole.Fisher)
            {
                var attack = worker.GetComponent<AttackController>();
                var agent = worker.GetComponent<NavMeshAgent>();
                if (worker.IsDeliveringResources) task = "Доставляет";
                else if (attack == null || attack.targetToAttack == null) task = "Ищет работу";
                else if (agent != null && agent.enabled && agent.isOnNavMesh && agent.hasPath
                    && (agent.pathPending || agent.remainingDistance > agent.stoppingDistance))
                    task = "Перемещается";
                else task = worker.role switch
                {
                    ResourcesMove.UnitRole.Lumberjack => "Рубит дерево",
                    ResourcesMove.UnitRole.StoneMason => "Добывает камень",
                    _ => "Ловит рыбу"
                };
            }
            else
            {
                var attack = worker.GetComponent<AttackController>();
                var agent = worker.GetComponent<NavMeshAgent>();
                if (attack != null && attack.targetToAttack != null)
                    task = agent != null && agent.hasPath ? "Перемещается" : "Работает";
                else if (worker.role == ResourcesMove.UnitRole.Builder) task = worker.GetComponent<Builder>()?.MaterialTask ?? "Ищет стройку";
            }
            SetText(taskText, "Задача: " + task);
            var cargo = worker.GetComponent<GettingResources>();
            var contents = new StringBuilder(); int total = 0;
            if (cargo != null) foreach (var type in WarehouseStorage.Types)
            {
                int count = cargo.GetStoredAmount(type); if (count <= 0) continue;
                total += count; contents.Append(ResourceName(type)).Append(": ").Append(count).Append("  ");
            }
            SetText(capacityText, "Груз: " + total + "/" + worker.MaxUnitResources);
            SetText(cargoText, total == 0 ? "Рюкзак пуст" : contents.ToString());
            var delivery = worker.GetComponent<CarrierTask>();
            SetText(reservationText, worker.role == ResourcesMove.UnitRole.Carrier && delivery != null && delivery.DestinationObject != null
                ? "Резерв места: " + delivery.ReservedCargo + " → " + delivery.DestinationObject.name : "");
        }
    }
}
