using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 마우스로 잡아 끌 때 가로 방향을 반대로 움직이는 스크롤.
//   보통 스크롤은 마우스를 오른쪽으로 끌면 내용도 오른쪽으로 따라온다.
//   이 스크롤은 반대로, 오른쪽으로 끌면 내용이 왼쪽으로 지나간다 (스크롤바 손잡이를 끄는 것과 같은 방향).
//   마우스 휠은 그대로다.
//   씬에 이미 있는 ScrollRect 를 Replace 로 바꿔 끼운다 (설정은 그대로 옮긴다).
public class ReversedDragScrollRect : ScrollRect
{
    // 끌기를 시작한 화면 위치. 끄는 동안 이 점을 기준으로 마우스 위치를 좌우로 뒤집어 넘긴다
    private Vector2 dragOrigin;

    public override void OnBeginDrag(PointerEventData eventData)
    {
        dragOrigin = eventData.position;
        base.OnBeginDrag(eventData);
    }

    public override void OnDrag(PointerEventData eventData)
    {
        Vector2 real = eventData.position;
        eventData.position = new Vector2(dragOrigin.x * 2f - real.x, real.y);
        base.OnDrag(eventData);
        eventData.position = real;
    }

    // 일반 ScrollRect 를 이 스크롤로 바꾼다. 이미 바뀌어 있으면 그대로 돌려준다
    public static ScrollRect Replace(ScrollRect source)
    {
        if (source == null || source is ReversedDragScrollRect)
            return source;

        GameObject go = source.gameObject;

        RectTransform content = source.content;
        RectTransform viewport = source.viewport;
        bool horizontal = source.horizontal;
        bool vertical = source.vertical;
        MovementType movementType = source.movementType;
        float elasticity = source.elasticity;
        bool inertia = source.inertia;
        float decelerationRate = source.decelerationRate;
        float scrollSensitivity = source.scrollSensitivity;
        Scrollbar horizontalBar = source.horizontalScrollbar;
        Scrollbar verticalBar = source.verticalScrollbar;
        ScrollbarVisibility horizontalVisibility = source.horizontalScrollbarVisibility;
        ScrollbarVisibility verticalVisibility = source.verticalScrollbarVisibility;
        float horizontalSpacing = source.horizontalScrollbarSpacing;
        float verticalSpacing = source.verticalScrollbarSpacing;
        Vector2 position = source.normalizedPosition;

        DestroyImmediate(source);

        ReversedDragScrollRect scroll = go.AddComponent<ReversedDragScrollRect>();
        scroll.content = content;
        scroll.viewport = viewport;
        scroll.horizontal = horizontal;
        scroll.vertical = vertical;
        scroll.movementType = movementType;
        scroll.elasticity = elasticity;
        scroll.inertia = inertia;
        scroll.decelerationRate = decelerationRate;
        scroll.scrollSensitivity = scrollSensitivity;
        scroll.horizontalScrollbar = horizontalBar;
        scroll.verticalScrollbar = verticalBar;
        scroll.horizontalScrollbarVisibility = horizontalVisibility;
        scroll.verticalScrollbarVisibility = verticalVisibility;
        scroll.horizontalScrollbarSpacing = horizontalSpacing;
        scroll.verticalScrollbarSpacing = verticalSpacing;
        scroll.normalizedPosition = position;
        return scroll;
    }
}
