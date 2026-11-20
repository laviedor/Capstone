using UnityEngine;

// 스크립트 설명: 건물 목록 패널의 표시 상태를 관리한다.
public class BuildingListUI : MonoBehaviour
{
    [Header("References")]
    // 변수 설명: 건물 목록을 표시하는 UI 패널
    [SerializeField] private GameObject buildingListPanel;
    // 변수 설명: 현재 건설 모드 및 선택 도구를 관리하는 컴포넌트
    [SerializeField] private BuildingModeManager buildingModeManager;

    // 속성 설명: 현재 UI 패널이 열려 있는지 외부에서 확인할 수 있다.
    public bool IsOpen
    {
        get
        {
            return buildingListPanel != null &&
                   buildingListPanel.activeSelf;
        }
    }

    // 함수 설명: 시작할 때 건물 목록 패널을 닫아 초기 상태를 설정한다.
    private void Start()
    {
        if (buildingListPanel != null)
        {
            buildingListPanel.SetActive(false);
        }
    }

    // 함수 설명: 건설 모드가 아니면 건물 목록을 닫는다. (키보드 단축키는 쓰지 않는다)
    private void Update()
    {
        if (buildingModeManager == null)
            return;

        // 건설 모드가 아니면 목록을 닫음
        if (!buildingModeManager.IsBuildMode)
        {
            Close();
        }
    }

    // 함수 설명: 현재 목록 패널의 활성화 상태를 반전한다.
    public void Toggle()
    {
        if (buildingListPanel == null)
            return;

        buildingListPanel.SetActive(
            !buildingListPanel.activeSelf
        );
    }

    // 함수 설명: 건물 목록 패널을 표시한다.
    public void Open()
    {
        if (buildingListPanel != null)
        {
            buildingListPanel.SetActive(true);
        }
    }

    // 함수 설명: 건물 목록 패널을 숨긴다.
    public void Close()
    {
        if (buildingListPanel != null)
        {
            buildingListPanel.SetActive(false);
        }
    }
}
