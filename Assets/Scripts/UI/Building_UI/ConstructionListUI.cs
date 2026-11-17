using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
// 건설 목록에 등록할 건물 데이터와 프리팹을 한 쌍으로 저장한다.
public class ConstructionBuildingEntry
{
    public BuildingData buildingData;
    public GameObject buildingPrefab;
}

// 일반 건물 카드와 보유 중인 장식 카드를 건설 목록에 생성하고 갱신한다.
public class ConstructionListUI : MonoBehaviour
{
    [Header("Building List")]
    [SerializeField]
    private ConstructionBuildingEntry[] availableBuildings;

    [Header("Decoration Database")]
    [SerializeField]
    private DecorationDatabase decorationDatabase;

    [Header("UI References")]
    [SerializeField] private Transform content;
    [SerializeField] private BuildingCardUI buildingCardPrefab;

    // 디자인 개편으로 추가된 상세 패널과 분류 칩 (없어도 동작한다)
    [Header("Cozy Style (optional)")]
    [SerializeField] private ConstructionDetailUI detailUI;
    [SerializeField] private ChipGroupUI categoryChips;

    [Header("System References")]
    [SerializeField] private BuildingPlacement buildingPlacement;
    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField] private BuildingModeManager buildingModeManager;

    private readonly Dictionary<BuildingData, BuildingCardUI>
        decorationCards =
        new Dictionary<BuildingData, BuildingCardUI>();

    // 이벤트를 구독 중인 인벤토리를 기록해 중복 구독을 피한다.
    private DecorationInventory subscribedInventory;

    // 일반 건물 카드 목록 (분류 필터용)
    private readonly List<BuildingCardUI> buildingCards =
        new List<BuildingCardUI>();

    private bool chipsSubscribed;

    // 활성화될 때 장식 인벤토리 이벤트를 구독하고 장식 카드를 갱신한다.
    private void OnEnable()
    {
        if (categoryChips != null && !chipsSubscribed)
        {
            categoryChips.OnSelected += _ => ApplyFilter();
            chipsSubscribed = true;
        }

        SubscribeInventory();
        RefreshDecorationCards();
    }

    // 초기 일반 건물 목록을 만들고 장식 목록을 동기화한다.
    private void Start()
    {
        CreateBuildingList();
        SubscribeInventory();
        RefreshDecorationCards();
    }

    // 비활성화 시 인벤토리 변경 이벤트 연결을 해제한다.
    private void OnDisable()
    {
        if (subscribedInventory != null)
        {
            subscribedInventory.OnInventoryChanged -= RefreshDecorationCards;
            subscribedInventory = null;
        }
    }

    // 현재 장식 인벤토리의 변경 이벤트를 중복 없이 구독한다.
    private void SubscribeInventory()
    {
        DecorationInventory inventory = DecorationInventory.Instance;

        if (inventory == null || subscribedInventory == inventory)
            return;

        if (subscribedInventory != null)
            subscribedInventory.OnInventoryChanged -= RefreshDecorationCards;

        subscribedInventory = inventory;
        subscribedInventory.OnInventoryChanged += RefreshDecorationCards;
    }

    // 기존 카드를 비운 후 등록된 일반 건물 데이터로 새 카드를 생성한다.
    private void CreateBuildingList()
    {
        if (content == null ||
            buildingCardPrefab == null ||
            buildingPlacement == null ||
            inventoryUI == null ||
            buildingModeManager == null)
        {
            Debug.LogError("건물 목록 UI 연결이 누락되었습니다.");
            return;
        }

        // 기존 카드 초기화
        foreach (Transform child in content)
        {
            Destroy(child.gameObject);
        }

        decorationCards.Clear();
        buildingCards.Clear();

        if (availableBuildings == null)
            return;

        // 기존 건물 카드 생성
        foreach (ConstructionBuildingEntry entry in availableBuildings)
        {
            if (entry == null ||
                entry.buildingData == null ||
                entry.buildingPrefab == null)
                continue;

            Building building =
                entry.buildingPrefab.GetComponent<Building>();

            if (building == null ||
                building.Data != entry.buildingData)
            {
                Debug.LogWarning(
                    "건물 데이터와 Prefab이 일치하지 않습니다: " +
                    entry.buildingData.buildingName
                );
                continue;
            }

            BuildingCardUI card =
                Instantiate(buildingCardPrefab, content);

            card.Setup(
                entry.buildingData,
                entry.buildingPrefab,
                buildingPlacement,
                inventoryUI,
                buildingModeManager
            );

            card.SetDetailPanel(detailUI);
            buildingCards.Add(card);
        }

        ApplyFilter();
    }

    // 현재 보유 장식과 카드 목록을 비교해 사라진 카드를 제거하고 새 카드를 추가한다.
    public void RefreshDecorationCards()
    {
        //
        Debug.Log("데코레이션 목록 갱신 호출됨");

        Debug.Log("Inventory: " + (DecorationInventory.Instance != null));
        Debug.Log("Database: " + (decorationDatabase != null));
        Debug.Log("Content: " + (content != null));
        Debug.Log("Card Prefab: " + (buildingCardPrefab != null));
        //

        if (DecorationInventory.Instance == null ||
            decorationDatabase == null ||
            content == null ||
            buildingCardPrefab == null)
            return;

        List<BuildingData> ownedDecorations =
            DecorationInventory.Instance.GetOwnedDecorations();

        //
        Debug.Log("보유 데코레이션 종류 수: " + ownedDecorations.Count);
        //

        HashSet<BuildingData> ownedSet =
            new HashSet<BuildingData>(ownedDecorations);

        // 보유 수량 0개인 카드 제거
        List<BuildingData> removeList =
            new List<BuildingData>();

        foreach (var pair in decorationCards)
        {
            if (!ownedSet.Contains(pair.Key))
            {
                Destroy(pair.Value.gameObject);
                removeList.Add(pair.Key);
            }
        }

        foreach (BuildingData data in removeList)
        {
            decorationCards.Remove(data);
        }

        // 새롭게 획득한 데코레이션 카드 생성
        foreach (BuildingData data in ownedDecorations)
        {
            if (decorationCards.ContainsKey(data))
                continue;

            GameObject prefab =
                decorationDatabase.GetPrefab(data);

            if (prefab == null)
            {
                Debug.LogWarning(
                    "데코레이션 Prefab을 찾을 수 없습니다: " +
                    data.buildingName
                );
                continue;
            }

            Building building = prefab.GetComponent<Building>();

            if (building == null || building.Data != data)
            {
                Debug.LogWarning(
                    "데코레이션 데이터와 Prefab이 일치하지 않습니다: " +
                    data.buildingName
                );
                continue;
            }

            BuildingCardUI card =
                Instantiate(buildingCardPrefab, content);

            card.Setup(
                data,
                prefab,
                buildingPlacement,
                inventoryUI,
                buildingModeManager
            );

            card.SetDetailPanel(detailUI);
            decorationCards.Add(data, card);
        }

        ApplyFilter();
    }

    // 분류 칩에 맞는 카드만 보여 주고, 선택이 없으면 첫 카드를 선택한다.
    // 0: 전체, 1: 생산, 2: 특수, 3: 장식
    private void ApplyFilter()
    {
        int filter = categoryChips != null ? categoryChips.Selected : 0;

        foreach (BuildingCardUI card in buildingCards)
            SetCardVisible(card, filter);

        foreach (BuildingCardUI card in decorationCards.Values)
            SetCardVisible(card, filter);

        if (detailUI == null)
            return;

        detailUI.ClearIfHidden();

        if (detailUI.SelectedCard != null)
            return;

        foreach (Transform child in content)
        {
            BuildingCardUI card = child.GetComponent<BuildingCardUI>();

            if (card != null && card.gameObject.activeSelf && card.Data != null)
            {
                detailUI.Select(card);
                break;
            }
        }
    }

    private static void SetCardVisible(BuildingCardUI card, int filter)
    {
        if (card == null || card.Data == null)
            return;

        bool visible;

        switch (filter)
        {
            case 1: visible = card.Data.buildingType == BuildingType.Production; break;
            case 2: visible = card.Data.buildingType == BuildingType.Special; break;
            case 3: visible = card.Data.buildingType == BuildingType.Decoration; break;
            default: visible = true; break;
        }

        card.gameObject.SetActive(visible);
    }
}
