using UnityEngine;

// 장비 아이템 한 종류의 정보를 ScriptableObject 에셋으로 저장한다.
[CreateAssetMenu(
    fileName = "NewEquipmentItem",
    menuName = "Equipment/Equipment Item"
)]
public class EquipmentItemData : ScriptableObject
{
    // 아이템 고유 ID
    public string itemID;

    // 화면에 표시할 이름
    public string itemName;

    // 상세 창에 표시할 설명
    [TextArea(2, 4)]
    public string description;

    // 인벤토리와 장비 칸에 표시할 아이콘
    public Sprite icon;

    // 장착 부위
    public EquipmentSlot slot;

    // 플레이어 탭 캐릭터 미리보기에 겹쳐 그릴 착용 이미지 (없으면 표시하지 않음)
    public Sprite wornSprite;
}
