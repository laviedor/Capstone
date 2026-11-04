using UnityEngine;
using UnityEngine.InputSystem;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Map Bounds")]
    [SerializeField] private MapBounds mapBounds;

    [Header("Follow")]
    [SerializeField] private float followSpeed = 5f;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 1f;
    [SerializeField] private float minZoom = 2f;
    [SerializeField] private float maxZoom = 8f;

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void LateUpdate()
    {
        HandleZoom();
        FollowTarget();
    }

    private void FollowTarget()
    {
        if (target == null || cam == null)
            return;

        Vector3 targetPosition = new Vector3(
            target.position.x,
            target.position.y,
            transform.position.z
        );

        // 먼저 플레이어를 따라갈 위치 계산
        Vector3 followPosition = Vector3.Lerp(
            transform.position,
            targetPosition,
            followSpeed * Time.deltaTime
        );

        // MapBounds가 있으면 카메라 위치 제한
        if (mapBounds != null)
        {
            followPosition =
                ClampCameraPosition(followPosition);
        }

        transform.position = followPosition;
    }

    private Vector3 ClampCameraPosition(
        Vector3 position)
    {
        Bounds bounds = mapBounds.Bounds;

        // Orthographic 카메라의 세로 절반 크기
        float cameraHalfHeight =
            cam.orthographicSize;

        // 화면 비율을 이용한 가로 절반 크기
        float cameraHalfWidth =
            cameraHalfHeight * cam.aspect;

        float minX =
            bounds.min.x + cameraHalfWidth;

        float maxX =
            bounds.max.x - cameraHalfWidth;

        float minY =
            bounds.min.y + cameraHalfHeight;

        float maxY =
            bounds.max.y - cameraHalfHeight;

        // 카메라 화면이 맵보다 더 큰 경우
        // 해당 축은 맵 중앙에 고정
        if (minX > maxX)
        {
            position.x = bounds.center.x;
        }
        else
        {
            position.x = Mathf.Clamp(
                position.x,
                minX,
                maxX
            );
        }

        if (minY > maxY)
        {
            position.y = bounds.center.y;
        }
        else
        {
            position.y = Mathf.Clamp(
                position.y,
                minY,
                maxY
            );
        }

        return position;
    }

    private void HandleZoom()
    {

        if (cam == null || Mouse.current == null)
            return;

        float scroll =
            Mouse.current.scroll.ReadValue().y;

        if (scroll == 0)
            return;

        cam.orthographicSize -=
            scroll * zoomSpeed * 0.01f;

        // 기본 최대 줌아웃 값
        float allowedMaxZoom = maxZoom;

        if (mapBounds != null)
        {
            Bounds bounds = mapBounds.Bounds;

            // 세로 기준 최대 Orthographic Size
            float maxZoomByHeight =
                bounds.size.y / 2f;

            // 가로 기준 최대 Orthographic Size
            // cameraHalfWidth = orthographicSize * aspect
            float maxZoomByWidth =
                bounds.size.x / (2f * cam.aspect);

            // 가로/세로 중 먼저 MapBounds를 넘어가는 쪽 사용
            float mapMaxZoom = Mathf.Min(
                maxZoomByHeight,
                maxZoomByWidth
            );

            allowedMaxZoom = Mathf.Min(
                maxZoom,
                mapMaxZoom
            );
        }

        cam.orthographicSize = Mathf.Clamp(
            cam.orthographicSize,
            minZoom,
            allowedMaxZoom
        );
    }
}
