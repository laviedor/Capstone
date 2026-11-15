using System;
using System.Collections.Generic;
using UnityEngine;

// 서버가 알려 주는 재화를 PlayerResources(화면에 보이는 재화)와 DecorationInventory(가진 장식물 수)에 반영
//   재화는 서버의 값이 기준이다: 건설 비용, 철거 환급, 건물의 주기적 생산, 채집 보상을 서버가 계산하고 바뀔 때마다 전부 알려 준다
//   BuildingPlacement, BuildingDemolition, ResourceNode, GatheringTimer 가 화면에 먼저 반영해 둔 값도 서버 값으로 맞춰진다
//   장식물은 서버 인벤토리에 장식물 건물 종류(buildingID)와 같은 이름의 아이템으로 들어 있다
//   건물 생산은 게임을 꺼도 서버에서 계속되므로, 오래 접속하지 않았다가 들어오면 그동안 생산한 양을 알려 준다
public class ResourceSync : MonoBehaviour
{
    [SerializeField] private ChatClient chatClient;
    [SerializeField] private PlayerResources playerResources;
    [SerializeField] private ConfirmDialog dialog;

    void OnEnable()
    {
        chatClient.ResourcesReceived += OnResources;
        chatClient.OfflineProduced += OnOfflineProduced;
    }

    void OnDisable()
    {
        chatClient.ResourcesReceived -= OnResources;
        chatClient.OfflineProduced -= OnOfflineProduced;
    }

    void OnResources(ResourceMessage m)
    {
        if (m.items == null) return;
        foreach (ResourceItem item in m.items)
        {
            if (TryGetType(item.id, out ResourceType type))
                SetAmount(type, item.n);
            else
                SetDecorationCount(item.id, item.n);
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
        if (inventory == null || data == null || data.buildingType != BuildingType.Decoration) return;

        int have = inventory.GetCount(data);
        if (count > have) inventory.AddDecoration(data, count - have);
        for (int i = count; i < have; i++) inventory.TryConsumeDecoration(data);
    }

    void OnOfflineProduced(ResourceMessage m)
    {
        var parts = new List<string>();
        foreach (ResourceItem item in m.items ?? new ResourceItem[0])
        {
            if (item.n > 0 && TryGetType(item.id, out ResourceType type))
                parts.Add($"{DisplayName(type)} +{item.n}");
        }
        if (parts.Count == 0) return;

        dialog.Show($"접속하지 않은 {Duration(m.seconds)} 동안\n건물이 자원을 생산했습니다.\n{string.Join(", ", parts)}", "확인", null);
    }

    // 서버의 자원 이름(wood, stone, gold)을 ResourceType 으로. 클라이언트가 모르는 자원이면 false
    static bool TryGetType(string id, out ResourceType type)
    {
        return Enum.TryParse(id, true, out type) && Enum.IsDefined(typeof(ResourceType), type);
    }

    static string DisplayName(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Wood: return "나무";
            case ResourceType.Stone: return "돌";
            case ResourceType.Gold: return "골드";
            default: return type.ToString();
        }
    }

    static string Duration(int seconds)
    {
        if (seconds >= 86400) return $"{seconds / 86400}일 {seconds % 86400 / 3600}시간";
        if (seconds >= 3600) return $"{seconds / 3600}시간 {seconds % 3600 / 60}분";
        if (seconds >= 60) return $"{seconds / 60}분";
        return $"{seconds}초";
    }
}
