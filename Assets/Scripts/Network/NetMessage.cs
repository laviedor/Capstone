using System;
using UnityEngine;

// 서버와 주고받는 메시지 (JSON)
//   클라 -> 서버  pos   { id: 세션ID, name, x, y, z }
//   서버 -> 클라  pos   { name, x, y, z }   다른 플레이어 좌표
//                 kick                      같은 사용자명이 다른 곳에서 접속해 끊김
[Serializable]
public class NetMessage
{
    public const string Pos = "pos";
    public const string Kick = "kick";
    public const int MaxNameLength = 32; // 서버 protocol.js의 MAX_NAME과 같게 유지

    public string t;
    public string id;
    public string name;
    public float x;
    public float y;
    public float z;

    public Vector3 Position => new Vector3(x, y, z);
}
