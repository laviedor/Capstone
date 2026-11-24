using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// 집 안을 꾸밀 장식물(침대, 책장, 소파, 난로, 괘종시계, 스탠드 조명, 몬스테라, 고양이 바구니, 축음기)을 추가한다.
//   메뉴: Tools > Cozy UI > 집 장식물 추가
//   1. Assets/Sprites/HouseDecor 의 그림(직접 그린 도트, 50px = 1유닛)을 스프라이트로 가져온다
//      기준점은 차지하는 칸의 가운데라서, 그림 아래쪽이 칸 아래쪽에 맞는다
//   2. 장식마다 BuildingData(장식, 집 안 전용) 와 프리팹을 만든다 (Assets/Data/HouseDecor, Assets/Prefab/HouseDecor)
//   3. 건설 목록(ConstructionListUI)과 BuildingSync 의 프리팹 목록에 넣는다
//      가구는 자원(나무·돌·골드)을 내고 바로 놓는다: 집 안에서 건설 창을 열면 보인다 (채집 보상 장식 목록에서는 뺀다)
//   4. 서버 데이터(buildings.json)를 다시 내보낸다
//   이름·칸 수는 HouseDecorLayout.json. 여러 번 실행해도 같은 결과가 나온다.
public static class HouseDecorBuilder
{
    private const string LayoutPath = "Assets/Scripts/Editor/HouseDecorLayout.json";
    private const string SpriteFolder = "Assets/Sprites/HouseDecor";
    private const string DataFolder = "Assets/Data/HouseDecor";
    private const string PrefabFolder = "Assets/Prefab/HouseDecor";
    private const string MaterialReferencePrefab = "Assets/Prefab/BlackSmith.prefab";
    private const float CellSize = 0.5f;

    [Serializable] private class Layout { public float pixelsPerUnit; public Item[] items; }
    [Serializable] private class Item { public string key, name; public int cw, ch; public int[] cost; }

    [MenuItem("Tools/Cozy UI/집 장식물 추가")]
    public static void Apply()
    {
        ConstructionListUI construction = Object.FindFirstObjectByType<ConstructionListUI>(FindObjectsInactive.Include);
        BuildingSync sync = Object.FindFirstObjectByType<BuildingSync>(FindObjectsInactive.Include);

        if (construction == null || sync == null)
        {
            EditorUtility.DisplayDialog("집 장식물",
                "열린 씬에서 ConstructionListUI, BuildingSync 를 찾지 못했어요.\n마을이 있는 씬(SampleScene)을 열고 다시 실행해 주세요.", "확인");
            return;
        }

        DecorationDatabase database =
            new SerializedObject(construction).FindProperty("decorationDatabase").objectReferenceValue as DecorationDatabase;

        if (database == null)
        {
            EditorUtility.DisplayDialog("집 장식물", "ConstructionListUI 에 장식 목록(DecorationDatabase)이 연결되어 있지 않아요.", "확인");
            return;
        }

        Layout layout = JsonUtility.FromJson<Layout>(File.ReadAllText(LayoutPath, System.Text.Encoding.UTF8));
        Material material = ReferenceMaterial();
        var created = new List<(BuildingData data, GameObject prefab)>();

        try
        {
            for (int i = 0; i < layout.items.Length; i++)
            {
                Item item = layout.items[i];
                EditorUtility.DisplayProgressBar("집 장식물", item.name, (float)i / layout.items.Length);

                Sprite sprite = ImportSprite(item, layout.pixelsPerUnit);

                if (sprite == null)
                {
                    Debug.LogWarning("[집 장식물] 그림이 없어 건너뜀: " + item.key);
                    continue;
                }

                BuildingData data = CreateData(item, sprite);
                created.Add((data, CreatePrefab(item, sprite, data, material)));
            }

            AssetDatabase.SaveAssets();
            Register(construction, database, sync, created);
            ServerDataExporter.Export();

            EditorSceneManager.MarkSceneDirty(sync.gameObject.scene);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Debug.Log($"[집 장식물] 장식 {created.Count}종을 추가했어요. 씬을 저장하고 서버를 재시작해 주세요.");
        EditorUtility.DisplayDialog("집 장식물",
            $"집 가구 {created.Count}종을 건설 목록에 추가했어요.\n집 안에서 건설 창을 열면 보이고, 자원을 내고 바로 놓을 수 있어요.\n\n" +
            "• 씬을 저장해 주세요.\n• 서버 데이터가 바뀌었으니 서버를 재시작해 주세요.", "확인");
    }

    // 그림: 부드러운 필터(다른 소품·캐릭터와 같음), 기준점 = 차지하는 칸의 가운데
    private static Sprite ImportSprite(Item item, float pixelsPerUnit)
    {
        string path = SpriteFolder + "/" + item.key + ".png";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
            return null;

        int height = ReadPngHeight(path);
        float pivotY = Mathf.Clamp01(item.ch * CellSize * pixelsPerUnit / 2f / Mathf.Max(1, height));

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = new Vector2(0.5f, pivotY);
        settings.spriteMeshType = SpriteMeshType.Tight;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // PNG 파일 머리에서 그림 높이를 읽는다 (가져오기 설정과 상관없는 원본 크기)
    private static int ReadPngHeight(string assetPath)
    {
        byte[] head = new byte[24];

        using (FileStream f = File.OpenRead(assetPath))
            f.Read(head, 0, head.Length);

        return (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23];
    }

    private static Material ReferenceMaterial()
    {
        GameObject reference = AssetDatabase.LoadAssetAtPath<GameObject>(MaterialReferencePrefab);
        SpriteRenderer renderer = reference != null ? reference.GetComponentInChildren<SpriteRenderer>() : null;
        return renderer != null ? renderer.sharedMaterial : null;
    }

    private static BuildingData CreateData(Item item, Sprite sprite)
    {
        CozySpriteFactory.EnsureFolder(DataFolder);
        string path = DataFolder + "/" + item.key + ".asset";
        BuildingData data = AssetDatabase.LoadAssetAtPath<BuildingData>(path);

        if (data == null)
        {
            data = ScriptableObject.CreateInstance<BuildingData>();
            AssetDatabase.CreateAsset(data, path);
        }

        data.buildingID = item.key;
        data.buildingName = item.name;
        data.buildingIcon = sprite;
        data.buildingType = BuildingType.Decoration;
        data.houseOnly = true; // 실내 가구: 집 안에만 놓는다
        data.width = item.cw;
        data.height = item.ch;
        // 가격: [자원 종류, 수량, ...] (0 나무, 1 돌, 2 골드). 비용이 있는 장식은 아이템 없이 자원으로 바로 놓는다
        var costs = new List<ResourceAmount>();

        for (int i = 0; item.cost != null && i + 1 < item.cost.Length; i += 2)
            costs.Add(new ResourceAmount { resourceType = (ResourceType)item.cost[i], amount = item.cost[i + 1] });

        data.constructionCosts = costs.ToArray();
        data.productions = new ResourceProduction[0];
        data.canInteract = false;
        EditorUtility.SetDirty(data);
        return data;
    }

    private static GameObject CreatePrefab(Item item, Sprite sprite, BuildingData data, Material material)
    {
        CozySpriteFactory.EnsureFolder(PrefabFolder);
        string path = PrefabFolder + "/" + item.key + ".prefab";
        GameObject go = new GameObject(item.key);

        try
        {
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 0;

            if (material != null)
                renderer.sharedMaterial = material;

            // 클릭(철거 선택 등) 판정: 그림 전체
            BoxCollider2D box = go.AddComponent<BoxCollider2D>();
            box.size = sprite.bounds.size;
            box.offset = sprite.bounds.center;

            Building building = go.AddComponent<Building>();
            SerializedObject so = new SerializedObject(building);
            so.FindProperty("data").objectReferenceValue = data;
            so.ApplyModifiedPropertiesWithoutUndo();

            return PrefabUtility.SaveAsPrefabAsset(go, path);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static void Register(ConstructionListUI construction, DecorationDatabase database, BuildingSync sync,
        List<(BuildingData data, GameObject prefab)> created)
    {
        // 건설 목록 (자원으로 바로 놓는 가구)
        SerializedObject cl = new SerializedObject(construction);
        SerializedProperty list = cl.FindProperty("availableBuildings");

        foreach (var c in created)
        {
            int index = -1;

            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).FindPropertyRelative("buildingData").objectReferenceValue == c.data)
                    index = i;
            }

            if (index < 0)
            {
                index = list.arraySize;
                list.arraySize++;
            }

            SerializedProperty entry = list.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("buildingData").objectReferenceValue = c.data;
            entry.FindPropertyRelative("buildingPrefab").objectReferenceValue = c.prefab;
        }

        cl.ApplyModifiedProperties();

        // 예전에 장식 목록(채집 보상)에 넣었던 가구는 뺀다
        SerializedObject db = new SerializedObject(database);
        SerializedProperty decorations = db.FindProperty("decorations");

        for (int i = decorations.arraySize - 1; i >= 0; i--)
        {
            Object data = decorations.GetArrayElementAtIndex(i).FindPropertyRelative("buildingData").objectReferenceValue;

            if (created.Exists(c => c.data == data))
                decorations.DeleteArrayElementAtIndex(i);
        }

        db.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(database);

        // 서버에서 받은 건물을 화면에 만들 프리팹 목록
        SerializedObject bs = new SerializedObject(sync);
        SerializedProperty prefabs = bs.FindProperty("buildingPrefabs");

        foreach (var c in created)
        {
            Building building = c.prefab.GetComponent<Building>();
            bool exists = false;

            for (int i = 0; i < prefabs.arraySize; i++)
            {
                if (prefabs.GetArrayElementAtIndex(i).objectReferenceValue == building)
                    exists = true;
            }

            if (exists)
                continue;

            prefabs.arraySize++;
            prefabs.GetArrayElementAtIndex(prefabs.arraySize - 1).objectReferenceValue = building;
        }

        bs.ApplyModifiedProperties();
    }
}
