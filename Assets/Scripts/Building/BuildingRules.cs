// 지금 있는 곳에 이 건물을 지을 수 있는지 (장소별 건물 종류 규칙).
//   마을      : 모든 건물
//   집 안     : 장식(Decoration)과 특수(Special) 건물만. 생산(Production) 건물은 지을 수 없다
//              집 안 전용(BuildingData.houseOnly: 침대·난로 같은 실내 가구)은 집 안에만 놓을 수 있다
//   필드      : 짓지 않는다
//   건설 창의 상세 정보·건설 시작(카드)·실제 설치(BuildingPlacement)에서 모두 이 규칙으로 확인한다.
//   (광장·친구 마을의 건설 금지는 WorldSync, 마을당 개수 제한은 BuildingLimits 가 따로 확인한다)
public static class BuildingRules
{
    public const string HouseOnlyMessage = "집 안에는 장식과 특수 건물만 놓을 수 있어요.";
    public const string FieldMessage = "필드에서는 건물을 지을 수 없어요.";
    public const string HouseOnlyItemMessage = "이 장식은 집 안에만 놓을 수 있어요.";

    public static bool InHouse =>
        HouseSceneManager.Instance != null && HouseSceneManager.Instance.IsInsideHouse;

    public static bool InField =>
        FieldSceneManager.Instance != null && FieldSceneManager.Instance.IsInField;

    // 이 건물을 지금 있는 곳에 지을 수 있는지. 못 지으면 reason 에 안내 문구
    public static bool IsAllowedHere(BuildingData data, out string reason)
    {
        reason = "";

        if (data == null)
            return false;

        if (InField)
        {
            reason = FieldMessage;
            return false;
        }

        if (InHouse && !IsInteriorType(data.buildingType))
        {
            reason = HouseOnlyMessage;
            return false;
        }

        if (!InHouse && data.houseOnly)
        {
            reason = HouseOnlyItemMessage;
            return false;
        }

        return true;
    }

    // 집 안에 놓을 수 있는 종류인지
    public static bool IsInteriorType(BuildingType type)
    {
        return type == BuildingType.Decoration || type == BuildingType.Special;
    }

    // 지을 수 없으면 알림을 띄우고 true (건설을 시작하거나 설치하기 직전에 부른다)
    public static bool BlockIfNotAllowed(BuildingData data)
    {
        if (IsAllowedHere(data, out string reason))
            return false;

        if (!string.IsNullOrEmpty(reason))
            Toast.Show(reason);

        return true;
    }
}
