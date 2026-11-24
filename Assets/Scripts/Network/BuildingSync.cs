using System;
using System.Collections.Generic;
using UnityEngine;

// 건물 건설, 철거, 이동을 서버(채팅과 같은 TCP 연결)로 주고받아 같은 월드에 있는 모든 플레이어 화면에 같은 건물이 보이게 함
//   건물은 서버 DB 에 월드(광장, 각자의 마을)별로 저장된다. 월드에 들어가면 서버가 그 월드의 건물 목록을 보내 준다
//   내가 바꾼 것: BuildingPlacement, BuildingDemolition 이 OnLocalBuilt / OnLocalDemolished 를 불러 서버로 보냄
//     화면에는 먼저 반영하고, 서버가 거절하면(자원 부족, 남의 건물, 구경만 가능한 마을 등) 서버가 다시 보내는 목록으로 되돌린다
//   다른 사람이 바꾼 것: 서버에서 받아 화면과 점유 칸(겹쳐 짓기 금지, 경로 탐색)에 반영
//   건설 비용, 철거 환급, 건물 수확은 서버가 계산한다 (ResourceSync)
//   수확(BuildingHarvest: 건물 위에 뜨는 수확 버튼)은 내가 지은 건물에서만 한다
//     수확 버튼을 누르면 BuildingHarvest 가 OnLocalHarvest 를 불러 서버로 보내고, 서버가 수확량을 재화에 더한다
//     수확 타이머도 서버가 기준이다: 월드에 들어가면 서버가 내 건물의 남은 시간을 알려 주고, 화면의 타이머를 거기에 맞춘다
public class BuildingSync : MonoBehaviour
{
    public static BuildingSync Instance { get; private set; }

    // 서버에서 받은 건물 배치가 바뀜 (목록 수신, 다른 사람의 건설·이동). MousePathfinding 이 플레이어가 건물에 끼지 않게 확인한다
    public static event Action LayoutChanged;

    // 건물이 아닌 것(예: 광장 풍경)이 칸을 막거나 풀었을 때도 같은 알림을 보낸다
    public static void NotifyLayoutChanged() => LayoutChanged?.Invoke();

    private const float HarvestTolerance = 1f; // 화면의 수확 타이머가 서버와 이만큼(초) 넘게 어긋나면 맞춘다

    [SerializeField] private ChatClient chatClient;
    [SerializeField] private BuildingPlacement buildingPlacement;
    [SerializeField] private Grid grid;
    [SerializeField] private Building[] buildingPrefabs; // 건물 종류(BuildingData.buildingID)별 프리팹
    [SerializeField] private Transform buildingsParent;  // 서버에서 받은 건물을 넣을 곳 (BuildingPlacement 의 placedBuildingsParent 와 같은 오브젝트)
    [SerializeField] private PlayerResources playerResources;

    private readonly Dictionary<string, Building> buildings = new Dictionary<string, Building>(); // 네트워크 ID -> 건물
    private readonly Dictionary<string, int> numbers = new Dictionary<string, int>(); // 건물 종류 -> 마지막으로 붙인 번호
    private readonly Dictionary<string, HarvestState> harvests = new Dictionary<string, HarvestState>(); // 네트워크 ID -> 내 건물의 수확 타이머
    private long world = -1; // 지금 화면에 있는 건물들의 월드 id

    // 집 안에 놓인 건물을 담는 곳. 집 안에 있을 때만 켜진다 (마을 건물은 buildingsParent, 집에 들어가면 마을과 함께 숨겨짐)
    private Transform houseBuildingsRoot;

    // 집 안 건물을 담을 곳 (BuildingPlacement 가 집 안에 지을 때 씀)
    public Transform HouseBuildingsRoot => houseBuildingsRoot;

    // 내 건물의 수확 타이머
    private class HarvestState
    {
        public BuildingHarvest harvest;
        public float readyAt;       // 수확할 수 있게 되는 시각 (Time.realtimeSinceStartup 기준)
        public bool awaitingServer; // 수확을 요청하고 서버가 다음 타이머를 알려 주기를 기다리는 중
    }

    void Awake()
    {
        Instance = this;

        houseBuildingsRoot = new GameObject("HouseBuildings").transform;
        houseBuildingsRoot.SetParent(transform.parent, false);
        houseBuildingsRoot.gameObject.SetActive(false);
    }

    // 집 안 건물은 집 안에 있을 때만 보이고 클릭된다
    void LateUpdate()
    {
        bool inHouse = HouseSceneManager.Instance != null && HouseSceneManager.Instance.IsInsideHouse;

        if (houseBuildingsRoot != null && houseBuildingsRoot.gameObject.activeSelf != inHouse)
            houseBuildingsRoot.gameObject.SetActive(inHouse);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnEnable()
    {
        chatClient.BuildingReceived += OnMessage;
        chatClient.WorldEntered += OnWorldEntered;
        chatClient.HarvestReceived += OnHarvestTimers;
    }

    void OnDisable()
    {
        chatClient.BuildingReceived -= OnMessage;
        chatClient.WorldEntered -= OnWorldEntered;
        chatClient.HarvestReceived -= OnHarvestTimers;
    }

    // 건물 종류(BuildingData.buildingID)의 BuildingData, 등록되지 않은 종류면 null
    public BuildingData FindData(string type)
    {
        Building prefab = Array.Find(buildingPrefabs, p => p != null && p.BuildingID == type);
        return prefab != null ? prefab.Data : null;
    }

    // 내가 지었을 때
    public void OnLocalBuilt(Building building)
    {
        StopLocalProduction(building);

        string id = Guid.NewGuid().ToString("N");
        var msg = new BuildingMessage
        {
            t = BuildingMessage.Build, id = id, type = building.BuildingID,
            x = building.OriginCell.x, y = building.OriginCell.y,
        };
        if (!chatClient.SendBuilding(msg))
        {
            // 건물과 비용은 서버가 관리하므로 연결되어 있지 않으면 지을 수 없다: 방금 지은 것을 되돌림
            UndoLocalBuild(building);
            Toast.Show("서버에 연결되어 있지 않아 건물을 지을 수 없습니다.");
            return;
        }

        buildings[id] = building;
        building.SetBuildingNumber(NextNumber(building.BuildingID));

        // 수확 타이머: 서버도 지은 순간부터 수확 주기를 센다 (BuildingHarvest 는 BuildingPlacement 가 붙여 둠)
        BuildingHarvest harvest = building.GetComponent<BuildingHarvest>();
        if (harvest != null)
            harvests[id] = new HarvestState { harvest = harvest, readyAt = Time.realtimeSinceStartup + harvest.RemainingTime };

        // 장식물은 장식 아이템 1개를 써서 짓는다: 줄어든 1개가 버린 것으로 서버에 가지 않게 알려 둔다
        if (building.Data != null && building.Data.UsesDecorationItem && ResourceSync.Instance != null)
            ResourceSync.Instance.OnDecorationPlaced(building.Data);
    }

    // 내가 수확 버튼을 눌렀을 때 (BuildingHarvest 가 자원을 주기 전에 호출). 서버로 보냈으면 true
    //   true 면 BuildingHarvest 가 화면의 재화에 먼저 더하고, 서버가 더한 값이 재화 알림으로 온다
    //   false 면 수확하지 않는다: 서버에 연결되어 있지 않거나, 서버가 내 건물이라고 알려 준 적이 없는 건물
    public bool OnLocalHarvest(BuildingHarvest harvest)
    {
        // 광장은 공용 공간이라 건물을 수확할 수 없다 (서버도 거절한다)
        if (world == WorldSync.Multiverse)
        {
            Toast.Show("광장의 건물은 수확할 수 없습니다.");
            return false;
        }

        string id = IdOf(harvest.GetComponent<Building>());
        if (id == null || !harvests.TryGetValue(id, out HarvestState state)) return false;

        if (!chatClient.SendHarvest(id))
        {
            Toast.Show("서버에 연결되어 있지 않아 수확할 수 없습니다.");
            return false;
        }

        state.awaitingServer = true; // 다음 타이머는 서버의 답(OnHarvestTimers)으로 정해진다
        return true;
    }

    // 내가 철거했을 때 (건물을 지우기 전에 호출)
    public void OnLocalDemolished(Building building)
    {
        string id = IdOf(building);
        if (id == null) return;

        buildings.Remove(id);
        harvests.Remove(id);
        chatClient.SendBuilding(new BuildingMessage { t = BuildingMessage.Demolish, id = id });
    }

    // 내가 옮겼을 때 (건물 이동 기능을 만들면 옮긴 뒤 호출)
    public void OnLocalMoved(Building building)
    {
        string id = IdOf(building);
        if (id == null) return;

        chatClient.SendBuilding(new BuildingMessage
        {
            t = BuildingMessage.Move, id = id, x = building.OriginCell.x, y = building.OriginCell.y,
        });
    }

    // 다른 월드에 들어가면 있던 월드의 건물을 모두 지운다 (이어서 오는 목록으로 새 월드의 건물이 채워짐)
    void OnWorldEntered(WorldMessage m)
    {
        if (m.id == world) return; // 다시 접속해 같은 월드로 돌아온 경우: 목록으로 맞추기만 하면 됨

        world = m.id;
        foreach (string id in new List<string>(buildings.Keys))
            Remove(id);
        numbers.Clear();
    }

    void OnMessage(BuildingMessage m)
    {
        switch (m.t)
        {
            case BuildingMessage.Build:
                Spawn(m.id, m.type, m.x, m.y, m.zone);
                LayoutChanged?.Invoke();
                break;
            case BuildingMessage.Demolish:
                Remove(m.id);
                break;
            case BuildingMessage.Move:
                MoveTo(m.id, m.x, m.y);
                LayoutChanged?.Invoke();
                break;
            case BuildingMessage.List:
                Reconcile(m.list);
                break;
        }
    }

    // 서버의 목록에 맞춤: 없는 건물은 짓고, 위치가 다르면 옮기고, 서버에 없는 건물은 지움
    //   월드에 들어갈 때, 다시 접속했을 때, 내 요청이 거절되어 화면을 되돌릴 때 온다
    void Reconcile(BuildingInfo[] list)
    {
        var alive = new HashSet<string>();
        foreach (BuildingInfo b in list ?? new BuildingInfo[0])
        {
            alive.Add(b.id);
            if (!buildings.TryGetValue(b.id, out Building existing))
                Spawn(b.id, b.type, b.x, b.y, b.zone);
            else if (existing != null && existing.OriginCell != new Vector3Int(b.x, b.y, 0))
                MoveTo(b.id, b.x, b.y);
        }

        foreach (string id in new List<string>(buildings.Keys))
        {
            if (!alive.Contains(id)) Remove(id);
        }

        LayoutChanged?.Invoke();
    }

    void Spawn(string id, string type, int x, int y, string zone)
    {
        if (string.IsNullOrEmpty(id) || buildings.ContainsKey(id)) return;

        Building prefab = Array.Find(buildingPrefabs, p => p != null && p.BuildingID == type);
        if (prefab == null)
        {
            Debug.LogWarning("알 수 없는 건물 종류라서 표시하지 못했습니다: " + type);
            return;
        }

        var cell = new Vector3Int(x, y, 0);
        bool inHouse = zone == Building.HouseZone;
        Transform parent = inHouse ? houseBuildingsRoot : buildingsParent;
        Building building = Instantiate(prefab, PositionOf(cell, prefab), Quaternion.identity, parent);
        building.SetZone(zone);
        building.Initialize(cell);
        building.SetBuildingNumber(NextNumber(type));
        StopLocalProduction(building);
        buildingPlacement.OccupyBuildingCells(building);
        buildings[id] = building;
    }

    // 서버가 알려 준 내 건물의 수확 타이머: 그 건물에 수확 버튼(BuildingHarvest)을 붙이고 남은 시간을 기억해 둔다
    //   남은 시간은 Update 가 BuildingHarvest 에 반영한다 (방금 붙인 BuildingHarvest 는 Start 에서 타이머를 처음으로 되돌리므로)
    void OnHarvestTimers(HarvestMessage m)
    {
        // 광장은 공용 공간이라 수확하지 않는다: 서버가 예전 버전이라 타이머를 보내도 무시하고 수확 버튼을 모두 뗀다
        bool multiverse = world == WorldSync.Multiverse;
        HarvestTimer[] list = multiverse ? new HarvestTimer[0] : m.list ?? new HarvestTimer[0];

        var listed = new HashSet<string>();
        foreach (HarvestTimer timer in list)
        {
            listed.Add(timer.id);
            if (!buildings.TryGetValue(timer.id, out Building building) || building == null) continue;

            if (!harvests.TryGetValue(timer.id, out HarvestState state))
            {
                BuildingHarvest harvest = building.GetComponent<BuildingHarvest>();
                if (harvest == null) harvest = building.gameObject.AddComponent<BuildingHarvest>();
                harvests[timer.id] = state = new HarvestState { harvest = harvest };
            }
            state.readyAt = Time.realtimeSinceStartup + timer.remaining;
            state.awaitingServer = false;
        }
        if (!m.all && !multiverse) return;

        // 전체 목록에 없는 건물은 수확할 수 없다: 수확 버튼을 뗀다
        foreach (string id in new List<string>(harvests.Keys))
        {
            if (listed.Contains(id)) continue;
            if (harvests[id].harvest != null) Destroy(harvests[id].harvest);
            harvests.Remove(id);
        }
    }

    // 화면의 수확 타이머를 서버가 알려 준 시각에 맞춘다
    //   BuildingHarvest 는 건물이 켜져 있는 동안의 게임 시간으로 세므로(필드나 집 안에 가 있으면 멈춘다) 서버와 어긋날 수 있다
    void Update()
    {
        foreach (HarvestState state in harvests.Values)
        {
            if (state.awaitingServer || state.harvest == null) continue;

            float expected = Mathf.Max(0f, state.readyAt - Time.realtimeSinceStartup);
            bool differs = state.harvest.IsReady
                ? expected > HarvestTolerance
                : Mathf.Abs(state.harvest.RemainingTime - expected) > HarvestTolerance;
            if (differs) state.harvest.SetRemainingTime(expected);
        }
    }

    void Remove(string id)
    {
        if (!buildings.TryGetValue(id, out Building building)) return;

        buildings.Remove(id);
        harvests.Remove(id);
        if (building == null) return; // 이미 지워진 건물

        buildingPlacement.ReleaseBuildingCells(building);
        Destroy(building.gameObject);
    }

    void MoveTo(string id, int x, int y)
    {
        if (!buildings.TryGetValue(id, out Building building) || building == null) return;

        var cell = new Vector3Int(x, y, 0);
        buildingPlacement.ReleaseBuildingCells(building);
        building.Initialize(cell);
        building.transform.position = PositionOf(cell, building);
        buildingPlacement.OccupyBuildingCells(building);
    }

    // 서버에 보내지 못한 건물을 없던 일로: 칸을 비우고, BuildingPlacement 가 쓴 것(건설 비용, 장식물은 장식 아이템 1개)을 돌려놓고, 건물을 지움
    void UndoLocalBuild(Building building)
    {
        buildingPlacement.ReleaseBuildingCells(building);
        BuildingData data = building.Data;
        if (data != null && data.UsesDecorationItem)
        {
            if (DecorationInventory.Instance != null) DecorationInventory.Instance.AddDecoration(data, 1);
        }
        else if (playerResources != null && data != null && data.constructionCosts != null)
        {
            foreach (ResourceAmount cost in data.constructionCosts)
                playerResources.AddResource(cost.resourceType, cost.amount);
        }
        Destroy(building.gameObject);
    }

    // 재화는 서버가 계산해서 알려 주므로, 건물이 화면에서 따로 자원을 더하지 않게 자동 생산(BuildingProduction)을 끈다
    //   (수확 버튼 BuildingHarvest 는 OnLocalHarvest 를 거쳐 서버로 보낸다)
    static void StopLocalProduction(Building building)
    {
        BuildingProduction production = building.GetComponent<BuildingProduction>();
        if (production != null) production.enabled = false;
    }

    // 건물 번호 (같은 종류끼리 1, 2, 3 ...). 서버 목록의 순서(지은 순서)대로 붙는다
    int NextNumber(string type)
    {
        numbers.TryGetValue(type ?? "", out int last);
        numbers[type ?? ""] = last + 1;
        return last + 1;
    }

    // 기준 칸에 놓을 때의 위치 (BuildingPlacement 의 미리보기 위치와 같은 계산)
    Vector3 PositionOf(Vector3Int cell, Building building)
    {
        Vector3 center = grid.GetCellCenterWorld(cell);
        float offsetX = (building.Width - 1) * grid.cellSize.x / 2f;
        float offsetY = (building.Height - 1) * grid.cellSize.y / 2f;
        return center + new Vector3(offsetX, offsetY, 0);
    }

    string IdOf(Building building)
    {
        foreach (KeyValuePair<string, Building> pair in buildings)
        {
            if (pair.Value == building) return pair.Key;
        }
        return null;
    }
}
