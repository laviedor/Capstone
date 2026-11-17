using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;

// 설치된 건물 한 개의 이름과 생산 정보를 표시하고 상세 정보 선택을 처리한다.
public class InstalledBuildingCardUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text buildingNameText;
    [SerializeField] private TMP_Text buildingProductionText;
    [SerializeField] private Button cardButton;

    private Building building;
    private BuildingDetailUI detailUI;

    // 건물 참조와 번호를 설정하고 카드 텍스트 및 클릭 이벤트를 초기화한다.
    public void Setup(
        Building targetBuilding,
        BuildingDetailUI targetDetailUI,
        int buildingNumber)
    {
        building = targetBuilding;
        detailUI = targetDetailUI;

        if (building == null || building.Data == null)
            return;

        // 건물 이름 및 고유 번호
        if (buildingNameText != null)
        {
            buildingNameText.text =
                building.Data.buildingName +
                " #" + buildingNumber;
        }

        // 생산 정보 표시
        UpdateProductionText();

        // 카드 클릭 이벤트
        if (cardButton != null)
        {
            cardButton.onClick.RemoveListener(OnCardClicked);
            cardButton.onClick.AddListener(OnCardClicked);
        }
    }

    // 건물의 자원별 생산량과 생산 주기를 카드에 표시한다.
    private void UpdateProductionText()
    {
        if (buildingProductionText == null ||
            building == null ||
            building.Data == null)
            return;

        BuildingData data = building.Data;

        if (data.productions == null ||
            data.productions.Length == 0)
        {
            buildingProductionText.text = "생산 없음";
            return;
        }

        StringBuilder sb = new StringBuilder();

        foreach (ResourceProduction production
                 in data.productions)
        {
            if (production.amount <= 0)
                continue;

            if (sb.Length > 0)
                sb.AppendLine();

            sb.Append(
                production.resourceType +
                " +" + production.amount +
                " / " + data.productionInterval + "초"
            );
        }

        buildingProductionText.text =
            sb.Length > 0 ? sb.ToString() : "생산 없음";
    }

    // 선택한 건물의 상세 정보를 관리 패널에 전달한다.
    private void OnCardClicked()
    {
        if (building == null || detailUI == null)
            return;

        detailUI.ShowBuilding(building);

        Debug.Log(
            "관리 건물 선택: " +
            building.Data.buildingName
        );
    }

    // 카드 파괴 시 버튼 리스너를 해제한다.
    private void OnDestroy()
    {
        if (cardButton != null)
        {
            cardButton.onClick.RemoveListener(OnCardClicked);
        }
    }
}