using UnityEngine;
using UnityEngine.InputSystem;

public class BuildingListUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject buildingListPanel;
    [SerializeField] private BuildingModeManager buildingModeManager;

    public bool IsOpen
    {
        get
        {
            return buildingListPanel != null &&
                   buildingListPanel.activeSelf;
        }
    }

    private void Start()
    {
        if (buildingListPanel != null)
        {
            buildingListPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (buildingModeManager == null)
            return;

        // 건설 모드가 아니면 목록을 닫음
        if (!buildingModeManager.IsBuildMode)
        {
            Close();
            return;
        }

        // O키로 건물 목록 열기 / 닫기
        if (Keyboard.current.oKey.wasPressedThisFrame)
        {
            Toggle();
        }
    }

    public void Toggle()
    {
        if (buildingListPanel == null)
            return;

        buildingListPanel.SetActive(
            !buildingListPanel.activeSelf
        );
    }

    public void Open()
    {
        if (buildingListPanel != null)
        {
            buildingListPanel.SetActive(true);
        }
    }

    public void Close()
    {
        if (buildingListPanel != null)
        {
            buildingListPanel.SetActive(false);
        }
    }
}
