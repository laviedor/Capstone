using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 플레이어 탭: 캐릭터 주변 장비 칸, 인벤토리 격자, 선택한 아이템의 상세 정보를 관리한다.
public class PlayerTabUI : MonoBehaviour
{
    // 인벤토리 격자에 표시할 항목 (장비 또는 장식)
    private class Entry
    {
        public EquipmentItemData equipment;
        public BuildingData decoration;
        public int count = 1;

        public object Key => equipment != null ? (object)equipment : decoration;
        public bool IsEquipment => equipment != null;
        public string Name => IsEquipment ? equipment.itemName : decoration.buildingName;
        public Sprite Icon => IsEquipment ? equipment.icon : decoration.buildingIcon;
    }

    // 분류 칩 번호
    private const int FilterAll = 0;
    private const int FilterEquipment = 1;
    private const int FilterDecoration = 2;

    [Header("Equipment")]
    [SerializeField] private EquipmentSlotUI[] equipmentSlots;
    [SerializeField] private CharacterPreviewUI characterPreview;
    [SerializeField] private Button unequipAllButton;

    // 인벤토리 격자: 처음에 보이는 칸(6 x 3)은 씬에 있는 칸을 쓰고, 칸이 모자라면 한 줄(6칸)씩 늘려 위아래로 스크롤한다.
    //   개수 제한은 없다 (가진 만큼 줄이 늘어난다)
    [Header("Inventory Grid")]
    [SerializeField] private InventorySlotUI[] inventorySlots;
    [SerializeField] private ChipGroupUI filterChips;
    [SerializeField] private TMP_Text countText;

    [Header("Detail")]
    [SerializeField] private Image detailIcon;
    [SerializeField] private TMP_Text detailName;
    [SerializeField] private TMP_Text detailTag;
    [SerializeField] private TMP_Text detailDescription;
    [SerializeField] private Button primaryButton;
    [SerializeField] private TMP_Text primaryLabel;
    [SerializeField] private Button discardButton;

    [Header("Decoration Placement")]
    [SerializeField] private DecorationDatabase decorationDatabase;
    [SerializeField] private BuildingPlacement buildingPlacement;
    [SerializeField] private BuildingModeManager buildingModeManager;
    [SerializeField] private InventoryUI inventoryUI;

    // 현재 격자에 표시 중인 항목
    private readonly List<Entry> visibleEntries = new List<Entry>();

    // 선택한 항목의 키 (EquipmentItemData 또는 BuildingData)
    private object selectedKey;

    // 격자 한 줄의 칸 수
    private const int Columns = 6;
    // 칸이 잘리는 경계를 카드 테두리 안쪽으로 이만큼 들인다
    private const float GridInset = 3f;

    // 지금 쓰는 모든 칸 (씬의 칸 + 늘린 칸)
    private readonly List<InventorySlotUI> slots = new List<InventorySlotUI>();
    private RectTransform gridContent;
    private ScrollRect gridScroll;
    private Vector2 slotOrigin;   // 첫 칸의 위치 (격자 왼쪽 위 기준)
    private Vector2 slotStep;     // 칸 사이 간격 (가로, 세로)
    private Vector2 slotSize;
    private int baseRows;         // 처음에 보이는 줄 수
    private int lastFilter = -1;

    private PlayerEquipment subscribedEquipment;
    private DecorationInventory subscribedDecorations;
    private bool initialized;

    // 버튼과 칸의 클릭 이벤트를 한 번만 연결한다.
    private void Initialize()
    {
        if (initialized)
            return;

        initialized = true;

        foreach (InventorySlotUI slot in inventorySlots ?? new InventorySlotUI[0])
        {
            if (slot == null)
                continue;

            slot.Init(slots.Count);
            slot.OnClicked += OnInventorySlotClicked;
            slots.Add(slot);
        }

        SetupGridScroll();

        foreach (EquipmentSlotUI slotUI in equipmentSlots)
            slotUI.OnClicked += OnEquipmentSlotClicked;

        if (unequipAllButton != null)
            unequipAllButton.onClick.AddListener(UnequipAll);

        if (primaryButton != null)
            primaryButton.onClick.AddListener(OnPrimaryClicked);

        if (discardButton != null)
            discardButton.onClick.AddListener(OnDiscardClicked);

        if (filterChips != null)
            filterChips.OnSelected += _ => Refresh();
    }

    // 탭이 열릴 때 데이터 변경 이벤트를 구독하고 화면을 갱신한다.
    private void OnEnable()
    {
        Initialize();
        Subscribe();
        Refresh();
    }

    // 탭이 닫히면 구독을 해제한다.
    private void OnDisable()
    {
        if (subscribedEquipment != null)
            subscribedEquipment.OnEquipmentChanged -= Refresh;

        if (subscribedDecorations != null)
            subscribedDecorations.OnInventoryChanged -= Refresh;

        subscribedEquipment = null;
        subscribedDecorations = null;
    }

    private void Subscribe()
    {
        PlayerEquipment equipment = PlayerEquipment.Instance;

        if (equipment != null && subscribedEquipment != equipment)
        {
            if (subscribedEquipment != null)
                subscribedEquipment.OnEquipmentChanged -= Refresh;

            subscribedEquipment = equipment;
            subscribedEquipment.OnEquipmentChanged += Refresh;
        }

        DecorationInventory decorations = DecorationInventory.Instance;

        if (decorations != null && subscribedDecorations != decorations)
        {
            if (subscribedDecorations != null)
                subscribedDecorations.OnInventoryChanged -= Refresh;

            subscribedDecorations = decorations;
            subscribedDecorations.OnInventoryChanged += Refresh;
        }
    }

    // 장비 칸, 캐릭터, 인벤토리 격자, 상세 정보를 모두 다시 그린다.
    public void Refresh()
    {
        Subscribe();

        PlayerEquipment equipment = PlayerEquipment.Instance;
        List<Entry> allEntries = BuildEntries(equipment);

        // 분류 칩에 맞는 항목만 남긴다.
        int filter = filterChips != null ? filterChips.Selected : FilterAll;
        visibleEntries.Clear();

        foreach (Entry entry in allEntries)
        {
            if (filter == FilterEquipment && !entry.IsEquipment)
                continue;

            if (filter == FilterDecoration && entry.IsEquipment)
                continue;

            visibleEntries.Add(entry);
        }

        // 선택한 항목이 사라졌으면 첫 항목을 선택한다.
        Entry selected = FindEntry(allEntries, selectedKey);

        if (selected == null)
        {
            selected = visibleEntries.Count > 0 ? visibleEntries[0] : null;
            selectedKey = selected != null ? selected.Key : null;
        }

        // 장비 칸
        foreach (EquipmentSlotUI slotUI in equipmentSlots)
        {
            EquipmentItemData item =
                equipment != null ? equipment.GetEquipped(slotUI.Slot) : null;

            slotUI.Show(item, item != null && (object)item == selectedKey);
        }

        if (characterPreview != null)
            characterPreview.Refresh(equipment);

        // 인벤토리 격자: 필요한 줄 수만큼 칸을 늘리고(최소 처음 줄 수), 분류를 바꾸면 맨 위로
        EnsureRows(Mathf.Max(baseRows, Mathf.CeilToInt(visibleEntries.Count / (float)Columns)));

        if (filter != lastFilter)
        {
            lastFilter = filter;

            if (gridScroll != null)
                gridScroll.verticalNormalizedPosition = 1f;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            if (!slots[i].gameObject.activeSelf)
                continue;

            if (i >= visibleEntries.Count)
            {
                slots[i].ShowEmpty();
                continue;
            }

            Entry entry = visibleEntries[i];
            bool isEquipped =
                entry.IsEquipment && equipment != null && equipment.IsEquipped(entry.equipment);

            slots[i].Show(
                entry.Icon,
                entry.count,
                isEquipped,
                entry.Key == selectedKey
            );
        }

        if (countText != null)
            countText.text = allEntries.Count + "개";

        ShowDetail(selected, equipment);
    }

    // 격자 카드 안에 스크롤 영역을 만들고 씬의 칸들을 그 안으로 옮긴다.
    //   카드(격자) = 스크롤 (마우스 휠, 끌기) / Viewport = 카드 안쪽에서 칸을 잘라 보이는 영역 / Content = 칸들이 놓이는 판
    private void SetupGridScroll()
    {
        if (slots.Count == 0 || gridScroll != null)
            return;

        RectTransform first = (RectTransform)slots[0].transform;
        RectTransform grid = first.parent as RectTransform;

        if (grid == null)
            return;

        // 씬의 칸 배치에서 첫 칸 위치, 칸 간격, 칸 크기를 읽는다 (왼쪽 위 기준, 아래로 +)
        slotOrigin = new Vector2(first.anchoredPosition.x, -first.anchoredPosition.y);
        slotSize = first.rect.size;
        slotStep = new Vector2(
            slots.Count > 1 ? ((RectTransform)slots[1].transform).anchoredPosition.x - first.anchoredPosition.x : slotSize.x + 3f,
            slots.Count > Columns ? first.anchoredPosition.y - ((RectTransform)slots[Columns].transform).anchoredPosition.y : slotSize.y + 4f);
        baseRows = Mathf.Max(1, Mathf.CeilToInt(slots.Count / (float)Columns));

        GameObject viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        RectTransform viewport = (RectTransform)viewportGo.transform;
        viewport.SetParent(grid, false);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(0f, GridInset);
        viewport.offsetMax = new Vector2(0f, -GridInset);

        GameObject contentGo = new GameObject("Content", typeof(RectTransform));
        gridContent = (RectTransform)contentGo.transform;
        gridContent.SetParent(viewport, false);
        gridContent.anchorMin = new Vector2(0f, 1f);
        gridContent.anchorMax = new Vector2(1f, 1f);
        gridContent.pivot = new Vector2(0.5f, 1f);
        gridContent.anchoredPosition = Vector2.zero;

        foreach (InventorySlotUI slot in slots)
            slot.transform.SetParent(gridContent, false);

        gridScroll = grid.gameObject.AddComponent<ScrollRect>();
        gridScroll.content = gridContent;
        gridScroll.viewport = viewport;
        gridScroll.horizontal = false;
        gridScroll.vertical = true;
        gridScroll.movementType = ScrollRect.MovementType.Clamped;
        gridScroll.scrollSensitivity = 20f;
        gridScroll.verticalScrollbar = CreateScrollbar(grid);
        gridScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

        // 끌기·휠을 받을 수 있게 카드 그림이 마우스를 받도록
        Graphic background = grid.GetComponent<Graphic>();

        if (background != null)
            background.raycastTarget = true;

        EnsureRows(baseRows);
    }

    // 격자 오른쪽의 얇은 스크롤 막대 (칸이 넘칠 때만 보인다)
    private static Scrollbar CreateScrollbar(RectTransform grid)
    {
        Color ink = new Color(0.43f, 0.30f, 0.22f, 1f);

        GameObject barGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
        RectTransform bar = (RectTransform)barGo.transform;
        bar.SetParent(grid, false);
        bar.anchorMin = new Vector2(1f, 0f);
        bar.anchorMax = new Vector2(1f, 1f);
        bar.pivot = new Vector2(1f, 0.5f);
        bar.sizeDelta = new Vector2(4f, -16f);
        bar.anchoredPosition = new Vector2(-3f, 0f);
        barGo.GetComponent<Image>().color = new Color(ink.r, ink.g, ink.b, 0.12f);

        GameObject areaGo = new GameObject("Sliding Area", typeof(RectTransform));
        RectTransform area = (RectTransform)areaGo.transform;
        area.SetParent(bar, false);
        area.anchorMin = Vector2.zero;
        area.anchorMax = Vector2.one;
        area.offsetMin = area.offsetMax = Vector2.zero;

        GameObject handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        RectTransform handle = (RectTransform)handleGo.transform;
        handle.SetParent(area, false);
        handle.offsetMin = handle.offsetMax = Vector2.zero;
        Image handleImage = handleGo.GetComponent<Image>();
        handleImage.color = new Color(ink.r, ink.g, ink.b, 0.55f);

        Scrollbar scrollbar = barGo.GetComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;
        return scrollbar;
    }

    // 격자를 rows 줄로 맞춘다: 칸이 모자라면 첫 칸을 복제해 늘리고, 남는 칸은 숨긴다. 판 높이도 맞춘다
    private void EnsureRows(int rows)
    {
        if (slots.Count == 0)
            return;

        int needed = rows * Columns;

        while (slots.Count < needed)
        {
            InventorySlotUI clone = Instantiate(slots[0], slots[0].transform.parent);
            clone.name = "Slot_" + slots.Count.ToString("00");
            clone.Init(slots.Count);
            clone.OnClicked += OnInventorySlotClicked;
            slots.Add(clone);
        }

        for (int i = 0; i < slots.Count; i++)
        {
            bool active = i < needed;

            if (slots[i].gameObject.activeSelf != active)
                slots[i].gameObject.SetActive(active);

            if (gridContent == null)
                continue;

            RectTransform rt = (RectTransform)slots[i].transform;
            int col = i % Columns;
            int row = i / Columns;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(
                slotOrigin.x + col * slotStep.x,
                -(slotOrigin.y - GridInset + row * slotStep.y));
        }

        if (gridContent != null)
        {
            // 위 여백 + 줄들 + 아래 여백(위와 같게)
            float pad = slotOrigin.y - GridInset;
            gridContent.sizeDelta = new Vector2(0f, pad * 2f + (rows - 1) * slotStep.y + slotSize.y);
        }
    }

    // 보유 장비와 보유 장식을 하나의 목록으로 만든다.
    private List<Entry> BuildEntries(PlayerEquipment equipment)
    {
        List<Entry> result = new List<Entry>();

        if (equipment != null)
        {
            foreach (EquipmentItemData item in equipment.OwnedItems)
            {
                if (item != null)
                    result.Add(new Entry { equipment = item });
            }
        }

        DecorationInventory decorations = DecorationInventory.Instance;

        if (decorations != null)
        {
            foreach (BuildingData data in decorations.GetOwnedDecorations())
            {
                result.Add(new Entry
                {
                    decoration = data,
                    count = decorations.GetCount(data)
                });
            }
        }

        return result;
    }

    private static Entry FindEntry(List<Entry> entries, object key)
    {
        if (key == null)
            return null;

        foreach (Entry entry in entries)
        {
            if (entry.Key == key)
                return entry;
        }

        return null;
    }

    // 선택한 항목의 이름, 분류, 설명, 버튼 문구를 표시한다.
    private void ShowDetail(Entry entry, PlayerEquipment equipment)
    {
        if (entry == null)
        {
            detailIcon.enabled = false;
            detailName.text = "아이템을 선택하세요";
            detailTag.text = "";
            detailDescription.text = "인벤토리 칸을 누르면 정보를 볼 수 있어요.";
            primaryButton.interactable = false;
            primaryLabel.text = "장착하기";
            discardButton.interactable = false;
            return;
        }

        detailIcon.sprite = entry.Icon;
        detailIcon.enabled = entry.Icon != null;
        detailName.text = entry.Name;
        discardButton.interactable = true;

        if (entry.IsEquipment)
        {
            bool isEquipped = equipment != null && equipment.IsEquipped(entry.equipment);

            detailTag.text = "장비 · " + EquipmentSlotNames.ToKorean(entry.equipment.slot) +
                             (isEquipped ? " · 장착 중" : "");
            detailDescription.text = entry.equipment.description;
            primaryLabel.text = isEquipped ? "해제하기" : "장착하기";
            primaryButton.interactable = equipment != null;
        }
        else
        {
            detailTag.text = "장식 · " + entry.count + "개 보유";
            detailDescription.text =
                $"마을에 배치할 수 있는 장식이에요. (크기 {entry.decoration.width} × {entry.decoration.height})";
            primaryLabel.text = "배치하기";
            primaryButton.interactable = entry.count > 0;
        }
    }

    private Entry SelectedEntry()
    {
        foreach (Entry entry in visibleEntries)
        {
            if (entry.Key == selectedKey)
                return entry;
        }

        return null;
    }

    private void OnInventorySlotClicked(int index)
    {
        if (index < 0 || index >= visibleEntries.Count)
            return;

        selectedKey = visibleEntries[index].Key;
        Refresh();
    }

    private void OnEquipmentSlotClicked(EquipmentSlot slot)
    {
        PlayerEquipment equipment = PlayerEquipment.Instance;
        EquipmentItemData item = equipment != null ? equipment.GetEquipped(slot) : null;

        if (item == null)
            return;

        // 장착한 아이템이 현재 분류에서 안 보이면 전체 분류로 바꾼다.
        if (filterChips != null && filterChips.Selected == FilterDecoration)
            filterChips.Select(FilterAll, false);

        selectedKey = item;
        Refresh();
    }

    private void UnequipAll()
    {
        if (PlayerEquipment.Instance != null)
            PlayerEquipment.Instance.UnequipAll();
    }

    // 장비는 장착/해제하고, 장식은 배치 모드로 들어간다.
    private void OnPrimaryClicked()
    {
        Entry entry = SelectedEntry();

        if (entry == null)
            return;

        if (entry.IsEquipment)
        {
            PlayerEquipment equipment = PlayerEquipment.Instance;

            if (equipment == null)
                return;

            if (equipment.IsEquipped(entry.equipment))
                equipment.Unequip(entry.equipment.slot);
            else
                equipment.Equip(entry.equipment);

            return;
        }

        StartDecorationPlacement(entry.decoration);
    }

    // 장비는 보유 목록에서 지우고, 장식은 1개를 버린다.
    private void OnDiscardClicked()
    {
        Entry entry = SelectedEntry();

        if (entry == null)
            return;

        if (entry.IsEquipment)
        {
            // 대장간에서 만든 도구(삽)는 서버에 저장된 것이라 버리지 않는다
            if (ToolInventory.IsCraftedTool(entry.equipment))
            {
                Toast.Show("대장간에서 만든 도구는 버릴 수 없어요.");
                return;
            }

            if (PlayerEquipment.Instance != null)
                PlayerEquipment.Instance.RemoveItem(entry.equipment);
        }
        else if (DecorationInventory.Instance != null)
        {
            DecorationInventory.Instance.TryConsumeDecoration(entry.decoration);
        }
    }

    // 건물 카드와 같은 방식으로 장식 배치 모드를 연다.
    private void StartDecorationPlacement(BuildingData data)
    {
        if (decorationDatabase == null ||
            buildingPlacement == null ||
            buildingModeManager == null ||
            inventoryUI == null)
        {
            Debug.LogError("장식 배치에 필요한 참조가 없습니다.");
            return;
        }

        GameObject prefab = decorationDatabase.GetPrefab(data);

        if (prefab == null)
        {
            Debug.LogWarning("데코레이션 Prefab을 찾을 수 없습니다: " + data.buildingName);
            return;
        }

        buildingPlacement.SelectBuilding(prefab);
        buildingModeManager.EnterBuildMode();
        inventoryUI.CloseInventory();
    }
}
