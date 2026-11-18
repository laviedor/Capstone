using UnityEngine;
using UnityEngine.UI;

// 캐릭터 머리 위 사용자명 표시
//   카메라가 맵 전체를 비추도록 멀어지면(가로로 긴 창) 캐릭터와 함께 글자도 작아져 읽을 수 없으므로,
//   화면에서 일정 크기보다 작게 보이지 않도록 이름표를 키운다
public class PlayerNameLabel : MonoBehaviour
{
    [SerializeField] private Text label;
    [SerializeField] private float minScreenSize = 13f; // 글자가 화면에서 이 높이(픽셀)보다 작아지지 않게 한다

    private Vector3 designScale; // 씬/프리팹에 만들어 둔 이름표 크기

    void Awake()
    {
        designScale = transform.localScale;
    }

    public void SetName(string playerName)
    {
        label.text = playerName;
    }

    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return;

        // 만들어 둔 크기일 때 글자가 화면에서 몇 픽셀로 보이는지
        float parentScale = transform.parent != null ? transform.parent.lossyScale.y : 1f;
        float pixelsPerUnit = Screen.height / (2f * cam.orthographicSize);
        float shown = label.fontSize * designScale.y * parentScale * pixelsPerUnit;

        transform.localScale = shown > 0f && shown < minScreenSize ? designScale * (minScreenSize / shown) : designScale;
    }
}
