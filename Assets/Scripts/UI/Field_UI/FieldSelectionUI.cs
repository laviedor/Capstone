
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 필드 선택 버튼과 이동 버튼의 상태 및 안내 문구를 관리하는 UI입니다.
public class FieldSelectionUI : MonoBehaviour
{
    [Header("필드 선택 버튼")]
    // 숲 필드를 선택하는 버튼입니다.
    [SerializeField] private Button forestButton;

    [Header("필드 정보")]
    // 현재 선택한 지역의 이름을 표시합니다.
    [SerializeField] private TMP_Text fieldNameText;
    // 선택 지역에 대한 안내 문구를 표시합니다.
    [SerializeField] private TMP_Text fieldDescriptionText;
    // 선택한 필드로 이동하거나 마을로 돌아가는 버튼입니다.
    [SerializeField] private Button moveButton;

    // 사용자가 선택한 목적지 씬 이름을 임시 저장합니다.
    private string selectedSceneName = "";

    // UI가 다시 표시될 때 현재 위치에 맞는 버튼 상태로 갱신합니다.
    private void OnEnable()
    {
        RefreshFieldUI();
    }

    // 선택/이동 버튼의 클릭 이벤트를 연결하고 초기 UI를 갱신합니다.
    private void Start()
    {
        forestButton.onClick.AddListener(SelectForest);
        moveButton.onClick.AddListener(OnMoveButtonClicked);

        RefreshFieldUI();
    }

    // 마을에 있는지 필드에 있는지에 따라 버튼과 설명을 다르게 표시합니다.
    private void RefreshFieldUI()
    {
        // 현재 씬 전환 관리자에 접근하여 위치 상태를 확인하거나 이동을 요청합니다.
        FieldSceneManager manager = FieldSceneManager.Instance;

        if (manager == null)
            return;

        // 필드에 있다면 마을 복귀용 UI를 보여줍니다.
        if (manager.IsInField)
        {
            // 숲에 있을 때
            // 복귀 화면에 표시할 마을 씬 이름입니다. 실제 복귀는 매니저의 언로드 함수로 처리합니다.
            selectedSceneName = "VillageScene";

            fieldNameText.text = "마을";
            fieldDescriptionText.text =
                "기존 마을로 돌아갑니다.";

            forestButton.gameObject.SetActive(false);

            moveButton.interactable = true;
        }
        else
        {
            // 마을에 있을 때
            // 마을에서는 아직 필드를 선택하지 않은 상태로 초기화합니다.
            selectedSceneName = "";

            fieldNameText.text = "필드를 선택하세요";
            fieldDescriptionText.text = "이동할 지역을 선택해주세요.";

            forestButton.gameObject.SetActive(true);

            moveButton.interactable = false;
        }
    }

    // 숲 선택 시 목적지와 설명을 갱신하고 이동 버튼을 활성화합니다.
    private void SelectForest()
    {
        // 숲에 해당하는 씬 이름을 선택 상태에 기록합니다.
        selectedSceneName = "ResourceField";

        fieldNameText.text = "숲";
        fieldDescriptionText.text =
            "나무와 자연 자원을 획득할 수 있는 지역입니다.";

        moveButton.interactable = true;

        Debug.Log("선택한 필드: " + selectedSceneName);
    }

    // 현재 위치에 따라 마을 복귀 또는 선택한 필드로의 이동을 요청합니다.
    private void OnMoveButtonClicked()
    {
        FieldSceneManager manager = FieldSceneManager.Instance;

        if (manager == null)
        {
            Debug.LogError("FieldSceneManager를 찾을 수 없습니다.");
            return;
        }

        if (manager.IsInField)
        {
            Debug.Log("마을 복귀 요청");
            manager.ReturnToVillage();
            return;
        }

        // 목적지를 선택하지 않은 경우 잘못된 이동 요청을 차단합니다.
        if (string.IsNullOrEmpty(selectedSceneName))
        {
            Debug.LogWarning("이동할 필드가 선택되지 않았습니다.");
            return;
        }

        // 선택한 필드 씬을 비동기로 불러오도록 매니저에 요청합니다.
        manager.MoveToField(selectedSceneName);
    }
}
