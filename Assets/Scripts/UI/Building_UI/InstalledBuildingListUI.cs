
using UnityEngine;

public class InstalledBuildingListUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform content;
    [SerializeField] private InstalledBuildingCardUI buildingCardPrefab;
    [SerializeField] private BuildingDetailUI buildingDetailUI;

    [SerializeField] private TotalProductionUI totalProductionUI;

    [SerializeField] private GameObject totalProductionPanel;

    private void OnEnable()
    {
        if (totalProductionPanel != null)
            totalProductionPanel.SetActive(true);

        RefreshList();
    }

    private void OnDisable()
    {
        if (totalProductionPanel != null)
            totalProductionPanel.SetActive(false);
    }

    public void RefreshList()
    {
        if (content == null ||
            buildingCardPrefab == null ||
            buildingDetailUI == null)
        {
            Debug.LogWarning("건물 관리 UI 연결이 누락되었습니다.");
            return;
        }

        // 기존 상세 정보 초기화
        buildingDetailUI.ClearDetail();

        // 기존 카드 제거
        foreach (Transform child in content)
        {
            Destroy(child.gameObject);
        }

        // 설치된 건물 검색
        Building[] buildings = FindObjectsByType<Building>(
            FindObjectsSortMode.None
        );

        System.Array.Sort(buildings, (a, b) =>
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            // 건물 종류별 정렬
            int typeCompare = string.Compare(
                a.BuildingID,
                b.BuildingID,
                System.StringComparison.Ordinal
            );

            if (typeCompare != 0)
                return typeCompare;

            // 같은 종류라면 고유 번호순 정렬
            return a.BuildingNumber.CompareTo(b.BuildingNumber);
        });

        foreach (Building building in buildings)
        {
            if (building == null ||
                !building.IsPlaced ||
                building.Data == null)
            {
                continue;
            }

            // 설치 시 부여된 고유 번호 사용
            int buildingNumber = building.BuildingNumber;

            InstalledBuildingCardUI card =
                Instantiate(buildingCardPrefab, content);

            card.Setup(
                building,
                buildingDetailUI,
                buildingNumber
            );
        }

        Debug.Log("설치된 건물 목록 갱신 완료");
        if (totalProductionUI != null)
        {
            totalProductionUI.RefreshProduction();
        }
    }
}
