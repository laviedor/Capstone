using UnityEngine;

    public class GridNode
    {
        // 이 Node의 Grid 좌표
        public Vector3Int cellPosition;

        // 이동 가능한 칸인가?
        public bool walkable;

        // 시작점부터 현재 Node까지의 비용
        public int gCost;

        // 현재 Node에서 목적지까지의 예상 비용
        public int hCost;

        // 이전 Node
        public GridNode parent;

        // 총 비용
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

