using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

// 서버와의 UDP 통신 담당: 메시지 송신, 수신한 메시지는 메인 스레드(Update)에서 전달
public class NetworkClient : MonoBehaviour
{
    public static NetworkClient Instance { get; private set; }

    public string serverIp = "168.110.121.214";
    public int serverPort = 7777;

    public event Action<NetMessage> MessageReceived;

    private UdpClient udp;
    private readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>();

    void Awake()
    {
        Instance = this;
        udp = new UdpClient();
        udp.Connect(serverIp, serverPort);
        ReceiveLoop();
    }

    public void Send(NetMessage msg)
    {
        byte[] data = Encoding.UTF8.GetBytes(JsonUtility.ToJson(msg));
        udp.Send(data, data.Length);
    }

    async void ReceiveLoop()
    {
        while (udp != null)
        {
            try
            {
                UdpReceiveResult result = await udp.ReceiveAsync();
                inbox.Enqueue(Encoding.UTF8.GetString(result.Buffer));
            }
            catch (ObjectDisposedException)
            {
                return; // 소켓 닫힘
            }
            catch (SocketException)
            {
                // 서버가 꺼져 있을 때 등 일시적인 오류 - 계속 수신
            }
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
            MessageReceived?.Invoke(msg);
        }
    }

    void OnDestroy()
    {
        udp?.Close();
        udp = null;
        if (Instance == this) Instance = null;
    }
}
