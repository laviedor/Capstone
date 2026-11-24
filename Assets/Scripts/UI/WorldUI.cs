using UnityEngine;
using UnityEngine.UI;

// 화면 오른쪽 위 도구 줄의 오른쪽 끝: 지금 있는 곳(멀티버스 / 내 마을 / 친구 마을)과 [내 마을], [멀티버스] 이동 버튼
// 화면 아래 가운데: 잠깐 떴다 사라지는 안내(Toast, 서버가 요청을 거절한 이유 등)
//   화면 가운데는 Tab 메뉴(인벤토리)가 차지하므로 가리지 않는 자리에 둔다 (게임 창은 가로로 길고 낮다)
//   건설·철거 모드에는 같은 자리에 건설 안내 막대(BuildModeHUD)가 뜨므로, 겹치는 동안에는 안내를 그 막대 바로 위로 올린다
//   친구 마을은 친구 창(FriendUI)의 [방문] 으로 들어간다
public class WorldUI : MonoBehaviour
{
    [SerializeField] private WorldSync worldSync;
    [SerializeField] private ChatClient chatClient;
    [SerializeField] private GameObject bar; // 위치와 이동 버튼 (로그인 후에 표시)
    [SerializeField] private Text locationText;
    [SerializeField] private Button homeButton;
    [SerializeField] private Button multiverseButton;
    [SerializeField] private Color destinationTextColor = new Color32(0x7A, 0x4F, 0x33, 0xFF); // 갈 수 있는 곳의 버튼 글자색
    [SerializeField] private Color currentTextColor = new Color32(0xFF, 0xF8, 0xEC, 0xFF);     // 지금 있는 곳의 버튼 글자색
    [SerializeField] private GameObject noticeBar; // 안내의 배경 (안내가 떠 있는 동안만 보인다)
    [SerializeField] private Text noticeText;
    [SerializeField] private float noticeSeconds = 4f;
    [SerializeField] private float noticeGap = 8f; // 건설 안내 막대 위로 올릴 때 그 막대와 띄우는 간격 (이 캔버스 단위)

    private float noticeUntil;
    private RectTransform noticeRect;
    private Canvas noticeCanvas;
    private float noticeHomeY;     // 안내의 원래 높이 (화면 맨 아래)
    private BuildModeHUD buildHud; // 건설·철거 모드에 화면 아래 가운데에 뜨는 안내 막대 (BuildingModeManager 가 만든다)
    private readonly Vector3[] corners = new Vector3[4];

    void Awake()
    {
        noticeRect = (RectTransform)noticeBar.transform;
        noticeCanvas = noticeBar.GetComponentInParent<Canvas>(true).rootCanvas;
        noticeHomeY = noticeRect.anchoredPosition.y;

        bar.SetActive(false);
        HideNotice();
        homeButton.onClick.AddListener(worldSync.GoHome);
        multiverseButton.onClick.AddListener(worldSync.GoMultiverse);
    }

    void OnEnable()
    {
        LoginUI.LoggedIn += ShowBar;
        worldSync.Changed += Refresh;
        chatClient.ServerNotice += ShowNotice;
        Toast.Shown += ShowNotice;
    }

    void OnDisable()
    {
        LoginUI.LoggedIn -= ShowBar;
        worldSync.Changed -= Refresh;
        chatClient.ServerNotice -= ShowNotice;
        Toast.Shown -= ShowNotice;
    }

    void ShowBar()
    {
        bar.SetActive(true);
        Refresh();
    }

    void Refresh()
    {
        locationText.text = "현재 위치: " + worldSync.Label;
        SetDestination(homeButton, worldSync.Entered && !worldSync.IsMine);
        SetDestination(multiverseButton, worldSync.Entered && !worldSync.InMultiverse);
    }

    // 지금 있는 곳으로 가는 버튼은 누를 수 없고, 선택된 모양(짙은 바탕에 밝은 글자)으로 보인다
    //   바탕은 버튼의 '누를 수 없을 때' 스프라이트가 맡고, 여기서는 글자색만 맞춘다
    void SetDestination(Button button, bool canGo)
    {
        button.interactable = canGo;
        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null) label.color = canGo ? destinationTextColor : currentTextColor;
    }

    void ShowNotice(string text)
    {
        noticeText.text = text;
        noticeBar.SetActive(true);
        noticeUntil = Time.unscaledTime + noticeSeconds;
    }

    void HideNotice()
    {
        noticeText.text = "";
        noticeBar.SetActive(false);
    }

    void Update()
    {
        if (noticeBar.activeSelf && Time.unscaledTime >= noticeUntil)
            HideNotice();
    }

    // 안내가 떠 있는 동안 자리를 정한다. 건설 안내 막대는 그 스크립트의 Update 에서 켜지고 꺼지므로 그 뒤(LateUpdate)에 맞춘다
    void LateUpdate()
    {
        if (noticeBar.activeSelf) PlaceNotice();
    }

    // 원래 자리(화면 맨 아래)에 두되, 건설 안내 막대와 겹치면 그 막대 바로 위로 올린다
    //   그 막대는 다른 캔버스에 있으므로 화면 픽셀로 비교한 뒤 이 캔버스 단위로 바꾼다
    void PlaceNotice()
    {
        float scale = Mathf.Max(noticeCanvas.scaleFactor, 0.0001f); // 이 캔버스의 단위 1 이 화면에서 몇 픽셀인지
        float lift = 0f; // 원래 자리에서 위로 올릴 높이 (픽셀)

        if (buildHud == null) buildHud = FindFirstObjectByType<BuildModeHUD>();
        if (buildHud != null)
        {
            // 원래 자리에 있을 때 안내가 차지하는 범위 (이미 올라가 있으면 그만큼 내려서 계산)
            Rect notice = ScreenRect(noticeRect);
            notice.y -= (noticeRect.anchoredPosition.y - noticeHomeY) * scale;
            float gap = noticeGap * scale;

            foreach (Transform child in buildHud.transform)
            {
                var part = child as RectTransform;
                if (part == null || !part.gameObject.activeInHierarchy) continue; // 건설·철거 모드가 아니면 막대가 꺼져 있다

                Rect hud = ScreenRect(part);
                bool overlaps = notice.xMin < hud.xMax && hud.xMin < notice.xMax &&
                                notice.yMin < hud.yMax + gap && hud.yMin < notice.yMax;
                if (overlaps) lift = Mathf.Max(lift, hud.yMax + gap - notice.yMin);
            }
        }

        float y = noticeHomeY + lift / scale;
        Vector2 position = noticeRect.anchoredPosition;
        if (Mathf.Approximately(position.y, y)) return;

        position.y = y;
        noticeRect.anchoredPosition = position;
    }

    // 화면에서 차지하는 범위 (픽셀, 왼쪽 아래가 원점)
    Rect ScreenRect(RectTransform rect)
    {
        rect.GetWorldCorners(corners);
        Canvas canvas = rect.GetComponentInParent<Canvas>(true);
        Camera camera = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.rootCanvas.worldCamera
            : null;
        Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
}
