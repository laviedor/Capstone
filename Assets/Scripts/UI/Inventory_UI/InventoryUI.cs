using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 인벤토리 창의 열기/닫기와 플레이어·건설·필드 탭 전환을 관리한다.
public class InventoryUI : MonoBehaviour
{
    // 전체 인벤토리 창 오브젝트
    [Header("Inventory Window")]
    [SerializeField] private GameObject inventoryWindow;

    // 탭에 따라 교대로 표시할 패널
    [Header("Inventory Panels")]
    [SerializeField] private GameObject playerPanel;
    [SerializeField] private GameObject buildingPanel;
    [SerializeField] private GameObject fieldPanel;

    // 각 패널로 전환하는 버튼
    [Header("Tab Buttons")]
    [SerializeField] private Button playerTabButton;
    [SerializeField] private Button buildingTabButton;
    [SerializeField] private Button fieldTabButton;

    // 건설 탭을 열 때 데코레이션 목록을 갱신할 참조
    [Header("Construction List")]
    [SerializeField] private ConstructionListUI constructionListUI;

    // 현재 인벤토리 열림 여부
    private bool isOpen = false;

    // 다른 스크립트에서 인벤토리 상태를 확인할 수 있도록 공개
    public bool IsOpen => isOpen;

    // 버튼 클릭 이벤트를 연결하고 초기에는 인벤토리를 숨긴다.
    private void Awake()
    {
        // 버튼 이벤트 연결
        playerTabButton.onClick.AddListener(ShowPlayerPanel);
        buildingTabButton.onClick.AddListener(ShowBuildingPanel);
        fieldTabButton.onClick.AddListener(ShowFieldPanel);

        // 시작 시 인벤토리 닫기
        isOpen = false;
        inventoryWindow.SetActive(false);
    }

    // 매 프레임 Tab 키 입력을 감지한다.
    private void Update()
    {
        // 키보드 장치가 없으면 입력을 처리하지 않는다.
        if (Keyboard.current == null)
            return;

        // Tab 키를 누른 순간 인벤토리 상태를 전환한다.
        if (Keyboard.current.tabKey.wasPressedThisFrame)
        {
            ToggleInventory();
        }
    }

    // 인벤토리를 열거나 닫고, 열릴 때 기본 탭을 플레이어 탭으로 지정한다.
    public void ToggleInventory()
    {
        isOpen = !isOpen;
        inventoryWindow.SetActive(isOpen);

        if (isOpen)
        {
            ShowPlayerPanel();
        }
    }
    // 다른 UI나 시스템에서 호출할 수 있는 인벤토리 닫기 기능
    public void CloseInventory()
    {
        isOpen = false;
        inventoryWindow.SetActive(false);
    }

    // 플레이어 패널만 표시한다.
    public void ShowPlayerPanel()
    {
        playerPanel.SetActive(true);
        buildingPanel.SetActive(false);
        fieldPanel.SetActive(false);
    }

    // 건설 패널만 표시하고 보유 데코레이션 카드를 최신 상태로 갱신한다.
    public void ShowBuildingPanel()
    {
        playerPanel.SetActive(false);
        buildingPanel.SetActive(true);
        fieldPanel.SetActive(false);

        if (constructionListUI != null)
        {
            constructionListUI.RefreshDecorationCards();
        }
    }

    // 필드 패널만 표시한다.
    public void ShowFieldPanel()
    {
        playerPanel.SetActive(false);
        buildingPanel.SetActive(false);
        fieldPanel.SetActive(true);
    }
}
