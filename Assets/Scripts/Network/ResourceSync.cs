using System;
using System.Collections.Generic;
using UnityEngine;

// 서버가 알려 주는 재화를 PlayerResources(화면에 보이는 재화)와 DecorationInventory(가진 장식물 수)에 반영
//   재화는 서버의 값이 기준이다: 건설 비용, 철거 환급, 건물 수확, 채집 보상을 서버가 계산하고 바뀔 때마다 전부 알려 준다
//   BuildingPlacement, BuildingDemolition, BuildingHarvest, ResourceNode, GatheringTimer 가 화면에 먼저 반영해 둔 값도 서버 값으로 맞춰진다
//   장식물은 서버 인벤토리에 장식물 건물 종류(buildingID)와 같은 이름의 아이템으로 들어 있다
//   대장간에서 만든 도구(예: shovel)는 ToolInventory 에 넣는다
//   장식물을 버리면(플레이어 탭의 [버리기]) 화면에서 먼저 1개가 줄어든다: 서버가 모르는 채로 줄어든 장식물은 버린 것으로 서버에 알린다
public class ResourceSync : MonoBehaviour
{
    public static ResourceSync Instance { get; private set; }

    [SerializeField] private ChatClient chatClient;
    [SerializeField] private PlayerResources playerResources;

    private readonly Dictionary<BuildingData, int> decorations = new Dictionary<BuildingData, int>(); // 서버와 맞춰 둔 장식물 수
    private readonly Dictionary<BuildingData, int> placed = new Dictionary<BuildingData, int>();      // 이번 프레임에 지어서 쓴 장식물 수
    private DecorationInventory watched; // 변경 알림을 받고 있는 인벤토리
    private bool changed;                // 장식물 수가 바뀌었는데 아직 확인하지 않음 (서버 값을 반영한 것은 빼고)
    private bool applying;               // 서버 값을 반영하는 중

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (watched != null) watched.OnInventoryChanged -= OnInventoryChanged;
    }

    void OnEnable()
    {
        chatClient.ResourcesReceived += OnResources;
    }

    void OnDisable()
    {
        chatClient.ResourcesReceived -= OnResources;
    }

    void LateUpdate()
    {
        DecorationInventory inventory = DecorationInventory.Instance;
        if (inventory != watched)
        {
            if (watched != null) watched.OnInventoryChanged -= OnInventoryChanged;
            watched = inventory;
            if (watched != null) watched.OnInventoryChanged += OnInventoryChanged;
        }

        ReportDiscards();
    }

    void OnInventoryChanged()
    {
        if (!applying) changed = true;
    }

    // 장식물을 지어서 장식 아이템 1개를 썼을 때 (BuildingSync 가 서버로 보낸 뒤 호출): 버린 것으로 세지 않는다
    public void OnDecorationPlaced(BuildingData data)
    {
        placed.TryGetValue(data, out int used);
        placed[data] = used + 1;
    }

    void OnResources(ResourceMessage m)
    {
        if (m.items == null) return;

        ReportDiscards(); // 서버 값으로 맞추기 전에, 방금 버린 것부터 서버에 알린다
        applying = true;
        try
        {
            foreach (ResourceItem item in m.items)
            {
                if (TryGetType(item.id, out ResourceType type))
                    SetAmount(type, item.n);
                else if (CraftingRecipes.Find(item.id) != null)
                    ToolInventory.Ensure().SetCount(item.id, item.n); // 대장간에서 만든 도구 (예: 삽)
                else
                    SetDecorationCount(item.id, item.n);
            }
        }
        finally
        {
            applying = false;
        }
    }

    // PlayerResources 에는 수량을 바로 정하는 기능이 없어서, 지금 값과의 차이만큼 더하거나 뺀다
    void SetAmount(ResourceType type, int amount)
    {
        int delta = amount - playerResources.GetAmount(type);
        if (delta > 0) playerResources.AddResource(type, delta);
        else if (delta < 0) playerResources.SpendResource(type, -delta);
    }

    // 가진 장식물 수를 서버 값으로 맞춤 (자원이 아닌 아이템 중 장식물 건물 종류와 같은 이름인 것)
    void SetDecorationCount(string id, int count)
    {
        DecorationInventory inventory = DecorationInventory.Instance;
        BuildingData data = BuildingSync.Instance != null ? BuildingSync.Instance.FindData(id) : null;
        if (inventory == null || data == null || !data.UsesDecorationItem) return;

        int have = inventory.GetCount(data);
        if (count > have) inventory.AddDecoration(data, count - have);
        for (int i = count; i < have; i++) inventory.TryConsumeDecoration(data);
        decorations[data] = count;
    }

    // 서버가 모르는 채로 줄어든 장식물을 버린 것으로 서버에 알린다 (서버가 인벤토리에서 빼고 재화를 다시 알려 준다)
    //   지어서 쓴 것(OnDecorationPlaced)은 건물 건설로 서버에 이미 갔으므로 뺀다
    void ReportDiscards()
    {
        if (changed && watched != null)
        {
            foreach (BuildingData data in new List<BuildingData>(decorations.Keys))
            {
                placed.TryGetValue(data, out int used);
                int discarded = decorations[data] - watched.GetCount(data) - used;
                for (int i = 0; i < discarded; i++)
                {
                    if (chatClient.SendDiscard(data.buildingID)) continue;

                    // 서버에 알리지 못한 만큼은 버리지 않은 것으로 되돌린다
                    applying = true;
                    watched.AddDecoration(data, discarded - i);
                    applying = false;
                    Toast.Show("서버에 연결되어 있지 않아 버리지 못했습니다.");
                    break;
                }
                decorations[data] = watched.GetCount(data);
            }
        }
        changed = false;
        placed.Clear();
    }

    // 서버의 자원 이름(wood, stone, gold)을 ResourceType 으로. 클라이언트가 모르는 자원이면 false
    static bool TryGetType(string id, out ResourceType type)
    {
        return Enum.TryParse(id, true, out type) && Enum.IsDefined(typeof(ResourceType), type);
    }
}
