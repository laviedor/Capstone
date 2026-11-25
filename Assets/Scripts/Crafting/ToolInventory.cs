using System;
using System.Collections.Generic;
using UnityEngine;

// 대장간에서 만든 도구(예: 삽)를 몇 개 가지고 있는지. 서버가 알려 준 값이 기준이다 (ResourceSync 가 넣어 준다)
//   가진 도구는 인벤토리의 장비 목록(PlayerEquipment)에도 넣어서, 도구 칸에 장착하면 캐릭터가 손에 든다
//   처음 쓸 때 스스로 만들어진다 (씬에 따로 놓지 않아도 됨)
public class ToolInventory : MonoBehaviour
{
    private static ToolInventory instance;

    private readonly Dictionary<string, int> counts = new Dictionary<string, int>();

    // 가진 도구 수가 바뀌면 (제작 창, 건설 창의 [타일] 탭 갱신용)
    public static event Action Changed;

    public static ToolInventory Instance => instance;

    public static bool HasShovel => instance != null && instance.GetCount(CraftingRecipes.Shovel) > 0;

    public static ToolInventory Ensure()
    {
        if (instance != null)
            return instance;

        GameObject holder = new GameObject("ToolInventory");
        DontDestroyOnLoad(holder);
        instance = holder.AddComponent<ToolInventory>();
        return instance;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public int GetCount(string id)
    {
        return counts.TryGetValue(id, out int n) ? n : 0;
    }

    public void SetCount(string id, int count)
    {
        count = Mathf.Max(0, count);

        if (GetCount(id) == count && counts.ContainsKey(id))
            return;

        counts[id] = count;
        SyncEquipment();
        Changed?.Invoke();
    }

    // 장비 목록이 늦게 준비되거나 다시 만들어져도 맞춰 둔다
    private void LateUpdate()
    {
        SyncEquipment();
    }

    // 가진 도구는 장비 목록에 있고, 없는 도구는 장비 목록에서 뺀다
    private void SyncEquipment()
    {
        PlayerEquipment equipment = PlayerEquipment.Instance;

        if (equipment == null)
            return;

        foreach (CraftingRecipes.Recipe recipe in CraftingRecipes.All)
        {
            EquipmentItemData item = recipe.Item;
            bool owned = false;

            foreach (EquipmentItemData o in equipment.OwnedItems)
            {
                if (o == item)
                {
                    owned = true;
                    break;
                }
            }

            bool has = GetCount(recipe.id) > 0;

            if (has && !owned)
                equipment.AddItem(item);
            else if (!has && owned)
                equipment.RemoveItem(item);
        }
    }

    // 장비 목록에서 버릴 수 없는 아이템인지 (제작한 도구는 서버에 있으므로 화면에서만 버릴 수 없다)
    public static bool IsCraftedTool(EquipmentItemData item)
    {
        return item != null && CraftingRecipes.Find(item.itemID) != null;
    }
}
