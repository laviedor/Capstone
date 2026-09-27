using System;
using UnityEngine;

// 내 캐릭터 좌표를 서버로 전송 (움직이면 바로, 가만히 있어도 주기적으로)
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
        // 로그인 전이거나 끊긴 뒤에는 전송하지 않음
        if (kicked || string.IsNullOrEmpty(LoginUI.Username)) return;

        sinceSent += Time.deltaTime;
        Vector3 p = transform.position;
        bool moved = (p - lastSent).sqrMagnitude > 0.0001f;
        if (sinceSent < (moved ? sendInterval : heartbeatInterval)) return;

        sinceSent = 0f;
        lastSent = p;
        msg.name = LoginUI.Username;
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
