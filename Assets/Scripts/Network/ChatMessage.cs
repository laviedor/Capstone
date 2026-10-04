using System;

// 채팅 메시지 (TCP, 한 줄에 JSON 하나)
//   클라 -> 서버  join  { name }        접속 후 처음 한 번
//                 chat  { text }
//   서버 -> 클라  chat  { name, text }  모든 접속자에게 전달
//                 kick                  같은 사용자명이 다른 곳에서 접속해 끊김
[Serializable]
public class ChatMessage
{
    public const string Join = "join";
    public const string Chat = "chat";
    public const string Kick = "kick";
    public const int MaxTextLength = 200; // 서버 config.js의 MAX_CHAT_LENGTH와 같게 유지

    public string t;
    public string name;
    public string text;
}
