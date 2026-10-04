using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 오른쪽 아래 채팅 버튼과 채팅창
public class ChatUI : MonoBehaviour
{
    [SerializeField] private ChatClient chatClient;
    [SerializeField] private Button chatButton;
    [SerializeField] private Text chatButtonText;
    [SerializeField] private GameObject chatPanel;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Text messagesText;
    [SerializeField] private InputField messageInput;
    [SerializeField] private Button sendButton;
    [SerializeField] private int maxLines = 50;

    private readonly Queue<string> lines = new Queue<string>();
    private int unread;

    void Awake()
    {
        chatButton.gameObject.SetActive(false); // 로그인 후에 표시
        chatPanel.SetActive(false);
        messagesText.text = "";
        messageInput.characterLimit = ChatMessage.MaxTextLength;

        chatButton.onClick.AddListener(TogglePanel);
        sendButton.onClick.AddListener(Send);
        messageInput.onSubmit.AddListener(_ => Send()); // Enter로 전송
    }

    void OnEnable()
    {
        LoginUI.LoggedIn += ShowButton;
        chatClient.MessageReceived += OnMessage;
        chatClient.Notice += AddLine;
    }

    void OnDisable()
    {
        LoginUI.LoggedIn -= ShowButton;
        chatClient.MessageReceived -= OnMessage;
        chatClient.Notice -= AddLine;
    }

    void ShowButton()
    {
        chatButton.gameObject.SetActive(true);
    }

    void TogglePanel()
    {
        bool open = !chatPanel.activeSelf;
        chatPanel.SetActive(open);
        if (!open) return;

        unread = 0;
        UpdateButtonText();
        ScrollToBottom();
        messageInput.ActivateInputField();
    }

    void Send()
    {
        string text = messageInput.text.Trim();
        if (text.Length == 0) return;

        if (chatClient.Send(text))
            StartCoroutine(ClearInputNextFrame());
        else
            AddLine("채팅 서버에 연결되어 있지 않습니다.");
    }

    // 한글 입력(IME) 조합 중에 Enter를 누르면 조합 중이던 글자가 다시 들어올 수 있어 한 프레임 뒤에 비움
    IEnumerator ClearInputNextFrame()
    {
        yield return null;
        messageInput.text = "";
        messageInput.ActivateInputField();
    }

    void OnMessage(ChatMessage m)
    {
        AddLine(m.name + ": " + m.text);
        if (!chatPanel.activeSelf)
        {
            unread++;
            UpdateButtonText();
        }
    }

    void AddLine(string line)
    {
        lines.Enqueue(line);
        while (lines.Count > maxLines) lines.Dequeue();
        messagesText.text = string.Join("\n", lines);
        ScrollToBottom();
    }

    void ScrollToBottom()
    {
        if (!chatPanel.activeInHierarchy) return;
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
    }

    void UpdateButtonText()
    {
        chatButtonText.text = unread > 0 ? $"채팅 ({unread})" : "채팅";
    }
}
