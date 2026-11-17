using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 탭·칩 버튼의 선택/비선택 모양(배경 스프라이트, 색, 크기, 글자색)을 바꾼다.
public class TabButtonVisual : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text label;
    [SerializeField] private RectTransform rect;

    [Header("Selected")]
    [SerializeField] private Sprite activeSprite;
    [SerializeField] private Color activeColor = Color.white;
    [SerializeField] private Color activeTextColor = Color.white;
    [SerializeField] private Vector2 activeSize;
    [SerializeField] private Vector2 activePosition;

    [Header("Not Selected")]
    [SerializeField] private Sprite inactiveSprite;
    [SerializeField] private Color inactiveColor = Color.white;
    [SerializeField] private Color inactiveTextColor = Color.white;
    [SerializeField] private Vector2 inactiveSize;
    [SerializeField] private Vector2 inactivePosition;

    // true면 선택 여부에 따라 위치도 바꾼다 (선택된 탭이 창 테두리를 덮도록 살짝 내려온다)
    [SerializeField] private bool changePosition;

    // 선택 여부에 맞게 모양을 적용한다. 크기가 0이면 크기는 바꾸지 않는다.
    // 배경은 끄지 않고 투명하게만 만들어 버튼 클릭은 계속 받을 수 있게 한다.
    public void SetActive(bool selected)
    {
        if (background != null)
        {
            Sprite sprite = selected ? activeSprite : inactiveSprite;
            background.sprite = sprite;
            background.color = selected ? activeColor : inactiveColor;
        }

        if (label != null)
            label.color = selected ? activeTextColor : inactiveTextColor;

        Vector2 size = selected ? activeSize : inactiveSize;

        if (rect != null && size != Vector2.zero)
            rect.sizeDelta = size;

        if (rect != null && changePosition)
            rect.anchoredPosition = selected ? activePosition : inactivePosition;
    }
}
