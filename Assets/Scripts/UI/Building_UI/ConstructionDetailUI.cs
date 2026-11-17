using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 건설 탭 오른쪽 상세 패널: 선택한 건물 카드의 정보와 비용을 보여 주고 건설을 시작한다.
public class ConstructionDetailUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image artBackground;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text tagText;
    [SerializeField] private TMP_Text productionText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private Button buildButton;
    [SerializeField] private TMP_Text buildLabel;

    [Header("System")]
    [SerializeField] private PlayerResources playerResources;

    // 선택 중인 카드
    private BuildingCardUI selectedCard;
    private float refreshTimer;

    public BuildingCardUI SelectedCard => selectedCard;

    private void Awake()
    {
        if (buildButton != null)
            buildButton.onClick.AddListener(OnBuildClicked);
    }

    // 자원 수량이 바뀔 수 있으므로 열려 있는 동안 주기적으로 다시 확인한다.
    private void Update()
    {
        refreshTimer -= Time.unscaledDeltaTime;

        if (refreshTimer > 0f)
            return;

        refreshTimer = 0.25f;
        ShowCurrent();
    }

    // 카드를 선택하고 상세 정보를 표시한다.
    public void Select(BuildingCardUI card)
    {
        if (selectedCard != null && selectedCard != card)
            selectedCard.SetSelected(false);

        selectedCard = card;

        if (selectedCard != null)
            selectedCard.SetSelected(true);

        ShowCurrent();
    }

    // 선택한 카드가 없거나 숨겨졌으면 선택을 해제한다.
    public void ClearIfHidden()
    {
        if (selectedCard == null || !selectedCard.gameObject.activeSelf)
            Select(null);
    }

    private void ShowCurrent()
    {
        BuildingData data = selectedCard != null ? selectedCard.Data : null;

        if (data == null)
        {
            icon.enabled = false;
            nameText.text = "건물을 선택하세요";
            tagText.text = "";
            productionText.text = "";
            costText.text = "왼쪽 카드를 눌러 정보를 확인해요.";
            buildButton.interactable = false;
            buildLabel.text = "건설하기";
            return;
        }

        icon.sprite = data.buildingIcon;
        icon.enabled = data.buildingIcon != null;
        icon.preserveAspect = true;

        if (artBackground != null)
            artBackground.color = CozyColors.TypeTint(data.buildingType);

        nameText.text = data.buildingName;
        tagText.text = CozyColors.TypeName(data.buildingType) +
                       $" · 크기 {data.width} × {data.height}";
        productionText.text = BuildProductionText(data);

        bool enough;

        if (data.buildingType == BuildingType.Decoration)
        {
            int count = DecorationInventory.Instance != null
                ? DecorationInventory.Instance.GetCount(data)
                : 0;

            costText.text = "보유 수량  <b>" + count + "개</b>";
            enough = count > 0;
            buildLabel.text = enough ? "배치하기" : "보유한 장식이 없어요";
        }
        else
        {
            costText.text = BuildCostText(data, out enough);
            buildLabel.text = enough ? "건설하기" : "자원이 부족해요";
        }

        buildButton.interactable = enough;
    }

    // 생산 정보를 "골드 +5 / 10초" 형태로 만든다.
    private static string BuildProductionText(BuildingData data)
    {
        if (data.productions == null || data.productions.Length == 0)
            return "";

        StringBuilder builder = new StringBuilder();

        foreach (ResourceProduction production in data.productions)
        {
            if (production == null || production.amount <= 0)
                continue;

            if (builder.Length > 0)
                builder.Append(", ");

            builder.Append(CozyColors.ResourceName(production.resourceType));
            builder.Append(" +");
            builder.Append(production.amount);
        }

        if (builder.Length == 0)
            return "";

        return "생산: " + builder + " / " + data.productionInterval + "초";
    }

    // 자원별 "보유 / 필요" 를 색으로 구분해 만든다. 하나라도 모자라면 enough는 false.
    private string BuildCostText(BuildingData data, out bool enough)
    {
        enough = true;

        if (data.constructionCosts == null || data.constructionCosts.Length == 0)
            return "건설 비용  <b>무료</b>";

        StringBuilder builder = new StringBuilder("건설 비용  ");

        foreach (ResourceAmount cost in data.constructionCosts)
        {
            if (cost == null)
                continue;

            int have = playerResources != null ? playerResources.GetAmount(cost.resourceType) : 0;
            bool ok = have >= cost.amount;

            if (!ok)
                enough = false;

            builder.Append(CozyColors.ResourceName(cost.resourceType));
            builder.Append(" <b><color=");
            builder.Append(ok ? CozyColors.OkHex : CozyColors.LackHex);
            builder.Append(">");
            builder.Append(have);
            builder.Append("/");
            builder.Append(cost.amount);
            builder.Append("</color></b>   ");
        }

        return builder.ToString().TrimEnd();
    }

    private void OnBuildClicked()
    {
        if (selectedCard != null)
            selectedCard.StartPlacement();
    }
}
