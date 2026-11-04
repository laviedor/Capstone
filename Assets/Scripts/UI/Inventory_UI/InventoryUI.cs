using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    [Header("Inventory Window")]
    [SerializeField] private GameObject inventoryWindow;

    [Header("Inventory Panels")]
    [SerializeField] private GameObject playerPanel;
    [SerializeField] private GameObject buildingPanel;
    [SerializeField] private GameObject fieldPanel;

    [Header("Tab Buttons")]
    [SerializeField] private Button playerTabButton;
    [SerializeField] private Button buildingTabButton;
    [SerializeField] private Button fieldTabButton;

    private bool isOpen = false;

    public bool IsOpen => isOpen;

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

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.tabKey.wasPressedThisFrame)
        {
            ToggleInventory();
        }
    }

    public void ToggleInventory()
    {
        isOpen = !isOpen;
        inventoryWindow.SetActive(isOpen);

        if (isOpen)
        {
            ShowPlayerPanel();
        }
    }
    public void CloseInventory()
    {
        isOpen = false;
        inventoryWindow.SetActive(false);
    }

    public void ShowPlayerPanel()
    {
        playerPanel.SetActive(true);
        buildingPanel.SetActive(false);
        fieldPanel.SetActive(false);
    }

    public void ShowBuildingPanel()
    {
        playerPanel.SetActive(false);
        buildingPanel.SetActive(true);
        fieldPanel.SetActive(false);
    }

    public void ShowFieldPanel()
    {
        playerPanel.SetActive(false);
        buildingPanel.SetActive(false);
        fieldPanel.SetActive(true);
    }
}
