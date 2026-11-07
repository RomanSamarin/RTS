using UnityEngine;

[DisallowMultipleComponent]
public class ArmyMember : MonoBehaviour
{
    public ArmyGroup Army { get; private set; }
    internal void SetArmy(ArmyGroup army) { Army = army; }
    public void LeaveArmy() { if (Army != null) Army.RemoveMember(this); }
    private void OnDisable() { LeaveArmy(); }
}
