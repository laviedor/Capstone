using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 캐릭터 주변의 장비 칸 하나(모자·상의·하의·도구).
public class EquipmentSlotUI : MonoBehaviour
{
    [SerializeField] private EquipmentSlot slot;

    [Header("References")]
    [SerializeField] private Button button;
    [SerializeField] private Image frame;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private GameObject selectedRing;

    [Header("Sprites")]
    [SerializeField] private Sprite filledSprite;
    [SerializeField] private Sprite emptySprite;
    // 비어 있을 때 흐리게 보여 줄 부위 모양
    [SerializeField] private Sprite ghostIcon;

    // 칸을 눌렀을 때 해당 부위와 함께 호출된다.
    public event Action<EquipmentSlot> OnClicked;

    public EquipmentSlot Slot => slot;

    // 클릭 이벤트를 연결하고 부위 이름을 표시한다.
    private void Awake()
    {
        button.onClick.AddListener(() => OnClicked?.Invoke(slot));

        if (labelText != null)
            labelText.text = EquipmentSlotNames.ToKorean(slot);
    }

    // 장착한 장비(없으면 null)와 선택 여부를 표시한다.
    public void Show(EquipmentItemData item, bool selected)
    {
        if (item != null)
        {
            frame.sprite = filledSprite;
            icon.sprite = item.icon != null ? item.icon : ghostIcon;
            icon.color = Color.white;
        }
        else
        {
            frame.sprite = emptySprite;
            icon.sprite = ghostIcon;
            icon.color = new Color(1f, 1f, 1f, 0.4f);
        }

        icon.enabled = icon.sprite != null;

        if (selectedRing != null)
            selectedRing.SetActive(selected && item != null);
    }
}
