using TMPro;
using UnityEngine;

public class ResourceUI : MonoBehaviour
{
    [Header("Resources")]
    [SerializeField] private PlayerResources playerResources;

    [Header("Texts")]
    [SerializeField] private TMP_Text woodText;
    [SerializeField] private TMP_Text stoneText;
    [SerializeField] private TMP_Text goldText;

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
                "Wood : " +
                playerResources.GetAmount(
                    ResourceType.Wood
                );
        }

        if (stoneText != null)
        {
            stoneText.text =
                "Stone : " +
                playerResources.GetAmount(
                    ResourceType.Stone
                );
        }

        if (goldText != null)
        {
            goldText.text =
                "Gold : " +
                playerResources.GetAmount(
                    ResourceType.Gold
                );
        }
    }
}
