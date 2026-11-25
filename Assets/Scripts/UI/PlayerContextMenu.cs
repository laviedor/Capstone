using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 다른 플레이어를 우클릭하면 마우스 옆에 뜨는 작은 메뉴 (RemotePlayerManager 가 띄운다)
//   ┌───────────────┐
//   │ 닉네임         │
//   │ 친구 요청 받음  │   지금 관계
//   │ [ 요청 수락 ]  │   친구가 아니면 [친구 추가], 요청을 보내 두었으면 [요청 취소],
//   └───────────────┘   요청을 받았으면 [요청 수락] [요청 거절], 친구면 [1:1 채팅] [친구 삭제]
//                       (버튼이 둘이면 메뉴가 조금 넓어진다)
//   친구 목록과 요청은 친구 창(FriendUI)이 가진 것을 쓰고, 실제 요청/취소/수락/거절/채팅 열기/삭제도 FriendUI 가 한다
//   (친구 삭제만 친구 창의 [삭제]와 같이 확인창에서 한 번 더 묻는다. 나머지는 친구 창에서처럼 누르면 바로 한다)
//   Tab 메뉴와 같은 모양으로 실행 중에 만든다 (CozyRuntimeUI). 씬에 따로 놓지 않아도 처음 쓸 때 스스로 만들어진다
//   다른 곳을 누르거나 Esc, Tab 메뉴, 건설 모드, 바닥 바꾸기 모드, 제작 창을 켜거나 그 사람이 떠나면 사라진다
public class PlayerContextMenu : MonoBehaviour
{
    private const float PanelWidth = 132f;     // 버튼이 하나일 때의 메뉴 폭
    private const float WidePanelWidth = 160f; // 버튼이 둘일 때 (친구, 받은 요청)
    private const float PanelHeight = 74f;
    private const float Padding = 10f;       // 메뉴 안쪽 좌우 여백
    private const float ButtonGap = 6f;      // 나란히 놓인 버튼 사이 간격
    private const float MouseGap = 10f;      // 마우스 커서와 메뉴 사이 간격
    private const float ScreenMargin = 4f;   // 화면 가장자리와의 최소 간격
    private const float PopSeconds = 0.08f;  // 나타날 때 커지는 시간

    private static readonly Color GreenFallback = new Color32(0x3F, 0x7A, 0x2E, 0xFF);

    private static PlayerContextMenu instance;

    private RectTransform canvasRect;
    private RectTransform panel;
    private TMP_Text nameText;
    private TMP_Text statusText;
    private Button actionButton;  // 첫 버튼: 친구 추가 / 요청 취소 / 요청 수락 / 1:1 채팅
    private Image actionImage;
    private TMP_Text actionText;
    private Button secondButton;  // 두 번째 버튼: 요청 거절 (요청을 받았을 때) / 친구 삭제 (친구일 때). 그 밖에는 숨긴다
    private TMP_Text secondText;

    private RemotePlayer target;
    private FriendUI friendUI;
    private FriendUI.Relation relation;
    private bool listening; // 친구 목록이 바뀌는 것을 듣고 있는지 (메뉴가 떠 있는 동안만)
    private Vector2 shownPosition; // 메뉴를 띄운 마우스 위치 (화면 좌표)
    private float shownAt;
    private int shownFrame;

    private InventoryUI inventoryUI;
    private BuildingModeManager buildingModeManager;

    // 그 캐릭터 옆(마우스 위치)에 메뉴를 띄운다
    public static void Show(RemotePlayer player, Vector2 screenPosition)
    {
        if (player == null) return;
        if (instance == null) instance = Create();
        instance.Open(player, screenPosition);
    }

    public static void Hide()
    {
        if (instance != null) instance.Close();
    }

    private static PlayerContextMenu Create()
    {
        RectTransform canvas = CozyRuntimeUI.CreateCanvas("PlayerContextMenu", 300);
        PlayerContextMenu menu = canvas.gameObject.AddComponent<PlayerContextMenu>();
        menu.Build(canvas);
        return menu;
    }

    private void Build(RectTransform canvas)
    {
        canvasRect = canvas;

        Image window = CozyRuntimeUI.Image(canvas, "Panel", "window", CozyRuntimeUI.Cream, true);
        panel = window.rectTransform;
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);

        nameText = CozyRuntimeUI.Text(panel, "Name", "", 12f, CozyRuntimeUI.Ink, TextAlignmentOptions.MidlineLeft, true);
        statusText = CozyRuntimeUI.Text(panel, "Status", "", 9.5f, CozyRuntimeUI.Muted, TextAlignmentOptions.MidlineLeft, false);

        actionButton = CozyRuntimeUI.Button(panel, "ActionButton", "btn_primary", "", CozyRuntimeUI.Cream, 11f, OnAction, GreenFallback);
        actionImage = actionButton.GetComponent<Image>();
        actionText = actionButton.GetComponentInChildren<TMP_Text>();

        // 끊거나 물리는 동작은 친구 창의 [삭제], [거절], [취소]처럼 붉은 테두리 버튼
        secondButton = CozyRuntimeUI.Button(panel, "SecondButton", "btn_danger", "", CozyRuntimeUI.Danger, 11f, OnSecond, CozyRuntimeUI.Cream);
        secondText = secondButton.GetComponentInChildren<TMP_Text>();

        Layout(false);
        panel.gameObject.SetActive(false);
    }

    // 버튼 수에 맞춰 메뉴의 폭과 안의 배치를 정한다 (two: 버튼 둘을 나란히)
    private void Layout(bool two)
    {
        float width = two ? WidePanelWidth : PanelWidth;
        float inner = width - Padding * 2f;
        float buttonWidth = two ? (inner - ButtonGap) / 2f : inner;

        panel.sizeDelta = new Vector2(width, PanelHeight);
        CozyRuntimeUI.TopLeft(nameText.rectTransform, Padding, 8f, inner, 16f);
        CozyRuntimeUI.TopLeft(statusText.rectTransform, Padding, 24f, inner, 13f);
        CozyRuntimeUI.TopLeft((RectTransform)actionButton.transform, Padding, 40f, buttonWidth, 24f);
        CozyRuntimeUI.TopLeft((RectTransform)secondButton.transform, Padding + buttonWidth + ButtonGap, 40f, buttonWidth, 24f);
        secondButton.gameObject.SetActive(two);
    }

    private void Open(RemotePlayer player, Vector2 screenPosition)
    {
        if (friendUI == null) friendUI = FindFirstObjectByType<FriendUI>();
        if (friendUI == null) return; // 친구 기능이 없는 씬

        if (!listening)
        {
            friendUI.ListChanged += Refresh;
            listening = true;
        }
        target = player;
        shownPosition = screenPosition;
        nameText.text = player.PlayerName;
        Refresh(); // 관계에 맞는 버튼과 메뉴 크기를 정하고 마우스 옆에 놓는다
        friendUI.RefreshList(); // 메뉴가 떠 있는 동안 관계가 최신이 되게 서버에서 다시 받는다 (바뀌면 ListChanged 로 다시 그린다)

        panel.gameObject.SetActive(true);
        panel.localScale = Vector3.one * 0.85f;
        shownAt = Time.unscaledTime;
        shownFrame = Time.frameCount;
    }

    private void Close()
    {
        StopListening();
        target = null;
        if (panel != null) panel.gameObject.SetActive(false);
    }

    private void StopListening()
    {
        if (listening && friendUI != null) friendUI.ListChanged -= Refresh;
        listening = false;
    }

    // 지금 관계에 맞춰 안내와 버튼을 정한다
    private void Refresh()
    {
        if (target == null || friendUI == null) return;

        relation = friendUI.RelationWith(target.UserId, target.PlayerName);
        string second = null; // 두 번째 버튼의 글자 (null 이면 버튼 하나)
        bool undo = false;    // 첫 버튼이 물리는 동작(요청 취소)인지: 붉은 테두리 버튼으로 그린다

        switch (relation)
        {
            case FriendUI.Relation.Friend:
                statusText.text = "친구";
                actionText.text = "1:1 채팅";
                second = "친구 삭제";
                break;
            case FriendUI.Relation.RequestReceived:
                statusText.text = "친구 요청을 받았습니다";
                actionText.text = "요청 수락";
                second = "요청 거절";
                break;
            case FriendUI.Relation.RequestSent:
                statusText.text = "친구 요청을 보냈습니다";
                actionText.text = "요청 취소";
                undo = true;
                break;
            default:
                statusText.text = "아직 친구가 아닙니다";
                actionText.text = "친구 추가";
                break;
        }

        Sprite sprite = CozyRuntimeUI.Sprite(undo ? "btn_danger" : "btn_primary");
        if (sprite != null) actionImage.sprite = sprite;
        else actionImage.color = undo ? CozyRuntimeUI.Cream : GreenFallback; // 스프라이트가 없을 때의 단색 버튼
        actionText.color = undo ? CozyRuntimeUI.Danger : CozyRuntimeUI.Cream;
        if (second != null) secondText.text = second;

        // 버튼이 둘이면 메뉴가 조금 넓어진다. 폭이 달라지므로 화면 안에 들어오게 다시 놓는다
        Layout(second != null);
        Place(shownPosition);
    }

    private void OnAction()
    {
        RemotePlayer player = target;
        FriendUI.Relation current = relation;
        Close();
        if (player == null || friendUI == null) return;

        switch (current)
        {
            case FriendUI.Relation.Friend:
                friendUI.OpenChatWith(player.UserId, player.PlayerName);
                break;
            case FriendUI.Relation.RequestReceived:
                friendUI.AcceptRequestFrom(player.UserId, player.PlayerName);
                break;
            case FriendUI.Relation.RequestSent:
                friendUI.CancelRequestTo(player.UserId, player.PlayerName);
                break;
            default:
                friendUI.RequestFriend(player.PlayerName);
                break;
        }
    }

    // 두 번째 버튼: 친구면 [친구 삭제] (FriendUI 가 확인창으로 한 번 더 물은 뒤 삭제한다), 요청을 받았으면 [요청 거절]
    private void OnSecond()
    {
        RemotePlayer player = target;
        FriendUI.Relation current = relation;
        Close();
        if (player == null || friendUI == null) return;

        if (current == FriendUI.Relation.Friend)
            friendUI.RemoveFriend(player.UserId, player.PlayerName);
        else if (current == FriendUI.Relation.RequestReceived)
            friendUI.RejectRequestFrom(player.UserId, player.PlayerName);
    }

    // 마우스 오른쪽에 둔다. 화면 오른쪽에 자리가 없으면 왼쪽에 두고, 위아래로도 화면 안에 들어오게 한다
    private void Place(Vector2 screenPosition)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out Vector2 local);

        Vector2 size = panel.sizeDelta;
        Vector2 half = canvasRect.rect.size / 2f;
        bool fitsRight = local.x + MouseGap + size.x <= half.x - ScreenMargin;
        panel.pivot = new Vector2(fitsRight ? 0f : 1f, 0.5f);

        float x = fitsRight ? local.x + MouseGap : local.x - MouseGap;
        float limit = half.y - size.y / 2f - ScreenMargin;
        float y = limit > 0f ? Mathf.Clamp(local.y, -limit, limit) : 0f;
        panel.anchoredPosition = new Vector2(x, y);
    }

    private void Update()
    {
        if (!panel.gameObject.activeSelf) return;

        // 나타날 때 살짝 커지는 효과
        float t = Mathf.Clamp01((Time.unscaledTime - shownAt) / PopSeconds);
        panel.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, t);

        // 그 사람이 떠났거나(오브젝트가 지워짐) 로그아웃됨
        if (target == null || string.IsNullOrEmpty(LoginUI.Username))
        {
            Close();
            return;
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Close();
            return;
        }

        if (inventoryUI == null) inventoryUI = FindFirstObjectByType<InventoryUI>();
        if (buildingModeManager == null) buildingModeManager = FindFirstObjectByType<BuildingModeManager>();
        if ((inventoryUI != null && inventoryUI.IsOpen) || (buildingModeManager != null && buildingModeManager.IsBuildMode) ||
            TilePainter.IsActive || CraftingUI.IsOpen)
        {
            Close();
            return;
        }

        // 메뉴가 아닌 곳을 누르면 닫는다 (메뉴를 띄운 그 우클릭은 빼고. 다른 캐릭터를 우클릭하면 RemotePlayerManager 가 다시 띄운다)
        Mouse mouse = Mouse.current;
        if (Time.frameCount != shownFrame && mouse != null &&
            (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame) &&
            !RectTransformUtility.RectangleContainsScreenPoint(panel, mouse.position.ReadValue(), null))
        {
            Close();
        }
    }

    private void OnDestroy()
    {
        StopListening();
        if (instance == this) instance = null;
    }
}
