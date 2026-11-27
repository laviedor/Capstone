using UnityEngine;

// 다른 플레이어 캐릭터: 서버에서 받은 좌표로 부드럽게 이동
//   좌표는 UDP 와 채팅 연결(TCP) 두 길로 온다 (RemotePlayerManager). 늦게 도착한 예전 좌표는 순서 번호로 가려낸다
public class RemotePlayer : MonoBehaviour
{
    [SerializeField] private PlayerNameLabel nameLabel;
    [SerializeField] private float smoothing = 15f;

    private const int StaleWindow = 60000; // 순서 번호(ms)가 이만큼 안쪽으로 뒤져 있으면 늦게 온 예전 좌표로 본다

    public string PlayerName { get; private set; }
    public long UserId { get; private set; }        // 유저 id (채팅 연결로 알게 된 경우. 모르면 0)
    public bool Reliable { get; private set; }      // 채팅 연결로 같이 있다고 확인된 사람 (떠나면 서버가 알려 준다)
    public float LastReceived { get; private set; } // 마지막으로 소식을 받은 시각 (Time.time)

    private Vector3 target;
    private int lastSequence;
    private SpriteRenderer body;

    public void Init(string playerName, Vector3 position)
    {
        PlayerName = playerName;
        gameObject.name = "RemotePlayer_" + playerName;
        nameLabel.SetName(playerName);
        transform.position = target = position;
        LastReceived = Time.time;
        body = GetComponentInChildren<SpriteRenderer>();
    }

    // 받은 좌표 반영. sequence: 보낸 순서 (예전 클라이언트의 것은 0), reliable: 채팅 연결로 온 것인지
    public void Apply(Vector3 position, int sequence, long userId, bool reliable)
    {
        LastReceived = Time.time;
        if (reliable) Reliable = true;
        if (userId > 0) UserId = userId;

        if (sequence > 0)
        {
            int behind = lastSequence - sequence;
            if (behind > 0 && behind < StaleWindow) return; // 다른 길로 먼저 온 좌표보다 예전 것
            lastSequence = sequence;
        }
        target = position;
    }

    private LookInfo appliedLook;

    // 이 겉모습을 이미 그리고 있는지
    public bool HasLook(LookInfo look) => look != null && look.SameAs(appliedLook);

    // 그 사람이 입은 장비를 캐릭터에 겹쳐 그린다 (서버가 알려 준 겉모습)
    public void ApplyLook(LookInfo look)
    {
        appliedLook = look;

        if (body == null)
            body = GetComponentInChildren<SpriteRenderer>();

        if (body == null)
            return;

        RemoteEquipmentVisual visual = body.GetComponent<RemoteEquipmentVisual>();

        if (visual == null)
            visual = body.gameObject.AddComponent<RemoteEquipmentVisual>();

        visual.SetLook(look);
    }

    // 화면에서 이 캐릭터가 차지하는 범위 (픽셀). 우클릭으로 고를 때 쓴다: 몸과 머리 위 이름까지, 누르기 쉽게 조금 넉넉하게
    public Rect ScreenRect(Camera camera, float padding)
    {
        Bounds bounds = body != null ? body.bounds : new Bounds(transform.position, Vector3.one);
        Vector3 min = camera.WorldToScreenPoint(bounds.min);
        Vector3 max = camera.WorldToScreenPoint(bounds.max);
        float nameHeight = (max.y - min.y) * 0.5f; // 이름표는 머리 바로 위에 있다
        return Rect.MinMaxRect(min.x - padding, min.y - padding, max.x + padding, max.y + nameHeight + padding);
    }

    void Update()
    {
        transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
    }
}
