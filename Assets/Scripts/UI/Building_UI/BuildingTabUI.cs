using UnityEngine;
using UnityEngine.UI;

public class BuildingTabUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject constructionPanel;
    [SerializeField] private GameObject managementPanel;

    [Header("Buttons")]
    [SerializeField] private Button constructionButton;
    [SerializeField] private Button managementButton;

    private void Awake()
    {
        constructionButton.onClick.AddListener(ShowConstruction);
        managementButton.onClick.AddListener(ShowManagement);
    }

    private void OnEnable()
    {
        ShowConstruction();
    }

    public void ShowConstruction()
    {
        if (constructionPanel != null)
            constructionPanel.SetActive(true);

        if (managementPanel != null)
            managementPanel.SetActive(false);
    }

    public void ShowManagement()
    {
        if (constructionPanel != null)
            constructionPanel.SetActive(false);

        if (managementPanel != null)
            managementPanel.SetActive(true);
    }
}
