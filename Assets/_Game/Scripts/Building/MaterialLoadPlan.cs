using System.Collections.Generic;
using _Game.Scripts.UI;

/// <summary>Распределяет вместимость между доступными видами, а не заполняет её первым видом.</summary>
public static class MaterialLoadPlan
{
    public static Dictionary<ResourceType, int> Create(Dictionary<ResourceType, int> available, int capacity)
    {
        var result = new Dictionary<ResourceType, int>();
        var types = new List<ResourceType>(available.Keys);
        types.Sort();
        bool added = true;
        while (capacity > 0 && added)
        {
            added = false;
            foreach (var type in types)
            {
                result.TryGetValue(type, out int count);
                if (count >= available[type]) continue;
                result[type] = count + 1;
                capacity--; added = true;
                if (capacity == 0) break;
            }
        }
        return result;
    }
}
