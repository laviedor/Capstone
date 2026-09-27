using System.Collections.Generic;
using UnityEngine;

// 다른 플레이어 생성/이동/제거
public class RemotePlayerManager : MonoBehaviour
{
    [SerializeField] private RemotePlayer remotePlayerPrefab;
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
