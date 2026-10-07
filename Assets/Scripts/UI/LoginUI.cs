using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// 시작 화면: Steam 계정으로 로그인
//   지난번에 로그인했으면(저장된 토큰) 바로 로그인, 아니면 [Steam으로 로그인] 버튼으로 브라우저에서 로그인
//   처음 온 Steam 계정이면 회원가입 할지 물어봄
// (await 뒤의 코드는 Unity 메인 스레드에서 이어서 실행됨)
public class LoginUI : MonoBehaviour
{
    public static string Username { get; private set; } // 로그인한 계정의 닉네임 (채팅, 머리 위 이름, 좌표 전송에 사용)

    public static event Action LoggedIn;

    [SerializeField] private GameObject loginPanel;
    [SerializeField] private Text statusText;
    [FormerlySerializedAs("confirmButton")]
    [SerializeField] private Button steamButton;
    [SerializeField] private Text steamButtonText;
    [SerializeField] private ConfirmDialog dialog;
    [SerializeField] private PlayerNameLabel playerNameLabel; // 내 캐릭터 머리 위 이름
    [SerializeField] private float pollInterval = 1.5f;       // 브라우저 로그인 결과를 확인하는 간격 (초)

    // 로그인 전까지 꺼둘 스크립트 (로그인 화면 클릭이 캐릭터 이동/건물 설치로 이어지지 않도록)
    [SerializeField] private Behaviour[] disableUntilLogin;

    private const string Welcome = "Steam 계정으로 로그인해 주세요.";
    private const string ServerDown = "서버에 연결할 수 없습니다.\n잠시 후 다시 시도해 주세요.";

    private int attempt;  // 로그인 시도 번호, 취소하거나 새로 시도하면 바뀌어 이전 시도는 멈춤
    private bool waiting; // 브라우저 로그인을 기다리는 중 (버튼이 [취소]로 바뀜)

    void Awake()
    {
        Username = null; // 탈퇴 후 씬을 다시 불러온 경우 이전 계정을 지움

        foreach (Behaviour b in disableUntilLogin)
            b.enabled = false;

        loginPanel.SetActive(true);
        steamButton.onClick.AddListener(OnSteamButton);
    }

    void Start()
    {
        if (string.IsNullOrEmpty(AuthApi.SavedToken))
            ShowIdle(Welcome);
        else
            Login(false);
    }

    void OnSteamButton()
    {
        if (waiting)
        {
            attempt++;
            ShowIdle("로그인을 취소했습니다.");
        }
        else
        {
            Login(true);
        }
    }

    // 저장된 토큰으로 로그인하고, 안 되면 steam이 true일 때 브라우저에서 Steam 로그인
    async void Login(bool steam)
    {
        int my = ++attempt;
        ShowBusy("로그인 중...");

        string token = AuthApi.SavedToken;
        if (!string.IsNullOrEmpty(token))
        {
            var me = await AuthApi.Me(token);
            if (Stale(my)) return;
            if (me.Status == 200)
            {
                Enter(me.Body);
                return;
            }
            if (me.Status == 0)
            {
                ShowIdle(ServerDown);
                return;
            }
            AuthApi.SavedToken = null; // 만료되었거나 탈퇴한 계정
        }

        if (!steam)
        {
            ShowIdle(Welcome);
            return;
        }

        var start = await AuthApi.StartSteamLogin();
        if (Stale(my)) return;
        if (start.Status != 200 || start.Body == null)
        {
            ShowIdle(start.Status == 0 ? ServerDown : "로그인을 시작하지 못했습니다.\n잠시 후 다시 시도해 주세요.");
            return;
        }

        string loginKey = start.Body.loginKey;
        Application.OpenURL(start.Body.url);
        ShowWaiting();

        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(pollInterval));
            if (Stale(my)) return;

            var poll = await AuthApi.Poll(loginKey);
            if (Stale(my)) return;
            if (poll.Status == 0) continue; // 서버 연결이 잠깐 끊김, 계속 확인

            switch (poll.Status == 200 ? poll.Body?.status : "expired")
            {
                case "pending":
                    continue;
                case "ok":
                    AuthApi.SavedToken = poll.Body.token;
                    Enter(poll.Body.user);
                    return;
                case "unregistered":
                    AskSignUp(loginKey, poll.Body.nickname);
                    return;
                case "failed":
                    ShowIdle("Steam 인증에 실패했습니다.\n다시 시도해 주세요.");
                    return;
                default:
                    ShowIdle("로그인 시간이 지났습니다.\n다시 시도해 주세요.");
                    return;
            }
        }
    }

    // 처음 온 Steam 계정: 회원가입 할지 물어봄
    void AskSignUp(string loginKey, string nickname)
    {
        ShowBusy("회원가입 여부를 선택해 주세요.");
        dialog.Show($"처음 오셨네요!\n닉네임 '{nickname}'(으)로\n회원가입 하시겠습니까?",
            "회원가입", () => SignUp(loginKey),
            "취소", () => ShowIdle("회원가입을 취소했습니다."));
    }

    async void SignUp(string loginKey)
    {
        int my = ++attempt;
        ShowBusy("회원가입 중...");

        var result = await AuthApi.SignUp(loginKey);
        if (Stale(my)) return;
        if (result.Status == 200 && result.Body?.status == "ok")
        {
            AuthApi.SavedToken = result.Body.token;
            Enter(result.Body.user);
        }
        else
        {
            ShowIdle(result.Status == 0 ? ServerDown : "회원가입하지 못했습니다.\n다시 시도해 주세요.");
        }
    }

    void Enter(AuthApi.User user)
    {
        Username = user.nickname;
        loginPanel.SetActive(false);
        playerNameLabel.SetName(Username);

        foreach (Behaviour b in disableUntilLogin)
            b.enabled = true;

        LoggedIn?.Invoke();
    }

    // 다른 시도로 바뀌었거나 씬이 바뀌어 이 화면이 사라졌으면 true
    bool Stale(int my) => this == null || my != attempt;

    // [Steam으로 로그인] 버튼을 누를 수 있는 상태
    void ShowIdle(string message)
    {
        waiting = false;
        statusText.text = message;
        steamButtonText.text = "Steam으로 로그인";
        steamButton.interactable = true;
    }

    // 브라우저에서 로그인하기를 기다리는 상태, 버튼은 [취소]
    void ShowWaiting()
    {
        waiting = true;
        statusText.text = "브라우저에서 Steam 로그인을 마친 뒤\n게임으로 돌아와 주세요.";
        steamButtonText.text = "취소";
        steamButton.interactable = true;
    }

    // 서버 응답이나 선택을 기다리는 상태, 버튼은 누를 수 없음
    void ShowBusy(string message)
    {
        waiting = false;
        statusText.text = message;
        steamButtonText.text = "Steam으로 로그인";
        steamButton.interactable = false;
    }
}
