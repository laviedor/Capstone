using System;
using UnityEngine;

// 내 캐릭터 좌표를 서버로 전송. 서버가 같은 월드, 같은 구역에 있는 사람에게만 전달한다
//   UDP (NetworkClient): 움직이면 바로, 가만히 있어도 주기적으로. 자주 보내서 움직임이 부드럽다
//   채팅 연결 (ChatClient, TCP): 같은 좌표를 드문드문. UDP 는 닿지 않아도 알 수 없어서, 그럴 때도 서로 보이도록 같이 보낸다
//     (다른 곳에 들어간 직후, 움직이는 동안, 멈춘 자리, 그리고 가만히 있어도 가끔)
public class LocalPlayerSync : MonoBehaviour
{
    public float sendInterval = 0.05f;   // 움직일 때 최소 전송 간격
    public float heartbeatInterval = 1f; // 가만히 있어도 이 간격마다 전송 (접속 유지용)
    public float reliableInterval = 0.2f;  // 채팅 연결로 보내는 간격 (움직일 때)
    public float reliableHeartbeat = 2f;   // 가만히 있어도 이 간격마다 채팅 연결로 전송 (놓친 것이 있어도 곧 맞춰지게)

    private readonly NetMessage msg = new NetMessage { t = NetMessage.Pos, id = Guid.NewGuid().ToString("N") };
    private ChatClient chatClient;
    private Vector3 lastSent;
    private float sinceSent = float.MaxValue; // 로그인 직후 바로 전송
    private Vector3 lastReliable;
    private float sinceReliable = float.MaxValue;
    private bool reliableNow;     // 다음 Update 에서 채팅 연결로 바로 보낸다 (서버가 그곳 사람들의 목록을 막 보내 준 뒤)
    private bool connectedBefore; // 채팅 연결이 한 번이라도 붙었는지 (다시 붙은 것인지 구별)
    private bool kicked;

    void Start()
    {
        NetworkClient.Instance.MessageReceived += OnMessage;
        chatClient = FindFirstObjectByType<ChatClient>();
        if (chatClient != null)
        {
            chatClient.PlayersReceived += OnPlayersReceived;
            chatClient.Connected += OnChatConnected;
        }
    }

    void OnDestroy()
    {
        if (NetworkClient.Instance != null)
            NetworkClient.Instance.MessageReceived -= OnMessage;
        if (chatClient != null)
        {
            chatClient.PlayersReceived -= OnPlayersReceived;
            chatClient.Connected -= OnChatConnected;
        }
    }

    // 보낸 순서: 지금 시각(ms). 다시 로그인해도 계속 커지므로, 받는 쪽이 늦게 도착한 예전 좌표를 가려낼 수 있다
    static int Sequence()
    {
        return (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % int.MaxValue) + 1;
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
        sinceReliable += Time.deltaTime;
        Vector3 p = transform.position;
        // 다른 월드나 구역에 들어간 직후에는 기다리지 않고 바로 보낸다 (서버가 그곳 사람들의 좌표를 곧바로 보내 주도록)
        bool enteredWorld = currentWorld != msg.w || currentZone != msg.zn;

        SendReliable(p, currentWorld, currentZone, enteredWorld);

        bool moved = (p - lastSent).sqrMagnitude > 0.0001f;
        if (!enteredWorld && sinceSent < (moved ? sendInterval : heartbeatInterval)) return;

        sinceSent = 0f;
        lastSent = p;
        msg.name = LoginUI.Username;
        msg.w = currentWorld;
        msg.zn = currentZone;
        msg.x = p.x;
        msg.y = p.y;
        msg.z = p.z;
        msg.n = Sequence();
        NetworkClient.Instance.Send(msg);
    }

    // 채팅 연결로 보내기: 그곳에 막 들어갔을 때, 움직이는 동안 reliableInterval 마다 (멈추면 멈춘 자리까지), 가만히 있어도 가끔
    void SendReliable(Vector3 p, long world, string zone, bool entered)
    {
        if (chatClient == null) return;

        bool moved = (p - lastReliable).sqrMagnitude > 0.0001f;
        bool due = entered || reliableNow || sinceReliable >= (moved ? reliableInterval : reliableHeartbeat);
        if (!due) return;
        if (!chatClient.SendPosition(p, world, zone, Sequence())) return; // 연결이 끊겨 있음: 붙으면 다시 보낸다

        reliableNow = false;
        sinceReliable = 0f;
        lastReliable = p;
    }

    // 서버가 지금 있는 곳의 사람들 목록을 보냈다 = 내가 그곳에 들어간 것을 서버가 처리했다: 그 뒤에 내 위치를 바로 알린다
    //   (그 전에 보낸 위치는 서버가 예전 곳의 것으로 보고 버렸을 수 있다)
    void OnPlayersReceived(PlayersMessage m)
    {
        reliableNow = true;
    }

    // 채팅 연결이 끊겼다 다시 붙음: 네트워크가 바뀌었을 수 있으므로 UDP 소켓도 새로 만든다
    void OnChatConnected()
    {
        if (connectedBefore && NetworkClient.Instance != null) NetworkClient.Instance.Reopen();
        connectedBefore = true;
        sinceSent = float.MaxValue; // 다음 Update 에서 UDP 로도 바로 보낸다
    }

    // 같은 사용자명이 다른 곳에서 접속하면 서버가 kick을 보냄
    void OnMessage(NetMessage m)
    {
        if (m.t != NetMessage.Kick) return;

        kicked = true;
        Debug.LogWarning("같은 사용자명으로 다른 곳에서 접속하여 연결이 끊겼습니다.");
    }
}
