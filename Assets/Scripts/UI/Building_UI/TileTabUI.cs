using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 건설 창의 [타일] 탭: 삽으로 마을 바닥을 바꾼다.
//   건설 창 위쪽에 [건설] [타일] 두 이름표가 생긴다. [타일] 을 누르면
//     ┌ 삽 안내 ┐ ┌ 잔디 ┐ ┌ 흙 ┐    바닥 종류 카드: 그림, 이름, 설명, [칠하기]
//   [칠하기] 를 누르면 창이 닫히고 바닥 바꾸기 모드(TilePainter)가 켜진다: 칸을 클릭해 바꾸고, 우클릭으로 끝낸다
//   삽이 없으면(대장간에서 만들기 전) 탭은 보이지만 카드가 잠겨 있고 안내만 보인다
//   InventoryUI 가 시작할 때 스스로 만든다 (씬에 따로 놓지 않아도 됨)
public class TileTabUI : MonoBehaviour
{
    private struct TileKind
    {
        public string id;
        public string name;
        public string description;
        public string card;

        public TileKind(string id, string name, string description, string card)
        {
            this.id = id;
            this.name = name;
            this.description = description;
            this.card = card;
        }
    }

    private static readonly TileKind[] Kinds =
    {
        new TileKind(GroundTileLayer.Dirt, "흙", "잔디를 파내 흙길이나 밭을 만들어요. 이웃한 칸끼리 자연스럽게 이어져요.", "card_dirt"),
        new TileKind(GroundTileLayer.Grass, "잔디", "바꾼 칸을 원래의 잔디로 되돌려요.", "card_grass"),
    };

    private static readonly Color Green = new Color32(0x3F, 0x7A, 0x2E, 0xFF);

    private class Card
    {
        public TileKind kind;
        public Button button;
        public Image buttonImage;
        public TMP_Text buttonText;
        public Image art;
    }

    private static TileTabUI instance;

    private InventoryUI inventoryUI;
    private GameObject buildingPanel;
    private Button buildTab;
    private Button tileTab;
    private TabButtonVisual buildVisual;
    private TabButtonVisual tileVisual;
    private GameObject panel;
    private TMP_Text infoTitle;
    private TMP_Text infoText;
    private Image infoIcon;
    private readonly List<Card> cards = new List<Card>();
    private bool showTiles;

    // InventoryUI 가 Awake 에서 부른다
    public static void Ensure(InventoryUI inventory, GameObject buildingPanel, Button buildingTabButton)
    {
        if (instance != null || inventory == null || buildingPanel == null || buildingTabButton == null)
            return;

        GroundTileLayer.Ensure();
        ToolInventory.Ensure();

        GameObject holder = new GameObject("TileTab");
        instance = holder.AddComponent<TileTabUI>();
        instance.Setup(inventory, buildingPanel, buildingTabButton);
    }

    private void Setup(InventoryUI inventory, GameObject building, Button buildingTabButton)
    {
        inventoryUI = inventory;
        buildingPanel = building;

        CreateTabs(buildingTabButton);
        CreatePanel();

        inventoryUI.Changed += Refresh;
        ToolInventory.Changed += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (inventoryUI != null)
            inventoryUI.Changed -= Refresh;

        ToolInventory.Changed -= Refresh;

        if (instance == this)
            instance = null;
    }

    // ----- 탭 ([건설] [타일]) -----

    private void CreateTabs(Button buildingTabButton)
    {
        buildTab = buildingTabButton;
        buildVisual = buildingTabButton.GetComponent<TabButtonVisual>();
        buildTab.onClick.AddListener(() => Select(false));

        GameObject clone = Instantiate(buildingTabButton.gameObject, buildingTabButton.transform.parent);
        clone.name = "TileTabButton";
        tileTab = clone.GetComponent<Button>();
        tileTab.onClick.RemoveAllListeners();
        tileTab.onClick.AddListener(() => Select(true));
        tileVisual = clone.GetComponent<TabButtonVisual>();

        TMP_Text label = clone.GetComponentInChildren<TMP_Text>(true);

        if (label != null)
            label.text = "타일";

        Transform glyph = clone.transform.Find("Cozy_TabIcon/Glyph");
        Sprite icon = Resources.Load<Sprite>("Crafting/shovel_icon");

        if (glyph != null && icon != null && glyph.GetComponent<Image>() is Image image)
        {
            image.sprite = icon;
            image.preserveAspect = true;
        }

        clone.SetActive(false);
    }

    private void Select(bool tiles)
    {
        showTiles = tiles;
        Refresh();
    }

    // 창 상태에 맞춰 두 이름표와 내용을 보여 준다
    private void Refresh()
    {
        bool buildingOpen = inventoryUI != null && inventoryUI.IsOpen &&
                            inventoryUI.CurrentWindow == InventoryUI.Window.Building;

        tileTab.gameObject.SetActive(buildingOpen);

        if (!buildingOpen)
        {
            panel.SetActive(false);
            return;
        }

        PlaceTab(buildTab, buildVisual, !showTiles, 0f);
        PlaceTab(tileTab, tileVisual, showTiles, 102f);

        buildingPanel.SetActive(!showTiles);
        panel.SetActive(showTiles);
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

        if (selected)
            tab.transform.SetAsLastSibling();
    }

    // ----- 내용 -----

    private void CreatePanel()
    {
        RectTransform root = CozyRuntimeUI.Rect(buildingPanel.transform.parent, "Cozy_TilePanel");
        CozyRuntimeUI.Stretch(root, 0f, 0f, 0f, 0f);
        panel = root.gameObject;

        HorizontalLayoutGroup layout = panel.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        CreateInfoCard(root);

        foreach (TileKind kind in Kinds)
            cards.Add(CreateCard(root, kind));

        panel.SetActive(false);
    }

    // 왼쪽 안내 카드: 삽 그림과 쓰는 법 (삽이 없으면 만드는 곳 안내)
    private void CreateInfoCard(Transform parent)
    {
        Image background = CozyRuntimeUI.Image(parent, "Info", "card", CozyRuntimeUI.Cream);
        LayoutElement element = background.gameObject.AddComponent<LayoutElement>();
        element.flexibleWidth = 1.5f;
        RectTransform c = background.rectTransform;

        Image iconBox = CozyRuntimeUI.Image(c, "IconBox", "slot", new Color32(0xF3, 0xE3, 0xC8, 0xFF));
        CozyRuntimeUI.TopLeft(iconBox.rectTransform, 10f, 10f, 52f, 52f);
        RectTransform iconRect = CozyRuntimeUI.Rect(iconBox.transform, "Icon");
        CozyRuntimeUI.Stretch(iconRect, 7f, 7f, 7f, 7f);
        infoIcon = iconRect.gameObject.AddComponent<Image>();
        infoIcon.sprite = Resources.Load<Sprite>("Crafting/shovel_icon");
        infoIcon.preserveAspect = true;
        infoIcon.raycastTarget = false;

        infoTitle = CozyRuntimeUI.Text(c, "Title", "", 13.5f, CozyRuntimeUI.Ink, TextAlignmentOptions.MidlineLeft, true);
        Row(infoTitle.rectTransform, 72f, 10f, 14f, 20f);

        infoText = CozyRuntimeUI.Text(c, "Text", "", 10.5f, CozyRuntimeUI.Muted, TextAlignmentOptions.TopLeft, false);
        infoText.textWrappingMode = TextWrappingModes.Normal;
        infoText.richText = true;
        Row(infoText.rectTransform, 12f, 12f, 72f, 84f);
    }

    private Card CreateCard(Transform parent, TileKind kind)
    {
        Card card = new Card { kind = kind };

        Image background = CozyRuntimeUI.Image(parent, "Tile_" + kind.id, "card", CozyRuntimeUI.Cream);
        LayoutElement element = background.gameObject.AddComponent<LayoutElement>();
        element.flexibleWidth = 1f;
        RectTransform c = background.rectTransform;

        // 바닥 그림
        Image frame = CozyRuntimeUI.Image(c, "ArtFrame", "slot", new Color32(0xF3, 0xE3, 0xC8, 0xFF));
        Row(frame.rectTransform, 8f, 8f, 8f, 56f);
        RectTransform artRect = CozyRuntimeUI.Rect(frame.transform, "Art");
        CozyRuntimeUI.Stretch(artRect, 5f, 5f, 5f, 5f);
        card.art = artRect.gameObject.AddComponent<Image>();
        card.art.sprite = Resources.Load<Sprite>("GroundTiles/" + kind.card);
        card.art.preserveAspect = false;
        card.art.type = Image.Type.Simple;
        card.art.raycastTarget = false;

        TMP_Text title = CozyRuntimeUI.Text(c, "Name", kind.name, 13.5f, CozyRuntimeUI.Ink, TextAlignmentOptions.MidlineLeft, true);
        Row(title.rectTransform, 10f, 10f, 68f, 18f);

        TMP_Text description = CozyRuntimeUI.Text(c, "Description", kind.description, 10f, CozyRuntimeUI.Muted,
            TextAlignmentOptions.TopLeft, false);
        description.textWrappingMode = TextWrappingModes.Normal;
        Row(description.rectTransform, 10f, 10f, 87f, 36f);

        card.button = CozyRuntimeUI.Button(c, "Button", "btn_primary", "칠하기", Color.white, 12f,
            () => OnPaint(kind), Green);
        card.buttonImage = card.button.GetComponent<Image>();
        card.buttonText = card.button.GetComponentInChildren<TMP_Text>();
        Row((RectTransform)card.button.transform, 8f, 8f, 126f, 30f);

        return card;
    }

    private void RefreshCards()
    {
        bool hasShovel = ToolInventory.HasShovel;
        bool canPaint = TilePainter.CanPaintHere(out string reason);

        infoTitle.text = hasShovel ? "삽으로 바닥 바꾸기" : "삽이 필요해요";
        infoText.text = hasShovel
            ? (canPaint
                ? "바닥을 고르고 [칠하기] 를 누른 뒤, 마을에서 바꿀 칸을 클릭하세요.\n<b>한 칸씩 계속 바꿀 수 있고, 우클릭하면 끝나요.</b>"
                : reason)
            : "마을의 <b>대장간</b>을 클릭해 삽을 만들면 바닥을 바꿀 수 있어요.";
        infoIcon.color = hasShovel ? Color.white : new Color(1f, 1f, 1f, 0.45f);

        foreach (Card card in cards)
        {
            card.art.color = hasShovel ? Color.white : new Color(1f, 1f, 1f, 0.5f);

            if (!hasShovel)
                SetButton(card, "잠김", false);
            else if (!canPaint)
                SetButton(card, "여기선 못 바꿔요", false);
            else
                SetButton(card, "칠하기", true);
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

    private void OnPaint(TileKind kind)
    {
        if (!TilePainter.CanPaintHere(out string reason))
        {
            Toast.Show(reason);
            return;
        }

        inventoryUI.CloseInventory();
        TilePainter.Begin(kind.id, kind.name);
    }

    // 창이 열려 있는 동안 상태가 바뀔 수 있으므로(월드 이동 등) 가끔 다시 그린다
    private float nextRefresh;

    private void Update()
    {
        if (panel == null || !panel.activeInHierarchy || Time.unscaledTime < nextRefresh)
            return;

        nextRefresh = Time.unscaledTime + 0.5f;
        RefreshCards();
    }

    // 부모 가로를 채우는 한 줄 (위에서 top 만큼 내려와 height 높이)
    private static void Row(RectTransform rt, float left, float right, float top, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(left, -top - height);
        rt.offsetMax = new Vector2(-right, -top);
    }
}
