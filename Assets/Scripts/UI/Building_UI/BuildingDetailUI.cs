
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;

public class BuildingDetailUI : MonoBehaviour
{
    [Header("Detail Texts")]
    [SerializeField] private TMP_Text buildingDetailName;
    [SerializeField] private TMP_Text buildingDetailProduction;
    [SerializeField] private TMP_Text buildingDetailPosition;

    [Header("Buttons")]
    [SerializeField] private Button viewBuildingButton;
    [SerializeField] private Button demolishBuildingButton;

    [Header("System References")]
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private InventoryUI inventoryUI;

    [SerializeField] private PlayerPathMovement playerMovement;

    [SerializeField] private MousePathfinding mousePathfinding;

    [SerializeField] private BuildingDemolition buildingDemolition;

    private Building selectedBuilding;

    private void Awake()
    {
        if (viewBuildingButton != null)
        {
            viewBuildingButton.onClick.AddListener(ViewSelectedBuilding);
        }

        if (demolishBuildingButton != null)
        {
            demolishBuildingButton.onClick.AddListener(
                DemolishSelectedBuilding
            );
        }

        ClearDetail();
    }

    public void ShowBuilding(Building building)
    {
        if (building == null || building.Data == null)
        {
            ClearDetail();
            return;
        }

        selectedBuilding = building;

        BuildingData data = building.Data;

        buildingDetailName.text = "이름: " + data.buildingName;

        Vector3Int cell = building.OriginCell;

        buildingDetailPosition.text =
            $"위치: ({cell.x}, {cell.y})";

        if (data.productions == null ||
            data.productions.Length == 0)
        {
            buildingDetailProduction.text = "생산: 없음";
        }
        else
        {
            StringBuilder builder = new StringBuilder();

            foreach (ResourceProduction production in data.productions)
            {
                if (production == null)
                    continue;

                if (builder.Length > 0)
                    builder.Append(", ");

                builder.Append(production.resourceType);
                builder.Append(" ");
                builder.Append(production.amount);
            }

            buildingDetailProduction.text =
                builder.Length == 0
                ? "생산: 없음"
                : $"생산: {builder} / {data.productionInterval}초";
        }

        if (viewBuildingButton != null)
            viewBuildingButton.interactable = true;
        if (demolishBuildingButton != null)
            demolishBuildingButton.interactable = true;
    }

    public void ClearDetail()
    {
        selectedBuilding = null;

        buildingDetailName.text = "건물을 선택하세요";
        buildingDetailProduction.text = "";
        buildingDetailPosition.text = "";

        if (viewBuildingButton != null)
            viewBuildingButton.interactable = false;
        if (demolishBuildingButton != null)
            demolishBuildingButton.interactable = false;
    }

    private void ViewSelectedBuilding()
    {
        if (selectedBuilding == null ||
            !selectedBuilding.IsPlaced)
        {
            Debug.LogWarning("선택된 건물이 없습니다.");
            return;
        }

        if (cameraFollow == null ||
            inventoryUI == null ||
            playerMovement == null ||
            mousePathfinding == null)
        {
            Debug.LogError("필요한 시스템 참조가 없습니다.");
            return;
        }

        // 1. 플레이어 이동 중지
        playerMovement.StopMovement();

        // 2. 기존 경로 선과 목적지 마커 제거
        mousePathfinding.ClearMovementVisuals();

        // 3. 카메라를 선택한 건물로 이동
        cameraFollow.ViewBuilding(selectedBuilding);

        // 4. 인벤토리 닫기
        inventoryUI.CloseInventory();

        Debug.Log("건물 위치 보기 및 이동 상태 초기화");
    }
    private void DemolishSelectedBuilding()
    {
        if (selectedBuilding == null ||
            !selectedBuilding.IsPlaced)
        {
            Debug.LogWarning("선택된 건물이 없습니다.");
            return;
        }

        if (buildingDemolition == null ||
            inventoryUI == null)
        {
            Debug.LogError("철거 시스템 참조가 없습니다.");
            return;
        }

        Building buildingToDemolish = selectedBuilding;

        // 인벤토리를 먼저 닫기
        inventoryUI.CloseInventory();

        // 기존 철거 확인창 열기
        buildingDemolition.RequestDemolition(
            buildingToDemolish
        );
    }


    private void OnDestroy()
    {
        if (viewBuildingButton != null)
            viewBuildingButton.onClick.RemoveListener(ViewSelectedBuilding);

        if (demolishBuildingButton != null)
        {
            demolishBuildingButton.onClick.RemoveListener(
                DemolishSelectedBuilding
            );
        }
    }
}
