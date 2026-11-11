using UnityEngine;


[RequireComponent(typeof(BoxCollider2D))]
public class MapBounds : MonoBehaviour
{
    private BoxCollider2D boxCollider;

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider2D>();
    }

    // 맵의 실제 월드 영역
    public Bounds Bounds
    {
        get
        {
            if (boxCollider == null)
                boxCollider = GetComponent<BoxCollider2D>();

            return boxCollider.bounds;
        }
    }

    // 맵의 왼쪽 아래
    public Vector2 Min
    {
        get
        {
            return Bounds.min;
        }
    }

    // 맵의 오른쪽 위
    public Vector2 Max
    {
        get
        {
            return Bounds.max;
        }
    }

    // 특정 월드 좌표가 맵 안에 있는지 검사
    public bool IsInside(Vector3 worldPosition)
    {
        return Bounds.Contains(
            new Vector3(
                worldPosition.x,
                worldPosition.y,
                Bounds.center.z
            )
        );
    }
}