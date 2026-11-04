using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BuildingSelectionButton : MonoBehaviour
{
    [Header("Building")]
    [SerializeField] private GameObject buildingPrefab;

    [Header("References")]
    [SerializeField] private BuildingPlacement buildingPlacement;
    [SerializeField] private BuildingListUI buildingListUI;

    [Header("UI")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text costText;


    private Button button;


    private void Start()
    {
        UpdateButtonUI();
    }

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

    private void Awake()
    {
        button = GetComponent<Button>();

        if (button != null)
        {
            button.onClick.AddListener(SelectBuilding);
        }
    }

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
