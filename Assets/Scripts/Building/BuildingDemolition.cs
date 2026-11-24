using System.Collections.Generic;
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

    // 철거 모드에서 마우스를 올린 건물 (붉게 깜빡이며 표시)
    private Building hoveredBuilding;

    // 색을 바꿔 둔 건물과 그 그림들의 원래 색 (한 번에 한 건물만 강조한다)
    private Building highlightedBuilding;
    private readonly List<KeyValuePair<SpriteRenderer, Color>> originalColors =
        new List<KeyValuePair<SpriteRenderer, Color>>();

    [Header("강조 색")]
    // 마우스를 올린 건물의 색 (두 색 사이를 오가며 깜빡인다)
    [SerializeField] private Color hoverTintA = new Color(1f, 0.84f, 0.8f, 1f);
    [SerializeField] private Color hoverTintB = new Color(1f, 0.62f, 0.56f, 1f);
    [SerializeField] private float hoverPulseSpeed = 6f;
    // 철거 확인창이 떠 있는 동안의 건물 색
    [SerializeField] private Color selectedTint = new Color(1f, 0.42f, 0.4f, 1f);

    public Building SelectedBuilding
    {
        get { return selectedBuilding; }
    }

    // 함수 설명: 철거 모드에서 마우스를 올린 건물을 강조하고, 좌클릭하면 철거 확인창을 연다.
    private void Update()
    {
        if (buildingModeManager == null)
            return;

        // 철거 확인창이 열려 있으면 선택한 건물만 강조한 채로 입력 차단
        if (confirmUI != null && confirmUI.IsOpen)
            return;

        if (!buildingModeManager.IsBuildMode ||
            buildingModeManager.CurrentTool != BuildingTool.Demolish ||
            Mouse.current == null)
        {
            ClearSelection();
            SetHovered(null);
            return;
        }

        // 확인창을 닫고 돌아왔으면 선택 표시를 지운다
        if (selectedBuilding != null)
            ClearSelection();

        // 안내 바의 [끝내기] 버튼 등 UI 위에서는 건물을 고르지 않는다
        bool overUI = UIInput.IsPointerOverUI();
        SetHovered(overUI ? null : BuildingAtPointer());

        if (hoveredBuilding != null)
        {
            float t = (Mathf.Sin(Time.unscaledTime * hoverPulseSpeed) + 1f) / 2f;
            Highlight(hoveredBuilding, Color.Lerp(hoverTintA, hoverTintB, t));
        }

        if (Mouse.current.leftButton.wasPressedThisFrame && !overUI && hoveredBuilding != null)
        {
            SelectBuilding(hoveredBuilding);
        }
    }

    // 함수 설명: 마우스 위치에 있는 설치된 건물을 찾는다.
    //           맵 경계(MapBounds) 같은 큰 충돌체가 함께 겹치므로, 겹친 충돌체를 모두 살펴 건물을 고른다.
    //           건물끼리 겹쳐 있으면 화면에서 위에 그려진(앞쪽) 건물을 고른다.
    Building BuildingAtPointer()
    {
        Camera cam = Camera.main;

        if (cam == null)
            return null;

        Vector3 mouseWorldPosition =
            cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());

        Collider2D[] hits = Physics2D.OverlapPointAll(
            new Vector2(mouseWorldPosition.x, mouseWorldPosition.y));

        Building best = null;
        int bestOrder = int.MinValue;
        float bestY = float.MaxValue;

        foreach (Collider2D hit in hits)
        {
            Building building = hit.GetComponentInParent<Building>();

            if (building == null || !building.IsPlaced || building.Data == null)
                continue;

            SpriteRenderer renderer = building.GetComponentInChildren<SpriteRenderer>();
            int order = renderer != null ? renderer.sortingOrder : 0;
            float y = building.transform.position.y;

            // 정렬 순서가 높을수록, 같으면 아래쪽(앞쪽)에 있을수록 우선
            if (order > bestOrder || (order == bestOrder && y < bestY))
            {
                best = building;
                bestOrder = order;
                bestY = y;
            }
        }

        return best;
    }

    // 마우스를 올린 건물이 바뀌면 이전 건물의 색을 되돌린다
    void SetHovered(Building building)
    {
        if (hoveredBuilding == building)
            return;

        hoveredBuilding = building;

        if (building == null && selectedBuilding == null)
            RestoreHighlight();
    }

    // 건물의 모든 그림에 색을 곱해 강조한다 (원래 색은 기억해 두었다가 되돌린다)
    void Highlight(Building building, Color tint)
    {
        if (highlightedBuilding != building)
        {
            RestoreHighlight();
            highlightedBuilding = building;

            if (building != null)
            {
                foreach (SpriteRenderer renderer in building.GetComponentsInChildren<SpriteRenderer>())
                    originalColors.Add(new KeyValuePair<SpriteRenderer, Color>(renderer, renderer.color));
            }
        }

        foreach (KeyValuePair<SpriteRenderer, Color> pair in originalColors)
        {
            if (pair.Key != null)
                pair.Key.color = pair.Value * tint;
        }
    }

    // 강조한 건물의 색을 원래대로 되돌린다
    void RestoreHighlight()
    {
        foreach (KeyValuePair<SpriteRenderer, Color> pair in originalColors)
        {
            if (pair.Key != null)
                pair.Key.color = pair.Value;
        }

        originalColors.Clear();
        highlightedBuilding = null;
    }

    // 함수 설명: 철거 대상으로 지정하고 붉게 표시한 뒤 확인창을 연다.
    void SelectBuilding(Building building)
    {
        if (building == null || building.Data == null)
            return;

        selectedBuilding = building;
        Highlight(building, selectedTint);

        Debug.Log(
            "철거 대상 선택 : " +
            selectedBuilding.Data.buildingName
        );

        if (confirmUI != null)
        {
            confirmUI.Open(selectedBuilding);
        }
        else
        {
            Debug.LogError("철거 확인 UI가 연결되지 않았습니다.");
        }
    }

    // 함수 설명: 외부에서 지정한 건물에 대한 철거 확인을 요청한다.
    public void RequestDemolition(Building building)
    {
        // 친구 마을, 광장에서는 철거할 수 없다: 알림을 띄우고 취소
        if (WorldSync.BlockBuildingChange())
            return;

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

        Debug.Log(
            "철거 대상 선택 해제 : " +
            (selectedBuilding.Data != null ? selectedBuilding.Data.buildingName : selectedBuilding.name)
        );

        selectedBuilding = null;
        RestoreHighlight();
    }

    // 실제 철거 처리
    // 함수 설명: 철거 종류별 환급을 처리하고 점유 셀을 해제한 뒤 건물을 삭제한다.
    public void ConfirmDemolition()
    {
        if (selectedBuilding == null)
            return;

        // 확인창이 열린 사이에 친구 마을이나 광장으로 옮겼으면 철거하지 않는다
        if (WorldSync.BlockBuildingChange())
        {
            ClearSelection();
            return;
        }

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
        if (data.UsesDecorationItem)
        {
            // 데코레이션은 아이템 1개 반환 (자원으로 산 장식은 일반 건물처럼 자원 환급)
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

        // 다른 플레이어 화면에서도 지워지도록 서버로 알림
        if (BuildingSync.Instance != null)
        {
            BuildingSync.Instance.OnLocalDemolished(buildingToDestroy);
        }

        selectedBuilding = null;
        hoveredBuilding = null;
        originalColors.Clear(); // 곧 지워질 건물이므로 색을 되돌리지 않는다
        highlightedBuilding = null;

        Destroy(buildingToDestroy.gameObject);
    }
}
