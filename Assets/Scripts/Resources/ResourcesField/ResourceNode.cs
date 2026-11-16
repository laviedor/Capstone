
using UnityEngine;

// 필드에 배치된 개별 자원 오브젝트의 채집 상태, 보상, 재생성 요청을 관리한다.
public class ResourceNode : MonoBehaviour
{
    // 이 자원 노드가 제공하는 자원의 종류를 정의한다.
    public enum ResourceKind
    {
        Wood,
        Stone,
        Gold
    }

    [Header("자원 종류")]
    [SerializeField]
    // 인스펙터에서 선택한 현재 자원의 종류.
    private ResourceKind resourceKind;

    [Header("채집 설정")]
    [SerializeField]
    // 채집을 완료하는 데 필요한 시간(초).
    private float gatherDuration = 3f;

    [SerializeField]
    // 채집 완료 시 플레이어에게 지급할 자원 수량.
    private int rewardAmount = 10;

    [SerializeField]
    // 채집 후 다시 활성화될 때까지의 대기 시간(초).
    private float respawnDuration = 30f;

    [Header("채집 완료 후 색상")]
    [SerializeField]
    // 채집 완료 상태를 시각적으로 나타낼 색상.
    private Color depletedColor =
        new Color(0.7f, 0.7f, 0.7f, 1f);

    // 이 자원을 생성하고 재생성 시간을 관리하는 생성 지점.
    private ResourceSpawnPoint spawnPoint;

    // 채집 상태에 따라 색을 바꿀 스프라이트 렌더러.
    private SpriteRenderer spriteRenderer;
    // 재생성 시 복구할 최초 스프라이트 색상.
    private Color originalColor;

    // 현재 자원이 채집된 상태인지 확인
    private bool isDepleted = false;

    // 다른 스크립트가 자원 종류를 읽을 수 있도록 제공한다.
    public ResourceKind Kind => resourceKind;
    // 외부 채집 시스템이 채집 소요 시간을 조회한다.
    public float GatherDuration => gatherDuration;
    // 외부에서 채집 보상 수량을 조회한다.
    public int RewardAmount => rewardAmount;
    // 외부에서 재생성 대기 시간을 조회한다.
    public float RespawnDuration => respawnDuration;
    // 이미 채집되어 사용 불가능한 상태인지 조회한다.
    public bool IsDepleted => isDepleted;

    // 채집 진행률을 표시하는 같은 오브젝트의 UI 컴포넌트.
    private GatheringProgressUI gatheringProgressUI;

    // 이동/채집 스크립트가 진행률 UI에 접근하도록 제공한다.
    public GatheringProgressUI ProgressUI => gatheringProgressUI;

    // 생성 시 렌더러와 진행률 UI를 찾아 참조하고 원래 색상을 저장한다.
    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }

        gatheringProgressUI =
            GetComponent<GatheringProgressUI>();
    }

    // 생성 위치 연결
    // 생성 지점이 자신을 등록하여 채집 완료 후 재생성을 요청할 수 있게 한다.
    public void SetSpawnPoint(ResourceSpawnPoint point)
    {
        spawnPoint = point;
    }

    // 자원을 클릭했을 때 실행
    // 자원을 클릭했을 때 이미 채집된 상태인지 확인하고 로그를 출력한다.
    public void OnResourceClicked()
    {
        if (isDepleted)
        {
            Debug.Log("아직 재생성되지 않은 자원입니다.");
            return;
        }

        Debug.Log("자원 클릭 감지: " + resourceKind);
    }

    // 채집 완료 시 실행
    // 채집이 끝나면 자원 종류를 변환해 보상을 지급하고 재생성 타이머를 시작한다.
    public void CompleteGathering()
    {
        // 이미 채집된 자원은 다시 채집할 수 없음
        if (isDepleted)
            return;

        if (spawnPoint == null)
        {
            Debug.LogWarning(
                "자원에 Spawn Point가 연결되지 않았습니다."
            );
            return;
        }

        // 자원 종류에 따라 지급할 자원 결정
        ResourceType rewardType;

        // ResourceKind 값을 PlayerResources에서 사용하는 ResourceType으로 대응시킨다.
        switch (resourceKind)
        {
            case ResourceKind.Wood:
                rewardType = ResourceType.Wood;
                break;

            case ResourceKind.Stone:
                rewardType = ResourceType.Stone;
                break;

            case ResourceKind.Gold:
                rewardType = ResourceType.Gold;
                break;

            default:
                return;
        }

        // 기존 PlayerResources 컴포넌트 검색
        // 씬에 있는 플레이어 자원 관리 컴포넌트를 검색한다.
        PlayerResources playerResources =
            FindFirstObjectByType<PlayerResources>();

        if (playerResources == null)
        {
            Debug.LogError(
                "PlayerResources를 찾을 수 없습니다."
            );
            return;
        }

        // 자원 지급
        playerResources.AddResource(
            rewardType,
            rewardAmount
        );

        Debug.Log(
            "채집 완료: " +
            resourceKind +
            " +" +
            rewardAmount
        );

        // 채집된 상태로 변경
        // 보상 지급 후 중복 채집을 방지하기 위해 채집 완료 상태로 전환한다.
        SetDepleted(true);

        // 재생성 예약
        spawnPoint.OnResourceGathered(respawnDuration);
    }

    // 채집 상태에 따라 이미지 색상 변경
    // 채집 가능 여부를 저장하고 스프라이트 색을 완료 색상 또는 원래 색상으로 변경한다.
    public void SetDepleted(bool depleted)
    {
        isDepleted = depleted;

        if (spriteRenderer != null)
        {
            spriteRenderer.color =
                depleted ? depletedColor : originalColor;
        }
    }
}
