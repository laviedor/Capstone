using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;


public class MousePathfinding : MonoBehaviour
{

    [SerializeField] private Grid grid;

    [SerializeField] private Transform player;

    [SerializeField]
    private GameObject destinationMarkerPrefab;

    private GameObject destinationMarker;

    [SerializeField] private BuildingPlacement buildingPlacement;

    [SerializeField] private LineRenderer lineRenderer;

    [SerializeField] private PlayerPathMovement playerMovement;

    private Vector3Int startCell;
    private Vector3Int targetCell;

    private Dictionary<Vector3Int, GridNode> nodes = new Dictionary<Vector3Int, GridNode>();

    [Header("UI")]
    [SerializeField]
    private DemolitionConfirmUI demolitionConfirmUI;

    [Header("Map Bounds")]
    [SerializeField] private MapBounds mapBounds;

    [Header("Inventory UI")]
    [SerializeField] private InventoryUI inventoryUI;

    [Header("Camera")]
    [SerializeField] private CameraFollow cameraFollow;

    private void Update()
    {
        if (Mouse.current == null)
            return;

        // 인벤토리가 열려 있으면 이동 차단
        if (inventoryUI != null && inventoryUI.IsOpen)
            return;

        // 철거 확인창이 열려 있으면 이동 차단
        if (demolitionConfirmUI != null &&
            demolitionConfirmUI.IsOpen)
            return;

        // 건물 위치 보기 중에는 이동 차단
        if (cameraFollow != null &&
            cameraFollow.IsViewingBuilding)
            return;

        // 마우스 좌클릭 이동 (UI 위를 클릭한 경우는 제외)
        if (Mouse.current.leftButton.wasPressedThisFrame && !UIInput.IsPointerOverUI())
        {
            SetDestination();
        }
    }

    public void ClearMovementVisuals()
    {
        // 이동 경로 선 제거
        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 0;
        }

        // 목적지 마커 제거
        if (destinationMarker != null)
        {
            Destroy(destinationMarker);
            destinationMarker = null;
        }

        Debug.Log("이동 경로 및 목적지 마커 제거");
    }

    GridNode GetNode(Vector3Int cellPosition)
    {
        // 기본적으로 이동 가능한 칸
        bool isWalkable = true;

        // 건물 배치 시스템이 존재하면 점유 여부 확인
        if (buildingPlacement != null)
        {
            isWalkable =
                !buildingPlacement.IsCellOccupied(cellPosition);
        }

        // 기존 노드가 있다면 상태 갱신
        if (nodes.TryGetValue(cellPosition, out GridNode node))
        {
            node.walkable = isWalkable;
            return node;
        }

        // 새로운 노드 생성
        GridNode newNode =
            new GridNode(cellPosition, isWalkable);

        nodes.Add(cellPosition, newNode);

        return newNode;


    }


    /// 
    /// 길찾기 
    /// 
    List<GridNode> FindPath(GridNode startNode, GridNode targetNode)
    {
        List<GridNode> openList = new List<GridNode>();
        HashSet<GridNode> closedList = new HashSet<GridNode>();

        openList.Add(startNode);

        // 시작점 설정
        startNode.gCost = 0;
        startNode.hCost =
            GetDistance(startNode, targetNode);

        startNode.parent = null;


        while (openList.Count > 0)
        {
            // Open List에서 F Cost가 가장 작은 Node 찾기
            GridNode currentNode = openList[0];

            for (int i = 1; i < openList.Count; i++)
            {
                if (openList[i].fCost < currentNode.fCost ||
                    (openList[i].fCost == currentNode.fCost &&
                     openList[i].hCost < currentNode.hCost))
                {
                    currentNode = openList[i];
                }
            }


            openList.Remove(currentNode);
            closedList.Add(currentNode);


            // 목적지 도착
            if (currentNode == targetNode)
            {
                return RetracePath(startNode, targetNode);
            }


            // 주변 Node 검사
            foreach (GridNode neighbor in GetNeighbors(currentNode))
            {
                if (!neighbor.walkable ||
                    closedList.Contains(neighbor))
                {
                    Debug.Log( "[A*] 장애물이라서 통과하지 않음 : " + neighbor.cellPosition);

                    continue;
                }



                int newMovementCost =
                    currentNode.gCost + 10;


                if (newMovementCost < neighbor.gCost ||
                    !openList.Contains(neighbor))
                {
                    neighbor.gCost = newMovementCost;

                    neighbor.hCost =
                        GetDistance(neighbor, targetNode);

                    neighbor.parent = currentNode;


                    if (!openList.Contains(neighbor))
                    {
                        openList.Add(neighbor);
                    }
                }
            }
        }


        // 경로를 찾지 못함
        return null;
    }
    /// 
    /// 길찾기 
    /// 
    List<GridNode> GetNeighbors(GridNode node)
    {
        List<GridNode> neighbors =
            new List<GridNode>();


        Vector3Int[] directions =
        {
        new Vector3Int(1, 0, 0),   // 오른쪽
        new Vector3Int(-1, 0, 0),  // 왼쪽
        new Vector3Int(0, 1, 0),   // 위
        new Vector3Int(0, -1, 0)   // 아래
    };


        foreach (Vector3Int direction in directions)
        {
            Vector3Int neighborPosition =node.cellPosition + direction;

            // 해당 Cell의 중앙 월드 좌표
            Vector3 neighborWorldPosition =
                grid.GetCellCenterWorld(neighborPosition);

            // MapBounds 밖의 Cell은 A* 탐색에서 제외
            if (mapBounds != null &&
                !mapBounds.IsInside(neighborWorldPosition))
            {
                continue;
            }

            neighbors.Add(
                GetNode(neighborPosition)
            );
        }


        return neighbors;
    }
    /// 
    /// 길찾기 
    /// 
    int GetDistance(GridNode nodeA, GridNode nodeB)
    {
        int distanceX =
            Mathf.Abs(
                nodeA.cellPosition.x -
                nodeB.cellPosition.x
            );

        int distanceY =
            Mathf.Abs(
                nodeA.cellPosition.y -
                nodeB.cellPosition.y
            );


        return (distanceX + distanceY) * 10;
    }
    /// 
    /// 길찾기 
    /// 
    List<GridNode> RetracePath(
    GridNode startNode,
    GridNode targetNode)
    {
        List<GridNode> path =
            new List<GridNode>();


        GridNode currentNode =
            targetNode;


        while (currentNode != startNode)
        {
            path.Add(currentNode);

            currentNode =
                currentNode.parent;
        }


        path.Reverse();

        return path;
    }
    /// 
    /// 길찾기 
    /// 


    ///
    /// 길 찾으면 선 긋기 
    ///
    void DrawPath(List<GridNode> path)
    {
        if (lineRenderer == null)
            return;

        if (path == null || path.Count == 0)
        {
            lineRenderer.positionCount = 0;
            return;
        }

        lineRenderer.positionCount =
            path.Count + 1;

        Vector3 startPosition =
            grid.GetCellCenterWorld(startCell);

        startPosition.z = -1f;

        lineRenderer.SetPosition(
            0,
            startPosition
        );

        for (int i = 0; i < path.Count; i++)
        {
            Vector3 worldPosition =
                grid.GetCellCenterWorld(
                    path[i].cellPosition
                );

            worldPosition.z = -1f;

            lineRenderer.SetPosition(
                i + 1,
                worldPosition
            );
        }
    }






    void SetDestination()
    {
        // ---------------------------
        // 1. Player의 시작 위치
        // ---------------------------

        startCell =
            grid.WorldToCell(player.position);


        // ---------------------------
        // 2. 마우스 목적지
        // ---------------------------

        Vector2 mouseScreenPosition =
            Mouse.current.position.ReadValue();

        Vector3 mouseWorldPosition =
            Camera.main.ScreenToWorldPoint(
                mouseScreenPosition
            );

        mouseWorldPosition.z = 0;


        // ---------------------------
        // 맵 밖 클릭 방지
        // ---------------------------

        if (mapBounds != null &&
            !mapBounds.IsInside(mouseWorldPosition))
        {
            Debug.Log(
                "맵 밖은 목적지로 지정할 수 없습니다."
            );

            if (lineRenderer != null)
            {
                lineRenderer.positionCount = 0;
            }

            return;
        }


        targetCell =
            grid.WorldToCell(mouseWorldPosition);


        // ---------------------------
        // 3. 목적지 Marker 위치
        // ---------------------------

        Vector3 destinationPosition =
            grid.GetCellCenterWorld(targetCell);

        if (destinationMarker == null)
        {
            destinationMarker =
                Instantiate(destinationMarkerPrefab);
        }

        destinationMarker.transform.position =
            destinationPosition;

        GridNode startNode = GetNode(startCell);
        GridNode targetNode = GetNode(targetCell);

        if (!targetNode.walkable)
        {
            Debug.Log(
                "건물이 있는 위치는 목적지로 지정할 수 없습니다."
            );

            if (lineRenderer != null)
            {
                lineRenderer.positionCount = 0;
            }

            return;
        }

        Debug.Log(
            "Start : " + startNode.cellPosition +
            " / Target : " + targetNode.cellPosition
        );

        Debug.Log(
            "[A* 검사] 시작 셀: " + startCell +
            " / 목적지 셀: " + targetCell +
            " / 시작 이동 가능: " + startNode.walkable +
            " / 목적지 이동 가능: " + targetNode.walkable +
            " / 플레이어 위치: " + player.position +
            " / 목적지 위치: " + destinationPosition
        );

        List<GridNode> path = FindPath(startNode, targetNode);


        if (path != null)
        {
            Debug.Log( "경로 탐색 성공! 이동 칸 수 : " + path.Count );

            foreach (GridNode node in path)
            {
                Debug.Log(
                    "Path : " + node.cellPosition
                );
            }

            DrawPath(path);

            playerMovement.MoveAlongPath(path);
        }
        else
        {
            Debug.Log(
                "목적지까지 이동할 수 없습니다."
            );

            if (lineRenderer != null)
            {
                lineRenderer.positionCount = 0;
            }
        }


        //// ---------------------------
        //// 4. 확인용 출력
        //// ---------------------------

        //Debug.Log(
        //    "Start : " + startCell +
        //    " / Target : " + targetCell
        //);
    }
}
