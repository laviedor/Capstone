using UnityEngine;
using UnityEngine.UI;

// 화면 오른쪽 위: 지금 있는 곳(멀티버스 / 내 마을 / 친구 마을)과 [내 마을], [멀티버스] 이동 버튼
// 화면 아래 가운데: 잠깐 떴다 사라지는 안내(Toast, 서버가 요청을 거절한 이유 등)
//   화면 가운데는 Tab 메뉴(인벤토리)가 차지하므로 가리지 않는 자리에 둔다 (게임 창은 가로로 길고 낮다)
//   친구 마을은 친구 창(FriendUI)의 [방문] 으로 들어간다
public class WorldUI : MonoBehaviour
{
    [SerializeField] private WorldSync worldSync;
    [SerializeField] private ChatClient chatClient;
    [SerializeField] private GameObject bar; // 위치와 이동 버튼 (로그인 후에 표시)
    [SerializeField] private Text locationText;
    [SerializeField] private Button homeButton;
    [SerializeField] private Button multiverseButton;
    [SerializeField] private GameObject noticeBar; // 안내의 배경 (안내가 떠 있는 동안만 보인다)
    [SerializeField] private Text noticeText;
    [SerializeField] private float noticeSeconds = 4f;

    private float noticeUntil;

    void Awake()
    {
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
        // 지금 있는 곳으로 가는 버튼은 누를 수 없게
        homeButton.interactable = worldSync.Entered && !worldSync.IsMine;
        multiverseButton.interactable = worldSync.Entered && !worldSync.InMultiverse;
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
}
