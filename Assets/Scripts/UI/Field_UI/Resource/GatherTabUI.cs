using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 탐색 창의 [채집] 탭: 플레이어가 조작하지 않아도 시간이 지나면 골드와 장식 아이템을 받는 자동 채집.
//   탐색 창 위쪽에 [탐색] [채집] 두 이름표가 생기고, [채집] 을 누르면 채집 장소 카드 네 장이 보인다.
//     ┌ 마당 ┐ ┌ 개울가 ┐ ┌ 들판 ┐ ┌ 호숫가 ┐   장소 그림, 이름, 걸리는 시간, 보상(골드, 장식 아이템 확률)
//     [채집 시작] → 진행 막대와 남은 시간 → [보상 받기]
//   한 번에 한 곳만 채집한다. 필드에 있지 않아도 어디서나 시작할 수 있다 (탐색과 별개).
//   채집이 끝나면 알림이 뜨고, [채집] 이름표에 느낌표가 붙는다.
//   시간 계산과 보상은 GatheringTimer 와 서버(GatherSync → 서버 expeditions.json)가 한다. 이 스크립트는 화면만 맡는다.
//   InventoryUI 가 시작할 때 스스로 만든다 (씬에 따로 놓지 않아도 됨).
public class GatherTabUI : MonoBehaviour
{
    // 채집 장소. 이름·시간·보상은 서버 data/expeditions.json, GatheringTimer 의 보상 표와 같아야 한다
    private struct Area
    {
        public string name;
        public string icon;
        public float seconds;
        public int gold;
        public float decorationChance;
        public Color tint;

        public Area(string name, string icon, float seconds, int gold, float chance, string tintHex)
        {
            this.name = name;
            this.icon = icon;
            this.seconds = seconds;
            this.gold = gold;
            decorationChance = chance;
            ColorUtility.TryParseHtmlString(tintHex, out tint);
        }
    }

    private static readonly Area[] Areas =
    {
        new Area("마당", "area_yard", 10f, 100, 0.1f, "#FBE0C8"),
        new Area("개울가", "area_stream", 10f, 350, 0.3f, "#D6ECF6"),
        new Area("들판", "area_field", 10f, 1200, 0.6f, "#E9F2C9"),
        new Area("호숫가", "area_lake", 10f, 12000, 1f, "#CFE9EE"),
    };

    private static readonly Color Green = new Color32(0x7F, 0xB5, 0x61, 0xFF);
    private static readonly Color BarBack = new Color32(0xEF, 0xDC, 0xBB, 0xFF);
    private static readonly Color Coral = new Color32(0xE0, 0x7A, 0x5F, 0xFF);

    // 카드 하나의 화면 요소
    private class Card
    {
        public Area area;
        public Image statusChip;
        public TMP_Text statusText;
        public Button button;
        public Image buttonImage;
        public TMP_Text buttonText;
        public RectTransform barRoot;
        public RectTransform barFill;
        public TMP_Text remainingText;
    }

    private static GatherTabUI instance;

    private InventoryUI inventoryUI;
    private GameObject fieldPanel;
    private Button exploreTab;
    private Button gatherTab;
    private TabButtonVisual exploreVisual;
    private TabButtonVisual gatherVisual;
    private GameObject badge;
    private GameObject panel;
    private readonly Card[] cards = new Card[Areas.Length];

    private bool showGather;
    private bool wasFieldOpen;
    private bool wasComplete;

    // InventoryUI 가 Awake 에서 부른다
    public static void Ensure(InventoryUI inventory, GameObject fieldPanel, Button fieldTabButton)
    {
        if (instance != null || inventory == null || fieldPanel == null || fieldTabButton == null)
            return;

        GameObject holder = new GameObject("GatherTab");
        instance = holder.AddComponent<GatherTabUI>();
        instance.Setup(inventory, fieldPanel, fieldTabButton);
    }

    private void Setup(InventoryUI inventory, GameObject field, Button fieldTabButton)
    {
        inventoryUI = inventory;
        fieldPanel = field;

        GatheringTimer.Ensure();
        HideOldGatheringUI();
        CreateTabs(fieldTabButton);
        CreatePanel();

        inventoryUI.Changed += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (inventoryUI != null)
            inventoryUI.Changed -= Refresh;

        if (instance == this)
            instance = null;
    }

    // 예전에는 필드 안에서만 [채집하기] 버튼으로 열던 채집 패널을 숨긴다 (이제 [채집] 탭에서 한다)
    private void HideOldGatheringUI()
    {
        GatheringUI old = fieldPanel.GetComponentInChildren<GatheringUI>(true);

        if (old != null)
            old.enabled = false;

        foreach (string name in new[] { "GatheringButton", "GatheringPanel" })
        {
            Transform t = FindDeep(fieldPanel.transform, name);

            if (t != null)
                t.gameObject.SetActive(false);
        }
    }

    // ----- 탭 ([탐색] [채집]) -----

    private void CreateTabs(Button fieldTabButton)
    {
        exploreTab = fieldTabButton;
        exploreVisual = fieldTabButton.GetComponent<TabButtonVisual>();
        exploreTab.onClick.AddListener(() => Select(false));

        GameObject clone = Instantiate(fieldTabButton.gameObject, fieldTabButton.transform.parent);
        clone.name = "GatherTabButton";
        gatherTab = clone.GetComponent<Button>();
        gatherTab.onClick.RemoveAllListeners();
        gatherTab.onClick.AddListener(() => Select(true));
        gatherVisual = clone.GetComponent<TabButtonVisual>();

        TMP_Text label = clone.GetComponentInChildren<TMP_Text>(true);

        if (label != null)
            label.text = "채집";

        Transform glyph = clone.transform.Find("Cozy_TabIcon/Glyph");
        Sprite icon = CozyRuntimeUI.Sprite("icon_gather");

        if (glyph != null && icon != null && glyph.GetComponent<Image>() is Image image)
            image.sprite = icon;

        // 보상을 받을 수 있으면 이름표 오른쪽 위에 느낌표
        Image dot = CozyRuntimeUI.Image(clone.transform, "Badge", "rounded_white", Coral);
        dot.color = Coral;
        RectTransform dotRect = dot.rectTransform;
        dotRect.anchorMin = dotRect.anchorMax = new Vector2(1f, 1f);
        dotRect.pivot = new Vector2(0.5f, 0.5f);
        dotRect.anchoredPosition = new Vector2(-6f, -5f);
        dotRect.sizeDelta = new Vector2(15f, 15f);
        TMP_Text mark = CozyRuntimeUI.Text(dot.transform, "Mark", "!", 11f, Color.white, TextAlignmentOptions.Center, true);
        CozyRuntimeUI.Stretch(mark.rectTransform, 0f, 0f, 0f, 0f);
        badge = dot.gameObject;
        badge.SetActive(false);

        clone.SetActive(false);
    }

    private void Select(bool gather)
    {
        showGather = gather;
        Refresh();
    }

    // 창 상태에 맞춰 두 이름표와 내용을 보여 준다
    private void Refresh()
    {
        bool fieldOpen = inventoryUI != null && inventoryUI.IsOpen &&
                         inventoryUI.CurrentWindow == InventoryUI.Window.Field;

        // 탐색 창을 새로 열었을 때 받을 보상이 있으면 [채집] 탭부터 보여 준다
        if (fieldOpen && !wasFieldOpen && IsRewardReady())
            showGather = true;

        wasFieldOpen = fieldOpen;

        gatherTab.gameObject.SetActive(fieldOpen);

        if (!fieldOpen)
        {
            panel.SetActive(false);
            return;
        }

        PlaceTab(exploreTab, exploreVisual, !showGather, 0f);
        PlaceTab(gatherTab, gatherVisual, showGather, 102f);

        fieldPanel.SetActive(!showGather);
        panel.SetActive(showGather);
        RefreshCards();
    }

    private static void PlaceTab(Button tab, TabButtonVisual visual, bool selected, float x)
    {
        tab.gameObject.SetActive(true);
        tab.interactable = true;

        if (visual != null)
            visual.SetActive(selected);

        RectTransform rect = (RectTransform)tab.transform;
        rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y);

        // 선택된 이름표가 창 테두리를 덮도록 앞에 그린다
        if (selected)
            tab.transform.SetAsLastSibling();
    }

    // ----- 채집 장소 카드 -----

    private void CreatePanel()
    {
        RectTransform root = CozyRuntimeUI.Rect(fieldPanel.transform.parent, "Cozy_GatherPanel");
        CozyRuntimeUI.Stretch(root, 0f, 0f, 0f, 0f);
        panel = root.gameObject;

        HorizontalLayoutGroup layout = panel.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        for (int i = 0; i < Areas.Length; i++)
            cards[i] = CreateCard(root, Areas[i]);

        panel.SetActive(false);
    }

    private Card CreateCard(Transform parent, Area area)
    {
        Card card = new Card { area = area };

        Image background = CozyRuntimeUI.Image(parent, "Card_" + area.name, "card", CozyRuntimeUI.Cream);
        RectTransform c = background.rectTransform;

        // 장소 그림
        Image art = CozyRuntimeUI.Image(c, "Art", "rounded_white", area.tint);
        art.color = area.tint;
        Row(art.rectTransform, 6f, 6f, 6f, 46f);

        RectTransform iconRect = CozyRuntimeUI.Rect(art.transform, "Icon");
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(38f, 38f);
        Image icon = iconRect.gameObject.AddComponent<Image>();
        icon.sprite = CozyRuntimeUI.Sprite(area.icon);
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        // 진행 상태 표시 (진행 중 / 완료!)
        card.statusChip = CozyRuntimeUI.Image(art.transform, "Status", "pill_cream", CozyRuntimeUI.Cream);
        RectTransform chip = card.statusChip.rectTransform;
        chip.anchorMin = chip.anchorMax = new Vector2(1f, 1f);
        chip.pivot = new Vector2(1f, 1f);
        chip.anchoredPosition = new Vector2(-4f, -4f);
        chip.sizeDelta = new Vector2(50f, 16f);
        card.statusText = CozyRuntimeUI.Text(chip, "Label", "", 9.5f, CozyRuntimeUI.Ink, TextAlignmentOptions.Center, true);
        CozyRuntimeUI.Stretch(card.statusText.rectTransform, 2f, 0f, 2f, 0f);

        // 이름, 걸리는 시간
        TMP_Text title = CozyRuntimeUI.Text(c, "Name", area.name, 13.5f, CozyRuntimeUI.Ink, TextAlignmentOptions.MidlineLeft, true);
        Row(title.rectTransform, 10f, 10f, 55f, 18f);

        TMP_Text duration = CozyRuntimeUI.Text(c, "Duration", "걸리는 시간 " + FormatDuration(area.seconds), 10.5f,
            CozyRuntimeUI.Muted, TextAlignmentOptions.MidlineLeft, false);
        Row(duration.rectTransform, 10f, 10f, 73f, 14f);

        // 보상: 골드, 장식 아이템 확률
        RectTransform goldRow = CozyRuntimeUI.Rect(c, "GoldReward");
        Row(goldRow, 10f, 10f, 89f, 15f);
        RectTransform coinRect = CozyRuntimeUI.Rect(goldRow, "Coin");
        CozyRuntimeUI.TopLeft(coinRect, 0f, 0.5f, 14f, 14f);
        Image coin = coinRect.gameObject.AddComponent<Image>();
        coin.sprite = CozyRuntimeUI.Sprite("icon_coin");
        coin.preserveAspect = true;
        coin.raycastTarget = false;
        TMP_Text gold = CozyRuntimeUI.Text(goldRow, "Text", "골드 +" + area.gold.ToString("N0"), 11f, CozyRuntimeUI.Ink,
            TextAlignmentOptions.MidlineLeft, true);
        CozyRuntimeUI.Stretch(gold.rectTransform, 18f, 0f, 0f, 0f);

        TMP_Text item = CozyRuntimeUI.Text(c, "ItemReward",
            "장식 아이템 " + Mathf.RoundToInt(area.decorationChance * 100f) + "% 확률", 10.5f,
            CozyRuntimeUI.Muted, TextAlignmentOptions.MidlineLeft, false);
        Row(item.rectTransform, 10f, 10f, 105f, 15f);

        // 버튼 (채집 시작 / 보상 받기)
        card.button = CozyRuntimeUI.Button(c, "Button", "btn_primary", "채집 시작", Color.white, 12f,
            () => OnCardButton(area), new Color32(0x3F, 0x7A, 0x2E, 0xFF));
        card.buttonImage = card.button.GetComponent<Image>();
        card.buttonText = card.button.GetComponentInChildren<TMP_Text>();
        Row((RectTransform)card.button.transform, 8f, 8f, 126f, 30f);

        // 진행 막대와 남은 시간 (채집 중일 때만)
        Image barBack = CozyRuntimeUI.Image(c, "Progress", "rounded_white", BarBack);
        barBack.color = BarBack;
        card.barRoot = barBack.rectTransform;
        Row(card.barRoot, 10f, 10f, 128f, 9f);

        Image fill = CozyRuntimeUI.Image(barBack.transform, "Fill", "rounded_white", Green);
        fill.color = Green;
        card.barFill = fill.rectTransform;
        card.barFill.anchorMin = Vector2.zero;
        card.barFill.anchorMax = new Vector2(0f, 1f);
        card.barFill.pivot = new Vector2(0f, 0.5f);
        card.barFill.offsetMin = Vector2.zero;
        card.barFill.offsetMax = Vector2.zero;

        card.remainingText = CozyRuntimeUI.Text(c, "Remaining", "", 10.5f, CozyRuntimeUI.Ink,
            TextAlignmentOptions.Center, true);
        Row(card.remainingText.rectTransform, 10f, 10f, 141f, 16f);

        return card;
    }

    private void OnCardButton(Area area)
    {
        GatheringTimer timer = GatheringTimer.Ensure();

        if (timer.IsComplete)
        {
            if (timer.CurrentLocation == area.name)
                timer.ClaimReward();

            RefreshCards();
            return;
        }

        if (timer.IsGathering)
            return;

        timer.StartGathering(area.name, area.seconds);
        RefreshCards();
    }

    private void Update()
    {
        GatheringTimer timer = GatheringTimer.Instance;
        bool complete = timer != null && timer.IsComplete;

        // 채집이 끝난 순간: 알림
        if (complete && !wasComplete)
            Toast.Show($"{timer.CurrentLocation} 채집이 끝났어요! 탐색 창의 [채집] 탭에서 보상을 받으세요.");

        wasComplete = complete;

        // 보상을 받을 수 있는데 [채집] 탭을 보고 있지 않으면 느낌표
        bool viewing = panel != null && panel.activeInHierarchy;

        if (badge != null)
            badge.SetActive(complete && !viewing);

        if (viewing)
            RefreshCards();
    }

    private void RefreshCards()
    {
        GatheringTimer timer = GatheringTimer.Instance;

        foreach (Card card in cards)
        {
            if (card == null)
                continue;

            bool gathering = timer != null && timer.IsGathering;
            bool complete = timer != null && timer.IsComplete;
            bool here = timer != null && timer.CurrentLocation == card.area.name;

            bool showBar = gathering && here;
            card.barRoot.gameObject.SetActive(showBar);
            card.remainingText.gameObject.SetActive(showBar);
            card.button.gameObject.SetActive(!showBar);

            if (showBar)
            {
                float progress = 1f - Mathf.Clamp01(timer.RemainingTime / Mathf.Max(card.area.seconds, 0.01f));
                card.barFill.anchorMax = new Vector2(progress, 1f);
                card.remainingText.text = "남은 시간 " + FormatClock(timer.RemainingTime);
            }

            if (gathering && here)
                SetStatus(card, "진행 중", CozyRuntimeUI.Ink);
            else if (complete && here)
                SetStatus(card, "완료!", Coral);
            else
                SetStatus(card, null, CozyRuntimeUI.Ink);

            if (complete && here)
                SetButton(card, "보상 받기", true);
            else if (complete)
                SetButton(card, "보상을 먼저 받아요", false);
            else if (gathering)
                SetButton(card, "다른 곳에서 채집 중", false);
            else
                SetButton(card, "채집 시작", true);
        }
    }

    private static void SetStatus(Card card, string text, Color color)
    {
        card.statusChip.gameObject.SetActive(text != null);

        if (text != null)
        {
            card.statusText.text = text;
            card.statusText.color = color;
        }
    }

    private static void SetButton(Card card, string text, bool enabled)
    {
        card.buttonText.text = text;
        card.button.interactable = enabled;

        Sprite sprite = CozyRuntimeUI.Sprite(enabled ? "btn_primary" : "btn_secondary");

        if (sprite != null)
            card.buttonImage.sprite = sprite;

        card.buttonText.color = enabled ? Color.white : CozyRuntimeUI.Muted;
        card.buttonText.fontSize = enabled ? 12f : 10.5f;
    }

    private bool IsRewardReady()
    {
        return GatheringTimer.Instance != null && GatheringTimer.Instance.IsComplete;
    }

    // ----- 도구 -----

    // 부모 가로를 채우는 한 줄 (위에서 top 만큼 내려와 height 높이)
    private static void Row(RectTransform rt, float left, float right, float top, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(left, -top - height);
        rt.offsetMax = new Vector2(-right, -top);
    }

    private static string FormatDuration(float seconds)
    {
        if (seconds < 60f)
            return Mathf.RoundToInt(seconds) + "초";

        if (seconds < 3600f)
            return Mathf.RoundToInt(seconds / 60f) + "분";

        float hours = seconds / 3600f;
        return (Mathf.Approximately(hours, Mathf.Round(hours)) ? Mathf.RoundToInt(hours).ToString() : hours.ToString("0.#")) + "시간";
    }

    private static string FormatClock(float seconds)
    {
        int total = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        int h = total / 3600;
        int m = total % 3600 / 60;
        int s = total % 60;
        return h > 0 ? $"{h:00}:{m:00}:{s:00}" : $"{m:00}:{s:00}";
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name)
            return root;

        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, name);

            if (found != null)
                return found;
        }

        return null;
    }
}
