using System;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;

// 친구, 1:1 채팅 서버(HTTP) API (서버 friendRoutes.js 참고)
// 로그인한 뒤에만 쓴다: 저장된 세션 토큰을 같이 보내고, 토큰이 만료됐으면 Status 가 401
// (await 뒤의 코드는 Unity 메인 스레드에서 이어서 실행됨)
public static class FriendApi
{
    public const int MaxNicknameLength = 20; // 서버 config.js의 MAX_NICKNAME_LENGTH와 같게 유지

    [Serializable]
    public class Friend
    {
        public long id;
        public string nickname;
        public bool online; // 지금 접속 중인지
        public int unread;  // 이 친구가 보낸 1:1 채팅 중 아직 안 읽은 개수
    }

    [Serializable]
    public class Message
    {
        public long id;
        public long sender; // 보낸 사람의 유저 id (LoginUI.UserId 와 같으면 내가 보낸 것)
        public string text;
    }

    [Serializable]
    public class ListResponse
    {
        public Friend[] friends;
    }

    // 친구 추가, 1:1 채팅 응답
    [Serializable]
    public class Response
    {
        public string status;      // ok, 아니면 not_found, self, already, full, ambiguous, not_friend, invalid
        public Friend friend;      // 친구 추가: 추가된 친구 (id, nickname)
        public Message[] messages; // 1:1 채팅 목록 (오래된 것부터)
    }

    [Serializable]
    private class NicknameBody
    {
        public string nickname;
    }

    [Serializable]
    private class TextBody
    {
        public string text;
    }

    // 친구 목록 (접속 여부, 안 읽은 메시지 수 포함)
    public static Task<AuthApi.Result<ListResponse>> List() =>
        AuthApi.Send<ListResponse>(HttpMethod.Get, "/friends", null, AuthApi.SavedToken);

    // 닉네임으로 친구 추가 (바로 서로 친구가 됨)
    public static Task<AuthApi.Result<Response>> Add(string nickname) =>
        AuthApi.Send<Response>(HttpMethod.Post, "/friends",
            JsonUtility.ToJson(new NicknameBody { nickname = nickname }), AuthApi.SavedToken);

    // 친구 삭제 (서로의 목록에서 지워짐), 성공하면 Status 가 200
    public static Task<AuthApi.Result<Response>> Remove(long friendId) =>
        AuthApi.Send<Response>(HttpMethod.Delete, $"/friends/{friendId}", null, AuthApi.SavedToken);

    // 친구와의 1:1 채팅 중 afterId 뒤의 메시지 (처음 열 때는 0: 최근 메시지), 받은 메시지는 서버에서 읽음 처리됨
    public static Task<AuthApi.Result<Response>> Messages(long friendId, long afterId) =>
        AuthApi.Send<Response>(HttpMethod.Get, $"/friends/{friendId}/messages?after={afterId}", null, AuthApi.SavedToken);

    // 친구에게 1:1 채팅 보내기
    public static Task<AuthApi.Result<Response>> SendMessage(long friendId, string text) =>
        AuthApi.Send<Response>(HttpMethod.Post, $"/friends/{friendId}/messages",
            JsonUtility.ToJson(new TextBody { text = text }), AuthApi.SavedToken);
}
