using System;
using System.Collections.Generic;
using UnityEngine;

// 건물 건설, 철거, 이동을 서버(채팅과 같은 TCP 연결)로 주고받아 모든 플레이어 화면에 같은 건물이 보이게 함
//   내가 바꾼 것: BuildingPlacement, BuildingDemolition 이 OnLocalBuilt / OnLocalDemolished 를 불러 서버로 보냄
//   다른 사람이 바꾼 것: 서버에서 받아 화면과 점유 칸(겹쳐 짓기 금지, 경로 탐색)에 반영
public class BuildingSync : MonoBehaviour
{
    public static BuildingSync Instance { get; private set; }

    [SerializeField] private ChatClient chatClient;
    [SerializeField] private BuildingPlacement buildingPlacement;
    [SerializeField] private Grid grid;
    [SerializeField] private Building[] buildingPrefabs; // 건물 종류(BuildingData.buildingID)별 프리팹

    private readonly Dictionary<string, Building> buildings = new Dictionary<string, Building>(); // 네트워크 ID -> 건물

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnEnable()
    {
        chatClient.BuildingReceived += OnMessage;
    }

    void OnDisable()
    {
        chatClient.BuildingReceived -= OnMessage;
    }

    // 내가 지었을 때
    public void OnLocalBuilt(Building building)
    {
        string id = Guid.NewGuid().ToString("N");
        var msg = new BuildingMessage
        {
            t = BuildingMessage.Build, id = id, type = building.BuildingID,
            x = building.OriginCell.x, y = building.OriginCell.y,
        };
        if (chatClient.SendBuilding(msg))
            buildings[id] = building;
        else
            Debug.LogWarning("서버에 연결되어 있지 않아 다른 플레이어에게는 이 건물이 보이지 않습니다.");
    }

    // 내가 철거했을 때 (건물을 지우기 전에 호출)
    public void OnLocalDemolished(Building building)
    {
        string id = IdOf(building);
        if (id == null) return;

        buildings.Remove(id);
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

    void OnMessage(BuildingMessage m)
    {
        switch (m.t)
        {
            case BuildingMessage.Build:
                Spawn(m.id, m.type, m.x, m.y);
                break;
            case BuildingMessage.Demolish:
                Remove(m.id);
                break;
            case BuildingMessage.Move:
                MoveTo(m.id, m.x, m.y);
                break;
            case BuildingMessage.List:
                Reconcile(m.list);
                break;
        }
    }

    // 입장(다시 접속) 때 받은 목록에 맞춤: 없는 건물은 짓고, 위치가 다르면 옮기고, 서버에 없는 건물은 지움
    void Reconcile(BuildingInfo[] list)
    {
        var alive = new HashSet<string>();
        foreach (BuildingInfo b in list)
        {
            alive.Add(b.id);
            if (!buildings.TryGetValue(b.id, out Building existing))
                Spawn(b.id, b.type, b.x, b.y);
            else if (existing != null && existing.OriginCell != new Vector3Int(b.x, b.y, 0))
                MoveTo(b.id, b.x, b.y);
        }

        foreach (string id in new List<string>(buildings.Keys))
        {
            if (!alive.Contains(id)) Remove(id);
        }
    }

    void Spawn(string id, string type, int x, int y)
    {
        if (string.IsNullOrEmpty(id) || buildings.ContainsKey(id)) return;

        Building prefab = Array.Find(buildingPrefabs, p => p != null && p.BuildingID == type);
        if (prefab == null)
        {
            Debug.LogWarning("알 수 없는 건물 종류라서 표시하지 못했습니다: " + type);
            return;
        }

        var cell = new Vector3Int(x, y, 0);
        Building building = Instantiate(prefab, PositionOf(cell, prefab), Quaternion.identity);
        building.Initialize(cell);
        buildingPlacement.OccupyBuildingCells(building);
        buildings[id] = building;
    }

    void Remove(string id)
    {
        if (!buildings.TryGetValue(id, out Building building)) return;

        buildings.Remove(id);
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
