using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 건물 탭: 건설 목록을 보여 주고, [철거] 버튼으로 철거 모드에 들어간다.
//   예전의 '관리' 탭은 없앴다. 관리 버튼(managementButton)은 이제 [철거] 버튼으로 쓰인다:
//   누르면 Tab 창이 닫히고 철거 모드가 켜지며, 마을의 건물을 클릭하면 철거 확인창이 뜬다.
public class BuildingTabUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject constructionPanel;
    [SerializeField] private GameObject managementPanel;

    [Header("Buttons")]
    [SerializeField] private Button constructionButton;
    [SerializeField] private Button managementButton;

    // 선택된 버튼 모양 표시 (없어도 동작한다)
    [Header("Cozy Style (optional)")]
    [SerializeField] private TabButtonVisual constructionVisual;
    // 예전 관리 탭 버튼의 모양 (이제 [철거] 버튼이라 선택 모양을 바꾸지 않는다)
    [SerializeField] private TabButtonVisual managementVisual;

    // 건설/관리 버튼의 클릭 이벤트를 연결한다.
    private void Awake()
    {
        constructionButton.onClick.AddListener(ShowConstruction);
        managementButton.onClick.AddListener(StartDemolition);

        // 관리 버튼을 [철거] 버튼으로 보이게 한다 (붉은 글자)
        TMP_Text label = managementButton.GetComponentInChildren<TMP_Text>(true);

        if (label != null)
        {
            label.text = "철거";
            label.color = DemolishTextColor;
        }
    }

    private static readonly Color DemolishTextColor = new Color32(0xA2, 0x40, 0x2B, 0xFF);

    // 철거 모드로 들어간다: Tab 창을 닫고 건설 모드를 철거 도구로 켠다
    public void StartDemolition()
    {
        // 친구 마을, 광장에서는 철거할 수 없다
        if (WorldSync.BlockBuildingChange())
            return;

        if (inventoryUI == null)
            inventoryUI = FindFirstObjectByType<InventoryUI>();

        if (buildingModeManager == null)
            buildingModeManager = FindFirstObjectByType<BuildingModeManager>();

        if (buildingModeManager == null)
        {
            Debug.LogError("BuildingModeManager를 찾을 수 없어 철거 모드를 켤 수 없습니다.");
            return;
        }

        if (inventoryUI != null)
            inventoryUI.CloseInventory();

        buildingModeManager.EnterDemolishMode();
    }

    private InventoryUI inventoryUI;
    private BuildingModeManager buildingModeManager;

    // UI가 활성화될 때 기본 탭인 건설 탭을 보여 준다.
    private void OnEnable()
    {
        ShowConstruction();
    }

    // 건설 패널을 켜고 관리 패널을 숨긴다.
    public void ShowConstruction()
    {
        if (constructionPanel != null)
            constructionPanel.SetActive(true);

        if (managementPanel != null)
            managementPanel.SetActive(false);

        if (constructionVisual != null)
            constructionVisual.SetActive(true);
    }
}
