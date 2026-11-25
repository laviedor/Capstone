using UnityEngine;

// 마을당 건물 개수 제한 (BuildingData.maxPerVillage, 예: 플레이어 집은 마을에 하나만).
//   건설 창의 상세 정보, 건설 시작(카드), 실제 설치(BuildingPlacement)에서 확인하고, 서버도 같은 값으로 다시 검사한다.
//   건물은 내 마을에만 지을 수 있으므로(광장·친구 마을은 건설 불가) 지금 화면에 설치된 건물 수가 곧 내 마을의 개수다.
public static class BuildingLimits
{
    // 지금 마을에 설치된 같은 종류의 건물 수 (미리보기 건물은 세지 않는다)
    public static int CountPlaced(BuildingData data)
    {
        if (data == null)
            return 0;

        int count = 0;

        foreach (Building building in Object.FindObjectsByType<Building>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (building == null || !building.IsPlaced || building.Data == null)
                continue;

            if (building.Data == data ||
                (!string.IsNullOrEmpty(data.buildingID) && building.Data.buildingID == data.buildingID))
            {
                count++;
            }
        }

        return count;
    }

    // 더 지을 수 없는지
    public static bool ReachedLimit(BuildingData data)
    {
        return data != null && data.maxPerVillage > 0 && CountPlaced(data) >= data.maxPerVillage;
    }

    // 제한 안내 문구 (예: "Player House은(는) 마을에 하나만 지을 수 있어요")
    public static string LimitText(BuildingData data)
    {
        if (data == null)
            return "";

        string count = data.maxPerVillage == 1 ? "하나" : data.maxPerVillage + "개";
        return $"{data.buildingName}은(는) 마을에 {count}만 지을 수 있어요.";
    }

    // 제한에 걸리면 알림을 띄우고 true (건설을 시작하거나 설치하기 직전에 부른다)
    public static bool BlockIfReached(BuildingData data)
    {
        if (!ReachedLimit(data))
            return false;

        Toast.Show(LimitText(data));
        return true;
    }
}
