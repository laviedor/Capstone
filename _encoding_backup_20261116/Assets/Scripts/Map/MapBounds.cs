using UnityEngine;


// 맵 영역을 판정하는 데 필요한 BoxCollider2D를 자동으로 요구함
[RequireComponent(typeof(BoxCollider2D))]
public class MapBounds : MonoBehaviour
{
    // 현재 오브젝트에 부착된 영역 판정용 콜라이더
    private BoxCollider2D boxCollider;

    // 오브젝트 초기화 시 BoxCollider2D 참조를 저장
    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider2D>();
    }

    // 맵의 실제 월드 영역
    public Bounds Bounds
    {
        get
        {
            // 초기화되지 않았다면 컴포넌트를 다시 가져옴
            if (boxCollider == null)
                boxCollider = GetComponent<BoxCollider2D>();

            // 월드 좌표 기준의 콜라이더 경계를 반환
            return boxCollider.bounds;
        }
    }

    // 맵의 왼쪽 아래
    // Bounds.min에서 X, Y 값을 사용
    public Vector2 Min
    {
        get
        {
            return Bounds.min;
        }
    }

    // 맵의 오른쪽 위
    // Bounds.max에서 X, Y 값을 사용
    public Vector2 Max
    {
        get
        {
            return Bounds.max;
        }
    }

    // 특정 월드 좌표가 맵 안에 있는지 검사
    // Z축 차이로 인해 2D 영역 검사가 실패하지 않도록 중심 Z를 사용
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