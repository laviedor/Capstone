using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// 마을 씬을 유지한 채 필드 씬을 추가 로드하거나 해제하는 전환 관리자입니다.
public class FieldSceneManager : MonoBehaviour
{
    // 다른 UI에서 씬 전환을 요청할 수 있도록 제공하는 단일 접근 지점입니다.
    public static FieldSceneManager Instance { get; private set; }

    [Header("마을에서 숨길 오브젝트")]
    // 필드로 이동하면 화면에서 숨길 마을 시각 오브젝트의 루트입니다.
    [SerializeField] private GameObject villageVisualRoot;

    [Header("마을 카메라")]
    // 기존 마을 카메라 참조입니다. 현재 전환 코드에서는 비활성화하지 않습니다.
    [SerializeField] private Camera villageCamera;

    [Header("마을 바닥 Tilemap")]
    // 필드로 이동할 때 숨기고 돌아오면 다시 표시할 마을 바닥입니다.
    [SerializeField] private GameObject villageTilemap;

    // 마을 바닥 타일맵 (삽으로 바꾼 칸은 그 위에 덧칠한다: GroundTileLayer)
    public GameObject VillageTilemap => villageTilemap;

    [Header("플레이어 (비어 있으면 자동으로 찾음)")]
    // 필드에 들어가기 전 마을 위치를 저장했다가 돌아올 때 되돌릴 플레이어입니다.
    [SerializeField] private Transform player;
    // 씬 전환 전 이동·경로 표시를 정리하고, 돌아온 뒤 건물에 끼지 않았는지 확인합니다.
    [SerializeField] private MousePathfinding mousePathfinding;
    // 돌아온 뒤 카메라를 플레이어 위치로 바로 옮깁니다.
    [SerializeField] private CameraFollow cameraFollow;

    // 필드에 들어가기 직전의 마을 위치 (돌아올 때 이 자리로 돌아온다)
    private Vector3 villageReturnPosition;
    private bool hasVillageReturnPosition;

    // 씬 로드/언로드가 진행 중일 때 중복 요청을 막는 상태값입니다.
    private bool isTransitioning = false;
    // 현재 추가 로드된 필드 씬의 이름입니다. 빈 문자열이면 마을 상태입니다.
    private string currentFieldScene = "";

    // 필드 씬 이름이 저장되어 있는지 확인하여 현재 필드 진입 여부를 반환합니다.
    public bool IsInField
    {
        get { return !string.IsNullOrEmpty(currentFieldScene); }
    }

    // 중복 매니저가 생성되면 새 오브젝트를 제거하고 최초 인스턴스를 유지합니다.
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // 전환 중이거나 이미 필드에 있으면 요청을 무시하고, 유효한 씬 이름이면 로드를 시작합니다.
    public void MoveToField(string sceneName)
    {
        if (isTransitioning || !string.IsNullOrEmpty(currentFieldScene))
            return;

        if (string.IsNullOrEmpty(sceneName))
            return;

        FindReferences();

        // 탐색 창 등 열려 있는 창을 닫는다
        CloseWindows();

        // 이동 중이던 경로와 채집을 정리하고, 지금 마을 위치를 기억해 둔다
        if (mousePathfinding != null)
            mousePathfinding.PrepareForSceneTransition();

        if (player != null)
        {
            villageReturnPosition = player.position;
            hasVillageReturnPosition = true;
        }

        StartCoroutine(LoadFieldRoutine(sceneName));
    }

    // 비어 있는 참조를 씬에서 찾는다
    private void FindReferences()
    {
        if (player == null && HouseSceneManager.Instance != null)
            player = HouseSceneManager.Instance.Player;

        if (mousePathfinding == null)
            mousePathfinding = FindFirstObjectByType<MousePathfinding>();

        if (cameraFollow == null)
            cameraFollow = FindFirstObjectByType<CameraFollow>();
    }

    // 장소를 옮길 때는 열려 있는 창(인벤토리·건설·탐색)과 집 이동 버튼을 닫는다
    private static void CloseWindows()
    {
        InventoryUI inventoryUI = FindFirstObjectByType<InventoryUI>();

        if (inventoryUI != null)
            inventoryUI.CloseInventory();

        HouseMovePopup.Hide();
    }

    // 필드 씬을 Additive 방식으로 비동기 로드한 후 마을의 화면 표시를 숨깁니다.
    private IEnumerator LoadFieldRoutine(string sceneName)
    {
        isTransitioning = true;

        // 숲 Scene을 추가로 불러온다.
        // Unity의 비동기 씬 작업을 시작하고 완료 여부를 확인합니다.
        AsyncOperation operation =
            SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

        // 씬 작업을 시작하지 못했을 때 오류를 남기고 전환 상태를 해제합니다.
        if (operation == null)
        {
            Debug.LogError("필드 Scene 로드 실패: " + sceneName);
            isTransitioning = false;
            yield break;
        }

        // 로드 또는 언로드가 완료될 때까지 매 프레임 기다립니다.
        while (!operation.isDone)
            yield return null;

        // 마을의 화면 표시만 끈다.
        if (villageVisualRoot != null)
            villageVisualRoot.SetActive(false);

        if (villageTilemap != null)
            villageTilemap.SetActive(false);

        // 기존 추적 카메라를 계속 사용한다.
        // if (villageCamera != null)
        //     villageCamera.gameObject.SetActive(false);

        // 성공적으로 로드한 필드 이름을 기록하여 이후 복귀 대상에 사용합니다.
        currentFieldScene = sceneName;

        Debug.Log("필드 이동 완료: " + sceneName);

        isTransitioning = false;
    }

    // 필드에 있을 때만 마을 복귀 코루틴을 실행합니다. 필드에 들어가기 전 마을 위치로 돌아옵니다.
    public void ReturnToVillage()
    {
        ReturnToVillage(true);
    }

    // restorePosition 이 false 면 위치는 되돌리지 않는다 (다른 월드로 옮겨 시작 자리에 세울 때: WorldSync)
    public void ReturnToVillage(bool restorePosition)
    {
        if (isTransitioning || string.IsNullOrEmpty(currentFieldScene))
            return;

        FindReferences();
        CloseWindows();

        if (mousePathfinding != null)
            mousePathfinding.PrepareForSceneTransition();

        if (!restorePosition)
            hasVillageReturnPosition = false;

        StartCoroutine(ReturnRoutine());
    }

    // 현재 필드 씬을 비동기로 해제한 뒤 마을 오브젝트를 다시 표시합니다.
    private IEnumerator ReturnRoutine()
    {
        isTransitioning = true;

        // 해제할 필드 씬 이름을 현재 상태에서 가져옵니다.
        string sceneToUnload = currentFieldScene;

        AsyncOperation operation =
            SceneManager.UnloadSceneAsync(sceneToUnload);

        if (operation == null)
        {
            Debug.LogError("필드 Scene 언로드 실패: " + sceneToUnload);
            isTransitioning = false;
            yield break;
        }

        while (!operation.isDone)
            yield return null;

        if (villageVisualRoot != null)
            villageVisualRoot.SetActive(true);

        if (villageTilemap != null)
            villageTilemap.SetActive(true);

        //if (villageCamera != null)
        //    villageCamera.gameObject.SetActive(true);

        // 필드 이름을 비워 마을에 있는 상태로 전환합니다.
        currentFieldScene = "";

        // 필드에 들어가기 전 마을 위치로 되돌린다 (필드에서 돌아다닌 위치에 그대로 서 있지 않도록)
        if (hasVillageReturnPosition && player != null)
        {
            if (mousePathfinding != null)
                mousePathfinding.PrepareForSceneTransition();

            player.position = villageReturnPosition;
            Debug.Log("플레이어 마을 위치 복구 완료");
        }

        hasVillageReturnPosition = false;

        // 필드에 있는 동안 그 자리에 건물이 생겼으면 가까운 빈칸으로 옮긴다
        if (mousePathfinding != null)
            mousePathfinding.EnsurePlayerOnWalkableCell();

        if (cameraFollow != null)
            cameraFollow.SnapToPlayer();

        Debug.Log("마을 복귀 완료");

        isTransitioning = false;
    }
}
