using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 스크립트 설명: 건물 목록의 개별 버튼 UI와 선택 이벤트를 관리한다.
public class BuildingSelectionButton : MonoBehaviour
{
    [Header("Building")]
    // 변수 설명: 선택 버튼이 가리키는 건물 프리팹
    [SerializeField] private GameObject buildingPrefab;

    [Header("References")]
    // 변수 설명: 설치된 건물의 격자 점유 정보를 관리하는 컴포넌트
    [SerializeField] private BuildingPlacement buildingPlacement;
    // 변수 설명: 건물 선택 후 닫을 건물 목록 UI
    [SerializeField] private BuildingListUI buildingListUI;

    [Header("UI")]
    // 변수 설명: 건물 이름을 표시하는 텍스트
    [SerializeField] private TMP_Text nameText;
    // 변수 설명: 건설 비용을 표시하는 텍스트
    [SerializeField] private TMP_Text costText;


    // 변수 설명: 클릭 이벤트를 연결할 Unity UI Button
    private Button button;


    // 함수 설명: 버튼에 표시할 건물 이름과 건설 비용을 갱신한다.
    private void Start()
    {
        UpdateButtonUI();
    }

    // 함수 설명: 연결된 건물 데이터로 버튼의 이름 및 비용 텍스트를 구성한다.
    private void UpdateButtonUI()
    {
        if (buildingPrefab == null)
            return;

        Building building =
            buildingPrefab.GetComponent<Building>();

        if (building == null ||
            building.Data == null)
        {
            return;
        }

        BuildingData data = building.Data;

        // 건물 이름
        if (nameText != null)
        {
            nameText.text = data.buildingName;
        }

        // 건설 비용
        if (costText != null)
        {
            string costString = "";

            foreach (ResourceAmount cost
                     in data.constructionCosts)
            {
                costString +=
                    cost.resourceType +
                    " " +
                    cost.amount +
                    "\n";
            }

            costText.text =
                costString.TrimEnd();
        }
    }

    // 함수 설명: Button 컴포넌트를 가져와 클릭 이벤트에 건물 선택 함수를 등록한다.
    private void Awake()
    {
        button = GetComponent<Button>();

        if (button != null)
        {
            button.onClick.AddListener(SelectBuilding);
        }
    }

    // 함수 설명: 선택한 건물 프리팹을 배치 시스템에 전달하고 건물 목록을 닫는다.
    private void SelectBuilding()
    {
        if (buildingPlacement == null)
        {
            Debug.LogError(
                "BuildingPlacement가 연결되어 있지 않습니다."
            );

            return;
        }

        if (buildingPrefab == null)
        {
            Debug.LogError(
                "Building Prefab이 연결되어 있지 않습니다."
            );

            return;
        }

        // 선택한 Prefab을 BuildingPlacement에 전달
        buildingPlacement.SelectBuilding(
            buildingPrefab
        );

        // 선택 후 건물 목록 닫기
        if (buildingListUI != null)
        {
            buildingListUI.Close();
        }
    }
}
