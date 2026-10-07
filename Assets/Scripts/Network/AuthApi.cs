using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

// 로그인 서버(HTTP) API, 서버 주소는 NetworkClient의 serverIp, serverPort를 같이 씀 (서버 auth.js 참고)
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

    // start, poll, signup 응답
    [Serializable]
    public class LoginResponse
    {
        public string loginKey; // start: 결과를 확인할 때 쓰는 키
        public string url;      // start: 브라우저로 열 Steam 로그인 주소
        public string status;   // poll, signup: pending, ok, unregistered, failed
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

    private const string TokenKey = "auth.token";

    private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

    // 저장해 둔 세션 토큰 (다음 실행 때 Steam 로그인 없이 바로 로그인)
    public static string SavedToken
    {
        get => PlayerPrefs.GetString(TokenKey, "");
        set
        {
            if (string.IsNullOrEmpty(value)) PlayerPrefs.DeleteKey(TokenKey);
            else PlayerPrefs.SetString(TokenKey, value);
            PlayerPrefs.Save();
        }
    }

    public static Task<Result<LoginResponse>> StartSteamLogin() =>
        Send<LoginResponse>(HttpMethod.Post, "/auth/steam/start", "{}");

    public static Task<Result<LoginResponse>> Poll(string loginKey) =>
        Send<LoginResponse>(HttpMethod.Post, "/auth/steam/poll", KeyBody(loginKey));

    public static Task<Result<LoginResponse>> SignUp(string loginKey) =>
        Send<LoginResponse>(HttpMethod.Post, "/auth/steam/signup", KeyBody(loginKey));

    public static Task<Result<User>> Me(string token) =>
        Send<User>(HttpMethod.Get, "/auth/me", null, token);

    // 회원 탈퇴, HTTP 상태 코드를 반환 (200 성공, 401 로그인 만료, 0 서버 연결 실패)
    public static async Task<int> DeleteAccount(string token) =>
        (await SendRaw(HttpMethod.Delete, "/auth/me", null, token)).status;

    private static string KeyBody(string loginKey) => JsonUtility.ToJson(new LoginKeyBody { loginKey = loginKey });

    private static async Task<Result<T>> Send<T>(HttpMethod method, string path, string json = null, string token = null)
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
            Debug.LogWarning($"로그인 서버 요청 실패 ({method} {path}): {e.Message}");
            return (0, null); // 서버에 연결할 수 없음, 시간 초과
        }
    }
}
