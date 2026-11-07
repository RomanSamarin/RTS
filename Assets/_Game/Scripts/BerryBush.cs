using UnityEngine;
using _Game.Scripts.UI;

public class BerryBush : FoodResourceSource
{
    public override ResourceType Resource => ResourceType.Berries;
    [Tooltip("Необязательно: отдельный дочерний объект с ягодами. Сам куст остаётся.")]
    public GameObject berryVisual;
    protected override void OnEnable()
    {
        base.OnEnable();
        if (berryVisual != null && berryVisual != gameObject) berryVisual.SetActive(Remaining > 0);
    }
    protected override void OnDepleted()
    { if (berryVisual != null && berryVisual != gameObject) berryVisual.SetActive(false); }
}
