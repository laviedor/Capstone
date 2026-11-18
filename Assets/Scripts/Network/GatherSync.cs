using System.Collections.Generic;
using UnityEngine;

// 채집을 서버와 맞춤. 보상은 서버가 정해서 재화로 알려 준다 (ResourceSync): 화면에 먼저 더해진 값도 서버 값으로 맞춰진다
//   필드의 자원 노드(ResourceNode): 다 캐서 노드가 비면 서버에 알린다
//   채집 타이머(GatheringTimer): 시작과 보상 수령을 서버에 알리고, 접속하면 서버에 남아 있는 타이머를 화면에 되살린다
//     타이머가 끝나는 시각은 서버 DB 에 저장되므로 게임을 꺼도 계속 흘러가고, 다시 들어오면 이어서 보인다
//   채집 스크립트(ResourceNode, GatheringTimer)는 고치지 않고, 그 상태가 바뀌는 것을 지켜보다가 서버에 알린다
public class GatherSync : MonoBehaviour
{
    [SerializeField] private ChatClient chatClient;
    [SerializeField] private float nodeScanInterval = 0.5f; // 필드에 새로 생긴 자원 노드를 찾는 간격 (초)

    private readonly List<ResourceNode> nodes = new List<ResourceNode>();
    private readonly List<bool> wasDepleted = new List<bool>(); // nodes 와 같은 순서: 지난 프레임에 비어 있었는지
    private float sinceScan = float.MaxValue;

    private bool timerWasGathering;
    private bool timerWasComplete;
    private string pendingArea;   // 서버에 남아 있는 타이머의 장소: GatheringTimer 가 아직 없어서 화면에 되살리지 못하고 기다리는 것 (없으면 null)
    private float pendingEndTime; // 그 타이머가 끝나는 시각 (Time.time 기준)

    void OnEnable()
    {
        chatClient.ExpeditionReceived += OnExpedition;
    }

    void OnDisable()
    {
        chatClient.ExpeditionReceived -= OnExpedition;
    }

    void Update()
    {
        if (string.IsNullOrEmpty(LoginUI.Username)) return; // 로그인 전

        WatchNodes();
        WatchTimer();
    }

    // ---------- 필드의 자원 노드 ----------

    void WatchNodes()
    {
        sinceScan += Time.deltaTime;
        if (sinceScan >= nodeScanInterval)
        {
            sinceScan = 0f;
            ScanNodes();
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            ResourceNode node = nodes[i];
            if (node == null) continue; // 필드를 떠나 사라진 노드 (다음 ScanNodes 에서 빠진다)

            bool depleted = node.IsDepleted;
            if (depleted && !wasDepleted[i])
            {
                // 방금 다 캠: ResourceNode 가 화면의 재화에는 이미 더했다
                if (!chatClient.SendGather(NodeId(node)))
                    Toast.Show("서버에 연결되어 있지 않아 채집한 자원이 저장되지 않았습니다.");
            }
            wasDepleted[i] = depleted;
        }
    }

    // 지금 씬에 있는 자원 노드 목록을 다시 만듦 (이미 보던 노드의 상태는 유지)
    void ScanNodes()
    {
        var known = new Dictionary<ResourceNode, bool>();
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] != null) known[nodes[i]] = wasDepleted[i];
        }

        nodes.Clear();
        wasDepleted.Clear();
        foreach (ResourceNode node in FindObjectsByType<ResourceNode>(FindObjectsSortMode.None))
        {
            nodes.Add(node);
            wasDepleted.Add(known.TryGetValue(node, out bool depleted) ? depleted : node.IsDepleted);
        }
    }

    // 서버가 아는 노드 종류 이름: 자원 프리팹 이름 (서버 data/gathering.json 의 nodes, Tools > Export Server Data 로 내보냄)
    static string NodeId(ResourceNode node)
    {
        return node.gameObject.name.Replace("(Clone)", "").Trim();
    }

    // ---------- 채집 타이머 ----------

    void WatchTimer()
    {
        // GatheringTimer 는 인벤토리 창 안에 있어서, 창이 처음 열릴 때 만들어진다 (그 전에는 없다)
        GatheringTimer timer = GatheringTimer.Instance;
        if (timer == null) return;

        if (pendingArea != null)
        {
            // 타이머가 없을 때 서버가 알려 준 것을 이제 되살림
            string area = pendingArea;
            pendingArea = null;
            Restore(timer, area, pendingEndTime - Time.time);
        }

        bool gathering = timer.IsGathering;
        bool complete = timer.IsComplete;

        if (gathering && !timerWasGathering && !timerWasComplete)
        {
            // 방금 시작됨
            if (!chatClient.SendExpeditionStart(timer.CurrentLocation))
                Toast.Show("서버에 연결되어 있지 않아 이 채집은 보상을 받을 수 없습니다.");
        }
        else if (!complete && timerWasComplete && !gathering)
        {
            // 방금 보상을 수령함: GatheringTimer 가 화면에 더한 보상은 서버가 정한 보상으로 바뀐다
            if (!chatClient.SendExpeditionClaim())
                Toast.Show("서버에 연결되어 있지 않아 채집 보상을 받지 못했습니다.");
        }

        timerWasGathering = gathering;
        timerWasComplete = complete;
    }

    // 서버가 알려 준 내 채집 타이머 (접속했을 때, 시작/수령 요청의 결과)
    void OnExpedition(ExpeditionMessage m)
    {
        pendingArea = null;
        if (string.IsNullOrEmpty(m.area)) return; // 서버에 진행 중인 것이 없음

        GatheringTimer timer = GatheringTimer.Instance;
        if (timer == null)
        {
            // 인벤토리 창을 아직 한 번도 열지 않아 타이머가 없음: 창이 열리면 되살린다 (WatchTimer)
            pendingArea = m.area;
            pendingEndTime = Time.time + m.remaining;
            return;
        }
        Restore(timer, m.area, m.remaining);
    }

    // 서버에 남아 있는 타이머를 화면에 되살림: 다시 접속했거나, 아직 끝나지 않은 채집의 보상을 받으려 했던 경우
    void Restore(GatheringTimer timer, string area, float remaining)
    {
        if (timer.IsGathering || timer.IsComplete) return; // 화면에 이미 타이머가 있음 (방금 시작한 것의 응답 등)

        // 남은 시간이 없으면 아주 짧게 시작시켜 곧바로 [보상 수령] 상태가 되게 한다
        timer.StartGathering(area, Mathf.Max(remaining, 0.01f));

        // 서버가 이미 아는 타이머이므로, WatchTimer 가 "방금 시작됨"으로 보고 서버에 다시 알리지 않게 지금 상태를 기억해 둔다
        timerWasGathering = timer.IsGathering;
        timerWasComplete = timer.IsComplete;
    }
}
