using System.Collections.Generic;
using UnityEngine;

public abstract class FoodService : MonoBehaviour
{
    private static readonly List<FoodService> services = new List<FoodService>();
    public static IReadOnlyList<FoodService> All => services;
    public Transform servingPoint;
    public abstract int AvailableMeals { get; }
    public abstract bool TryServe(int portions);
    public virtual Vector3 ServicePoint
    {
        get
        {
            if (servingPoint != null) return servingPoint.position;
            return BuildingEntrance.TryGetWorldPoint(gameObject, out var point) ? point : transform.position;
        }
    }
    protected virtual void OnEnable() { if (!services.Contains(this)) services.Add(this); }
    protected virtual void OnDisable() { services.Remove(this); }
}
