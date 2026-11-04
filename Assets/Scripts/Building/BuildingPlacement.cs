using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;




public class BuildingPlacement : MonoBehaviour
{
    [SerializeField] private Grid grid;
    [SerializeField] private GameObject buildingPrefab;
    [SerializeField] private GameObject gridPreviewCellPrefab;

    

    ///
    /////건설모드 
    ///
    [Header("Build Grid")] // 마우스용 그리드 
    [SerializeField] private GameObject buildGridCellPrefab;

    [SerializeField] private int gridPadding = 2;

    private List<GameObject> buildGridCells = new List<GameObject>();
    ///
    ///
    ///
    [Header("Map Grid")]
    [SerializeField] private GameObject mapGridCellPrefab;
    [SerializeField] private MapBounds mapBounds;

    private List<GameObject> mapGridCells = new List<GameObject>();




    [Header("Player Resources")]
    [SerializeField] private PlayerResources playerResources;

    [Header("Building Mode")] //B 모드 건설
    [SerializeField] private BuildingModeManager buildingModeManager;


    // 설치 전 미리 보여줄 건물
    private GameObject previewObject;

    // Preview의 SpriteRenderer
    private SpriteRenderer previewRenderer;

    // 현재 Preview가 위치한 Grid 칸
    private Vector3Int currentCellPosition;

    // 현재 설치가 가능한가?
    private bool canPlace;

    // 이미 건물이 설치된 Grid 칸들
    private HashSet<Vector3Int> occupiedCells = new HashSet<Vector3Int>();
    // 건물 종류별 마지막으로 사용한 번호
    private Dictionary<string, int> buildingNumberCounters =
        new Dictionary<string, int>();

    private int GetNextBuildingNumber(string buildingID)
    {
        if (string.IsNullOrEmpty(buildingID))
        {
            Debug.LogError("BuildingID가 비어 있습니다.");
            return 0;
        }

        if (!buildingNumberCounters.ContainsKey(buildingID))
        {
            buildingNumberCounters[buildingID] = 0;
        }

        buildingNumberCounters[buildingID]++;

        return buildingNumberCounters[buildingID];
    }

    //Grid Preview Cell을 저장할 변수
    private List<GameObject> gridPreviewCells = new List<GameObject>();

    //BuildingData를 저장할 변수 및 각기 다른 건물들의 크기 저장
    //현재 건물의 데이터
    private BuildingData currentBuildingData;
    // 현재 건물 Component
    private Building currentBuilding;


    void Start()
    {
        CreatePreview();

        CreateGridPreview();

        CreateBuildGrid();

        CreateMapGrid();

        SetBuildModeVisuals(false);
    }

    void Update()
    {
        if (buildingModeManager == null)
            return;


        // 건설 모드가 아니면 Preview 숨김
        if (!buildingModeManager.IsBuildMode)
        {
            SetBuildModeVisuals(false);
            return;
        }


        // 철거 도구를 사용 중이면
        // BuildingPlacement는 아무것도 하지 않음
        if (buildingModeManager.CurrentTool != BuildingTool.Build)
        {
            SetBuildModeVisuals(false);
            return;
        }


        // Build 도구일 때만 Preview 표시
        SetBuildModeVisuals(true);


        UpdatePreviewPosition();

        UpdateGridPreviewPosition();

        UpdateBuildGridPosition();

        UpdatePreviewColor();

        UpdateGridPreviewColor();


        // 우클릭 = 건물 설치
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            PlaceBuilding();
        }
    }

    // Preview 생성
    void CreatePreview()
    {
        previewObject = Instantiate(buildingPrefab);

        previewRenderer =
            previewObject.GetComponent<SpriteRenderer>();


        // Building Component 가져오기
        currentBuilding =
            previewObject.GetComponent<Building>();


        if (currentBuilding == null)
        {
            Debug.LogError(
                "Building Component가 없습니다."
            );

            return;
        }


        // Building이 가지고 있는 BuildingData 가져오기
        currentBuildingData =
            currentBuilding.Data;


        if (currentBuildingData == null)
        {
            Debug.LogError(
                "BuildingData가 연결되어 있지 않습니다."
            );

            return;
        }


        if (previewRenderer != null)
        {
            Color color = previewRenderer.color;

            color.a = 0.5f;

            previewRenderer.color = color;
        }
    }
    // 마우스를 따라 Preview 이동
    void UpdatePreviewPosition()
    {
        Vector2 mouseScreenPosition =
            Mouse.current.position.ReadValue();

        Vector3 mouseWorldPosition =
            Camera.main.ScreenToWorldPoint(mouseScreenPosition);

        mouseWorldPosition.z = 0;

        // 마우스 위치를 Grid 좌표로 변환
        currentCellPosition = grid.WorldToCell(mouseWorldPosition);

        // 현재 칸의 중앙 위치
        Vector3 cellCenterPosition = grid.GetCellCenterWorld(currentCellPosition);

        // 건물 크기에 따른 위치 보정
        float offsetX = (currentBuildingData.width - 1) * grid.cellSize.x / 2f;

        float offsetY = (currentBuildingData.height - 1) * grid.cellSize.y / 2f;

        Vector3 buildingPosition = cellCenterPosition + new Vector3(offsetX, offsetY, 0);

        previewObject.transform.position = buildingPosition;

        // 해당 칸이 비어 있는지 확인
        canPlace = CanPlaceBuilding();
    }

    // Preview 색상 변경
    void UpdatePreviewColor()
    {
        if (previewRenderer == null)
            return;

        Color color;

        if (canPlace)
        {
            // 설치 가능 = 초록색
            color = new Color(0f, 1f, 0f, 0.5f);
        }
        else
        {
            // 설치 불가능 = 빨간색
            color = new Color(1f, 0f, 0f, 0.5f);
        }

        previewRenderer.color = color;
    }

    //여러 칸 검사
    bool CanPlaceBuilding()
    {
        if (currentBuildingData == null)
            return false;

        if (mapBounds == null)
            return false;

        for (int x = 0; x < currentBuildingData.width; x++)
        {
            for (int y = 0; y < currentBuildingData.height; y++)
            {
                Vector3Int cell =
                    currentCellPosition +
                    new Vector3Int(x, y, 0);

                // 이미 건물이 있는 Cell인지 검사
                if (occupiedCells.Contains(cell))
                {
                    return false;
                }

                // Cell의 중앙 월드 좌표
                Vector3 cellWorldPosition =
                    grid.GetCellCenterWorld(cell);

                // MapBounds 밖인지 검사
                if (!mapBounds.IsInside(cellWorldPosition))
                {
                    return false;
                }
            }
        }

        return true;
    }

    // 건물 목록 및 목록에서 건물 선택 및 설치
    public void SelectBuilding( GameObject newBuildingPrefab )
    {
        if (newBuildingPrefab == null)
        {
            Debug.LogError(
                "선택할 Building Prefab이 없습니다."
            );

            return;
        }

        Building building =
            newBuildingPrefab.GetComponent<Building>();

        if (building == null)
        {
            Debug.LogError(
                "선택한 Prefab에 Building 컴포넌트가 없습니다."
            );

            return;
        }

        if (building.Data == null)
        {
            Debug.LogError(
                "선택한 Prefab에 BuildingData가 연결되어 있지 않습니다."
            );

            return;
        }

        buildingPrefab = newBuildingPrefab;
        currentBuildingData = building.Data;

        Debug.Log(
            "건물 선택 : " +
            currentBuildingData.buildingName
        );

        if (previewObject != null)
        {
            Destroy(previewObject);
            previewObject = null;
        }

        CreatePreview();
        CreateGridPreview();
        CreateBuildGrid();
    }


    // 실제 건물 설치
    void PlaceBuilding()
    {
        if (!canPlace)
        {
            Debug.Log("이 위치에는 건물을 설치할 수 없습니다.");
            return;
        }

        if (buildingPrefab == null ||
            currentBuildingData == null ||
            previewObject == null)
        {
            Debug.LogError("건물 설치에 필요한 정보가 없습니다.");
            return;
        }

        if (string.IsNullOrEmpty(currentBuildingData.buildingID))
        {
            Debug.LogError("BuildingID가 설정되지 않았습니다.");
            return;
        }

        Building prefabBuilding =
            buildingPrefab.GetComponent<Building>();

        if (prefabBuilding == null || prefabBuilding.Data == null)
        {
            Debug.LogError("건물 Prefab 설정이 올바르지 않습니다.");
            return;
        }

        if (!CanAffordBuilding())
        {
            Debug.Log("건물을 설치하기 위한 자원이 부족합니다.");
            return;
        }

        GameObject placedObject = Instantiate(
            buildingPrefab,
            previewObject.transform.position,
            Quaternion.identity
        );

        Building placedBuilding =
            placedObject.GetComponent<Building>();

        if (placedBuilding == null)
        {
            Debug.LogError("생성된 건물에 Building 컴포넌트가 없습니다.");
            Destroy(placedObject);
            return;
        }

        // 건물 번호 부여
        int newNumber = GetNextBuildingNumber(
            placedBuilding.BuildingID
        );

        if (newNumber <= 0)
        {
            Destroy(placedObject);
            return;
        }

        placedBuilding.SetBuildingNumber(newNumber);

        // 실제 설치 완료 처리
        placedBuilding.Initialize(currentCellPosition);

        // 건설 비용 차감
        PayBuildingCost();

        // Grid 점유 등록
        OccupyBuildingCells();

        Debug.Log(
            "건물 설치 완료 : " +
            placedBuilding.Data.buildingName +
            " #" + placedBuilding.BuildingNumber +
            " / 위치 : " + currentCellPosition
        );
    }

    //GridpreviewCell을 생성하는 함수
    void CreateGridPreview()
    {
        // 기존 Preview Cell 삭제
        foreach (GameObject cell in gridPreviewCells)
        {
            Destroy(cell);
        }

        gridPreviewCells.Clear();


        // 건물 크기만큼 Preview Cell 생성
        for (int x = 0; x < currentBuildingData.width; x++)
        {
            for (int y = 0; y < currentBuildingData.height; y++)
            {
                GameObject cell =
                    Instantiate(gridPreviewCellPrefab);

                gridPreviewCells.Add(cell);
            }
        }
    }
    //girdPreviewCell의 위치 , 각 square를 grid cell의 중앙에 위치하도록 설정
    void UpdateGridPreviewPosition()
    {
        int index = 0;

        for (int x = 0; x < currentBuildingData.width; x++)
        {
            for (int y = 0; y < currentBuildingData.height; y++)
            {
                Vector3Int cellPosition =
                    currentCellPosition +
                    new Vector3Int(x, y, 0);

                Vector3 worldPosition =
                    grid.GetCellCenterWorld(cellPosition);

                gridPreviewCells[index].transform.position =
                    worldPosition;

                index++;
            }
        }
    }
    //GirdpreviewCell의 색상을 변경하는 함수 , 초록 과 빨강
    void UpdateGridPreviewColor()
    {
        Color color;

        if (canPlace)
        {
            color = new Color(0f, 1f, 0f, 0.35f);
        }
        else
        {
            color = new Color(1f, 0f, 0f, 0.35f);
        }


        foreach (GameObject cell in gridPreviewCells)
        {
            SpriteRenderer renderer =
                cell.GetComponent<SpriteRenderer>();

            if (renderer != null)
            {
                renderer.color = color;
            }
        }
    }

    // Cell 해제 기능 추가 -> 철거하면 occupiedCells 에서 해당 건물 영역 grid cell 을 제거할 수 있음
    public void ReleaseBuildingCells(Building building)
    {
        if (building == null)
            return;

        Vector3Int originCell =
            building.OriginCell;

        for (int x = 0; x < building.Width; x++)
        {
            for (int y = 0; y < building.Height; y++)
            {
                Vector3Int cell =
                    originCell +
                    new Vector3Int(x, y, 0);

                occupiedCells.Remove(cell);
            }
        }

        Debug.Log(
            "건물 점유 Cell 해제 : " +
            originCell
        );
    }

    ///
    /// 건설 모드
    /// 
    void CreateBuildGrid()
    {
        // 기존 격자 삭제
        foreach (GameObject cell in buildGridCells)
        {
            Destroy(cell);
        }

        buildGridCells.Clear();


        if (currentBuildingData == null)
            return;


        // 건물 크기 + 주변 여백
        int gridWidth =
            currentBuildingData.width +
            gridPadding * 2;

        int gridHeight =
            currentBuildingData.height +
            gridPadding * 2;


        int cellCount =
            gridWidth * gridHeight;


        for (int i = 0; i < cellCount; i++)
        {
            GameObject cell =
                Instantiate(buildGridCellPrefab);

            buildGridCells.Add(cell);
        }
    }
    //맵 그리드 만들기
    void CreateMapGrid()
    {
        foreach (GameObject cell in mapGridCells)
        {
            Destroy(cell);
        }

        mapGridCells.Clear();

        if (mapBounds == null)
        {
            Debug.LogError("MapBounds가 연결되어 있지 않습니다.");
            return;
        }

        Vector3Int minCell =
            grid.WorldToCell(mapBounds.Min);

        Vector3Int maxCell =
            grid.WorldToCell(mapBounds.Max);

        for (int x = minCell.x; x < maxCell.x; x++)
        {
            for (int y = minCell.y; y < maxCell.y; y++)
            {
                Vector3Int cellPosition =
                    new Vector3Int(x, y, 0);

                Vector3 worldPosition =
                    grid.GetCellCenterWorld(cellPosition);

                GameObject cell =
                    Instantiate(
                        mapGridCellPrefab,
                        worldPosition,
                        Quaternion.identity
                    );

                mapGridCells.Add(cell);
            }
        }

        Debug.Log(
            "Map Grid 생성 완료 : " +
            minCell + " ~ " + maxCell
        );
    }




    void SetBuildModeVisuals(bool active)
    {
        if (previewObject != null)
        {
            previewObject.SetActive(active);
        }


        // 건물이 실제 차지하는 영역
        foreach (GameObject cell in gridPreviewCells)
        {
            cell.SetActive(active);
        }


        // 마우스 주변 격자
        foreach (GameObject cell in buildGridCells)
        {
            cell.SetActive(active);
        }
        // 맵 전체 격자
        foreach (GameObject cell in mapGridCells)
        {
            cell.SetActive(active);
        }
    }
    void UpdateBuildGridPosition()
    {
        if (currentBuildingData == null)
            return;

        int index = 0;

        for (int x = -gridPadding;
             x < currentBuildingData.width + gridPadding;
             x++)
        {
            for (int y = -gridPadding;
                 y < currentBuildingData.height + gridPadding;
                 y++)
            {
                Vector3Int cellPosition =
                    currentCellPosition +
                    new Vector3Int(x, y, 0);


                Vector3 worldPosition =
                    grid.GetCellCenterWorld(
                        cellPosition
                    );


                buildGridCells[index].transform.position =
                    worldPosition;

                index++;
            }
        }
    }







    //여러 칸을 차지하는 건물들도 설치할 수 있게 해줌
    void OccupyBuildingCells()
    {
        for (int x = 0; x < currentBuildingData.width; x++)
        {
            for (int y = 0; y < currentBuildingData.height; y++)
            {
                Vector3Int cell =
                    currentCellPosition + new Vector3Int(x, y, 0);

                occupiedCells.Add(cell);

                Debug.Log("건물 점유 Cell : " + cell);
            }
        }
    }

    //건물 건설을 하기 전에 자원 검사
    bool CanAffordBuilding()
    {
        if (playerResources == null)
        {
            Debug.LogError("PlayerResources가 연결되어 있지 않습니다.");
            return false;
        }

        if (currentBuildingData == null)
            return false;

        foreach (ResourceAmount cost
                 in currentBuildingData.constructionCosts)
        {
            if (!playerResources.HasResource(
                    cost.resourceType,
                    cost.amount))
            {
                return false;
            }
        }

        return true;
    }

    //건설 비용에 의한 자원 차감
    void PayBuildingCost()
    {
        foreach (ResourceAmount cost
                 in currentBuildingData.constructionCosts)
        {
            playerResources.SpendResource(
                cost.resourceType,
                cost.amount
            );
        }
    }


    //다른 문서에서도 private 를 읽을 수 있게 해줌
    public bool IsCellOccupied(Vector3Int cellPosition)
    {
        bool occupied =
        occupiedCells.Contains(cellPosition);

        if (occupied)
        {
            Debug.Log(
            "[건물] 장애물 확인됨 : " + cellPosition
            );
        }

        return occupied;
    }



}
