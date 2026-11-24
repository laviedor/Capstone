using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 다른 플레이어 생성/이동/제거 (서버가 같은 월드, 같은 구역에 있는 사람의 것만 보내 준다)
//   누가 같이 있는지와 그 위치는 두 길로 온다:
//     채팅 연결(TCP, ChatClient): 들어갈 때 그곳 사람들의 목록(players), 그 뒤로 위치(pos)와 떠남(left). 반드시 도착한다
//     UDP(NetworkClient): 더 자주 오는 좌표. 움직임이 부드럽지만, 가는 길이나 오는 길이 막히면 아무것도 오지 않는다
//   채팅 연결로 확인된 사람은 서버가 떠났다고 알려 줄 때까지 화면에 둔다 (UDP 가 막혀 있어도 사라지지 않는다)
//   UDP 로만 아는 사람(예전 클라이언트)은 예전처럼 한동안 소식이 없으면 지운다
//   다른 플레이어를 우클릭하면 친구 추가·요청 취소 / 요청 수락·거절 / 1:1 채팅·친구 삭제 메뉴를 띄운다 (PlayerContextMenu)
public class RemotePlayerManager : MonoBehaviour
{
    [SerializeField] private RemotePlayer remotePlayerPrefab;
    [SerializeField] private ChatClient chatClient; // 같이 있는 사람들의 목록, 위치, 떠났다는 알림을 받음
    [SerializeField] private float timeout = 5f;    // UDP 로만 아는 사람은 이 시간 동안 소식이 없으면 제거
    [SerializeField] private float pickPadding = 6f; // 우클릭으로 캐릭터를 고를 때 봐 주는 여유 (픽셀)

    private readonly Dictionary<string, RemotePlayer> players = new Dictionary<string, RemotePlayer>();
    private readonly List<string> expired = new List<string>();
    private readonly HashSet<string> present = new HashSet<string>();
    private BuildingModeManager buildingMode;

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
        if (chatClient == null) return;
        chatClient.PlayerLeft += Remove;
        chatClient.PlayersReceived += OnPlayers;
        chatClient.PositionReceived += OnPosition;
    }

    void OnDisable()
    {
        if (chatClient == null) return;
        chatClient.PlayerLeft -= Remove;
        chatClient.PlayersReceived -= OnPlayers;
        chatClient.PositionReceived -= OnPosition;
    }

    // 다른 월드나 구역으로 갔거나 접속을 끊은 사람을 바로 지움
    public void Remove(string playerName)
    {
        if (string.IsNullOrEmpty(playerName) || !players.TryGetValue(playerName, out RemotePlayer player)) return;

        Destroy(player.gameObject);
        players.Remove(playerName);
    }

    // 내가 다른 월드나 구역으로 갈 때: 있던 곳의 플레이어를 모두 지움 (들어간 곳의 사람들은 서버가 목록으로 보내 준다)
    public void Clear()
    {
        foreach (RemotePlayer player in players.Values)
            Destroy(player.gameObject);
        players.Clear();
    }

    // UDP 로 온 좌표
    void OnMessage(NetMessage m)
    {
        if (m.t != NetMessage.Pos) return;
        Place(m.name, 0, m.Position, m.n, false);
    }

    // 채팅 연결로 온 좌표
    void OnPosition(PosMessage m)
    {
        Place(m.name, m.id, new Vector3(m.x, m.y, m.z), m.n, true);
    }

    // 지금 같은 곳에 있는 사람들 (그곳에 들어갈 때마다, 다시 접속했을 때도 온다): 화면의 사람들을 이 목록에 맞춘다
    void OnPlayers(PlayersMessage m)
    {
        present.Clear();
        foreach (PlayerInfo info in m.list ?? new PlayerInfo[0])
        {
            if (string.IsNullOrEmpty(info.name)) continue;
            present.Add(info.name);
            Place(info.name, info.id, new Vector3(info.x, info.y, info.z), info.n, true);
        }

        // 채팅 연결로 알던 사람이 목록에 없으면 떠난 것이다 (UDP 로만 아는 사람은 Update 의 시간 검사로 정리된다)
        foreach (KeyValuePair<string, RemotePlayer> pair in players)
        {
            if (pair.Value.Reliable && !present.Contains(pair.Key))
                expired.Add(pair.Key);
        }
        RemoveExpired();
    }

    void Place(string playerName, long userId, Vector3 position, int sequence, bool reliable)
    {
        if (string.IsNullOrEmpty(playerName) || playerName == LoginUI.Username) return;

        if (!players.TryGetValue(playerName, out RemotePlayer player))
        {
            player = Instantiate(remotePlayerPrefab, transform);
            player.Init(playerName, position);
            players.Add(playerName, player);
        }
        player.Apply(position, sequence, userId, reliable);
    }

    void Update()
    {
        // 채팅 연결로 확인된 사람은 떠났다는 알림이 올 때까지 둔다. 다만 그 연결이 끊겨 있는 동안에는 알림도 올 수 없으므로,
        // UDP 로만 아는 사람과 같이 한동안 소식이 없으면 지운다 (다시 붙으면 서버가 목록을 보내 줘서 다시 나타난다)
        bool confirmed = chatClient != null && chatClient.IsConnected;
        foreach (KeyValuePair<string, RemotePlayer> pair in players)
        {
            if ((!pair.Value.Reliable || !confirmed) && Time.time - pair.Value.LastReceived > timeout)
                expired.Add(pair.Key);
        }
        RemoveExpired();

        OpenMenuOnRightClick();
    }

    void RemoveExpired()
    {
        foreach (string playerName in expired)
        {
            Destroy(players[playerName].gameObject);
            players.Remove(playerName);
        }
        expired.Clear();
    }

    // 다른 플레이어를 우클릭하면 그 옆에 메뉴(관계에 따라 친구 추가, 요청 취소·수락·거절, 1:1 채팅, 친구 삭제)를 띄운다
    void OpenMenuOnRightClick()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.rightButton.wasPressedThisFrame || players.Count == 0) return;
        if (string.IsNullOrEmpty(LoginUI.Username) || UIInput.IsPointerOverUI()) return;

        // 건설·철거 모드에서는 우클릭이 그 모드를 끝내는 데 쓰인다
        if (buildingMode == null) buildingMode = FindFirstObjectByType<BuildingModeManager>();
        if (buildingMode != null && buildingMode.IsBuildMode) return;

        Camera view = Camera.main;
        if (view == null) return;

        // 마우스 아래에 있는 캐릭터 중 가장 가까운 사람
        Vector2 point = mouse.position.ReadValue();
        RemotePlayer picked = null;
        float best = float.MaxValue;
        foreach (RemotePlayer player in players.Values)
        {
            Rect rect = player.ScreenRect(view, pickPadding);
            if (!rect.Contains(point)) continue;

            float distance = (rect.center - point).sqrMagnitude;
            if (distance < best)
            {
                best = distance;
                picked = player;
            }
        }

        if (picked != null) PlayerContextMenu.Show(picked, point);
    }
}
