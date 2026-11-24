using System.Collections.Generic;
using UnityEngine;

// 장소별 시간제 채집 진행 상태와 완료 보상 지급을 관리한다.
public class GatheringTimer : MonoBehaviour
{
    // 다른 UI에서 접근하는 단일 관리자 인스턴스.
    public static GatheringTimer Instance { get; private set; }

    [Header("Decoration Rewards")]
    [SerializeField]
    // 무작위 데코레이션 보상 후보를 가져오는 데이터베이스.
    private DecorationDatabase decorationDatabase;

    [Header("Player Resources")]
    [SerializeField]
    // 채집 보상 골드를 실제 플레이어 자원에 더할 대상.
    private PlayerResources playerResources;

    // 채집 진행 중 여부와 보상 수령 대기 여부를 구분한다.
    public bool IsGathering { get; private set; }
    public bool IsComplete { get; private set; }

    // 현재 채집 장소 이름과 남은 시간을 UI에 공개한다.
    public string CurrentLocation { get; private set; } = "";
    public float RemainingTime { get; private set; }

    // Time.time 기준 채집 종료 예정 시각.
    private float endTime;

    // 중복 관리자가 생기지 않도록 싱글턴을 초기화한다.
    //   (이미 있으면 이 컴포넌트만 지운다: 오브젝트를 지우면 같은 오브젝트의 채집 UI 패널까지 사라진다)
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // 채집 타이머가 항상 돌아가도록 보장한다.
    //   씬의 타이머는 탐색 창 안의 (꺼져 있는) 채집 패널에 붙어 있어서, 창을 열기 전에는 깨어나지 않고 시간이 가지 않는다.
    //   그래서 그 설정(데코레이션 목록, 플레이어 자원)을 그대로 가진 타이머를 항상 켜져 있는 오브젝트에 따로 만든다.
    //   채집 탭(GatherTabUI)이 시작할 때 부른다.
    public static GatheringTimer Ensure()
    {
        if (Instance != null)
            return Instance;

        GatheringTimer source = null;

        foreach (GatheringTimer timer in FindObjectsByType<GatheringTimer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            source = timer;
            break;
        }

        GameObject holder = new GameObject("GatheringTimer (Always On)");
        GatheringTimer created = holder.AddComponent<GatheringTimer>(); // Awake 에서 Instance 가 된다

        if (source != null)
        {
            created.decorationDatabase = source.decorationDatabase;
            created.playerResources = source.playerResources;
        }

        if (created.playerResources == null)
            created.playerResources = FindFirstObjectByType<PlayerResources>();

        return created;
    }

    // 매 프레임 남은 시간을 계산하고 종료 시 완료 상태로 전환한다.
    private void Update()
    {
        if (!IsGathering)
            return;

        // 음수 시간이 표시되지 않도록 남은 시간을 0 이상으로 제한한다.
        RemainingTime = Mathf.Max(0f, endTime - Time.time);

        if (RemainingTime <= 0f)
        {
            IsGathering = false;
            IsComplete = true;
            RemainingTime = 0f;

            Debug.Log("채집 완료: " + CurrentLocation);
        }
    }

    // 진행 중이거나 미수령 보상이 있으면 새로운 채집을 시작하지 않는다.
    public void StartGathering(string location, float duration)
    {
        if (IsGathering || IsComplete)
            return;

        if (string.IsNullOrEmpty(location) || duration <= 0f)
            return;

        CurrentLocation = location;
        RemainingTime = duration;
        // 게임 실행 시간을 기준으로 채집 종료 시점을 설정한다.
        endTime = Time.time + duration;

        IsGathering = true;
        // 수령을 마친 채집 기록을 비워 다음 채집을 허용한다.
        IsComplete = false;

        Debug.Log($"채집 시작: {location}, {duration}초");
    }

    // 채집 완료 후 골드 및 확률적 데코레이션 보상을 지급한다.
    public void ClaimReward()
    {
        // 완료 전에는 보상을 받을 수 없다.
        if (!IsComplete)
            return;

        if (playerResources == null)
        {
            Debug.LogError(
                "PlayerResources가 연결되지 않았습니다."
            );
            return;
        }

        if (decorationDatabase == null)
        {
            Debug.LogError(
                "DecorationDatabase가 연결되지 않았습니다."
            );
            return;
        }

        if (DecorationInventory.Instance == null)
        {
            Debug.LogError(
                "DecorationInventory가 없습니다."
            );
            return;
        }

        // 보상 계산에 사용할 완료된 채집 장소를 보관한다.
        string location = CurrentLocation;

        // 1. 골드 보상 계산
        int goldReward = GetGoldReward(location);

        // 2. 데코레이션 획득 확률 계산
        float decorationChance =
            GetDecorationChance(location);

        // 0~1 난수와 장소별 확률을 비교하여 당첨 여부를 결정한다.
        bool decorationSuccess =
            Random.value < decorationChance;

        BuildingData decorationReward = null;

        // 당첨되었다면 실제 지급 가능한 데코레이션을 선정한다.
        if (decorationSuccess)
        {
            decorationReward = GetRandomDecoration();

            if (decorationReward == null)
            {
                Debug.LogError(
                    "지급 가능한 데코레이션이 없습니다."
                );
                return;
            }
        }

        // 3. 골드 확정 지급
        if (goldReward > 0)
        {
            playerResources.AddResource(
                ResourceType.Gold,
                goldReward
            );

            Debug.Log(
                $"채집 골드 보상: {location} / " +
                $"{goldReward} G"
            );
        }

        // 4. 데코레이션 보상 지급
        if (decorationSuccess)
        {
            DecorationInventory.Instance.AddDecoration(
                decorationReward,
                1
            );

            Debug.Log(
                $"채집 데코레이션 보상: " +
                $"{decorationReward.buildingName} x1"
            );
        }
        else
        {
            Debug.Log(
                "이번 채집에서는 데코레이션을 획득하지 못했습니다."
            );
        }

        // 5. 채집 상태 초기화
        IsComplete = false;
        CurrentLocation = "";
        RemainingTime = 0f;

        Debug.Log("채집 보상 수령 완료");
    }

    // 장소별 고정 골드 보상: 마당 100, 개울가 350, 들판 1200, 호숫가 12000.
    private int GetGoldReward(string location)
    {
        switch (location)
        {
            case "마당":
                return 100;

            case "개울가":
                return 350;

            case "들판":
                return 1200;

            case "호숫가":
                return 12000;

            default:
                Debug.LogWarning(
                    "알 수 없는 채집 장소: " + location
                );
                return 0;
        }
    }

    // 장소별 데코레이션 획득 확률: 10%, 30%, 60%, 100%.
    private float GetDecorationChance(string location)
    {
        switch (location)
        {
            case "마당":
                return 0.10f;

            case "개울가":
                return 0.30f;

            case "들판":
                return 0.60f;

            case "호숫가":
                return 1.00f;

            default:
                Debug.LogWarning(
                    "알 수 없는 채집 장소: " + location
                );
                return 0f;
        }
    }

    // 유효한 데코레이션 프리팹과 건물 데이터만 후보로 모아 무작위 선택한다.
    private BuildingData GetRandomDecoration()
    {
        if (decorationDatabase == null ||
            decorationDatabase.decorations == null)
            return null;

        // 보상으로 지급할 수 있는 데이터만 담을 임시 목록.
        List<BuildingData> validDecorations =
            new List<BuildingData>();

        foreach (var entry in decorationDatabase.decorations)
        {
            if (entry == null ||
                entry.buildingData == null ||
                entry.buildingPrefab == null)
                continue;

            if (entry.buildingData.buildingType !=
                BuildingType.Decoration)
                continue;

            Building building =
                entry.buildingPrefab.GetComponent<Building>();

            if (building == null ||
                building.Data != entry.buildingData)
                continue;

            validDecorations.Add(entry.buildingData);
        }

        if (validDecorations.Count == 0)
            return null;

        // 유효 후보 중 한 종류를 동일한 확률로 뽑는다.
        int randomIndex =
            Random.Range(0, validDecorations.Count);

        return validDecorations[randomIndex];
    }
}
