using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 실행 중에 코드로 만드는 작은 UI(철거 확인창, 건설·철거 안내 바, 집 이동 버튼)를 Tab 메뉴와 같은 모양으로 만드는 도구.
//   스프라이트는 Assets/Resources/CozyUI 에 있는 것(Tab 메뉴 디자인에서 만든 것)을 쓰고,
//   글꼴은 Tab 메뉴 한글 글꼴(CozyKorean SDF)을 찾아 쓴다.
public static class CozyRuntimeUI
{
    // 게임 창(1920 × 270)과 같은 기준 해상도: 다른 UI와 크기가 같아 보이게 한다
    public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 270f);

    public static readonly Color Ink = new Color32(0x4A, 0x2F, 0x1F, 0xFF);
    public static readonly Color Muted = new Color32(0x6E, 0x4E, 0x38, 0xFF);
    public static readonly Color Wood = new Color32(0x7A, 0x4F, 0x33, 0xFF);
    public static readonly Color Cream = new Color32(0xFF, 0xF8, 0xEC, 0xFF);
    public static readonly Color Danger = new Color32(0xA2, 0x40, 0x2B, 0xFF);

    private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    private static TMP_FontAsset font;

    // 화면 위에 뜨는 캔버스를 만든다 (sortingOrder 가 클수록 위)
    public static RectTransform CreateCanvas(string name, int sortingOrder)
    {
        GameObject root = new GameObject(name, typeof(RectTransform));

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        root.AddComponent<GraphicRaycaster>();
        return (RectTransform)root.transform;
    }

    // Resources/CozyUI 의 스프라이트 (없으면 null)
    public static Sprite Sprite(string name)
    {
        if (sprites.TryGetValue(name, out Sprite cached) && cached != null)
            return cached;

        Sprite sprite = Resources.Load<Sprite>("CozyUI/" + name);
        sprites[name] = sprite;
        return sprite;
    }

    public static RectTransform Rect(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    // 둥근 패널 이미지. 스프라이트가 없으면 fallback 색으로 칠한다
    public static Image Image(Transform parent, string name, string spriteName, Color fallback, bool raycast = false)
    {
        RectTransform rt = Rect(parent, name);
        Image image = rt.gameObject.AddComponent<Image>();
        Sprite sprite = string.IsNullOrEmpty(spriteName) ? null : Sprite(spriteName);

        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.color = Color.white;
        }
        else
        {
            image.color = fallback;
        }

        image.raycastTarget = raycast;
        return image;
    }

    public static TMP_Text Text(Transform parent, string name, string text, float size, Color color,
        TextAlignmentOptions align, bool bold, bool wrap = false)
    {
        RectTransform rt = Rect(parent, name);
        TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset f = Font();

        if (f != null)
        {
            tmp.font = f;
            tmp.fontSharedMaterial = f.material;
        }

        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        tmp.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
        return tmp;
    }

    // 버튼 (spriteName: btn_primary / btn_secondary / btn_danger). 버튼 그림 아래 그림자만큼 글자를 올린다
    public static Button Button(Transform parent, string name, string spriteName, string label,
        Color labelColor, float labelSize, Action onClick, Color fallback)
    {
        Image background = Image(parent, name, spriteName, fallback, true);
        Button button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;

        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.highlightedColor = new Color(1f, 0.96f, 0.9f);
        colors.pressedColor = new Color(0.88f, 0.84f, 0.78f);
        colors.selectedColor = Color.white;
        button.colors = colors;

        if (onClick != null)
            button.onClick.AddListener(() => onClick());

        TMP_Text text = Text(background.transform, "Label", label, labelSize, labelColor, TextAlignmentOptions.Center, true);
        Stretch((RectTransform)text.transform, 2f, 0f, 2f, background.sprite != null ? 2f : 0f);
        return button;
    }

    // 부모의 왼쪽 위 기준으로 배치 (y 는 아래로 증가)
    public static void TopLeft(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    // 부모를 가득 채우고 안쪽 여백을 준다
    public static void Stretch(RectTransform rt, float left, float top, float right, float bottom)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    // 한글을 그릴 수 있는 TMP 글꼴: Tab 메뉴 글꼴(CozyKorean SDF) > 한글이 되는 다른 글꼴 > Resources 의 D2Coding 으로 만든 글꼴
    public static TMP_FontAsset Font()
    {
        if (font != null)
            return font;

        TMP_FontAsset fallback = null;

        foreach (TMP_FontAsset asset in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            if (asset == null)
                continue;

            if (asset.name == "CozyKorean SDF")
                return font = asset;

            if (fallback == null && asset.HasCharacters("철거이동", out uint[] _, true, true))
                fallback = asset;
        }

        if (fallback != null)
            return font = fallback;

        Font source = Resources.Load<Font>("Fonts/D2CodingBold");
        font = source != null ? TMP_FontAsset.CreateFontAsset(source) : null;
        return font;
    }
}
