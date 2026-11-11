using UnityEngine;

public class BuildingProduction : MonoBehaviour
{
    private Building building;
    private PlayerResources playerResources;

    private float productionTimer;

    private void Start()
    {
        building = GetComponent<Building>();

        if (building == null)
        {
            Debug.LogError(
                gameObject.name +
                " : Building 컴포넌트가 없습니다."
            );

            enabled = false;
            return;
        }

        if (building.Data == null)
        {
            Debug.LogError(
                gameObject.name +
                " : BuildingData가 없습니다."
            );

            enabled = false;
            return;
        }

        playerResources =
            FindFirstObjectByType<PlayerResources>();

        if (playerResources == null)
        {
            Debug.LogError(
                "PlayerResources를 찾을 수 없습니다."
            );

            enabled = false;
            return;
        }

        productionTimer = 0f;
    }

    private void Update()
    {
        // 실제로 설치된 건물만 생산
        if (building == null ||
            !building.IsPlaced)
        {
            return;
        }

        BuildingData data = building.Data;

        if (data.productions == null ||
            data.productions.Length == 0)
        {
            return;
        }

        if (data.productionInterval <= 0f)
            return;

        productionTimer += Time.deltaTime;

        while (productionTimer >= data.productionInterval)
        {
            ProduceResources();

            productionTimer -=
                data.productionInterval;
        }
    }

    private void ProduceResources()
    {
        BuildingData data = building.Data;

        foreach (ResourceProduction production
                 in data.productions)
        {
            if (production == null)
                continue;

            if (production.amount <= 0)
                continue;

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
