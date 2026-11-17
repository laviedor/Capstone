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

    private PlayerEquipment subscribedEquipment;
    private DecorationInventory subscribedDecorations;
    private bool initialized;

    // 버튼과 칸의 클릭 이벤트를 한 번만 연결한다.
    private void Initialize()
    {
        if (initialized)
            return;

        initialized = true;

        for (int i = 0; i < inventorySlots.Length; i++)
        {
            inventorySlots[i].Init(i);
            inventorySlots[i].OnClicked += OnInventorySlotClicked;
        }

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

        // 인벤토리 격자
        for (int i = 0; i < inventorySlots.Length; i++)
        {
            if (i >= visibleEntries.Count)
            {
                inventorySlots[i].ShowEmpty();
                continue;
            }

            Entry entry = visibleEntries[i];
            bool isEquipped =
                entry.IsEquipment && equipment != null && equipment.IsEquipped(entry.equipment);

            inventorySlots[i].Show(
                entry.Icon,
                entry.count,
                isEquipped,
                entry.Key == selectedKey
            );
        }

        if (countText != null)
            countText.text = allEntries.Count + " / " + inventorySlots.Length;

        ShowDetail(selected, equipment);
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
