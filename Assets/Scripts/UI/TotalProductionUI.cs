
using UnityEngine;
using TMPro;

public class TotalProductionUI : MonoBehaviour
{
    [Header("Production Texts")]
    [SerializeField] private TMP_Text woodProductionText;
    [SerializeField] private TMP_Text stoneProductionText;
    [SerializeField] private TMP_Text goldProductionText;

    private void OnEnable()
    {
        RefreshProduction();
    }

    public void RefreshProduction()
    {
        float woodPerMinute = 0f;
        float stonePerMinute = 0f;
        float goldPerMinute = 0f;

        // 현재 설치된 건물 검색
        Building[] buildings =
            FindObjectsByType<Building>(
                FindObjectsSortMode.None
            );

        foreach (Building building in buildings)
        {
            // 설치되지 않은 건물 제외
            if (building == null ||
                !building.IsPlaced ||
                building.Data == null)
            {
                continue;
            }

            BuildingData data = building.Data;

            // 생산 정보가 없는 건물 제외
            if (data.productions == null ||
                data.productions.Length == 0)
            {
                continue;
            }

            // 잘못된 생산 주기 제외
            if (data.productionInterval <= 0f)
                continue;

            foreach (ResourceProduction production
                     in data.productions)
            {
                if (production == null ||
                    production.amount <= 0)
                {
                    continue;
                }

                // 분당 생산량 계산
                float amountPerMinute =
                    production.amount *
                    (60f / data.productionInterval);

                switch (production.resourceType)
                {
                    case ResourceType.Wood:
                        woodPerMinute += amountPerMinute;
                        break;

                    case ResourceType.Stone:
                        stonePerMinute += amountPerMinute;
                        break;

                    case ResourceType.Gold:
                        goldPerMinute += amountPerMinute;
                        break;
                }
            }
        }

        // UI 갱신
        if (woodProductionText != null)
        {
            woodProductionText.text = "+" + woodPerMinute.ToString("0.##") + " / 분";
        }

        if (stoneProductionText != null)
        {
            stoneProductionText.text =  "+" + stonePerMinute.ToString("0.##") + " / 분";
        }

        if (goldProductionText != null)
        {
            goldProductionText.text = "+" + goldPerMinute.ToString("0.##") + " / 분";
        }

        Debug.Log("전체 건물 생산 현황 갱신 완료");
    }
}
