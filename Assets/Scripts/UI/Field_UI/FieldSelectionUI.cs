
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FieldSelectionUI : MonoBehaviour
{
    [Header("필드 선택 버튼")]
    [SerializeField] private Button forestButton;

    [Header("필드 정보")]
    [SerializeField] private TMP_Text fieldNameText;
    [SerializeField] private TMP_Text fieldDescriptionText;
    [SerializeField] private Button moveButton;

    private string selectedSceneName = "";

    private void OnEnable()
    {
        RefreshFieldUI();
    }

    private void Start()
    {
        forestButton.onClick.AddListener(SelectForest);
        moveButton.onClick.AddListener(OnMoveButtonClicked);

        RefreshFieldUI();
    }

    private void RefreshFieldUI()
    {
        FieldSceneManager manager = FieldSceneManager.Instance;

        if (manager == null)
            return;

        if (manager.IsInField)
        {
            // 숲에 있을 때
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
            selectedSceneName = "";

            fieldNameText.text = "필드를 선택하세요";
            fieldDescriptionText.text = "이동할 지역을 선택해주세요.";

            forestButton.gameObject.SetActive(true);

            moveButton.interactable = false;
        }
    }

    private void SelectForest()
    {
        selectedSceneName = "ResourceField";

        fieldNameText.text = "숲";
        fieldDescriptionText.text =
            "나무와 자연 자원을 획득할 수 있는 지역입니다.";

        moveButton.interactable = true;

        Debug.Log("선택한 필드: " + selectedSceneName);
    }

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

        if (string.IsNullOrEmpty(selectedSceneName))
        {
            Debug.LogWarning("이동할 필드가 선택되지 않았습니다.");
            return;
        }

        manager.MoveToField(selectedSceneName);
    }
}
