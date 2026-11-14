using UnityEngine;

/// <summary>A hitched horse shares its wagon's transport reservation, not an extra seat.</summary>
[DisallowMultipleComponent]
public class ShipPassengerCompanion : MonoBehaviour
{
    public ShipPassenger passenger;
}
