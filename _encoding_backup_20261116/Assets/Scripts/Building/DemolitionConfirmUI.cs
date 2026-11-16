using TMPro;
using UnityEngine;

// 스크립트 설명: 철거 확인창과 환급 안내 문구를 관리한다.
public class DemolitionConfirmUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField]
    // 변수 설명: 철거 확인창의 최상위 UI 오브젝트
    private GameObject confirmPanel;

    [SerializeField]
    // 변수 설명: 철거 대상 및 환급 안내를 표시할 TMP 텍스트
    private TMP_Text messageText;

    [Header("Demolition")]
    [SerializeField]
    // 변수 설명: 실제 건물 철거 처리를 수행하는 컴포넌트
    private BuildingDemolition buildingDemolition;

    // 속성 설명: 현재 UI 패널이 열려 있는지 외부에서 확인할 수 있다.
    public bool IsOpen
    {
        get
        {
            return confirmPanel != null &&
                   confirmPanel.activeInHierarchy;
        }
    }

    // 함수 설명: 철거 확인 패널을 처음부터 숨긴다.
    private void Awake()
    {
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(false);
        }
    }


    // 함수 설명: 건물 종류에 맞는 환급 안내 문구를 표시하고 확인 패널을 연다.
    public void Open(Building building)
    {
        if (building == null || building.Data == null)
            return;

        if (confirmPanel == null)
            return;

        confirmPanel.SetActive(true);

        if (messageText == null)
            return;

        string buildingName = building.Data.buildingName;

        if (building.Data.buildingType == BuildingType.Decoration)
        {
            messageText.text =
                $"{buildingName}을(를) 철거하시겠습니까?\n" +
                "철거하면 데코레이션 아이템 1개가 반환됩니다.";
        }
        else
        {
            messageText.text =
                $"{buildingName}을(를) 철거하시겠습니까?\n" +
                "건설에 사용한 자원의 90%가 환급됩니다.";
        }
    }


    // 함수 설명: 확인창을 닫고 철거 대상 선택을 해제한다.
    public void Cancel()
    {
        confirmPanel.SetActive(false);

        if (buildingDemolition != null)
        {
            buildingDemolition.ClearSelection();
        }

        Debug.Log("철거 취소");
    }





    //철거 확인
    // 함수 설명: 선택한 건물의 실제 철거 처리를 요청하고 확인창을 닫는다.
    public void Confirm()
    {
        if (buildingDemolition == null)
            return;

        buildingDemolition.ConfirmDemolition();

        confirmPanel.SetActive(false);
    }
}