using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 화면 오른쪽 아래의 작은 둥근 아이콘 버튼 세 개. 누르면 해당 창이 바로 열린다 (마우스로만 조작)
//   [가방]          인벤토리 창
//   [망치+곡괭이]    건설 창 (건물 짓기, 철거)
//   [돋보기+잎사귀]  탐색 창 (필드 이동, 채집)
//   열려 있는 창의 아이콘은 주황 테두리로 표시되고, 같은 아이콘을 다시 누르면 창이 닫힌다.
//   마우스를 올리면 버튼이 살짝 커지고 위에 이름표가 뜬다.
//   InventoryUI 가 시작할 때 스스로 만든다 (씬에 따로 놓지 않아도 됨). 로그인한 뒤에만 보인다.
public class QuickMenuHUD : MonoBehaviour
{
    private const float ButtonSize = 42f;
    private const float IconSize = 26f;
    private const float Gap = 8f;
    private const float RightMargin = 14f;
    private const float BottomMargin = 12f;
    private const float HoverScale = 1.08f;

    private struct Entry
    {
        public InventoryUI.Window window;
        public string icon;
        public string tooltip;
    }

    private static readonly Entry[] Entries =
    {
        new Entry { window = InventoryUI.Window.Inventory, icon = "icon_bag", tooltip = "인벤토리" },
        new Entry { window = InventoryUI.Window.Building, icon = "icon_build", tooltip = "건설 · 철거" },
        new Entry { window = InventoryUI.Window.Field, icon = "icon_explore", tooltip = "탐색 · 채집" },
    };

    private static QuickMenuHUD instance;

    private InventoryUI inventoryUI;
    private BuildingModeManager buildingModeManager;

    private RectTransform group;
    private readonly RectTransform[] buttons = new RectTransform[Entries.Length];
    private readonly Image[] backgrounds = new Image[Entries.Length];
    private RectTransform tooltip;
    private TMP_Text tooltipText;

    private Sprite normalSprite;
    private Sprite activeSprite;

    // InventoryUI 가 Awake 에서 부른다
    public static void Ensure(InventoryUI inventory)
    {
        if (instance != null)
        {
            instance.inventoryUI = inventory;
            return;
        }

        RectTransform canvas = CozyRuntimeUI.CreateCanvas("QuickMenuHUD", 240);
        instance = canvas.gameObject.AddComponent<QuickMenuHUD>();
        instance.inventoryUI = inventory;
        instance.Build(canvas);
    }

    private void Build(RectTransform canvas)
    {
        normalSprite = CozyRuntimeUI.Sprite("btn_round");
        activeSprite = CozyRuntimeUI.Sprite("btn_round_on");

        float width = Entries.Length * ButtonSize + (Entries.Length - 1) * Gap;

        group = CozyRuntimeUI.Rect(canvas, "Buttons");
        group.anchorMin = group.anchorMax = new Vector2(1f, 0f);
        group.pivot = new Vector2(1f, 0f);
        group.anchoredPosition = new Vector2(-RightMargin, BottomMargin);
        group.sizeDelta = new Vector2(width, ButtonSize);

        for (int i = 0; i < Entries.Length; i++)
        {
            Entry entry = Entries[i];

            RectTransform rect = CozyRuntimeUI.Rect(group, "Button_" + entry.window);
            CozyRuntimeUI.TopLeft(rect, i * (ButtonSize + Gap), 0f, ButtonSize, ButtonSize);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(i * (ButtonSize + Gap) + ButtonSize / 2f, -ButtonSize / 2f);

            Image background = rect.gameObject.AddComponent<Image>();
            background.sprite = normalSprite;
            background.color = normalSprite != null ? Color.white : CozyRuntimeUI.Cream;
            background.preserveAspect = true;

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.88f, 0.84f, 0.78f);
            colors.selectedColor = Color.white;
            button.colors = colors;

            InventoryUI.Window window = entry.window;
            button.onClick.AddListener(() => OnButton(window));

            RectTransform iconRect = CozyRuntimeUI.Rect(rect, "Icon");
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(IconSize, IconSize);
            iconRect.anchoredPosition = new Vector2(0f, 1.5f); // 버튼 아래 그림자만큼 위로
            Image icon = iconRect.gameObject.AddComponent<Image>();
            icon.sprite = CozyRuntimeUI.Sprite(entry.icon);
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            buttons[i] = rect;
            backgrounds[i] = background;
        }

        // 마우스를 올린 버튼 위에 뜨는 이름표
        Image tip = CozyRuntimeUI.Image(canvas, "Tooltip", "pill_cream", CozyRuntimeUI.Cream);
        tooltip = tip.rectTransform;
        tooltip.anchorMin = tooltip.anchorMax = new Vector2(1f, 0f);
        tooltip.pivot = new Vector2(0.5f, 0f);
        tooltip.sizeDelta = new Vector2(74f, 20f);
        tooltipText = CozyRuntimeUI.Text(tooltip, "Label", "", 10.5f, CozyRuntimeUI.Ink,
            TextAlignmentOptions.Center, true);
        CozyRuntimeUI.Stretch(tooltipText.rectTransform, 2f, 0f, 2f, 0f);
        tooltip.gameObject.SetActive(false);

        if (inventoryUI != null)
            inventoryUI.Changed += RefreshActive;

        RefreshActive();
    }

    private void OnDestroy()
    {
        if (inventoryUI != null)
            inventoryUI.Changed -= RefreshActive;

        if (instance == this)
            instance = null;
    }

    private void OnButton(InventoryUI.Window window)
    {
        if (inventoryUI == null)
            return;

        // 건설·철거 중이었으면 그 모드를 끝내고 창을 연다
        if (buildingModeManager == null)
            buildingModeManager = FindFirstObjectByType<BuildingModeManager>();

        if (buildingModeManager != null && buildingModeManager.IsBuildMode)
            buildingModeManager.ExitBuildMode();

        HouseMovePopup.Hide();
        inventoryUI.Toggle(window);
    }

    // 열려 있는 창의 아이콘을 주황 테두리로 표시한다
    private void RefreshActive()
    {
        for (int i = 0; i < Entries.Length; i++)
        {
            bool active = inventoryUI != null && inventoryUI.IsOpen && inventoryUI.CurrentWindow == Entries[i].window;
            Sprite sprite = active && activeSprite != null ? activeSprite : normalSprite;

            if (sprite != null)
                backgrounds[i].sprite = sprite;
        }
    }

    private void Update()
    {
        // 로그인하기 전(로그인 화면)에는 숨긴다
        bool visible = !string.IsNullOrEmpty(LoginUI.Username) && inventoryUI != null;

        if (group.gameObject.activeSelf != visible)
            group.gameObject.SetActive(visible);

        if (!visible)
        {
            tooltip.gameObject.SetActive(false);
            return;
        }

        // 마우스를 올린 버튼: 살짝 키우고 위에 이름표를 띄운다
        int hovered = -1;

        if (Mouse.current != null)
        {
            Vector2 mouse = Mouse.current.position.ReadValue();

            for (int i = 0; i < buttons.Length; i++)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(buttons[i], mouse, null))
                {
                    hovered = i;
                    break;
                }
            }
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            float target = i == hovered ? HoverScale : 1f;
            float scale = Mathf.MoveTowards(buttons[i].localScale.x, target, Time.unscaledDeltaTime * 1.5f);
            buttons[i].localScale = new Vector3(scale, scale, 1f);
        }

        if (hovered < 0)
        {
            tooltip.gameObject.SetActive(false);
            return;
        }

        float buttonCenterFromRight = RightMargin + (Entries.Length - 1 - hovered) * (ButtonSize + Gap) + ButtonSize / 2f;
        tooltip.anchoredPosition = new Vector2(-buttonCenterFromRight, BottomMargin + ButtonSize + 6f);
        tooltipText.text = Entries[hovered].tooltip;
        tooltip.gameObject.SetActive(true);
    }
}
