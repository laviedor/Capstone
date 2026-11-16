using UnityEngine;
using UnityEngine.InputSystem;

// 스크립트 설명: 건물 선택과 철거, 자원 환급 및 데코레이션 반환을 처리한다.
public class BuildingDemolition : MonoBehaviour
{
    [Header("Building Mode")]
    [SerializeField]
    // 변수 설명: 현재 건설 모드 및 선택 도구를 관리하는 컴포넌트
    private BuildingModeManager buildingModeManager;

    [Header("Confirm UI")]
    [SerializeField]
    // 변수 설명: 철거 여부를 확인받는 UI 참조
    private DemolitionConfirmUI confirmUI;

    [Header("Building Placement")]
    [SerializeField]
    // 변수 설명: 설치된 건물의 격자 점유 정보를 관리하는 컴포넌트
    private BuildingPlacement buildingPlacement;

    [Header("Player Resources")]
    [SerializeField]
    // 변수 설명: 건설 비용을 검사하고 자원을 차감하거나 환급할 대상
    private PlayerResources playerResources;

    // 현재 철거 대상으로 선택된 건물
    // 변수 설명: 현재 선택하거나 철거 대상으로 지정한 건물
    private Building selectedBuilding;

    // 선택 전 원래 색상
    // 변수 설명: 철거 대상 선택 전 스프라이트 색상
    private Color originalColor = Color.white;

    public Building SelectedBuilding
    {
        get { return selectedBuilding; }
    }

    // 함수 설명: 철거 모드에서 좌클릭 입력을 받아 철거할 건물을 선택한다.
    private void Update()
    {
        if (buildingModeManager == null)
            return;

        // 철거 확인창이 열려 있으면 입력 차단
        if (confirmUI != null && confirmUI.IsOpen)
            return;

        if (!buildingModeManager.IsBuildMode)
        {
            ClearSelection();
            return;
        }

        if (buildingModeManager.CurrentTool != BuildingTool.Demolish)
        {
            ClearSelection();
            return;
        }

        if (Mouse.current == null)
            return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            TrySelectBuilding();
        }
    }

    // 함수 설명: 마우스 위치의 2D 충돌체에서 Building 컴포넌트를 탐색한다.
    void TrySelectBuilding()
    {
        Vector2 mouseScreenPosition =
            Mouse.current.position.ReadValue();

        Vector3 mouseWorldPosition =
            Camera.main.ScreenToWorldPoint(mouseScreenPosition);

        Vector2 mousePosition2D =
            new Vector2(
                mouseWorldPosition.x,
                mouseWorldPosition.y
            );

        Collider2D hit =
            Physics2D.OverlapPoint(mousePosition2D);

        if (hit == null)
        {
            ClearSelection();
            return;
        }

        Building building =
            hit.GetComponentInParent<Building>();

        if (building == null)
        {
            ClearSelection();
            return;
        }

        SelectBuilding(building);
    }

    // 함수 설명: 철거 대상으로 지정하고 붉게 표시한 뒤 확인창을 연다.
    void SelectBuilding(Building building)
    {
        ClearSelection();

        selectedBuilding = building;

        SpriteRenderer renderer =
            selectedBuilding.GetComponent<SpriteRenderer>();

        if (renderer != null)
        {
            originalColor = renderer.color;

            renderer.color =
                new Color(1f, 0.3f, 0.3f, 1f);
        }

        Debug.Log(
            "철거 대상 선택 : " +
            selectedBuilding.Data.buildingName
        );

        if (confirmUI != null)
        {
            confirmUI.Open(selectedBuilding);
        }
    }

    // 함수 설명: 외부에서 지정한 건물에 대한 철거 확인을 요청한다.
    public void RequestDemolition(Building building)
    {
        if (building == null ||
            !building.IsPlaced ||
            building.Data == null)
        {
            Debug.LogWarning("철거할 수 없는 건물입니다.");
            return;
        }

        if (confirmUI == null)
        {
            Debug.LogError("철거 확인 UI가 연결되지 않았습니다.");
            return;
        }

        SelectBuilding(building);
    }

    // 일반 건물 철거 시 90% 자원 환급
    // 함수 설명: 건설 비용 항목별로 90%를 내림 계산하여 플레이어 자원에 반환한다.
    void RefundBuildingCost(Building building)
    {
        if (building == null || building.Data == null)
            return;

        if (playerResources == null)
        {
            Debug.LogError(
                "PlayerResources가 연결되어 있지 않습니다."
            );
            return;
        }

        if (building.Data.constructionCosts == null)
            return;

        foreach (ResourceAmount cost
                 in building.Data.constructionCosts)
        {
            int refundAmount =
                Mathf.FloorToInt(cost.amount * 0.9f);

            if (refundAmount <= 0)
                continue;

            playerResources.AddResource(
                cost.resourceType,
                refundAmount
            );

            Debug.Log(
                cost.resourceType +
                " 철거 환급 : " +
                refundAmount
            );
        }
    }

    // 데코레이션 철거 시 아이템 1개 반환
    // 함수 설명: 철거한 데코레이션을 인벤토리에 1개 되돌린다.
    bool RefundDecoration(Building building)
    {
        if (building == null || building.Data == null)
            return false;

        if (DecorationInventory.Instance == null)
        {
            Debug.LogError(
                "DecorationInventory가 존재하지 않습니다."
            );
            return false;
        }

        DecorationInventory.Instance.AddDecoration(
            building.Data,
            1
        );

        Debug.Log(
            "데코레이션 반환 : " +
            building.Data.buildingName +
            " x1"
        );

        return true;
    }

    // 함수 설명: 선택된 건물의 원래 색상을 복원하고 선택을 해제한다.
    public void ClearSelection()
    {
        if (selectedBuilding == null)
            return;

        SpriteRenderer renderer =
            selectedBuilding.GetComponent<SpriteRenderer>();

        if (renderer != null)
        {
            renderer.color = originalColor;
        }

        Debug.Log(
            "철거 대상 선택 해제 : " +
            selectedBuilding.Data.buildingName
        );

        selectedBuilding = null;
    }

    // 실제 철거 처리
    // 함수 설명: 철거 종류별 환급을 처리하고 점유 셀을 해제한 뒤 건물을 삭제한다.
    public void ConfirmDemolition()
    {
        if (selectedBuilding == null)
            return;

        Building buildingToDestroy = selectedBuilding;

        if (!buildingToDestroy.IsPlaced ||
            buildingToDestroy.Data == null)
        {
            Debug.LogWarning("철거할 수 없는 건물입니다.");
            ClearSelection();
            return;
        }

        BuildingData data = buildingToDestroy.Data;

        // 건물 종류에 따라 환급 방식 구분
        if (data.buildingType == BuildingType.Decoration)
        {
            // 데코레이션은 아이템 1개 반환
            if (!RefundDecoration(buildingToDestroy))
                return;
        }
        else
        {
            // 일반 건물은 기존 90% 자원 환급
            RefundBuildingCost(buildingToDestroy);
        }

        // Grid 점유 해제
        if (buildingPlacement != null)
        {
            buildingPlacement.ReleaseBuildingCells(
                buildingToDestroy
            );
        }

        Debug.Log(
            "건물 철거 완료 : " +
            data.buildingName
        );

        selectedBuilding = null;

        Destroy(buildingToDestroy.gameObject);
    }
}
