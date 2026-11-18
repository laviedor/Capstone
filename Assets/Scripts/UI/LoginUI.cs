using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

// 시작 화면: Steam 계정 또는 Guest 계정(아이디 + 비밀번호)으로 로그인
//   지난번에 로그인했으면(저장된 토큰) 바로 로그인
//   [Steam으로 로그인]: 브라우저에서 로그인, 처음 온 Steam 계정이면 회원가입 할지 물어봄
//   [Guest로 로그인]: 아이디와 비밀번호를 입력. 로그인과 회원가입을 한 번에 처리한다
//     있는 아이디면 비밀번호가 맞아야 로그인되고, 없는 아이디면 회원가입 할지 물어본 뒤 그 아이디로 가입
//   한 계정은 한 곳에서만 로그인된다: 다른 곳에서 같은 계정으로 로그인하면 이쪽은 로그아웃되어 이 화면으로 돌아온다 (ForceLogout)
// (await 뒤의 코드는 Unity 메인 스레드에서 이어서 실행됨)
public class LoginUI : MonoBehaviour
{
    public static string Username { get; private set; } // 로그인한 계정의 닉네임 (채팅, 머리 위 이름, 좌표 전송에 사용)
    public static long UserId { get; private set; }     // 로그인한 계정의 유저 id (1:1 채팅에서 내가 보낸 메시지를 구분)

    public static event Action LoggedIn;

    // 강제로 로그아웃될 때의 안내
    public const string LoggedInElsewhere = "다른 곳에서 이 계정으로 로그인하여\n로그아웃되었습니다.";
    public const string SessionEnded = "다른 곳에서 로그인했거나 로그인이 만료되어\n로그아웃되었습니다.";

    // 스스로 로그아웃하거나 탈퇴하는 중 (LogoutUI, WithdrawUI 가 켠다): 그 사이 서버가 토큰을 거절해도 강제 로그아웃으로 처리하지 않는다
    public static bool Leaving { get; set; }

    // 강제로 로그아웃된 이유. 씬을 다시 불러온 뒤의 로그인 화면이 한 번 보여 주고 지운다
    private static string logoutNotice;

    [SerializeField] private GameObject loginPanel;
    [SerializeField] private Text statusText;
    [FormerlySerializedAs("confirmButton")]
    [SerializeField] private Button steamButton;
    [SerializeField] private Text steamButtonText;
    [SerializeField] private ConfirmDialog dialog;
    [SerializeField] private PlayerNameLabel playerNameLabel; // 내 캐릭터 머리 위 이름
    [SerializeField] private float pollInterval = 1.5f;       // 브라우저 로그인 결과를 확인하는 간격 (초)

    [Header("Guest 로그인")]
    [SerializeField] private Button guestButton;     // [Guest로 로그인]: 아이디, 비밀번호 입력 칸을 엶
    [SerializeField] private GameObject guestForm;   // 아이디, 비밀번호 입력 칸 (열려 있는 동안 위의 두 버튼은 숨김)
    [SerializeField] private InputField guestIdInput;
    [SerializeField] private InputField guestPasswordInput;
    [SerializeField] private Button guestSubmitButton;
    [SerializeField] private Button guestBackButton; // [뒤로]: 로그인 방법을 고르는 화면으로

    // 로그인 전까지 꺼둘 스크립트 (로그인 화면 클릭이 캐릭터 이동/건물 설치로 이어지지 않도록)
    [SerializeField] private Behaviour[] disableUntilLogin;

    private const string Welcome = "Steam 계정 또는 Guest 계정으로\n로그인해 주세요.";
    private const string ServerDown = "서버에 연결할 수 없습니다.\n잠시 후 다시 시도해 주세요.";
    private const string GuestGuide = "아이디와 비밀번호를 입력해 주세요.\n처음 쓰는 아이디면 회원가입으로 이어집니다.";

    // Guest 아이디, 비밀번호 규칙 (서버 config.js의 GUEST_ID_PATTERN, MIN/MAX_PASSWORD_LENGTH와 같게 유지)
    private static readonly Regex GuestIdPattern = new Regex("^[A-Za-z0-9_가-힣]{2,16}$");
    private const int MaxGuestIdLength = 16;
    private const int MinPasswordLength = 4;
    private const int MaxPasswordLength = 64;

    private int attempt;     // 로그인 시도 번호, 취소하거나 새로 시도하면 바뀌어 이전 시도는 멈춤
    private bool waiting;    // 브라우저 로그인을 기다리는 중 (버튼이 [취소]로 바뀜)
    private bool retrySaved; // 시작할 때 서버에 연결하지 못해 지난번 로그인을 아직 확인하지 못함

    void Awake()
    {
        Username = null; // 로그아웃, 탈퇴 후 씬을 다시 불러온 경우 이전 계정을 지움
        UserId = 0;
        Leaving = false;
        AuthApi.EndSession(false); // 쓰던 세션 토큰도 잊는다 (저장해 둔 토큰은 그대로: 아래 Start 에서 자동 로그인)

        foreach (Behaviour b in disableUntilLogin)
            b.enabled = false;

        loginPanel.SetActive(true);
        steamButton.onClick.AddListener(OnSteamButton);

        guestIdInput.characterLimit = MaxGuestIdLength;
        guestPasswordInput.characterLimit = MaxPasswordLength;
        guestButton.onClick.AddListener(() => ShowGuestForm(GuestGuide));
        guestBackButton.onClick.AddListener(() => ShowIdle(Welcome));
        guestSubmitButton.onClick.AddListener(OnGuestSubmit);
        guestIdInput.onSubmit.AddListener(_ => guestPasswordInput.ActivateInputField()); // Enter: 비밀번호 칸으로
        guestPasswordInput.onSubmit.AddListener(_ => OnGuestSubmit());                   // Enter: 로그인
    }

    void Start()
    {
        if (logoutNotice != null)
        {
            // 강제로 로그아웃된 직후: 이유를 알리고 로그인 방법을 고르게 한다 (쓰던 로그인은 ForceLogout 에서 지웠다)
            string notice = logoutNotice;
            logoutNotice = null;
            ShowIdle(notice);
            dialog.Show(notice, "확인", null);
        }
        else if (string.IsNullOrEmpty(AuthApi.SavedToken))
        {
            ShowIdle(Welcome);
        }
        else
        {
            Login(true, false); // 지난번 로그인으로 바로 들어감
        }
    }

    // 로그인이 풀렸을 때 (다른 곳에서 같은 계정으로 로그인함, 토큰 만료): 로그인 화면으로 돌아가 이유를 알려 준다
    //   ChatClient(서버가 접속을 끊으며 보낸 kick), FriendUI(서버가 토큰을 거절: 401)가 부른다
    //   이 창이 쓰던 로그인은 저장해 둔 것까지 지운다: 그래야 [Steam으로 로그인]이 같은 계정으로 다시 들어가
    //   지금 로그인해 있는 쪽을 끊는 대신, 브라우저에서 새로 로그인하게 된다
    public static void ForceLogout(string message)
    {
        if (string.IsNullOrEmpty(Username) || Leaving || logoutNotice != null) return; // 로그인 전, 스스로 나가는 중, 이미 처리 중

        logoutNotice = message;
        AuthApi.EndSession(true);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); // 로그인 화면부터 다시 시작 (연결도 모두 끊긴다)
    }

    void Update()
    {
        // Tab: 아이디 칸 <-> 비밀번호 칸
        if (!guestForm.activeInHierarchy || !guestIdInput.interactable) return;
        if (Keyboard.current == null || !Keyboard.current.tabKey.wasPressedThisFrame) return;

        (guestIdInput.isFocused ? guestPasswordInput : guestIdInput).ActivateInputField();
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
            // 브라우저에서 Steam 로그인. 시작할 때 서버에 연결하지 못해 지난번 로그인을 확인하지 못했다면 그것부터 다시 해 본다
            Login(retrySaved, true);
        }
    }

    // saved: 저장해 둔 토큰(지난번 로그인)으로 먼저 로그인해 본다
    // steam: 그걸로 들어가지 못하면 브라우저에서 Steam 로그인
    async void Login(bool saved, bool steam)
    {
        int my = ++attempt;
        ShowBusy("로그인 중...");
        retrySaved = false;

        string token = saved ? AuthApi.SavedToken : null;
        if (!string.IsNullOrEmpty(token))
        {
            var me = await AuthApi.Me(token);
            if (Stale(my)) return;
            if (me.Status == 200)
            {
                AuthApi.StartSession(token);
                Enter(me.Body);
                return;
            }
            if (me.Status == 0)
            {
                retrySaved = true; // 서버가 다시 연결되면 [Steam으로 로그인]으로 이어서 들어갈 수 있게
                ShowIdle(ServerDown);
                return;
            }
            AuthApi.Forget(token); // 만료되었거나, 다른 곳에서 로그인해 무효가 되었거나, 탈퇴한 계정
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
                    AuthApi.StartSession(poll.Body.token);
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
            AuthApi.StartSession(result.Body.token);
            Enter(result.Body.user);
        }
        else
        {
            ShowIdle(result.Status == 0 ? ServerDown : "회원가입하지 못했습니다.\n다시 시도해 주세요.");
        }
    }

    // Guest 로그인 칸의 [로그인] 버튼 (또는 비밀번호 칸에서 Enter)
    void OnGuestSubmit()
    {
        if (!guestSubmitButton.interactable) return; // 서버 응답을 기다리는 중

        string id = guestIdInput.text.Trim();
        string password = guestPasswordInput.text;
        if (!GuestIdPattern.IsMatch(id))
            ShowGuestForm("아이디는 영문, 숫자, 한글, _ 로\n2~16자여야 합니다.");
        else if (password.Length < MinPasswordLength)
            ShowGuestForm($"비밀번호는 {MinPasswordLength}자 이상이어야 합니다.");
        else
            GuestLogin(id, password, false);
    }

    // Guest 로그인: 있는 아이디면 비밀번호가 맞아야 로그인, 없는 아이디면 회원가입
    //   signup이 false면 없는 아이디일 때 가입하지 않고 먼저 가입할지 물어봄
    async void GuestLogin(string id, string password, bool signup)
    {
        int my = ++attempt;
        ShowGuestForm(signup ? "회원가입 중..." : "로그인 중...", true);

        var result = await AuthApi.GuestLogin(id, password, signup);
        if (Stale(my)) return;

        switch (result.Status == 0 ? null : result.Body?.status)
        {
            case "ok":
                AuthApi.StartSession(result.Body.token);
                Enter(result.Body.user);
                break;
            case "unregistered":
                ShowGuestForm("회원가입 여부를 선택해 주세요.", true);
                dialog.Show($"처음 보는 아이디입니다.\n'{id}'(으)로\n회원가입 하시겠습니까?",
                    "회원가입", () => GuestLogin(id, password, true),
                    "취소", () => ShowGuestForm("회원가입을 취소했습니다."));
                break;
            case "wrong_password":
                ShowGuestForm("이미 있는 아이디입니다.\n비밀번호가 일치하지 않습니다.");
                break;
            case "taken":
                ShowGuestForm("다른 사람이 쓰고 있는 이름입니다.\n다른 아이디를 입력해 주세요.");
                break;
            case "invalid":
                ShowGuestForm("아이디나 비밀번호의 형식이 맞지 않습니다.");
                break;
            case "too_many":
                ShowGuestForm("시도 횟수가 너무 많습니다.\n잠시 후 다시 시도해 주세요.");
                break;
            default:
                ShowGuestForm(result.Status == 0 ? ServerDown : "로그인하지 못했습니다.\n잠시 후 다시 시도해 주세요.");
                break;
        }
    }

    void Enter(AuthApi.User user)
    {
        Username = user.nickname;
        UserId = user.id;
        guestPasswordInput.text = "";
        loginPanel.SetActive(false);
        playerNameLabel.SetName(Username);

        foreach (Behaviour b in disableUntilLogin)
            b.enabled = true;

        LoggedIn?.Invoke();
    }

    // 다른 시도로 바뀌었거나 씬이 바뀌어 이 화면이 사라졌으면 true
    bool Stale(int my) => this == null || my != attempt;

    // 로그인 방법을 고르는 상태: [Steam으로 로그인], [Guest로 로그인] 버튼을 누를 수 있음
    void ShowIdle(string message)
    {
        waiting = false;
        ShowMenu(message, "Steam으로 로그인", true, true);
    }

    // 브라우저에서 로그인하기를 기다리는 상태, Steam 버튼은 [취소]
    void ShowWaiting()
    {
        waiting = true;
        ShowMenu("브라우저에서 Steam 로그인을 마친 뒤\n게임으로 돌아와 주세요.", "취소", true, false);
    }

    // 서버 응답이나 선택을 기다리는 상태, 버튼은 누를 수 없음
    void ShowBusy(string message)
    {
        waiting = false;
        ShowMenu(message, "Steam으로 로그인", false, false);
    }

    void ShowMenu(string message, string steamLabel, bool steamEnabled, bool guestEnabled)
    {
        statusText.text = message;
        guestForm.SetActive(false);
        steamButton.gameObject.SetActive(true);
        guestButton.gameObject.SetActive(true);
        steamButtonText.text = steamLabel;
        steamButton.interactable = steamEnabled;
        guestButton.interactable = guestEnabled;
    }

    // Guest 아이디, 비밀번호를 입력하는 상태 (busy면 서버 응답이나 선택을 기다리는 중이라 입력할 수 없음)
    void ShowGuestForm(string message, bool busy = false)
    {
        waiting = false;
        statusText.text = message;
        steamButton.gameObject.SetActive(false);
        guestButton.gameObject.SetActive(false);
        guestForm.SetActive(true);

        guestIdInput.interactable = !busy;
        guestPasswordInput.interactable = !busy;
        guestSubmitButton.interactable = !busy;
        guestBackButton.interactable = !busy;
        if (!busy)
            (guestIdInput.text.Length == 0 ? guestIdInput : guestPasswordInput).ActivateInputField();
    }
}
