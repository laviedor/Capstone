using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 스크립트 설명: 마우스를 따라다니는 건설·철거 도구 아이콘을 표시한다.
public class MouseToolIcon : MonoBehaviour
{
    [Header("References")]
    // 변수 설명: 마우스를 따라 움직이는 도구 아이콘의 RectTransform
    [SerializeField] private RectTransform iconRect;
    // 변수 설명: 도구 이미지를 화면에 그리는 UI Image
    [SerializeField] private Image iconImage;
    // 변수 설명: 현재 건설 모드 및 선택 도구를 관리하는 컴포넌트
    [SerializeField] private BuildingModeManager buildingModeManager;

    [Header("Tool Icons")]
    // 변수 설명: 건설 도구 선택 시 표시할 망치 스프라이트
    [SerializeField] private Sprite hammerIcon;
    // 변수 설명: 철거 도구 선택 시 표시할 곡괭이 스프라이트
    [SerializeField] private Sprite pickaxeIcon;

    [Header("Position")]
    [SerializeField]
    // 변수 설명: 마우스 커서에서 아이콘을 띄워 표시할 화면 좌표 오프셋
    private Vector2 offset =
        new Vector2(20f, -20f);

    // 함수 설명: 마우스 위치를 따라 아이콘을 이동시키고 선택 도구에 맞는 이미지를 갱신한다.
    private void Update()
    {
        if (Mouse.current == null)
            return;

        UpdatePosition();
        UpdateToolIcon();
    }

    // 함수 설명: 현재 마우스 화면 좌표에 오프셋을 더해 아이콘의 UI 위치를 지정한다.
    private void UpdatePosition()
    {
        Vector2 mousePosition =
            Mouse.current.position.ReadValue();

        iconRect.position =
            mousePosition + offset;
    }

    // 함수 설명: 건설/철거 모드에 따라 망치·곡괭이 아이콘의 표시 여부와 스프라이트를 바꾼다.
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
