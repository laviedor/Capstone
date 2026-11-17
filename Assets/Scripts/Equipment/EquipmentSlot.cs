// 플레이어가 장비를 장착할 수 있는 부위.
public enum EquipmentSlot
{
    // 모자
    Hat,
    // 상의
    Top,
    // 하의
    Bottom,
    // 도구
    Tool
}

// 장비 부위를 화면에 표시할 한국어 이름으로 바꾼다.
public static class EquipmentSlotNames
{
    public static string ToKorean(EquipmentSlot slot)
    {
        switch (slot)
        {
            case EquipmentSlot.Hat: return "모자";
            case EquipmentSlot.Top: return "상의";
            case EquipmentSlot.Bottom: return "하의";
            case EquipmentSlot.Tool: return "도구";
            default: return slot.ToString();
        }
    }
}
