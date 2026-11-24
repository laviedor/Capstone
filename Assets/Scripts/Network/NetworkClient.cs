using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

// 서버와의 UDP 통신 담당: 메시지 송신, 수신한 메시지는 메인 스레드(Update)에서 전달
//   소켓이 한 번 잘못되면(시작할 때 네트워크가 없었음, 중간에 네트워크가 바뀜, 서버가 잠깐 꺼짐) 그 뒤로 좌표가 전혀
//   오가지 않을 수 있으므로, 보내다가 오류가 나거나 채팅 연결이 다시 붙으면 소켓을 새로 만든다
public class NetworkClient : MonoBehaviour
{
    public static NetworkClient Instance { get; private set; }

    public string serverIp = "168.110.121.214";
    public int serverPort = 7777;

    public event Action<NetMessage> MessageReceived;

    private const float RetrySeconds = 2f; // 소켓을 만들지 못했거나 버린 뒤 다시 만들기까지 기다리는 시간

    private UdpClient udp;
    private float retryAt; // 이 시각(Time.unscaledTime) 전에는 소켓을 다시 만들지 않는다
    private readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>();

    void Awake()
    {
        Instance = this;
        Open();
    }

    // 소켓을 (다시) 만든다. 만들지 못하면 false (잠시 뒤 Send 에서 다시 시도한다)
    bool Open()
    {
        Close();
        try
        {
            var client = new UdpClient();
            IgnoreConnectionReset(client);
            client.Connect(serverIp, serverPort);
            udp = client;
        }
        catch (Exception e) when (e is SocketException || e is ObjectDisposedException)
        {
            Debug.LogWarning("UDP 소켓을 만들지 못했습니다 (잠시 뒤 다시 시도): " + e.Message);
            retryAt = Time.unscaledTime + RetrySeconds;
            return false;
        }

        ReceiveLoop(udp);
        return true;
    }

    void Close()
    {
        UdpClient old = udp;
        udp = null; // 이 소켓의 수신 루프는 여기서 끝난다
        if (old == null) return;
        try
        {
            old.Close();
        }
        catch (SocketException)
        {
        }
    }

    // 다시 접속했을 때 (채팅 연결이 끊겼다 붙음): 네트워크가 바뀌었을 수 있으므로 소켓을 새로 만든다
    public void Reopen()
    {
        Open();
    }

    public void Send(NetMessage msg)
    {
        if (udp == null && (Time.unscaledTime < retryAt || !Open())) return;

        try
        {
            byte[] data = Encoding.UTF8.GetBytes(JsonUtility.ToJson(msg));
            udp.Send(data, data.Length);
        }
        catch (Exception e) when (e is SocketException || e is ObjectDisposedException || e is InvalidOperationException)
        {
            // 네트워크가 끊겼거나 바뀜: 이 소켓은 버리고 잠시 뒤 새로 만든다 (그동안의 좌표는 채팅 연결로 간다)
            Close();
            retryAt = Time.unscaledTime + RetrySeconds;
        }
    }

    async void ReceiveLoop(UdpClient client)
    {
        int errors = 0; // 연달아 난 오류 수
        while (client == udp)
        {
            try
            {
                UdpReceiveResult result = await client.ReceiveAsync();
                inbox.Enqueue(Encoding.UTF8.GetString(result.Buffer));
                errors = 0;
            }
            catch (ObjectDisposedException)
            {
                return; // 소켓 닫힘
            }
            catch (SocketException)
            {
                // 서버가 꺼져 있을 때 등 일시적인 오류 - 계속 수신. 오류가 이어지면 메인 스레드를 붙잡지 않도록 잠깐 쉰다
                if (++errors >= 5) await Task.Delay(200);
            }
        }
    }

    // Windows 는 보낸 UDP 가 닿지 않았다는 응답이 오면(서버가 잠깐 꺼져 있을 때) 그 소켓의 다음 수신을 오류로 끝낸다. 그 동작을 끈다
    static void IgnoreConnectionReset(UdpClient client)
    {
        const int SioUdpConnReset = unchecked((int)0x9800000C);
        try
        {
            client.Client.IOControl(SioUdpConnReset, new byte[] { 0, 0, 0, 0 }, null);
        }
        catch (Exception)
        {
            // Windows 가 아니거나 지원하지 않음: 그대로 둔다 (수신 루프가 그 오류를 넘긴다)
        }
    }

    void Update()
    {
        while (inbox.TryDequeue(out string json))
        {
            NetMessage msg;
            try
            {
                msg = JsonUtility.FromJson<NetMessage>(json);
            }
            catch (ArgumentException)
            {
                continue; // 잘못된 JSON
            }
            if (msg != null) MessageReceived?.Invoke(msg);
        }
    }

    void OnDestroy()
    {
        Close();
        if (Instance == this) Instance = null;
    }
}
