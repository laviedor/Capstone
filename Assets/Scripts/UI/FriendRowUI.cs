using System;
using UnityEngine;
using UnityEngine.UI;

// 친구 창 / 1:1 채팅 창 목록의 친구 한 줄
//   이름: 누르면 그 친구와 1:1 채팅 (접속 여부와 새 메시지 수 표시)
//   [방문]: 그 친구의 마을 구경, [삭제]: 친구 삭제 (둘 다 친구 창에서만 보임)
public class FriendRowUI : MonoBehaviour
{
    [SerializeField] private Button openButton;
    [SerializeField] private Text label;
    [SerializeField] private Button visitButton;
    [SerializeField] private Button removeButton;
    [SerializeField] private Color onlineColor = new Color(0.1f, 0.45f, 0.15f);   // 접속 중인 친구 이름 색
    [SerializeField] private Color offlineColor = new Color(0.45f, 0.45f, 0.45f); // 접속해 있지 않은 친구 이름 색

    // manage: 친구 창이면 true ([방문], [삭제] 표시), 1:1 채팅 창이면 false (이름만)
    public void Set(FriendApi.Friend friend, bool manage, Action onOpen, Action onVisit, Action onRemove)
    {
        string text = (friend.online ? "● " : "○ ") + friend.nickname;
        if (friend.unread > 0) text += $"  (새 메시지 {friend.unread})";
        label.text = text;
        label.color = friend.online ? onlineColor : offlineColor;

        visitButton.gameObject.SetActive(manage);
        removeButton.gameObject.SetActive(manage);
        openButton.onClick.AddListener(() => onOpen());
        visitButton.onClick.AddListener(() => onVisit());
        removeButton.onClick.AddListener(() => onRemove());
    }
}
