
using UnityEngine;

// 설치된 건물들을 정렬하여 관리 카드로 나열하고 전체 생산량을 갱신한다.
public class InstalledBuildingListUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform content;
    [SerializeField] private InstalledBuildingCardUI buildingCardPrefab;
    [SerializeField] private BuildingDetailUI buildingDetailUI;

    [SerializeField] private TotalProductionUI totalProductionUI;

    [SerializeField] private GameObject totalProductionPanel;

    // 관리 화면이 열리면 전체 생산 패널을 표시하고 목록을 새로 만든다.
    private void OnEnable()
    {
        if (totalProductionPanel != null)
            totalProductionPanel.SetActive(true);

        RefreshList();
    }

    // 관리 화면이 닫히면 전체 생산 패널을 숨긴다.
    private void OnDisable()
    {
        if (totalProductionPanel != null)
            totalProductionPanel.SetActive(false);
    }

    // 기존 카드를 지우고 설치된 건물을 ID와 번호로 정렬해 목록을 재생성한다.
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

        // 건물 ID를 우선 비교하고 같은 ID라면 고유 번호로 순서를 결정한다.
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
