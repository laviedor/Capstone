using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// 마을 바닥 위에 덧칠한 타일 (삽으로 바꾼 칸). 지금은 흙(dirt) 한 종류.
//   마을 잔디 타일맵(FieldSceneManager 의 villageTilemap) 바로 위에 'GroundPaint' 타일맵을 만들어 그린다.
//   잔디로 되돌리면 덧칠한 칸을 지우므로 원래 잔디가 그대로 보인다.
//   흙은 이웃 8칸이 흙인지에 따라 47가지 모양 중 하나를 고른다 (자동 타일): 잔디와 닿는 가장자리는 부드럽게 둥글고,
//   위쪽(북) 가장자리는 그늘, 아래쪽(남)은 밝은 턱이 져서 땅이 살짝 파인 것처럼 보인다. 속이 꽉 찬 칸은 무늬 4가지를 섞는다
//   그림: Resources/GroundTiles/DirtTiles.png (50px 칸, 8열. 47가지 모양 + 꽉 찬 칸 무늬 3가지)
//   어떤 칸이 흙인지는 서버가 기준이다: 월드에 들어가면 그 월드의 바뀐 칸 전부(tiles), 다른 사람이 바꾸면 한 칸(tile)을 받는다
//   InventoryUI 가 시작할 때 스스로 만든다 (씬에 따로 놓지 않아도 됨)
public class GroundTileLayer : MonoBehaviour
{
    public const string Grass = "grass";
    public const string Dirt = "dirt";

    private const string AtlasPath = "GroundTiles/DirtTiles";
    private const int AtlasColumns = 8;
    private const int TilePixels = 50;
    private const float PixelsPerUnit = 100f;
    private const int FullVariants = 3;

    // 이웃 방향 비트 (위가 북)
    private const int N = 1, NE = 2, E = 4, SE = 8, S = 16, SW = 32, W = 64, NW = 128;

    // 그림 칸 순서 = 이 모양 번호 순서 (그림을 만든 스크립트와 같은 순서)
    private static readonly int[] Shapes =
    {
        0, 1, 4, 5, 7, 16, 17, 20, 21, 23, 28, 29, 31, 64, 65, 68, 69, 71, 80, 81, 84, 85, 87, 92, 93, 95,
        112, 113, 116, 117, 119, 124, 125, 127, 193, 197, 199, 209, 213, 215, 221, 223, 241, 245, 247, 253, 255
    };

    private static GroundTileLayer instance;

    private Tilemap paint;
    private readonly Dictionary<Vector3Int, string> cells = new Dictionary<Vector3Int, string>();
    private readonly Dictionary<int, Tile> dirtTiles = new Dictionary<int, Tile>();
    private Tile[] fullVariants;
    private ChatClient chatClient;

    public static GroundTileLayer Instance => instance;

    // 칠하는 타일맵 (칸 좌표 계산용). 아직 마을 바닥을 찾지 못했으면 null
    public Tilemap Paint => EnsurePaintTilemap() ? paint : null;

    public static GroundTileLayer Ensure()
    {
        if (instance != null)
            return instance;

        GameObject holder = new GameObject("GroundTileLayer");
        instance = holder.AddComponent<GroundTileLayer>();
        return instance;
    }

    private void OnEnable()
    {
        chatClient = FindFirstObjectByType<ChatClient>(FindObjectsInactive.Include);

        if (chatClient != null)
        {
            chatClient.TilesReceived += OnTiles;
            chatClient.TileReceived += OnTile;
        }
    }

    private void OnDisable()
    {
        if (chatClient != null)
        {
            chatClient.TilesReceived -= OnTiles;
            chatClient.TileReceived -= OnTile;
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    // 이 칸의 바닥 종류 (덧칠하지 않았으면 잔디)
    public string Get(Vector3Int cell)
    {
        return cells.TryGetValue(cell, out string tile) ? tile : Grass;
    }

    // 한 칸을 바꾸고 주변 칸의 모양을 다시 고른다
    public void Set(Vector3Int cell, string tile)
    {
        if (tile == Grass || string.IsNullOrEmpty(tile))
            cells.Remove(cell);
        else
            cells[cell] = tile;

        if (!EnsurePaintTilemap())
            return;

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
                Redraw(cell + new Vector3Int(dx, dy, 0));
        }
    }

    // 서버가 보낸 이 월드의 바뀐 칸 전부로 바꾼다
    private void OnTiles(TileListMessage m)
    {
        cells.Clear();

        if (m.list != null)
        {
            foreach (TileMessage t in m.list)
            {
                if (t != null && !string.IsNullOrEmpty(t.tile) && t.tile != Grass)
                    cells[new Vector3Int(t.x, t.y, 0)] = t.tile;
            }
        }

        RedrawAll();
    }

    private void OnTile(TileMessage t)
    {
        if (t != null)
            Set(new Vector3Int(t.x, t.y, 0), t.tile);
    }

    private void RedrawAll()
    {
        if (!EnsurePaintTilemap())
            return;

        paint.ClearAllTiles();

        foreach (Vector3Int cell in cells.Keys)
            Redraw(cell);
    }

    private void Redraw(Vector3Int cell)
    {
        paint.SetTile(cell, Get(cell) == Dirt ? DirtTileFor(cell) : null);
    }

    // 이웃 8칸으로 흙 모양을 고른다 (모서리 칸은 양옆 두 변이 모두 흙일 때만 따진다)
    private Tile DirtTileFor(Vector3Int cell)
    {
        int mask = 0;

        if (IsDirt(cell, 0, 1)) mask |= N;
        if (IsDirt(cell, 1, 1)) mask |= NE;
        if (IsDirt(cell, 1, 0)) mask |= E;
        if (IsDirt(cell, 1, -1)) mask |= SE;
        if (IsDirt(cell, 0, -1)) mask |= S;
        if (IsDirt(cell, -1, -1)) mask |= SW;
        if (IsDirt(cell, -1, 0)) mask |= W;
        if (IsDirt(cell, -1, 1)) mask |= NW;

        if ((mask & N) == 0 || (mask & E) == 0) mask &= ~NE;
        if ((mask & E) == 0 || (mask & S) == 0) mask &= ~SE;
        if ((mask & S) == 0 || (mask & W) == 0) mask &= ~SW;
        if ((mask & W) == 0 || (mask & N) == 0) mask &= ~NW;

        LoadTiles();

        // 속이 꽉 찬 칸: 위치로 정한 무늬를 섞어 반복이 덜 보이게
        if (mask == 255 && fullVariants != null)
        {
            int v = Mathf.Abs(cell.x * 92821 + cell.y * 68917) % (FullVariants + 1);

            if (v > 0 && fullVariants[v - 1] != null)
                return fullVariants[v - 1];
        }

        return dirtTiles.TryGetValue(mask, out Tile tile) ? tile : null;
    }

    private bool IsDirt(Vector3Int cell, int dx, int dy)
    {
        return Get(cell + new Vector3Int(dx, dy, 0)) == Dirt;
    }

    // 그림에서 칸마다 타일을 만든다 (처음 한 번)
    private void LoadTiles()
    {
        if (dirtTiles.Count > 0)
            return;

        Texture2D atlas = Resources.Load<Texture2D>(AtlasPath);

        if (atlas == null)
        {
            Debug.LogWarning("[바닥 타일] 흙 타일 그림을 찾지 못했어요: Resources/" + AtlasPath);
            return;
        }

        atlas.filterMode = FilterMode.Point;
        fullVariants = new Tile[FullVariants];

        for (int i = 0; i < Shapes.Length + FullVariants; i++)
        {
            Tile tile = MakeTile(atlas, i);

            if (i < Shapes.Length)
                dirtTiles[Shapes[i]] = tile;
            else
                fullVariants[i - Shapes.Length] = tile;
        }
    }

    // 그림의 i 번째 칸 (왼쪽 위부터 오른쪽으로, 8칸씩 줄바꿈)
    private static Tile MakeTile(Texture2D atlas, int index)
    {
        int column = index % AtlasColumns;
        int row = index / AtlasColumns;
        Rect rect = new Rect(column * TilePixels, atlas.height - (row + 1) * TilePixels, TilePixels, TilePixels);

        Sprite sprite = Sprite.Create(atlas, rect, new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
        sprite.name = "Dirt_" + index;

        Tile tile = ScriptableObject.CreateInstance<Tile>();
        tile.name = sprite.name;
        tile.sprite = sprite;
        tile.colliderType = Tile.ColliderType.None;
        return tile;
    }

    // 마을 잔디 타일맵 바로 위에 칠할 타일맵을 만든다 (잔디 타일맵의 자식이라 필드·집 안에 가면 함께 숨는다)
    private bool EnsurePaintTilemap()
    {
        if (paint != null)
            return true;

        FieldSceneManager field = FieldSceneManager.Instance != null
            ? FieldSceneManager.Instance
            : FindFirstObjectByType<FieldSceneManager>(FindObjectsInactive.Include);

        GameObject floorObject = field != null ? field.VillageTilemap : null;
        Tilemap floor = floorObject != null ? floorObject.GetComponent<Tilemap>() : null;

        if (floor == null)
            return false;

        GameObject go = new GameObject("GroundPaint");
        go.transform.SetParent(floor.transform, false);
        paint = go.AddComponent<Tilemap>();
        paint.tileAnchor = floor.tileAnchor;
        paint.orientation = floor.orientation;

        TilemapRenderer floorRenderer = floor.GetComponent<TilemapRenderer>();
        TilemapRenderer renderer = go.AddComponent<TilemapRenderer>();

        if (floorRenderer != null)
        {
            renderer.sortingLayerID = floorRenderer.sortingLayerID;
            renderer.sortingOrder = floorRenderer.sortingOrder + 1; // 잔디 바로 위, 격자선·건물 아래
            renderer.sharedMaterial = floorRenderer.sharedMaterial;
        }

        renderer.mode = TilemapRenderer.Mode.Chunk;
        RedrawAll();
        return true;
    }
}
