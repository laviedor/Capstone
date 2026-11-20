using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 인벤토리·건설·탐색 창을 열고 닫는다.
//   게임은 마우스로만 조작한다: 화면 오른쪽 아래의 아이콘 버튼(QuickMenuHUD)을 누르면 해당 창이 바로 열린다.
//     [가방] 인벤토리 창 (장비, 아이템)
//     [망치+곡괭이] 건설 창 (건물 짓기, 철거)
//     [돋보기] 탐색 창 (필드 이동, 채집)
//   세 창은 탭으로 묶지 않고 각각 따로 뜬다: 창 위에는 지금 창의 이름표 하나만 보인다.
//   같은 아이콘을 다시 누르거나 창의 닫기 버튼을 누르면 닫힌다. (Tab 키는 쓰지 않는다)
public class InventoryUI : MonoBehaviour
{
    // 열 수 있는 창
    public enum Window
    {
        Inventory,
        Building,
        Field
    }

    // 창 틀 오브젝트 (세 창이 같은 틀을 쓴다)
    [Header("Inventory Window")]
    [SerializeField] private GameObject inventoryWindow;

    // 창마다 보여 줄 내용
    [Header("Inventory Panels")]
    [SerializeField] private GameObject playerPanel;
    [SerializeField] private GameObject buildingPanel;
    [SerializeField] private GameObject fieldPanel;

    // 창 이름표 (예전 탭 버튼. 지금 열린 창의 이름표 하나만 보인다)
    [Header("Tab Buttons")]
    [SerializeField] private Button playerTabButton;
    [SerializeField] private Button buildingTabButton;
    [SerializeField] private Button fieldTabButton;

    // 건설 창을 열 때 데코레이션 목록을 갱신할 참조
    [Header("Construction List")]
    [SerializeField] private ConstructionListUI constructionListUI;

    // 디자인 개편으로 추가된 이름표 모양과 닫기 버튼 (없어도 동작한다)
    [Header("Cozy Style (optional)")]
    [SerializeField] private TabButtonVisual playerTabVisual;
    [SerializeField] private TabButtonVisual buildingTabVisual;
    [SerializeField] private TabButtonVisual fieldTabVisual;
    [SerializeField] private Button closeButton;

    // 창 이름표에 쓸 이름
    private static readonly string[] WindowTitles = { "인벤토리", "건설", "탐색" };

    // 현재 창 열림 여부
    private bool isOpen = false;

    // 다른 스크립트에서 창 상태를 확인할 수 있도록 공개
    public bool IsOpen => isOpen;

    // 지금 열려 있는 창 (닫혀 있으면 마지막으로 열었던 창)
    public Window CurrentWindow { get; private set; } = Window.Inventory;

    // 창이 열리거나 닫히거나 바뀌면 호출된다 (아이콘 버튼의 선택 표시 갱신용)
    public event Action Changed;

    // 버튼 클릭 이벤트를 연결하고, 오른쪽 아래 아이콘 버튼을 만든 뒤 창을 숨긴다.
    private void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(CloseInventory);

        SetTitle(playerTabButton, WindowTitles[0], "icon_bag");
        SetTitle(buildingTabButton, WindowTitles[1], "icon_build");
        SetTitle(fieldTabButton, WindowTitles[2], "icon_explore");

        isOpen = false;
        inventoryWindow.SetActive(false);

        QuickMenuHUD.Ensure(this);
    }

    // 아이콘 버튼에서 호출: 그 창이 열려 있으면 닫고, 아니면 연다
    public void Toggle(Window window)
    {
        if (isOpen && CurrentWindow == window)
        {
            CloseInventory();
            return;
        }

        Open(window);
    }

    // 창을 연다 (다른 창이 열려 있었으면 바꿔서 보여 준다)
    public void Open(Window window)
    {
        isOpen = true;
        inventoryWindow.SetActive(true);

        switch (window)
        {
            case Window.Building:
                ShowBuildingPanel();
                break;
            case Window.Field:
                ShowFieldPanel();
                break;
            default:
                ShowPlayerPanel();
                break;
        }
    }

    // 인벤토리 창을 열거나 닫는다 (예전 Tab 키 동작과 같음)
    public void ToggleInventory()
    {
        Toggle(Window.Inventory);
    }

    // 다른 UI나 시스템에서 호출할 수 있는 창 닫기 기능
    public void CloseInventory()
    {
        if (!isOpen && !inventoryWindow.activeSelf)
            return;

        isOpen = false;
        inventoryWindow.SetActive(false);
        Changed?.Invoke();
    }

    // 인벤토리(플레이어) 내용만 표시한다.
    public void ShowPlayerPanel()
    {
        playerPanel.SetActive(true);
        buildingPanel.SetActive(false);
        fieldPanel.SetActive(false);
        ShowTitle(Window.Inventory);
    }

    // 건설 내용만 표시하고 보유 데코레이션 카드를 최신 상태로 갱신한다.
    public void ShowBuildingPanel()
    {
        playerPanel.SetActive(false);
        buildingPanel.SetActive(true);
        fieldPanel.SetActive(false);
        ShowTitle(Window.Building);

        if (constructionListUI != null)
        {
            constructionListUI.RefreshDecorationCards();
        }
    }

    // 탐색(필드) 내용만 표시한다.
    public void ShowFieldPanel()
    {
        playerPanel.SetActive(false);
        buildingPanel.SetActive(false);
        fieldPanel.SetActive(true);
        ShowTitle(Window.Field);
    }

    // 지금 창의 이름표만 창 왼쪽 위에 보이고, 나머지 이름표는 숨긴다.
    private void ShowTitle(Window window)
    {
        CurrentWindow = window;

        ShowTitleButton(playerTabButton, playerTabVisual, window == Window.Inventory);
        ShowTitleButton(buildingTabButton, buildingTabVisual, window == Window.Building);
        ShowTitleButton(fieldTabButton, fieldTabVisual, window == Window.Field);

        Changed?.Invoke();
    }

    private static void ShowTitleButton(Button button, TabButtonVisual visual, bool show)
    {
        if (button == null)
            return;

        button.gameObject.SetActive(show);

        if (!show)
            return;

        if (visual != null)
            visual.SetActive(true);

        // 이름표는 클릭할 일이 없다 (창을 바꾸는 것은 오른쪽 아래 아이콘 버튼)
        button.interactable = false;
        button.transition = Selectable.Transition.None;

        // 이름표는 항상 창 왼쪽 위 첫 자리에 둔다
        RectTransform rect = (RectTransform)button.transform;
        rect.anchoredPosition = new Vector2(0f, rect.anchoredPosition.y);
    }

    // 이름표 글자와 아이콘을 오른쪽 아래 아이콘 버튼과 같게 맞춘다
    private static void SetTitle(Button button, string title, string iconName)
    {
        if (button == null)
            return;

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);

        if (label != null)
            label.text = title;

        Transform glyph = button.transform.Find("Cozy_TabIcon/Glyph");
        Sprite icon = CozyRuntimeUI.Sprite(iconName);

        if (glyph != null && icon != null && glyph.GetComponent<Image>() is Image image)
        {
            image.sprite = icon;
            image.preserveAspect = true;

            // 아이콘이 동그라미 배경을 조금 넘도록 키운다
            RectTransform rect = (RectTransform)glyph;
            rect.offsetMin = new Vector2(-2f, -2f);
            rect.offsetMax = new Vector2(2f, 2f);
        }
    }
}
