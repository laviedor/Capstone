using UnityEngine;
using UnityEngine.UI;

// 필드에 있을 때만 채집 UI 진입 버튼을 보여 주고 패널 열기를 제어한다.
public class GatheringUI : MonoBehaviour
{
    [Header("채집 UI")]
    // 채집 메뉴를 여는 버튼 오브젝트.
    [SerializeField] private GameObject gatheringButton;
    // 실제 채집 장소 선택 화면을 담는 패널.
    [SerializeField] private GameObject gatheringPanel;

    [Header("버튼")]
    // 채집 메뉴 열기 및 뒤로 가기 버튼.
    [SerializeField] private Button openButton;
    [SerializeField] private Button backButton;

    // 시작 시 패널을 닫고 버튼 클릭 이벤트를 연결한다.
    private void Awake()
    {
        gatheringPanel.SetActive(false);

        openButton.onClick.AddListener(OpenGathering);
        backButton.onClick.AddListener(CloseGathering);
    }

    // UI가 활성화될 때 현재 필드 진입 여부를 다시 확인한다.
    private void OnEnable()
    {
        RefreshUI();
    }

    // 필드 밖에서는 채집 버튼을 숨기고 열려 있는 패널도 닫는다.
    private void RefreshUI()
    {
        FieldSceneManager manager = FieldSceneManager.Instance;

        // FieldSceneManager의 현재 필드 진입 상태를 기준으로 판단한다.
        bool isInField = manager != null && manager.IsInField;

        gatheringButton.SetActive(isInField);

        if (!isInField)
            gatheringPanel.SetActive(false);
    }

    // 채집 선택 패널을 연다.
    private void OpenGathering()
    {
        gatheringPanel.SetActive(true);
    }

    // 채집 선택 패널을 닫는다.
    private void CloseGathering()
    {
        gatheringPanel.SetActive(false);
    }
}
