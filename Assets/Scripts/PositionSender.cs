using System.Globalization;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

// 캐릭터에 붙이면 위치가 바뀔 때마다 UDP로 서버에 좌표 전송 ("사용자명,x,y,z")
public class PositionSender : MonoBehaviour
{
    public string serverIp = "168.110.121.214";
    public int serverPort = 7777;
    public float sendInterval = 0.05f;

    private UdpClient udp;
    private Vector3 lastSent;
    private float timer;
    private volatile bool kicked;
    private bool stopped;

    void Start()
    {
        udp = new UdpClient();
        udp.Connect(serverIp, serverPort);
        lastSent = transform.position + Vector3.one;
        ReceiveLoop();
    }

    // 서버에서 "kick"을 받으면 전송 중단 (같은 사용자명이 다른 곳에서 접속)
    async void ReceiveLoop()
    {
        try
        {
            while (true)
            {
                var r = await udp.ReceiveAsync();
                if (Encoding.UTF8.GetString(r.Buffer) == "kick")
                {
                    kicked = true;
                    return;
                }
            }
        }
        catch (System.Exception)
        {
            // 소켓이 닫히면 종료
        }
    }

    void Update()
    {
        if (stopped) return;
        if (kicked)
        {
            stopped = true;
            Debug.LogWarning("같은 사용자명으로 다른 곳에서 접속하여 연결이 끊겼습니다.");
            return;
        }

        // 로그인 전에는 전송하지 않음
        if (string.IsNullOrEmpty(LoginUI.Username)) return;

        timer += Time.deltaTime;
        if (timer < sendInterval) return;
        timer = 0f;

        Vector3 p = transform.position;
        if ((p - lastSent).sqrMagnitude < 0.0001f) return;
        lastSent = p;

        string msg = string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3}", LoginUI.Username, p.x, p.y, p.z);
        byte[] data = Encoding.UTF8.GetBytes(msg);
        udp.Send(data, data.Length);
    }

    void OnDestroy()
    {
        udp?.Close();
    }
}
