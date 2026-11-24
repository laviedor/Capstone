using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 바닥 바꾸기 모드 (삽이 있을 때 건설 창 [타일] 탭에서 타일을 고르면 켜진다).
//   마우스가 가리키는 칸에 고른 타일이 반투명하게 미리 보이고, 좌클릭하면 그 칸을 바꾼다.
//   한 칸을 바꿔도 모드는 끝나지 않는다: 계속 클릭해서 여러 칸을 바꾸고, 우클릭(또는 아래 안내 바의 [끝내기])으로 끝낸다.
//   좌클릭한 채로 끌면 지금처럼 화면을 둘러본다 (칠하지 않음).
//   내 마을의 바깥(마을 구역)에서만, 삽을 가지고 있을 때만 쓸 수 있다. 서버에 바뀐 칸을 저장한다 (거절되면 서버 값으로 되돌아감)
//   처음 쓸 때 스스로 만들어진다 (씬에 따로 놓지 않아도 됨)
public class TilePainter : MonoBehaviour
{
    private static readonly Vector2 BarSize = new Vector2(372f, 32f);
    private const float BottomMargin = 14f;
    private static readonly Color Brown = new Color32(0x9C, 0x6A, 0x3E, 0xFF);
    private static readonly Color HoverOk = new Color(1f, 1f, 1f, 0.9f);
    private static readonly Color HoverBad = new Color(1f, 0.35f, 0.3f, 0.9f);

    private static TilePainter instance;

    private string tile;              // 칠할 타일 (GroundTileLayer.Dirt / Grass)
    private string tileName;
    private RectTransform bar;
    private TMP_Text messageText;
    private SpriteRenderer hover;     // 가리키는 칸 테두리
    private SpriteRenderer ghost;     // 칠할 타일 미리보기
    private Sprite dirtPreview;
    private ChatClient chatClient;
    private CameraFollow cameraFollow;
    private BuildingModeManager buildingModeManager;
    private InventoryUI inventoryUI;
    private Vector2 pressPosition;
    private bool pressPending;
    private float startedAt;
    private bool warnedOffline;

    public static bool IsActive => instance != null && instance.tile != null;

    // tile 로 칠하기 시작한다. 못 쓰는 곳이면 안내만 하고 false
    public static bool Begin(string tile, string displayName)
    {
        if (!CanPaintHere(out string reason))
        {
            Toast.Show(reason);
            return false;
        }

        if (instance == null)
        {
            RectTransform canvas = CozyRuntimeUI.CreateCanvas("TilePainterHUD", 250);
            instance = canvas.gameObject.AddComponent<TilePainter>();
            instance.Build(canvas);
        }

        instance.Activate(tile, displayName);
        return true;
    }

    public static void End()
    {
        if (instance != null)
            instance.Stop();
    }

    // 지금 바닥을 바꿀 수 있는지 (못 바꾸면 reason 에 안내)
    public static bool CanPaintHere(out string reason)
    {
        reason = "";

        if (!ToolInventory.HasShovel)
        {
            reason = "삽이 있어야 바닥을 바꿀 수 있어요. 대장간에서 만들어 보세요.";
            return false;
        }

        WorldSync world = WorldSync.Instance;

        if (world == null || !world.Entered)
        {
            reason = "서버에 연결되어 있어야 바닥을 바꿀 수 있어요.";
            return false;
        }

        if (!world.IsMine || world.InMultiverse)
        {
            reason = "내 마을에서만 바닥을 바꿀 수 있어요.";
            return false;
        }

        if (BuildingRules.InHouse || BuildingRules.InField)
        {
            reason = "마을 바깥에서만 바닥을 바꿀 수 있어요.";
            return false;
        }

        return true;
    }

    private void Build(RectTransform canvas)
    {
        // 아래 가운데 안내 바 (건설 안내 바와 같은 모양)
        Image background = CozyRuntimeUI.Image(canvas, "Bar", "pill_cream", CozyRuntimeUI.Cream, true);
        bar = background.rectTransform;
        bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0f);
        bar.pivot = new Vector2(0.5f, 0f);
        bar.anchoredPosition = new Vector2(0f, BottomMargin);
        bar.sizeDelta = BarSize;

        Image chip = CozyRuntimeUI.Image(bar, "ModeChip", "btn_secondary", Brown);
        CozyRuntimeUI.TopLeft(chip.rectTransform, 5f, 4f, 52f, 23f);
        TMP_Text chipText = CozyRuntimeUI.Text(chip.transform, "Label", "타일", 11f, CozyRuntimeUI.Wood,
            TextAlignmentOptions.Center, true);
        CozyRuntimeUI.Stretch(chipText.rectTransform, 0f, 0f, 0f, 2f);

        messageText = CozyRuntimeUI.Text(bar, "Message", "", 12f, CozyRuntimeUI.Ink, TextAlignmentOptions.MidlineLeft, true);
        CozyRuntimeUI.TopLeft(messageText.rectTransform, 64f, 0f, BarSize.x - 64f - 82f, BarSize.y);

        Button button = CozyRuntimeUI.Button(bar, "ExitButton", "btn_secondary", "끝내기", CozyRuntimeUI.Wood, 11f,
            Stop, CozyRuntimeUI.Cream);
        CozyRuntimeUI.TopLeft((RectTransform)button.transform, BarSize.x - 5f - 72f, 4f, 72f, 23f);

        // 월드에 그리는 칸 표시
        ghost = NewWorldSprite("TilePaintGhost", -8);
        hover = NewWorldSprite("TilePaintHover", -7);
        hover.sprite = OutlineSprite();

        bar.gameObject.SetActive(false);
    }

    private static SpriteRenderer NewWorldSprite(string name, int order)
    {
        GameObject go = new GameObject(name);
        go.hideFlags = HideFlags.HideInHierarchy;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = order;
        go.SetActive(false);
        return sr;
    }

    // 한 칸(0.5 유닛) 크기의 흰 테두리 그림
    private static Sprite OutlineSprite()
    {
        const int size = 25;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool edge = x < 2 || y < 2 || x >= size - 2 || y >= size - 2;
                pixels[y * size + x] = edge ? new Color32(255, 255, 255, 230) : new Color32(255, 255, 255, 28);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 50f);
    }

    private void Activate(string newTile, string displayName)
    {
        tile = newTile;
        tileName = displayName;
        startedAt = Time.unscaledTime;
        pressPending = false;

        if (chatClient == null) chatClient = FindFirstObjectByType<ChatClient>(FindObjectsInactive.Include);
        if (cameraFollow == null) cameraFollow = FindFirstObjectByType<CameraFollow>(FindObjectsInactive.Include);
        if (buildingModeManager == null) buildingModeManager = FindFirstObjectByType<BuildingModeManager>(FindObjectsInactive.Include);
        if (inventoryUI == null) inventoryUI = FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);

        // 건설·철거 모드와 동시에 켜지지 않게
        if (buildingModeManager != null && buildingModeManager.IsBuildMode)
            buildingModeManager.ExitBuildMode();

        ghost.sprite = tile == GroundTileLayer.Dirt ? DirtPreview() : GrassPreview();
        messageText.text = $"<b>{tileName}</b>: 바꿀 칸을 클릭 · 우클릭 끝내기";
        messageText.richText = true;
        bar.gameObject.SetActive(true);
    }

    private void Stop()
    {
        tile = null;

        if (bar != null) bar.gameObject.SetActive(false);
        if (hover != null) hover.gameObject.SetActive(false);
        if (ghost != null) ghost.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;

        if (hover != null) Destroy(hover.gameObject);
        if (ghost != null) Destroy(ghost.gameObject);
    }

    private void Update()
    {
        if (tile == null)
            return;

        // 못 쓰는 곳으로 옮겼거나(친구 마을, 필드, 집 안) 삽이 없어졌거나 다른 모드·창이 켜지면 끝낸다
        if (!CanPaintHere(out _) ||
            (buildingModeManager != null && buildingModeManager.IsBuildMode) ||
            (inventoryUI != null && inventoryUI.IsOpen) ||
            CraftingUI.IsOpen)
        {
            Stop();
            return;
        }

        Mouse mouse = Mouse.current;

        if (mouse == null || Camera.main == null)
            return;

        // 우클릭으로 끝낸다
        if (mouse.rightButton.wasPressedThisFrame)
        {
            Stop();
            return;
        }

        GroundTileLayer layer = GroundTileLayer.Ensure();
        var paint = layer.Paint;

        if (paint == null)
            return;

        Vector2 screen = mouse.position.ReadValue();
        Vector3 world = Camera.main.ScreenToWorldPoint(screen);
        Vector3Int cell = paint.WorldToCell(new Vector3(world.x, world.y, 0f));
        Vector3 center = paint.GetCellCenterWorld(cell);
        bool overUI = UIInput.IsPointerOverUI();
        bool inside = InsideVillage(center);

        // 가리키는 칸 표시 (마을 밖이면 붉게)
        hover.gameObject.SetActive(!overUI);
        ghost.gameObject.SetActive(!overUI && inside);
        hover.transform.position = new Vector3(center.x, center.y, 0f);
        ghost.transform.position = hover.transform.position;
        hover.color = inside ? HoverOk : HoverBad;
        ghost.color = new Color(1f, 1f, 1f, 0.7f);
        Vector3 cellSize = paint.layoutGrid != null ? paint.layoutGrid.cellSize : new Vector3(0.5f, 0.5f, 0f);
        hover.transform.localScale = new Vector3(cellSize.x / 0.5f, cellSize.y / 0.5f, 1f);

        // 좌클릭: 누른 자리에서 크게 움직이지 않고 떼면 칠한다 (끌면 화면 둘러보기)
        if (mouse.leftButton.wasPressedThisFrame)
        {
            pressPending = !overUI && Time.unscaledTime - startedAt > 0.1f;
            pressPosition = screen;
        }

        if (pressPending && mouse.leftButton.isPressed)
        {
            float threshold = cameraFollow != null ? cameraFollow.DragThresholdPixels : 8f;

            if (Vector2.Distance(screen, pressPosition) >= threshold)
                pressPending = false;
        }

        if (pressPending && mouse.leftButton.wasReleasedThisFrame)
        {
            pressPending = false;

            if (cameraFollow != null)
                cameraFollow.ResumeFollow();

            if (!inside)
            {
                Toast.Show("마을 안의 칸만 바꿀 수 있어요.");
                return;
            }

            PaintCell(layer, cell);
        }
    }

    private void PaintCell(GroundTileLayer layer, Vector3Int cell)
    {
        if (layer.Get(cell) == tile)
            return;

        // 화면에 먼저 바꾸고 서버에 알린다 (거절되면 서버가 바닥 전체를 다시 보내 되돌린다)
        layer.Set(cell, tile);

        if ((chatClient == null || !chatClient.SendTile(cell.x, cell.y, tile)) && !warnedOffline)
        {
            warnedOffline = true;
            Toast.Show("서버에 연결되어 있지 않아 바꾼 바닥이 저장되지 않아요.");
        }
    }

    // 마을 맵 경계 안인지 (경계를 모르면 어디든 허용)
    private bool InsideVillage(Vector3 point)
    {
        MapBounds bounds = cameraFollow != null ? cameraFollow.CurrentMapBounds : null;
        return bounds == null || bounds.Bounds.Contains(new Vector3(point.x, point.y, bounds.Bounds.center.z));
    }

    // 미리보기: 흙 타일 그림의 꽉 찬 칸 / 잔디 타일
    private Sprite DirtPreview()
    {
        if (dirtPreview != null)
            return dirtPreview;

        Texture2D atlas = Resources.Load<Texture2D>("GroundTiles/DirtTiles");

        if (atlas == null)
            return null;

        // 마지막 모양(46번, 사방이 흙)의 칸
        const int index = 46;
        Rect rect = new Rect(index % 8 * 50, atlas.height - (index / 8 + 1) * 50, 50, 50);
        return dirtPreview = Sprite.Create(atlas, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }

    private static Sprite GrassPreview()
    {
        // 잔디로 되돌리기: 칸 테두리만 보여 준다
        return null;
    }
}
