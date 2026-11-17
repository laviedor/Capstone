using UnityEngine;

// Tab 메뉴 디자인의 공통 색과 이름표.
public static class CozyColors
{
    public static readonly Color Ink = Hex("#4A2F1F");
    public static readonly Color Muted = Hex("#6E4E38");
    public static readonly Color Wood = Hex("#7A4F33");
    public static readonly Color Cream = Hex("#FFF8EC");
    public static readonly Color Green = Hex("#3F7A2E");

    // TMP 리치 텍스트용 색 (자원 충분 / 부족)
    public const string OkHex = "#3F7A2E";
    public const string LackHex = "#A2402B";

    // 건물 종류별 배경색
    public static Color TypeTint(BuildingType type)
    {
        switch (type)
        {
            case BuildingType.Production: return Hex("#DCEFC9");
            case BuildingType.Special: return Hex("#FBE0C8");
            case BuildingType.Decoration: return Hex("#F9DCE6");
            default: return Hex("#F3E3C7");
        }
    }

    // 건물 종류의 한국어 이름
    public static string TypeName(BuildingType type)
    {
        switch (type)
        {
            case BuildingType.Production: return "생산";
            case BuildingType.Special: return "특수";
            case BuildingType.Decoration: return "장식";
            default: return type.ToString();
        }
    }

    // 자원 종류의 한국어 이름
    public static string ResourceName(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Wood: return "나무";
            case ResourceType.Stone: return "돌";
            case ResourceType.Gold: return "골드";
            default: return type.ToString();
        }
    }

    public static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color color);
        return color;
    }
}
