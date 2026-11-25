using System.Collections.Generic;
using UnityEngine;

// 집 입구·출구의 공통 기능: 클릭 판정과 이동 실행.
//   마을의 집을 누르면 마우스 옆에 '이동' 버튼(HouseMovePopup)이 뜨고, 누르면 집 안으로 들어간다. 두 번 빠르게 누르면 바로 들어간다
//   플레이어가 집 가까이 있지 않아도 된다. 클릭 처리는 MousePathfinding 이 FindAt 으로 찾아서 한다
//   광장(모두가 같이 쓰는 공간)에서는 집에 들어가지 않는다: 집을 누르면 안내만 뜬다 (FindBlockedEntranceAt)
public abstract class HouseDoor : MonoBehaviour
{
    // 지금 화면에 켜져 있는 입구·출구
    private static readonly List<HouseDoor> active = new List<HouseDoor>();

    [Header("클릭 판정")]
    // 콜라이더와 그림이 없을 때 이 거리(월드 단위) 안을 누르면 이 문을 누른 것으로 본다
    [SerializeField] private float clickRadius = 0.8f;

    public const string MultiverseMessage = "광장에서는 집에 들어갈 수 없어요.";

    // 이동 버튼에 쓸 글자 (예: "이동")
    public virtual string ActionLabel => "이동";

    // 집으로 들어가는 입구인지 (집 안의 출구면 false)
    public virtual bool IsEntrance => true;

    // 지금 이 문으로 이동할 수 있는지
    public abstract bool CanUse { get; }

    // 이동 실행
    public abstract void Use();

    protected virtual void OnEnable()
    {
        if (!active.Contains(this))
            active.Add(this);
    }

    protected virtual void OnDisable()
    {
        active.Remove(this);
    }

    // 월드 좌표의 클릭 위치에 있는, 지금 쓸 수 있는 문. 여러 개면 가장 가까운 것. 없으면 null
    public static HouseDoor FindAt(Vector2 worldPoint)
    {
        HouseDoor best = null;
        float bestDistance = float.MaxValue;

        foreach (HouseDoor door in active)
        {
            if (door == null || !door.CanUse || !door.Contains(worldPoint))
                continue;

            float distance = Vector2.Distance(worldPoint, door.transform.position);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = door;
            }
        }

        return best;
    }

    // 광장이라서 들어갈 수 없는 집 입구를 눌렀는지 (안내만 띄우고 이동하지 않을 때 쓴다)
    public static bool FindBlockedEntranceAt(Vector2 worldPoint)
    {
        if (!InMultiverse)
            return false;

        foreach (HouseDoor door in active)
        {
            if (door != null && door.IsEntrance && door.Contains(worldPoint))
                return true;
        }

        return false;
    }

    // 지금 광장에 있는지
    public static bool InMultiverse =>
        WorldSync.Instance != null && WorldSync.Instance.Entered && WorldSync.Instance.InMultiverse;

    // 클릭 위치가 이 문(콜라이더 > 그림 > 반경 순서로 판정) 안인지
    private bool Contains(Vector2 point)
    {
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
        bool hasCollider = false;

        foreach (Collider2D c in colliders)
        {
            if (!c.enabled)
                continue;

            hasCollider = true;

            if (c.OverlapPoint(point))
                return true;
        }

        if (hasCollider)
            return false;

        SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            Bounds bounds = spriteRenderer.bounds;
            bounds.Expand(new Vector3(0f, 0f, 100f));
            return bounds.Contains(new Vector3(point.x, point.y, bounds.center.z));
        }

        return Vector2.Distance(point, transform.position) <= clickRadius;
    }

    // 집 밖(마을)에서 들어갈 수 있는지
    protected static bool CanEnterHouse()
    {
        HouseSceneManager manager = HouseSceneManager.Instance;

        if (manager == null || manager.IsInsideHouse)
            return false;

        // 광장에서는 집에 들어가지 않는다
        if (InMultiverse)
            return false;

        return FieldSceneManager.Instance == null || !FieldSceneManager.Instance.IsInField;
    }

    // 집 안에서 나갈 수 있는지
    protected static bool CanExitHouse()
    {
        HouseSceneManager manager = HouseSceneManager.Instance;
        return manager != null && manager.IsInsideHouse;
    }
}
