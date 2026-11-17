using UnityEngine;
using UnityEngine.InputSystem;

// 스크립트 설명: 마우스로 건물을 선택하고 선택 상태를 저장한다.
public class BuildingSelection : MonoBehaviour
{
    [Header("Building Mode")]
    // 변수 설명: 현재 건설 모드 및 선택 도구를 관리하는 컴포넌트
    [SerializeField] private BuildingModeManager buildingModeManager;

    [SerializeField]
    // 변수 설명: 철거 확인창의 열림 여부를 확인하는 UI
    private DemolitionConfirmUI demolitionConfirmUI;

    // 현재 선택된 건물
    // 변수 설명: 현재 선택하거나 철거 대상으로 지정한 건물
    private Building selectedBuilding;

    public Building SelectedBuilding
    {
        get { return selectedBuilding; }
    }


    // 함수 설명: 철거 확인창과 건설 모드 상태를 확인한 뒤 좌클릭 건물 선택을 처리한다.
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


    // 함수 설명: 클릭한 위치의 충돌체에서 Building을 찾는다.
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


    // 함수 설명: 현재 선택된 건물을 저장하고 로그를 출력한다.
    void SelectBuilding(Building building)
    {
        selectedBuilding = building;

        Debug.Log(
            "건물 선택 : " +
            selectedBuilding.Data.buildingName
        );
    }


    // 함수 설명: 건물 선택을 해제하고 로그를 출력한다.
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