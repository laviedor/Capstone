using System;
using UnityEngine;

// 지금 있는 월드 관리: 광장(모두가 같이 있는 곳), 내 마을, 친구 마을(구경만)
//   이동은 서버(채팅 연결)에 요청하고, 서버가 들어간 월드를 알려 주면 화면을 그 월드로 바꾼다
//   건물은 서버가 이어서 보내는 목록으로 BuildingSync 가 다시 채우고, 다른 플레이어는 같은 월드에 있는 사람만 보인다
//   월드 id: 0 은 광장, 그 외에는 그 유저(id)의 마을 (서버 worlds.js 참고)
//   월드 안에는 구역이 있다: 마을, 필드(FieldSceneManager), 집 안(HouseSceneManager). 필드나 집에 들어가면 서버에 알려서
//   같은 구역에 있는 사람끼리만 서로 보이게 한다 (필드와 집은 마을과 같은 좌표를 쓰므로 섞이면 안 된다)
public class WorldSync : MonoBehaviour
{
    public const long Multiverse = 0;

    public static WorldSync Instance { get; private set; }

    [SerializeField] private ChatClient chatClient;
    [SerializeField] private RemotePlayerManager remotePlayers;
    [SerializeField] private Transform player;
    [SerializeField] private PlayerPathMovement playerMovement;
    [SerializeField] private BuildingModeManager buildingModeManager;

    public long Current { get; private set; } = -1;     // 지금 있는 월드 id (서버가 알려 주기 전에는 -1)
    public string OwnerName { get; private set; } = ""; // 마을 주인 닉네임 (광장은 "")
    public bool IsMine { get; private set; }            // 내 마을인지
    public bool CanBuild { get; private set; }          // 여기서 건물을 지을 수 있는지 (친구 마을은 구경만)
    public string Zone { get; private set; } = "";      // 월드 안의 구역: "" 마을, "field" 필드, "house" 집 안

    public bool Entered => Current >= 0;
    public bool InMultiverse => Current == Multiverse;

    // 여기서 건물을 짓거나 철거, 이동할 수 없는지 (친구 마을은 구경만, 광장은 건물을 바꿀 수 없음)
    public bool BuildingLocked => Entered && (!CanBuild || InMultiverse);

    // 건물을 바꿀 수 없을 때 띄우는 알림
    public string BuildingLockedMessage =>
        InMultiverse ? "광장에서는 건물을 짓거나 철거할 수 없습니다." : "친구의 마을에서는 구경만 할 수 있습니다.";

    // 지금 월드에서 건물을 바꿀 수 없으면 알림을 띄우고 true (건설, 철거를 시작하기 전에 부른다)
    public static bool BlockBuildingChange()
    {
        WorldSync world = Instance;
        if (world == null || !world.BuildingLocked) return false;

        Toast.Show(world.BuildingLockedMessage);
        return true;
    }

    // 화면에 보여 줄 지금 위치
    public string Label
    {
        get
        {
            if (!Entered) return "접속 중...";
            string place = InMultiverse ? "광장" : IsMine ? "내 마을" : OwnerName + "님의 마을 (구경 중)";
            if (Zone == "field") place += " - 필드";
            else if (Zone == "house") place += " - 집 안";
            return place;
        }
    }

    public event Action Changed; // 월드에 들어가거나 구역을 옮김 (다시 접속해 같은 월드에 돌아온 경우 포함)

    private Vector3 spawnPosition; // 월드에 들어가면 서는 자리 (씬에 놓인 캐릭터의 처음 위치)
    private string serverZone = ""; // 서버가 알고 있는 내 구역 (월드에 들어가면 서버에서는 마을로 돌아간다)

    void Awake()
    {
        Instance = this;
        spawnPosition = player.position;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnEnable()
    {
        LoginUI.LoggedIn += OnLoggedIn;
        chatClient.WorldEntered += OnWorldEntered;
    }

    void OnDisable()
    {
        LoginUI.LoggedIn -= OnLoggedIn;
        chatClient.WorldEntered -= OnWorldEntered;
    }

    // 로그인하면 내 마을에서 시작
    void OnLoggedIn()
    {
        chatClient.JoinWorld = LoginUI.UserId;
    }

    public void GoHome() => Enter(LoginUI.UserId);

    public void GoMultiverse() => Enter(Multiverse);

    // 월드 이동 요청 (친구 마을은 그 친구의 유저 id). 서버가 받아들이면 OnWorldEntered 로 이어진다
    public void Enter(long world)
    {
        if (world == Current) return;
        if (!chatClient.SendEnter(world))
            Toast.Show("서버에 연결되어 있지 않아 이동할 수 없습니다.");
    }

    void OnWorldEntered(WorldMessage m)
    {
        bool moved = m.id != Current;
        Current = m.id;
        OwnerName = m.name ?? "";
        IsMine = m.mine;
        CanBuild = m.build;
        chatClient.JoinWorld = m.id; // 연결이 끊겼다 다시 붙으면 이 월드로 돌아옴
        serverZone = "";            // 서버는 월드에 들어온 접속을 마을에 있는 것으로 본다 (UpdateZone 이 다시 맞춘다)

        if (moved)
        {
            // 있던 월드의 다른 플레이어를 지우고, 필드나 집 안이었으면 마을로 나와서, 들어간 월드의 시작 자리에 선다
            remotePlayers.Clear();
            if (HouseSceneManager.Instance != null && HouseSceneManager.Instance.IsInsideHouse)
                HouseSceneManager.Instance.ExitHouse();
            else if (FieldSceneManager.Instance != null && FieldSceneManager.Instance.IsInField)
                FieldSceneManager.Instance.ReturnToVillage(false); // 위치는 아래에서 새 월드의 시작 자리로 정한다
            playerMovement.StopMovement();
            player.position = spawnPosition;
        }
        Changed?.Invoke();
    }

    // 필드나 집에 들어가고 나온 것을 서버에 알린다 (main 의 FieldSceneManager, HouseSceneManager 상태를 보고 판단)
    void UpdateZone()
    {
        if (!Entered) return;

        string zone = "";
        if (HouseSceneManager.Instance != null && HouseSceneManager.Instance.IsInsideHouse) zone = "house";
        else if (FieldSceneManager.Instance != null && FieldSceneManager.Instance.IsInField) zone = "field";

        if (zone != Zone)
        {
            Zone = zone;
            remotePlayers.Clear(); // 있던 구역의 사람들은 더 이상 보이지 않는다
            Changed?.Invoke();
        }
        // 서버가 아는 구역과 다르면 알린다 (연결이 끊겨 못 보냈으면 다시 연결된 뒤에 보내진다)
        if (zone != serverZone && chatClient.SendZone(zone))
            serverZone = zone;
    }

    void Update()
    {
        UpdateZone();

        // 친구 마을, 광장에서는 건설 모드를 켤 수 없다 (서버도 건설, 철거 요청을 거절한다)
        if (!BuildingLocked || !buildingModeManager.IsBuildMode) return;

        buildingModeManager.SendMessage("ToggleBuildMode", SendMessageOptions.DontRequireReceiver);
        Toast.Show(BuildingLockedMessage);
    }
}
