using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

// 채팅 서버와의 TCP 통신 (채팅, 월드/구역 이동, 건물 변경, 채집, 재화, 친구/1:1 채팅 알림): 로그인하면 접속하고, 끊기면 다시 접속
// (await 뒤의 코드는 Unity 메인 스레드에서 이어서 실행됨)
public class ChatClient : MonoBehaviour
{
    [SerializeField] private int chatPort = 7778;
    [SerializeField] private float reconnectDelay = 3f;

    public event Action<ChatMessage> MessageReceived;
    public event Action<BuildingMessage> BuildingReceived; // 건물 변경 (BuildingSync 가 처리)
    public event Action<string> Notice; // 연결 상태 안내
    public event Action Connected;                  // 채팅 서버에 연결됨 (다시 연결될 때마다)
    public event Action FriendsChanged;             // 친구 목록이나 친구의 접속 상태가 바뀜 (FriendUI 가 목록을 다시 받음)
    public event Action<long> DirectMessageArrived; // 이 유저(id)가 1:1 채팅을 보냄 (내용은 FriendUI 가 FriendApi 로 받음)
    public event Action<WorldMessage> WorldEntered;         // 월드에 들어감 (WorldSync, BuildingSync 가 처리)
    public event Action<string> PlayerLeft;                 // 같은 월드에 있던 사람(사용자명)이 떠남
    public event Action<ResourceMessage> ResourcesReceived; // 내 재화 (ResourceSync 가 처리)
    public event Action<ResourceMessage> OfflineProduced;   // 접속하지 않은 동안 건물이 생산한 자원
    public event Action<string> ServerNotice;               // 서버의 안내 (요청이 거절된 이유, 받은 보상 등)
    public event Action<ExpeditionMessage> ExpeditionReceived; // 내 채집 타이머 (GatherSync 가 처리)

    // 접속할 때 들어갈 월드 id. WorldSync 가 지금 있는 월드로 맞춰 두므로 연결이 끊겼다 다시 붙어도 같은 곳으로 돌아간다
    public long JoinWorld { get; set; }

    private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

    private TcpClient tcp;
    private NetworkStream stream;
    private bool running;
    private bool kicked;
    private bool destroyed;

    void OnEnable()
    {
        LoginUI.LoggedIn += Run;
    }

    void OnDisable()
    {
        LoginUI.LoggedIn -= Run;
    }

    void OnDestroy()
    {
        destroyed = true;
        tcp?.Close();
    }

    // 연결되어 있으면 보내고 true
    public bool Send(string text) => TryWrite(new ChatMessage { t = ChatMessage.Chat, text = text });

    public bool SendBuilding(BuildingMessage msg) => TryWrite(msg);

    // 월드 이동 요청 (서버가 받아들이면 WorldEntered 가 온다)
    public bool SendEnter(long world) => TryWrite(new EnterMessage { world = world });

    // 월드 안에서 구역을 옮겼다고 알림 (같은 구역에 있는 사람끼리만 서로 보인다)
    public bool SendZone(string zone) => TryWrite(new ZoneMessage { zone = zone });

    // 채집: 필드의 자원 노드를 캠 / 채집 타이머 시작 / 보상 수령 (보상은 서버가 정해 ResourcesReceived 로 알려 준다)
    public bool SendGather(string node) => TryWrite(new GatherMessage { node = node });

    public bool SendExpeditionStart(string area) => TryWrite(new ExpeditionStartMessage { area = area });

    public bool SendExpeditionClaim() => TryWrite(new ChatMessage { t = ChatMessage.ExpeditionClaim });

    bool TryWrite(object msg)
    {
        if (stream == null) return false;
        try
        {
            Write(msg);
            return true;
        }
        catch (Exception e) when (e is IOException || e is ObjectDisposedException)
        {
            return false;
        }
    }

    async void Run()
    {
        if (running) return;
        running = true;

        string host = NetworkClient.Instance.serverIp;
        bool wasConnected = false;
        bool noticed = false; // 끊김 안내를 이미 했는지

        while (!destroyed && !kicked)
        {
            tcp = new TcpClient();
            try
            {
                await tcp.ConnectAsync(host, chatPort);
                stream = tcp.GetStream();
                Write(new JoinMessage { name = LoginUI.Username, token = AuthApi.SavedToken, world = JoinWorld });
                if (noticed) Notice?.Invoke("채팅 서버에 다시 연결되었습니다.");
                wasConnected = true;
                noticed = false;
                Connected?.Invoke();

                var reader = new StreamReader(stream, Utf8);
                string line;
                while ((line = await reader.ReadLineAsync()) != null)
                    Handle(line);
            }
            catch (Exception e) when (e is SocketException || e is IOException || e is ObjectDisposedException)
            {
                // 연결 실패 또는 끊김 - 아래에서 다시 시도
            }
            finally
            {
                stream = null;
                tcp.Close();
            }

            if (destroyed || kicked) return;
            if (!noticed)
            {
                Notice?.Invoke(wasConnected
                    ? "채팅 서버와 연결이 끊겼습니다. 다시 연결하는 중..."
                    : "채팅 서버에 연결할 수 없습니다. 다시 시도하는 중...");
                noticed = true;
            }
            await Task.Delay(TimeSpan.FromSeconds(reconnectDelay));
        }
    }

    void Handle(string line)
    {
        ChatMessage msg;
        try
        {
            msg = JsonUtility.FromJson<ChatMessage>(line);
        }
        catch (ArgumentException)
        {
            return; // 잘못된 JSON
        }

        if (msg.t == ChatMessage.Chat)
        {
            MessageReceived?.Invoke(msg);
        }
        else if (msg.t == ChatMessage.Kick)
        {
            kicked = true;
            Notice?.Invoke("같은 사용자명으로 다른 곳에서 접속하여 채팅 연결이 끊겼습니다.");
        }
        else if (BuildingMessage.IsBuildingMessage(msg.t))
        {
            BuildingReceived?.Invoke(JsonUtility.FromJson<BuildingMessage>(line));
        }
        else if (msg.t == ChatMessage.Dm)
        {
            DirectMessageArrived?.Invoke(JsonUtility.FromJson<FriendPush>(line).sender);
        }
        else if (msg.t == ChatMessage.Friends)
        {
            FriendsChanged?.Invoke();
        }
        else if (msg.t == ChatMessage.World)
        {
            WorldEntered?.Invoke(JsonUtility.FromJson<WorldMessage>(line));
        }
        else if (msg.t == ChatMessage.Left)
        {
            PlayerLeft?.Invoke(msg.name);
        }
        else if (msg.t == ChatMessage.Resources)
        {
            ResourcesReceived?.Invoke(JsonUtility.FromJson<ResourceMessage>(line));
        }
        else if (msg.t == ChatMessage.Offline)
        {
            OfflineProduced?.Invoke(JsonUtility.FromJson<ResourceMessage>(line));
        }
        else if (msg.t == ChatMessage.ServerNotice)
        {
            ServerNotice?.Invoke(msg.text);
        }
        else if (msg.t == ChatMessage.Expedition)
        {
            ExpeditionReceived?.Invoke(JsonUtility.FromJson<ExpeditionMessage>(line));
        }
    }

    void Write(object msg)
    {
        byte[] data = Utf8.GetBytes(JsonUtility.ToJson(msg) + "\n");
        stream.Write(data, 0, data.Length);
    }
}
