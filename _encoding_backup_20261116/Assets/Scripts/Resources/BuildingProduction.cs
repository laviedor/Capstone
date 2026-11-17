using UnityEngine;

public class BuildingProduction : MonoBehaviour
{
    // 같은 게임 오브젝트의 Building 컴포넌트를 보관한다.
    private Building building;
    // 생산한 자원을 지급할 플레이어 자원 관리 컴포넌트.
    private PlayerResources playerResources;

    // 마지막 생산 이후 누적된 시간(초).
    private float productionTimer;

    // 초기화 단계에서 건물 데이터와 자원 관리자를 찾아 연결한다.
    private void Start()
    {
        // 이 오브젝트에 붙은 Building 컴포넌트를 가져온다.
        building = GetComponent<Building>();

        if (building == null)
        {
            Debug.LogError(
                gameObject.name +
                " : Building 컴포넌트가 없습니다."
            );

            // 필수 참조가 없으면 생산 로직이 더 이상 실행되지 않도록 비활성화한다.
            enabled = false;
            return;
        }

        if (building.Data == null)
        {
            Debug.LogError(
                gameObject.name +
                " : BuildingData가 없습니다."
            );

            // 필수 참조가 없으면 생산 로직이 더 이상 실행되지 않도록 비활성화한다.
            enabled = false;
            return;
        }

        // 씬에서 PlayerResources 컴포넌트를 찾아 생산 결과의 지급 대상을 설정한다.
        playerResources =
            FindFirstObjectByType<PlayerResources>();

        if (playerResources == null)
        {
            Debug.LogError(
                "PlayerResources를 찾을 수 없습니다."
            );

            // 필수 참조가 없으면 생산 로직이 더 이상 실행되지 않도록 비활성화한다.
            enabled = false;
            return;
        }

        // 처음 시작할 때 누적 생산 시간을 0으로 초기화한다.
        productionTimer = 0f;
    }

    // 매 프레임 건물 설치 여부와 생산 설정을 확인하고 생산 시간을 누적한다.
    private void Update()
    {
        // 실제로 설치된 건물만 생산
        // 실제 설치 완료된 건물이 아니면 생산하지 않는다.
        if (building == null ||
            !building.IsPlaced)
        {
            return;
        }

        // 이 건물에 연결된 생산 정보와 생산 주기를 읽는다.
        BuildingData data = building.Data;

        // 생산할 자원 목록이 비어 있으면 생산을 건너뛴다.
        if (data.productions == null ||
            data.productions.Length == 0)
        {
            return;
        }

        // 생산 간격이 0 이하이면 반복 생산을 방지하기 위해 종료한다.
        if (data.productionInterval <= 0f)
            return;

        // 이번 프레임에 경과한 시간을 누적한다.
        productionTimer += Time.deltaTime;

        // 생산 주기가 여러 번 경과했다면 경과 횟수만큼 생산한다.
        while (productionTimer >= data.productionInterval)
        {
            ProduceResources();

            // 한 번 생산한 주기만큼 시간을 차감하고 남은 시간을 유지한다.
            productionTimer -=
                data.productionInterval;
        }
    }

    // BuildingData의 생산 목록을 순회하며 유효한 자원을 플레이어에게 지급한다.
    private void ProduceResources()
    {
        // 이 건물에 연결된 생산 정보와 생산 주기를 읽는다.
        BuildingData data = building.Data;

        // 설정된 자원 종류와 생산량을 하나씩 처리한다.
        foreach (ResourceProduction production
                 in data.productions)
        {
            // 비어 있는 생산 항목은 무시한다.
            if (production == null)
                continue;

            // 생산량이 0 이하인 항목은 지급하지 않는다.
            if (production.amount <= 0)
                continue;

            // 플레이어의 해당 자원 보유량을 생산량만큼 증가시킨다.
            playerResources.AddResource(
                production.resourceType,
                production.amount
            );

            Debug.Log(
                data.buildingName +
                " 생산 : " +
                production.resourceType +
                " +" +
                production.amount
            );
        }
    }
}
