using System.Collections.Generic;
using UnityEngine;

// 다른 플레이어 생성/이동/제거 (서버가 같은 월드에 있는 사람의 좌표만 보내 준다)
public class RemotePlayerManager : MonoBehaviour
{
    [SerializeField] private RemotePlayer remotePlayerPrefab;
    [SerializeField] private ChatClient chatClient; // 같은 월드에 있던 사람이 떠났다는 알림을 받음
    [SerializeField] private float timeout = 5f; // 이 시간 동안 소식이 없으면 제거

    private readonly Dictionary<string, RemotePlayer> players = new Dictionary<string, RemotePlayer>();
    private readonly List<string> expired = new List<string>();

    void Start()
    {
        NetworkClient.Instance.MessageReceived += OnMessage;
    }

    void OnDestroy()
    {
        if (NetworkClient.Instance != null)
            NetworkClient.Instance.MessageReceived -= OnMessage;
    }

    void OnEnable()
    {
        if (chatClient != null) chatClient.PlayerLeft += Remove;
    }

    void OnDisable()
    {
        if (chatClient != null) chatClient.PlayerLeft -= Remove;
    }

    // 다른 월드로 갔거나 접속을 끊은 사람을 바로 지움 (알림을 못 받아도 timeout 뒤에는 지워진다)
    public void Remove(string playerName)
    {
        if (string.IsNullOrEmpty(playerName) || !players.TryGetValue(playerName, out RemotePlayer player)) return;

        Destroy(player.gameObject);
        players.Remove(playerName);
    }

    // 내가 다른 월드로 갈 때: 있던 월드의 플레이어를 모두 지움
    public void Clear()
    {
        foreach (RemotePlayer player in players.Values)
            Destroy(player.gameObject);
        players.Clear();
    }

    void OnMessage(NetMessage m)
    {
        if (m.t != NetMessage.Pos || string.IsNullOrEmpty(m.name) || m.name == LoginUI.Username)
            return;

        if (players.TryGetValue(m.name, out RemotePlayer player))
        {
            player.SetTarget(m.Position);
        }
        else
        {
            player = Instantiate(remotePlayerPrefab, transform);
            player.Init(m.name, m.Position);
            players.Add(m.name, player);
        }
    }

    void Update()
    {
        foreach (KeyValuePair<string, RemotePlayer> pair in players)
        {
            if (Time.time - pair.Value.LastReceived > timeout)
                expired.Add(pair.Key);
        }

        foreach (string playerName in expired)
        {
            Destroy(players[playerName].gameObject);
            players.Remove(playerName);
        }
        expired.Clear();
    }
}
