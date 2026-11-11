using UnityEngine;
using UnityEngine.InputSystem;

public class BuildingDemolition : MonoBehaviour
{
    [Header("Building Mode")]
    [SerializeField]
    private BuildingModeManager buildingModeManager;

    [Header("Confirm UI")]
    [SerializeField]
    private DemolitionConfirmUI confirmUI;

    [Header("Building Placement")]
    [SerializeField]
    private BuildingPlacement buildingPlacement;

    //플레이어 자원 환급용
    [Header("Player Resources")]
    [SerializeField]
    private PlayerResources playerResources;

    // 현재 철거 대상으로 선택된 건물
    private Building selectedBuilding;

    // 선택 전 원래 색상
    private Color originalColor = Color.white;


    public Building SelectedBuilding
    {
        get { return selectedBuilding; }
    }


    private void Update()
    {
        if (buildingModeManager == null)
            return;

        // 철거 확인창이 열려 있는 동안
        // 선택한 건물을 유지하고 입력 차단
        if (confirmUI != null && confirmUI.IsOpen)
            return;

        // 건설 모드가 아니면 선택 해제
        if (!buildingModeManager.IsBuildMode)
        {
            ClearSelection();
            return;
        }

        // 철거 도구가 아니면 선택 해제
        if (buildingModeManager.CurrentTool != BuildingTool.Demolish)
        {
            ClearSelection();
            return;
        }

        if (Mouse.current == null)
            return;

        // 철거 도구 상태에서 좌클릭
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            TrySelectBuilding();
        }
    }


    void TrySelectBuilding()
    {
        Vector2 mouseScreenPosition =
            Mouse.current.position.ReadValue();

        Vector3 mouseWorldPosition =
            Camera.main.ScreenToWorldPoint(
                mouseScreenPosition
            );

        Vector2 mousePosition2D =
            new Vector2(
                mouseWorldPosition.x,
                mouseWorldPosition.y
            );


        Collider2D hit =
            Physics2D.OverlapPoint(mousePosition2D);


        // 건물이 없는 곳 클릭
        if (hit == null)
        {
            ClearSelection();
            return;
        }


        Building building =
            hit.GetComponentInParent<Building>();


        // Building이 아닌 오브젝트 클릭
        if (building == null)
        {
            ClearSelection();
            return;
        }


        SelectBuilding(building);
    }


    void SelectBuilding(Building building)
    {
        // 기존 선택 복구
        ClearSelection();


        selectedBuilding = building;


        SpriteRenderer renderer =
            selectedBuilding.GetComponent<SpriteRenderer>();


        if (renderer != null)
        {
            // 기존 색상 저장
            originalColor = renderer.color;

            // 철거 대상 = 빨간색
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

        // 기존 철거 선택 기능 재사용
        SelectBuilding(building);
    }

    // 건물 철거 시 자원 환급 ( 현재 비율 : 90% 환급 )
    void RefundBuildingCost(Building building)
    {
        if (building == null)
            return;

        if (building.Data == null)
            return;

        if (playerResources == null)
        {
            Debug.LogError( "PlayerResources가 연결되어 있지 않습니다." );

            return;
        }

        foreach (ResourceAmount cost
                 in building.Data.constructionCosts)
        {
            int refundAmount = Mathf.FloorToInt(cost.amount * 0.9f); //자원 90% 환급

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









    public void ClearSelection()
    {
        if (selectedBuilding == null)
            return;


        SpriteRenderer renderer =
            selectedBuilding.GetComponent<SpriteRenderer>();


        if (renderer != null)
        {
            // 원래 색상 복구
            renderer.color = originalColor;
        }


        Debug.Log(
            "철거 대상 선택 해제 : " +
            selectedBuilding.Data.buildingName
        );


        selectedBuilding = null;
    }


    // cell 제거 및 건물 제거 및 자원 환급
    public void ConfirmDemolition()
    {
        if (selectedBuilding == null)
            return;

        Building buildingToDestroy =
            selectedBuilding;


        // 건설 비용 90% 환급
        RefundBuildingCost(buildingToDestroy);


        // Grid 점유 해제
        if (buildingPlacement != null)
        {
            buildingPlacement.ReleaseBuildingCells(
                buildingToDestroy
            );
        }


        Debug.Log( "건물 철거 완료 : " + buildingToDestroy.Data.buildingName );


        // 다른 플레이어 화면에서도 지워지도록 서버로 알림
        if (BuildingSync.Instance != null)
        {
            BuildingSync.Instance.OnLocalDemolished(buildingToDestroy);
        }

        selectedBuilding = null;

        Destroy(buildingToDestroy.gameObject);
    }








}