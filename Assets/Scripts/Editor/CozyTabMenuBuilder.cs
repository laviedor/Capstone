using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using static CozySpriteFactory;

// Village 씬의 Tab 메뉴(InventoryUI)를 새 디자인으로 바꾸는 에디터 메뉴.
// 기존 오브젝트와 스크립트 연결은 그대로 두고 모양·배치만 바꾸며, 플레이어 탭의 장비/인벤토리 UI는 새로 만든다.
// 여러 번 실행해도 같은 결과가 나오도록 만들어져 있다.
public static class CozyTabMenuBuilder
{
    // 창 크기 (캔버스 기준 해상도 1920 × 270)
    private const float WindowWidth = 780f;
    private const float WindowHeight = 188f;
    private const float PadX = 14f;
    private const float PadY = 12f;

    // 안쪽 영역: 752 × 164
    private const float InnerW = WindowWidth - PadX * 2f;
    private const float InnerH = WindowHeight - PadY * 2f;

    // 플레이어 탭 장비 카드 폭과 장비 칸 크기
    private const float EquipW = 260f;
    private const float SlotSize = 52f;

    private const string EquipmentFolder = "Assets/Data/Equipment";

    // 한글이 들어 있는 D2Coding SDF 폰트 에셋 (원본 폰트 파일을 가져오는 데 쓴다)
    private const string KoreanFontGuid = "025fa76581d74654482409fa39906ca3";

    // Tab 메뉴 전용 한글 폰트 에셋.
    // 기존 D2Coding SDF는 글자 그림판(1024)이 한 장뿐이라 약 100글자에서 꽉 차 새 한글이 □로 깨진다.
    // 작은 크기로 그리고 그림판이 차면 새 장을 추가하도록(멀티 아틀라스) 따로 만든다.
    private const string CozyFontFolder = "Assets/UI/Cozy/Fonts";
    private const string CozyFontPath = CozyFontFolder + "/CozyKorean SDF.asset";

    private static TMP_FontAsset font;
    private static bool recordUndo;

    // 스프라이트 묶음
    private class Kit
    {
        public Sprite window, tabOn, tabOff, card, panelCream, slot, slotEmpty, equipOn, equipOff;
        public Sprite ring10, ring14, btnPrimary, btnSecondary, btnDanger, btnDisabled;
        public Sprite pillWood, pillCream, pillChip, pillSoft, pillPlain, pillCoral, white, map;
        public Sprite closeCircle, dotPlayer, dotBuilding, dotField, pinVillage, pinForest, pinLocked;
        public Sprite badgeCheck, badgeLock, halo, pedestal, pond, hillBlob, meadow;
        public Sprite gPerson, gHouse, gPin, gClose, gCheck, gLock;
        public Sprite mVillage, mForest, mHill, mBeach;
        public Sprite ghostHat, ghostTop, ghostBottom, ghostTool;
    }

    [MenuItem("Tools/Cozy UI/Tab 메뉴에 새 디자인 적용")]
    public static void Apply()
    {
        InventoryUI inventoryUI = FindInventoryUI();

        if (inventoryUI == null)
        {
            EditorUtility.DisplayDialog("Cozy UI",
                "열려 있는 씬에서 InventoryUI를 찾지 못했어요.\nVillage 씬을 열고 다시 실행해 주세요.", "확인");
            return;
        }

        if (!EditorUtility.DisplayDialog("Cozy UI",
                "Tab 메뉴를 새 디자인으로 바꿔요.\n\n" +
                "• 씬 변경은 Ctrl+Z(⌘Z)로 되돌릴 수 있어요.\n" +
                "• 건물 카드 프리팹 2개도 함께 바뀌어요 (되돌리기 대상이 아님).\n\n" +
                "실행하기 전에 git 커밋으로 백업해 두는 걸 권장해요.",
                "적용", "취소"))
        {
            return;
        }

        try
        {
            EditorUtility.DisplayProgressBar("Cozy UI", "스프라이트 만드는 중…", 0.1f);
            ClearCache();
            Kit kit = BuildKit();

            EditorUtility.DisplayProgressBar("Cozy UI", "샘플 장비 만드는 중…", 0.4f);
            List<EquipmentItemData> items = EnsureSampleEquipment();

            EditorUtility.DisplayProgressBar("Cozy UI", "Tab 메뉴 바꾸는 중…", 0.6f);
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Cozy Tab 메뉴 적용");
            recordUndo = true;

            EnsurePlayerEquipment(items);
            BuildScene(inventoryUI, kit);

            Undo.CollapseUndoOperations(undoGroup);
            recordUndo = false;

            EditorUtility.DisplayProgressBar("Cozy UI", "건물 카드 프리팹 바꾸는 중…", 0.85f);
            if (font == null)
                font = FindKoreanFont(inventoryUI.gameObject);

            StylePrefabs(inventoryUI, kit);

            EditorSceneManager.MarkSceneDirty(inventoryUI.gameObject.scene);
            Debug.Log("[Cozy UI] Tab 메뉴에 새 디자인을 적용했어요. 씬을 저장하면 반영돼요.");
        }
        finally
        {
            recordUndo = false;
            EditorUtility.ClearProgressBar();
        }
    }

    // 한글을 표시할 수 있는 TMP 폰트를 준비한다. D2Coding 원본으로 전용 폰트 에셋을 만들어 우선 쓴다.
    private static TMP_FontAsset FindKoreanFont(GameObject window)
    {
        string path = AssetDatabase.GUIDToAssetPath(KoreanFontGuid);
        TMP_FontAsset preferred = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);

        TMP_FontAsset cozy = EnsureCozyFont(preferred != null ? preferred.sourceFontFile : null);

        if (cozy != null)
            return cozy;

        if (preferred != null)
        {
            preferred.isMultiAtlasTexturesEnabled = true;
            EditorUtility.SetDirty(preferred);
            return preferred;
        }

        // 창 안의 글자 중 한글이 표시되는 폰트
        foreach (TMP_Text text in window.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.font != null && text.font.HasCharacter('가', true, true))
                return text.font;
        }

        // 프로젝트 전체에서 찾기
        foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
        {
            TMP_FontAsset asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));

            if (asset != null && asset.HasCharacter('가', true, true))
                return asset;
        }

        return null;
    }

    private static TMP_FontAsset EnsureCozyFont(Font source)
    {
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(CozyFontPath);

        if (existing != null)
        {
            existing.isMultiAtlasTexturesEnabled = true;
            return existing;
        }

        if (source == null)
            return null;

        TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(
            source, 40, 5, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);

        if (asset == null)
            return null;

        EnsureFolder(CozyFontFolder);
        asset.name = "CozyKorean SDF";
        AssetDatabase.CreateAsset(asset, CozyFontPath);

        // 아틀라스 텍스처와 머티리얼을 폰트 에셋 안에 함께 저장한다.
        Texture2D atlas = asset.atlasTextures[0];
        atlas.name = "CozyKorean Atlas";
        AssetDatabase.AddObjectToAsset(atlas, asset);

        Material material = asset.material;
        material.name = "CozyKorean Material";
        AssetDatabase.AddObjectToAsset(material, asset);

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return asset;
    }

    private static InventoryUI FindInventoryUI()
    {
        InventoryUI[] all = Object.FindObjectsByType<InventoryUI>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        InventoryUI fallback = null;

        foreach (InventoryUI ui in all)
        {
            if (ui.gameObject.activeInHierarchy)
                return ui;

            fallback = ui;
        }

        return fallback;
    }

    // ===============================================================
    // 스프라이트
    // ===============================================================

    private static Kit BuildKit()
    {
        Color sand2 = C("#F3E3C7");

        return new Kit
        {
            window = Panel("window", Cream, Wood, 3, 18, 5, WoodDark),
            tabOn = Panel("tab_on", Cream, Wood, 3, 12, 0, default, true),
            tabOff = Panel("tab_off", TabOff, Wood, 3, 12, 0, default, true),
            card = Panel("card", Sand, SandLine, 2, 14),
            panelCream = Panel("panel_cream", Cream, SandLine, 2, 14),
            slot = Panel("slot", Cream, SlotLine, 2, 10, 2, SandLine),
            slotEmpty = Panel("slot_empty", C("#EFDCBB"), C("#E2CBA5"), 1.5f, 10),
            equipOn = Panel("equip_slot", Cream, Tan, 2, 14, 3, C("#DCC29A")),
            equipOff = Panel("equip_slot_empty", C("#FBEFDC"), C("#CDB188"), 2, 14),
            ring10 = Ring("ring_10", 10),
            ring14 = Ring("ring_14", 14),
            btnPrimary = Panel("btn_primary", Green, GreenDark, 2, 10, 3, GreenDark),
            btnSecondary = Panel("btn_secondary", Cream, Tan, 2, 10, 2, SandLine),
            btnDanger = Panel("btn_danger", Cream, C("#E3A08D"), 2, 10, 2, C("#F0C7BA")),
            btnDisabled = Panel("btn_disabled", C("#E6D6BC"), C("#CDB188"), 2, 10),
            pillWood = Panel("pill_wood", Wood, Wood, 0, 10),
            pillCream = Panel("pill_cream", Cream, Wood, 2, 10),
            pillChip = Panel("pill_chip", Cream, SlotLine, 2, 10),
            pillSoft = Panel("pill_soft", sand2, SandLine, 2, 10),
            pillPlain = Panel("pill_plain", Cream, Cream, 0, 8),
            pillCoral = Panel("pill_coral", Coral, Wood, 1.5f, 8),
            white = Panel("rounded_white", Color.white, Color.white, 0, 10),
            map = Panel("map", C("#CFE6B0"), C("#A5C987"), 2, 14),
            closeCircle = Circle("close_circle", 28, Coral, Wood, 2, 3, Wood),
            dotPlayer = Circle("dot_player", 20, C("#F7C59F"), Ink, 0),
            dotBuilding = Circle("dot_building", 20, C("#BFDDA6"), Ink, 0),
            dotField = Circle("dot_field", 20, C("#A9D3EA"), Ink, 0),
            pinVillage = Circle("pin_village", 56, C("#FBE0C8"), Wood, 2.5f, 4, Wood),
            pinForest = Circle("pin_forest", 56, C("#DCEFC9"), Wood, 2.5f, 4, Wood),
            pinLocked = Circle("pin_locked", 56, C("#E9E1D2"), C("#A08C74"), 2.5f, 4, C("#A08C74")),
            badgeCheck = Circle("badge_check", 16, Green, Cream, 1.5f),
            badgeLock = Circle("badge_lock", 20, Wood, Cream, 1.5f),
            halo = Ellipse("stage_halo", 150, 128, C("#FCEFD9")),
            pedestal = Ellipse("stage_pedestal", 120, 24, C("#E3C79C"), Tan, 2),
            pond = Ellipse("map_pond", 110, 48, C("#A9D3EA")),
            hillBlob = Ellipse("map_hill_blob", 180, 70, C("#DCD6C6")),
            meadow = Ellipse("map_meadow", 170, 80, C("#E8E0B8")),
            gPerson = CozyIcons.Person(),
            gHouse = CozyIcons.House(),
            gPin = CozyIcons.Pin(),
            gClose = CozyIcons.Close(),
            gCheck = CozyIcons.Check(),
            gLock = CozyIcons.Lock(),
            mVillage = CozyIcons.Village(),
            mForest = CozyIcons.Forest(),
            mHill = CozyIcons.Hill(),
            mBeach = CozyIcons.Beach(),
            ghostHat = CozyIcons.Hat("ghost_hat", Color.white, true),
            ghostTop = CozyIcons.Shirt("ghost_top", Color.white, true),
            ghostBottom = CozyIcons.Pants("ghost_bottom", Color.white, true),
            ghostTool = CozyIcons.Axe("ghost_tool", true),
        };
    }

    // ===============================================================
    // 장비 데이터
    // ===============================================================

    // 샘플 장비 에셋을 만든다. 이미 있으면 그대로 쓴다.
    private static List<EquipmentItemData> EnsureSampleEquipment()
    {
        EnsureFolder(EquipmentFolder);

        return new List<EquipmentItemData>
        {
            Item("StrawHat", "밀짚모자", EquipmentSlot.Hat, "챙이 넓어 햇볕 아래에서도 시원한 모자예요.",
                () => CozyIcons.Hat("item_straw_hat", C("#EBC35F"))),
            Item("FlowerHat", "꽃 모자", EquipmentSlot.Hat, "분홍 꽃잎 색으로 물들인 귀여운 모자예요.",
                () => CozyIcons.Hat("item_flower_hat", C("#F2A7B8"))),
            Item("CheckShirt", "체크 셔츠", EquipmentSlot.Top, "포근한 면으로 만든 일할 때 입는 셔츠예요.",
                () => CozyIcons.Shirt("item_check_shirt", C("#E98B6A"))),
            Item("StripeShirt", "줄무늬 티셔츠", EquipmentSlot.Top, "하늘색 줄무늬가 들어간 가벼운 티셔츠예요.",
                () => CozyIcons.Shirt("item_stripe_shirt", C("#8FB8DE"))),
            Item("Overalls", "멜빵 바지", EquipmentSlot.Bottom, "주머니가 많아 도구를 넣어 다니기 좋아요.",
                () => CozyIcons.Pants("item_overalls", C("#6F8FC9"))),
            Item("WoodAxe", "나무 도끼", EquipmentSlot.Tool, "숲에서 나무를 벨 때 쓰는 기본 도끼예요.",
                () => CozyIcons.Axe("item_wood_axe")),
            Item("StonePick", "돌 곡괭이", EquipmentSlot.Tool, "바위를 깨서 돌을 모을 때 쓰는 곡괭이예요.",
                () => CozyIcons.Pick("item_stone_pick")),
        };
    }

    private static EquipmentItemData Item(string id, string itemName, EquipmentSlot slot,
        string description, System.Func<Sprite> makeIcon)
    {
        string path = EquipmentFolder + "/" + id + ".asset";
        EquipmentItemData data = AssetDatabase.LoadAssetAtPath<EquipmentItemData>(path);

        if (data != null)
        {
            // 아이콘이 비어 있을 때만 채운다 (사용자가 바꾼 아이콘은 유지)
            if (data.icon == null)
            {
                data.icon = makeIcon();
                EditorUtility.SetDirty(data);
            }

            return data;
        }

        data = ScriptableObject.CreateInstance<EquipmentItemData>();
        data.itemID = id;
        data.itemName = itemName;
        data.slot = slot;
        data.description = description;
        data.icon = makeIcon();
        AssetDatabase.CreateAsset(data, path);
        return data;
    }

    // 씬에 PlayerEquipment가 없으면 만들고 샘플 장비를 시작 장비로 넣는다.
    private static void EnsurePlayerEquipment(List<EquipmentItemData> items)
    {
        PlayerEquipment equipment = Object.FindFirstObjectByType<PlayerEquipment>(FindObjectsInactive.Include);

        if (equipment != null)
            return;

        GameObject go = new GameObject("PlayerEquipment");
        Undo.RegisterCreatedObjectUndo(go, "Create PlayerEquipment");
        equipment = go.AddComponent<PlayerEquipment>();

        SetRefs(equipment, "startingItems", items.ToArray());
        SetRefs(equipment, "startingEquipped", new Object[] { items[0], items[2], items[5] });
    }

    // ===============================================================
    // 씬
    // ===============================================================

    private static void BuildScene(InventoryUI inventoryUI, Kit kit)
    {
        SerializedObject inv = new SerializedObject(inventoryUI);
        GameObject window = (GameObject)inv.FindProperty("inventoryWindow").objectReferenceValue;
        GameObject playerPanel = (GameObject)inv.FindProperty("playerPanel").objectReferenceValue;
        GameObject buildingPanel = (GameObject)inv.FindProperty("buildingPanel").objectReferenceValue;
        GameObject fieldPanel = (GameObject)inv.FindProperty("fieldPanel").objectReferenceValue;
        Button playerTab = (Button)inv.FindProperty("playerTabButton").objectReferenceValue;
        Button buildingTab = (Button)inv.FindProperty("buildingTabButton").objectReferenceValue;
        Button fieldTab = (Button)inv.FindProperty("fieldTabButton").objectReferenceValue;
        ConstructionListUI constructionList =
            (ConstructionListUI)inv.FindProperty("constructionListUI").objectReferenceValue;

        Undo.RegisterFullObjectHierarchyUndo(window, "Cozy Tab 메뉴 적용");

        font = FindKoreanFont(window);

        if (font == null)
            Debug.LogWarning("[Cozy UI] 한글 폰트를 찾지 못했어요. 글자가 깨지면 TMP 폰트 에셋을 확인해 주세요.");

        BuildWindow(inventoryUI, window, playerTab, buildingTab, fieldTab, kit);
        BuildPlayerTab(inventoryUI, playerPanel, constructionList, kit);
        BuildBuildingTab(buildingPanel, constructionList, kit);
        BuildFieldTab(fieldPanel, kit);

        // 창 안의 남은 글자(채집 시간 버튼 등)도 한글 폰트로 통일한다.
        if (font != null)
        {
            foreach (TMP_Text text in window.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.font == font)
                    continue;

                text.font = font;
                text.fontSharedMaterial = font.material;
            }
        }

        Selection.activeGameObject = window;
    }

    // ----- 창, 탭, 자원, 닫기 -----

    private static void BuildWindow(InventoryUI inventoryUI, GameObject window,
        Button playerTab, Button buildingTab, Button fieldTab, Kit kit)
    {
        RectTransform wrt = Rt(window);
        wrt.anchorMin = wrt.anchorMax = new Vector2(0.5f, 0.5f);
        wrt.pivot = new Vector2(0.5f, 0.5f);
        wrt.anchoredPosition = new Vector2(0f, -14f);
        wrt.sizeDelta = new Vector2(WindowWidth, WindowHeight);
        Img(window, kit.window, Color.white, true, true);

        // 탭 줄: 창 위쪽에 붙고, 선택된 탭은 3만큼 내려와 창 테두리를 덮는다.
        Transform row = playerTab.transform.parent;
        RemoveLayout(row.gameObject);
        RectTransform rowRt = Rt(row.gameObject);
        rowRt.anchorMin = rowRt.anchorMax = new Vector2(0f, 1f);
        rowRt.pivot = new Vector2(0f, 0f);
        rowRt.anchoredPosition = new Vector2(20f, 0f);
        rowRt.sizeDelta = new Vector2(320f, 34f);

        TabButtonVisual pv = StyleTab(playerTab, 0, "인벤토리", kit.dotPlayer, kit.gPerson, kit);
        TabButtonVisual bv = StyleTab(buildingTab, 1, "건설", kit.dotBuilding, kit.gHouse, kit);
        TabButtonVisual fv = StyleTab(fieldTab, 2, "탐색", kit.dotField, kit.gPin, kit);

        // 자원 표시: 기존 ResourceArea(InventoryResourceUI)를 창 오른쪽 위로 옮긴다.
        InventoryResourceUI resourceUI = window.GetComponentInChildren<InventoryResourceUI>(true);

        if (resourceUI != null)
            BuildResourcePills(window, resourceUI, kit);

        // 닫기 버튼
        GameObject close = Child(window.transform, "Cozy_CloseButton");
        RectTransform crt = Rt(close);
        crt.anchorMin = crt.anchorMax = new Vector2(1f, 1f);
        crt.pivot = new Vector2(1f, 0f);
        crt.anchoredPosition = new Vector2(-12f, 2f);
        crt.sizeDelta = new Vector2(28f, 31f);
        Img(close, kit.closeCircle, Color.white, false, true);
        Button closeButton = Btn(close);
        GameObject x = Child(close.transform, "Glyph");
        Center(Rt(x), 0f, 1.5f, 13f, 13f);
        Img(x, kit.gClose, Color.white, false, false);

        // 패널 영역
        Transform panelArea = window.transform.Find("InventoryPanel");

        if (panelArea == null && inventoryUI != null)
            panelArea = new SerializedObject(inventoryUI).FindProperty("playerPanel")
                .objectReferenceValue is GameObject pp ? pp.transform.parent : null;

        if (panelArea != null)
        {
            Stretch(Rt(panelArea.gameObject), PadX, PadY, PadX, PadY);
            Image areaImage = panelArea.GetComponent<Image>();

            if (areaImage != null)
            {
                areaImage.color = new Color(1f, 1f, 1f, 0f);
                areaImage.raycastTarget = false;
            }
        }

        SetRef(inventoryUI, "playerTabVisual", pv);
        SetRef(inventoryUI, "buildingTabVisual", bv);
        SetRef(inventoryUI, "fieldTabVisual", fv);
        SetRef(inventoryUI, "closeButton", closeButton);

        // 편집 화면에서도 플레이어 탭이 선택된 모양으로 보이게 한다.
        pv.SetActive(true);
        bv.SetActive(false);
        fv.SetActive(false);
    }

    private static TabButtonVisual StyleTab(Button tab, int index, string title,
        Sprite dot, Sprite glyph, Kit kit)
    {
        GameObject go = tab.gameObject;
        RectTransform rt = Rt(go);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);

        float x = index * 102f;
        Vector2 onPos = new Vector2(x, -3f);
        Vector2 offPos = new Vector2(x, 0f);
        Vector2 onSize = new Vector2(98f, 34f);
        Vector2 offSize = new Vector2(98f, 28f);

        Image bg = Img(go, kit.tabOff, Color.white, true, true);
        tab.targetGraphic = bg;
        tab.transition = Selectable.Transition.ColorTint;

        GameObject dotGo = Child(go.transform, "Cozy_TabIcon");
        RectTransform drt = Rt(dotGo);
        drt.anchorMin = drt.anchorMax = new Vector2(0f, 0.5f);
        drt.pivot = new Vector2(0f, 0.5f);
        drt.anchoredPosition = new Vector2(9f, 0f);
        drt.sizeDelta = new Vector2(20f, 20f);
        Img(dotGo, dot, Color.white, false, false);

        GameObject glyphGo = Child(dotGo.transform, "Glyph");
        Stretch(Rt(glyphGo), 3f, 3f, 3f, 3f);
        Img(glyphGo, glyph, Color.white, false, false);

        TMP_Text label = go.GetComponentInChildren<TMP_Text>(true);

        if (label == null)
            label = NewText(go.transform, "Label", title, 14, Ink, TextAlignmentOptions.MidlineLeft, true);

        Stretch(Rt(label.gameObject), 32f, 0f, 4f, 0f);
        StyleText(label, 13, Ink, TextAlignmentOptions.MidlineLeft, true);
        label.text = title;

        TabButtonVisual visual = GetOrAdd<TabButtonVisual>(go);
        SetRef(visual, "background", bg);
        SetRef(visual, "label", label);
        SetRef(visual, "rect", rt);
        SetRef(visual, "activeSprite", kit.tabOn);
        SetColor(visual, "activeColor", Color.white);
        SetColor(visual, "activeTextColor", Ink);
        SetVector(visual, "activeSize", onSize);
        SetVector(visual, "activePosition", onPos);
        SetRef(visual, "inactiveSprite", kit.tabOff);
        SetColor(visual, "inactiveColor", Color.white);
        SetColor(visual, "inactiveTextColor", WoodDark);
        SetVector(visual, "inactiveSize", offSize);
        SetVector(visual, "inactivePosition", offPos);
        SetBool(visual, "changePosition", true);

        rt.sizeDelta = offSize;
        rt.anchoredPosition = offPos;
        return visual;
    }

    private static void BuildResourcePills(GameObject window, InventoryResourceUI resourceUI, Kit kit)
    {
        GameObject area = resourceUI.gameObject;
        SetParent(area.transform, window.transform);
        RemoveLayout(area);

        RectTransform art = Rt(area);
        art.anchorMin = art.anchorMax = new Vector2(1f, 1f);
        art.pivot = new Vector2(1f, 0f);
        art.anchoredPosition = new Vector2(-48f, 3f);
        art.sizeDelta = new Vector2(228f, 26f);

        Image areaImage = area.GetComponent<Image>();

        if (areaImage != null)
            areaImage.enabled = false;

        Transform title = FindDeep(area.transform, "ResourceTitle");

        if (title != null)
            title.gameObject.SetActive(false);

        SerializedObject so = new SerializedObject(resourceUI);
        string[] texts = { "woodText", "stoneText", "goldText" };
        string[] icons = { "WoodImage", "StoneImage", "GoldImage" };
        string[] names = { "Wood", "Stone", "Gold" };

        for (int i = 0; i < 3; i++)
        {
            GameObject pill = Child(area.transform, "Cozy_Pill_" + names[i]);
            TL(Rt(pill), i * 78f, 0f, 72f, 26f);
            Img(pill, kit.pillCream, Color.white, true, false);

            Transform icon = FindDeep(area.transform, icons[i]);

            if (icon != null)
            {
                SetParent(icon, pill.transform);
                TL(Rt(icon.gameObject), 4f, 3f, 20f, 20f);
                Image iconImage = icon.GetComponent<Image>();

                if (iconImage != null)
                {
                    iconImage.preserveAspect = true;
                    iconImage.raycastTarget = false;
                }
            }

            TMP_Text text = so.FindProperty(texts[i]).objectReferenceValue as TMP_Text;

            if (text != null)
            {
                SetParent(text.transform, pill.transform);
                TL(Rt(text.gameObject), 26f, 0f, 42f, 26f);
                StyleText(text, 13, Ink, TextAlignmentOptions.MidlineLeft, true);
            }
        }

        SetBool(resourceUI, "numbersOnly", true);
    }

    // ----- 플레이어 탭 -----

    private static void BuildPlayerTab(InventoryUI inventoryUI, GameObject playerPanel,
        ConstructionListUI constructionList, Kit kit)
    {
        Transform pp = playerPanel.transform;
        Stretch(Rt(playerPanel), 0f, 0f, 0f, 0f);

        // ===== 장비 카드 =====
        GameObject equipCard = Child(pp, "Cozy_EquipCard");
        TL(Rt(equipCard), 0f, 0f, EquipW, InnerH);
        Img(equipCard, kit.card, Color.white, true, false);

        GameObject halo = Child(equipCard.transform, "StageHalo");
        TL(Rt(halo), 70f, 10f, 120f, 116f);
        Img(halo, kit.halo, Color.white, false, false);

        GameObject pedestal = Child(equipCard.transform, "StagePedestal");
        TL(Rt(pedestal), 80f, 118f, 100f, 22f);
        Img(pedestal, kit.pedestal, Color.white, false, false);

        // 기존 캐릭터 이미지를 무대 위로 옮긴다.
        Transform charImage = FindDeep(pp, "CharacterImage");
        GameObject character;

        if (charImage != null)
        {
            SetParent(charImage, equipCard.transform);
            character = charImage.gameObject;
        }
        else
        {
            character = Child(equipCard.transform, "CharacterImage");
            Img(character, null, Color.white, false, false);
        }

        TL(Rt(character), 85f, 18f, 90f, 112f);
        Image characterImage = character.GetComponent<Image>();
        characterImage.preserveAspect = true;
        characterImage.raycastTarget = false;
        character.transform.SetSiblingIndex(pedestal.transform.GetSiblingIndex() + 1);

        // 장착 이미지 레이어 (아래 → 위 순서)
        Image bottomLayer = Layer(character.transform, "Layer_Bottom");
        Image topLayer = Layer(character.transform, "Layer_Top");
        Image toolLayer = Layer(character.transform, "Layer_Tool");
        Image hatLayer = Layer(character.transform, "Layer_Hat");

        CharacterPreviewUI preview = GetOrAdd<CharacterPreviewUI>(equipCard);
        SetRef(preview, "hatLayer", hatLayer);
        SetRef(preview, "topLayer", topLayer);
        SetRef(preview, "bottomLayer", bottomLayer);
        SetRef(preview, "toolLayer", toolLayer);

        // 이름표
        GameObject plate = Child(equipCard.transform, "NamePlate");
        TL(Rt(plate), 85f, 142f, 90f, 17f);
        Img(plate, kit.pillWood, Color.white, true, false);

        Transform nameTransform = FindDeep(pp, "PlayerNameText");
        TMP_Text nameText;

        if (nameTransform != null && nameTransform.GetComponent<TMP_Text>() != null)
        {
            SetParent(nameTransform, plate.transform);
            nameText = nameTransform.GetComponent<TMP_Text>();
        }
        else
        {
            nameText = NewText(plate.transform, "PlayerNameText", "플레이어", 11, Cream,
                TextAlignmentOptions.Center, true);
        }

        Stretch(Rt(nameText.gameObject), 4f, 0f, 4f, 0f);
        StyleText(nameText, 11, Cream, TextAlignmentOptions.Center, true);

        // 모두 해제
        GameObject unequip = Child(equipCard.transform, "UnequipAllButton");
        TL(Rt(unequip), 194f, 145f, 58f, 16f);
        Img(unequip, kit.btnSecondary, Color.white, true, true);
        Button unequipButton = Btn(unequip);
        Label(unequip.transform, "모두 해제", 9.5f, Wood);

        // 장비 칸
        const float rightSlotX = EquipW - 10f - SlotSize;
        EquipmentSlotUI hat = EquipSlot(equipCard.transform, "Slot_Hat", EquipmentSlot.Hat, 10f, 10f, kit.ghostHat, kit);
        EquipmentSlotUI top = EquipSlot(equipCard.transform, "Slot_Top", EquipmentSlot.Top, 10f, 82f, kit.ghostTop, kit);
        EquipmentSlotUI bottom = EquipSlot(equipCard.transform, "Slot_Bottom", EquipmentSlot.Bottom, rightSlotX, 10f, kit.ghostBottom, kit);
        EquipmentSlotUI tool = EquipSlot(equipCard.transform, "Slot_Tool", EquipmentSlot.Tool, rightSlotX, 82f, kit.ghostTool, kit);

        // 예전 플레이어 패널 구성은 숨긴다.
        HideOld(pp, "CharacterArea");
        HideOld(pp, "ToolArea");

        // ===== 인벤토리 =====
        const float invX = EquipW + 10f;
        const float invW = 248f;

        GameObject header = Child(pp, "Cozy_InvHeader");
        TL(Rt(header), invX, 0f, invW, 22f);

        TMP_Text invTitle = NewText(header.transform, "Title", "인벤토리", 13, Ink, TextAlignmentOptions.MidlineLeft, true);
        TL(Rt(invTitle.gameObject), 2f, 0f, 58f, 22f);

        GameObject countChip = Child(header.transform, "CountChip");
        TL(Rt(countChip), 60f, 3f, 44f, 16f);
        Img(countChip, kit.pillSoft, Color.white, true, false);
        TMP_Text countText = NewText(countChip.transform, "Count", "0 / 18", 10, C("#6E4E38"), TextAlignmentOptions.Center, true);
        Stretch(Rt(countText.gameObject), 0f, 0f, 0f, 0f);

        ChipGroupUI filters = Chips(header.transform, "FilterChips", new[] { "전체", "장비", "장식" },
            invW - (3 * 38f + 2 * 4f), 1f, 38f, kit);

        GameObject grid = Child(pp, "Cozy_InvGrid");
        TL(Rt(grid), invX, 24f, invW, 140f);
        Img(grid, kit.card, Color.white, true, false);

        InventorySlotUI[] slots = new InventorySlotUI[18];

        for (int i = 0; i < slots.Length; i++)
        {
            int col = i % 6;
            int line = i / 6;
            slots[i] = InventorySlot(grid.transform, "Slot_" + i.ToString("00"),
                8f + col * 39f, 10f + line * 40f, kit);
        }

        // ===== 상세 =====
        const float detailX = invX + invW + 10f;
        float detailW = InnerW - detailX;

        GameObject detail = Child(pp, "Cozy_Detail");
        TL(Rt(detail), detailX, 0f, detailW, InnerH);
        Img(detail, kit.card, Color.white, true, false);

        GameObject iconBox = Child(detail.transform, "IconBox");
        TL(Rt(iconBox), 10f, 10f, 52f, 52f);
        Img(iconBox, kit.slot, Color.white, true, false);

        GameObject detailIconGo = Child(iconBox.transform, "Icon");
        Center(Rt(detailIconGo), 0f, 1f, 38f, 38f);
        Image detailIcon = Img(detailIconGo, null, Color.white, false, false);
        detailIcon.preserveAspect = true;

        float textW = detailW - 80f;
        TMP_Text detailName = NewText(detail.transform, "Name", "아이템 이름", 14, Ink, TextAlignmentOptions.MidlineLeft, true);
        TL(Rt(detailName.gameObject), 70f, 12f, textW, 20f);

        TMP_Text detailTag = NewText(detail.transform, "Tag", "장비 · 모자", 10.5f, C("#6E4E38"), TextAlignmentOptions.MidlineLeft, false);
        TL(Rt(detailTag.gameObject), 70f, 34f, textW, 16f);

        TMP_Text detailDesc = NewText(detail.transform, "Description", "", 11, C("#6E4E38"), TextAlignmentOptions.TopLeft, false);
        TL(Rt(detailDesc.gameObject), 10f, 70f, detailW - 20f, 48f);
        detailDesc.textWrappingMode = TextWrappingModes.Normal;

        float primaryW = (detailW - 26f) * 0.6f;
        GameObject primary = Child(detail.transform, "PrimaryButton");
        TL(Rt(primary), 10f, 124f, primaryW, 32f);
        Img(primary, kit.btnPrimary, Color.white, true, true);
        Button primaryButton = Btn(primary, kit.btnDisabled);
        TMP_Text primaryLabel = Label(primary.transform, "장착하기", 13, Color.white, 1.5f);

        GameObject discard = Child(detail.transform, "DiscardButton");
        TL(Rt(discard), 16f + primaryW, 124f, detailW - 26f - primaryW, 32f);
        Img(discard, kit.btnSecondary, Color.white, true, true);
        Button discardButton = Btn(discard, kit.btnDisabled);
        Label(discard.transform, "버리기", 12, Wood, 1f);

        // ===== 연결 =====
        PlayerTabUI tab = GetOrAdd<PlayerTabUI>(playerPanel);
        SetRefs(tab, "equipmentSlots", new Object[] { hat, top, bottom, tool });
        SetRef(tab, "characterPreview", preview);
        SetRef(tab, "unequipAllButton", unequipButton);
        SetRefs(tab, "inventorySlots", slots);
        SetRef(tab, "filterChips", filters);
        SetRef(tab, "countText", countText);
        SetRef(tab, "detailIcon", detailIcon);
        SetRef(tab, "detailName", detailName);
        SetRef(tab, "detailTag", detailTag);
        SetRef(tab, "detailDescription", detailDesc);
        SetRef(tab, "primaryButton", primaryButton);
        SetRef(tab, "primaryLabel", primaryLabel);
        SetRef(tab, "discardButton", discardButton);
        SetRef(tab, "inventoryUI", inventoryUI);

        if (constructionList != null)
        {
            SerializedObject cl = new SerializedObject(constructionList);
            SetRef(tab, "decorationDatabase", cl.FindProperty("decorationDatabase").objectReferenceValue);
            SetRef(tab, "buildingPlacement", cl.FindProperty("buildingPlacement").objectReferenceValue);
            SetRef(tab, "buildingModeManager", cl.FindProperty("buildingModeManager").objectReferenceValue);
        }
    }

    private static Image Layer(Transform parent, string name)
    {
        GameObject go = Child(parent, name);
        Stretch(Rt(go), 0f, 0f, 0f, 0f);
        Image image = Img(go, null, Color.white, false, false);
        image.preserveAspect = true;
        image.enabled = false;
        return image;
    }

    private static EquipmentSlotUI EquipSlot(Transform parent, string name, EquipmentSlot slot,
        float x, float y, Sprite ghost, Kit kit)
    {
        GameObject go = Child(parent, name);
        TL(Rt(go), x, y, SlotSize, SlotSize);
        Image frame = Img(go, kit.equipOff, Color.white, true, true);
        Button button = Btn(go);

        GameObject iconGo = Child(go.transform, "Icon");
        Center(Rt(iconGo), 0f, 1.5f, 36f, 36f);
        Image icon = Img(iconGo, ghost, new Color(1f, 1f, 1f, 0.4f), false, false);
        icon.preserveAspect = true;

        GameObject ring = Child(go.transform, "SelectedRing");
        Stretch(Rt(ring), -3f, -3f, -3f, -3f);
        Img(ring, kit.ring14, Color.white, true, false);
        ring.SetActive(false);

        GameObject chip = Child(go.transform, "LabelChip");
        RectTransform crt = Rt(chip);
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.anchoredPosition = new Vector2(0f, -1f);
        crt.sizeDelta = new Vector2(38f, 15f);
        Img(chip, kit.pillWood, Color.white, true, false);
        TMP_Text label = NewText(chip.transform, "Label", EquipmentSlotNames.ToKorean(slot), 10, Cream,
            TextAlignmentOptions.Center, true);
        Stretch(Rt(label.gameObject), 0f, 0f, 0f, 0f);

        EquipmentSlotUI ui = GetOrAdd<EquipmentSlotUI>(go);
        SetEnum(ui, "slot", (int)slot);
        SetRef(ui, "button", button);
        SetRef(ui, "frame", frame);
        SetRef(ui, "icon", icon);
        SetRef(ui, "labelText", label);
        SetRef(ui, "selectedRing", ring);
        SetRef(ui, "filledSprite", kit.equipOn);
        SetRef(ui, "emptySprite", kit.equipOff);
        SetRef(ui, "ghostIcon", ghost);
        return ui;
    }

    private static InventorySlotUI InventorySlot(Transform parent, string name, float x, float y, Kit kit)
    {
        GameObject go = Child(parent, name);
        TL(Rt(go), x, y, 36f, 36f);
        Image frame = Img(go, kit.slotEmpty, Color.white, true, true);
        Button button = Btn(go);

        GameObject iconGo = Child(go.transform, "Icon");
        Center(Rt(iconGo), 0f, 1f, 27f, 27f);
        Image icon = Img(iconGo, null, Color.white, false, false);
        icon.preserveAspect = true;
        icon.enabled = false;

        TMP_Text count = NewText(go.transform, "Count", "", 10, Ink, TextAlignmentOptions.BottomRight, true);
        RectTransform crt = Rt(count.gameObject);
        crt.anchorMin = crt.anchorMax = new Vector2(1f, 0f);
        crt.pivot = new Vector2(1f, 0f);
        crt.anchoredPosition = new Vector2(-3f, 2f);
        crt.sizeDelta = new Vector2(30f, 14f);
        count.gameObject.SetActive(false);

        GameObject badge = Child(go.transform, "EquippedBadge");
        RectTransform brt = Rt(badge);
        brt.anchorMin = brt.anchorMax = new Vector2(1f, 1f);
        brt.pivot = new Vector2(0.5f, 0.5f);
        brt.anchoredPosition = new Vector2(-3f, -3f);
        brt.sizeDelta = new Vector2(15f, 15f);
        Img(badge, kit.badgeCheck, Color.white, false, false);
        GameObject check = Child(badge.transform, "Check");
        Stretch(Rt(check), 3f, 3f, 3f, 3f);
        Img(check, kit.gCheck, Color.white, false, false);
        badge.SetActive(false);

        GameObject ring = Child(go.transform, "SelectedRing");
        Stretch(Rt(ring), -3f, -3f, -3f, -3f);
        Img(ring, kit.ring10, Color.white, true, false);
        ring.SetActive(false);

        InventorySlotUI ui = GetOrAdd<InventorySlotUI>(go);
        SetRef(ui, "button", button);
        SetRef(ui, "frame", frame);
        SetRef(ui, "icon", icon);
        SetRef(ui, "countText", count);
        SetRef(ui, "equippedBadge", badge);
        SetRef(ui, "selectedRing", ring);
        SetRef(ui, "filledSprite", kit.slot);
        SetRef(ui, "emptySprite", kit.slotEmpty);
        return ui;
    }

    // 분류 칩 묶음을 만든다.
    private static ChipGroupUI Chips(Transform parent, string name, string[] labels,
        float x, float y, float chipWidth, Kit kit)
    {
        GameObject group = Child(parent, name);
        TL(Rt(group), x, y, labels.Length * (chipWidth + 4f) - 4f, 20f);

        Button[] buttons = new Button[labels.Length];
        TabButtonVisual[] visuals = new TabButtonVisual[labels.Length];

        for (int i = 0; i < labels.Length; i++)
        {
            GameObject chip = Child(group.transform, "Chip_" + i);
            TL(Rt(chip), i * (chipWidth + 4f), 0f, chipWidth, 20f);
            Image bg = Img(chip, kit.pillChip, Color.white, true, true);
            buttons[i] = Btn(chip);
            TMP_Text label = Label(chip.transform, labels[i], 10.5f, Wood);

            TabButtonVisual visual = GetOrAdd<TabButtonVisual>(chip);
            SetRef(visual, "background", bg);
            SetRef(visual, "label", label);
            SetRef(visual, "activeSprite", kit.pillWood);
            SetColor(visual, "activeColor", Color.white);
            SetColor(visual, "activeTextColor", Cream);
            SetRef(visual, "inactiveSprite", kit.pillChip);
            SetColor(visual, "inactiveColor", Color.white);
            SetColor(visual, "inactiveTextColor", Wood);
            visual.SetActive(i == 0);
            visuals[i] = visual;
        }

        ChipGroupUI ui = GetOrAdd<ChipGroupUI>(group);
        SetRefs(ui, "chips", buttons);
        SetRefs(ui, "visuals", visuals);
        return ui;
    }

    // ----- 건물 탭 -----

    private static void BuildBuildingTab(GameObject buildingPanel, ConstructionListUI constructionList, Kit kit)
    {
        Stretch(Rt(buildingPanel), 0f, 0f, 0f, 0f);
        BuildingTabUI tabUI = buildingPanel.GetComponent<BuildingTabUI>();
        const float leftW = 480f;
        const float rightX = leftW + 10f;
        float rightW = InnerW - rightX;

        GameObject constructionPanel = null;
        GameObject managementPanel = null;

        // 건설/관리 전환 버튼
        if (tabUI != null)
        {
            SerializedObject so = new SerializedObject(tabUI);
            constructionPanel = so.FindProperty("constructionPanel").objectReferenceValue as GameObject;
            managementPanel = so.FindProperty("managementPanel").objectReferenceValue as GameObject;
            Button cb = so.FindProperty("constructionButton").objectReferenceValue as Button;
            Button mb = so.FindProperty("managementButton").objectReferenceValue as Button;

            if (cb != null && mb != null)
            {
                GameObject menu = cb.transform.parent.gameObject;
                TL(Rt(menu), 0f, 0f, 150f, 28f);
                Img(menu, kit.pillSoft, Color.white, true, false);

                HorizontalLayoutGroup hlg = GetOrAdd<HorizontalLayoutGroup>(menu);
                hlg.padding = new RectOffset(3, 3, 3, 3);
                hlg.spacing = 2f;
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = true;
                hlg.childForceExpandHeight = true;

                SetRef(tabUI, "constructionVisual", SegmentButton(cb, "건설", kit, true));
                SetRef(tabUI, "managementVisual", SegmentButton(mb, "철거", kit, false)); // 예전 관리 탭 버튼: 이제 철거 모드 버튼
            }
        }

        // ===== 건설 =====
        if (constructionPanel != null)
        {
            Stretch(Rt(constructionPanel), 0f, 0f, 0f, 0f);

            Transform scroll = FindDeep(constructionPanel.transform, "BuildingScrollView");

            if (scroll != null)
            {
                TL(Rt(scroll.gameObject), 0f, 34f, leftW, 130f);
                StyleScroll(scroll, true, kit);
            }

            if (constructionList != null)
            {
                SerializedObject cl = new SerializedObject(constructionList);
                Transform content = cl.FindProperty("content").objectReferenceValue as Transform;

                if (content != null)
                {
                    RectTransform crt = Rt(content.gameObject);
                    crt.anchorMin = new Vector2(0f, 0f);
                    crt.anchorMax = new Vector2(0f, 1f);
                    crt.pivot = new Vector2(0f, 0.5f);
                    crt.anchoredPosition = Vector2.zero;
                    crt.sizeDelta = new Vector2(0f, 0f);

                    RemoveComponent<VerticalLayoutGroup>(content.gameObject);
                    HorizontalLayoutGroup hlg = GetOrAdd<HorizontalLayoutGroup>(content.gameObject);
                    hlg.padding = new RectOffset(2, 2, 2, 0);
                    hlg.spacing = 10f;
                    hlg.childAlignment = TextAnchor.UpperLeft;
                    hlg.childControlWidth = true;
                    hlg.childControlHeight = true;
                    hlg.childForceExpandWidth = false;
                    hlg.childForceExpandHeight = false;

                    ContentSizeFitter csf = GetOrAdd<ContentSizeFitter>(content.gameObject);
                    csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                    csf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
                }
            }

            ChipGroupUI categories = Chips(constructionPanel.transform, "Cozy_CategoryChips",
                new[] { "전체", "생산", "특수", "장식" }, leftW - (4 * 40f + 3 * 4f), 4f, 40f, kit);

            ConstructionDetailUI detail = ConstructionDetail(constructionPanel.transform, rightX, rightW, kit);

            if (constructionList != null)
            {
                SetRef(constructionList, "detailUI", detail);
                SetRef(constructionList, "categoryChips", categories);
            }
        }

        // ===== 관리 =====
        if (managementPanel != null)
        {
            Stretch(Rt(managementPanel), 0f, 0f, 0f, 0f);
            InstalledBuildingListUI listUI = managementPanel.GetComponent<InstalledBuildingListUI>();

            Transform list = FindDeep(managementPanel.transform, "InstalledBuildingList");

            if (list != null)
            {
                TL(Rt(list.gameObject), 0f, 34f, leftW, 130f);
                StyleScroll(list, false, kit);
            }

            if (listUI != null)
            {
                SerializedObject so = new SerializedObject(listUI);
                Transform content = so.FindProperty("content").objectReferenceValue as Transform;

                if (content != null)
                {
                    RectTransform crt = Rt(content.gameObject);
                    crt.anchorMin = new Vector2(0f, 1f);
                    crt.anchorMax = new Vector2(1f, 1f);
                    crt.pivot = new Vector2(0.5f, 1f);
                    crt.anchoredPosition = Vector2.zero;
                    crt.sizeDelta = Vector2.zero;

                    VerticalLayoutGroup vlg = GetOrAdd<VerticalLayoutGroup>(content.gameObject);
                    vlg.padding = new RectOffset(2, 4, 2, 2);
                    vlg.spacing = 6f;
                    vlg.childAlignment = TextAnchor.UpperLeft;
                    vlg.childControlWidth = true;
                    vlg.childControlHeight = true;
                    vlg.childForceExpandWidth = true;
                    vlg.childForceExpandHeight = false;

                    ContentSizeFitter csf = GetOrAdd<ContentSizeFitter>(content.gameObject);
                    csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                    csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                }

                GameObject production = so.FindProperty("totalProductionPanel").objectReferenceValue as GameObject;

                if (production != null)
                    StyleTotalProduction(production, kit);

                BuildingDetailUI detailUI = so.FindProperty("buildingDetailUI").objectReferenceValue as BuildingDetailUI;

                if (detailUI != null)
                    StyleBuildingDetail(detailUI, rightX, rightW, kit);
            }
        }
    }

    private static TabButtonVisual SegmentButton(Button button, string title, Kit kit, bool selected)
    {
        Image bg = Img(button.gameObject, kit.pillWood, Color.white, true, true);
        button.targetGraphic = bg;
        button.transition = Selectable.Transition.ColorTint;

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);

        if (label == null)
            label = Label(button.transform, title, 12, Cream);

        Stretch(Rt(label.gameObject), 0f, 0f, 0f, 0f);
        StyleText(label, 12, Cream, TextAlignmentOptions.Center, true);
        label.text = title;

        TabButtonVisual visual = GetOrAdd<TabButtonVisual>(button.gameObject);
        SetRef(visual, "background", bg);
        SetRef(visual, "label", label);
        SetRef(visual, "activeSprite", kit.pillWood);
        SetColor(visual, "activeColor", Color.white);
        SetColor(visual, "activeTextColor", Cream);
        SetRef(visual, "inactiveSprite", kit.pillWood);
        SetColor(visual, "inactiveColor", new Color(1f, 1f, 1f, 0f));
        SetColor(visual, "inactiveTextColor", Wood);
        visual.SetActive(selected);
        return visual;
    }

    private static ConstructionDetailUI ConstructionDetail(Transform parent, float x, float w, Kit kit)
    {
        GameObject detail = Child(parent, "Cozy_ConstructionDetail");
        TL(Rt(detail), x, 0f, w, InnerH);
        Img(detail, kit.card, Color.white, true, false);

        GameObject art = Child(detail.transform, "Art");
        TL(Rt(art), 10f, 10f, 66f, 66f);
        Image artImage = Img(art, kit.white, C("#F3E3C7"), true, false);

        GameObject iconGo = Child(art.transform, "Icon");
        Center(Rt(iconGo), 0f, 0f, 52f, 52f);
        Image icon = Img(iconGo, null, Color.white, false, false);
        icon.preserveAspect = true;

        float textW = w - 94f;
        TMP_Text nameText = NewText(detail.transform, "Name", "건물을 선택하세요", 14, Ink, TextAlignmentOptions.MidlineLeft, true);
        TL(Rt(nameText.gameObject), 86f, 12f, textW, 20f);

        TMP_Text tagText = NewText(detail.transform, "Tag", "", 10.5f, C("#6E4E38"), TextAlignmentOptions.MidlineLeft, false);
        TL(Rt(tagText.gameObject), 86f, 33f, textW, 16f);

        TMP_Text production = NewText(detail.transform, "Production", "", 10.5f, Green, TextAlignmentOptions.TopLeft, false);
        TL(Rt(production.gameObject), 86f, 51f, textW, 30f);
        production.textWrappingMode = TextWrappingModes.Normal;

        TMP_Text cost = NewText(detail.transform, "Cost", "", 11f, Ink, TextAlignmentOptions.MidlineLeft, false);
        TL(Rt(cost.gameObject), 12f, 94f, w - 24f, 22f);
        cost.richText = true;

        GameObject build = Child(detail.transform, "BuildButton");
        TL(Rt(build), 10f, 124f, w - 20f, 32f);
        Img(build, kit.btnPrimary, Color.white, true, true);
        Button buildButton = Btn(build, kit.btnDisabled);
        TMP_Text buildLabel = Label(build.transform, "건설하기", 13, Color.white, 1.5f);

        ConstructionDetailUI ui = GetOrAdd<ConstructionDetailUI>(detail);
        SetRef(ui, "artBackground", artImage);
        SetRef(ui, "icon", icon);
        SetRef(ui, "nameText", nameText);
        SetRef(ui, "tagText", tagText);
        SetRef(ui, "productionText", production);
        SetRef(ui, "costText", cost);
        SetRef(ui, "buildButton", buildButton);
        SetRef(ui, "buildLabel", buildLabel);

        PlayerResources resources = Object.FindFirstObjectByType<PlayerResources>(FindObjectsInactive.Include);
        SetRef(ui, "playerResources", resources);
        return ui;
    }

    private static void StyleScroll(Transform scroll, bool horizontal, Kit kit)
    {
        Image bg = scroll.GetComponent<Image>();

        if (bg != null)
        {
            bg.color = new Color(1f, 1f, 1f, 0f);
            bg.raycastTarget = true;
        }

        ScrollRect sr = scroll.GetComponent<ScrollRect>();

        if (sr != null)
        {
            sr.horizontal = horizontal;
            sr.vertical = !horizontal;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 20f;
        }

        Transform viewport = scroll.Find("Viewport");

        if (viewport != null)
        {
            if (horizontal)
                Stretch(Rt(viewport.gameObject), 0f, 0f, 0f, 10f);
            else
                Stretch(Rt(viewport.gameObject), 0f, 0f, 10f, 0f);

            Mask mask = viewport.GetComponent<Mask>();

            if (mask != null)
                mask.showMaskGraphic = false;
        }

        Transform bar = FindDeep(scroll, horizontal ? "Scrollbar Horizontal" : "Scrollbar Vertical");

        if (bar == null)
            return;

        RectTransform brt = Rt(bar.gameObject);

        if (horizontal)
        {
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(1f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.anchoredPosition = Vector2.zero;
            brt.sizeDelta = new Vector2(0f, 6f);
        }
        else
        {
            brt.anchorMin = new Vector2(1f, 0f);
            brt.anchorMax = new Vector2(1f, 1f);
            brt.pivot = new Vector2(1f, 0.5f);
            brt.anchoredPosition = Vector2.zero;
            brt.sizeDelta = new Vector2(6f, 0f);
        }

        Img(bar.gameObject, kit.white, C("#EFDCBB"), true, true);

        Transform handle = FindDeep(bar, "Handle");

        if (handle != null)
            Img(handle.gameObject, kit.white, Tan, true, true);
    }

    private static void StyleTotalProduction(GameObject panel, Kit kit)
    {
        TL(Rt(panel), 158f, 0f, 322f, 28f);
        Img(panel, kit.pillSoft, Color.white, true, false);

        Place(panel.transform, "ProductionTitle", 8f, 0f, 54f, 28f, 10.5f, Wood, true);
        PlaceIcon(panel.transform, "WoodProductionIcon", 64f);
        Place(panel.transform, "WoodProductionText", 83f, 0f, 66f, 28f, 10.5f, Ink, false);
        PlaceIcon(panel.transform, "StoneProductionIcon", 150f);
        Place(panel.transform, "StoneProductionText", 169f, 0f, 66f, 28f, 10.5f, Ink, false);
        PlaceIcon(panel.transform, "WoodProductionIcon (2)", 236f);
        Place(panel.transform, "GoldProductionText", 255f, 0f, 62f, 28f, 10.5f, Ink, false);
    }

    private static void Place(Transform parent, string name, float x, float y, float w, float h,
        float size, Color color, bool bold)
    {
        Transform t = FindDeep(parent, name);

        if (t == null)
            return;

        TL(Rt(t.gameObject), x, y, w, h);
        TMP_Text text = t.GetComponent<TMP_Text>();

        if (text != null)
            StyleText(text, size, color, TextAlignmentOptions.MidlineLeft, bold);
    }

    private static void PlaceIcon(Transform parent, string name, float x)
    {
        Transform t = FindDeep(parent, name);

        if (t == null)
            return;

        TL(Rt(t.gameObject), x, 5f, 18f, 18f);
        Image image = t.GetComponent<Image>();

        if (image != null)
            image.preserveAspect = true;
    }

    private static void StyleBuildingDetail(BuildingDetailUI detailUI, float x, float w, Kit kit)
    {
        GameObject go = detailUI.gameObject;
        TL(Rt(go), x, 0f, w, InnerH);
        Img(go, kit.card, Color.white, true, false);

        SerializedObject so = new SerializedObject(detailUI);
        StyleRef(so, "buildingDetailName", 14f, 12f, w - 28f, 20f, 14, Ink, true, TextAlignmentOptions.MidlineLeft);
        StyleRef(so, "buildingDetailProduction", 14f, 38f, w - 28f, 36f, 11, Green, false, TextAlignmentOptions.TopLeft);
        StyleRef(so, "buildingDetailPosition", 14f, 78f, w - 28f, 16f, 11, C("#6E4E38"), false, TextAlignmentOptions.MidlineLeft);

        float half = (w - 28f - 8f) / 2f;

        if (so.FindProperty("viewBuildingButton").objectReferenceValue is Button view)
        {
            TL(Rt(view.gameObject), 14f, 124f, half, 32f);
            Img(view.gameObject, kit.btnSecondary, Color.white, true, true);
            Btn(view.gameObject, kit.btnDisabled);
            Label(view.transform, "위치 보기", 12, Wood, 1f);
        }

        if (so.FindProperty("demolishBuildingButton").objectReferenceValue is Button demolish)
        {
            TL(Rt(demolish.gameObject), 22f + half, 124f, half, 32f);
            Img(demolish.gameObject, kit.btnDanger, Color.white, true, true);
            Btn(demolish.gameObject, kit.btnDisabled);
            Label(demolish.transform, "철거하기", 12, C("#A2402B"), 1f);
        }
    }

    private static void StyleRef(SerializedObject so, string field, float x, float y, float w, float h,
        float size, Color color, bool bold, TextAlignmentOptions align)
    {
        if (!(so.FindProperty(field).objectReferenceValue is TMP_Text text))
            return;

        TL(Rt(text.gameObject), x, y, w, h);
        StyleText(text, size, color, align, bold);

        if (align == TextAlignmentOptions.TopLeft)
            text.textWrappingMode = TextWrappingModes.Normal;
    }

    // ----- 필드 탭 -----

    private static void BuildFieldTab(GameObject fieldPanel, Kit kit)
    {
        Transform fp = fieldPanel.transform;
        Stretch(Rt(fieldPanel), 0f, 0f, 0f, 0f);

        FieldSelectionUI selection = fieldPanel.GetComponent<FieldSelectionUI>();
        GatheringUI gathering = fieldPanel.GetComponent<GatheringUI>();
        SerializedObject sel = selection != null ? new SerializedObject(selection) : null;

        const float mapW = 440f;

        // ===== 지도 =====
        Transform map = FindDeep(fp, "FieldListPanel");

        if (map == null)
            map = Child(fp, "FieldListPanel").transform;

        RemoveLayout(map.gameObject);
        TL(Rt(map.gameObject), 0f, 0f, mapW, InnerH);
        Img(map.gameObject, kit.map, Color.white, true, true);

        Deco(map, "Deco_Pond", kit.pond, 150f, 112f, 100f, 44f);
        Deco(map, "Deco_Meadow", kit.meadow, 8f, 70f, 150f, 78f);
        Deco(map, "Deco_Hill", kit.hillBlob, 268f, 4f, 162f, 66f);

        GameObject village = MapPin(map, "Pin_Village", "마을", 50f, 66f, kit.pinVillage, kit.mVillage, false, kit);
        GameObject here = Child(village.transform, "HereBadge");
        RectTransform hrt = Rt(here);
        hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 1f);
        hrt.pivot = new Vector2(0.5f, 0.5f);
        hrt.anchoredPosition = new Vector2(0f, -2f);
        hrt.sizeDelta = new Vector2(32f, 15f);
        Img(here, kit.pillCoral, Color.white, true, false);
        Label(here.transform, "여기", 9.5f, Color.white);

        MapPin(map, "Pin_Hill", "바위 언덕", 300f, 10f, kit.pinLocked, kit.mHill, true, kit);
        MapPin(map, "Pin_Beach", "바닷가", 366f, 78f, kit.pinLocked, kit.mBeach, true, kit);

        // 숲 버튼 → 핀
        if (sel != null && sel.FindProperty("forestButton").objectReferenceValue is Button forest)
        {
            GameObject fgo = forest.gameObject;
            SetParent(fgo.transform, map);
            RemoveLayout(fgo);
            TL(Rt(fgo), 176f, 22f, 56f, 60f);
            Image bg = Img(fgo, kit.pinForest, Color.white, false, true);
            forest.targetGraphic = bg;
            forest.transition = Selectable.Transition.ColorTint;

            GameObject icon = Child(fgo.transform, "Icon");
            Center(Rt(icon), 0f, 2f, 36f, 36f);
            Img(icon, kit.mForest, Color.white, false, false);

            GameObject chip = PinLabelChip(fgo.transform, 46f, kit);
            TMP_Text label = fgo.GetComponentInChildren<TMP_Text>(true);

            if (label == null)
                label = NewText(chip.transform, "Text (TMP)", "숲", 11, Ink, TextAlignmentOptions.Center, true);

            SetParent(label.transform, chip.transform);
            Stretch(Rt(label.gameObject), 0f, 0f, 0f, 0f);
            StyleText(label, 11, Ink, TextAlignmentOptions.Center, true);
            label.text = "숲";
        }

        FieldMapUI mapUI = GetOrAdd<FieldMapUI>(map.gameObject);
        SetRef(mapUI, "villageHereBadge", here);

        // ===== 정보 패널 =====
        Transform info = FindDeep(fp, "FieldInfoPanel");
        const float infoX = mapW + 10f;
        float infoW = InnerW - infoX;

        if (info != null)
        {
            RemoveLayout(info.gameObject);
            TL(Rt(info.gameObject), infoX, 0f, infoW, InnerH);
            Img(info.gameObject, kit.card, Color.white, true, false);
        }

        if (sel != null)
        {
            StyleRef(sel, "fieldNameText", 14f, 12f, infoW - 28f, 22f, 16, Ink, true, TextAlignmentOptions.MidlineLeft);
            StyleRef(sel, "fieldDescriptionText", 14f, 40f, infoW - 28f, 70f, 12, C("#6E4E38"), false, TextAlignmentOptions.TopLeft);

            if (sel.FindProperty("moveButton").objectReferenceValue is Button move)
            {
                TL(Rt(move.gameObject), 14f, 122f, infoW - 28f, 32f);
                Img(move.gameObject, kit.btnPrimary, Color.white, true, true);
                Btn(move.gameObject, kit.btnDisabled);
                Label(move.transform, "이동하기", 14, Color.white, 1.5f);
            }
        }

        // ===== 채집 =====
        if (gathering != null)
        {
            SerializedObject g = new SerializedObject(gathering);

            if (g.FindProperty("gatheringButton").objectReferenceValue is GameObject gatherButton)
            {
                TL(Rt(gatherButton), 10f, 10f, 104f, 28f);
                Img(gatherButton, kit.btnSecondary, Color.white, true, true);
                Btn(gatherButton);
                Label(gatherButton.transform, "채집하기", 12, Wood, 1f);
                gatherButton.transform.SetAsLastSibling();
            }

            if (g.FindProperty("gatheringPanel").objectReferenceValue is GameObject panel)
                StyleGatheringPanel(panel, g, kit);
        }
    }

    private static void StyleGatheringPanel(GameObject panel, SerializedObject gathering, Kit kit)
    {
        Stretch(Rt(panel), 0f, 0f, 0f, 0f);
        Img(panel, kit.panelCream, Color.white, true, true);
        panel.transform.SetAsLastSibling();

        Transform title = FindDeep(panel.transform, "GatheringTitleText");

        if (title != null && title.GetComponent<TMP_Text>() is TMP_Text titleText)
        {
            TL(Rt(title.gameObject), 14f, 8f, InnerW - 130f, 22f);
            StyleText(titleText, 15, Ink, TextAlignmentOptions.MidlineLeft, true);
        }

        if (gathering.FindProperty("backButton").objectReferenceValue is Button back)
        {
            TL(Rt(back.gameObject), InnerW - 14f - 90f, 6f, 90f, 26f);
            Img(back.gameObject, kit.btnSecondary, Color.white, true, true);
            Btn(back.gameObject);
            Label(back.transform, "돌아가기", 11.5f, Wood, 1f);
        }

        Transform times = FindDeep(panel.transform, "TimeSelectionPanel");

        if (times == null)
            return;

        TL(Rt(times.gameObject), 14f, 38f, InnerW - 28f, 118f);
        Image timesImage = times.GetComponent<Image>();

        if (timesImage != null)
        {
            timesImage.color = new Color(1f, 1f, 1f, 0f);
            timesImage.raycastTarget = false;
        }

        HorizontalLayoutGroup hlg = GetOrAdd<HorizontalLayoutGroup>(times.gameObject);
        hlg.spacing = 10f;
        hlg.padding = new RectOffset(0, 0, 0, 0);
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        foreach (Transform place in times)
        {
            Img(place.gameObject, kit.card, Color.white, true, false);

            foreach (Transform child in place)
            {
                Button button = child.GetComponent<Button>();
                RectTransform crt = Rt(child.gameObject);

                if (button != null)
                {
                    crt.anchorMin = new Vector2(0f, 0f);
                    crt.anchorMax = new Vector2(1f, 0f);
                    crt.pivot = new Vector2(0.5f, 0f);
                    crt.anchoredPosition = new Vector2(0f, 8f);
                    crt.sizeDelta = new Vector2(-16f, 28f);
                    Img(child.gameObject, kit.btnPrimary, Color.white, true, true);
                    Btn(child.gameObject, kit.btnDisabled);

                    foreach (TMP_Text t in child.GetComponentsInChildren<TMP_Text>(true))
                        StyleText(t, 12, Color.white, TextAlignmentOptions.Center, true);
                }
                else if (child.GetComponent<Image>() is Image image)
                {
                    Stretch(crt, 8f, 8f, 8f, 44f);
                    image.preserveAspect = true;
                    image.raycastTarget = false;
                }
            }
        }
    }

    private static void Deco(Transform parent, string name, Sprite sprite, float x, float y, float w, float h)
    {
        GameObject go = Child(parent, name);
        TL(Rt(go), x, y, w, h);
        Img(go, sprite, Color.white, false, false);
        go.transform.SetSiblingIndex(0);
    }

    private static GameObject MapPin(Transform map, string name, string title, float x, float y,
        Sprite circle, Sprite icon, bool locked, Kit kit)
    {
        GameObject pin = Child(map, name);
        TL(Rt(pin), x, y, 56f, 60f);
        Img(pin, circle, Color.white, false, false);

        GameObject iconGo = Child(pin.transform, "Icon");
        Center(Rt(iconGo), 0f, 2f, 36f, 36f);
        Img(iconGo, icon, locked ? new Color(1f, 1f, 1f, 0.55f) : Color.white, false, false);

        if (locked)
        {
            GameObject badge = Child(pin.transform, "LockBadge");
            RectTransform brt = Rt(badge);
            brt.anchorMin = brt.anchorMax = new Vector2(1f, 0f);
            brt.pivot = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = new Vector2(-6f, 12f);
            brt.sizeDelta = new Vector2(20f, 20f);
            Img(badge, kit.badgeLock, Color.white, false, false);
            GameObject glyph = Child(badge.transform, "Glyph");
            Stretch(Rt(glyph), 4f, 4f, 4f, 4f);
            Img(glyph, kit.gLock, Color.white, false, false);
        }

        GameObject chip = PinLabelChip(pin.transform, title.Length > 3 ? 64f : 46f, kit);
        Label(chip.transform, title, 11, locked ? C("#6E4E38") : Ink);
        return pin;
    }

    private static GameObject PinLabelChip(Transform pin, float width, Kit kit)
    {
        GameObject chip = Child(pin, "LabelChip");
        RectTransform rt = Rt(chip);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, 4f);
        rt.sizeDelta = new Vector2(width, 18f);
        Img(chip, kit.pillCream, Color.white, true, false);
        return chip;
    }

    // ===============================================================
    // 프리팹 (건물 카드, 설치 건물 카드)
    // ===============================================================

    private static void StylePrefabs(InventoryUI inventoryUI, Kit kit)
    {
        ConstructionListUI constructionList = Object.FindFirstObjectByType<ConstructionListUI>(FindObjectsInactive.Include);
        InstalledBuildingListUI installedList = Object.FindFirstObjectByType<InstalledBuildingListUI>(FindObjectsInactive.Include);

        if (constructionList != null &&
            new SerializedObject(constructionList).FindProperty("buildingCardPrefab").objectReferenceValue is BuildingCardUI card)
        {
            EditPrefab(AssetDatabase.GetAssetPath(card), root => StyleBuildingCard(root, kit));
        }

        if (installedList != null &&
            new SerializedObject(installedList).FindProperty("buildingCardPrefab").objectReferenceValue is InstalledBuildingCardUI row)
        {
            EditPrefab(AssetDatabase.GetAssetPath(row), root => StyleInstalledCard(root, kit));
        }
    }

    private static void EditPrefab(string path, System.Action<GameObject> edit)
    {
        if (string.IsNullOrEmpty(path))
            return;

        GameObject root = PrefabUtility.LoadPrefabContents(path);

        try
        {
            edit(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void StyleBuildingCard(GameObject root, Kit kit)
    {
        BuildingCardUI ui = root.GetComponent<BuildingCardUI>();
        SerializedObject so = new SerializedObject(ui);

        Rt(root).sizeDelta = new Vector2(110f, 116f);
        LayoutElement le = GetOrAdd<LayoutElement>(root);
        le.preferredWidth = 110f;
        le.preferredHeight = 116f;
        le.minWidth = 110f;
        le.minHeight = 116f;

        Image bg = Img(root, kit.slot, Color.white, true, true);
        Button button = root.GetComponent<Button>();

        if (button != null)
        {
            button.targetGraphic = bg;
            button.transition = Selectable.Transition.ColorTint;
            SetDisabledColor(button);
        }

        GameObject art = Child(root.transform, "Art");
        TL(Rt(art), 4f, 4f, 102f, 62f);
        Image artImage = Img(art, kit.white, C("#F3E3C7"), true, false);
        art.transform.SetSiblingIndex(0);

        if (so.FindProperty("buildingImage").objectReferenceValue is Image image)
        {
            SetParent(image.transform, art.transform);
            Center(Rt(image.gameObject), 0f, -2f, 48f, 48f);
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        GameObject chip = Child(art.transform, "TypeChip");
        TL(Rt(chip), 4f, 4f, 30f, 14f);
        Img(chip, kit.pillPlain, Color.white, true, false);
        TMP_Text type = Label(chip.transform, "", 9, C("#6E4E38"));

        StyleRef(so, "buildingNameText", 8f, 70f, 94f, 18f, 12.5f, Ink, true, TextAlignmentOptions.MidlineLeft);
        StyleRef(so, "buildingCostText", 8f, 88f, 94f, 26f, 10, C("#6E4E38"), false, TextAlignmentOptions.TopLeft);

        GameObject ring = Child(root.transform, "SelectedRing");
        Stretch(Rt(ring), -3f, -3f, -3f, -3f);
        Img(ring, kit.ring10, Color.white, true, false);
        ring.SetActive(false);

        SetRef(ui, "selectedRing", ring);
        SetRef(ui, "typeText", type);
        SetRef(ui, "artBackground", artImage);
    }

    private static void StyleInstalledCard(GameObject root, Kit kit)
    {
        InstalledBuildingCardUI ui = root.GetComponent<InstalledBuildingCardUI>();
        SerializedObject so = new SerializedObject(ui);

        RectTransform rt = Rt(root);
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, 40f);
        LayoutElement le = GetOrAdd<LayoutElement>(root);
        le.preferredHeight = 40f;
        le.minHeight = 40f;

        Image bg = Img(root, kit.slot, Color.white, true, true);
        Button button = root.GetComponent<Button>();

        if (button != null)
        {
            button.targetGraphic = bg;
            button.transition = Selectable.Transition.ColorTint;
        }

        if (so.FindProperty("buildingNameText").objectReferenceValue is TMP_Text nameText)
        {
            Anchors(Rt(nameText.gameObject), new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(12f, 0f), Vector2.zero);
            StyleText(nameText, 13, Ink, TextAlignmentOptions.MidlineLeft, true);
        }

        if (so.FindProperty("buildingProductionText").objectReferenceValue is TMP_Text production)
        {
            Anchors(Rt(production.gameObject), new Vector2(0.5f, 0f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-12f, 0f));
            StyleText(production, 11, Green, TextAlignmentOptions.MidlineRight, false);
        }
    }

    // ===============================================================
    // 공통 도구
    // ===============================================================

    private static RectTransform Rt(GameObject go)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        return rt != null ? rt : go.AddComponent<RectTransform>();
    }

    // 부모의 왼쪽 위 기준으로 배치한다 (y는 아래로 증가).
    private static void TL(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    // 부모 가운데 기준으로 배치한다 (y는 위로 증가).
    private static void Center(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    // 부모를 가득 채우고 안쪽 여백을 준다.
    private static void Stretch(RectTransform rt, float left, float top, float right, float bottom)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    private static void Anchors(RectTransform rt, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    // 이름으로 바로 아래 자식을 찾고, 없으면 만든다.
    private static GameObject Child(Transform parent, string name)
    {
        Transform found = parent.Find(name);

        if (found != null)
        {
            found.gameObject.SetActive(true);
            return found.gameObject;
        }

        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);

        if (recordUndo)
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);

        return go;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name)
            return root;

        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, name);

            if (found != null)
                return found;
        }

        return null;
    }

    private static void SetParent(Transform t, Transform parent)
    {
        if (t.parent == parent)
            return;

        if (recordUndo)
            Undo.SetTransformParent(t, parent, "Move " + t.name);
        else
            t.SetParent(parent, false);
    }

    private static void HideOld(Transform root, string name)
    {
        Transform t = FindDeep(root, name);

        if (t != null)
            t.gameObject.SetActive(false);
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();

        if (c != null)
            return c;

        return recordUndo ? Undo.AddComponent<T>(go) : go.AddComponent<T>();
    }

    private static void RemoveComponent<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();

        if (c == null)
            return;

        if (recordUndo)
            Undo.DestroyObjectImmediate(c);
        else
            Object.DestroyImmediate(c);
    }

    // 직접 배치하는 오브젝트에서 자동 배치 컴포넌트를 없앤다.
    private static void RemoveLayout(GameObject go)
    {
        RemoveComponent<HorizontalLayoutGroup>(go);
        RemoveComponent<VerticalLayoutGroup>(go);
        RemoveComponent<GridLayoutGroup>(go);
        RemoveComponent<ContentSizeFitter>(go);
    }

    private static Image Img(GameObject go, Sprite sprite, Color color, bool sliced, bool raycast)
    {
        Image image = GetOrAdd<Image>(go);
        image.sprite = sprite;
        image.color = color;
        image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        image.fillCenter = true;
        image.pixelsPerUnitMultiplier = 1f;
        image.raycastTarget = raycast;
        image.enabled = true;
        return image;
    }

    // 버튼 모양: 눌렀을 때 살짝 어두워지고, 비활성화되면 회색 스프라이트로 바뀐다.
    private static Button Btn(GameObject go, Sprite disabledSprite = null)
    {
        Button button = GetOrAdd<Button>(go);
        Image image = go.GetComponent<Image>();
        button.targetGraphic = image;

        if (disabledSprite != null)
        {
            button.transition = Selectable.Transition.SpriteSwap;
            SpriteState state = new SpriteState { disabledSprite = disabledSprite };
            button.spriteState = state;
        }
        else
        {
            button.transition = Selectable.Transition.ColorTint;
            SetDisabledColor(button);
        }

        return button;
    }

    private static void SetDisabledColor(Button button)
    {
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.highlightedColor = new Color(1f, 0.97f, 0.92f);
        colors.pressedColor = new Color(0.9f, 0.86f, 0.8f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.85f, 0.82f, 0.78f, 0.7f);
        button.colors = colors;
    }

    // 버튼 안 글자. 기존 TMP 글자가 있으면 그것을 쓴다. lift는 아래 그림자만큼 글자를 올리는 값.
    private static TMP_Text Label(Transform parent, string text, float size, Color color, float lift = 0f)
    {
        TMP_Text label = parent.GetComponentInChildren<TMP_Text>(true);

        if (label == null)
            label = NewText(parent, "Text (TMP)", text, size, color, TextAlignmentOptions.Center, true);

        Stretch(Rt(label.gameObject), 2f, 0f, 2f, lift * 2f);
        StyleText(label, size, color, TextAlignmentOptions.Center, true);
        label.text = text;
        return label;
    }

    private static TMP_Text NewText(Transform parent, string name, string text, float size, Color color,
        TextAlignmentOptions align, bool bold)
    {
        GameObject go = Child(parent, name);
        TextMeshProUGUI tmp = GetOrAdd<TextMeshProUGUI>(go);

        if (font != null)
            tmp.font = font;

        tmp.text = text;
        StyleText(tmp, size, color, align, bold);
        return tmp;
    }

    private static void StyleText(TMP_Text text, float size, Color color, TextAlignmentOptions align, bool bold)
    {
        if (font != null && text.font != font)
        {
            text.font = font;
            text.fontSharedMaterial = font.material;
        }

        text.fontSize = size;
        text.enableAutoSizing = false;
        text.color = color;
        text.alignment = align;
        text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        text.margin = Vector4.zero;
    }

    // ----- SerializedObject 도우미 -----

    private static void SetRef(Object target, string field, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(field);

        if (prop == null)
        {
            Debug.LogWarning($"[Cozy UI] {target.GetType().Name}.{field} 필드를 찾지 못했어요.");
            return;
        }

        prop.objectReferenceValue = value;
        Apply(so);
    }

    private static void SetRefs(Object target, string field, Object[] values)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(field);

        if (prop == null)
            return;

        prop.arraySize = values.Length;

        for (int i = 0; i < values.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

        Apply(so);
    }

    private static void SetColor(Object target, string field, Color value)
    {
        SerializedObject so = new SerializedObject(target);
        so.FindProperty(field).colorValue = value;
        Apply(so);
    }

    private static void SetVector(Object target, string field, Vector2 value)
    {
        SerializedObject so = new SerializedObject(target);
        so.FindProperty(field).vector2Value = value;
        Apply(so);
    }

    private static void SetBool(Object target, string field, bool value)
    {
        SerializedObject so = new SerializedObject(target);
        so.FindProperty(field).boolValue = value;
        Apply(so);
    }

    private static void SetEnum(Object target, string field, int value)
    {
        SerializedObject so = new SerializedObject(target);
        so.FindProperty(field).enumValueIndex = value;
        Apply(so);
    }

    private static void Apply(SerializedObject so)
    {
        if (recordUndo)
            so.ApplyModifiedProperties();
        else
            so.ApplyModifiedPropertiesWithoutUndo();
    }
}
