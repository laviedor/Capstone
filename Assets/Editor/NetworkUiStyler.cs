using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 네트워크 UI(로그인 창, 오른쪽 위 도구 줄, 계정 메뉴, 채팅창, 친구 창, 확인 창, 안내)를
// 팀의 Tab 메뉴와 같은 모양으로 만든다: 메뉴 Tools > Apply Cozy Style To Network UI
//   팀이 만든 스프라이트(Assets/UI/Cozy/Generated)와 같은 글꼴(D2Coding)을 쓰고, 배치도 여기서 정한다
//     오른쪽 위 한 줄: [친구] [1:1 채팅] [광장 채팅] [톱니바퀴] [현재 위치 | 내 마을 | 광장]
//     톱니바퀴를 누르면 계정 메뉴(유저명, [로그아웃], [회원 탈퇴])가 열린다
//     채팅창과 친구 창은 도구 줄 바로 아래에 열린다
//   SampleScene 을 열어 둔 상태에서 실행한다. 여러 번 실행해도 결과가 같다
//   (팀의 스프라이트가 바뀌었거나 아래의 치수, 색을 고쳤으면 다시 실행한다)
//   네트워크 UI 의 캔버스는 기준 해상도가 1920x1080 이라, 가로로 긴 게임 창에서는 1단위가 Tab 메뉴(1920x270)의 절반 크기다.
//   그래서 Tab 메뉴의 치수를 2배(U)로 쓰고, 9분할 스프라이트의 테두리도 2배로 그린다(BorderScale)
public static class NetworkUiStyler
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string CozyFolder = "Assets/UI/Cozy/Generated";
    private const string OwnFolder = "Assets/UI/Network";
    private const string RegularFontPath = "Assets/Fonts/nanum-gothic-coding/D2Coding/D2Coding-Ver1.3.2-20180524.ttf";
    private const string BoldFontPath = "Assets/Fonts/nanum-gothic-coding/D2Coding/D2CodingBold-Ver1.3.2-20180524.ttf";

    private const float U = 2f;             // Tab 메뉴 1단위 = 네트워크 UI 2단위
    private const float BorderScale = 0.5f; // Image.pixelsPerUnitMultiplier: 테두리를 Tab 메뉴와 같은 굵기로

    // 도구 줄 (네트워크 UI 단위)
    private const float Margin = 16f;        // 화면 오른쪽 끝과의 간격
    private const float TopMargin = 12f;     // 화면 위쪽 끝과의 간격
    private const float BarHeight = 26f * U; // 자원 표시 칸과 같은 높이
    private const float Gap = 10f;           // 버튼 사이
    private const float WorldBarWidth = 490f;
    private const float PanelTop = TopMargin + BarHeight + 8f; // 도구 줄 아래에 여는 창의 위쪽 위치
    private const float PanelWidth = 520f;
    private const float PanelHeight = 452f;

    private static readonly Color Ink = CozySpriteFactory.Ink;
    private static readonly Color Wood = CozySpriteFactory.Wood;
    private static readonly Color Cream = CozySpriteFactory.Cream;
    private static readonly Color Green = CozySpriteFactory.Green;
    private static readonly Color Muted = CozySpriteFactory.C("#6E4E38");
    private static readonly Color Danger = CozySpriteFactory.C("#A2402B");
    private static readonly Color Offline = CozySpriteFactory.C("#8A7462");
    private static readonly Color Request = CozySpriteFactory.C("#C25E1E");
    private static readonly Color Dim = new Color(0.17f, 0.10f, 0.06f, 0.55f); // 창 뒤를 어둡게 덮는 색

    private class Kit
    {
        public Sprite window, card, pillCream, pillChip, pillWood, btnPrimary, btnSecondary, btnDanger, btnDisabled;
        public Sprite dotPlayer, glyphPerson, glyphGear;
        public Font regular, bold;
    }

    private static Kit kit;

    [MenuItem("Tools/Apply Cozy Style To Network UI")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Play 모드에서는 실행할 수 없습니다.");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            Debug.LogError("SampleScene 을 연 상태에서 실행해 주세요.");
            return;
        }
        if (scene.isDirty)
        {
            Debug.LogError("저장하지 않은 변경이 있습니다. 먼저 저장하거나 되돌린 뒤 다시 실행해 주세요.");
            return;
        }

        try
        {
            kit = LoadKit();
            Transform hud = Root(scene, "HUDCanvas");
            BuildTopBar(hud);
            BuildAccountMenu(hud);
            StyleChatPanel(hud);
            StyleFriendPanel(hud);
            StyleNotice(hud);
            StyleDialog(Root(scene, "DialogCanvas"));
            StyleLogin(Root(scene, "LoginCanvas"));
        }
        catch (Exception e)
        {
            // 중간에 멈추면 반쯤 바뀐 씬이 남지 않게 저장된 씬으로 되돌린다
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.LogError("네트워크 UI 를 바꾸지 못했습니다 (SampleScene 은 그대로입니다):\n" + e);
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("네트워크 UI 를 Tab 메뉴와 같은 모양으로 맞췄습니다.");
    }

    // ---------- 오른쪽 위 도구 줄 ----------

    private static void BuildTopBar(Transform hud)
    {
        RectTransform bar = Child(hud, "TopBar");
        bar.SetAsFirstSibling();
        bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(1f, 1f);
        bar.anchoredPosition = new Vector2(-Margin, -TopMargin);

        // 버튼의 크기는 여기서 정한 그대로 두고, 왼쪽에서 오른쪽으로 나란히 놓는다 (꺼져 있는 버튼의 자리는 비우지 않는다)
        HorizontalLayoutGroup layout = GetOrAdd<HorizontalLayoutGroup>(bar.gameObject);
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = Gap;
        layout.childAlignment = TextAnchor.MiddleRight;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childScaleWidth = layout.childScaleHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = GetOrAdd<ContentSizeFitter>(bar.gameObject);
        fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        FriendUI friendUI = hud.GetComponent<FriendUI>();
        ChatUI chatUI = hud.GetComponent<ChatUI>();
        WorldUI worldUI = hud.GetComponent<WorldUI>();

        // 글자 수에 맞춘 너비: 안 읽은 수 "(99+)" 까지 들어가야 한다
        Button friend = ToolButton(Ref<Button>(friendUI, "friendButton"), bar, 132f);
        Button dm = ToolButton(Ref<Button>(friendUI, "dmButton"), bar, 176f);
        Button chat = ToolButton(Ref<Button>(chatUI, "chatButton"), bar, 214f);

        // 톱니바퀴: 계정 메뉴를 연다 (AccountMenuUI)
        RectTransform gear = Child(bar, "GearButton");
        gear.sizeDelta = new Vector2(BarHeight, BarHeight);
        Img(gear, kit.btnSecondary, Color.white, true, true);
        PlainButton(gear.gameObject);
        RectTransform glyph = Child(gear, "Glyph");
        Center(glyph, 0f, 2f, 16f * U, 16f * U);
        Img(glyph, kit.glyphGear, Wood, false, false);

        // 현재 위치와 이동 버튼
        RectTransform world = (RectTransform)Ref<GameObject>(worldUI, "bar").transform;
        world.SetParent(bar, false);
        world.sizeDelta = new Vector2(WorldBarWidth, BarHeight);
        Img(world, kit.pillCream, Color.white, true, true);

        Text location = Ref<Text>(worldUI, "locationText");
        RectTransform locationRect = location.rectTransform;
        locationRect.anchorMin = new Vector2(0f, 0f);
        locationRect.anchorMax = new Vector2(0f, 1f);
        locationRect.pivot = new Vector2(0f, 0.5f);
        locationRect.anchoredPosition = new Vector2(18f, 0f);
        locationRect.sizeDelta = new Vector2(236f, 0f);
        OneLine(locationRect, 24);
        Txt(location, 24, Ink, true, TextAnchor.MiddleLeft);
        Shrink(location, 13); // 친구 마을 이름처럼 긴 글자는 줄여서 보여 준다

        Destination(Ref<Button>(worldUI, "multiverseButton"), -8f);
        Destination(Ref<Button>(worldUI, "homeButton"), -8f - 104f - 6f);

        friend.transform.SetSiblingIndex(0);
        dm.transform.SetSiblingIndex(1);
        chat.transform.SetSiblingIndex(2);
        gear.SetSiblingIndex(3);
        world.SetSiblingIndex(4);
    }

    // 도구 줄의 버튼 (친구, 1:1 채팅, 광장 채팅)
    private static Button ToolButton(Button button, RectTransform bar, float width)
    {
        RectTransform rect = (RectTransform)button.transform;
        rect.SetParent(bar, false);
        rect.sizeDelta = new Vector2(width, BarHeight);
        Secondary(button, 24);
        Shrink(button.GetComponentInChildren<Text>(true), 14);
        return button;
    }

    // [내 마을], [광장]: 갈 수 있으면 밝은 칩, 지금 있는 곳이면(누를 수 없음) 짙은 칩. 글자색은 WorldUI 가 바꾼다
    private static void Destination(Button button, float x)
    {
        RectTransform rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(x, 0f);
        rect.sizeDelta = new Vector2(104f, 18f * U);
        Image image = Img(rect, kit.pillChip, Color.white, true, true);
        button.targetGraphic = image;
        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState { disabledSprite = kit.pillWood };
        Text label = button.GetComponentInChildren<Text>(true);
        Stretch(label.rectTransform, 4f, 0f, 4f, 0f);
        Txt(label, 20, Wood, true, TextAnchor.MiddleCenter);
    }

    // ---------- 계정 메뉴 (톱니바퀴) ----------

    private static void BuildAccountMenu(Transform hud)
    {
        const float pad = 18f, buttonWidth = 146f, buttonHeight = 24f * U;

        RectTransform menu = Child(hud, "AccountMenu");
        menu.anchorMin = menu.anchorMax = menu.pivot = new Vector2(1f, 1f);
        menu.anchoredPosition = new Vector2(-(Margin + WorldBarWidth + Gap), -PanelTop); // 톱니바퀴의 오른쪽 끝에 맞춘다
        menu.sizeDelta = new Vector2(pad * 2f + buttonWidth * 2f + 12f, 142f);
        Img(menu, kit.window, Color.white, true, true);

        // 유저명
        RectTransform icon = Child(menu, "Icon");
        icon.anchorMin = icon.anchorMax = icon.pivot = new Vector2(0f, 1f);
        icon.anchoredPosition = new Vector2(pad, -16f);
        icon.sizeDelta = new Vector2(18f * U, 18f * U);
        Img(icon, kit.dotPlayer, Color.white, false, false);
        RectTransform person = Child(icon, "Glyph");
        Stretch(person, 5f, 5f, 5f, 5f);
        Img(person, kit.glyphPerson, Color.white, false, false);

        RectTransform nameRect = Child(menu, "NameText");
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0f, 1f);
        nameRect.offsetMin = new Vector2(pad + 18f * U + 10f, -16f - 18f * U);
        nameRect.offsetMax = new Vector2(-pad, -16f);
        Text nameText = GetOrAdd<Text>(nameRect.gameObject);
        if (string.IsNullOrEmpty(nameText.text)) nameText.text = "유저명";
        Txt(nameText, 24, Ink, true, TextAnchor.MiddleLeft);
        Shrink(nameText, 14);

        // [로그아웃] [회원 탈퇴]: 버튼은 LogoutUI, WithdrawUI 가 쓰던 것을 그대로 옮겨 온다
        Button logout = Ref<Button>(hud.GetComponent<LogoutUI>(), "logoutButton");
        RectTransform logoutRect = (RectTransform)logout.transform;
        logoutRect.SetParent(menu, false);
        logoutRect.anchorMin = logoutRect.anchorMax = logoutRect.pivot = new Vector2(0f, 0f);
        logoutRect.anchoredPosition = new Vector2(pad, 26f);
        logoutRect.sizeDelta = new Vector2(buttonWidth, buttonHeight);
        Secondary(logout, 22);

        Button withdraw = Ref<Button>(hud.GetComponent<WithdrawUI>(), "withdrawButton");
        RectTransform withdrawRect = (RectTransform)withdraw.transform;
        withdrawRect.SetParent(menu, false);
        withdrawRect.anchorMin = withdrawRect.anchorMax = withdrawRect.pivot = new Vector2(1f, 0f);
        withdrawRect.anchoredPosition = new Vector2(-pad, 26f);
        withdrawRect.sizeDelta = new Vector2(buttonWidth, buttonHeight);
        DangerButton(withdraw, 22);

        AccountMenuUI account = GetOrAdd<AccountMenuUI>(hud.gameObject);
        SetRef(account, "gearButton", hud.Find("TopBar/GearButton").GetComponent<Button>());
        SetRef(account, "menu", menu.gameObject);
        SetRef(account, "nameText", nameText);
        SetRefs(account, "otherPanels", hud.Find("ChatPanel").gameObject, hud.Find("FriendPanel").gameObject);
        menu.gameObject.SetActive(false);
    }

    // ---------- 채팅창, 친구 창 ----------

    // 도구 줄의 채팅 버튼들 아래에 여는 창
    private static RectTransform DropPanel(Transform hud, string name)
    {
        RectTransform panel = (RectTransform)hud.Find(name);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1f, 1f);
        panel.anchoredPosition = new Vector2(-(Margin + WorldBarWidth + Gap + BarHeight + Gap), -PanelTop); // 채팅 버튼의 오른쪽 끝에 맞춘다
        panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        Img(panel, kit.window, Color.white, true, true);
        return panel;
    }

    private static void StyleChatPanel(Transform hud)
    {
        RectTransform panel = DropPanel(hud, "ChatPanel");
        MessageArea((RectTransform)panel.Find("Messages"), 80f, 18f);
        MessageRow(panel);
    }

    // 메시지가 쌓이는 칸: 아래쪽에 입력 줄, 위쪽에 제목 줄이 들어갈 만큼 띄운다
    private static void MessageArea(RectTransform area, float bottom, float top)
    {
        Stretch(area, 18f, top, 18f, bottom);
        Img(area, kit.card, Color.white, true, true);
        ScrollRect scroll = area.GetComponent<ScrollRect>();
        Stretch(scroll.viewport, 6f, 6f, 6f, 6f); // 글자가 칸의 테두리 위에 그려지지 않게
        Text content = scroll.content.GetComponent<Text>();
        Txt(content, 22, Ink, false, TextAnchor.UpperLeft);
        content.horizontalOverflow = HorizontalWrapMode.Wrap;
        content.verticalOverflow = VerticalWrapMode.Overflow;
        content.lineSpacing = 1.1f;
    }

    // 창 아래쪽의 입력 칸과 [전송]
    private static void MessageRow(RectTransform parent)
    {
        RectTransform input = (RectTransform)parent.Find("MessageInput");
        input.anchorMin = new Vector2(0f, 0f);
        input.anchorMax = new Vector2(1f, 0f);
        input.pivot = new Vector2(0.5f, 0f);
        input.offsetMin = new Vector2(18f, 26f);
        input.offsetMax = new Vector2(-(18f + 100f + 8f), 26f + 24f * U);
        Field(input.GetComponent<InputField>());

        RectTransform send = (RectTransform)parent.Find("SendButton");
        send.anchorMin = send.anchorMax = send.pivot = new Vector2(1f, 0f);
        send.anchoredPosition = new Vector2(-18f, 26f);
        send.sizeDelta = new Vector2(100f, 24f * U);
        Primary(send.GetComponent<Button>(), 22);
    }

    private static void StyleFriendPanel(Transform hud)
    {
        RectTransform panel = DropPanel(hud, "FriendPanel");
        FriendUI friendUI = hud.GetComponent<FriendUI>();

        // --- 목록 화면: 닉네임 입력 줄(또는 제목), 안내 한 줄, 친구 목록
        RectTransform addRow = (RectTransform)panel.Find("ListView/AddRow");
        TopRow(addRow, 18f, 18f, 18f, 24f * U);
        RectTransform nickname = (RectTransform)addRow.Find("NicknameInput");
        Stretch(nickname, 0f, 0f, 100f + 8f, 0f);
        Field(nickname.GetComponent<InputField>());
        RectTransform add = (RectTransform)addRow.Find("AddButton");
        add.anchorMin = new Vector2(1f, 0f);
        add.anchorMax = new Vector2(1f, 1f);
        add.pivot = new Vector2(1f, 0.5f);
        add.anchoredPosition = Vector2.zero;
        add.sizeDelta = new Vector2(100f, 0f);
        Primary(add.GetComponent<Button>(), 22);

        Text title = Ref<Text>(friendUI, "listTitleText");
        TopRow(title.rectTransform, 22f, 18f, 18f, 24f * U);
        Txt(title, 22, Ink, true, TextAnchor.MiddleLeft);
        Shrink(title, 14);

        Text status = Ref<Text>(friendUI, "statusText");
        TopRow(status.rectTransform, 22f, 18f + 24f * U + 4f, 18f, 28f);
        Txt(status, 20, Wood, true, TextAnchor.MiddleLeft);
        Shrink(status, 13);

        const float listTop = 18f + 24f * U + 4f + 28f + 4f;
        RectTransform list = (RectTransform)panel.Find("ListView/FriendList");
        Stretch(list, 18f, listTop, 18f, 26f);
        Img(list, kit.card, Color.white, true, true);
        ScrollRect listScroll = list.GetComponent<ScrollRect>();
        Stretch(listScroll.viewport, 4f, 4f, 4f, 4f);
        VerticalLayoutGroup rows = listScroll.content.GetComponent<VerticalLayoutGroup>();
        rows.padding = new RectOffset(6, 6, 6, 6);
        rows.spacing = 6f;

        Text empty = Ref<Text>(friendUI, "emptyText");
        Stretch(empty.rectTransform, 18f, listTop, 18f, 26f);
        Txt(empty, 22, Muted, false, TextAnchor.MiddleCenter);

        // 친구 한 줄: 이름(누르면 1:1 채팅) + 버튼 두 개
        FriendRowUI row = Ref<FriendRowUI>(friendUI, "rowTemplate");
        Button open = Ref<Button>(row, "openButton");
        Image openImage = Img((RectTransform)open.transform, kit.pillChip, Color.white, true, true);
        open.targetGraphic = openImage;
        open.transition = Selectable.Transition.ColorTint;
        open.colors = Tint();
        Text rowLabel = Ref<Text>(row, "label");
        OneLine(rowLabel.rectTransform, 22);
        rowLabel.rectTransform.offsetMin = new Vector2(14f, rowLabel.rectTransform.offsetMin.y);
        rowLabel.rectTransform.offsetMax = new Vector2(-8f, rowLabel.rectTransform.offsetMax.y);
        Txt(rowLabel, 22, Ink, true, TextAnchor.MiddleLeft);
        Shrink(rowLabel, 14); // 긴 닉네임이나 "(새 메시지 n)" 이 붙어도 한 줄에 보이게
        Secondary(Ref<Button>(row, "visitButton"), 20);
        DangerButton(Ref<Button>(row, "removeButton"), 20);
        SetColor(row, "onlineColor", Green);
        SetColor(row, "offlineColor", Offline);
        SetColor(row, "requestColor", Request);

        // --- 대화 화면: [목록] 과 제목, 메시지, 입력 줄
        RectTransform chatView = (RectTransform)panel.Find("ChatView");
        RectTransform back = (RectTransform)chatView.Find("BackButton");
        back.anchorMin = back.anchorMax = back.pivot = new Vector2(0f, 1f);
        back.anchoredPosition = new Vector2(18f, -18f);
        back.sizeDelta = new Vector2(100f, 22f * U);
        Secondary(back.GetComponent<Button>(), 20);

        Text chatTitle = Ref<Text>(friendUI, "chatTitleText");
        TopRow(chatTitle.rectTransform, 18f + 100f + 12f, 18f, 18f, 22f * U);
        Txt(chatTitle, 22, Ink, true, TextAnchor.MiddleLeft);
        Shrink(chatTitle, 14);

        MessageArea((RectTransform)chatView.Find("Messages"), 80f, 18f + 22f * U + 8f);
        MessageRow(chatView);
    }

    // ---------- 안내, 확인 창, 로그인 창 ----------

    private static void StyleNotice(Transform hud)
    {
        WorldUI worldUI = hud.GetComponent<WorldUI>();
        RectTransform bar = (RectTransform)Ref<GameObject>(worldUI, "noticeBar").transform;
        Image image = Img(bar, kit.pillWood, Color.white, true, false);
        image.raycastTarget = false; // 안내가 뒤의 게임 화면 클릭을 막지 않게
        Text text = Ref<Text>(worldUI, "noticeText");
        Txt(text, 22, Cream, true, TextAnchor.MiddleCenter);
        Outline outline = text.GetComponent<Outline>();
        if (outline != null) outline.enabled = false; // 짙은 바탕 위라 외곽선이 필요 없다
    }

    private static void StyleDialog(Transform dialog)
    {
        ConfirmDialog confirm = dialog.GetComponent<ConfirmDialog>();
        RectTransform panel = (RectTransform)Ref<GameObject>(confirm, "panel").transform;
        panel.GetComponent<Image>().color = Dim;
        // 로그인 창 위에 뜰 때 뒤의 창이 삐져나와 보이지 않게 로그인 창과 같은 크기로 한다
        RectTransform box = (RectTransform)panel.Find("Box");
        Center(box, 0f, 0f, 520f, 340f);
        Img(box, kit.window, Color.white, true, true);
        Text message = Ref<Text>(confirm, "messageText");
        Center(message.rectTransform, 0f, 48f, 460f, 160f);
        Txt(message, 24, Ink, true, TextAnchor.MiddleCenter);
        Center((RectTransform)box.Find("Buttons"), 0f, -98f, 460f, 60f);
        Primary(Ref<Button>(confirm, "okButton"), 24);
        Secondary(Ref<Button>(confirm, "cancelButton"), 24);
    }

    private static void StyleLogin(Transform login)
    {
        LoginUI loginUI = login.GetComponent<LoginUI>();
        RectTransform panel = (RectTransform)Ref<GameObject>(loginUI, "loginPanel").transform;
        panel.GetComponent<Image>().color = Dim;
        Img((RectTransform)panel.Find("Box"), kit.window, Color.white, true, true);
        Txt(Ref<Text>(loginUI, "statusText"), 22, Ink, true, TextAnchor.MiddleCenter);
        Primary(Ref<Button>(loginUI, "steamButton"), 26);
        Secondary(Ref<Button>(loginUI, "guestButton"), 26);
        Field(Ref<InputField>(loginUI, "guestIdInput"));
        Field(Ref<InputField>(loginUI, "guestPasswordInput"));
        Primary(Ref<Button>(loginUI, "guestSubmitButton"), 24);
        Secondary(Ref<Button>(loginUI, "guestBackButton"), 24);
    }

    // ---------- 모양 ----------

    // 초록 버튼 (주된 동작): 누를 수 없을 때는 회색 스프라이트
    private static void Primary(Button button, int fontSize)
    {
        Image image = Img((RectTransform)button.transform, kit.btnPrimary, Color.white, true, true);
        button.targetGraphic = image;
        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState { disabledSprite = kit.btnDisabled };
        ButtonLabel(button, fontSize, Color.white, 6f);
    }

    // 크림색 버튼 (그 밖의 동작)
    private static void Secondary(Button button, int fontSize)
    {
        Image image = Img((RectTransform)button.transform, kit.btnSecondary, Color.white, true, true);
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = Tint();
        ButtonLabel(button, fontSize, Wood, 4f);
    }

    // 붉은 테두리 버튼 (되돌릴 수 없는 동작: 회원 탈퇴, 친구 삭제)
    private static void DangerButton(Button button, int fontSize)
    {
        Image image = Img((RectTransform)button.transform, kit.btnDanger, Color.white, true, true);
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = Tint();
        ButtonLabel(button, fontSize, Danger, 4f);
    }

    private static void PlainButton(GameObject go)
    {
        Button button = GetOrAdd<Button>(go);
        button.targetGraphic = go.GetComponent<Image>();
        button.transition = Selectable.Transition.ColorTint;
        button.colors = Tint();
    }

    // 버튼의 글자: lift 는 버튼 아래쪽 그림자만큼 글자를 올리는 값
    private static void ButtonLabel(Button button, int fontSize, Color color, float lift)
    {
        Text label = button.GetComponentInChildren<Text>(true);
        OneLine(label.rectTransform, fontSize);
        label.rectTransform.anchoredPosition = new Vector2(0f, lift / 2f);
        Txt(label, fontSize, color, true, TextAnchor.MiddleCenter);
    }

    // 버튼을 눌렀을 때와 누를 수 없을 때의 색 (팀 빌더와 같은 값)
    private static ColorBlock Tint()
    {
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.highlightedColor = new Color(1f, 0.97f, 0.92f);
        colors.pressedColor = new Color(0.9f, 0.86f, 0.8f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.85f, 0.82f, 0.78f, 0.7f);
        return colors;
    }

    // 입력 칸
    private static void Field(InputField input)
    {
        Image image = Img((RectTransform)input.transform, kit.pillChip, Color.white, true, true);
        input.targetGraphic = image;
        input.transition = Selectable.Transition.None;
        input.customCaretColor = true;
        input.caretColor = Ink;
        input.selectionColor = new Color(0.91f, 0.51f, 0.23f, 0.35f);
        Txt(input.textComponent, 22, Ink, false, TextAnchor.MiddleLeft);
        Text placeholder = input.placeholder as Text;
        if (placeholder != null)
        {
            Color faded = Muted;
            faded.a = 0.6f;
            Txt(placeholder, 22, faded, false, TextAnchor.MiddleLeft);
        }
    }

    private static Image Img(RectTransform rect, Sprite sprite, Color color, bool sliced, bool raycast)
    {
        Image image = GetOrAdd<Image>(rect.gameObject);
        image.sprite = sprite;
        image.color = color;
        image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        image.fillCenter = true;
        image.pixelsPerUnitMultiplier = BorderScale;
        image.raycastTarget = raycast;
        image.enabled = true;
        return image;
    }

    private static void Txt(Text text, int size, Color color, bool bold, TextAnchor anchor)
    {
        text.font = bold ? kit.bold : kit.regular; // 굵은 글자는 굵은 글꼴 파일을 쓴다 (fontStyle 로 굵게 하면 뭉개진다)
        text.fontStyle = FontStyle.Normal;
        text.fontSize = size;
        text.color = color;
        text.alignment = anchor;
        text.resizeTextForBestFit = false;
        text.raycastTarget = false;
    }

    // 칸에 다 들어가지 않는 글자는 min 크기까지 줄여서 보여 준다
    private static void Shrink(Text text, int min)
    {
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = min;
        text.resizeTextMaxSize = text.fontSize;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
    }

    // ---------- 배치 ----------

    // 부모를 꽉 채우되 네 변에서 그만큼 들여 놓는다
    private static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    // 부모의 가로를 채우는 글자 한 줄 (세로 가운데). 높이를 한 줄만큼만 주어서, 줄여 보여 주는 글자(Shrink)가
    // 두 줄로 넘어가는 대신 작아지게 한다
    private static void OneLine(RectTransform rect, int fontSize)
    {
        float left = rect.offsetMin.x, right = rect.offsetMax.x;
        bool stretched = rect.anchorMin.x == 0f && rect.anchorMax.x == 1f;
        Vector2 position = rect.anchoredPosition;
        Vector2 size = rect.sizeDelta;
        rect.anchorMin = new Vector2(rect.anchorMin.x, 0.5f);
        rect.anchorMax = new Vector2(rect.anchorMax.x, 0.5f);
        rect.pivot = new Vector2(rect.pivot.x, 0.5f);
        rect.anchoredPosition = new Vector2(position.x, 0f);
        rect.sizeDelta = new Vector2(stretched ? -12f : size.x, fontSize * 1.5f);
    }

    // 부모의 위쪽에 붙는 한 줄
    private static void TopRow(RectTransform rect, float left, float top, float right, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(left, -top - height);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void Center(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    // ---------- 찾기, 만들기 ----------

    private static Transform Root(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name) return root.transform;
        }
        throw new Exception(name + " 오브젝트가 씬에 없습니다.");
    }

    // 이름이 name 인 자식 (없으면 만든다)
    private static RectTransform Child(Transform parent, string name)
    {
        Transform found = parent.Find(name);
        if (found != null) return (RectTransform)found;

        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    // 스크립트에 연결되어 있는 오브젝트 (씬 안에서 자리를 옮겨도 찾을 수 있게 이름 대신 연결로 찾는다)
    private static T Ref<T>(UnityEngine.Object owner, string field) where T : UnityEngine.Object
    {
        if (owner == null) throw new Exception(typeof(T).Name + " " + field + ": 스크립트가 없습니다.");
        SerializedProperty property = new SerializedObject(owner).FindProperty(field);
        T value = property != null ? property.objectReferenceValue as T : null;
        if (value == null) throw new Exception(owner.GetType().Name + "." + field + " 이 연결되어 있지 않습니다.");
        return value;
    }

    private static void SetRef(UnityEngine.Object owner, string field, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(owner);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetRefs(UnityEngine.Object owner, string field, params UnityEngine.Object[] values)
    {
        var serialized = new SerializedObject(owner);
        SerializedProperty array = serialized.FindProperty(field);
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetColor(UnityEngine.Object owner, string field, Color color)
    {
        var serialized = new SerializedObject(owner);
        serialized.FindProperty(field).colorValue = color;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------- 스프라이트, 글꼴 ----------

    private static Kit LoadKit()
    {
        return new Kit
        {
            window = Cozy("window"),
            card = Cozy("card"),
            pillCream = Cozy("pill_cream"),
            pillChip = Cozy("pill_chip"),
            pillWood = Cozy("pill_wood"),
            btnPrimary = Cozy("btn_primary"),
            btnSecondary = Cozy("btn_secondary"),
            btnDanger = Cozy("btn_danger"),
            btnDisabled = Cozy("btn_disabled"),
            dotPlayer = Cozy("dot_player"),
            glyphPerson = Cozy("glyph_person"),
            glyphGear = GearGlyph(),
            regular = Load<Font>(RegularFontPath),
            bold = Load<Font>(BoldFontPath),
        };
    }

    private static Sprite Cozy(string name)
    {
        return Load<Sprite>(CozyFolder + "/" + name + ".png");
    }

    private static T Load<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new Exception(path + " 을 찾지 못했습니다.");
        return asset;
    }

    // 톱니바퀴 아이콘. 팀에는 없는 그림이라 팀 아이콘과 같은 도구(CozySpriteFactory.Painter)로 그려서 내 폴더에 저장한다
    //   흰색으로 그리고, 쓰는 쪽(Image.color)에서 색을 입힌다
    private static Sprite GearGlyph()
    {
        const float size = 20f; // 탭 아이콘과 같은 20단위
        const int scale = 2;    // 1단위 = 2픽셀 (팀 스프라이트와 같다)
        const float c = size / 2f;

        var teeth = new Func<float, float, float>[8];
        for (int i = 0; i < teeth.Length; i++)
        {
            float angle = i * Mathf.PI / 4f;
            teeth[i] = Turned(CozySpriteFactory.Sdf.RoundBox(0f, 0f, 1.7f, 1.9f, 0.6f),
                c + Mathf.Sin(angle) * 6.9f, c - Mathf.Cos(angle) * 6.9f, angle);
        }
        Func<float, float, float> body = CozySpriteFactory.Sdf.Union(CozySpriteFactory.Sdf.Circle(c, c, 5.7f), CozySpriteFactory.Sdf.Union(teeth));
        Func<float, float, float> hole = CozySpriteFactory.Sdf.Circle(c, c, 2.5f);

        var painter = new CozySpriteFactory.Painter(size, size, scale);
        painter.Fill((x, y) => Mathf.Max(body(x, y), -hole(x, y)), Color.white); // 몸통에서 가운데 구멍을 뺀다

        string path = OwnFolder + "/glyph_gear.png";
        CozySpriteFactory.EnsureFolder(OwnFolder);
        Texture2D texture = painter.ToTexture();
        File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), path), texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f * scale;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return Load<Sprite>(path);
    }

    // 도형을 (cx, cy) 로 옮기고 angle 만큼 돌린 것
    private static Func<float, float, float> Turned(Func<float, float, float> shape, float cx, float cy, float angle)
    {
        float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
        return (x, y) =>
        {
            float dx = x - cx, dy = y - cy;
            return shape(dx * cos + dy * sin, dx * sin - dy * cos);
        };
    }
}
