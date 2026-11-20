using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 집(입구·출구)을 클릭하면 마우스 옆에 작게 뜨는 '이동' 버튼.
//   Tab 메뉴와 같은 모양(초록 둥근 버튼, 크림색 글자)이며, 누르면 그 집으로 이동한다
//   씬에 따로 놓지 않아도 처음 쓸 때 스스로 만들어진다. 다른 곳을 누르거나 Esc, Tab 메뉴, 건설 모드를 켜면 사라진다
public class HouseMovePopup : MonoBehaviour
{
    // 버튼 모양 (Assets/Resources/CozyUI 의 스프라이트, Tab 메뉴 디자인에서 만든 것)
    private const string ButtonSpritePath = "CozyUI/btn_primary";

    // 캔버스 기준 해상도: 게임 창(1920 × 270)과 같게 맞춰 다른 UI와 크기가 같아 보이게 한다
    private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 270f);

    private static readonly Vector2 ButtonSize = new Vector2(54f, 24f);
    private const float MouseGap = 12f;      // 마우스 커서와 버튼 사이 간격
    private const float ScreenMargin = 4f;   // 화면 가장자리와의 최소 간격
    private const float PopSeconds = 0.08f;  // 나타날 때 커지는 시간

    private static readonly Color GreenFallback = new Color32(0x3F, 0x7A, 0x2E, 0xFF);

    private static HouseMovePopup instance;

    private RectTransform canvasRect;
    private RectTransform buttonRect;
    private TMP_Text label;
    private HouseDoor target;
    private float shownAt;

    private InventoryUI inventoryUI;
    private BuildingModeManager buildingModeManager;

    // 문 옆(마우스 위치)에 이동 버튼을 띄운다
    public static void Show(HouseDoor door, Vector2 screenPosition)
    {
        if (door == null)
            return;

        if (instance == null)
            instance = Create();

        instance.Open(door, screenPosition);
    }

    // 떠 있는 이동 버튼을 닫는다
    public static void Hide()
    {
        if (instance != null)
            instance.Close();
    }

    private static HouseMovePopup Create()
    {
        GameObject root = new GameObject("HouseMovePopup", typeof(RectTransform));
        HouseMovePopup popup = root.AddComponent<HouseMovePopup>();
        popup.Build();
        return popup;
    }

    // 캔버스, 버튼, 글자를 만든다
    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();
        canvasRect = (RectTransform)transform;

        GameObject buttonObject = new GameObject("MoveButton", typeof(RectTransform));
        buttonObject.transform.SetParent(transform, false);
        buttonRect = (RectTransform)buttonObject.transform;
        buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = ButtonSize;

        Image background = buttonObject.AddComponent<Image>();
        Sprite sprite = Resources.Load<Sprite>(ButtonSpritePath);

        if (sprite != null)
        {
            background.sprite = sprite;
            background.type = Image.Type.Sliced;
            background.color = Color.white;
        }
        else
        {
            background.color = GreenFallback;
        }

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = background;
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.highlightedColor = new Color(0.92f, 1f, 0.9f);
        colors.pressedColor = new Color(0.8f, 0.88f, 0.78f);
        colors.selectedColor = Color.white;
        button.colors = colors;
        button.onClick.AddListener(OnClick);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = (RectTransform)labelObject.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(2f, sprite != null ? 3f : 0f); // 버튼 아래 그림자만큼 글자를 올린다
        labelRect.offsetMax = new Vector2(-2f, 0f);

        TextMeshProUGUI text = labelObject.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset font = CozyRuntimeUI.Font();

        if (font != null)
        {
            text.font = font;
            text.fontSharedMaterial = font.material;
        }

        text.text = "이동";
        text.fontSize = 12f;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color32(0xFF, 0xF8, 0xEC, 0xFF);
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        label = text;

        buttonObject.SetActive(false);
    }

    private void Open(HouseDoor door, Vector2 screenPosition)
    {
        target = door;
        label.text = door.ActionLabel;
        Place(screenPosition);

        buttonRect.gameObject.SetActive(true);
        buttonRect.localScale = Vector3.one * 0.85f;
        shownAt = Time.unscaledTime;
    }

    private void Close()
    {
        target = null;

        if (buttonRect != null)
            buttonRect.gameObject.SetActive(false);
    }

    // 마우스 오른쪽에 둔다. 화면 오른쪽에 자리가 없으면 왼쪽에 둔다
    private void Place(Vector2 screenPosition)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenPosition, null, out Vector2 local);

        Vector2 half = canvasRect.rect.size / 2f;
        bool fitsRight = local.x + MouseGap + ButtonSize.x <= half.x - ScreenMargin;

        buttonRect.pivot = new Vector2(fitsRight ? 0f : 1f, 0.5f);

        float x = fitsRight ? local.x + MouseGap : local.x - MouseGap;
        float y = Mathf.Clamp(local.y,
            -half.y + ButtonSize.y / 2f + ScreenMargin,
            half.y - ButtonSize.y / 2f - ScreenMargin);

        buttonRect.anchoredPosition = new Vector2(x, y);
    }

    private void Update()
    {
        if (target == null)
            return;

        // 나타날 때 살짝 커지는 효과
        float t = Mathf.Clamp01((Time.unscaledTime - shownAt) / PopSeconds);
        buttonRect.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, t);

        if (!target.isActiveAndEnabled || !target.CanUse)
        {
            Close();
            return;
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Close();
            return;
        }

        if (inventoryUI == null)
            inventoryUI = FindFirstObjectByType<InventoryUI>();

        if (buildingModeManager == null)
            buildingModeManager = FindFirstObjectByType<BuildingModeManager>();

        if ((inventoryUI != null && inventoryUI.IsOpen) ||
            (buildingModeManager != null && buildingModeManager.IsBuildMode))
        {
            Close();
            return;
        }

        // 버튼이 아닌 곳을 누르면 닫는다 (집을 다시 누른 경우에는 MousePathfinding 이 다시 띄운다)
        if (Mouse.current != null &&
            (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame) &&
            !RectTransformUtility.RectangleContainsScreenPoint(buttonRect, Mouse.current.position.ReadValue(), null))
        {
            Close();
        }
    }

    private void OnClick()
    {
        HouseDoor door = target;
        Close();

        if (door != null && door.CanUse)
            door.Use();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}
