using UnityEngine;

// 스크립트 설명: 건설 모드의 활성화 여부와 현재 선택 도구를 관리한다.
public class BuildingModeManager : MonoBehaviour
{
    [Header("Build Mode")]
    // 변수 설명: 현재 건설 모드 활성화 여부
    [SerializeField] private bool isBuildMode = false;

    [Header("Current Tool")]
    [SerializeField]
    // 변수 설명: 현재 선택한 건설 또는 철거 도구
    private BuildingTool currentTool = BuildingTool.Build;

    // 속성 설명: 현재 건설 모드 활성화 상태를 외부에 읽기 전용으로 제공한다.
    public bool IsBuildMode
    {
        get { return isBuildMode; }
    }

    // 속성 설명: 현재 선택된 건설/철거 도구를 외부에 제공한다.
    public BuildingTool CurrentTool
    {
        get { return currentTool; }
    }

    // 건설·철거는 마우스로만 한다 (키보드 단축키 없음).
    //   건설: Tab 메뉴 건물 탭의 카드 → 건설하기 / 철거: 건물 탭의 [철거] 버튼
    //   끝내기: 화면 아래 안내 바의 [취소]/[끝내기] 버튼 또는 우클릭 (BuildModeHUD)
    private void Awake()
    {
        BuildModeHUD.Ensure(this);
    }

    // 함수 설명: 건설 모드를 켜거나 끄며 활성화 시 건설 도구를 기본 선택한다.
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
    // 함수 설명: 외부 UI에서 건설 모드를 켜고 건설 도구를 선택한다.
    public void EnterBuildMode()
    {
        isBuildMode = true;
        currentTool = BuildingTool.Build;

        Debug.Log("건설 모드 ON");
        Debug.Log("현재 도구 : Build");
    }

    // 외부 UI에서 철거 모드를 활성화
    // 함수 설명: 건물 탭의 [철거] 버튼에서 호출한다. 건설 모드를 켜고 철거 도구를 선택한다.
    public void EnterDemolishMode()
    {
        isBuildMode = true;
        currentTool = BuildingTool.Demolish;

        Debug.Log("철거 모드 ON");
    }

    // 함수 설명: 건설 모드를 끈다. (건물 하나를 설치한 뒤 등 외부에서 호출)
    public void ExitBuildMode()
    {
        if (!isBuildMode)
            return;

        isBuildMode = false;

        Debug.Log("건설 모드 OFF");
    }

    // 함수 설명: 건설 모드가 활성화된 경우 건설 도구로 전환한다.
    public void SetBuildTool()
    {
        if (!isBuildMode)
            return;

        currentTool = BuildingTool.Build;

        Debug.Log("현재 도구 : Build");
    }

    // 함수 설명: 건설 모드가 활성화된 경우 철거 도구로 전환한다.
    public void SetDemolishTool()
    {
        if (!isBuildMode)
            return;

        currentTool = BuildingTool.Demolish;

        Debug.Log("현재 도구 : Demolish");
    }
}
