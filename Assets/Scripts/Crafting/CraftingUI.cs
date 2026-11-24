using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 대장간 제작 창. 마을의 대장간(blacksmith) 건물을 클릭하면 뜬다 (MousePathfinding 이 연다).
//   ┌ 대장간 · 제작 ───────────────────────────── [×] ┐
//   │ [삽 그림]  삽                        나무 40   [만들기] │
//   │            땅을 파서 바닥을 바꿀 수 있어요   돌 20          │
//   └─────────────────────────────────────────┘
//   [만들기] 를 누르면 서버에 제작을 요청하고, 서버가 자원을 쓰고 도구를 넣어 준다 (ResourceSync → ToolInventory)
//   닫기: [×], 우클릭, 창 밖 클릭, 인벤토리·건설 창 열기
//   처음 열 때 스스로 만들어진다 (씬에 따로 놓지 않아도 됨)
public class CraftingUI : MonoBehaviour
{
    private static readonly Vector2 WindowSize = new Vector2(470f, 128f);
    private const float RowHeight = 76f;

    private static readonly Color Green = new Color32(0x3F, 0x7A, 0x2E, 0xFF);
    private static readonly Color Lack = new Color32(0xC0, 0x4A, 0x32, 0xFF);

    private static CraftingUI instance;

    private class Row
    {
        public CraftingRecipes.Recipe recipe;
        public TMP_Text cost;
        public Button button;
        public Image buttonImage;
        public TMP_Text buttonText;
    }

    private RectTransform window;
    private readonly List<Row> rows = new List<Row>();
    private PlayerResources resources;
    private ChatClient chatClient;
    private InventoryUI inventoryUI;
    private float openedAt;
    private string pending; // 서버의 답을 기다리는 제작 (중복 요청 방지)
    private float pendingSince;

    public static bool IsOpen => instance != null && instance.window != null && instance.window.gameObject.activeSelf;

    public static void Open()
    {
        if (instance == null)
        {
            RectTransform canvas = CozyRuntimeUI.CreateCanvas("CraftingUI", 240);
            instance = canvas.gameObject.AddComponent<CraftingUI>();
            instance.Build(canvas);
        }

        instance.Show();
    }

    public static void Close()
    {
        if (IsOpen)
            instance.window.gameObject.SetActive(false);
    }

    private void Build(RectTransform canvas)
    {
        float height = 44f + RowHeight * CraftingRecipes.All.Count + 10f;

        Image background = CozyRuntimeUI.Image(canvas, "Window", "window", CozyRuntimeUI.Cream, true);
        window = background.rectTransform;
        window.anchorMin = window.anchorMax = new Vector2(0.5f, 0.5f);
        window.pivot = new Vector2(0.5f, 0.5f);
        window.sizeDelta = new Vector2(WindowSize.x, height);

        // 제목
        Image titleChip = CozyRuntimeUI.Image(window, "Title", "pill_cream", CozyRuntimeUI.Cream);
        CozyRuntimeUI.TopLeft(titleChip.rectTransform, 14f, 10f, 120f, 24f);
        TMP_Text title = CozyRuntimeUI.Text(titleChip.transform, "Label", "대장간 · 제작", 13f, CozyRuntimeUI.Ink,
            TextAlignmentOptions.Center, true);
        CozyRuntimeUI.Stretch(title.rectTransform, 4f, 0f, 4f, 0f);

        // 닫기
        Button close = CozyRuntimeUI.Button(window, "Close", "btn_secondary", "×", CozyRuntimeUI.Wood, 16f, Close,
            CozyRuntimeUI.Cream);
        RectTransform closeRect = (RectTransform)close.transform;
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-12f, -10f);
        closeRect.sizeDelta = new Vector2(28f, 24f);

        for (int i = 0; i < CraftingRecipes.All.Count; i++)
            rows.Add(CreateRow(window, CraftingRecipes.All[i], 42f + i * RowHeight));
    }

    private Row CreateRow(RectTransform parent, CraftingRecipes.Recipe recipe, float top)
    {
        Row row = new Row { recipe = recipe };

        Image card = CozyRuntimeUI.Image(parent, "Recipe_" + recipe.id, "card", CozyRuntimeUI.Cream);
        RectTransform c = card.rectTransform;
        c.anchorMin = new Vector2(0f, 1f);
        c.anchorMax = new Vector2(1f, 1f);
        c.pivot = new Vector2(0.5f, 1f);
        c.offsetMin = new Vector2(12f, -top - RowHeight + 6f);
        c.offsetMax = new Vector2(-12f, -top);

        // 도구 그림
        Image iconBox = CozyRuntimeUI.Image(c, "IconBox", "slot", new Color32(0xF3, 0xE3, 0xC8, 0xFF));
        CozyRuntimeUI.TopLeft(iconBox.rectTransform, 8f, 7f, 56f, 56f);
        RectTransform iconRect = CozyRuntimeUI.Rect(iconBox.transform, "Icon");
        CozyRuntimeUI.Stretch(iconRect, 8f, 8f, 8f, 8f);
        Image icon = iconRect.gameObject.AddComponent<Image>();
        icon.sprite = recipe.Icon;
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        // 이름, 설명
        TMP_Text name = CozyRuntimeUI.Text(c, "Name", recipe.name, 14f, CozyRuntimeUI.Ink, TextAlignmentOptions.MidlineLeft, true);
        CozyRuntimeUI.TopLeft(name.rectTransform, 74f, 8f, 200f, 20f);
        TMP_Text description = CozyRuntimeUI.Text(c, "Description", recipe.description, 10.5f, CozyRuntimeUI.Muted,
            TextAlignmentOptions.TopLeft, false);
        description.textWrappingMode = TextWrappingModes.Normal;
        CozyRuntimeUI.TopLeft(description.rectTransform, 74f, 30f, 210f, 34f);

        // 필요한 자원 (가진 양이 모자라면 붉게)
        row.cost = CozyRuntimeUI.Text(c, "Cost", "", 11f, CozyRuntimeUI.Ink, TextAlignmentOptions.MidlineLeft, true);
        row.cost.richText = true;
        row.cost.textWrappingMode = TextWrappingModes.Normal;
        RectTransform costRect = row.cost.rectTransform;
        costRect.anchorMin = costRect.anchorMax = new Vector2(1f, 0.5f);
        costRect.pivot = new Vector2(1f, 0.5f);
        costRect.anchoredPosition = new Vector2(-98f, 0f);
        costRect.sizeDelta = new Vector2(86f, 44f);

        // 만들기 버튼
        row.button = CozyRuntimeUI.Button(c, "CraftButton", "btn_primary", "만들기", Color.white, 12.5f,
            () => OnCraft(row), Green);
        row.buttonImage = row.button.GetComponent<Image>();
        row.buttonText = row.button.GetComponentInChildren<TMP_Text>();
        RectTransform buttonRect = (RectTransform)row.button.transform;
        buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(1f, 0.5f);
        buttonRect.pivot = new Vector2(1f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(-8f, 0f);
        buttonRect.sizeDelta = new Vector2(84f, 32f);

        return row;
    }

    private void Show()
    {
        if (resources == null)
            resources = FindFirstObjectByType<PlayerResources>(FindObjectsInactive.Include);

        if (chatClient == null)
            chatClient = FindFirstObjectByType<ChatClient>(FindObjectsInactive.Include);

        if (inventoryUI == null)
            inventoryUI = FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);

        // 인벤토리·건설 창과 겹치지 않게 닫는다
        if (inventoryUI != null && inventoryUI.IsOpen)
            inventoryUI.CloseInventory();

        window.gameObject.SetActive(true);
        openedAt = Time.unscaledTime;
        Refresh();
    }

    private void OnEnable()
    {
        ToolInventory.Changed += OnToolsChanged;
    }

    private void OnDisable()
    {
        ToolInventory.Changed -= OnToolsChanged;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void OnToolsChanged()
    {
        pending = null;

        if (IsOpen)
            Refresh();
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        // 서버 답이 오지 않으면 알려 주고 다시 누를 수 있게 (서버가 제작을 모르는 옛 버전이면 아무 답도 없다)
        if (pending != null && Time.unscaledTime - pendingSince > 3f)
        {
            pending = null;
            Toast.Show("서버에서 답이 없어요. 서버가 최신 버전(제작 기능 포함)으로 실행 중인지 확인해 주세요.");
        }

        Refresh();

        if (Mouse.current == null || Time.unscaledTime - openedAt < 0.15f)
            return;

        // 우클릭, 또는 창 밖을 누르면 닫는다
        bool outside = Mouse.current.leftButton.wasPressedThisFrame &&
                       !RectTransformUtility.RectangleContainsScreenPoint(window, Mouse.current.position.ReadValue(), null);

        if (Mouse.current.rightButton.wasPressedThisFrame || outside)
            Close();

        // 인벤토리·건설 창이 열리면 닫는다
        if (inventoryUI != null && inventoryUI.IsOpen)
            Close();
    }

    private void Refresh()
    {
        foreach (Row row in rows)
        {
            CraftingRecipes.Recipe recipe = row.recipe;
            bool enough = true;
            List<string> parts = new List<string>();

            foreach (ResourceAmount cost in recipe.cost)
            {
                int have = resources != null ? resources.GetAmount(cost.resourceType) : 0;
                bool ok = have >= cost.amount;
                enough &= ok;
                string color = ok ? "#4A2F1F" : "#C04A32";
                parts.Add($"{ResourceName(cost.resourceType)} <color={color}>{cost.amount}</color> <size=85%><color=#8A6F5A>({have})</color></size>");
            }

            string costText = string.Join("\n", parts);

            if (row.cost.text != costText)
                row.cost.text = costText;

            int owned = ToolInventory.Instance != null ? ToolInventory.Instance.GetCount(recipe.id) : 0;

            if (owned >= recipe.max)
                SetButton(row, "가지고 있어요", false);
            else if (pending == recipe.id)
                SetButton(row, "만드는 중…", false);
            else if (!enough)
                SetButton(row, "자원 부족", false);
            else
                SetButton(row, "만들기", true);
        }
    }

    private static void SetButton(Row row, string text, bool enabled)
    {
        if (row.buttonText.text == text && row.button.interactable == enabled)
            return;

        row.buttonText.text = text;
        row.button.interactable = enabled;
        Sprite sprite = CozyRuntimeUI.Sprite(enabled ? "btn_primary" : "btn_secondary");

        if (sprite != null)
            row.buttonImage.sprite = sprite;

        row.buttonText.color = enabled ? Color.white : CozyRuntimeUI.Muted;
        row.buttonText.fontSize = enabled ? 12.5f : 10.5f;
    }

    private void OnCraft(Row row)
    {
        if (pending != null)
            return;

        if (chatClient == null || !chatClient.SendCraft(row.recipe.id))
        {
            Toast.Show("서버에 연결되어 있지 않아 만들 수 없어요.");
            return;
        }

        pending = row.recipe.id;
        pendingSince = Time.unscaledTime;
        Refresh();
    }

    private static string ResourceName(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Wood: return "나무";
            case ResourceType.Stone: return "돌";
            default: return "골드";
        }
    }
}
