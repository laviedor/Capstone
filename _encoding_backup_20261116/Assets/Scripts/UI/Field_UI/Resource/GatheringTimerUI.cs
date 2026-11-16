using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 장소 선택 버튼과 채집 타이머의 상태를 화면에 반영한다.
public class GatheringTimerUI : MonoBehaviour
{
    // 마당, 개울가, 들판, 호숫가의 선택 버튼 참조.
    [Header("장소별 채집 버튼")]
    [SerializeField] private Button yardButton;
    [SerializeField] private Button streamButton;
    [SerializeField] private Button fieldButton;
    [SerializeField] private Button lakesideButton;

    // 각 버튼에 장소 이름, 남은 시간, 보상 수령 상태를 표시한다.
    [Header("버튼 텍스트")]
    [SerializeField] private TMP_Text yardText;
    [SerializeField] private TMP_Text streamText;
    [SerializeField] private TMP_Text fieldText;
    [SerializeField] private TMP_Text lakesideText;

    // 장소별 버튼에 클릭 이벤트를 등록한다. 현재 전달하는 채집 시간은 모두 10초다.
    private void Start()
    {
        yardButton.onClick.AddListener(
            () => OnLocationClicked("마당", 10f));

        streamButton.onClick.AddListener(
            () => OnLocationClicked("개울가", 10f));

        fieldButton.onClick.AddListener(
            () => OnLocationClicked("들판", 10f));

        lakesideButton.onClick.AddListener(
            () => OnLocationClicked("호숫가", 10f));

        RefreshUI();
    }

    // 패널이 다시 표시될 때 현재 채집 상태를 반영한다.
    private void OnEnable()
    {
        RefreshUI();
    }

    // 진행 중인 타이머 숫자를 매 프레임 새로 표시한다.
    private void Update()
    {
        RefreshUI();
    }

    // 장소 선택 시 채집 시작 또는 완료된 보상 수령을 처리한다.
    private void OnLocationClicked(string location, float duration)
    {
        GatheringTimer timer = GatheringTimer.Instance;

        if (timer == null)
        {
            Debug.LogError("GatheringTimer가 없습니다.");
            return;
        }

        // 채집 완료 상태
        // 완료된 장소의 버튼을 다시 누르면 보상을 수령한다.
        if (timer.IsComplete)
        {
            if (timer.CurrentLocation == location)
            {
                timer.ClaimReward();
                RefreshUI();
            }

            return;
        }

        // 이미 채집 중이라면 새로운 채집 시작 불가
        // 한 번에 하나의 채집만 진행할 수 있다.
        if (timer.IsGathering)
            return;

        // 숲에서만 채집 시작 가능
        // 필드에 있는 동안에만 새로운 채집을 시작하도록 제한한다.
        if (FieldSceneManager.Instance == null ||
            !FieldSceneManager.Instance.IsInField)
        {
            Debug.LogWarning("숲에서만 채집할 수 있습니다.");
            return;
        }

        timer.StartGathering(location, duration);
        RefreshUI();
    }

    // 네 장소의 버튼 표시 상태를 한 번에 갱신한다.
    private void RefreshUI()
    {
        GatheringTimer timer = GatheringTimer.Instance;

        if (timer == null)
            return;

        // 버튼에 표시되는 시간 문구는 실제 시작 시간(10초)과 별도로 지정되어 있다.
        UpdateLocationButton(
            yardButton, yardText, "마당", "15분");

        UpdateLocationButton(
            streamButton, streamText, "개울가", "30분");

        UpdateLocationButton(
            fieldButton, fieldText, "들판", "1시간");

        UpdateLocationButton(
            lakesideButton, lakesideText, "호숫가", "6시간");
    }

    // 현재 장소와 채집 상태에 따라 버튼 문구 및 클릭 가능 여부를 결정한다.
    private void UpdateLocationButton(
        Button button,
        TMP_Text buttonText,
        string location,
        string durationLabel)
    {
        GatheringTimer timer = GatheringTimer.Instance;

        if (timer == null)
            return;

        // 현재 진행 또는 완료된 채집의 장소와 해당 버튼의 장소를 비교한다.
        bool isCurrentLocation =
            timer.CurrentLocation == location;

        // 진행 중에는 모든 장소 버튼을 잠그고 해당 장소에 남은 시간을 표시한다.
        // 채집 진행 중
        if (timer.IsGathering)
        {
            if (isCurrentLocation)
            {
                buttonText.text =
                    FormatTime(timer.RemainingTime);

                button.interactable = false;
            }
            else
            {
                buttonText.text =
                    $"{location} ({durationLabel})";

                button.interactable = false;
            }

            return;
        }

        // 완료 시 해당 장소의 버튼만 보상 수령용으로 활성화한다.
        // 채집 완료
        if (timer.IsComplete)
        {
            if (isCurrentLocation)
            {
                buttonText.text = "보상 수령";
                button.interactable = true;
            }
            else
            {
                buttonText.text =
                    $"{location} ({durationLabel})";

                button.interactable = false;
            }

            return;
        }

        // 대기 중에는 모든 장소 버튼을 다시 선택할 수 있게 한다.
        // 채집 대기 상태
        buttonText.text =
            $"{location} ({durationLabel})";

        button.interactable = true;
    }

    // 남은 초를 올림 처리하여 시:분:초(00:00:00) 형식으로 변환한다.
    private string FormatTime(float time)
    {
        int totalSeconds = Mathf.CeilToInt(time);

        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int seconds = totalSeconds % 60;

        return $"{hours:00}:{minutes:00}:{seconds:00}";
    }
}
