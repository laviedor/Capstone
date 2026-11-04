using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class MouseToolIcon : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform iconRect;
    [SerializeField] private Image iconImage;
    [SerializeField] private BuildingModeManager buildingModeManager;

    [Header("Tool Icons")]
    [SerializeField] private Sprite hammerIcon;
    [SerializeField] private Sprite pickaxeIcon;

    [Header("Position")]
    [SerializeField]
    private Vector2 offset =
        new Vector2(20f, -20f);

    private void Update()
    {
        if (Mouse.current == null)
            return;

        UpdatePosition();
        UpdateToolIcon();
    }

    private void UpdatePosition()
    {
        Vector2 mousePosition =
            Mouse.current.position.ReadValue();

        iconRect.position =
            mousePosition + offset;
    }

    private void UpdateToolIcon()
    {
        if (buildingModeManager == null)
        {
            iconImage.enabled = false;
            return;
        }

        // 건설 모드가 아니면 아이콘 숨김
        if (!buildingModeManager.IsBuildMode)
        {
            iconImage.enabled = false;
            return;
        }

        // 건설 모드
        if (buildingModeManager.CurrentTool ==
            BuildingTool.Build)
        {
            iconImage.sprite = hammerIcon;
            iconImage.enabled = true;
        }

        // 철거 모드
        else if (buildingModeManager.CurrentTool ==
                 BuildingTool.Demolish)
        {
            iconImage.sprite = pickaxeIcon;
            iconImage.enabled = true;
        }
        else
        {
            iconImage.enabled = false;
        }
    }
}
