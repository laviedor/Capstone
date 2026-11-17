using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;


/// <summary>마우스 클릭 목적지의 A* 경로 탐색, 이동 표시, 자원 접근과 채집을 관리한다.</summary>
public class MousePathfinding : MonoBehaviour
{

    // 셀 좌표와 월드 좌표를 변환할 기준 그리드
    [SerializeField] private Grid grid;

    // 이동 시작점 및 자원 접근 거리 계산에 사용할 플레이어
    [SerializeField] private Transform player;

    [SerializeField]
    private GameObject destinationMarkerPrefab;

    // 현재 화면에 생성된 목적지 표시 오브젝트
    private GameObject destinationMarker;

    // 건물 점유 셀을 이동 장애물로 판정하기 위한 참조
    [SerializeField] private BuildingPlacement buildingPlacement;

    // 탐색된 이동 경로를 선으로 표시
    [SerializeField] private LineRenderer lineRenderer;

    // 실제 경로 이동을 수행하는 컴포넌트
    [SerializeField] private PlayerPathMovement playerMovement;

    // A* 탐색 시작 셀
    private Vector3Int startCell;
    // A* 탐색 목표 셀
    private Vector3Int targetCell;

    // 셀별 탐색 노드를 보관하는 캐시
    private Dictionary<Vector3Int, GridNode> nodes = new Dictionary<Vector3Int, GridNode>();

    [Header("UI")]
    [SerializeField]
    private DemolitionConfirmUI demolitionConfirmUI;

    [Header("Map Bounds")]
    // 현재 Scene에서 이동 가능한 맵 경계
    [SerializeField] private MapBounds mapBounds;

    // 집 등 다른 Scene에서 돌아올 때 복구할 마을 경계
    private MapBounds villageMapBounds;

    [Header("Inventory UI")]
    [SerializeField] private InventoryUI inventoryUI;

    [Header("Camera")]
    [SerializeField] private CameraFollow cameraFollow;

    [Header("자원 접근")]
    // 자원에 접근한 것으로 판정하는 거리
    [SerializeField] private float resourceInteractionDistance = 1.5f;

    // 이동 완료 후 채집할 목표 자원
    private ResourceNode targetResource;
    // 진행 중인 자원 채집 코루틴
    private Coroutine gatheringCoroutine;


    // 현재 표시 중인 채집 진행률 UI
    private GatheringProgressUI activeProgressUI;


    /// <summary>최초 지정된 마을 경계를 저장한다.</summary>
    private void Awake()
    {
        // 처음 연결된 마을 MapBounds 저장
        villageMapBounds = mapBounds;
    }

    /// <summary>UI 상태를 확인한 뒤 좌클릭 자원 탐색 또는 일반 이동을 실행한다.</summary>
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

        // 마우스 좌클릭 이동
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            // 먼저 클릭한 위치에 자원이 있는지 확인
            Vector2 mouseScreenPosition =
                Mouse.current.position.ReadValue();

            Vector3 mouseWorldPosition =
                Camera.main.ScreenToWorldPoint(
                    mouseScreenPosition
                );

            Vector2 clickPosition = new Vector2(
                mouseWorldPosition.x,
                mouseWorldPosition.y
            );

            // 클릭 지점과 겹치는 모든 콜라이더를 조사해 자원을 우선 탐색
            Collider2D[] hitColliders =
                Physics2D.OverlapPointAll(clickPosition);

            foreach (Collider2D hit in hitColliders)
            {
                // 자식 콜라이더를 클릭해도 부모의 ResourceNode를 찾는다
                ResourceNode resource =
                    hit.GetComponentInParent<ResourceNode>();

                if (resource != null)
                {
                    resource.OnResourceClicked();

                    // 이미 채집된 자원은 접근하지 않음
                    if (resource.IsDepleted)
                        return;

                    MoveToResource(resource);
                    return;
                }
            }

            CancelResourceTarget();
            // 자원이 아닌 바닥을 클릭한 경우 기존 이동 실행
            SetDestination();
        }
    }

    /// <summary>경로 선과 목적지 마커를 제거한다.</summary>
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

    // 현재 이동 범위를 다른 MapBounds로 변경
    /// <summary>현재 이동 가능 영역을 새 MapBounds로 변경한다.</summary>
    public void SetMapBounds(MapBounds newBounds)
    {
        if (newBounds == null)
        {
            Debug.LogWarning("변경할 MapBounds가 없습니다.");
            return;
        }

        mapBounds = newBounds;

        // 이전 경로 탐색 데이터 초기화
        nodes.Clear();

        Debug.Log("MapBounds 변경 완료: " + newBounds.name);
    }

    // 마을 MapBounds로 복구
    /// <summary>처음 저장한 마을 MapBounds로 복귀한다.</summary>
    public void RestoreVillageMapBounds()
    {
        SetMapBounds(villageMapBounds);
    }

    /// <summary>Scene 전환 전에 채집, 이동, 경로 표시 및 탐색 데이터를 정리한다.</summary>
    public void PrepareForSceneTransition()
    {
        // 진행 중인 자원 채집 취소
        CancelResourceTarget();

        // 자원 도착 이벤트 해제
        if (playerMovement != null)
        {
            playerMovement.OnDestinationReached -=
                OnResourceDestinationReached;

            // 기존 이동 중단
            playerMovement.StopMovement();
        }

        targetResource = null;

        // 경로 및 목적지 표시 제거
        ClearMovementVisuals();

        // A* 탐색 데이터 초기화
        nodes.Clear();

        Debug.Log("Scene 전환을 위한 이동 상태 초기화 완료");
    }

    /// <summary>지정한 셀의 이동 가능 여부를 갱신하고 탐색 노드를 반환한다.</summary>
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
    /// <summary>A* 알고리즘으로 시작 노드에서 목표 노드까지의 경로를 계산한다.</summary>
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
                    Debug.Log("[A*] 장애물이라서 통과하지 않음 : " + neighbor.cellPosition);

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
    /// <summary>현재 노드의 상하좌우 이웃을 맵 경계 안에서 수집한다.</summary>
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
            Vector3Int neighborPosition = node.cellPosition + direction;

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
    /// <summary>대각선 이동이 없는 4방향 격자에서 맨해튼 거리를 계산한다.</summary>
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
    /// <summary>부모 노드를 역추적하여 시작점에서 목적지 순서의 경로를 만든다.</summary>
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
    /// <summary>찾은 경로를 LineRenderer의 점들로 시각화한다.</summary>
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






    /// <summary>클릭 위치를 목적지로 변환하고 이동 가능한 경로를 계산하여 이동을 요청한다.</summary>
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

        // 출발 노드에서 목표 노드까지 A* 탐색 실행
        List<GridNode> path = FindPath(startNode, targetNode);


        if (path != null)
        {
            Debug.Log("경로 탐색 성공! 이동 칸 수 : " + path.Count);

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

    /// <summary>자원 주변의 상하좌우 접근 가능 셀 중 가장 짧은 경로를 선택한다.</summary>
    private void MoveToResource(ResourceNode resource)
    {
        if (resource == null || playerMovement == null || grid == null)
            return;

        CancelResourceTarget();

        Vector3Int playerCell = grid.WorldToCell(player.position);
        Vector3Int resourceCell = grid.WorldToCell(resource.transform.position);

        // 자원이 있는 칸을 제외한 상하좌우 후보 위치
        Vector3Int[] directions =
        {
        new Vector3Int(1, 0, 0),
        new Vector3Int(-1, 0, 0),
        new Vector3Int(0, 1, 0),
        new Vector3Int(0, -1, 0)
    };

        // 현재까지 발견한 최단 접근 경로
        List<GridNode> bestPath = null;
        Vector3Int bestCell = playerCell;

        foreach (Vector3Int direction in directions)
        {
            Vector3Int candidateCell = resourceCell + direction;
            Vector3 candidateWorld = grid.GetCellCenterWorld(candidateCell);

            if (mapBounds != null && !mapBounds.IsInside(candidateWorld))
                continue;

            GridNode candidateNode = GetNode(candidateCell);

            if (!candidateNode.walkable)
                continue;

            // A* 탐색에서 이전 계산값이 남지 않도록 초기화
            nodes.Clear();

            GridNode startNode = GetNode(playerCell);
            GridNode endNode = GetNode(candidateCell);

            List<GridNode> candidatePath = FindPath(startNode, endNode);

            if (candidatePath == null)
                continue;

            if (bestPath == null || candidatePath.Count < bestPath.Count)
            {
                bestPath = candidatePath;
                bestCell = candidateCell;
            }
        }

        if (bestPath == null)
        {
            Debug.Log("자원 주변으로 이동할 수 없습니다.");
            return;
        }

        targetResource = resource;
        startCell = playerCell;
        targetCell = bestCell;

        DrawPath(bestPath);

        if (destinationMarkerPrefab != null)
        {
            if (destinationMarker == null)
                destinationMarker = Instantiate(destinationMarkerPrefab);

            destinationMarker.transform.position =
                grid.GetCellCenterWorld(bestCell);
        }

        // 이동 완료 시 자동 채집을 시작하도록 이벤트 등록
        playerMovement.OnDestinationReached += OnResourceDestinationReached;
        playerMovement.MoveAlongPath(bestPath);
    }

    // 자원 근처에 도착했을 때 채집을 시작합니다.
    /// <summary>자원 주변 도착 시 거리 확인 후 채집 코루틴을 시작한다.</summary>
    private void OnResourceDestinationReached()
    {
        ResourceNode reachedResource = targetResource;

        if (playerMovement != null)
            playerMovement.OnDestinationReached -= OnResourceDestinationReached;

        targetResource = null;

        if (reachedResource == null)
            return;

        float distance = Vector2.Distance(
            player.position,
            reachedResource.transform.position
        );

        if (distance <= resourceInteractionDistance)
        {
            Debug.Log("자원 근처 도착! 채집 시작: " + reachedResource.Kind);
            gatheringCoroutine = StartCoroutine(GatherResourceRoutine(reachedResource));
        }
        else
        {
            Debug.Log("채집 거리가 부족합니다. 현재 거리: " + distance);
        }
    }

    // 정해진 시간 동안 기다린 뒤 자원 획득을 요청합니다.
    /// <summary>채집 시간에 따라 진행률을 갱신하고 완료 시 자원 획득을 처리한다.</summary>
    private IEnumerator GatherResourceRoutine(ResourceNode resource)
    {
        if (resource == null || resource.IsDepleted)
        {
            gatheringCoroutine = null;
            yield break;
        }

        GatheringProgressUI progressUI = resource.ProgressUI;

        activeProgressUI = progressUI;

        // 진행률 UI 표시
        if (progressUI != null)
            progressUI.ShowProgress();

        float elapsedTime = 0f;
        // 0으로 나누는 문제를 방지하도록 최소 채집 시간을 보장
        float duration = Mathf.Max(0.01f, resource.GatherDuration);

        Debug.Log("채집 시작: " + resource.Kind);

        // 채집 시간에 맞춰 진행률 갱신
        while (elapsedTime < duration)
        {
            if (resource == null || resource.IsDepleted)
            {
                if (progressUI != null)
                    progressUI.HideProgress();

                gatheringCoroutine = null;
                yield break;
            }

            elapsedTime += Time.deltaTime;

            if (progressUI != null)
            {
                progressUI.SetProgress(
                    elapsedTime / duration
                );
            }

            yield return null;
        }

        // 채집 완료
        if (resource != null && !resource.IsDepleted)
        {
            resource.CompleteGathering();
        }

        // 진행률 UI 숨기기
        if (progressUI != null)
            progressUI.HideProgress();

        gatheringCoroutine = null;
        activeProgressUI = null;
    }

    // 다른 곳을 클릭하면 이전 자원 목표 및 진행 중인 채집을 취소합니다.
    /// <summary>진행 중인 채집을 중지하고 진행률 표시를 숨긴다.</summary>
    private void CancelResourceTarget()
    {
        if (gatheringCoroutine != null)
        {
            StopCoroutine(gatheringCoroutine);
            gatheringCoroutine = null;

            Debug.Log("자원 채집 취소");
        }

        if (activeProgressUI != null)
        {
            activeProgressUI.HideProgress();
            activeProgressUI = null;
        }
    }

    /// <summary>컴포넌트가 비활성화될 때 진행 중인 채집을 취소한다.</summary>
    private void OnDisable()
    {
        CancelResourceTarget();
    }
}
