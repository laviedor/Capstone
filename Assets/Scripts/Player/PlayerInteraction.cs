using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private float interactionRange = 2f;

    private void Update()
    {
        if (Keyboard.current.fKey.wasPressedThisFrame)
        {
            Debug.Log("F키 입력 확인");
            TryInteract();
        }
    }

    private void TryInteract()
    {
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

        BuildingInteraction closestBuilding = null;
        float closestDistance = Mathf.Infinity;

        foreach (Collider2D hit in hits)
        {
            BuildingInteraction building =
                hit.GetComponent<BuildingInteraction>();

            if (building == null)
                continue;

            float distance =
                Vector2.Distance(
                    transform.position,
                    building.transform.position
                );

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestBuilding = building;
            }
        }

        if (closestBuilding != null)
        {
            closestBuilding.Interact();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            interactionRange
        );
    }
}
