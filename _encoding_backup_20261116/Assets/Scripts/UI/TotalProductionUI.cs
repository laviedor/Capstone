
using UnityEngine;
using TMPro;

// 씬에 설치된 건물들의 자원별 분당 생산량을 합산해 텍스트로 표시한다.
public class TotalProductionUI : MonoBehaviour
{
    // Inspector에서 연결할 나무·돌·골드 생산량 표시 텍스트.
    [Header("Production Texts")]
    // 나무의 분당 총생산량 표시 영역.
    [SerializeField] private TMP_Text woodProductionText;
    // 돌의 분당 총생산량 표시 영역.
    [SerializeField] private TMP_Text stoneProductionText;
    // 골드의 분당 총생산량 표시 영역.
    [SerializeField] private TMP_Text goldProductionText;

    // UI가 활성화될 때 현재 설치된 건물들을 기준으로 생산량을 다시 계산한다.
    private void OnEnable()
    {
        RefreshProduction();
    }

    // 전체 건물의 생산 정보를 조회하고 자원 종류별 분당 생산량을 합산한다.
    public void RefreshProduction()
    {
        // 이번 갱신에 사용할 자원별 합계를 0부터 계산한다.
        float woodPerMinute = 0f;
        float stonePerMinute = 0f;
        float goldPerMinute = 0f;

        // 현재 설치된 건물 검색
        // 현재 씬에서 Building 컴포넌트를 가진 오브젝트들을 찾는다.
        Building[] buildings =
            FindObjectsByType<Building>(
                FindObjectsSortMode.None
            );

        // 검색한 건물마다 설치 상태와 생산 데이터를 검사한다.
        foreach (Building building in buildings)
        {
            // 설치되지 않은 건물 제외
            if (building == null ||
                !building.IsPlaced ||
                building.Data == null)
            {
                continue;
            }

            // 건물에 연결된 ScriptableObject 데이터에서 생산 설정을 가져온다.
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

            // 한 건물이 여러 종류의 자원을 생산할 수 있으므로 생산 항목을 순회한다.
            foreach (ResourceProduction production
                     in data.productions)
            {
                if (production == null ||
                    production.amount <= 0)
                {
                    continue;
                }

                // 분당 생산량 계산
                // 한 번에 생산하는 양에 1분 동안의 생산 횟수(60 / 생산 간격)를 곱한다.
                float amountPerMinute =
                    production.amount *
                    (60f / data.productionInterval);

                // 계산된 생산량을 해당 자원 종류의 총합에 더한다.
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
        // UI 텍스트가 연결된 경우에만 결과를 출력한다.
        if (woodProductionText != null)
        {
            // 소수점 둘째 자리까지 필요한 만큼만 표시한다.
            woodProductionText.text = "+" + woodPerMinute.ToString("0.##") + " / 분";
        }

        if (stoneProductionText != null)
        {
            stoneProductionText.text = "+" + stonePerMinute.ToString("0.##") + " / 분";
        }

        if (goldProductionText != null)
        {
            goldProductionText.text = "+" + goldPerMinute.ToString("0.##") + " / 분";
        }

        // 갱신 작업이 끝났음을 Unity 콘솔에 기록한다.
        Debug.Log("전체 건물 생산 현황 갱신 완료");
    }
}
