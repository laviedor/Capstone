using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 인벤토리 격자의 칸 하나. 아이템 아이콘, 수량, 장착 표시, 선택 테두리를 그린다.
public class InventorySlotUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button button;
    [SerializeField] private Image frame;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private GameObject equippedBadge;
    [SerializeField] private GameObject selectedRing;

    [Header("Frame Sprites")]
    [SerializeField] private Sprite filledSprite;
    [SerializeField] private Sprite emptySprite;

    // 칸을 눌렀을 때 칸 번호와 함께 호출된다.
    public event Action<int> OnClicked;

    private int index;

    // 칸 번호를 정하고 클릭 이벤트를 연결한다.
    public void Init(int slotIndex)
    {
        index = slotIndex;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => OnClicked?.Invoke(index));
    }

    // 아이템이 들어 있는 칸으로 표시한다.
    public void Show(Sprite itemIcon, int count, bool equipped, bool selected)
    {
        frame.sprite = filledSprite;
        button.interactable = true;

        icon.sprite = itemIcon;
        icon.enabled = itemIcon != null;

        if (countText != null)
        {
            countText.gameObject.SetActive(count > 1);
            countText.text = "×" + count;
        }

        if (equippedBadge != null)
            equippedBadge.SetActive(equipped);

        if (selectedRing != null)
            selectedRing.SetActive(selected);
    }

    // 빈 칸으로 표시한다.
    public void ShowEmpty()
    {
        frame.sprite = emptySprite;
        button.interactable = false;
        icon.enabled = false;

        if (countText != null)
            countText.gameObject.SetActive(false);

        if (equippedBadge != null)
            equippedBadge.SetActive(false);

        if (selectedRing != null)
            selectedRing.SetActive(false);
    }
}
