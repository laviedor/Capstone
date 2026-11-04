
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InventoryUI : MonoBehaviour
{
    [Header("Inventory Window")]
    [SerializeField] private GameObject inventoryWindow;

    [Header("Panels")]
    [SerializeField] private GameObject playerPanel;
    [SerializeField] private GameObject buildingPanel;
    [SerializeField] private GameObject fieldPanel;

    [Header("Buttons")]
    [SerializeField] private Button playerTabButton;
    [SerializeField] private Button buildingTabButton;
    [SerializeField] private Button fieldTabButton;

    private bool isOpen;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        playerTabButton.onClick.AddListener(ShowPlayerPanel);
        buildingTabButton.onClick.AddListener(ShowBuildingPanel);
        fieldTabButton.onClick.AddListener(ShowFieldPanel);

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
