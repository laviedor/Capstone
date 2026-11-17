using System.Collections.Generic;
using UnityEngine;

// 보유 중인 데코레이션을 건설 목록 카드로 표시하는 UI 관리자.
public class DecorationBuildListUI : MonoBehaviour
{
    [Header("Decoration Database")]
    [SerializeField]
    // 데코레이션 데이터에 대응하는 설치 프리팹을 찾는 데이터베이스.
    private DecorationDatabase decorationDatabase;

    [Header("UI")]
    [SerializeField]
    // 생성된 카드 UI를 자식으로 배치할 콘텐츠 영역.
    private Transform contentParent;

    [SerializeField]
    // 데코레이션마다 생성할 카드 UI 프리팹.
    private BuildingCardUI cardPrefab;

    [Header("References")]
    [SerializeField]
    // 카드 선택 시 설치할 프리팹을 전달받는 건물 배치 시스템.
    private BuildingPlacement buildingPlacement;

    [SerializeField]
    // 카드 UI 설정에 전달하는 인벤토리 UI 참조.
    private InventoryUI inventoryUI;

    [SerializeField]
    // 카드 UI 설정에 전달하는 건설 모드 관리자.
    private BuildingModeManager buildingModeManager;

    // 같은 데코레이션 카드가 중복 생성되지 않도록 생성된 카드를 보관한다.
    private Dictionary<BuildingData, BuildingCardUI> cards =
        new Dictionary<BuildingData, BuildingCardUI>();

    // 이벤트 중복 구독을 방지하기 위해 현재 구독한 인벤토리를 기록한다.
    private DecorationInventory subscribedInventory;

    // UI가 활성화되면 이벤트를 구독하고 보유 목록을 다시 표시한다.
    private void OnEnable()
    {
        SubscribeInventory();
        RefreshList();
    }

    // 초기 실행 시점에도 인벤토리 구독과 목록 갱신을 시도한다.
    private void Start()
    {
        SubscribeInventory();
        RefreshList();
    }

    // UI가 비활성화되면 이벤트 구독을 해제해 불필요한 호출을 막는다.
    private void OnDisable()
    {
        // 이전 인벤토리 구독이 남아 있다면 먼저 해제한다.
        if (subscribedInventory != null)
        {
            subscribedInventory.OnInventoryChanged -= RefreshList;
            subscribedInventory = null;
        }
    }

    // 현재 인벤토리의 변경 이벤트에 RefreshList를 연결한다.
    private void SubscribeInventory()
    {
        DecorationInventory inventory = DecorationInventory.Instance;

        // 인벤토리가 없거나 이미 같은 인벤토리를 구독 중이면 종료한다.
        if (inventory == null ||
            subscribedInventory == inventory)
            return;

        // 이전 인벤토리 구독이 남아 있다면 먼저 해제한다.
        if (subscribedInventory != null)
            subscribedInventory.OnInventoryChanged -= RefreshList;

        subscribedInventory = inventory;
        subscribedInventory.OnInventoryChanged += RefreshList;
    }

    // 현재 보유한 데코레이션에 맞춰 카드 UI를 추가하거나 제거한다.
    public void RefreshList()
    {
        if (DecorationInventory.Instance == null ||
            decorationDatabase == null ||
            contentParent == null ||
            cardPrefab == null)
            return;

        // 인벤토리에서 보유 수량이 1개 이상인 데코레이션을 가져온다.
        List<BuildingData> ownedDecorations =
            DecorationInventory.Instance.GetOwnedDecorations();

        // 카드 제거 여부를 빠르게 판단하기 위해 보유 목록을 집합으로 변환한다.
        HashSet<BuildingData> ownedSet =
            new HashSet<BuildingData>(ownedDecorations);

        // 보유 수량이 0개가 된 카드 제거
        // 순회 중 딕셔너리를 수정하지 않도록 제거할 키를 따로 모은다.
        List<BuildingData> toRemove = new List<BuildingData>();

        // 기존 카드 중 더 이상 보유하지 않는 종류를 찾아 제거한다.
        foreach (var pair in cards)
        {
            if (!ownedSet.Contains(pair.Key))
            {
                Destroy(pair.Value.gameObject);
                toRemove.Add(pair.Key);
            }
        }

        // 삭제 예약된 카드 정보를 딕셔너리에서도 제거한다.
        foreach (BuildingData data in toRemove)
        {
            cards.Remove(data);
        }

        // 보유 중인 데코레이션 카드 생성
        // 보유 중이지만 아직 카드가 없는 종류만 새 카드를 만든다.
        foreach (BuildingData data in ownedDecorations)
        {
            // 이미 화면에 등록된 카드는 중복 생성하지 않는다.
            if (cards.ContainsKey(data))
                continue;

            // 데이터베이스에서 선택한 데코레이션의 설치 프리팹을 찾는다.
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

            // 카드 프리팹을 콘텐츠 영역 아래에 생성한다.
            BuildingCardUI newCard =
                Instantiate(cardPrefab, contentParent);

            // 카드에 건물 데이터, 프리팹, UI 및 건설 시스템 참조를 전달한다.
            newCard.Setup(
                data,
                prefab,
                buildingPlacement,
                inventoryUI,
                buildingModeManager
            );

            // 새 카드를 딕셔너리에 등록해 이후 중복 생성을 막는다.
            cards.Add(data, newCard);
        }
    }
}
