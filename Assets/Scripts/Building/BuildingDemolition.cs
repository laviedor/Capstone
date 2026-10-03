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

    // 현재 철거 대상으로 선택된 건물
    private Building selectedBuilding;

    // 선택 전 원래 색상
    private Color originalColor = Color.white;


    public Building SelectedBuilding
    {
        get { return selectedBuilding; }
    }


    void Update()
    {
        if (buildingModeManager == null)
            return;


        // 건설 모드가 아니면 철거 선택 해제
        if (!buildingModeManager.IsBuildMode)
        {
            ClearSelection();
            return;
        }


        // 철거 도구가 아니면 철거 선택 해제
        if (buildingModeManager.CurrentTool !=
            BuildingTool.Demolish)
        {
            ClearSelection();
            return;
        }

        // 확인창이 열려 있으면 입력 금지
        if (confirmUI != null &&
            confirmUI.IsOpen)
        {
            return;
        }


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

    // cell 제거 및 건물 제거
    public void ConfirmDemolition()
    {
        if (selectedBuilding == null)
            return;

        Building buildingToDestroy =
            selectedBuilding;

        // 먼저 점유 Cell 해제
        if (buildingPlacement != null)
        {
            buildingPlacement.ReleaseBuildingCells(
                buildingToDestroy
            );
        }

        Debug.Log(
            "건물 철거 완료 : " +
            buildingToDestroy.Data.buildingName
        );

        // 선택 참조 제거
        selectedBuilding = null;

        // 실제 건물 제거
        Destroy(buildingToDestroy.gameObject);
    }








}