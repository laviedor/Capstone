using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>F키 입력 시 플레이어 주변의 상호작용 가능한 건물을 탐색한다.</summary>
public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction")]
    // 플레이어 중심의 상호작용 탐색 반경
    [SerializeField] private float interactionRange = 2f;

    /// <summary>매 프레임 F키를 누른 순간을 검사한다.</summary>
    private void Update()
    {
        // 새로운 Input System의 F키 눌림 감지
        if (Keyboard.current.fKey.wasPressedThisFrame)
        {
            Debug.Log("F키 입력 확인");
            TryInteract();
        }
    }

    /// <summary>반경 내 Collider2D를 조사하여 가장 가까운 BuildingInteraction을 실행한다.</summary>
    private void TryInteract()
    {
        // 플레이어 주변 원형 범위와 겹치는 모든 2D 콜라이더 검색
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                transform.position,
                interactionRange
            );

        Debug.Log("감지된 Collider 수 : " + hits.Length);

        foreach (Collider2D hit in hits)
        {
            Debug.Log("감지된 오브젝트 : " + hit.gameObject.name);
        }

        // 가장 가까운 상호작용 가능 건물을 저장
        BuildingInteraction closestBuilding = null;
        // 거리 비교를 위한 초기 최댓값
        float closestDistance = Mathf.Infinity;

        foreach (Collider2D hit in hits)
        {
            // 탐지된 오브젝트에 BuildingInteraction이 직접 붙어 있는지 확인
            BuildingInteraction building =
                hit.GetComponent<BuildingInteraction>();

            if (building == null)
                continue;

            float distance =
                Vector2.Distance(
                    transform.position,
                    building.transform.position
                );

            // 더 가까운 건물이 발견되면 상호작용 대상으로 갱신
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestBuilding = building;
            }
        }

        if (closestBuilding != null)
        {
            // 최종 선택된 건물의 상호작용 함수 호출
            closestBuilding.Interact();
        }
    }

    /// <summary>Scene 뷰에서 선택 시 상호작용 반경을 원으로 표시한다.</summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            interactionRange
        );
    }
}
