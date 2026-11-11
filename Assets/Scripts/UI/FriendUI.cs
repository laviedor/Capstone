using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// 오른쪽 아래 [친구] 버튼과 친구 창
//   친구 목록: 닉네임으로 친구 추가(바로 서로 친구가 됨), 친구를 누르면 1:1 채팅, [삭제]로 친구 삭제
//   1:1 채팅: 그 친구와 둘이서만 주고받는 채팅. 접속한 모두에게 보이는 광장 채팅(ChatUI)과 달리 서버 DB 에 남아서
//             상대가 접속해 있지 않아도 보낼 수 있고, 다음에 열면 이어서 보인다
//   서버가 채팅 연결(ChatClient)로 알림을 주면 FriendApi 로 새 메시지나 친구 목록을 다시 받아 온다
// (await 뒤의 코드는 Unity 메인 스레드에서 이어서 실행됨)
public class FriendUI : MonoBehaviour
{
    [SerializeField] private ChatClient chatClient;
    [SerializeField] private ConfirmDialog dialog;
    [SerializeField] private Button friendButton;
    [SerializeField] private Text friendButtonText;
    [SerializeField] private GameObject friendPanel;
    [SerializeField] private GameObject chatPanel; // 광장 채팅창 (같은 자리에 뜨므로 친구 창을 열면 닫음)

    [Header("친구 목록")]
    [SerializeField] private GameObject listView;
    [SerializeField] private InputField nicknameInput;
    [SerializeField] private Button addButton;
    [SerializeField] private Text statusText;       // 친구 추가, 삭제 결과 안내
    [SerializeField] private Transform listContent; // 친구 줄이 들어갈 곳
    [SerializeField] private FriendRowUI rowTemplate;
    [SerializeField] private GameObject emptyText;  // 친구가 없을 때의 안내

    [Header("1:1 채팅")]
    [SerializeField] private GameObject chatView;
    [SerializeField] private Text chatTitleText;
    [SerializeField] private Button backButton;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Text messagesText;
    [SerializeField] private InputField messageInput;
    [SerializeField] private Button sendButton;
    [SerializeField] private int maxLines = 100;

    private FriendApi.Friend[] friends = new FriendApi.Friend[0];
    private readonly List<FriendRowUI> rows = new List<FriendRowUI>();
    private readonly Queue<string> lines = new Queue<string>();
    private FriendApi.Friend chatFriend; // 1:1 채팅 화면의 친구 (친구 목록 화면이면 null)
    private long lastMessageId;          // 화면에 보인 마지막 메시지 id, 이 뒤의 메시지만 받아 온다
    private int refreshId;               // 친구 목록 요청 번호 (늦게 도착한 이전 응답은 버림)

    void Awake()
    {
        friendButton.gameObject.SetActive(false); // 로그인 후에 표시
        friendPanel.SetActive(false);
        rowTemplate.gameObject.SetActive(false);
        statusText.text = "";
        messagesText.text = "";
        nicknameInput.characterLimit = FriendApi.MaxNicknameLength;
        messageInput.characterLimit = ChatMessage.MaxTextLength;

        friendButton.onClick.AddListener(TogglePanel);
        addButton.onClick.AddListener(Add);
        nicknameInput.onSubmit.AddListener(_ => Add()); // Enter로 추가
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
        Refresh(); // 접속해 있지 않은 동안 온 메시지 수를 버튼에 표시
    }

    void TogglePanel()
    {
        bool open = !friendPanel.activeSelf;
        friendPanel.SetActive(open);
        if (!open) return;

        if (chatPanel != null) chatPanel.SetActive(false);
        if (chatFriend == null)
        {
            ShowList("");
        }
        else
        {
            // 보던 1:1 채팅을 이어서 보여 줌
            LoadMessages();
            messageInput.ActivateInputField();
        }
        Refresh();
    }

    // ---------- 친구 목록 ----------

    void ShowList(string message)
    {
        chatFriend = null;
        chatView.SetActive(false);
        listView.SetActive(true);
        statusText.text = message;
    }

    void BackToList()
    {
        ShowList("");
        Refresh();
    }

    // 서버에서 친구 목록(접속 여부, 안 읽은 메시지 수)을 다시 받아 옴
    async void Refresh()
    {
        if (string.IsNullOrEmpty(LoginUI.Username)) return; // 로그인 전

        int my = ++refreshId;
        var result = await FriendApi.List();
        if (this == null || my != refreshId) return;
        if (result.Status != 200 || result.Body == null) return; // 서버에 연결하지 못함: 보이던 목록을 그대로 둠

        // 접속 중인 친구를 위로 (같은 상태끼리는 서버가 보낸 이름순)
        friends = (result.Body.friends ?? new FriendApi.Friend[0]).OrderByDescending(f => f.online).ToArray();

        if (chatFriend != null)
        {
            long id = chatFriend.id;
            FriendApi.Friend current = Array.Find(friends, f => f.id == id);
            if (current != null) chatFriend = current;
            else ShowList($"{chatFriend.nickname}님과 더 이상 친구가 아닙니다."); // 상대가 친구를 삭제했거나 탈퇴함
        }

        foreach (FriendRowUI row in rows)
            Destroy(row.gameObject);
        rows.Clear();

        foreach (FriendApi.Friend friend in friends)
        {
            FriendRowUI row = Instantiate(rowTemplate, listContent);
            row.Set(friend, () => OpenChat(friend), () => AskRemove(friend));
            row.gameObject.SetActive(true);
            rows.Add(row);
        }
        emptyText.SetActive(friends.Length == 0);
        UpdateButtonText();
    }

    async void Add()
    {
        string nickname = nicknameInput.text.Trim();
        if (nickname.Length == 0 || !addButton.interactable) return;

        addButton.interactable = false;
        statusText.text = "친구 추가 중...";
        var result = await FriendApi.Add(nickname);
        if (this == null) return;
        addButton.interactable = true;

        switch (result.Status == 0 ? null : result.Body?.status)
        {
            case "ok":
                statusText.text = $"{result.Body.friend.nickname}님과 친구가 되었습니다.";
                nicknameInput.text = "";
                Refresh();
                break;
            case "not_found":
                statusText.text = "그런 닉네임의 유저가 없습니다.";
                break;
            case "self":
                statusText.text = "자기 자신은 추가할 수 없습니다.";
                break;
            case "already":
                statusText.text = "이미 친구입니다.";
                break;
            case "full":
                statusText.text = "친구를 더 추가할 수 없습니다.";
                break;
            case "ambiguous":
                statusText.text = "같은 닉네임의 유저가 여러 명이라 추가할 수 없습니다.";
                break;
            default:
                statusText.text = ErrorText(result.Status, "친구를 추가하지 못했습니다.");
                break;
        }
    }

    void AskRemove(FriendApi.Friend friend)
    {
        dialog.Show($"{friend.nickname}님을\n친구에서 삭제하시겠습니까?", "삭제", () => Remove(friend), "취소");
    }

    async void Remove(FriendApi.Friend friend)
    {
        var result = await FriendApi.Remove(friend.id);
        if (this == null) return;

        statusText.text = result.Status == 200
            ? $"{friend.nickname}님을 친구에서 삭제했습니다."
            : ErrorText(result.Status, "친구를 삭제하지 못했습니다.");
        Refresh();
    }

    // ---------- 1:1 채팅 ----------

    void OpenChat(FriendApi.Friend friend)
    {
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
        if (this == null || chatFriend == null || chatFriend.id != friendId) return; // 그 사이 다른 화면으로 바뀜

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
        if (this == null || chatFriend == null || chatFriend.id != friendId) return;

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
        if (Viewing(senderId)) LoadMessages();
        else Refresh(); // 안 읽은 메시지 수를 버튼과 목록에 표시
    }

    // 지금 이 친구와의 1:1 채팅 화면을 보고 있는지
    bool Viewing(long friendId)
    {
        return friendPanel.activeInHierarchy && chatFriend != null && chatFriend.id == friendId;
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

    // [친구] 버튼에 안 읽은 메시지 수 표시 (지금 보고 있는 대화는 빼고)
    void UpdateButtonText()
    {
        int unread = 0;
        foreach (FriendApi.Friend friend in friends)
        {
            if (!Viewing(friend.id)) unread += friend.unread;
        }
        friendButtonText.text = unread > 0 ? $"친구 ({unread})" : "친구";
    }

    static string ErrorText(int status, string message)
    {
        if (status == 0) return "서버에 연결할 수 없습니다.";
        if (status == 401) return "로그인이 만료되었습니다. 다시 로그인해 주세요.";
        return message;
    }
}
