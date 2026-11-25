using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

// 팀원이 준 건물·장식 시트를 건설 창에 추가하고, 광장을 분수 광장으로 꾸민다.
//   메뉴: Tools > Cozy UI > 광장 에셋 가져오기 · 꾸미기
//   1. MultiverseSheet.png 를 건물·장식별로 잘라 스프라이트로 만든다 (위치·크기·칸 수는 MultiverseLayout.json)
//      그림의 기준점은 건물이 차지하는 칸의 가운데라서, 그림 아래쪽이 칸 아래쪽에 맞는다
//   2. 건물·장식마다 BuildingData 와 프리팹을 만든다 (Assets/Data/Multiverse, Assets/Prefab/Multiverse)
//   3. 건물은 건설 목록(ConstructionListUI), 장식은 장식 목록(DecorationDatabase: 채집 보상으로도 나옴)에 넣고,
//      서버에서 받은 건물을 화면에 만들 수 있게 BuildingSync 의 프리팹 목록에도 넣는다
//   4. 광장 풍경을 마을 화면 아래에 만든다 (MultiverseScenery: 광장에서만 보임)
//      바닥: 벽돌 포장 광장과 동서 큰길·남북 길 타일맵 (잔디 위에 깔리고, 잔디와 닿는 곳은 돌 테두리 타일)
//      소품: 분수, 분수 둘레 돌 링, 가로등, 벤치, 화단, 나무, 덤불, 꽃 (직접 그린 도트 그림)
//      바닥 모양과 소품 위치는 MultiversePlaza.json (분수·가로등 등이 막는 칸도 여기 들어 있다)
//   5. 서버 데이터(buildings.json)를 다시 내보낸다 (Tools > Export Server Data 와 같음)
//   여러 번 실행해도 같은 결과가 나온다.
public static class MultiverseBuilder
{
    private const string LayoutPath = "Assets/Scripts/Editor/MultiverseLayout.json";
    private const string SheetPath = "Assets/Sprites/Multiverse/MultiverseSheet.png";
    private const string PlazaLayoutPath = "Assets/Scripts/Editor/MultiversePlaza.json";
    private const string FloorFolder = "Assets/Sprites/Multiverse/Floor";
    private const string FloorTileFolder = "Assets/Sprites/Multiverse/Floor/Tiles";
    private const string PropFolder = "Assets/Sprites/Multiverse/Props";
    private const string DataFolder = "Assets/Data/Multiverse";
    private const string PrefabFolder = "Assets/Prefab/Multiverse";
    private const string SceneryName = "MultiverseScenery";

    // 기존 건물과 같은 그림 재질을 쓰기 위해 참고할 프리팹
    private const string MaterialReferencePrefab = "Assets/Prefab/BlackSmith.prefab";

    private const int FloorSortingOrder = -9;     // 잔디 바닥(-10) 위, 격자선(-5)·건물(0) 아래
    private const int GroundDecalOrder = -8;      // 바닥에 붙은 장식 (분수 둘레 돌 링, 꽃)
    private const float CellSize = 0.5f;          // 마을 그리드 한 칸

    [Serializable] private class Layout { public float sheetPixelsPerUnit; public Piece[] pieces; }
    [Serializable] private class Piece { public string key, name; public int type, x, y, w, h, cw, ch; public int[] cost, prod; }

    // 광장: floor = 포장 칸과 타일 이름, props = 소품 (발 위치, 크기 배율, 막는 칸 [x, y, x, y, ...])
    [Serializable] private class PlazaLayout { public float pixelsPerUnit; public FloorCell[] floor; public Prop[] props; public SpriteSetting[] spriteSettings; }
    // 소품마다 다른 가져오기 설정 (예: 분수 아래 돌 고리는 바닥 타일과 같은 도트 크기·또렷한 픽셀)
    [Serializable] private class SpriteSetting { public string sprite; public float pixelsPerUnit; public bool point; }
    [Serializable] private class FloorCell { public int x, y; public string tile; }
    [Serializable] private class Prop { public string sprite; public float x, y, scale; public int[] blockedFlat; }

    [MenuItem("Tools/Cozy UI/광장 에셋 가져오기 · 꾸미기")]
    public static void Apply()
    {
        FieldSceneManager field = Object.FindFirstObjectByType<FieldSceneManager>(FindObjectsInactive.Include);
        ConstructionListUI construction = Object.FindFirstObjectByType<ConstructionListUI>(FindObjectsInactive.Include);
        BuildingSync sync = Object.FindFirstObjectByType<BuildingSync>(FindObjectsInactive.Include);

        if (field == null || construction == null || sync == null)
        {
            EditorUtility.DisplayDialog("광장",
                "열린 씬에서 FieldSceneManager, ConstructionListUI, BuildingSync 를 찾지 못했어요.\n마을이 있는 씬(SampleScene)을 열고 다시 실행해 주세요.", "확인");
            return;
        }

        if (!EditorUtility.DisplayDialog("광장",
                "팀원 에셋으로 건물·장식을 만들어 건설 창에 추가하고, 광장을 꾸며요.\n" +
                "서버 데이터(Server/data/buildings.json)도 다시 내보내요.\n\n실행하기 전에 git 커밋으로 백업해 두는 걸 권장해요.",
                "만들기", "취소"))
        {
            return;
        }

        try
        {
            Layout layout = JsonUtility.FromJson<Layout>(File.ReadAllText(LayoutPath, System.Text.Encoding.UTF8));

            EditorUtility.DisplayProgressBar("광장", "그림 자르는 중…", 0.1f);
            Dictionary<string, Sprite> sprites = SliceSheet(layout);

            EditorUtility.DisplayProgressBar("광장", "건물·장식 만드는 중…", 0.35f);
            Material material = ReferenceMaterial();
            var created = new List<(Piece piece, BuildingData data, GameObject prefab)>();

            foreach (Piece piece in layout.pieces)
            {
                if (!sprites.TryGetValue(piece.key, out Sprite sprite))
                {
                    Debug.LogWarning("[광장] 잘라 낸 그림이 없어 건너뜀: " + piece.key);
                    continue;
                }

                BuildingData data = CreateData(piece, sprite);
                GameObject prefab = CreatePrefab(piece, sprite, data, material);
                created.Add((piece, data, prefab));
            }

            AssetDatabase.SaveAssets();

            EditorUtility.DisplayProgressBar("광장", "건설 창에 등록하는 중…", 0.6f);
            Register(construction, sync, created);

            EditorUtility.DisplayProgressBar("광장", "광장 꾸미는 중…", 0.75f);
            BuildScenery(field, material);

            EditorUtility.DisplayProgressBar("광장", "서버 데이터 내보내는 중…", 0.9f);
            ServerDataExporter.Export();

            EditorSceneManager.MarkSceneDirty(field.gameObject.scene);
            AssetDatabase.SaveAssets();

            int buildings = created.Count(c => c.piece.type != (int)BuildingType.Decoration);
            int decorations = created.Count - buildings;
            Debug.Log($"[광장] 건물 {buildings}종, 장식 {decorations}종을 추가하고 광장을 꾸몄어요. 씬을 저장하고 서버를 재시작해 주세요.");
            EditorUtility.DisplayDialog("광장",
                $"건물 {buildings}종, 장식 {decorations}종을 추가하고 광장을 꾸몄어요.\n\n" +
                "• 씬을 저장해 주세요.\n• 서버 데이터가 바뀌었으니 서버를 재시작해 주세요.", "확인");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    // ----- 그림 -----

#pragma warning disable CS0618 // TextureImporter.spritesheet: 아직 쓸 수 있는 간단한 방법
    private static Dictionary<string, Sprite> SliceSheet(Layout layout)
    {
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(SheetPath);
        int sheetHeight = ReadPngHeight(SheetPath);
        float cellPixels = CellSize * layout.sheetPixelsPerUnit;

        var metas = new List<SpriteMetaData>();

        foreach (Piece p in layout.pieces)
        {
            // JSON 은 위에서 아래로 잰 좌표, Unity 는 아래에서 위로
            var rect = new Rect(p.x, sheetHeight - p.y - p.h, p.w, p.h);

            // 기준점: 차지하는 칸의 가운데 (그림 아래쪽 = 칸 아래쪽)
            float pivotY = Mathf.Clamp01(p.ch * cellPixels / 2f / p.h);

            metas.Add(new SpriteMetaData
            {
                name = p.key,
                rect = rect,
                alignment = (int)SpriteAlignment.Custom,
                pivot = new Vector2(0.5f, pivotY),
                border = Vector4.zero
            });
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = layout.sheetPixelsPerUnit;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spritesheet = metas.ToArray();
        importer.SaveAndReimport();

        return AssetDatabase.LoadAllAssetsAtPath(SheetPath)
            .OfType<Sprite>()
            .GroupBy(s => s.name)
            .ToDictionary(g => g.Key, g => g.First());
    }
#pragma warning restore CS0618

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

    // ----- 건물 데이터와 프리팹 -----

    private static BuildingData CreateData(Piece piece, Sprite sprite)
    {
        CozySpriteFactory.EnsureFolder(DataFolder);
        string path = DataFolder + "/" + piece.key + ".asset";
        BuildingData data = AssetDatabase.LoadAssetAtPath<BuildingData>(path);

        if (data == null)
        {
            data = ScriptableObject.CreateInstance<BuildingData>();
            AssetDatabase.CreateAsset(data, path);
        }

        data.buildingID = piece.key;
        data.buildingName = piece.name;
        data.buildingIcon = sprite;
        data.buildingType = (BuildingType)piece.type;
        data.width = piece.cw;
        data.height = piece.ch;
        data.constructionCosts = Pairs(piece.cost).Select(p => new ResourceAmount { resourceType = (ResourceType)p.Item1, amount = p.Item2 }).ToArray();
        data.productions = Pairs(piece.prod).Select(p => new ResourceProduction { resourceType = (ResourceType)p.Item1, amount = p.Item2 }).ToArray();
        data.productionInterval = 30f;
        data.canInteract = false;
        EditorUtility.SetDirty(data);
        return data;
    }

    private static IEnumerable<(int, int)> Pairs(int[] values)
    {
        if (values == null)
            yield break;

        for (int i = 0; i + 1 < values.Length; i += 2)
            yield return (values[i], values[i + 1]);
    }

    private static GameObject CreatePrefab(Piece piece, Sprite sprite, BuildingData data, Material material)
    {
        CozySpriteFactory.EnsureFolder(PrefabFolder);
        string path = PrefabFolder + "/" + piece.key + ".prefab";

        GameObject go = new GameObject(piece.key);

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

    // ----- 등록 -----

    private static void Register(ConstructionListUI construction, BuildingSync sync,
        List<(Piece piece, BuildingData data, GameObject prefab)> created)
    {
        // 건물: 건설 목록
        SerializedObject cl = new SerializedObject(construction);
        SerializedProperty list = cl.FindProperty("availableBuildings");

        foreach (var c in created.Where(c => c.piece.type != (int)BuildingType.Decoration))
        {
            int index = FindEntry(list, "buildingData", c.data);

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

        // 장식: 장식 목록 (건설 창에는 가지고 있을 때 보이고, 채집 보상으로도 나온다)
        if (cl.FindProperty("decorationDatabase").objectReferenceValue is DecorationDatabase database)
        {
            SerializedObject db = new SerializedObject(database);
            SerializedProperty decorations = db.FindProperty("decorations");

            foreach (var c in created.Where(c => c.piece.type == (int)BuildingType.Decoration))
            {
                int index = FindEntry(decorations, "buildingData", c.data);

                if (index < 0)
                {
                    index = decorations.arraySize;
                    decorations.arraySize++;
                }

                SerializedProperty entry = decorations.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("buildingData").objectReferenceValue = c.data;
                entry.FindPropertyRelative("buildingPrefab").objectReferenceValue = c.prefab;
            }

            db.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(database);
        }

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

    private static int FindEntry(SerializedProperty array, string field, Object value)
    {
        for (int i = 0; i < array.arraySize; i++)
        {
            if (array.GetArrayElementAtIndex(i).FindPropertyRelative(field).objectReferenceValue == value)
                return i;
        }

        return -1;
    }

    // ----- 광장 풍경 -----

    private static void BuildScenery(FieldSceneManager field, Material material)
    {
        PlazaLayout plaza = JsonUtility.FromJson<PlazaLayout>(File.ReadAllText(PlazaLayoutPath, System.Text.Encoding.UTF8));
        FloorCell[] floor = plaza.floor ?? new FloorCell[0];

        Dictionary<string, TileBase> tiles = ImportFloorTiles(floor.Select(f => f.tile).Distinct());
        Dictionary<string, Sprite> props = ImportProps(plaza.pixelsPerUnit, plaza.spriteSettings, plaza.props.Select(p => p.sprite).Distinct());

        SerializedObject fs = new SerializedObject(field);
        GameObject villageRoot = fs.FindProperty("villageVisualRoot").objectReferenceValue as GameObject;
        Transform parent = villageRoot != null ? villageRoot.transform : null;

        // 다시 실행하면 새로 만든다
        Transform old = parent != null ? parent.Find(SceneryName) : null;

        if (old == null)
        {
            GameObject found = GameObject.Find(SceneryName);
            old = found != null ? found.transform : null;
        }

        if (old != null)
            Undo.DestroyObjectImmediate(old.gameObject);

        GameObject root = new GameObject(SceneryName);
        Undo.RegisterCreatedObjectUndo(root, "광장 풍경");
        root.transform.SetParent(parent, false);
        root.transform.position = Vector3.zero;

        // 바닥 타일맵 (마을 그리드와 같은 0.5 칸)
        GameObject content = new GameObject("Content");
        content.transform.SetParent(root.transform, false);
        Grid grid = content.AddComponent<Grid>();
        grid.cellSize = new Vector3(CellSize, CellSize, 0f);

        GameObject floorObject = new GameObject("Floor");
        floorObject.transform.SetParent(content.transform, false);
        Tilemap tilemap = floorObject.AddComponent<Tilemap>();
        TilemapRenderer tilemapRenderer = floorObject.AddComponent<TilemapRenderer>();
        tilemapRenderer.sortingOrder = FloorSortingOrder;

        if (material != null)
            tilemapRenderer.sharedMaterial = material;

        tilemap.SetTiles(
            floor.Select(f => new Vector3Int(f.x, f.y, 0)).ToArray(),
            floor.Select(f => tiles.TryGetValue(f.tile, out TileBase t) ? t : null).ToArray());
        tilemap.CompressBounds();

        // 소품
        GameObject propsRoot = new GameObject("Props");
        propsRoot.transform.SetParent(content.transform, false);
        var blocked = new HashSet<Vector3Int>();

        foreach (Prop prop in plaza.props)
        {
            if (!props.TryGetValue(prop.sprite, out Sprite sprite))
                continue;

            GameObject go = new GameObject(prop.sprite);
            go.transform.SetParent(propsRoot.transform, false);
            go.transform.position = new Vector3(prop.x, prop.y, 0f);
            float scale = prop.scale > 0f ? prop.scale : 1f;
            go.transform.localScale = new Vector3(scale, scale, 1f);

            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = prop.sprite == "fountain_ring" || prop.sprite == "flowers" ? GroundDecalOrder : 0;

            if (material != null)
                renderer.sharedMaterial = material;

            if (prop.blockedFlat != null)
            {
                for (int i = 0; i + 1 < prop.blockedFlat.Length; i += 2)
                    blocked.Add(new Vector3Int(prop.blockedFlat[i], prop.blockedFlat[i + 1], 0));
            }
        }

        MultiverseScenery scenery = root.AddComponent<MultiverseScenery>();
        SerializedObject so = new SerializedObject(scenery);
        so.FindProperty("content").objectReferenceValue = content;
        SerializedProperty cells = so.FindProperty("blockedCells");
        cells.arraySize = blocked.Count;
        int index = 0;

        foreach (Vector3Int cell in blocked)
            cells.GetArrayElementAtIndex(index++).vector3IntValue = cell;

        so.ApplyModifiedPropertiesWithoutUndo();
        content.SetActive(false); // 광장에 들어가면 켜진다
    }

    // 바닥 타일 그림: 100px = 1유닛(한 칸 50px), 흐림 없음, 타일 사이 틈 없음 -> Tile 에셋
    private static Dictionary<string, TileBase> ImportFloorTiles(IEnumerable<string> names)
    {
        CozySpriteFactory.EnsureFolder(FloorTileFolder);
        var result = new Dictionary<string, TileBase>();

        foreach (string name in names)
        {
            string path = FloorFolder + "/" + name + ".png";
            ConfigureSprite(path, 100f, FilterMode.Point, SpriteMeshType.FullRect, new Vector2(0.5f, 0.5f));
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (sprite == null)
            {
                Debug.LogWarning("[광장] 바닥 타일 그림이 없어요: " + path);
                continue;
            }

            string tilePath = FloorTileFolder + "/" + name + ".asset";
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);

            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, tilePath);
            }

            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.None;
            EditorUtility.SetDirty(tile);
            result[name] = tile;
        }

        return result;
    }

    // 소품 그림: 50px = 1유닛 (캐릭터·건물과 같은 도트 크기), 기준점은 아래 가운데(발 위치)
    //   spriteSettings 에 적힌 소품은 그 설정(도트 크기, 또렷한 픽셀)을 따른다
    private static Dictionary<string, Sprite> ImportProps(float pixelsPerUnit, SpriteSetting[] settings, IEnumerable<string> names)
    {
        var result = new Dictionary<string, Sprite>();

        foreach (string name in names)
        {
            string path = PropFolder + "/" + name + ".png";
            SpriteSetting setting = settings?.FirstOrDefault(s => s.sprite == name);
            float ppu = setting != null && setting.pixelsPerUnit > 0f ? setting.pixelsPerUnit : pixelsPerUnit;
            FilterMode filter = setting != null && setting.point ? FilterMode.Point : FilterMode.Bilinear;
            ConfigureSprite(path, ppu, filter, SpriteMeshType.Tight, new Vector2(0.5f, 0f));
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (sprite != null)
                result[name] = sprite;
            else
                Debug.LogWarning("[광장] 소품 그림이 없어요: " + path);
        }

        return result;
    }

    private static void ConfigureSprite(string path, float pixelsPerUnit, FilterMode filter, SpriteMeshType meshType, Vector2 pivot)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.filterMode = filter;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = meshType;
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        settings.wrapMode = TextureWrapMode.Clamp;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }
}
