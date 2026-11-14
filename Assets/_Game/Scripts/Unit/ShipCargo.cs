using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ShipHull), typeof(GettingResources))]
public class ShipCargo : MonoBehaviour
{
    private readonly CargoHold hold = new CargoHold();
    public CargoHold Hold => hold.Initialize(GetComponent<GettingResources>(), GetComponent<ShipHull>().CargoCapacity);
    public int Stored => Hold.Count;
    public int FreeSpace => Hold.FreeSpace;
    private void OnDisable() { hold.ClearReservations(); }
}
