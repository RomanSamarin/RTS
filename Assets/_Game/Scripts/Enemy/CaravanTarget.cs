using System.Collections.Generic;
using UnityEngine;

/// <summary>Маркер торговой/снабженческой повозки для дорожных засад.</summary>
[RequireComponent(typeof(Unit))]
public class CaravanTarget : MonoBehaviour
{
    private static readonly List<CaravanTarget> targets = new List<CaravanTarget>();
    public static IReadOnlyList<CaravanTarget> All => targets;
    public bool IsAvailable => isActiveAndEnabled && GetComponent<Unit>().unitHealth > 0;
    private void OnEnable() { if (!targets.Contains(this)) targets.Add(this); }
    private void OnDisable() { targets.Remove(this); }
}
