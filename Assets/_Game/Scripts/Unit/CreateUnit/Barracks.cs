using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using _Game.Scripts.UI;

/// <summary>
/// Казарма: очередь тренировки как в классических RTS.
/// Каждое нажатие кнопки добавляет на панель здания отдельный слот
/// (иконка юнита + шкала над ней). Слоты стоят в ряд,
/// тренируется первый, остальные ждут своей очереди.
/// </summary>
public class Barracks : MonoBehaviour
{
    [Header("Настройки")]
    public GameObject objectToSpawn;
    public GameObject SpawnPoint;
    public float spawnDelay = 3f;

    [Header("Стоимость юнита")]
    public int woodCost = 50;
    public int stoneCost = 0;
    public int wheatCost = 20;
    public int fishCost = 20;

    [Header("UI очереди тренировки")]
    [Tooltip("Спрайт юнита в слоте очереди")]
    public Sprite unitIcon;

    [Tooltip("Шаблон слота очереди (обычно UnitTraining — иконка + шкала). " +
             "Если пусто — ищется автоматически в меню этого здания. " +
             "Сам шаблон на экране не показывается, с него копируются слоты")]
    public GameObject queueSlotTemplate;

    [Tooltip("Промежуток между слотами по горизонтали, px")]
    public float slotSpacing = 6f;

    [Tooltip("Сколько слотов помещается в одном ряду, остальные переносятся вниз")]
    public int slotsPerRow = 8;

    [Tooltip("Смещение всего ряда слотов относительно шаблона, px")]
    public Vector2 queueRowOffset = Vector2.zero;

    [Tooltip("Промежуток между рядами слотов, px")]
    public float rowSpacing = 4f;

    [Tooltip("Шкала показывает оставшееся время (убывает). " +
             "Выключено — показывает прогресс (растёт)")]
    public bool showRemainingTime = true;

    [Tooltip("Насколько прозрачны иконки слотов, которые ещё не начали тренироваться")]
    [Range(0f, 1f)]
    public float waitingSlotAlpha = 0.45f;

    private class Slot
    {
        public GameObject root;
        public Image icon;
        public Image bar;
        public RectTransform rect;
    }

    private readonly Queue<GameObject> unitQueue = new Queue<GameObject>();
    private readonly List<Slot> slots = new List<Slot>();

    private bool isTraining;

    private Transform slotContainer;
    private Vector2 slotBasePosition;
    private Vector2 slotIconOffset;
    private float slotStepX = 36f;
    private float slotStepY = 42f;

    void Start()
    {
        Debug.Log("Barracks::Start(); -- objectToSpawn:" + objectToSpawn);
        Debug.Log("Barracks::Start(); -- SpawnPoint:" + SpawnPoint);

        ResolveQueueTemplate();
    }

    // ------------------------------------------------------------- кнопка

    public void OnButtonSpawnUnit()
    {
        Debug.Log("Нажата кнопка");

        if (objectToSpawn == null)
            return;

        if (ResourcesManager.Instance != null &&
            !ResourcesManager.Instance.HasEnough(woodCost, stoneCost, wheatCost, fishCost))
        {
            Debug.Log("Не хватает ресурсов для тренировки");
            return;
        }

        if (ResourcesManager.Instance != null)
            ResourcesManager.Instance.Spend(woodCost, stoneCost, wheatCost, fishCost);

        GameObject prefab = objectToSpawn;

        unitQueue.Enqueue(prefab);

        // Слот появляется сразу по нажатию — как в классических RTS,
        // ещё до того, как начнётся его тренировка.
        CreateSlot();

        if (!isTraining)
            StartCoroutine(ProcessQueueRoutine());
    }

    // ------------------------------------------------------------- очередь

    IEnumerator ProcessQueueRoutine()
    {
        isTraining = true;

        while (unitQueue.Count > 0)
        {
            GameObject nextUnit = unitQueue.Peek();
            Slot current = slots.Count > 0 ? slots[0] : null;

            SetSlotActive(current, true);

            float elapsed = 0f;

            while (elapsed < spawnDelay)
            {
                elapsed += Time.deltaTime;
                SetSlotProgress(current, spawnDelay > 0f ? elapsed / spawnDelay : 1f);
                yield return null;
            }

            SetSlotProgress(current, 1f);

            if (SpawnPoint != null)
                Instantiate(nextUnit, SpawnPoint.transform.position, SpawnPoint.transform.rotation);

            unitQueue.Dequeue();

            RemoveSlot(current);

            yield return null;
        }

        isTraining = false;
    }

    // ------------------------------------------------------------------ UI

    /// <summary>
    /// Находит шаблон слота (UnitTraining) в меню здания и запоминает,
    /// откуда и с каким шагом строить ряд слотов.
    /// </summary>
    private void ResolveQueueTemplate()
    {
        if (queueSlotTemplate == null)
        {
            UiSelectBuilding selector = GetComponent<UiSelectBuilding>();

            if (selector == null)
                selector = GetComponentInParent<UiSelectBuilding>();

            if (selector == null)
                selector = GetComponentInChildren<UiSelectBuilding>();

            GameObject menu = selector != null ? selector.UIPanel : null;

            if (menu != null)
            {
                Transform found = FindDeepChild(menu.transform, "UnitTraining");
                if (found != null)
                    queueSlotTemplate = found.gameObject;
            }
        }

        if (queueSlotTemplate == null)
        {
            Debug.LogWarning("[Barracks] Не найден шаблон слота очереди (UnitTraining) — " +
                             "UI тренировки не будет. Назначь Queue Slot Template вручную.", this);
            return;
        }

        RectTransform templateRect = queueSlotTemplate.GetComponent<RectTransform>();

        if (templateRect != null)
        {
            slotContainer = templateRect.parent;
            slotBasePosition = templateRect.anchoredPosition;

            // Шаг ряда считаем по реальному размеру иконки, а не по размеру
            // служебного RectTransform'а шаблона (он бывает 100x100 «на вырост»).
            float width = 30f;
            float height = 30f;

            Image templateIcon, templateBar;
            FindSlotImages(queueSlotTemplate, out templateIcon, out templateBar);

            if (templateIcon != null)
            {
                width = templateIcon.rectTransform.rect.width;
                height = templateIcon.rectTransform.rect.height;

                // Иконка в шаблоне сдвинута относительно его RectTransform
                // (анкоры в углу). Запоминаем сдвиг, чтобы ряд слотов
                // отсчитывался от центров иконок, а не от служебного прямоугольника.
                // По X выравниваем левый край первой иконки по точке шаблона,
                // по Y оставляем исходное положение — так слоты не уезжают
                // вверх относительно панели.
                slotIconOffset = GetLocalCenter(templateIcon.rectTransform, templateRect);
                slotIconOffset.x -= width * 0.5f;
                slotIconOffset.y = 0f;
            }

            if (templateBar != null)
                height += templateBar.rectTransform.rect.height + 2f;

            slotStepX = width + slotSpacing;
            slotStepY = height + rowSpacing;
        }

        if (unitIcon == null)
            Debug.LogWarning("[Barracks] Не назначен Unit Icon — иконка в слотах очереди будет пустой.", this);

        // Шаблон — только образец, на экране его быть не должно
        queueSlotTemplate.SetActive(false);
    }

    private void CreateSlot()
    {
        if (queueSlotTemplate == null || slotContainer == null)
            return;

        GameObject clone = Instantiate(queueSlotTemplate, slotContainer);
        clone.name = "TrainingSlot";

        // Внутри UnitIsSpawningPanel стоит HorizontalLayoutGroup — он бы
        // расставил слоты по-своему. Отключаем автораскладку и ставим сами.
        LayoutElement layoutElement = clone.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = clone.AddComponent<LayoutElement>();
        layoutElement.ignoreLayout = true;

        clone.SetActive(true);

        Slot slot = new Slot
        {
            root = clone,
            rect = clone.GetComponent<RectTransform>()
        };

        FindSlotImages(clone, out slot.icon, out slot.bar);

        if (slot.icon != null)
        {
            slot.icon.preserveAspect = true;

            if (unitIcon != null)
                slot.icon.sprite = unitIcon;
        }

        if (slot.bar != null)
            slot.bar.type = Image.Type.Filled;

        slots.Add(slot);

        SetSlotProgress(slot, 0f);
        SetSlotActive(slot, false);
        RebuildLayout();
    }

    private void RemoveSlot(Slot slot)
    {
        if (slot == null)
            return;

        slots.Remove(slot);

        if (slot.root != null)
            Destroy(slot.root);

        RebuildLayout();

        if (slots.Count > 0)
            SetSlotActive(slots[0], true);
    }

    /// <summary>Раскладывает слоты слева направо от исходной позиции шаблона.</summary>
    private void RebuildLayout()
    {
        int perRow = Mathf.Max(1, slotsPerRow);

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].rect == null)
                continue;

            int column = i % perRow;
            int row = i / perRow;

            Vector2 gridPoint = slotBasePosition + queueRowOffset +
                                new Vector2(column * slotStepX, -row * slotStepY);

            slots[i].rect.anchoredPosition = gridPoint - slotIconOffset;
        }
    }

    /// <summary>
    /// normalizedElapsed — 0 в начале тренировки слота, 1 в конце.
    /// Шкала показывает либо остаток (убывает), либо прогресс (растёт).
    /// </summary>
    private void SetSlotProgress(Slot slot, float normalizedElapsed)
    {
        if (slot == null || slot.bar == null)
            return;

        float value = showRemainingTime ? 1f - normalizedElapsed : normalizedElapsed;
        slot.bar.fillAmount = Mathf.Clamp01(value);
    }

    /// <summary>Активный слот — яркий и со шкалой, ждущие — приглушённые без шкалы.</summary>
    private void SetSlotActive(Slot slot, bool active)
    {
        if (slot == null)
            return;

        if (slot.bar != null)
            slot.bar.enabled = active;

        if (slot.icon == null)
            return;

        Color color = slot.icon.color;
        color.a = active ? 1f : Mathf.Clamp01(waitingSlotAlpha);
        slot.icon.color = color;
    }

    /// <summary>
    /// Ищет в слоте шкалу (Image типа Filled) и иконку юнита.
    /// </summary>
    private static void FindSlotImages(GameObject slot, out Image icon, out Image bar)
    {
        icon = null;
        bar = null;

        Image[] images = slot.GetComponentsInChildren<Image>(true);

        foreach (Image image in images)
        {
            if (image.type == Image.Type.Filled)
            {
                if (bar == null)
                    bar = image;

                continue;
            }

            if (icon == null)
                icon = image;
        }
    }

    /// <summary>
    /// Центр дочернего RectTransform относительно пивота родителя.
    /// Считается по анкорам, поэтому работает и для выключенных объектов.
    /// </summary>
    private static Vector2 GetLocalCenter(RectTransform child, RectTransform parent)
    {
        Vector2 anchorCenter = (child.anchorMin + child.anchorMax) * 0.5f;

        Vector2 offset = new Vector2(
            (anchorCenter.x - 0.5f) * parent.rect.width,
            (anchorCenter.y - 0.5f) * parent.rect.height);

        return offset + child.anchoredPosition;
    }

    private static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;

            Transform found = FindDeepChild(child, childName);
            if (found != null)
                return found;
        }

        return null;
    }
}
