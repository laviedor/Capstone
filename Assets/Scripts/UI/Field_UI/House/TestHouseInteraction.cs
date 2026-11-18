using UnityEngine;

public class TestHouseInteraction : MonoBehaviour
{
    [Header("상호작용 설정")]
    [SerializeField] private float interactionDistance = 1.5f;

    [Header("플레이어")]
    [SerializeField] private Transform player;

    private void Update()
    {
        if (player == null || HouseSceneManager.Instance == null)
            return;

        // 이미 집 안에 있다면 입장 입력을 받지 않는다.
        if (HouseSceneManager.Instance.IsInsideHouse)
            return;

        // 플레이어와 건물 사이의 거리 계산
        float distance = Vector2.Distance(
            player.position,
            transform.position
        );

        // 일정 거리 이내에서 F키를 누르면 집 입장
        if (distance <= interactionDistance &&
            Input.GetKeyDown(KeyCode.F))
        {
            Debug.Log("임시 집 상호작용 성공");

            HouseSceneManager.Instance.EnterHouse();
        }
    }
}
