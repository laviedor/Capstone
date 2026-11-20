using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// 마을 바닥을 잔디 타일로, 집 내부 바닥을 마룻바닥 타일로 깐다.
//   마을: FieldSceneManager 가 필드에 갈 때 숨기는 바닥 타일맵(villageTilemap)에 맵 경계(MapBounds)보다 조금 넓게 잔디를 깔고,
//         예전의 큰 바닥 그림(VillageVisualRoot 의 SpriteRenderer)은 끈다 (지우지 않으므로 다시 켤 수 있다)
//   집 안: HouseInteriorScene 의 FloorTilemap 에 지금 타일이 있는 칸만 마룻바닥으로 바꾼다 (방 모양은 그대로)
//   잔디는 8가지 타일(맨 잔디, 풀 포기, 작은 꽃)을 섞어 깔아 반복 무늬가 보이지 않게 한다
//   여러 번 실행해도 결과가 같다 (칸마다 고르는 타일이 위치로 정해진다)
public static class CozyFloorTiler
{
    private const string GrassFolder = "Assets/Sprites/Tiles/Grass";
    private const string WoodFloorPath = "Assets/Sprites/Tiles/WoodFloor.png";
    private const string TileAssetFolder = "Assets/Sprites/Tiles/TileAssets";
    private const string HouseScenePath = "Assets/Scenes/HouseInteriorScene.unity";

    // 잔디 타일과 섞는 비율 (합 100): 맨 잔디(Grass, Var1) 82%, 풀 포기 4종 16%, 꽃 2%
    //   (마을 한 칸은 화면에서 약 13px 로 보이므로 무늬는 5px 단위의 큰 도트로 그려져 있다)
    //   풀이나 꽃을 더 많이/적게 하려면 이 숫자만 바꾸고 메뉴를 다시 실행하면 된다
    private static readonly (string name, int weight)[] GrassTiles =
    {
        ("Grass", 41), ("Grass_Var1", 41), ("Grass_Var2", 4), ("Grass_Var3", 4),
        ("Grass_Var4", 4), ("Grass_Var5", 4), ("Grass_Flower1", 1), ("Grass_Flower2", 1),
    };

    // 맵 경계 밖으로 더 깔아 둘 여유 (카메라가 경계 끝까지 가도 빈 곳이 보이지 않게)
    private const float MapMargin = 3f;

    [MenuItem("Tools/Cozy UI/바닥 타일 깔기 (마을 잔디 · 집 마루)")]
    public static void Apply()
    {
        FieldSceneManager field = Object.FindFirstObjectByType<FieldSceneManager>(FindObjectsInactive.Include);

        if (field == null)
        {
            EditorUtility.DisplayDialog("바닥 타일",
                "열려 있는 씬에서 FieldSceneManager 를 찾지 못했어요.\n마을이 있는 씬(SampleScene)을 열고 다시 실행해 주세요.", "확인");
            return;
        }

        if (!EditorUtility.DisplayDialog("바닥 타일",
                "마을 바닥 타일맵을 잔디로 채우고, 예전 바닥 그림은 꺼요.\n" +
                "집 내부(HouseInteriorScene) 바닥은 마룻바닥으로 바꾸고 그 씬을 저장해요.\n\n" +
                "마을 씬 변경은 Ctrl+Z(⌘Z)로 되돌릴 수 있어요.",
                "깔기", "취소"))
        {
            return;
        }

        try
        {
            EditorUtility.DisplayProgressBar("바닥 타일", "타일 만드는 중…", 0.2f);
            List<(TileBase tile, int weight)> grass = new List<(TileBase, int)>();

            foreach ((string name, int weight) in GrassTiles)
                grass.Add((EnsureTile(GrassFolder + "/" + name + ".png", name), weight));

            TileBase wood = EnsureTile(WoodFloorPath, "WoodFloor");

            EditorUtility.DisplayProgressBar("바닥 타일", "마을에 잔디 까는 중…", 0.5f);
            int villageCount = PaintVillage(field, grass);

            EditorUtility.DisplayProgressBar("바닥 타일", "집 안에 마루 까는 중…", 0.8f);
            int houseCount = PaintHouse(wood);

            Debug.Log($"[바닥 타일] 마을 잔디 {villageCount}칸, 집 마루 {houseCount}칸을 깔았어요.");
            EditorUtility.DisplayDialog("바닥 타일",
                $"마을 잔디 {villageCount}칸, 집 마루 {houseCount}칸을 깔았어요.\n마을 씬을 저장하면 반영돼요.", "확인");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    // ----- 타일 에셋 -----

    // 그림을 도트 타일용으로 가져오고(100px = 1유닛, 흐림 없음) 그 그림의 Tile 에셋을 만든다
    private static TileBase EnsureTile(string spritePath, string tileName)
    {
        TextureImporter importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;

        if (importer == null)
        {
            AssetDatabase.ImportAsset(spritePath, ImportAssetOptions.ForceUpdate);
            importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;
        }

        if (importer == null)
            throw new System.Exception("타일 그림을 찾지 못했어요: " + spritePath);

        bool changed =
            importer.textureType != TextureImporterType.Sprite ||
            importer.spriteImportMode != SpriteImportMode.Single ||
            !Mathf.Approximately(importer.spritePixelsPerUnit, 100f) ||
            importer.filterMode != FilterMode.Point ||
            importer.mipmapEnabled ||
            importer.textureCompression != TextureImporterCompression.Uncompressed;

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);

        if (settings.spriteMeshType != SpriteMeshType.FullRect || settings.wrapMode != TextureWrapMode.Clamp)
            changed = true;

        if (changed)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; // 타일 사이 틈이 생기지 않게 사각형 그대로
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.wrapMode = TextureWrapMode.Clamp;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);

        if (sprite == null)
            throw new System.Exception("타일 스프라이트를 불러오지 못했어요: " + spritePath);

        CozySpriteFactory.EnsureFolder(TileAssetFolder);
        string tilePath = TileAssetFolder + "/" + tileName + ".asset";
        Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);

        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.None;
            AssetDatabase.CreateAsset(tile, tilePath);
        }
        else if (tile.sprite != sprite)
        {
            tile.sprite = sprite;
            EditorUtility.SetDirty(tile);
        }

        return tile;
    }

    // ----- 마을 -----

    private static int PaintVillage(FieldSceneManager field, List<(TileBase tile, int weight)> grass)
    {
        SerializedObject so = new SerializedObject(field);
        GameObject tilemapObject = so.FindProperty("villageTilemap").objectReferenceValue as GameObject;
        GameObject visualRoot = so.FindProperty("villageVisualRoot").objectReferenceValue as GameObject;

        Tilemap tilemap = tilemapObject != null ? tilemapObject.GetComponent<Tilemap>() : null;

        if (tilemap == null)
            throw new System.Exception("FieldSceneManager 의 Village Tilemap 이 비어 있거나 Tilemap 이 아니에요.");

        Bounds area = FindVillageBounds(field.gameObject.scene);
        area.Expand(new Vector3(MapMargin * 2f, MapMargin * 2f, 0f));

        Vector3Int min = tilemap.WorldToCell(area.min);
        Vector3Int max = tilemap.WorldToCell(area.max);

        List<Vector3Int> positions = new List<Vector3Int>();
        List<TileBase> tiles = new List<TileBase>();
        int totalWeight = 0;

        foreach ((TileBase _, int weight) in grass)
            totalWeight += weight;

        for (int y = min.y; y <= max.y; y++)
        {
            for (int x = min.x; x <= max.x; x++)
            {
                positions.Add(new Vector3Int(x, y, 0));
                tiles.Add(PickWeighted(grass, totalWeight, x, y));
            }
        }

        Undo.RegisterCompleteObjectUndo(tilemap, "바닥 잔디 깔기");
        tilemap.ClearAllTiles();
        tilemap.SetTiles(positions.ToArray(), tiles.ToArray());
        tilemap.color = Color.white;
        tilemap.CompressBounds();
        EditorUtility.SetDirty(tilemap);

        TilemapRenderer tilemapRenderer = tilemap.GetComponent<TilemapRenderer>();

        // 예전의 큰 바닥 그림은 끈다 (잔디 타일맵보다 위에 그려져 가리므로)
        if (visualRoot != null && visualRoot.GetComponent<SpriteRenderer>() is SpriteRenderer background && background.enabled)
        {
            Undo.RecordObject(background, "예전 바닥 그림 끄기");
            background.enabled = false;
            EditorUtility.SetDirty(background);
        }

        if (tilemapRenderer != null && !tilemap.gameObject.activeSelf)
        {
            Undo.RecordObject(tilemap.gameObject, "바닥 타일맵 켜기");
            tilemap.gameObject.SetActive(true);
        }

        EditorSceneManager.MarkSceneDirty(tilemap.gameObject.scene);
        return positions.Count;
    }

    // 마을의 맵 경계: 길찾기에 쓰는 MapBounds (집 안의 경계는 제외)
    private static Bounds FindVillageBounds(Scene scene)
    {
        MousePathfinding pathfinding = Object.FindFirstObjectByType<MousePathfinding>(FindObjectsInactive.Include);

        if (pathfinding != null &&
            new SerializedObject(pathfinding).FindProperty("mapBounds").objectReferenceValue is MapBounds mapBounds)
        {
            return BoundsOf(mapBounds);
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            MapBounds found = root.GetComponentInChildren<MapBounds>(true);

            if (found != null)
                return BoundsOf(found);
        }

        throw new System.Exception("마을의 MapBounds 를 찾지 못했어요.");
    }

    // 편집 중에는 물리가 돌지 않아 Collider.bounds 가 비어 있을 수 있으므로 크기와 위치로 직접 계산한다
    private static Bounds BoundsOf(MapBounds mapBounds)
    {
        BoxCollider2D box = mapBounds.GetComponent<BoxCollider2D>();
        Transform t = mapBounds.transform;
        Vector3 center = t.TransformPoint(box.offset);
        Vector3 size = Vector3.Scale(box.size, t.lossyScale);
        return new Bounds(center, new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), 1f));
    }

    // 칸 위치로 정해지는 의사 난수로 잔디 타일을 고른다 (다시 실행해도 같은 모양)
    private static TileBase PickWeighted(List<(TileBase tile, int weight)> list, int totalWeight, int x, int y)
    {
        unchecked
        {
            uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ 0x9E3779B9u;
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;

            int roll = (int)(h % (uint)totalWeight);

            foreach ((TileBase tile, int weight) in list)
            {
                if (roll < weight)
                    return tile;

                roll -= weight;
            }

            return list[0].tile;
        }
    }

    // ----- 집 안 -----

    private static int PaintHouse(TileBase wood)
    {
        Scene scene = SceneManager.GetSceneByPath(HouseScenePath);
        bool openedHere = false;

        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(HouseScenePath, OpenSceneMode.Additive);
            openedHere = true;
        }

        Tilemap floor = null;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Tilemap tilemap in root.GetComponentsInChildren<Tilemap>(true))
            {
                if (tilemap.name == "FloorTilemap")
                {
                    floor = tilemap;
                    break;
                }
            }

            if (floor != null)
                break;
        }

        if (floor == null)
        {
            if (openedHere)
                EditorSceneManager.CloseScene(scene, true);

            Debug.LogWarning("[바닥 타일] HouseInteriorScene 에서 FloorTilemap 을 찾지 못해 집 바닥은 그대로 두었어요.");
            return 0;
        }

        // 지금 타일이 있는 칸(방 바닥 모양)만 마루로 바꾼다
        floor.CompressBounds();
        List<Vector3Int> positions = new List<Vector3Int>();

        foreach (Vector3Int position in floor.cellBounds.allPositionsWithin)
        {
            if (floor.HasTile(position))
                positions.Add(position);
        }

        TileBase[] tiles = new TileBase[positions.Count];

        for (int i = 0; i < tiles.Length; i++)
            tiles[i] = wood;

        Undo.RegisterCompleteObjectUndo(floor, "집 마루 깔기");
        floor.SetTiles(positions.ToArray(), tiles);
        floor.color = Color.white;
        EditorUtility.SetDirty(floor);
        EditorSceneManager.MarkSceneDirty(scene);

        if (openedHere)
        {
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
        }

        return positions.Count;
    }
}
