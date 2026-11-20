using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 건설·철거 모드일 때 화면 아래 가운데에 뜨는 안내 바.
//   [건설] 놓을 자리를 클릭하세요   [취소]
//   [철거] 철거할 건물을 클릭하세요  [끝내기]
//   게임은 마우스로만 조작하므로, 모드를 끝내는 방법은 이 버튼과 우클릭이다 (키보드 단축키 없음)
//   BuildingModeManager 가 시작할 때 스스로 만든다 (씬에 따로 놓지 않아도 됨)
public class BuildModeHUD : MonoBehaviour
{
    private static readonly Vector2 BarSize = new Vector2(372f, 32f);
    private const float BottomMargin = 14f;

    private static readonly Color GreenFallback = new Color32(0x3F, 0x7A, 0x2E, 0xFF);

    private static BuildModeHUD instance;

    private BuildingModeManager modeManager;
    private RectTransform bar;
    private TMP_Text chipText;
    private Image chipImage;
    private TMP_Text messageText;
    private TMP_Text buttonText;
    private bool? shownTool; // 지금 표시 중인 모드 (null: 숨김, true: 건설, false: 철거)

    // BuildingModeManager 가 Awake 에서 부른다
    public static void Ensure(BuildingModeManager manager)
    {
        if (instance != null)
        {
            instance.modeManager = manager;
            return;
        }

        RectTransform canvas = CozyRuntimeUI.CreateCanvas("BuildModeHUD", 250);
        instance = canvas.gameObject.AddComponent<BuildModeHUD>();
        instance.modeManager = manager;
        instance.Build(canvas);
    }

    private void Build(RectTransform canvas)
    {
        Image background = CozyRuntimeUI.Image(canvas, "Bar", "pill_cream", CozyRuntimeUI.Cream, true);
        bar = background.rectTransform;
        bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0f);
        bar.pivot = new Vector2(0.5f, 0f);
        bar.anchoredPosition = new Vector2(0f, BottomMargin);
        bar.sizeDelta = BarSize;

        // 모드 표시 칩 (건설: 초록 / 철거: 붉은 테두리)
        chipImage = CozyRuntimeUI.Image(bar, "ModeChip", "btn_primary", GreenFallback);
        CozyRuntimeUI.TopLeft(chipImage.rectTransform, 5f, 4f, 52f, 23f);
        chipText = CozyRuntimeUI.Text(chipImage.transform, "Label", "건설", 11f, Color.white,
            TextAlignmentOptions.Center, true);
        CozyRuntimeUI.Stretch(chipText.rectTransform, 0f, 0f, 0f, 2f);

        messageText = CozyRuntimeUI.Text(bar, "Message", "", 12f, CozyRuntimeUI.Ink,
            TextAlignmentOptions.MidlineLeft, true);
        CozyRuntimeUI.TopLeft(messageText.rectTransform, 64f, 0f, BarSize.x - 64f - 82f, BarSize.y);

        Button button = CozyRuntimeUI.Button(bar, "ExitButton", "btn_secondary", "끝내기",
            CozyRuntimeUI.Wood, 11f, ExitMode, CozyRuntimeUI.Cream);
        CozyRuntimeUI.TopLeft((RectTransform)button.transform, BarSize.x - 5f - 72f, 4f, 72f, 23f);
        buttonText = button.GetComponentInChildren<TMP_Text>();

        bar.gameObject.SetActive(false);
    }

    private void Update()
    {
        bool active = modeManager != null && modeManager.isActiveAndEnabled && modeManager.IsBuildMode;
        bool? tool = active ? modeManager.CurrentTool == BuildingTool.Build : (bool?)null;

        if (tool != shownTool)
            Show(tool);

        if (!active)
            return;

        // 우클릭으로 모드를 끝낸다 (철거 확인창이 떠 있으면 그 창이 먼저 처리한다)
        if (Mouse.current != null &&
            Mouse.current.rightButton.wasPressedThisFrame &&
            !DemolitionConfirmUI.AnyOpen)
        {
            ExitMode();
        }
    }

    private void Show(bool? build)
    {
        shownTool = build;

        if (build == null)
        {
            bar.gameObject.SetActive(false);
            return;
        }

        bool isBuild = build.Value;

        chipImage.sprite = CozyRuntimeUI.Sprite(isBuild ? "btn_primary" : "btn_danger") ?? chipImage.sprite;
        chipText.text = isBuild ? "건설" : "철거";
        chipText.color = isBuild ? Color.white : CozyRuntimeUI.Danger;
        messageText.text = isBuild
            ? "놓을 자리를 클릭하세요 · 우클릭 취소"
            : "철거할 건물을 클릭하세요 · 우클릭 끝내기";
        buttonText.text = isBuild ? "취소" : "끝내기";

        bar.gameObject.SetActive(true);
    }

    private void ExitMode()
    {
        if (modeManager != null)
            modeManager.ExitBuildMode();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}
