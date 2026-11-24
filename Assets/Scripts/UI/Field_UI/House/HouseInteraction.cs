using UnityEngine;

// 집 입구(마을의 집 건물) 또는 출구(집 안의 문).
//   예전에는 플레이어가 가까이 가서 F 키를 눌러야 했지만, 이제는 위치와 상관없이
//   클릭하면 뜨는 '이동' 버튼을 누르거나 더블클릭해서 들어가고 나온다 (HouseDoor, HouseMovePopup)
//   출구: 집 안 위쪽 가운데에 놓인 문 (HouseInteriorScene 의 ExitPoint). 누르면 '나가기' 버튼이 뜨고, 누르면 내 마을로 나간다
//         문 그림은 Resources/HouseDecor/HouseExitDoor (직접 그린 도트, 아래 가운데가 발 위치)
//         문이 서 있는 자리는 집 안 건설·길찾기에서 막는다
public class HouseInteraction : HouseDoor
{
    private const string ExitDoorSpritePath = "HouseDecor/HouseExitDoor";

    // 문 그림에서 깔개(발 앞)를 뺀 문짝·문틀이 차지하는 영역 (발 위치 기준, 월드 단위)
    private static readonly Vector2 ExitBlockMin = new Vector2(-0.62f, 0.3f);
    private static readonly Vector2 ExitBlockMax = new Vector2(0.62f, 2.4f);

    [Header("상호작용 종류")]
    // true면 퇴장 지점, false면 입장 지점으로 동작한다.
    [SerializeField] private bool isExitPoint = false;

    [Header("출구 문 그림 (비우면 Resources 의 기본 문)")]
    [SerializeField] private Sprite exitDoorSprite;

    private BuildingPlacement placement;

    public override bool CanUse => isExitPoint ? CanExitHouse() : CanEnterHouse();

    public override string ActionLabel => isExitPoint ? "나가기" : "이동";

    private void Awake()
    {
        if (isExitPoint)
            ApplyExitDoorSprite();
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        if (isExitPoint)
            SetBlocked(true);
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (isExitPoint)
            SetBlocked(false);
    }

    public override void Use()
    {
        if (!CanUse)
            return;

        if (isExitPoint)
            HouseSceneManager.Instance.ExitHouse();
        else
            HouseSceneManager.Instance.EnterHouse();
    }

    // 출구 문 그림을 놓는다: 이 오브젝트 위치가 문의 발(깔개 아래 가운데)이 된다
    private void ApplyExitDoorSprite()
    {
        Sprite sprite = exitDoorSprite != null ? exitDoorSprite : Resources.Load<Sprite>(ExitDoorSpritePath);

        if (sprite == null)
        {
            Debug.LogWarning("[집 출구] 문 그림을 찾지 못했어요: Resources/" + ExitDoorSpritePath);
            return;
        }

        SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

        if (spriteRenderer == null)
        {
            GameObject visual = new GameObject("ExitDoorVisual");
            visual.transform.SetParent(transform, false);
            spriteRenderer = visual.AddComponent<SpriteRenderer>();
        }

        spriteRenderer.sprite = sprite;
        spriteRenderer.drawMode = SpriteDrawMode.Simple;
        spriteRenderer.transform.localPosition = Vector3.zero;
        spriteRenderer.transform.localScale = Vector3.one;
        spriteRenderer.sortingOrder = 0; // 집 안 장식과 같은 순서 (위아래 위치로 앞뒤가 정해진다)
    }

    // 문이 서 있는 칸을 집 안 건설·길찾기에서 막거나 푼다
    private void SetBlocked(bool on)
    {
        if (placement == null)
            placement = FindFirstObjectByType<BuildingPlacement>(FindObjectsInactive.Include);

        if (placement == null)
            return;

        Vector2 foot = transform.position;
        placement.SetHouseSceneryArea(foot + ExitBlockMin, foot + ExitBlockMax, on);

        // 막을 때만: 플레이어가 문 자리에 서 있으면 가까운 빈칸으로 옮긴다 (풀 때는 막힌 칸이 줄어들 뿐이라 필요 없음)
        if (on)
            BuildingSync.NotifyLayoutChanged();
    }
}
