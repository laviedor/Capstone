using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 스크립트 설명: 철거 확인창. Tab 메뉴와 같은 모양(크림색 창, 나무색 테두리)으로 실행 중에 만든다.
//   ┌──────────────────────────────────────┐
//   │ [건물 그림]  대장간을 철거할까요?        │
//   │             돌려받는 자원: 나무 54 · 돌 27 │
//   │   [ 취소 ]              [ 철거하기 ]     │
//   └──────────────────────────────────────┘
//   창 밖(어두운 곳)을 누르거나 우클릭, Esc 를 누르면 취소된다.
//   예전에 씬에 만들어 두었던 확인 패널(confirmPanel)은 쓰지 않고 숨긴다.
public class DemolitionConfirmUI : MonoBehaviour
{
    [Header("UI (예전 확인창: 숨김 처리)")]
    [SerializeField]
    // 변수 설명: 예전 철거 확인창의 최상위 UI 오브젝트 (새 창을 쓰므로 항상 숨긴다)
    private GameObject confirmPanel;

    [SerializeField]
    // 변수 설명: 예전 확인창의 안내 텍스트 (쓰지 않음)
    private TMP_Text messageText;

    [Header("Demolition")]
    [SerializeField]
    // 변수 설명: 실제 건물 철거 처리를 수행하는 컴포넌트
    private BuildingDemolition buildingDemolition;

    private static readonly Vector2 DialogSize = new Vector2(330f, 140f);
    private const float PopSeconds = 0.1f;

    private static readonly Color DimColor = new Color(0.16f, 0.11f, 0.07f, 0.35f);

    // 열려 있는 확인창이 있는지 (건설·철거 안내 바의 우클릭 처리에서 확인)
    //   우클릭으로 창을 닫은 그 프레임에도 true 라서, 같은 우클릭으로 철거 모드까지 끝나지 않는다
    public static bool AnyOpen => anyOpen || Time.frameCount == closedFrame;

    private static bool anyOpen;
    private static int closedFrame = -1;

    private RectTransform root;
    private RectTransform dialog;
    private Image buildingIcon;
    private TMP_Text titleText;
    private TMP_Text detailText;
    private float openedAt;

    // 속성 설명: 현재 확인창이 열려 있는지 외부에서 확인할 수 있다.
    public bool IsOpen => root != null && root.gameObject.activeSelf;

    // 함수 설명: 예전 확인 패널을 숨기고 새 확인창을 만든다.
    private void Awake()
    {
        if (confirmPanel != null)
            confirmPanel.SetActive(false);

        Build();
        SetOpen(false);
    }

    private void OnDestroy()
    {
        if (root != null)
            Destroy(root.gameObject);

        anyOpen = false;
    }

    private void Build()
    {
        root = CozyRuntimeUI.CreateCanvas("DemolitionConfirm", 400);

        // 뒤를 어둡게 하고, 누르면 취소
        Image dim = CozyRuntimeUI.Image(root, "Dim", null, DimColor, true);
        CozyRuntimeUI.Stretch(dim.rectTransform, 0f, 0f, 0f, 0f);
        Button dimButton = dim.gameObject.AddComponent<Button>();
        dimButton.transition = Selectable.Transition.None;
        dimButton.onClick.AddListener(Cancel);

        Image window = CozyRuntimeUI.Image(root, "Dialog", "window", CozyRuntimeUI.Cream, true);
        dialog = window.rectTransform;
        dialog.anchorMin = dialog.anchorMax = new Vector2(0.5f, 0.5f);
        dialog.pivot = new Vector2(0.5f, 0.5f);
        dialog.anchoredPosition = Vector2.zero;
        dialog.sizeDelta = DialogSize;

        // 건물 그림
        Image iconBox = CozyRuntimeUI.Image(dialog, "IconBox", "slot", CozyRuntimeUI.Cream);
        CozyRuntimeUI.TopLeft(iconBox.rectTransform, 16f, 16f, 62f, 62f);

        RectTransform iconRect = CozyRuntimeUI.Rect(iconBox.transform, "Icon");
        CozyRuntimeUI.Stretch(iconRect, 8f, 7f, 8f, 9f);
        buildingIcon = iconRect.gameObject.AddComponent<Image>();
        buildingIcon.preserveAspect = true;
        buildingIcon.raycastTarget = false;

        // 문구
        float textX = 90f;
        float textW = DialogSize.x - textX - 16f;

        titleText = CozyRuntimeUI.Text(dialog, "Title", "", 14f, CozyRuntimeUI.Ink,
            TextAlignmentOptions.BottomLeft, true, true);
        CozyRuntimeUI.TopLeft(titleText.rectTransform, textX, 14f, textW, 38f);

        detailText = CozyRuntimeUI.Text(dialog, "Detail", "", 11f, CozyRuntimeUI.Muted,
            TextAlignmentOptions.TopLeft, false, true);
        CozyRuntimeUI.TopLeft(detailText.rectTransform, textX, 56f, textW, 30f);

        // 버튼
        float buttonY = DialogSize.y - 16f - 32f - 3f;
        float buttonW = (DialogSize.x - 32f - 10f) / 2f;

        Button cancel = CozyRuntimeUI.Button(dialog, "CancelButton", "btn_secondary", "취소",
            CozyRuntimeUI.Wood, 13f, Cancel, CozyRuntimeUI.Cream);
        CozyRuntimeUI.TopLeft((RectTransform)cancel.transform, 16f, buttonY, buttonW, 32f);

        Button confirm = CozyRuntimeUI.Button(dialog, "ConfirmButton", "btn_danger", "철거하기",
            CozyRuntimeUI.Danger, 13f, Confirm, CozyRuntimeUI.Cream);
        CozyRuntimeUI.TopLeft((RectTransform)confirm.transform, 16f + buttonW + 10f, buttonY, buttonW, 32f);
    }

    private void SetOpen(bool open)
    {
        if (root != null)
            root.gameObject.SetActive(open);

        if (anyOpen && !open)
            closedFrame = Time.frameCount;

        anyOpen = open;
    }

    // 함수 설명: 건물 정보와 돌려받는 것을 표시하고 확인창을 연다.
    public void Open(Building building)
    {
        if (building == null || building.Data == null || root == null)
            return;

        BuildingData data = building.Data;

        Sprite icon = data.buildingIcon;

        if (icon == null)
        {
            SpriteRenderer renderer = building.GetComponentInChildren<SpriteRenderer>();
            icon = renderer != null ? renderer.sprite : null;
        }

        buildingIcon.sprite = icon;
        buildingIcon.enabled = icon != null;

        titleText.text = data.buildingName + Josa(data.buildingName) + " 철거할까요?";
        detailText.text = data.UsesDecorationItem
            ? "장식 아이템 1개가 인벤토리로 돌아와요."
            : RefundText(data);

        SetOpen(true);
        dialog.localScale = Vector3.one * 0.9f;
        openedAt = Time.unscaledTime;
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        float t = Mathf.Clamp01((Time.unscaledTime - openedAt) / PopSeconds);
        dialog.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, t);

        if ((Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) ||
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame))
        {
            Cancel();
        }
    }

    // 함수 설명: 확인창을 닫고 철거 대상 선택을 해제한다.
    public void Cancel()
    {
        if (!IsOpen)
            return;

        SetOpen(false);

        if (buildingDemolition != null)
            buildingDemolition.ClearSelection();

        Debug.Log("철거 취소");
    }

    // 함수 설명: 선택한 건물의 실제 철거 처리를 요청하고 확인창을 닫는다.
    public void Confirm()
    {
        if (!IsOpen)
            return;

        SetOpen(false);

        if (buildingDemolition != null)
            buildingDemolition.ConfirmDemolition();
    }

    // 건설 비용의 90%(내림)를 "돌려받는 자원: 나무 54 · 돌 27" 형태로 만든다 (BuildingDemolition 의 환급과 같은 계산)
    private static string RefundText(BuildingData data)
    {
        List<string> parts = new List<string>();

        if (data.constructionCosts != null)
        {
            foreach (ResourceAmount cost in data.constructionCosts)
            {
                if (cost == null)
                    continue;

                int refund = Mathf.FloorToInt(cost.amount * 0.9f);

                if (refund > 0)
                    parts.Add(CozyColors.ResourceName(cost.resourceType) + " " + refund);
            }
        }

        return parts.Count > 0
            ? "돌려받는 자원: " + string.Join(" · ", parts)
            : "돌려받는 자원이 없어요.";
    }

    // 이름 끝 글자에 받침이 있으면 "을", 없으면 "를"
    private static string Josa(string word)
    {
        if (string.IsNullOrEmpty(word))
            return "을(를)";

        char last = word[word.Length - 1];

        if (last < 0xAC00 || last > 0xD7A3)
            return "을(를)";

        return (last - 0xAC00) % 28 == 0 ? "를" : "을";
    }
}
