using UnityEngine;

public class BuildingToolUI : MonoBehaviour
{
    [Header("Building Mode")]
    [SerializeField]
    private BuildingModeManager buildingModeManager;

    [Header("Cursor Textures")]
    [SerializeField]
    private Texture2D hammerCursor;

    [SerializeField]
    private Texture2D pickaxeCursor;

    private BuildingTool lastTool;
    private bool wasBuildMode;


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


    void UpdateCursor()
    {
        switch (buildingModeManager.CurrentTool)
        {
            case BuildingTool.Build:

                Cursor.SetCursor(
                    hammerCursor,
                    Vector2.zero,
                    CursorMode.Auto
                );

                break;


            case BuildingTool.Demolish:

                Cursor.SetCursor(
                    pickaxeCursor,
                    Vector2.zero,
                    CursorMode.Auto
                );

                break;
        }
    }


    void SetDefaultCursor()
    {
        Cursor.SetCursor(
            null,
            Vector2.zero,
            CursorMode.Auto
        );
    }


    private void OnDisable()
    {
        SetDefaultCursor();
    }
}