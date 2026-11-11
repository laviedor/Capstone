
using UnityEngine;
using UnityEngine.UI;

public class CameraReturnUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private Button returnButton;

    private void Awake()
    {
        if (returnButton != null)
        {
            returnButton.onClick.AddListener(ReturnToPlayer);
        }
    }

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

    private void ReturnToPlayer()
    {
        if (cameraFollow == null)
            return;

        cameraFollow.ReturnToPlayer();

        Debug.Log("카메라 플레이어 복귀 버튼 클릭");
    }

    private void OnDestroy()
    {
        if (returnButton != null)
        {
            returnButton.onClick.RemoveListener(ReturnToPlayer);
        }
    }
}
