using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// 로그인 서버(HTTP) API, 서버 주소는 NetworkClient의 serverIp, serverPort를 같이 씀 (서버 auth.js 참고)
// 친구, 1:1 채팅 API(FriendApi)도 여기의 Send 로 같은 서버에 요청한다
// 서버가 HTTPS가 아니라서 UnityWebRequest 대신 HttpClient 사용 (UnityWebRequest는 HTTP 요청을 막음)
// (await 뒤의 코드는 Unity 메인 스레드에서 이어서 실행됨)
public static class AuthApi
{
    [Serializable]
    public class User
    {
        public long id;
        public string nickname;
    }

    // start, poll, signup, guest login 응답
    [Serializable]
    public class LoginResponse
    {
        public string loginKey; // start: 결과를 확인할 때 쓰는 키
        public string url;      // start: 브라우저로 열 Steam 로그인 주소
        public string status;   // poll, signup: pending, ok, unregistered, failed
                                // guest login: ok, unregistered, wrong_password, taken, invalid, too_many
        public string token;    // ok: 세션 토큰
        public User user;       // ok
        public string nickname; // unregistered: 가입하면 쓰일 닉네임
    }

    // 서버 응답, Status는 HTTP 상태 코드 (서버에 연결하지 못하면 0)
    public struct Result<T>
    {
        public int Status;
        public T Body;
    }

    [Serializable]
    private class LoginKeyBody
    {
        public string loginKey;
    }

    [Serializable]
    private class GuestLoginBody
    {
        public string id;
        public string password;
        public bool signup;
    }

    private const int MaxWindows = 8; // 창 번호를 나눠 주는 최대 개수 (넘는 창은 첫 번째 창과 같은 키를 씀)

    // 이 창이 차지한 창 번호를 게임이 꺼질 때까지 쥐고 있다 (같은 PC 의 다른 창이 같은 번호를 쓰지 않게)
#pragma warning disable 0414 // 값을 읽지는 않고 쥐고만 있는다
    private static Mutex windowLock;
#pragma warning restore 0414

    // 이 창의 번호: 같은 PC 에 먼저 띄운 창이 1, 그다음에 띄운 창부터 2, 3 ... (떠 있는 창이 쓰지 않는 가장 작은 번호)
    //   에디터, 가상 플레이어, 번호를 정하지 못한 환경에서는 1
    //   저장해 둔 로그인을 창마다 따로 두는 데(TokenKey)와 창을 겹치지 않게 놓는 데(DesktopWindowLayout) 쓴다
    public static int WindowNumber => Mathf.Max(1, windowNumber);
    private static int windowNumber; // TokenKey 를 정할 때 같이 정해진다 (WindowSuffix)

    // 한 PC 에서 게임 창을 여러 개 띄우면 PlayerPrefs 를 같이 쓰므로 (Windows 는 레지스트리: 다른 창이 저장한 값이 바로 보인다),
    // 창마다 다른 키에 토큰을 저장해 각자 자기 계정을 기억한다
    private static readonly string TokenKey = "auth.token" + InstanceSuffix();

    // 토큰을 저장하는 키의 뒷부분
    //   Multiplayer Play Mode 의 가상 플레이어(-vpId=mppm... 로 실행됨): 그 id
    //   빌드: 먼저 띄운 창은 "" (auth.token), 그다음 창부터 ".2", ".3" ... (떠 있는 창이 쓰지 않는 가장 작은 번호)
    private static string InstanceSuffix()
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("-vpId=")) return "." + args[i].Substring("-vpId=".Length);
            if (args[i] == "-vpId" && i + 1 < args.Length) return "." + args[i + 1];
        }

#if UNITY_EDITOR
        return ""; // 에디터는 창이 하나뿐 (가상 플레이어는 위의 vpId 로 구분)
#else
        return WindowSuffix();
#endif
    }

    // 같은 PC 에 떠 있는 게임 창이 쓰지 않는 가장 작은 창 번호를 차지하고, 그 번호의 키 뒷부분을 돌려준다
    //   번호는 이름 있는 뮤텍스로 잡는다: 그 이름을 처음 만든 창이 주인이고, 창이 꺼지면 저절로 풀린다
    private static string WindowSuffix()
    {
        try
        {
            for (int n = 1; n <= MaxWindows; n++)
            {
                var mutex = new Mutex(false, "CapStone.GameWindow." + n, out bool free);
                if (free)
                {
                    windowLock = mutex;
                    windowNumber = n;
                    return n == 1 ? "" : "." + n;
                }
                mutex.Dispose(); // 다른 창이 쓰고 있는 번호
            }
        }
        catch (Exception e)
        {
            // 이름 있는 뮤텍스를 쓸 수 없는 환경: 창을 구분하지 않고 같은 키를 쓴다
            Debug.LogWarning("창 번호를 정하지 못했습니다: " + e.Message);
        }
        return "";
    }

    private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    // // 임시: HTTP 요청을 127.0.0.1:8888 프록시로 보냄 (원래대로 하려면 new HttpClient(...) 괄호 안의 handler 를 지움)
    // private static readonly HttpClient Http = new HttpClient(new HttpClientHandler
    // {
    //     Proxy = new WebProxy("http://127.0.0.1:8888"),
    //     UseProxy = true,
    // }) { Timeout = TimeSpan.FromSeconds(10) };

    // 지금 로그인해 있는 세션 토큰. 로그인하면 정해지고(StartSession), 로그아웃해 씬을 다시 불러오면 비워진다(EndSession)
    //   서버 요청과 채팅 서버 접속에는 항상 이것을 쓴다 (FriendApi, ChatClient, LogoutUI, WithdrawUI)
    //   저장해 둔 토큰(SavedToken)을 실행 중에 다시 읽으면 안 된다: 같은 PC 에 띄운 다른 창이 그 사이 로그인해
    //   바꿔 놓은 다른 계정의 토큰일 수 있고, 그러면 그 계정의 친구 목록을 받아 오게 된다
    public static string Token { get; private set; }

    // 저장해 둔 세션 토큰 (다음 실행 때 Steam 로그인 없이 바로 로그인). 시작할 때의 자동 로그인에만 쓴다
    public static string SavedToken
    {
        get => PlayerPrefs.GetString(TokenKey, "");
        private set
        {
            if (string.IsNullOrEmpty(value)) PlayerPrefs.DeleteKey(TokenKey);
            else PlayerPrefs.SetString(TokenKey, value);
            PlayerPrefs.Save();
        }
    }

    // 로그인됨: 이 토큰으로 요청하고, 다음 실행 때 자동으로 로그인하도록 저장해 둔다
    public static void StartSession(string token)
    {
        Token = token;
        SavedToken = token;
    }

    // 로그아웃됨. forget 이 true 면 저장해 둔 토큰도 지워서 다음 실행 때 자동으로 로그인하지 않는다
    public static void EndSession(bool forget)
    {
        if (forget) Forget(Token);
        Token = null;
    }

    // 저장해 둔 토큰이 이 토큰이면 지운다 (다른 창이 그 사이 저장한 다른 계정의 토큰은 건드리지 않는다)
    public static void Forget(string token)
    {
        if (!string.IsNullOrEmpty(token) && SavedToken == token) SavedToken = null;
    }

    public static Task<Result<LoginResponse>> StartSteamLogin() =>
        Send<LoginResponse>(HttpMethod.Post, "/auth/steam/start", "{}");

    public static Task<Result<LoginResponse>> Poll(string loginKey) =>
        Send<LoginResponse>(HttpMethod.Post, "/auth/steam/poll", KeyBody(loginKey));

    public static Task<Result<LoginResponse>> SignUp(string loginKey) =>
        Send<LoginResponse>(HttpMethod.Post, "/auth/steam/signup", KeyBody(loginKey));

    // Guest 로그인 (아이디 + 비밀번호), 로그인과 회원가입을 같이 처리
    //   있는 아이디면 비밀번호가 맞아야 ok, 틀리면 wrong_password
    //   없는 아이디면 unregistered, signup 을 true 로 다시 보내면 그 아이디로 가입하고 ok
    public static Task<Result<LoginResponse>> GuestLogin(string id, string password, bool signup) =>
        Send<LoginResponse>(HttpMethod.Post, "/auth/guest/login",
            JsonUtility.ToJson(new GuestLoginBody { id = id, password = password, signup = signup }));

    public static Task<Result<User>> Me(string token) =>
        Send<User>(HttpMethod.Get, "/auth/me", null, token);

    // 회원 탈퇴, HTTP 상태 코드를 반환 (200 성공, 401 로그인 만료, 0 서버 연결 실패)
    public static async Task<int> DeleteAccount(string token) =>
        (await SendRaw(HttpMethod.Delete, "/auth/me", null, token)).status;

    // 로그아웃, 서버에서 이 토큰을 무효화 (HTTP 상태 코드 반환, 서버 연결 실패면 0)
    public static async Task<int> Logout(string token) =>
        (await SendRaw(HttpMethod.Post, "/auth/logout", null, token)).status;

    private static string KeyBody(string loginKey) => JsonUtility.ToJson(new LoginKeyBody { loginKey = loginKey });

    // 요청을 보내고 JSON 응답을 T 로 읽음 (FriendApi 도 사용)
    internal static async Task<Result<T>> Send<T>(HttpMethod method, string path, string json = null, string token = null)
    {
        var (status, text) = await SendRaw(method, path, json, token);
        T body = default;
        if (!string.IsNullOrEmpty(text))
        {
            try
            {
                body = JsonUtility.FromJson<T>(text);
            }
            catch (ArgumentException)
            {
                // JSON이 아닌 응답 (서버 오류 페이지 등)
            }
        }
        return new Result<T> { Status = status, Body = body };
    }

    private static async Task<(int status, string text)> SendRaw(HttpMethod method, string path, string json, string token)
    {
        string url = $"http://{NetworkClient.Instance.serverIp}:{NetworkClient.Instance.serverPort}{path}";
        try
        {
            using var request = new HttpRequestMessage(method, url);
            if (json != null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using HttpResponseMessage response = await Http.SendAsync(request);
            return ((int)response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        catch (Exception e) when (e is HttpRequestException || e is TaskCanceledException || e is UriFormatException)
        {
            Debug.LogWarning($"서버 요청 실패 ({method} {path}): {e.Message}");
            return (0, null); // 서버에 연결할 수 없음, 시간 초과
        }
    }
}
