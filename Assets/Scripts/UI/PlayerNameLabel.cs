using UnityEngine;
using UnityEngine.UI;

// 캐릭터 머리 위 사용자명 표시
public class PlayerNameLabel : MonoBehaviour
{
    [SerializeField] private Text label;

    public void SetName(string playerName)
    {
        label.text = playerName;
    }
}
