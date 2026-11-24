using System.Collections.Generic;
using UnityEngine;

// 대장간에서 만들 수 있는 도구 목록.
//   서버 Server/data/crafting.json 의 값(비용, 최대 개수)과 같게 유지한다 (실제 계산은 서버가 한다)
//   그림은 Resources/Crafting 에 있다: <id>_icon (아이콘), <id>_worn (캐릭터가 손에 든 모습)
public static class CraftingRecipes
{
    public const string Shovel = "shovel";

    public class Recipe
    {
        public string id;
        public string name;
        public string description;
        public ResourceAmount[] cost;
        public int max;

        private Sprite icon;
        private Sprite worn;
        private EquipmentItemData item;

        public Sprite Icon => icon != null ? icon : icon = Resources.Load<Sprite>("Crafting/" + id + "_icon");
        public Sprite Worn => worn != null ? worn : worn = Resources.Load<Sprite>("Crafting/" + id + "_worn");

        // 인벤토리·장비 칸에 넣을 장비 데이터 (도구 칸). 처음 쓸 때 만든다
        public EquipmentItemData Item
        {
            get
            {
                if (item != null)
                    return item;

                item = ScriptableObject.CreateInstance<EquipmentItemData>();
                item.name = id;
                item.itemID = id;
                item.itemName = name;
                item.description = description;
                item.icon = Icon;
                item.wornSprite = Worn;
                item.slot = EquipmentSlot.Tool;
                return item;
            }
        }
    }

    public static readonly IReadOnlyList<Recipe> All = new[]
    {
        new Recipe
        {
            id = Shovel,
            name = "삽",
            description = "땅을 파서 바닥을 바꿀 수 있어요. 건설 창의 [타일] 탭이 열려요.",
            cost = new[]
            {
                new ResourceAmount { resourceType = ResourceType.Wood, amount = 40 },
                new ResourceAmount { resourceType = ResourceType.Stone, amount = 20 },
            },
            max = 1,
        },
    };

    public static Recipe Find(string id)
    {
        foreach (Recipe recipe in All)
        {
            if (recipe.id == id)
                return recipe;
        }

        return null;
    }

    // 대장간 건물인지 (클릭하면 제작 창이 열린다)
    public static bool IsCraftingStation(Building building)
    {
        return building != null && building.Data != null && building.Data.buildingID == "blacksmith";
    }
}
