using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

// 채팅 서버와의 TCP 통신 (채팅, 월드/구역 이동, 건물 변경, 건물 수확, 채집, 재화, 친구/1:1 채팅 알림): 로그인하면 접속하고, 끊기면 다시 접속
//   다른 곳에서 같은 계정으로 로그인해 서버가 끊은 경우(kick)에는 다시 접속하지 않고 로그아웃한다
//   같이 있는 사람들의 위치도 이 연결로 주고받는다 (UDP 가 닿지 않아도 서로 보이도록, RemotePlayerManager / LocalPlayerSync)
//   연결이 말없이 끊긴 것을 알아차리려고 주기적으로 ping 을 보내고, 한동안 아무것도 오지 않으면 닫고 다시 접속한다
// (await 뒤의 코드는 Unity 메인 스레드에서 이어서 실행됨)
public class ChatClient : MonoBehaviour
{
    [SerializeField] private int chatPort = 7778;
    [SerializeField] private float reconnectDelay = 3f;
    [SerializeField] private float pingInterval = 5f;    // 이 간격으로 ping 을 보낸다 (서버는 20초 동안 아무것도 안 오면 끊는다)
    [SerializeField] private float receiveTimeout = 15f; // 이 시간 동안 아무것도 오지 않으면 끊긴 것으로 보고 다시 접속한다

    public event Action<ChatMessage> MessageReceived;
    public event Action<BuildingMessage> BuildingReceived; // 건물 변경 (BuildingSync 가 처리)
    public event Action<string> Notice; // 연결 상태 안내
    public event Action Connected;                  // 채팅 서버에 연결됨 (다시 연결될 때마다)
    public event Action FriendsChanged;             // 친구 목록이나 친구의 접속 상태가 바뀜 (FriendUI 가 목록을 다시 받음)
    public event Action<long> DirectMessageArrived; // 이 유저(id)가 1:1 채팅을 보냄 (내용은 FriendUI 가 FriendApi 로 받음)
    public event Action<WorldMessage> WorldEntered;         // 월드에 들어감 (WorldSync, BuildingSync 가 처리)
    public event Action<string> PlayerLeft;                 // 같은 월드에 있던 사람(사용자명)이 떠남
    public event Action<ResourceMessage> ResourcesReceived; // 내 재화 (ResourceSync 가 처리)
    public event Action<HarvestMessage> HarvestReceived;    // 내 건물의 수확 타이머 (BuildingSync 가 처리)
    public event Action<string> ServerNotice;               // 서버의 안내 (요청이 거절된 이유, 받은 보상 등)
    public event Action<ExpeditionMessage> ExpeditionReceived; // 내 채집 타이머 (GatherSync 가 처리)
    public event Action<PlayersMessage> PlayersReceived;    // 지금 같은 월드, 같은 구역에 있는 사람들 (들어갈 때마다 옴)
    public event Action<PosMessage> PositionReceived;       // 같이 있는 사람의 위치 (이 연결로 온 것)
    public event Action<LookMessage> LookReceived;          // 같이 있는 사람의 겉모습 (착용한 장비)
    public event Action<TileListMessage> TilesReceived;        // 지금 월드의 바뀐 바닥 칸 전부 (GroundTileLayer 가 처리)
    public event Action<TileMessage> TileReceived;             // 다른 사람이 바꾼 바닥 한 칸 (GroundTileLayer 가 처리)

    // 접속할 때 들어갈 월드 id. WorldSync 가 지금 있는 월드로 맞춰 두므로 연결이 끊겼다 다시 붙어도 같은 곳으로 돌아간다
    public long JoinWorld { get; set; }

    // 지금 채팅 서버에 연결되어 있는지 (끊기면 다시 붙을 때까지 false)
    public bool IsConnected => stream != null;

    private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

    private TcpClient tcp;
    private NetworkStream stream;
    private bool running;
    private bool kicked;
    private bool destroyed;
    private float lastReceived; // 이 연결에서 마지막으로 무언가 받은 시각 (Time.unscaledTime)
    private float lastPing;     // 마지막으로 ping 을 보낸 시각
    private bool pongSeen;      // 이 연결에서 pong 을 받은 적이 있는지 (ping 을 모르는 예전 서버면 응답 없음 검사를 하지 않는다)
    private readonly ChatMessage ping = new ChatMessage { t = ChatMessage.Ping };
    private readonly PosMessage position = new PosMessage();

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

    // 건물 수확 (수확량은 서버가 재화에 더해 ResourcesReceived 로, 다음 타이머는 HarvestReceived 로 알려 준다)
    public bool SendHarvest(string id) => TryWrite(new HarvestMessage { id = id });

    // 장식 아이템 1개 버리기
    public bool SendDiscard(string item) => TryWrite(new DiscardMessage { item = item });

    // 내 위치 (world, zone: 지금 있는 곳, sequence: 보낸 순서). 서버가 같은 곳에 있는 사람들에게 전한다
    // 내 겉모습(착용한 장비) 보내기. 서버가 기억해 두고 같은 곳의 사람들에게 전한다
    public bool SendLook(LookInfo look) => look != null && TryWrite(new LookSendMessage
    {
        hat = look.hat ?? "", top = look.top ?? "", bottom = look.bottom ?? "", tool = look.tool ?? ""
    });

    public bool SendPosition(Vector3 p, long world, string zone, int sequence)
    {
        position.w = world;
        position.zn = zone;
        position.x = p.x;
        position.y = p.y;
        position.z = p.z;
        position.n = sequence;
        return TryWrite(position);
    }

    // 대장간에서 도구 만들기 (만든 도구와 쓴 자원은 ResourcesReceived 로 알려 준다)
    public bool SendCraft(string item) => TryWrite(new CraftMessage { item = item });

    // 내 마을 바닥 한 칸 바꾸기 (거절되면 서버가 TilesReceived 로 바닥 전체를 다시 보낸다)
    public bool SendTile(int x, int y, string tile) => TryWrite(new TileMessage { x = x, y = y, tile = tile });

    bool TryWrite(object msg)
    {
        if (stream == null) return false;
        try
        {
            Write(msg);
            return true;
        }
        catch (Exception e) when (e is IOException || e is ObjectDisposedException || e is InvalidOperationException)
        {
            return false;
        }
    }

    // 연결 확인: 주기적으로 ping 을 보내고, 한동안 아무것도 오지 않으면 닫는다 (닫으면 Run 이 다시 접속한다)
    //   네트워크가 말없이 끊기면 TCP 는 오랫동안 알아차리지 못해서, 그동안 화면이 멈춘 채로 접속해 있는 것처럼 보인다
    void Update()
    {
        if (stream == null) return;

        float now = Time.unscaledTime;
        // 창을 끌거나 장면을 불러오느라 게임이 멈춰 있었으면, 그동안 못 읽은 것일 뿐이므로 다시 기다린다
        if (Time.unscaledDeltaTime > 2f) lastReceived = now;

        if (pongSeen && now - lastReceived > receiveTimeout)
        {
            Debug.LogWarning("채팅 서버에서 응답이 없어 연결을 닫고 다시 접속합니다.");
            stream = null; // 닫는 중에는 보내지 않는다 (읽고 있던 Run 이 오류로 끝나면서 정리하고 다시 접속한다)
            tcp.Close();
            return;
        }
        if (now - lastPing >= pingInterval)
        {
            lastPing = now;
            TryWrite(ping);
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
            tcp = new TcpClient { NoDelay = true, SendTimeout = 2000 }; // 작은 메시지를 모으지 않고 바로 보낸다 (위치)
            try
            {
                await tcp.ConnectAsync(host, chatPort);
                stream = tcp.GetStream();
                lastReceived = lastPing = Time.unscaledTime;
                pongSeen = false;
                Write(new JoinMessage { name = LoginUI.Username, token = AuthApi.Token, world = JoinWorld });
                if (noticed) Notice?.Invoke("채팅 서버에 다시 연결되었습니다.");
                wasConnected = true;
                noticed = false;
                Connected?.Invoke();

                var reader = new StreamReader(stream, Utf8);
                string line;
                while ((line = await reader.ReadLineAsync()) != null)
                    Handle(line);
            }
            catch (Exception e) when (e is SocketException || e is IOException || e is ObjectDisposedException ||
                                      e is InvalidOperationException)
            {
                // 연결 실패 또는 끊김 (응답이 없어 Update 가 닫은 경우 포함) - 아래에서 다시 시도
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
        lastReceived = Time.unscaledTime;

        ChatMessage msg;
        try
        {
            msg = JsonUtility.FromJson<ChatMessage>(line);
        }
        catch (ArgumentException)
        {
            return; // 잘못된 JSON
        }

        if (msg.t == ChatMessage.Pos)
        {
            PositionReceived?.Invoke(JsonUtility.FromJson<PosMessage>(line));
        }
        else if (msg.t == ChatMessage.Pong)
        {
            pongSeen = true;
        }
        else if (msg.t == ChatMessage.Players)
        {
            PlayersReceived?.Invoke(JsonUtility.FromJson<PlayersMessage>(line));
        }
        else if (msg.t == ChatMessage.Look)
        {
            LookReceived?.Invoke(JsonUtility.FromJson<LookMessage>(line));
        }
        else if (msg.t == ChatMessage.Chat)
        {
            MessageReceived?.Invoke(msg);
        }
        else if (msg.t == ChatMessage.Kick)
        {
            // 다른 곳에서 같은 계정으로 로그인(접속)함: 다시 접속하지 않고, 이쪽은 로그아웃해 로그인 화면으로 돌아간다
            kicked = true;
            LoginUI.ForceLogout(LoginUI.LoggedInElsewhere);
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
        else if (msg.t == ChatMessage.Harvest)
        {
            HarvestReceived?.Invoke(JsonUtility.FromJson<HarvestMessage>(line));
        }
        else if (msg.t == ChatMessage.ServerNotice)
        {
            ServerNotice?.Invoke(msg.text);
        }
        else if (msg.t == ChatMessage.Expedition)
        {
            ExpeditionReceived?.Invoke(JsonUtility.FromJson<ExpeditionMessage>(line));
        }
        else if (msg.t == ChatMessage.Tiles)
        {
            TilesReceived?.Invoke(JsonUtility.FromJson<TileListMessage>(line));
        }
        else if (msg.t == ChatMessage.Tile)
        {
            TileReceived?.Invoke(JsonUtility.FromJson<TileMessage>(line));
        }
    }

    void Write(object msg)
    {
        byte[] data = Utf8.GetBytes(JsonUtility.ToJson(msg) + "\n");
        stream.Write(data, 0, data.Length);
    }
}
