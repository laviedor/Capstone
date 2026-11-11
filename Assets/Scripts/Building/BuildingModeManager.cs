
using UnityEngine;
using UnityEngine.InputSystem;

public class BuildingModeManager : MonoBehaviour
{
    [Header("Build Mode")]
    [SerializeField] private bool isBuildMode = false;

    [Header("Current Tool")]
    [SerializeField]
    private BuildingTool currentTool = BuildingTool.Build;

    public bool IsBuildMode
    {
        get { return isBuildMode; }
    }

    public BuildingTool CurrentTool
    {
        get { return currentTool; }
    }

    void Update()
    {
        if (Keyboard.current == null)
            return;

        // B = 건설 모드 ON/OFF
        if (Keyboard.current.bKey.wasPressedThisFrame)
        {
            ToggleBuildMode();
        }

        if (!isBuildMode)
            return;

        // 1 = 건설 도구
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            SetBuildTool();
        }

        // 2 = 철거 도구
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            SetDemolishTool();
        }
    }

    void ToggleBuildMode()
    {
        isBuildMode = !isBuildMode;

        if (isBuildMode)
        {
            currentTool = BuildingTool.Build;

            Debug.Log("건설 모드 ON");
            Debug.Log("현재 도구 : Build");
        }
        else
        {
            Debug.Log("건설 모드 OFF");
        }
    }

    // 외부 UI에서 건설 모드를 활성화
    public void EnterBuildMode()
    {
        isBuildMode = true;
        currentTool = BuildingTool.Build;

        Debug.Log("건설 모드 ON");
        Debug.Log("현재 도구 : Build");
    }

    public void SetBuildTool()
    {
        if (!isBuildMode)
            return;

        currentTool = BuildingTool.Build;

        Debug.Log("현재 도구 : Build");
    }

    public void SetDemolishTool()
    {
        if (!isBuildMode)
            return;

        currentTool = BuildingTool.Demolish;

        Debug.Log("현재 도구 : Demolish");
    }
}
