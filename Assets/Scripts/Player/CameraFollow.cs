
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

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

    [Header("Screen Fit")]
    // 켜면 MapBounds를 하나의 화면 틀(1920x270 기준)로 사용한다.
    // 세로는 맵 높이에 딱 맞춰 고정하고, 가로는 맵 밖이 절대 보이지 않게 한다. (휠 줌 비활성)
    [SerializeField] private bool fitToMapBounds = true;

    [Header("Drag Pan")]
    // 좌클릭 드래그로 화면 둘러보기 사용 여부
    [SerializeField] private bool enableDragPan = true;
    // 이 거리(px) 이상 움직여야 클릭이 아닌 드래그로 판정
    [SerializeField] private float dragThresholdPixels = 8f;
    // 트랙패드 가로 스크롤로 둘러보는 속도 (0이면 사용 안 함)
    [SerializeField] private float horizontalScrollPanSpeed = 1f;
    // 둘러본 뒤 조작이 없으면 플레이어로 돌아오기까지의 시간(초). 0이면 자동 복귀 안 함
    [SerializeField] private float returnToPlayerDelay = 5f;

    // 같은 게임 오브젝트의 Camera 컴포넌트
    private Camera cam;

    // 좌클릭이 화면(UI 밖)에서 시작되어 드래그 후보인지 여부
    private bool isPanPointerDown = false;
    // 현재 드래그로 화면을 움직이는 중인지 여부
    private bool isDragging = false;
    // 좌클릭을 누른 화면 좌표
    private Vector2 pressScreenPosition;
    // 직전 프레임의 드래그 화면 좌표
    private Vector2 lastDragScreenPosition;

    // 사용자가 직접 둘러보는 중이라 플레이어 추적을 멈춘 상태
    private bool isManualView = false;
    // 마지막으로 직접 둘러본 시각
    private float lastManualViewTime;

    // 클릭과 드래그를 구분하는 기준 거리(px). MousePathfinding에서 같은 값을 사용
    public float DragThresholdPixels => dragThresholdPixels;
    // 현재 드래그 중인지 여부
    public bool IsDragging => isDragging;

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

    /// <summary>이동 후 카메라 추적, 줌, 드래그 둘러보기를 처리한다.</summary>
    private void LateUpdate()
    {
        if (fitToMapBounds)
        {
            ApplyScreenFit();
        }
        else
        {
            HandleZoom();
        }

        if (isViewingBuilding)
        {
            FollowBuilding();
            return;
        }

        HandleDragPan();
        HandleHorizontalScrollPan();

        if (isManualView)
        {
            UpdateManualView();
        }
        else
        {
            FollowTarget();
        }
    }

    /// <summary>
    /// 카메라 크기를 맵(MapBounds)에 맞춘다. 창 크기가 바뀌어도 매 프레임 다시 계산한다.
    /// - 세로: 맵 높이가 화면 높이에 딱 맞는다.
    /// - 가로: 창이 맵 비율보다 넓으면 맵 가로가 딱 맞게 줄이고,
    ///         좁으면(노트북 등) 맵 일부만 보이며 나머지는 드래그로 둘러본다.
    /// </summary>
    private void ApplyScreenFit()
    {
        if (cam == null || mapBounds == null)
            return;

        Bounds bounds = mapBounds.Bounds;

        float sizeByHeight =
            bounds.size.y / 2f;

        float sizeByWidth =
            bounds.size.x / (2f * cam.aspect);

        cam.orthographicSize = Mathf.Min(sizeByHeight, sizeByWidth);
    }

    /// <summary>좌클릭 드래그로 카메라를 움직인다. 클릭(짧은 이동)은 MousePathfinding이 처리한다.</summary>
    private void HandleDragPan()
    {
        if (!enableDragPan || cam == null || Mouse.current == null)
            return;

        ButtonControl leftButton = Mouse.current.leftButton;
        Vector2 screenPosition = Mouse.current.position.ReadValue();

        if (leftButton.wasPressedThisFrame)
        {
            // UI(슬라이더, 스크롤 목록 등) 위에서 시작한 드래그는 화면을 움직이지 않는다
            isPanPointerDown = !IsPointerOverUI();
            isDragging = false;
            pressScreenPosition = screenPosition;
            lastDragScreenPosition = screenPosition;
        }

        if (isPanPointerDown && leftButton.isPressed)
        {
            if (!isDragging &&
                Vector2.Distance(screenPosition, pressScreenPosition) >= dragThresholdPixels)
            {
                isDragging = true;
            }

            if (isDragging)
            {
                // 마우스 아래의 땅이 손가락을 따라오도록 같은 거리만큼 반대로 카메라 이동
                Vector3 previousWorld = cam.ScreenToWorldPoint(lastDragScreenPosition);
                Vector3 currentWorld = cam.ScreenToWorldPoint(screenPosition);

                Vector3 delta = previousWorld - currentWorld;
                delta.z = 0f;

                // 화면 맞춤 모드에서는 세로가 고정이므로 좌우로만 움직인다
                if (fitToMapBounds)
                    delta.y = 0f;

                MoveCameraManually(delta);

                lastDragScreenPosition = screenPosition;
            }
        }

        if (leftButton.wasReleasedThisFrame || !leftButton.isPressed)
        {
            isPanPointerDown = false;
            isDragging = false;
        }
    }

    /// <summary>트랙패드 두 손가락 가로 스와이프(또는 가로 휠)로 카메라를 좌우로 움직인다.</summary>
    private void HandleHorizontalScrollPan()
    {
        if (!enableDragPan || horizontalScrollPanSpeed <= 0f ||
            cam == null || Mouse.current == null)
            return;

        float scrollX = Mouse.current.scroll.ReadValue().x;

        if (scrollX == 0f)
            return;

        float worldDelta =
            scrollX * horizontalScrollPanSpeed * 0.01f * cam.orthographicSize;

        MoveCameraManually(new Vector3(worldDelta, 0f, 0f));
    }

    /// <summary>직접 둘러보기로 카메라를 옮기고 플레이어 추적을 잠시 멈춘다.</summary>
    private void MoveCameraManually(Vector3 delta)
    {
        Vector3 position = transform.position + delta;

        if (mapBounds != null)
        {
            position = ClampCameraPosition(position);
        }

        transform.position = position;

        isManualView = true;
        lastManualViewTime = Time.unscaledTime;
    }

    /// <summary>둘러보는 중에는 위치를 유지하고, 일정 시간 조작이 없으면 플레이어 추적으로 돌아간다.</summary>
    private void UpdateManualView()
    {
        if (isDragging)
        {
            lastManualViewTime = Time.unscaledTime;
        }
        else if (returnToPlayerDelay > 0f &&
                 Time.unscaledTime - lastManualViewTime >= returnToPlayerDelay)
        {
            isManualView = false;
            FollowTarget();
            return;
        }

        // 줌이 바뀌어도 맵 밖이 보이지 않도록 위치만 다시 제한
        if (mapBounds != null)
        {
            transform.position = ClampCameraPosition(transform.position);
        }
    }

    /// <summary>둘러보기를 끝내고 플레이어 추적을 다시 시작한다. (캐릭터 이동 클릭 시 호출)</summary>
    public void ResumeFollow()
    {
        isManualView = false;
    }

    /// <summary>마우스가 UI 위에 있는지 확인한다.</summary>
    private bool IsPointerOverUI()
    {
        return EventSystem.current != null &&
               EventSystem.current.IsPointerOverGameObject();
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
        isManualView = false;

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

        // 화면 맞춤 모드에서는 세로를 맵 중앙에 고정
        // 맵이 화면보다 낮다면 맵 중앙에 고정
        if (fitToMapBounds || minY > maxY)
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
