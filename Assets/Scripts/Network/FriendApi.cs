using System;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;

// 친구, 1:1 채팅 서버(HTTP) API (서버 friendRoutes.js 참고)
// 로그인한 뒤에만 쓴다: 이 창이 로그인한 세션 토큰(AuthApi.Token)을 같이 보내고, 토큰이 무효가 됐으면 Status 가 401
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
        public Friend[] incoming; // 나에게 친구 요청을 보낸 사람들 (id, nickname). 아직 수락, 거절하지 않은 것
        public Friend[] outgoing; // 내가 친구 요청을 보내 둔 사람들 (id, nickname). 상대가 아직 수락하지 않은 것
    }

    // 친구 요청, 수락, 1:1 채팅 응답
    [Serializable]
    public class Response
    {
        public string status;      // ok, requested(요청을 보냄), 아니면 not_found, self, already, pending, full, too_many,
                                   // ambiguous, none(그런 요청이 없음), not_friend, invalid
        public Friend friend;      // 친구 요청, 수락: 상대 (id, nickname)
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

    // 친구 목록 (접속 여부, 안 읽은 메시지 수 포함)과 주고받은 친구 요청
    public static Task<AuthApi.Result<ListResponse>> List() =>
        AuthApi.Send<ListResponse>(HttpMethod.Get, "/friends", null, AuthApi.Token);

    // 닉네임으로 친구 요청 보내기: status 가 requested 면 상대가 수락해야 친구가 된다
    //   (상대가 먼저 나에게 요청을 보내 둔 상태였으면 바로 친구가 되고 status 는 ok)
    public static Task<AuthApi.Result<Response>> Add(string nickname) =>
        AuthApi.Send<Response>(HttpMethod.Post, "/friends",
            JsonUtility.ToJson(new NicknameBody { nickname = nickname }), AuthApi.Token);

    // 받은 친구 요청 수락 (userId: 요청을 보낸 사람). 서로 친구가 된다
    public static Task<AuthApi.Result<Response>> Accept(long userId) =>
        AuthApi.Send<Response>(HttpMethod.Post, $"/friends/requests/{userId}/accept", null, AuthApi.Token);

    // 받은 친구 요청 거절 (userId: 요청을 보낸 사람), 성공하면 Status 가 200
    public static Task<AuthApi.Result<Response>> Reject(long userId) =>
        AuthApi.Send<Response>(HttpMethod.Post, $"/friends/requests/{userId}/reject", null, AuthApi.Token);

    // 내가 보낸 친구 요청 취소 (userId: 요청을 받은 사람), 성공하면 Status 가 200
    public static Task<AuthApi.Result<Response>> Cancel(long userId) =>
        AuthApi.Send<Response>(HttpMethod.Post, $"/friends/requests/{userId}/cancel", null, AuthApi.Token);

    // 친구 삭제 (서로의 목록에서 지워짐), 성공하면 Status 가 200
    public static Task<AuthApi.Result<Response>> Remove(long friendId) =>
        AuthApi.Send<Response>(HttpMethod.Delete, $"/friends/{friendId}", null, AuthApi.Token);

    // 친구와의 1:1 채팅 중 afterId 뒤의 메시지 (처음 열 때는 0: 최근 메시지), 받은 메시지는 서버에서 읽음 처리됨
    public static Task<AuthApi.Result<Response>> Messages(long friendId, long afterId) =>
        AuthApi.Send<Response>(HttpMethod.Get, $"/friends/{friendId}/messages?after={afterId}", null, AuthApi.Token);

    // 친구에게 1:1 채팅 보내기
    public static Task<AuthApi.Result<Response>> SendMessage(long friendId, string text) =>
        AuthApi.Send<Response>(HttpMethod.Post, $"/friends/{friendId}/messages",
            JsonUtility.ToJson(new TextBody { text = text }), AuthApi.Token);
}
