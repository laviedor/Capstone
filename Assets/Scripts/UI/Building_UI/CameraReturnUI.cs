
using UnityEngine;
using UnityEngine.UI;

// 건물 위치 보기 중에만 플레이어 카메라 복귀 버튼을 표시한다.
public class CameraReturnUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private Button returnButton;

    // 복귀 버튼 클릭 시 호출할 함수를 등록한다.
    private void Awake()
    {
        if (returnButton != null)
        {
            returnButton.onClick.AddListener(ReturnToPlayer);
        }
    }

    // 카메라의 건물 보기 상태에 맞춰 복귀 버튼 활성화를 동기화한다.
    private void Update()
    {
        if (cameraFollow == null || returnButton == null)
            return;

        // 건물 위치 보기 중일 때만 버튼 표시
        bool shouldShow = cameraFollow.IsViewingBuilding;

        if (returnButton.gameObject.activeSelf != shouldShow)
        {
            returnButton.gameObject.SetActive(shouldShow);
        }
    }

    // 건물 보기 모드를 종료하고 카메라 추적 대상을 플레이어로 되돌린다.
    private void ReturnToPlayer()
    {
        if (cameraFollow == null)
            return;

        cameraFollow.ReturnToPlayer();

        Debug.Log("카메라 플레이어 복귀 버튼 클릭");
    }

    // 등록된 버튼 클릭 리스너를 정리한다.
    private void OnDestroy()
    {
        if (returnButton != null)
        {
            returnButton.onClick.RemoveListener(ReturnToPlayer);
        }
    }
}
