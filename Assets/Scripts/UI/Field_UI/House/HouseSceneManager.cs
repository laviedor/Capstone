using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// 집 내부 씬을 추가 로드하고 마을과 집 사이의 이동 상태를 관리한다.
public class HouseSceneManager : MonoBehaviour
{
    // 다른 컴포넌트에서 집 이동 관리자를 참조하기 위한 싱글턴 인스턴스.
    public static HouseSceneManager Instance { get; private set; }

    [Header("집 내부 Scene 이름")]
    [SerializeField]
    // Inspector에서 지정할 집 내부 씬 이름. 최초 입장 시 이 씬을 로드한다.
    private string houseSceneName = "HouseInteriorScene";

    [Header("마을에서 숨길 오브젝트")]
    [SerializeField]
    // 집 내부에 있는 동안 숨길 마을의 시각 오브젝트 묶음.
    private GameObject villageVisualRoot;

    [SerializeField]
    // 집 내부에 있는 동안 숨길 마을 타일맵.
    private GameObject villageTilemap;

    [Header("플레이어 이동 시스템")]
    [SerializeField]
    // 씬 전환 시 이동 경로와 이동 가능 영역을 변경할 경로 탐색 컴포넌트.
    private MousePathfinding mousePathfinding;
    [Header("카메라")]
    [SerializeField] private CameraFollow cameraFollow;

    private MapBounds villageCameraBounds;

    [Header("플레이어")]
    // 씬 사이에서 위치를 이동시킬 플레이어 Transform.
    [SerializeField] private Transform player;
    public Transform Player => player;

    // 집 입장 직전 위치를 저장해 퇴장 시 동일한 장소로 되돌린다.
    private Vector3 villageReturnPosition;

    // 중복 입장 및 퇴장 요청을 막는 씬 전환 진행 플래그.
    private bool isTransitioning = false;
    // 집 내부 씬이 이미 로드되었는지 기록해 재입장 시 중복 로드를 방지한다.
    private bool isHouseLoaded = false;
    // 현재 플레이어가 집 안에 있는지를 기록한다.
    private bool isInsideHouse = false;

    // 다른 스크립트에서 집 내부 여부를 읽을 수 있는 속성.
    public bool IsInsideHouse => isInsideHouse;

    // 집 내부의 이동·건설 영역 (집 씬을 처음 불러올 때 찾는다). 집 안 건설(BuildingPlacement)이 쓴다
    public MapBounds InteriorBounds { get; private set; }

    // 씬에 동일한 관리자가 둘 이상 존재하지 않도록 인스턴스를 설정한다.
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (cameraFollow != null)
        {
            villageCameraBounds = cameraFollow.CurrentMapBounds;
        }
    }

    // 집 입장
    // 전환 중이거나 이미 집 안인 경우를 제외하고 집 입장을 요청한다.
    public void EnterHouse()
    {
        if (isTransitioning || isInsideHouse)
            return;

        // 광장(공용 공간)에서는 집에 들어가지 않는다
        if (HouseDoor.InMultiverse)
        {
            Toast.Show(HouseDoor.MultiverseMessage);
            return;
        }

        // 필드에 있는 동안 집 입장 방지
        if (FieldSceneManager.Instance != null &&
            FieldSceneManager.Instance.IsInField)
        {
            Debug.Log("필드에서는 집에 들어갈 수 없습니다.");
            return;
        }

        StartCoroutine(EnterHouseRoutine());
    }

    // 이동 상태 정리, 집 씬 로드, 플레이어 이동, 마을 화면 숨김을 순서대로 처리한다.
    private IEnumerator EnterHouseRoutine()
    {
        // 씬 전환 중 기존 이동 명령과 경로 표시가 남지 않도록 준비한다.
        if (mousePathfinding != null)
        {
            mousePathfinding.PrepareForSceneTransition();
        }

        isTransitioning = true;
        // 집에 들어가기 전 플레이어의 마을 위치 저장
        if (player != null)
        {
            villageReturnPosition = player.position;
        }

        // 처음 입장할 때만 Scene 로드
        // 최초 입장에만 집 씬을 Additive 모드로 불러온다.
        if (!isHouseLoaded)
        {
            AsyncOperation operation =
                SceneManager.LoadSceneAsync(
                    houseSceneName,
                    LoadSceneMode.Additive
                );

            if (operation == null)
            {
                Debug.LogError(
                    "집 내부 Scene 로드 실패: " +
                    houseSceneName
                );

                isTransitioning = false;
                yield break;
            }

            while (!operation.isDone)
                yield return null;

            isHouseLoaded = true;
        }

        // 집 내부 오브젝트 활성화
        // 이전에 비활성화했던 집 내부 씬 오브젝트를 다시 표시한다.
        SetHouseObjectsActive(true);
        // 집 내부 Scene 가져오기
        // 집 내부 MapBounds 검색
        Scene houseScene =
            SceneManager.GetSceneByName(houseSceneName);

        // 집 내부 입장 위치 검색
        // 집 내부 씬의 자식 오브젝트 중 HouseSpawnPoint를 찾아 입장 좌표로 사용한다.
        Transform houseSpawnPoint = null;

        foreach (GameObject root in houseScene.GetRootGameObjects())
        {
            Transform[] children =
                root.GetComponentsInChildren<Transform>(true);

            foreach (Transform child in children)
            {
                if (child.name == "HouseSpawnPoint")
                {
                    houseSpawnPoint = child;
                    break;
                }
            }

            if (houseSpawnPoint != null)
                break;
        }

        // 플레이어를 집 내부 입장 위치로 이동
        // 찾은 입장 위치로 플레이어를 옮기되 기존 Z 좌표를 유지한다.
        if (houseSpawnPoint != null && player != null)
        {
            Vector3 spawnPosition = houseSpawnPoint.position;

            spawnPosition.z = player.position.z;

            player.position = spawnPosition;

            Debug.Log("플레이어 집 내부 이동 완료");
        }
        else
        {
            Debug.LogWarning(
                "HouseSpawnPoint 또는 Player를 찾을 수 없습니다."
            );
        }


        // 집 내부 이동 제한에 사용할 MapBounds 컴포넌트를 탐색한다.
        MapBounds interiorBounds = null;

        foreach (GameObject root in houseScene.GetRootGameObjects())
        {
            interiorBounds =
                root.GetComponentInChildren<MapBounds>(true);

            if (interiorBounds != null)
                break;
        }

        InteriorBounds = interiorBounds;

        // 경로 탐색의 이동 가능 영역을 집 내부 경계로 교체한다.
        if (interiorBounds != null && mousePathfinding != null)
        {
            mousePathfinding.SetMapBounds(interiorBounds);
        }
        else
        {
            Debug.LogError(
                "InteriorMapBounds 또는 MousePathfinding이 없습니다."
            );
        }
        if (cameraFollow != null)
        {
            cameraFollow.SetMapBounds(interiorBounds);
            cameraFollow.SnapToPlayer();
        }

        // 마을 화면 숨기기
        if (villageVisualRoot != null)
            villageVisualRoot.SetActive(false);

        if (villageTilemap != null)
            villageTilemap.SetActive(false);

        // 모든 입장 처리가 끝난 후 집 내부 상태를 활성화한다.
        isInsideHouse = true;

        Debug.Log("집 내부 입장 완료");

        isTransitioning = false;
    }

    // 집 퇴장
    // 집 안에 있을 때에만 퇴장 코루틴을 시작한다.
    public void ExitHouse()
    {
        if (isTransitioning || !isInsideHouse)
            return;

        StartCoroutine(ExitHouseRoutine());
    }

    // 경로 탐색 영역과 플레이어 위치를 마을 기준으로 복원하고 집 화면을 숨긴다.
    private IEnumerator ExitHouseRoutine()
    {
        isTransitioning = true;

        if (mousePathfinding != null)
        {
            mousePathfinding.PrepareForSceneTransition();
            // 집 내부 경계 대신 마을의 이동 가능 경계를 다시 적용한다.
            mousePathfinding.RestoreVillageMapBounds();
        }
        // 집에 들어가기 전 마을 위치로 복귀
        if (player != null)
        {
            player.position = villageReturnPosition;

            Debug.Log("플레이어 마을 위치 복구 완료");
        }
        // 마을 카메라 경계 복구
        if (cameraFollow != null)
        {
            cameraFollow.SetMapBounds(villageCameraBounds);
            cameraFollow.SnapToPlayer();
        }

        // 집 내부 오브젝트 숨기기
        // 집 씬은 언로드하지 않고 루트 오브젝트만 비활성화해 재입장에 재사용한다.
        SetHouseObjectsActive(false);


        // 마을 화면 복구
        if (villageVisualRoot != null)
            villageVisualRoot.SetActive(true);

        if (villageTilemap != null)
            villageTilemap.SetActive(true);

        // 마을 화면과 플레이어 위치 복구 후 집 내부 상태를 해제한다.
        isInsideHouse = false;

        Debug.Log("집에서 마을로 복귀 완료");

        isTransitioning = false;

        yield break;
    }

    // 집 내부 Scene의 루트 오브젝트 활성화/비활성화
    // 집 내부 씬의 모든 루트 오브젝트를 일괄 활성화하거나 비활성화한다.
    private void SetHouseObjectsActive(bool active)
    {
        Scene houseScene =
            SceneManager.GetSceneByName(houseSceneName);

        // 집 씬이 유효하지 않거나 로드되지 않았다면 오브젝트 변경을 생략한다.
        if (!houseScene.IsValid() || !houseScene.isLoaded)
            return;

        GameObject[] rootObjects =
            houseScene.GetRootGameObjects();

        foreach (GameObject root in rootObjects)
        {
            root.SetActive(active);
        }
    }
}
