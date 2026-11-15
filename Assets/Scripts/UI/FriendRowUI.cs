using System;
using UnityEngine;
using UnityEngine.UI;

// 친구 창 / 1:1 채팅 창 목록의 한 줄. 같은 모양의 줄을 세 가지로 쓴다
//   친구: 이름을 누르면 그 친구와 1:1 채팅 (접속 여부와 새 메시지 수 표시). [방문] 그 친구의 마을 구경, [삭제] 친구 삭제 (둘 다 친구 창에서만 보임)
//   받은 친구 요청: [수락] 친구가 됨, [거절]
//   보낸 친구 요청: 상대가 수락하기를 기다리는 중, [취소]
public class FriendRowUI : MonoBehaviour
{
    [SerializeField] private Button openButton;
    [SerializeField] private Text label;
    [SerializeField] private Button visitButton;  // 첫 번째 버튼: [방문] 또는 [수락]
    [SerializeField] private Button removeButton; // 두 번째 버튼: [삭제], [거절] 또는 [취소]
    [SerializeField] private Color onlineColor = new Color(0.1f, 0.45f, 0.15f);   // 접속 중인 친구 이름 색
    [SerializeField] private Color offlineColor = new Color(0.45f, 0.45f, 0.45f); // 접속해 있지 않은 친구 이름 색, 보낸 요청
    [SerializeField] private Color requestColor = new Color(0.1f, 0.3f, 0.75f);   // 받은 친구 요청

    // 친구. manage: 친구 창이면 true ([방문], [삭제] 표시), 1:1 채팅 창이면 false (이름만)
    public void Set(FriendApi.Friend friend, bool manage, Action onOpen, Action onVisit, Action onRemove)
    {
        string text = (friend.online ? "● " : "○ ") + friend.nickname;
        if (friend.unread > 0) text += $"  (새 메시지 {friend.unread})";
        label.text = text;
        label.color = friend.online ? onlineColor : offlineColor;

        openButton.onClick.AddListener(() => onOpen());
        SetButton(visitButton, manage ? "방문" : null, onVisit);
        SetButton(removeButton, manage ? "삭제" : null, onRemove);
    }

    // 받은 친구 요청 (아직 친구가 아니라서 이름을 눌러도 채팅은 열리지 않는다)
    public void SetIncoming(FriendApi.Friend from, Action onAccept, Action onReject)
    {
        label.text = "[받은 요청] " + from.nickname;
        label.color = requestColor;
        SetButton(visitButton, "수락", onAccept);
        SetButton(removeButton, "거절", onReject);
    }

    // 보낸 친구 요청
    public void SetOutgoing(FriendApi.Friend to, Action onCancel)
    {
        label.text = "[보낸 요청] " + to.nickname;
        label.color = offlineColor;
        SetButton(visitButton, null, null);
        SetButton(removeButton, "취소", onCancel);
    }

    // text 가 null 이면 버튼을 숨김
    static void SetButton(Button button, string text, Action onClick)
    {
        button.gameObject.SetActive(text != null);
        if (text == null) return;

        button.GetComponentInChildren<Text>(true).text = text;
        button.onClick.AddListener(() => onClick());
    }
}
