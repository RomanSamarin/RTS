using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SelectedUnitsPanel : MonoBehaviour
{
    [Tooltip("Optional child containing the visible panel. Keep this script on an active object.")]
    public GameObject panelRoot;
    public RectTransform cardsContainer;
    public SelectedUnitCard cardPrefab;

    private readonly List<SelectedUnitCard> cards = new List<SelectedUnitCard>();
    private int shownCount;

    private void LateUpdate()
    {
        var manager = ManagerGO.Instance;
        int count = 0;
        if (manager != null && !SaveGameManager.IsLoading && cardsContainer != null && cardPrefab != null)
        {
            foreach (var selected in manager.unitsSelected)
            {
                if (selected == null || !selected.TryGetComponent<Unit>(out var unit)
                    || !unit.isActiveAndEnabled || unit.unitHealth <= 0f) continue;

                if (count == cards.Count) cards.Add(Instantiate(cardPrefab, cardsContainer));
                var card = cards[count++];
                card.Show(unit);
                card.gameObject.SetActive(true);
            }
        }

        for (int i = count; i < shownCount; i++) cards[i].gameObject.SetActive(false);
        shownCount = count;
        if (panelRoot != null && panelRoot != gameObject) panelRoot.SetActive(count > 0);
    }

    private void OnDisable()
    {
        for (int i = 0; i < shownCount; i++) cards[i].gameObject.SetActive(false);
        shownCount = 0;
        if (panelRoot != null && panelRoot != gameObject) panelRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        foreach (var card in cards)
            if (card != null) Destroy(card.gameObject);
    }
}