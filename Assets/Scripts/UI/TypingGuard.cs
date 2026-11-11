using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 입력 칸(아이디, 비밀번호, 채팅, 친구 닉네임)에 글자를 치는 동안 키보드 단축키를 쓰는 스크립트를 꺼 둠
// (B: 건설 모드, O: 건물 목록, F: 상호작용 등이 글자를 칠 때 같이 눌리지 않도록)
public class TypingGuard : MonoBehaviour
{
    [SerializeField] private Behaviour[] hotkeyScripts; // 글자를 치는 동안 꺼 둘 스크립트

    private bool[] wasEnabled; // 끄기 전에 켜져 있었는지 (원래 꺼져 있던 것은 다시 켜지 않음)
    private bool typing;

    // 입력 칸에 커서가 있어 글자를 치는 중인지
    public static bool IsTyping()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null || eventSystem.currentSelectedGameObject == null) return false;

        InputField input = eventSystem.currentSelectedGameObject.GetComponent<InputField>();
        return input != null && input.isFocused;
    }

    void Awake()
    {
        wasEnabled = new bool[hotkeyScripts.Length];
    }

    void Update()
    {
        bool now = IsTyping();
        if (now == typing) return;
        typing = now;

        for (int i = 0; i < hotkeyScripts.Length; i++)
        {
            Behaviour script = hotkeyScripts[i];
            if (script == null) continue;

            if (typing)
            {
                wasEnabled[i] = script.enabled;
                script.enabled = false;
            }
            else if (wasEnabled[i])
            {
                script.enabled = true;
            }
        }
    }
}
