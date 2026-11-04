
using System;
using UnityEngine;

[Serializable]
public class ConstructionBuildingEntry
{
    public BuildingData buildingData;
    public GameObject buildingPrefab;
}

public class ConstructionListUI : MonoBehaviour
{
    [Header("Building List")]
    [SerializeField]
    private ConstructionBuildingEntry[] availableBuildings;

    [Header("UI References")]
    [SerializeField] private Transform content;
    [SerializeField] private BuildingCardUI buildingCardPrefab;

    [Header("System References")]
    [SerializeField] private BuildingPlacement buildingPlacement;
    [SerializeField] private InventoryUI inventoryUI;

    [SerializeField] private BuildingModeManager buildingModeManager;

    private void Start()
    {
        CreateBuildingList();
    }

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

        foreach (Transform child in content)
        {
            Destroy(child.gameObject);
        }

        foreach (ConstructionBuildingEntry entry in availableBuildings)
        {
            if (entry == null ||
                entry.buildingData == null ||
                entry.buildingPrefab == null)
            {
                continue;
            }

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
        }
    }
}
