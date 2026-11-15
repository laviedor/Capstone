
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>플레이어 또는 선택된 건물을 추적하고 카메라 이동 범위 및 줌을 제어한다.</summary>
public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    // 기본 카메라 추적 대상인 플레이어
    [SerializeField] private Transform target;

    [Header("Map Bounds")]
    // 카메라가 벗어나지 않도록 제한할 맵 경계
    [SerializeField] private MapBounds mapBounds;

    [Header("Follow")]
    // 목표 위치로 부드럽게 이동하는 보간 속도
    [SerializeField] private float followSpeed = 5f;

    [Header("Zoom")]
    // 마우스 휠 줌 감도
    [SerializeField] private float zoomSpeed = 1f;
    // 카메라 직교 크기의 최소값
    [SerializeField] private float minZoom = 2f;
    // 카메라 직교 크기의 최대값
    [SerializeField] private float maxZoom = 8f;

    // 같은 게임 오브젝트의 Camera 컴포넌트
    private Camera cam;

    // 건물 위치 보기 모드
    private bool isViewingBuilding = false;

    // 확인할 건물
    // 건물 보기 모드에서 추적할 건물 위치
    private Transform viewingTarget;

    // 현재 건물 보기 모드 여부를 외부에서 조회
    public bool IsViewingBuilding => isViewingBuilding;

    /// <summary>카메라 컴포넌트를 캐싱한다.</summary>
    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    /// <summary>이동 후 카메라 추적 및 줌을 처리한다.</summary>
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
    // 일반 모드에서 플레이어 위치를 추적
    private void FollowTarget()
    {
        if (target == null || cam == null)
            return;

        MoveCamera(target.position);
    }

    // 건물 위치 추적
    // 건물 보기 모드에서 선택된 건물 위치를 추적
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
    /// <summary>목표 위치로 부드럽게 이동하고 필요하면 맵 범위 안으로 제한한다.</summary>
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
    /// <summary>설치된 건물을 카메라의 추적 대상으로 설정한다.</summary>
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
    /// <summary>건물 보기 모드를 종료하고 플레이어 추적으로 돌아간다.</summary>
    public void ReturnToPlayer()
    {
        isViewingBuilding = false;
        viewingTarget = null;

        Debug.Log("플레이어 카메라로 복귀");
    }

    /// <summary>화면 크기를 고려해 카메라 중심이 맵 밖으로 벗어나지 않게 한다.</summary>
    private Vector3 ClampCameraPosition(Vector3 position)
    {
        Bounds bounds = mapBounds.Bounds;

        // 현재 직교 카메라의 화면 높이 절반
        float cameraHalfHeight =
            cam.orthographicSize;

        // 화면 종횡비를 반영한 너비 절반
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

        // 맵이 화면보다 좁다면 맵 중앙에 고정
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

        // 맵이 화면보다 낮다면 맵 중앙에 고정
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

    /// <summary>휠 입력으로 직교 카메라 크기를 조절하되 맵 크기와 줌 한계를 적용한다.</summary>
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

            // 맵 높이로 계산한 최대 직교 크기
            float maxZoomByHeight =
                bounds.size.y / 2f;

            // 맵 너비와 카메라 비율로 계산한 최대 직교 크기
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
