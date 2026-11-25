using System;
using UnityEngine;

// 서버와 UDP 로 주고받는 메시지 (JSON)
//   클라 -> 서버  pos   { id: 세션ID, name, w: 지금 있는 월드 id, zn: 그 안의 구역, x, y, z, n: 보낸 순서 }
//   서버 -> 클라  pos   { name, x, y, z, n }   같은 월드, 같은 구역에 있는 다른 플레이어 좌표
//                 kick                      같은 사용자명이 다른 곳에서 접속해 끊김
//   UDP 는 닿지 않아도 알 수 없으므로 같은 좌표를 채팅 연결(TCP, PosMessage)로도 주고받는다. n 으로 두 길의 순서를 맞춘다
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
    public int n; // 보낸 순서 (LocalPlayerSync.Sequence). 순서를 붙이지 않는 예전 클라이언트의 것은 0

    public Vector3 Position => new Vector3(x, y, z);
}
