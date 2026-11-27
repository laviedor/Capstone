using System.Collections.Generic;
using UnityEngine;

// 장비 id(EquipmentItemData.itemID)로 장비 데이터를 찾는다.
//   다른 플레이어가 입은 옷은 서버가 id 로만 알려 주므로, 내가 가지고 있지 않은 장비도 찾을 수 있어야 한다
//   찾는 곳: Resources/Equipment 의 장비 전부 + 대장간에서 만드는 도구(CraftingRecipes) + 내 장비 목록
public static class EquipmentCatalog
{
    private const string ResourceFolder = "Equipment";

    private static Dictionary<string, EquipmentItemData> items;

    public static EquipmentItemData Find(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        Load();

        if (items.TryGetValue(id, out EquipmentItemData item) && item != null)
            return item;

        // 데이터 폴더에 없는 장비(코드로 만든 것 등)는 내 장비 목록에서 찾아 기억해 둔다
        PlayerEquipment equipment = PlayerEquipment.Instance;

        if (equipment != null)
        {
            foreach (EquipmentItemData owned in equipment.OwnedItems)
            {
                if (owned != null && owned.itemID == id)
                    return items[id] = owned;
            }
        }

        return null;
    }

    private static void Load()
    {
        if (items != null)
            return;

        items = new Dictionary<string, EquipmentItemData>();

        foreach (EquipmentItemData item in Resources.LoadAll<EquipmentItemData>(ResourceFolder))
            Add(item);

        foreach (CraftingRecipes.Recipe recipe in CraftingRecipes.All)
            Add(recipe.Item);
    }

    private static void Add(EquipmentItemData item)
    {
        if (item != null && !string.IsNullOrEmpty(item.itemID) && !items.ContainsKey(item.itemID))
            items.Add(item.itemID, item);
    }
}
