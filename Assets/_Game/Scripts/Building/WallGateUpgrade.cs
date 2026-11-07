using UnityEngine;

/// <summary>Хранит исходную стену до завершения перестройки; отмена возвращает её.</summary>
public class WallGateUpgrade : MonoBehaviour
{
    private GameObject original, gate;
    private ObjectPlacer placer;
    private GridData occupied;
    private Vector3Int cell;
    private int originalIndex, gateIndex, originalId;
    private bool initialized, finished;
    public void Initialize(GameObject source, GameObject replacement, ObjectPlacer placer, GridData occupied,
        Vector3Int cell, int sourceIndex, int replacementIndex, int sourceId)
    {
        original = source; gate = replacement; this.placer = placer; this.occupied = occupied;
        this.cell = cell; originalIndex = sourceIndex; gateIndex = replacementIndex; originalId = sourceId; initialized = true;
    }
    public void Finish(ConstructionSite site)
    {
        finished = true;
        if (original != null) placer.RemoveObjectAt(originalIndex);
    }
    public void RestoreSavedHooks()
    {
        occupied=PlacementSystem.Instance!=null?PlacementSystem.Instance.SavedFurnitureData:occupied;
        var site=GetComponent<ConstructionSite>();if(site!=null){site.Completed-=Finish;site.Completed+=Finish;}
    }
    private void Update()
    {
        if(SaveGameManager.IsLoading)return;
        if (initialized && !finished && (original == null || gate == null)) Destroy(gameObject);
    }
    private void OnDestroy()
    {
        if (!initialized || finished) return;
        if (gate != null) placer.RemoveObjectAt(gateIndex);
        int current = occupied.GetRepresentationIndex(cell);
        if (current == gateIndex) occupied.RemoveObjectAt(cell);
        if (original == null) return;
        if (current != gateIndex && current != -1 && current != originalIndex)
        { placer.RemoveObjectAt(originalIndex); return; }
        if (occupied.CanPlaceObjectAt(cell, Vector2Int.one))
            occupied.AddObjectAt(cell, Vector2Int.one, originalId, originalIndex);
        original.SetActive(true);
    }
}
