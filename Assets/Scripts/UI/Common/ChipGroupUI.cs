using System;
using UnityEngine;
using UnityEngine.UI;

// 여러 개의 칩 버튼 중 하나만 선택되도록 관리하고 선택 변경을 알린다.
public class ChipGroupUI : MonoBehaviour
{
    [SerializeField] private Button[] chips;
    [SerializeField] private TabButtonVisual[] visuals;
    [SerializeField] private int defaultIndex = 0;

    // 선택한 칩 번호가 바뀌면 호출된다.
    public event Action<int> OnSelected;

    // 현재 선택된 칩 번호
    public int Selected { get; private set; }

    // 칩 버튼 클릭 이벤트를 연결하고 기본 칩을 선택한다.
    private void Awake()
    {
        if (chips != null)
        {
            for (int i = 0; i < chips.Length; i++)
            {
                int index = i;

                if (chips[i] != null)
                    chips[i].onClick.AddListener(() => Select(index));
            }
        }

        Select(defaultIndex, false);
    }

    // 칩을 선택하고 모양을 갱신한다. notify가 true면 선택 이벤트를 보낸다.
    public void Select(int index, bool notify = true)
    {
        Selected = index;

        if (visuals != null)
        {
            for (int i = 0; i < visuals.Length; i++)
            {
                if (visuals[i] != null)
                    visuals[i].SetActive(i == index);
            }
        }

        if (notify)
            OnSelected?.Invoke(index);
    }
}
