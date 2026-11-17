
using UnityEngine;
using UnityEngine.InputSystem;

// 플레이어가 가까이에서 F 키를 누르면 집 입장 또는 퇴장을 요청한다.
public class HouseInteraction : MonoBehaviour
{
    [Header("플레이어")]
    // 상호작용 거리를 계산할 플레이어 Transform.
    [SerializeField] private Transform player;

    [Header("상호작용 거리")]
    // 상호작용이 허용되는 최대 거리(월드 단위).
    [SerializeField] private float interactionDistance = 1.5f;

    [Header("상호작용 종류")]
    // true면 퇴장 지점, false면 입장 지점으로 동작한다.
    [SerializeField] private bool isExitPoint = false;


    // 플레이어 참조가 비어 있다면 HouseSceneManager에서 가져온다.
    private void Start()
    {
        if (player == null && HouseSceneManager.Instance != null)
        {
            player = HouseSceneManager.Instance.Player;
        }
    }
    // 매 프레임 F 키 입력과 플레이어 거리 및 현재 위치 상태를 확인한다.
    private void Update()
    {
        // 키보드 장치가 없으면 입력 검사를 진행하지 않는다.
        if (Keyboard.current == null)
            return;

        // F 키를 누른 프레임에만 상호작용을 처리한다.
        if (!Keyboard.current.fKey.wasPressedThisFrame)
            return;

        // 필요한 참조가 없으면 거리 계산이나 씬 이동을 시도하지 않는다.
        if (player == null || HouseSceneManager.Instance == null)
            return;

        // 플레이어와 상호작용 대상 사이의 거리
        float distance = Vector2.Distance(
            player.position,
            transform.position
        );

        // 플레이어가 허용 거리보다 멀면 상호작용을 취소한다.
        if (distance > interactionDistance)
            return;

        // 퇴장 지점은 현재 집 내부에 있을 때만 ExitHouse를 호출한다.
        if (isExitPoint)
        {
            // 집 내부에서만 퇴장 가능
            if (HouseSceneManager.Instance.IsInsideHouse)
            {
                HouseSceneManager.Instance.ExitHouse();
            }
        }
        // 입장 지점은 현재 집 밖에 있을 때만 EnterHouse를 호출한다.
        else
        {
            // 마을에서만 입장 가능
            if (!HouseSceneManager.Instance.IsInsideHouse)
            {
                HouseSceneManager.Instance.EnterHouse();
            }
        }
    }
}
