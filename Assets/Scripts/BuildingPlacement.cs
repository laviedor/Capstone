using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;


public class BuildingPlacement : MonoBehaviour
{
    [SerializeField] private Grid grid;
    [SerializeField] private GameObject buildingPrefab;
    [SerializeField] private GameObject gridPreviewCellPrefab;

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

    //Grid Preview Cell을 저장할 변수
    private List<GameObject> gridPreviewCells = new List<GameObject>();

    //BuildingData를 저장할 변수 및 각기 다른 건물들의 크기 저장
    private BuildingData currentBuildingData;


    void Start()
    {
        CreatePreview();
        CreateGridPreview();
    }

    void Update()
    {
        UpdatePreviewPosition();

        UpdateGridPreviewPosition();

        UpdatePreviewColor();

        UpdateGridPreviewColor();


        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            PlaceBuilding();
        }
    }

    // Preview 생성
    void CreatePreview()
    {
        previewObject = Instantiate(buildingPrefab);

        previewRenderer = previewObject.GetComponent<SpriteRenderer>();

        currentBuildingData = previewObject.GetComponent<BuildingData>();

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

        for (int x = 0; x < currentBuildingData.width; x++)
        {
            for (int y = 0; y < currentBuildingData.height; y++)
            {
                Vector3Int cell =
                    currentCellPosition + new Vector3Int(x, y, 0);

                if (occupiedCells.Contains(cell))
                {
                    return false;
                }
            }
        }

        return true;
    }


    // 실제 건물 설치
    void PlaceBuilding()
    {
        // 설치할 수 없는 위치라면 종료
        if (!canPlace)
        {
            Debug.Log("이 위치에는 건물을 설치할 수 없습니다.");
            return;
        }

        // 실제 건물 생성
        Instantiate(
            buildingPrefab,
            previewObject.transform.position,
            Quaternion.identity
        );

        // 현재 Grid 칸을 사용 중으로 등록
        OccupyBuildingCells();

        Debug.Log(
            "건물 설치 완료 : " + currentCellPosition
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
