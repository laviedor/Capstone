using UnityEngine;
using UnityEngine.UI;

// 건설 탭과 관리 탭의 패널 표시 상태를 전환한다.
public class BuildingTabUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject constructionPanel;
    [SerializeField] private GameObject managementPanel;

    [Header("Buttons")]
    [SerializeField] private Button constructionButton;
    [SerializeField] private Button managementButton;

    // 건설/관리 버튼의 클릭 이벤트를 연결한다.
    private void Awake()
    {
        constructionButton.onClick.AddListener(ShowConstruction);
        managementButton.onClick.AddListener(ShowManagement);
    }

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
    }

    // 관리 패널을 켜고 건설 패널을 숨긴다.
    public void ShowManagement()
    {
        if (constructionPanel != null)
            constructionPanel.SetActive(false);

        if (managementPanel != null)
            managementPanel.SetActive(true);
    }
}
