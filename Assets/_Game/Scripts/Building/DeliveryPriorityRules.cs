using System;

public enum DeliveryPriorityLevel { Low, Normal, High, Urgent }

/// <summary>Недоступный срочный заказ не блокирует выполнимые заказы ниже приоритетом.</summary>
public static class DeliveryPriorityRules
{
    public static bool TryPlan(Func<DeliveryPriorityLevel, bool> tryLevel)
    {
        for(int level=(int)DeliveryPriorityLevel.Urgent;level>=(int)DeliveryPriorityLevel.Low;level--)
            if(tryLevel((DeliveryPriorityLevel)level))return true;
        return false;
    }
}
