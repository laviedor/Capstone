using UnityEngine;

// 스크립트 설명: 건설 도구 상태에 따라 시스템 마우스 커서를 설정한다.
public class BuildingToolUI : MonoBehaviour
{
    [Header("Building Mode")]
    [SerializeField]
    // 변수 설명: 현재 건설 모드 및 선택 도구를 관리하는 컴포넌트
    private BuildingModeManager buildingModeManager;

    [Header("Cursor Textures")]
    [SerializeField]
    // 변수 설명: 망치 커서로 사용할 텍스처 참조
    private Texture2D hammerCursor;

    [SerializeField]
    // 변수 설명: 곡괭이 커서로 사용할 텍스처 참조
    private Texture2D pickaxeCursor;

    // 변수 설명: 이전 프레임에 선택되어 있던 도구
    private BuildingTool lastTool;
    // 변수 설명: 이전 프레임의 건설 모드 활성화 여부
    private bool wasBuildMode;


    // 함수 설명: 건설 모드와 선택 도구 변경 여부를 감지하여 커서를 갱신한다.
    void Update()
    {
        if (buildingModeManager == null)
            return;


        // 건설 모드 OFF
        if (!buildingModeManager.IsBuildMode)
        {
            if (wasBuildMode)
            {
                SetDefaultCursor();
            }

            wasBuildMode = false;
            return;
        }


        // 건설 모드에 처음 들어왔거나
        // 도구가 변경되었을 때만 커서 변경
        if (!wasBuildMode ||
            lastTool != buildingModeManager.CurrentTool)
        {
            UpdateCursor();

            lastTool =
                buildingModeManager.CurrentTool;
        }

        wasBuildMode = true;
    }


    // 함수 설명: 선택 도구에 따라 커서 설정을 적용한다. 현재 두 경우 모두 기본 커서로 설정한다.
    void UpdateCursor()
    {
        switch (buildingModeManager.CurrentTool)
        {
            case BuildingTool.Build:

                Cursor.SetCursor(
                    null,
                    Vector2.zero,
                    CursorMode.Auto
                );

                break;


            case BuildingTool.Demolish:

                Cursor.SetCursor(
                    null,
                    Vector2.zero,
                    CursorMode.Auto
                );

                break;
        }
    }


    // 함수 설명: 시스템 기본 커서로 복원한다.
    void SetDefaultCursor()
    {
        Cursor.SetCursor(
            null,
            Vector2.zero,
            CursorMode.Auto
        );
    }


    // 함수 설명: 컴포넌트가 비활성화될 때 커서를 기본 상태로 복원한다.
    private void OnDisable()
    {
        SetDefaultCursor();
    }
}