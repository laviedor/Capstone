using System;
using UnityEngine;
using UnityEngine.UI;

// 친구 창 목록의 친구 한 줄: 이름(누르면 그 친구와 1:1 채팅)과 [삭제] 버튼
public class FriendRowUI : MonoBehaviour
{
    [SerializeField] private Button openButton;
    [SerializeField] private Text label;
    [SerializeField] private Button removeButton;
    [SerializeField] private Color onlineColor = new Color(0.1f, 0.45f, 0.15f);   // 접속 중인 친구 이름 색
    [SerializeField] private Color offlineColor = new Color(0.45f, 0.45f, 0.45f); // 접속해 있지 않은 친구 이름 색

    public void Set(FriendApi.Friend friend, Action onOpen, Action onRemove)
    {
        string text = (friend.online ? "● " : "○ ") + friend.nickname;
        if (friend.unread > 0) text += $"  (새 메시지 {friend.unread})";
        label.text = text;
        label.color = friend.online ? onlineColor : offlineColor;

        openButton.onClick.AddListener(() => onOpen());
        removeButton.onClick.AddListener(() => onRemove());
    }
}
