using TMPro;
using UnityEngine;

public class ResourceUI : MonoBehaviour
{
    [Header("Resources")]
    // 표시할 자원 데이터를 제공하는 PlayerResources 참조.
    [SerializeField] private PlayerResources playerResources;

    [Header("Texts")]
    // 나무 보유량을 표시할 텍스트.
    [SerializeField] private TMP_Text woodText;
    // 돌 보유량을 표시할 텍스트.
    [SerializeField] private TMP_Text stoneText;
    // 골드 보유량을 표시할 텍스트.
    [SerializeField] private TMP_Text goldText;

    // 매 프레임 자원 표시 내용을 최신 수량으로 갱신한다.
    private void Update()
    {
        UpdateResourceUI();
    }

    // 자원 관리자에서 종류별 보유량을 조회하여 UI 텍스트를 갱신한다.
    private void UpdateResourceUI()
    {
        // 자원 관리자가 연결되지 않았다면 UI 갱신을 중단한다.
        if (playerResources == null)
            return;

        // 나무 텍스트가 연결된 경우에만 수량을 표시한다.
        if (woodText != null)
        {
            woodText.text =
                "Wood : " +
                playerResources.GetAmount(
                    ResourceType.Wood
                );
        }

        // 돌 텍스트가 연결된 경우에만 수량을 표시한다.
        if (stoneText != null)
        {
            stoneText.text =
                "Stone : " +
                playerResources.GetAmount(
                    ResourceType.Stone
                );
        }

        // 골드 텍스트가 연결된 경우에만 수량을 표시한다.
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
