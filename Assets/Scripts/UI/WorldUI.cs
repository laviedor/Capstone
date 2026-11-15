using UnityEngine;
using UnityEngine.UI;

// 화면 위 가운데: 지금 있는 곳(멀티버스 / 내 마을 / 친구 마을)과 [내 마을], [멀티버스] 이동 버튼,
// 그 아래에 잠깐 떴다 사라지는 안내(Toast, 서버가 요청을 거절한 이유 등)
//   친구 마을은 친구 창(FriendUI)의 [방문] 으로 들어간다
public class WorldUI : MonoBehaviour
{
    [SerializeField] private WorldSync worldSync;
    [SerializeField] private ChatClient chatClient;
    [SerializeField] private GameObject bar; // 위치와 이동 버튼 (로그인 후에 표시)
    [SerializeField] private Text locationText;
    [SerializeField] private Button homeButton;
    [SerializeField] private Button multiverseButton;
    [SerializeField] private Text noticeText;
    [SerializeField] private float noticeSeconds = 4f;

    private float noticeUntil;

    void Awake()
    {
        bar.SetActive(false);
        noticeText.text = "";
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
        noticeUntil = Time.unscaledTime + noticeSeconds;
    }

    void Update()
    {
        if (noticeText.text.Length > 0 && Time.unscaledTime >= noticeUntil)
            noticeText.text = "";
    }
}
