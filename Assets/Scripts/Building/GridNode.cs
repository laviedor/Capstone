using UnityEngine;

// 스크립트 설명: A* 경로 탐색에 필요한 셀 위치와 비용 정보를 저장한다.
public class GridNode
{
    // 이 Node의 Grid 좌표
    // 변수 설명: 경로 탐색 노드가 나타내는 격자 좌표
    public Vector3Int cellPosition;

    // 이동 가능한 칸인가?
    // 변수 설명: 해당 노드를 통과할 수 있는지 나타내는 값
    public bool walkable;

    // 시작점부터 현재 Node까지의 비용
    // 변수 설명: 시작 노드부터 현재 노드까지 누적 이동 비용
    public int gCost;

    // 현재 Node에서 목적지까지의 예상 비용
    // 변수 설명: 현재 노드에서 목적지까지의 예상 이동 비용
    public int hCost;

    // 이전 Node
    // 변수 설명: 경로 역추적에 사용할 이전 노드 참조
    public GridNode parent;

    // 총 비용
    // 속성 설명: A* 탐색에서 사용할 총 비용(gCost + hCost)을 계산한다.
    public int fCost
    {
        get
        {
            return gCost + hCost;
        }
    }

    public GridNode(Vector3Int position, bool isWalkable)
    {
        cellPosition = position;
        walkable = isWalkable;
    }
}

