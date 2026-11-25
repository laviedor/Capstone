using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// 오른쪽 위 도구 줄의 [친구], [1:1 채팅] 버튼과 그 아래에 열리는 창 (두 버튼이 같은 창을 서로 다른 목록으로 연다)
//   [친구]: 닉네임으로 친구 요청을 보내고, 받은 요청을 [수락]/[거절]한다 (상대가 수락해야 친구가 된다)
//           친구 목록에서 [방문]으로 그 친구의 마을 구경, [삭제]로 친구 삭제. 보낸 요청은 [취소]할 수 있다
//           버튼에는 받은 친구 요청 수가 괄호로 표시된다: 친구 (1)
//   [1:1 채팅]: 대화할 친구를 고르면 그 친구와 둘이서만 주고받는 채팅. 어느 월드에 있든 접속한 모두에게 보이는
//               광장 채팅(ChatUI)과 달리 서버 DB 에 남아서, 상대가 접속해 있지 않아도 보낼 수 있고 다음에 열면 이어서 보인다
//               버튼에는 안 읽은 메시지 수가 괄호로 표시된다: 1:1 채팅 (1)
//   서버가 채팅 연결(ChatClient)로 알림을 주면 FriendApi 로 새 메시지나 친구 목록, 친구 요청을 다시 받아 온다
//   다른 플레이어를 우클릭하면 뜨는 메뉴(PlayerContextMenu)도 여기의 목록으로 관계를 보고, 요청/취소/수락/거절/1:1 채팅 열기/친구 삭제를 여기에 맡긴다
// (await 뒤의 코드는 Unity 메인 스레드에서 이어서 실행됨)
public class FriendUI : MonoBehaviour
{
    [SerializeField] private ChatClient chatClient;
    [SerializeField] private ConfirmDialog dialog;
    [SerializeField] private Button friendButton;   // [친구]
    [SerializeField] private Button dmButton;       // [1:1 채팅]
    [SerializeField] private Text dmButtonText;     // 안 읽은 메시지 수 표시
    [SerializeField] private GameObject friendPanel;
    [SerializeField] private GameObject chatPanel;  // 광장 채팅창 (같은 자리에 뜨므로 이 창을 열면 닫음)

    [Header("친구 / 대화 상대 목록")]
    [SerializeField] private GameObject listView;
    [SerializeField] private GameObject addRow;     // 닉네임 입력 칸과 [추가] 버튼 (친구 창에서만 보임)
    [SerializeField] private InputField nicknameInput;
    [SerializeField] private Button addButton;
    [SerializeField] private Text listTitleText;    // 1:1 채팅 창의 제목 (닉네임 입력 칸 자리에 보임)
    [SerializeField] private Text statusText;       // 친구 요청, 수락, 삭제 결과 안내
    [SerializeField] private Transform listContent; // 친구 줄, 친구 요청 줄이 들어갈 곳
    [SerializeField] private FriendRowUI rowTemplate;
    [SerializeField] private Text emptyText;        // 목록이 비어 있을 때의 안내

    [Header("1:1 채팅")]
    [SerializeField] private GameObject chatView;
    [SerializeField] private Text chatTitleText;
    [SerializeField] private Button backButton;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Text messagesText;
    [SerializeField] private InputField messageInput;
    [SerializeField] private Button sendButton;
    [SerializeField] private int maxLines = 100;

    private const int MaxShownCount = 99; // 버튼에 표시하는 가장 큰 수 (넘으면 99+)

    private FriendApi.Friend[] friends = new FriendApi.Friend[0];
    private FriendApi.Friend[] incoming = new FriendApi.Friend[0]; // 나에게 친구 요청을 보낸 사람들 (수락, 거절 전)
    private FriendApi.Friend[] outgoing = new FriendApi.Friend[0]; // 내가 친구 요청을 보내 둔 사람들 (상대가 수락하기 전)
    private readonly List<FriendRowUI> rows = new List<FriendRowUI>();
    private readonly Queue<string> lines = new Queue<string>();
    private readonly HashSet<long> answering = new HashSet<long>(); // 수락, 거절, 취소를 보내고 응답을 기다리는 상대 (두 번 눌러도 한 번만 처리)
    private Text friendButtonText;       // [친구] 버튼의 글자: 받은 친구 요청 수 표시
    private bool chatsMode;              // 창이 1:1 채팅 창인지 ([친구]로 열었으면 false)
    private FriendApi.Friend chatFriend; // 1:1 채팅 화면의 친구 (목록 화면이면 null)
    private long lastMessageId;          // 화면에 보인 마지막 메시지 id, 이 뒤의 메시지만 받아 온다
    private int refreshId;               // 친구 목록 요청 번호 (늦게 도착한 이전 응답은 버림)
    private bool listLoaded;             // 로그인한 뒤 목록을 한 번이라도 받았는지 (그 뒤에 새로 온 요청만 따로 안내)

    void Awake()
    {
        friendButton.gameObject.SetActive(false); // 로그인 후에 표시
        dmButton.gameObject.SetActive(false);
        friendPanel.SetActive(false);
        rowTemplate.gameObject.SetActive(false);
        friendButtonText = friendButton.GetComponentInChildren<Text>(true);
        statusText.text = "";
        messagesText.text = "";
        nicknameInput.characterLimit = FriendApi.MaxNicknameLength;
        messageInput.characterLimit = ChatMessage.MaxTextLength;

        friendButton.onClick.AddListener(() => TogglePanel(false));
        dmButton.onClick.AddListener(() => TogglePanel(true));
        addButton.onClick.AddListener(Add);
        nicknameInput.onSubmit.AddListener(_ => Add()); // Enter로 요청 보내기
        backButton.onClick.AddListener(BackToList);
        sendButton.onClick.AddListener(Send);
        messageInput.onSubmit.AddListener(_ => Send()); // Enter로 전송
    }

    void OnEnable()
    {
        LoginUI.LoggedIn += OnLoggedIn;
        chatClient.Connected += Refresh;
        chatClient.FriendsChanged += Refresh;
        chatClient.DirectMessageArrived += OnDirectMessage;
    }

    void OnDisable()
    {
        LoginUI.LoggedIn -= OnLoggedIn;
        chatClient.Connected -= Refresh;
        chatClient.FriendsChanged -= Refresh;
        chatClient.DirectMessageArrived -= OnDirectMessage;
    }

    void OnLoggedIn()
    {
        friendButton.gameObject.SetActive(true);
        dmButton.gameObject.SetActive(true);
        Refresh(); // 접속해 있지 않은 동안 온 메시지 수, 친구 요청 수를 버튼에 표시
    }

    // ---------- 다른 스크립트에서 쓰는 것 (캐릭터 우클릭 메뉴) ----------

    // 나와 그 사람의 관계
    public enum Relation
    {
        None,            // 친구가 아니고 주고받은 요청도 없음
        Friend,
        RequestSent,     // 내가 친구 요청을 보내 두었음 (상대가 수락하기 전)
        RequestReceived, // 상대가 나에게 친구 요청을 보냈음 (내가 수락, 거절하기 전)
    }

    public event Action ListChanged; // 친구 목록이나 주고받은 친구 요청을 서버에서 다시 받음

    // 서버에서 목록을 다시 받아 온다 (끝나면 ListChanged)
    public void RefreshList() => Refresh();

    // 그 사람과의 관계 (지금 가진 목록 기준). 유저 id 를 알면 id 로, 모르면(0) 닉네임으로 찾는다
    public Relation RelationWith(long userId, string nickname)
    {
        if (FindIn(friends, userId, nickname) != null) return Relation.Friend;
        if (FindIn(incoming, userId, nickname) != null) return Relation.RequestReceived;
        if (FindIn(outgoing, userId, nickname) != null) return Relation.RequestSent;
        return Relation.None;
    }

    static FriendApi.Friend FindIn(FriendApi.Friend[] list, long userId, string nickname)
    {
        return Array.Find(list, f => userId > 0 ? f.id == userId : f.nickname == nickname);
    }

    // 닉네임으로 친구 요청 보내기. 창이 닫혀 있을 수 있으므로 결과는 화면 아래 안내로 알린다
    public async void RequestFriend(string nickname)
    {
        if (string.IsNullOrEmpty(nickname)) return;

        var result = await FriendApi.Add(nickname);
        if (this == null || LoggedOut(result.Status)) return;

        string text = AddResultText(result, out bool changed);
        statusText.text = text;
        Toast.Show(text);
        if (changed) Refresh();
    }

    // 그 사람이 보낸 친구 요청 수락. 결과는 화면 아래 안내로 알린다
    public void AcceptRequestFrom(long userId, string nickname)
    {
        FriendApi.Friend from = FindIn(incoming, userId, nickname);
        if (from == null)
        {
            Toast.Show("받은 친구 요청이 없습니다.");
            Refresh();
            return;
        }
        Accept(from, true);
    }

    // 그 사람이 보낸 친구 요청 거절. 결과는 화면 아래 안내로 알린다
    public void RejectRequestFrom(long userId, string nickname)
    {
        FriendApi.Friend from = FindIn(incoming, userId, nickname);
        if (from == null)
        {
            Toast.Show("받은 친구 요청이 없습니다.");
            Refresh();
            return;
        }
        Reject(from, true);
    }

    // 그 사람에게 보내 둔 친구 요청 취소. 결과는 화면 아래 안내로 알린다
    public void CancelRequestTo(long userId, string nickname)
    {
        FriendApi.Friend to = FindIn(outgoing, userId, nickname);
        if (to == null)
        {
            Toast.Show("보낸 친구 요청이 없습니다.");
            Refresh();
            return;
        }
        Cancel(to, true);
    }

    // 그 친구와의 1:1 채팅 화면을 연다
    public void OpenChatWith(long userId, string nickname)
    {
        FriendApi.Friend friend = FindIn(friends, userId, nickname);
        if (friend == null)
        {
            Toast.Show("친구가 아니어서 1:1 채팅을 할 수 없습니다.");
            Refresh();
            return;
        }

        friendPanel.SetActive(true);
        if (chatPanel != null) chatPanel.SetActive(false);
        OpenChat(friend);
        Refresh();
    }

    // 그 친구를 삭제할지 묻고 (친구 창의 [삭제]와 같은 확인창) 삭제한다. 결과는 화면 아래 안내로 알린다
    public void RemoveFriend(long userId, string nickname)
    {
        FriendApi.Friend friend = FindIn(friends, userId, nickname);
        if (friend == null)
        {
            Toast.Show("이미 친구가 아닙니다.");
            Refresh();
            return;
        }
        AskRemove(friend, true);
    }

    // chats: [1:1 채팅] 버튼이면 true, [친구] 버튼이면 false. 같은 버튼을 다시 누르면 닫힘
    void TogglePanel(bool chats)
    {
        if (friendPanel.activeSelf && chatsMode == chats)
        {
            friendPanel.SetActive(false);
            return;
        }

        chatsMode = chats;
        friendPanel.SetActive(true);
        if (chatPanel != null) chatPanel.SetActive(false);

        if (chats && chatFriend != null)
        {
            // 보던 1:1 채팅을 이어서 보여 줌
            listView.SetActive(false);
            chatView.SetActive(true);
            LoadMessages();
            messageInput.ActivateInputField();
        }
        else
        {
            ShowList("");
        }
        Refresh();
    }

    // ---------- 친구 / 대화 상대 목록 ----------

    void ShowList(string message)
    {
        chatFriend = null;
        chatView.SetActive(false);
        listView.SetActive(true);
        addRow.SetActive(!chatsMode);
        listTitleText.gameObject.SetActive(chatsMode);
        statusText.text = message;
        BuildRows();
    }

    void BackToList()
    {
        ShowList("");
        Refresh();
    }

    // 서버에서 친구 목록(접속 여부, 안 읽은 메시지 수)과 친구 요청을 다시 받아 옴
    async void Refresh()
    {
        if (string.IsNullOrEmpty(LoginUI.Username)) return; // 로그인 전

        int my = ++refreshId;
        var result = await FriendApi.List();
        if (this == null || my != refreshId) return;
        if (LoggedOut(result.Status)) return;
        if (result.Status != 200 || result.Body == null) return; // 서버에 연결하지 못함: 보이던 목록을 그대로 둠

        FriendApi.Friend[] oldIncoming = incoming;
        FriendApi.Friend[] oldOutgoing = outgoing;
        // 접속 중인 친구를 위로 (같은 상태끼리는 서버가 보낸 이름순)
        friends = (result.Body.friends ?? new FriendApi.Friend[0]).OrderByDescending(f => f.online).ToArray();
        incoming = result.Body.incoming ?? new FriendApi.Friend[0];
        outgoing = result.Body.outgoing ?? new FriendApi.Friend[0];
        Announce(oldIncoming, oldOutgoing);
        listLoaded = true;

        if (chatFriend != null)
        {
            long id = chatFriend.id;
            FriendApi.Friend current = Array.Find(friends, f => f.id == id);
            if (current != null)
            {
                chatFriend = current;
            }
            else
            {
                // 상대가 친구를 삭제했거나 탈퇴함
                string name = chatFriend.nickname;
                ShowList($"{name}님과 더 이상 친구가 아닙니다.");
            }
        }

        BuildRows();
        UpdateButtonText();
        ListChanged?.Invoke();
    }

    // 목록에서 달라진 것 중 알려 줄 것을 화면 위에 잠깐 띄움: 새로 받은 친구 요청, 내가 보낸 요청을 상대가 수락함
    void Announce(FriendApi.Friend[] oldIncoming, FriendApi.Friend[] oldOutgoing)
    {
        if (!listLoaded)
        {
            // 로그인 직후: 접속해 있지 않은 동안 받아 둔 요청
            if (incoming.Length > 0)
                Toast.Show($"받은 친구 요청이 {incoming.Length}건 있습니다. [친구]에서 확인해 주세요.");
            return;
        }

        FriendApi.Friend[] added = incoming.Where(r => !oldIncoming.Any(o => o.id == r.id)).ToArray();
        if (added.Length == 1) Toast.Show($"{added[0].nickname}님이 친구 요청을 보냈습니다.");
        else if (added.Length > 1) Toast.Show($"친구 요청 {added.Length}건을 받았습니다.");

        foreach (FriendApi.Friend sent in oldOutgoing)
        {
            if (!friends.Any(f => f.id == sent.id)) continue;
            Toast.Show($"{sent.nickname}님이 친구 요청을 수락했습니다.");
            statusText.text = $"{sent.nickname}님과 친구가 되었습니다."; // "요청을 보냈습니다" 안내가 남아 있지 않게
        }
    }

    // 친구 창: 받은 요청([수락], [거절]) - 친구([방문], [삭제]) - 보낸 요청([취소]) 순서. 1:1 채팅 창: 친구만
    void BuildRows()
    {
        foreach (FriendRowUI row in rows)
            Destroy(row.gameObject);
        rows.Clear();

        if (!chatsMode)
        {
            foreach (FriendApi.Friend from in incoming)
                AddRow().SetIncoming(from, () => Accept(from), () => Reject(from));
        }

        foreach (FriendApi.Friend friend in friends)
            AddRow().Set(friend, !chatsMode, () => OpenChat(friend), () => Visit(friend), () => AskRemove(friend));

        if (!chatsMode)
        {
            foreach (FriendApi.Friend to in outgoing)
                AddRow().SetOutgoing(to, () => Cancel(to));
        }

        emptyText.gameObject.SetActive(rows.Count == 0);
        emptyText.text = chatsMode
            ? "대화할 친구가 없습니다.\n[친구]에서 먼저 친구를 추가해 주세요."
            : "아직 친구가 없습니다.\n닉네임으로 친구 요청을 보내 보세요.";
    }

    FriendRowUI AddRow()
    {
        FriendRowUI row = Instantiate(rowTemplate, listContent);
        row.gameObject.SetActive(true);
        rows.Add(row);
        return row;
    }

    // 닉네임으로 친구 요청 보내기 (상대가 수락하면 친구가 된다)
    async void Add()
    {
        string nickname = nicknameInput.text.Trim();
        if (nickname.Length == 0 || !addButton.interactable) return;

        addButton.interactable = false;
        statusText.text = "친구 요청을 보내는 중...";
        var result = await FriendApi.Add(nickname);
        if (this == null) return;
        addButton.interactable = true;
        if (LoggedOut(result.Status)) return;

        statusText.text = AddResultText(result, out bool changed);
        if (!changed) return;

        nicknameInput.text = "";
        Refresh();
    }

    // 친구 요청을 보낸 결과 안내. changed: 요청이 보내졌거나 바로 친구가 되어 목록을 다시 받아야 하는지
    static string AddResultText(AuthApi.Result<FriendApi.Response> result, out bool changed)
    {
        changed = false;
        switch (result.Status == 0 ? null : result.Body?.status)
        {
            case "requested":
                changed = true;
                return $"{result.Body.friend.nickname}님에게 친구 요청을 보냈습니다.";
            case "ok":
                // 상대가 먼저 나에게 요청을 보내 둔 상태였음
                changed = true;
                return $"{result.Body.friend.nickname}님과 친구가 되었습니다.";
            case "not_found":
                return "그런 닉네임의 유저가 없습니다.";
            case "self":
                return "자기 자신에게는 요청할 수 없습니다.";
            case "already":
                return "이미 친구입니다.";
            case "pending":
                return "이미 친구 요청을 보낸 상대입니다.";
            case "full":
                return "친구를 더 추가할 수 없습니다.";
            case "too_many":
                return "친구 요청을 더 보낼 수 없습니다.";
            case "ambiguous":
                return "같은 닉네임의 유저가 여러 명이라 요청할 수 없습니다.";
            default:
                return ErrorText(result.Status, "친구 요청을 보내지 못했습니다.");
        }
    }

    // ---------- 친구 요청 ----------

    // 받은 친구 요청 수락: 서로 친구가 된다. announce: 결과를 화면 아래 안내로도 알릴지 (창 밖에서 수락한 경우)
    async void Accept(FriendApi.Friend from, bool announce = false)
    {
        if (!answering.Add(from.id)) return;
        var result = await FriendApi.Accept(from.id);
        answering.Remove(from.id);
        if (this == null || LoggedOut(result.Status)) return;

        string text;
        switch (result.Status == 0 ? null : result.Body?.status)
        {
            case "ok":
                text = $"{from.nickname}님과 친구가 되었습니다.";
                break;
            case "none":
                text = "이미 취소된 친구 요청입니다.";
                break;
            case "full":
                text = "친구를 더 추가할 수 없습니다.";
                break;
            default:
                text = ErrorText(result.Status, "친구 요청을 수락하지 못했습니다.");
                break;
        }
        statusText.text = text;
        if (announce) Toast.Show(text);
        Refresh();
    }

    // 받은 친구 요청 거절. announce: 결과를 화면 아래 안내로도 알릴지 (창 밖에서 거절한 경우)
    async void Reject(FriendApi.Friend from, bool announce = false)
    {
        if (!answering.Add(from.id)) return;
        var result = await FriendApi.Reject(from.id);
        answering.Remove(from.id);
        if (this == null || LoggedOut(result.Status)) return;

        string text = result.Status == 200
            ? $"{from.nickname}님의 친구 요청을 거절했습니다."
            : ErrorText(result.Status, "친구 요청을 거절하지 못했습니다.");
        statusText.text = text;
        if (announce) Toast.Show(text);
        Refresh();
    }

    // 내가 보낸 친구 요청 취소. announce: 결과를 화면 아래 안내로도 알릴지 (창 밖에서 취소한 경우)
    async void Cancel(FriendApi.Friend to, bool announce = false)
    {
        if (!answering.Add(to.id)) return;
        var result = await FriendApi.Cancel(to.id);
        answering.Remove(to.id);
        if (this == null || LoggedOut(result.Status)) return;

        string text = result.Status == 200
            ? $"{to.nickname}님에게 보낸 친구 요청을 취소했습니다."
            : ErrorText(result.Status, "친구 요청을 취소하지 못했습니다.");
        statusText.text = text;
        if (announce) Toast.Show(text);
        Refresh();
    }

    // ---------- 친구 ----------

    // 친구의 마을로 이동 (구경만 할 수 있다). 돌아올 때는 화면 위의 [내 마을]
    void Visit(FriendApi.Friend friend)
    {
        friendPanel.SetActive(false);
        WorldSync.Instance.Enter(friend.id);
    }

    // announce: 결과를 화면 아래 안내로도 알릴지 (창 밖에서 삭제한 경우)
    void AskRemove(FriendApi.Friend friend, bool announce = false)
    {
        dialog.Show($"{friend.nickname}님을\n친구에서 삭제하시겠습니까?", "삭제", () => Remove(friend, announce), "취소");
    }

    async void Remove(FriendApi.Friend friend, bool announce)
    {
        var result = await FriendApi.Remove(friend.id);
        if (this == null || LoggedOut(result.Status)) return;

        string text = result.Status == 200
            ? $"{friend.nickname}님을 친구에서 삭제했습니다."
            : ErrorText(result.Status, "친구를 삭제하지 못했습니다.");
        statusText.text = text;
        if (announce) Toast.Show(text);
        Refresh();
    }

    // ---------- 1:1 채팅 ----------

    void OpenChat(FriendApi.Friend friend)
    {
        chatsMode = true; // 친구 창에서 이름을 눌러 들어와도 여기부터는 1:1 채팅 창
        chatFriend = friend;
        lastMessageId = 0;
        lines.Clear();
        messagesText.text = "";
        chatTitleText.text = friend.nickname + "님과의 대화";
        messageInput.text = "";

        listView.SetActive(false);
        chatView.SetActive(true);
        messageInput.ActivateInputField();
        LoadMessages();
    }

    // 1:1 채팅 화면의 친구와 주고받은 메시지 중 아직 화면에 없는 것을 받아 와 보여 줌 (처음에는 최근 메시지)
    async void LoadMessages()
    {
        long friendId = chatFriend.id;
        var result = await FriendApi.Messages(friendId, lastMessageId);
        if (this == null || LoggedOut(result.Status)) return;
        if (chatFriend == null || chatFriend.id != friendId) return; // 그 사이 다른 화면으로 바뀜

        if (result.Status != 200 || result.Body == null)
        {
            if (result.Body?.status == "not_friend") Refresh(); // 친구가 아니게 됨: 목록을 다시 받으면 목록 화면으로 돌아감
            else AddLine(ErrorText(result.Status, "메시지를 불러오지 못했습니다."));
            return;
        }

        foreach (FriendApi.Message m in result.Body.messages ?? new FriendApi.Message[0])
        {
            if (m.id <= lastMessageId) continue; // 동시에 보낸 다른 요청의 응답에서 이미 받음
            lastMessageId = m.id;
            AddLine((m.sender == LoginUI.UserId ? "나" : chatFriend.nickname) + ": " + m.text);
        }

        chatFriend.unread = 0; // 서버에서도 읽음 처리됨
        UpdateButtonText();
    }

    async void Send()
    {
        string text = messageInput.text.Trim();
        if (text.Length == 0 || chatFriend == null) return;

        long friendId = chatFriend.id;
        StartCoroutine(ClearInputNextFrame());
        var result = await FriendApi.SendMessage(friendId, text);
        if (this == null || LoggedOut(result.Status)) return;
        if (chatFriend == null || chatFriend.id != friendId) return;

        if (result.Status == 200)
        {
            LoadMessages(); // 보낸 메시지도 서버에 저장된 순서대로 받아서 보여 줌
            return;
        }

        if (result.Body?.status == "not_friend") AddLine("친구가 아니어서 보낼 수 없습니다.");
        else AddLine(ErrorText(result.Status, "메시지를 보내지 못했습니다."));
        if (messageInput.text.Length == 0) messageInput.text = text; // 다시 보낼 수 있게 되돌려 둠
    }

    // 한글 입력(IME) 조합 중에 Enter를 누르면 조합 중이던 글자가 다시 들어올 수 있어 한 프레임 뒤에 비움
    IEnumerator ClearInputNextFrame()
    {
        yield return null;
        messageInput.text = "";
        messageInput.ActivateInputField();
    }

    // 친구(유저 id)가 1:1 채팅을 보냈다는 서버 알림
    void OnDirectMessage(long senderId)
    {
        if (Viewing(senderId))
        {
            LoadMessages();
            return;
        }

        // 서버에서 목록을 다시 받아 오기 전에 [1:1 채팅] 버튼과 목록의 숫자부터 올려 둔다 (정확한 수는 곧 서버 값으로 맞춰짐)
        FriendApi.Friend sender = Array.Find(friends, f => f.id == senderId);
        if (sender != null)
        {
            sender.unread++;
            if (listView.activeInHierarchy) BuildRows();
            UpdateButtonText();
        }
        Refresh();
    }

    // 지금 이 친구와의 1:1 채팅 화면을 보고 있는지
    bool Viewing(long friendId)
    {
        return friendPanel.activeInHierarchy && chatView.activeSelf && chatFriend != null && chatFriend.id == friendId;
    }

    void AddLine(string line)
    {
        lines.Enqueue(line);
        while (lines.Count > maxLines) lines.Dequeue();
        messagesText.text = string.Join("\n", lines);

        if (!chatView.activeInHierarchy) return;
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
    }

    // 버튼의 괄호 안 숫자: [1:1 채팅] 은 안 읽은 메시지 수 (지금 보고 있는 대화는 빼고), [친구] 는 받은 친구 요청 수
    void UpdateButtonText()
    {
        int unread = 0;
        foreach (FriendApi.Friend friend in friends)
        {
            if (!Viewing(friend.id)) unread += friend.unread;
        }
        dmButtonText.text = "1:1 채팅" + CountText(unread);
        friendButtonText.text = "친구" + CountText(incoming.Length);
    }

    // 0 이면 표시하지 않고, 아니면 " (3)" (너무 크면 " (99+)")
    static string CountText(int count)
    {
        if (count <= 0) return "";
        return count > MaxShownCount ? $" ({MaxShownCount}+)" : $" ({count})";
    }

    // 로그인이 풀렸으면(다른 곳에서 같은 계정으로 로그인했거나 토큰이 만료됨) 로그인 화면으로 돌아가고 true
    static bool LoggedOut(int status)
    {
        if (status != 401) return false;
        LoginUI.ForceLogout(LoginUI.SessionEnded);
        return true;
    }

    static string ErrorText(int status, string message)
    {
        if (status == 0) return "서버에 연결할 수 없습니다.";
        return message;
    }
}
