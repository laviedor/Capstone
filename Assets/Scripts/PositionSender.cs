using System.Globalization;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

// 캐릭터에 붙이면 위치가 바뀔 때마다 UDP로 서버에 좌표 전송 ("x,y,z")
public class PositionSender : MonoBehaviour
{
    public string serverIp = "127.0.0.1";
    public int serverPort = 7777;
    public float sendInterval = 0.05f;

    private UdpClient udp;
    private Vector3 lastSent;
    private float timer;

    void Start()
    {
        udp = new UdpClient();
        udp.Connect(serverIp, serverPort);
        lastSent = transform.position + Vector3.one;
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer < sendInterval) return;
        timer = 0f;

        Vector3 p = transform.position;
        if ((p - lastSent).sqrMagnitude < 0.0001f) return;
        lastSent = p;

        string msg = string.Format(CultureInfo.InvariantCulture, "{0},{1},{2}", p.x, p.y, p.z);
        byte[] data = Encoding.UTF8.GetBytes(msg);
        udp.Send(data, data.Length);
    }

    void OnDestroy()
    {
        udp?.Close();
    }
}
