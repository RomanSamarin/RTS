using UnityEngine;

/// <summary>Приоритет новых заказов носильщиков. Не отменяет уже зарезервированную доставку.</summary>
[DisallowMultipleComponent]
public class DeliveryPriority : MonoBehaviour
{
    [SerializeField] private DeliveryPriorityLevel priority=DeliveryPriorityLevel.Normal;
    public DeliveryPriorityLevel Priority => (DeliveryPriorityLevel)Mathf.Clamp((int)priority,0,3);
    public void SetPriority(int value)
    {
        if(!FactionMember.IsPlayer(gameObject))return;
        priority=(DeliveryPriorityLevel)Mathf.Clamp(value,0,3);
    }
    public static DeliveryPriorityLevel Of(GameObject building)
    {
        var setting=building!=null?building.GetComponentInParent<DeliveryPriority>():null;
        return setting!=null?setting.Priority:DeliveryPriorityLevel.Normal;
    }
}
