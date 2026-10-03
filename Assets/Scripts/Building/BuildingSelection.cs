using UnityEngine;
using UnityEngine.InputSystem;

public class BuildingSelection : MonoBehaviour
{
    [Header("Building Mode")]
    [SerializeField] private BuildingModeManager buildingModeManager;

    [SerializeField]
    private DemolitionConfirmUI demolitionConfirmUI;

    // 현재 선택된 건물
    private Building selectedBuilding;

    public Building SelectedBuilding
    {
        get { return selectedBuilding; }
    }


    void Update()
    {
        if (demolitionConfirmUI != null && demolitionConfirmUI.IsOpen)
        {
            return;
        }

        // 건설 모드에서는 건물 선택하지 않음
        if (buildingModeManager != null && buildingModeManager.IsBuildMode)
        {
            return;
        }

        // 좌클릭
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


        // 클릭한 위치에 Collider2D가 있는지 확인
        Collider2D hit =
            Physics2D.OverlapPoint(mousePosition2D);


        // 아무것도 클릭하지 않음
        if (hit == null)
        {
            ClearSelection();
            return;
        }


        // Collider가 자식에 있어도 부모 Building 탐색
        Building building =
            hit.GetComponentInParent<Building>();


        // Building이 아님
        if (building == null)
        {
            ClearSelection();
            return;
        }


        // 건물 선택
        SelectBuilding(building);
    }


    void SelectBuilding(Building building)
    {
        selectedBuilding = building;

        Debug.Log(
            "건물 선택 : " +
            selectedBuilding.Data.buildingName
        );
    }


    public void ClearSelection()
    {
        if (selectedBuilding != null)
        {
            Debug.Log(
                "건물 선택 해제 : " +
                selectedBuilding.Data.buildingName
            );
        }

        selectedBuilding = null;
    }
}