using UnityEngine;
using TMPro;

// 플레이어가 보유한 나무·돌·골드 수량을 인벤토리 텍스트에 표시한다.
public class InventoryResourceUI : MonoBehaviour
{
    // 자원 보유량을 조회할 플레이어 자원 관리 컴포넌트
    [Header("Player Resources")]
    [SerializeField] private PlayerResources playerResources;

    // 자원 종류별 표시용 TextMeshPro 텍스트
    [Header("Resource Texts")]
    [SerializeField] private TMP_Text woodText;
    [SerializeField] private TMP_Text stoneText;
    [SerializeField] private TMP_Text goldText;

    // true면 "나무: 12" 대신 숫자만 표시한다 (아이콘 옆에 숫자를 두는 디자인용)
    [SerializeField] private bool numbersOnly;

    // 매 프레임 최신 자원 보유량을 UI에 반영한다.
    private void Update()
    {
        // 자원 관리 컴포넌트가 연결되지 않았으면 갱신을 중단한다.
        if (playerResources == null)
            return;

        // 나무 보유량 표시
        woodText.text = (numbersOnly ? "" : "나무: ") +
            playerResources.GetAmount(ResourceType.Wood);

        // 돌 보유량 표시
        stoneText.text = (numbersOnly ? "" : "돌: ") +
            playerResources.GetAmount(ResourceType.Stone);

        // 골드 보유량 표시
        goldText.text = (numbersOnly ? "" : "골드: ") +
            playerResources.GetAmount(ResourceType.Gold);
    }
}