using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

// 채팅 서버와의 TCP 통신: 로그인하면 접속하고, 끊기면 다시 접속
// (await 뒤의 코드는 Unity 메인 스레드에서 이어서 실행됨)
public class ChatClient : MonoBehaviour
{
    [SerializeField] private int chatPort = 7778;
    [SerializeField] private float reconnectDelay = 3f;

    public event Action<ChatMessage> MessageReceived;
    public event Action<string> Notice; // 연결 상태 안내

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
    public bool Send(string text)
    {
        if (stream == null) return false;
        try
        {
            Write(new ChatMessage { t = ChatMessage.Chat, text = text });
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
                Write(new ChatMessage { t = ChatMessage.Join, name = LoginUI.Username });
                if (noticed) Notice?.Invoke("채팅 서버에 다시 연결되었습니다.");
                wasConnected = true;
                noticed = false;

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
    }

    void Write(ChatMessage msg)
    {
        byte[] data = Utf8.GetBytes(JsonUtility.ToJson(msg) + "\n");
        stream.Write(data, 0, data.Length);
    }
}
