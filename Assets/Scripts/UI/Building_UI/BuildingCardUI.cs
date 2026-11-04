
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;

public class BuildingCardUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image buildingImage;
    [SerializeField] private TMP_Text buildingNameText;
    [SerializeField] private TMP_Text buildingCostText;
    [SerializeField] private Button cardButton;

    private BuildingData buildingData;
    private GameObject buildingPrefab;
    private BuildingPlacement buildingPlacement;
    private InventoryUI inventoryUI;
    private BuildingModeManager buildingModeManager;

    public void Setup(
        BuildingData data,
        GameObject prefab,
        BuildingPlacement placement,
        InventoryUI inventory,
        BuildingModeManager modeManager)
    {
        if (data == null || prefab == null)
        {
            Debug.LogWarning("건물 데이터 또는 Prefab이 없습니다.");
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

    private void OnCardClicked()
    {
        if (buildingPlacement == null ||
            buildingModeManager == null ||
            inventoryUI == null)
        {
            Debug.LogError("건물 선택에 필요한 참조가 없습니다.");
            return;
        }

        // 1. 선택한 건물로 변경
        buildingPlacement.SelectBuilding(buildingPrefab);

        // 2. 건설 모드 활성화
        buildingModeManager.EnterBuildMode();

        // 3. 인벤토리 닫기
        inventoryUI.CloseInventory();

        Debug.Log("건물 카드 선택: " + buildingData.buildingName);
    }

    private void UpdateCostText()
    {
        if (buildingData.constructionCosts == null ||
            buildingData.constructionCosts.Length == 0)
        {
            buildingCostText.text = "무료";
            return;
        }

        StringBuilder builder = new StringBuilder();

        foreach (ResourceAmount cost in buildingData.constructionCosts)
        {
            if (cost == null)
                continue;

            if (builder.Length > 0)
                builder.Append("\n");

            builder.Append(GetResourceName(cost.resourceType));
            builder.Append(": ");
            builder.Append(cost.amount);
        }

        buildingCostText.text =
            builder.Length > 0 ? builder.ToString() : "무료";
    }

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
