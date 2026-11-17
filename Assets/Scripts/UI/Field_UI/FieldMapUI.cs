using UnityEngine;

// 필드 탭 지도에서 현재 위치에 맞게 "여기" 표시를 켜고 끈다.
public class FieldMapUI : MonoBehaviour
{
    // 마을 핀 위의 "여기" 표시
    [SerializeField] private GameObject villageHereBadge;

    private void OnEnable()
    {
        FieldSceneManager manager = FieldSceneManager.Instance;
        bool inField = manager != null && manager.IsInField;

        if (villageHereBadge != null)
            villageHereBadge.SetActive(!inField);
    }
}
