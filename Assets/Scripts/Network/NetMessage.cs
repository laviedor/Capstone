using System;
using UnityEngine;

// 서버와 주고받는 메시지 (JSON)
//   클라 -> 서버  pos   { id: 세션ID, name, w: 지금 있는 월드 id, zn: 그 안의 구역, x, y, z }
//   서버 -> 클라  pos   { name, x, y, z }   같은 월드, 같은 구역에 있는 다른 플레이어 좌표
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
    public long w; // 지금 있는 월드 id (WorldSync.Current)
    public string zn = ""; // 그 월드 안의 구역 (WorldSync.Zone: "" 마을, "field" 필드, "house" 집 안)
    public float x;
    public float y;
    public float z;

    public Vector3 Position => new Vector3(x, y, z);
}
