using UnityEngine;
using UnityEngine.UI;

// 플레이어 탭의 캐릭터 이미지 위에 장착한 장비의 착용 이미지를 겹쳐 보여 준다.
public class CharacterPreviewUI : MonoBehaviour
{
    [SerializeField] private Image hatLayer;
    [SerializeField] private Image topLayer;
    [SerializeField] private Image bottomLayer;
    [SerializeField] private Image toolLayer;

    // 장착 상태에 맞춰 각 레이어를 갱신한다. 착용 이미지가 없는 장비는 표시하지 않는다.
    public void Refresh(PlayerEquipment equipment)
    {
        Apply(hatLayer, equipment, EquipmentSlot.Hat);
        Apply(topLayer, equipment, EquipmentSlot.Top);
        Apply(bottomLayer, equipment, EquipmentSlot.Bottom);
        Apply(toolLayer, equipment, EquipmentSlot.Tool);
    }

    private static void Apply(Image layer, PlayerEquipment equipment, EquipmentSlot slot)
    {
        if (layer == null)
            return;

        EquipmentItemData item =
            equipment != null ? equipment.GetEquipped(slot) : null;

        Sprite sprite = item != null ? item.wornSprite : null;

        layer.sprite = sprite;
        layer.enabled = sprite != null;
        layer.preserveAspect = true;
    }
}
