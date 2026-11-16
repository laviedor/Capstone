
using UnityEngine;
using UnityEngine.UI;

// 채집 진행 상태를 화면에 표시하고, 시작·진행·종료 시 UI를 관리하는 컴포넌트.
public class GatheringProgressUI : MonoBehaviour
{
    // Inspector에서 연결할 채집 진행률 UI 참조.
    [Header("진행률 UI")]
    // 채집 중에만 활성화할 UI 오브젝트.
    [SerializeField] private GameObject gatheringCanvas;
    // fillAmount를 이용해 채집 진행 정도를 표현할 이미지.
    [SerializeField] private Image progressFill;

    // 컴포넌트 초기화 시 진행률 표시가 남아 있지 않도록 숨긴다.
    private void Awake()
    {
        // 시작할 때 진행률 UI 숨기기
        HideProgress();
    }

    // 채집 시작
    // 채집 시작 시 UI를 표시하고 진행률을 0으로 초기화한다.
    public void ShowProgress()
    {
        if (gatheringCanvas != null)
            gatheringCanvas.SetActive(true);

        // 새로운 채집이므로 이전 진행률을 지운다.
        SetProgress(0f);
    }

    // 진행률 갱신 (0~1)
    // 진행률을 0~1 범위로 제한하여 이미지의 채움 비율에 적용한다.
    public void SetProgress(float progress)
    {
        if (progressFill != null)
        {
            progressFill.fillAmount =
                // 잘못된 입력값도 0 미만이나 1 초과로 표시되지 않게 제한한다.
                Mathf.Clamp01(progress);
        }
    }

    // 채집 완료 또는 취소
    // 채집 완료·취소 시 진행률을 초기화하고 UI를 숨긴다.
    public void HideProgress()
    {
        if (progressFill != null)
            progressFill.fillAmount = 0f;

        if (gatheringCanvas != null)
            gatheringCanvas.SetActive(false);
    }
}
