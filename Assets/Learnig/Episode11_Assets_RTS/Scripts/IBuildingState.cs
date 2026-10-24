using UnityEngine;

public interface IBuildingState
{
    void EndState();
    bool OnAction(Vector3Int gridPosition);
    void UpdateState(Vector3Int gridPosition);
}