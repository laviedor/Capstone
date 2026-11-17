using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;

// 건설 목록의 개별 건물/장식 카드에 이름, 아이콘, 비용 또는 보유량을 표시하고 선택을 처리한다.
public class BuildingCardUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image buildingImage;
    [SerializeField] private TMP_Text buildingNameText;
    [SerializeField] private TMP_Text buildingCostText;
    [SerializeField] private Button cardButton;

    // 카드가 선택할 건물 데이터와 생성할 프리팹, 건설 시스템 참조를 보관한다.
    private BuildingData buildingData;
    private GameObject buildingPrefab;
    private BuildingPlacement buildingPlacement;
    private InventoryUI inventoryUI;
    private BuildingModeManager buildingModeManager;

    // 장식 인벤토리 변경 이벤트를 구독하여 보유량 표시를 최신 상태로 유지한다.
    private void OnEnable()
    {
        if (DecorationInventory.Instance != null)
        {
            DecorationInventory.Instance.OnInventoryChanged
                += UpdateCostText;
        }

        UpdateCostText();
    }

    // UI 비활성화 시 이벤트 구독을 해제해 중복 호출을 방지한다.
    private void OnDisable()
    {
        if (DecorationInventory.Instance != null)
        {
            DecorationInventory.Instance.OnInventoryChanged
                -= UpdateCostText;
        }
    }

    // 카드에 건물 정보와 관련 시스템 참조를 연결하고 버튼 클릭을 등록한다.
    public void Setup(
        BuildingData data,
        GameObject prefab,
        BuildingPlacement placement,
        InventoryUI inventory,
        BuildingModeManager modeManager)
    {
        if (data == null || prefab == null)
        {
            Debug.LogWarning(
                "건물 데이터 또는 Prefab이 없습니다."
            );
            return;
        }

        buildingData = data;
        buildingPrefab = prefab;
        buildingPlacement = placement;
        inventoryUI = inventory;
        buildingModeManager = modeManager;

        buildingNameText.text = data.buildingName;

        if (data.buildingIcon != null)
        {
            buildingImage.sprite = data.buildingIcon;
            buildingImage.enabled = true;
            buildingImage.preserveAspect = true;
        }
        else
        {
            buildingImage.enabled = false;
        }

        UpdateCostText();

        cardButton.onClick.RemoveListener(OnCardClicked);
        cardButton.onClick.AddListener(OnCardClicked);
    }

    // 선택 가능 여부를 확인한 후 건설 모드를 열고 인벤토리를 닫는다.
    private void OnCardClicked()
    {
        if (buildingPlacement == null ||
            buildingModeManager == null ||
            inventoryUI == null ||
            buildingData == null)
        {
            Debug.LogError(
                "건물 선택에 필요한 참조가 없습니다."
            );
            return;
        }

        // 데코레이션은 보유 수량 확인
        if (buildingData.buildingType == BuildingType.Decoration)
        {
            if (DecorationInventory.Instance == null ||
                DecorationInventory.Instance.GetCount(buildingData) <= 0)
            {
                Debug.LogWarning(
                    "보유한 데코레이션이 없습니다."
                );
                return;
            }
        }

        buildingPlacement.SelectBuilding(buildingPrefab);

        buildingModeManager.EnterBuildMode();

        inventoryUI.CloseInventory();

        Debug.Log(
            "건물 카드 선택: " +
            buildingData.buildingName
        );
    }

    // 장식이면 보유 수량을, 일반 건물이면 건설 비용을 표시한다.
    private void UpdateCostText()
    {
        if (buildingData == null || buildingCostText == null)
            return;

        // 데코레이션은 보유 수량 표시
        if (buildingData.buildingType == BuildingType.Decoration)
        {
            int count = 0;

            if (DecorationInventory.Instance != null)
            {
                count = DecorationInventory.Instance
                    .GetCount(buildingData);
            }

            buildingCostText.text = "보유: " + count + "개";

            if (cardButton != null)
                cardButton.interactable = count > 0;

            return;
        }

        // 일반 건물은 기존 건설 비용 표시
        if (cardButton != null)
            cardButton.interactable = true;

        if (buildingData.constructionCosts == null ||
            buildingData.constructionCosts.Length == 0)
        {
            buildingCostText.text = "무료";
            return;
        }

        StringBuilder builder = new StringBuilder();

        foreach (ResourceAmount cost
                 in buildingData.constructionCosts)
        {
            if (cost == null)
                continue;

            if (builder.Length > 0)
                builder.Append("\n");

            builder.Append(
                GetResourceName(cost.resourceType)
            );

            builder.Append(": ");
            builder.Append(cost.amount);
        }

        buildingCostText.text =
            builder.Length > 0
            ? builder.ToString()
            : "무료";
    }

    // 자원 종류 열거형을 화면에 표시할 한국어 이름으로 변환한다.
    private string GetResourceName(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Wood:
                return "나무";

            case ResourceType.Stone:
                return "돌";

            case ResourceType.Gold:
                return "골드";

            default:
                return type.ToString();
        }
    }
}
