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

    private void Update()
    {
        if (playerResources == null)
            return;

        woodText.text = "³ª¹«: " +
            playerResources.GetAmount(ResourceType.Wood);

        stoneText.text = "µ¹: " +
            playerResources.GetAmount(ResourceType.Stone);

        goldText.text = "°ñµå: " +
            playerResources.GetAmount(ResourceType.Gold);
    }
}