
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

    // 건물 위치 보기 모드
    private bool isViewingBuilding = false;

    // 확인할 건물
    private Transform viewingTarget;

    public bool IsViewingBuilding => isViewingBuilding;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void LateUpdate()
    {
        HandleZoom();

        if (isViewingBuilding)
        {
            FollowBuilding();
        }
        else
        {
            FollowTarget();
        }
    }

    // 기존 플레이어 추적
    private void FollowTarget()
    {
        if (target == null || cam == null)
            return;

        MoveCamera(target.position);
    }

    // 건물 위치 추적
    private void FollowBuilding()
    {
        if (viewingTarget == null)
        {
            ReturnToPlayer();
            return;
        }

        MoveCamera(viewingTarget.position);
    }

    // 카메라 이동 공통 함수
    private void MoveCamera(Vector3 targetPosition)
    {
        Vector3 desiredPosition = new Vector3(
            targetPosition.x,
            targetPosition.y,
            transform.position.z
        );

        Vector3 followPosition = Vector3.Lerp(
            transform.position,
            desiredPosition,
            Mathf.Clamp01(followSpeed * Time.deltaTime)
        );

        if (mapBounds != null)
        {
            followPosition =
                ClampCameraPosition(followPosition);
        }

        transform.position = followPosition;
    }

    // 건물 위치 보기 시작
    public void ViewBuilding(Building building)
    {
        if (building == null || !building.IsPlaced)
        {
            Debug.LogWarning("설치된 건물이 아닙니다.");
            return;
        }

        viewingTarget = building.transform;
        isViewingBuilding = true;

        Debug.Log(
            "건물 위치 보기: " +
            building.Data.buildingName
        );
    }

    // 플레이어 카메라로 복귀
    public void ReturnToPlayer()
    {
        isViewingBuilding = false;
        viewingTarget = null;

        Debug.Log("플레이어 카메라로 복귀");
    }

    private Vector3 ClampCameraPosition(Vector3 position)
    {
        Bounds bounds = mapBounds.Bounds;

        float cameraHalfHeight =
            cam.orthographicSize;

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

        float allowedMaxZoom = maxZoom;

        if (mapBounds != null)
        {
            Bounds bounds = mapBounds.Bounds;

            float maxZoomByHeight =
                bounds.size.y / 2f;

            float maxZoomByWidth =
                bounds.size.x / (2f * cam.aspect);

            float mapMaxZoom = Mathf.Min(
                maxZoomByHeight,
                maxZoomByWidth
            );

            allowedMaxZoom = Mathf.Min(
                maxZoom,
                mapMaxZoom
            );
        }

        // 맵이 최소 줌 크기보다 작은 경우도 처리
        allowedMaxZoom = Mathf.Max(
            minZoom,
            allowedMaxZoom
        );

        cam.orthographicSize = Mathf.Clamp(
            cam.orthographicSize,
            minZoom,
            allowedMaxZoom
        );
    }
}
