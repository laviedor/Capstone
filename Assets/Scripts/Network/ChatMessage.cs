using System;

// 채팅 메시지 (TCP, 한 줄에 JSON 하나, 서버 chatProtocol.js 참고)
//   클라 -> 서버  join    { name, token }  접속 후 처음 한 번 (JoinMessage)
//                 chat    { text }
//   서버 -> 클라  chat    { name, text }  모든 접속자에게 전달
//                 kick                    같은 사용자명이 다른 곳에서 접속해 끊김
//                 dm      { sender }      친구(유저 id sender)가 1:1 채팅을 보냄 (FriendPush, 내용은 FriendApi 로 받음)
//                 friends                 친구 목록이나 친구의 접속 상태가 바뀜 (FriendApi 로 목록을 다시 받음)
[Serializable]
public class ChatMessage
{
    public const string Join = "join";
    public const string Chat = "chat";
    public const string Kick = "kick";
    public const string Dm = "dm";
    public const string Friends = "friends";
    public const int MaxTextLength = 200; // 서버 config.js의 MAX_CHAT_LENGTH와 같게 유지

    public string t;
    public string name;
    public string text;
}

// 입장 메시지: token(로그인 세션 토큰)을 서버가 확인하면 이 연결로 1:1 채팅, 친구 알림도 받는다
[Serializable]
public class JoinMessage
{
    public string t = ChatMessage.Join;
    public string name;
    public string token;
}

// 1:1 채팅 알림 (dm)
[Serializable]
public class FriendPush
{
    public string t;
    public long sender; // 보낸 친구의 유저 id
}
