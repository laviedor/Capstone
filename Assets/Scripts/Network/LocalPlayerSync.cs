using System;
using UnityEngine;

// 내 캐릭터 좌표를 서버로 전송 (움직이면 바로, 가만히 있어도 주기적으로). 서버가 같은 월드, 같은 구역에 있는 사람에게만 전달한다
public class LocalPlayerSync : MonoBehaviour
{
    public float sendInterval = 0.05f;   // 움직일 때 최소 전송 간격
    public float heartbeatInterval = 1f; // 가만히 있어도 이 간격마다 전송 (접속 유지용)

    private readonly NetMessage msg = new NetMessage { t = NetMessage.Pos, id = Guid.NewGuid().ToString("N") };
    private Vector3 lastSent;
    private float sinceSent = float.MaxValue; // 로그인 직후 바로 전송
    private bool kicked;

    void Start()
    {
        NetworkClient.Instance.MessageReceived += OnMessage;
    }

    void OnDestroy()
    {
        if (NetworkClient.Instance != null)
            NetworkClient.Instance.MessageReceived -= OnMessage;
    }

    void Update()
    {
        // 로그인 전이거나 끊긴 뒤, 아직 어느 월드에도 들어가지 않았을 때는 전송하지 않음
        if (kicked || string.IsNullOrEmpty(LoginUI.Username)) return;
        WorldSync world = WorldSync.Instance;
        if (world != null && !world.Entered) return;

        long currentWorld = world != null ? world.Current : WorldSync.Multiverse;
        string currentZone = world != null ? world.Zone : "";
        sinceSent += Time.deltaTime;
        Vector3 p = transform.position;
        bool moved = (p - lastSent).sqrMagnitude > 0.0001f;
        // 다른 월드나 구역에 들어간 직후에는 기다리지 않고 바로 보낸다 (서버가 그곳 사람들의 좌표를 곧바로 보내 주도록)
        bool enteredWorld = currentWorld != msg.w || currentZone != msg.zn;
        if (!enteredWorld && sinceSent < (moved ? sendInterval : heartbeatInterval)) return;

        sinceSent = 0f;
        lastSent = p;
        msg.name = LoginUI.Username;
        msg.w = currentWorld;
        msg.zn = currentZone;
        msg.x = p.x;
        msg.y = p.y;
        msg.z = p.z;
        NetworkClient.Instance.Send(msg);
    }

    // 같은 사용자명이 다른 곳에서 접속하면 서버가 kick을 보냄
    void OnMessage(NetMessage m)
    {
        if (m.t != NetMessage.Kick) return;

        kicked = true;
        Debug.LogWarning("같은 사용자명으로 다른 곳에서 접속하여 연결이 끊겼습니다.");
    }
}
