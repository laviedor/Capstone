using UnityEngine;
using TMPro;

public class InventoryResourceUI : MonoBehaviour
{
    [Header("Player Resources")]
    [SerializeField] private PlayerResources playerResources;

    [Header("Resource Texts")]
    [SerializeField] private TMP_Text woodText;
    [SerializeField] private TMP_Text stoneText;
    [SerializeField] private TMP_Text goldText;

    private void OnEnable()
    {
        UpdateResourceUI();
    }

    private void Update()
    {
        UpdateResourceUI();
    }

    private void UpdateResourceUI()
    {
        if (playerResources == null)
            return;

        if (woodText != null)
        {
            woodText.text =
                "³ª¹«: " +
                playerResources.GetAmount(ResourceType.Wood);
        }

        if (stoneText != null)
        {
            stoneText.text =
                "µ¹: " +
                playerResources.GetAmount(ResourceType.Stone);
        }

        if (goldText != null)
        {
            goldText.text =
                "°ñµå: " +
                playerResources.GetAmount(ResourceType.Gold);
        }
    }
}