using UnityEngine;
using UnityEngine.InputSystem;

// 개발 중 데코레이션 지급 기능을 테스트하기 위한 컴포넌트.
public class DecorationInventoryTester : MonoBehaviour
{
    [Header("Test Decoration")]
    [SerializeField] private BuildingData testDecoration;

    [Header("Test Amount")]
    [SerializeField] private int testAmount = 3;

    // 매 프레임 T키 입력을 확인한다.
    private void Update()
    {
        // 키보드 장치가 없으면 입력 검사를 수행하지 않는다.
        if (Keyboard.current == null)
            return;

        // T키를 누르면 테스트용 데코레이션 지급
        // T키를 누른 프레임에만 테스트 지급 함수를 호출한다.
        if (Keyboard.current.tKey.wasPressedThisFrame)
        {
            GiveTestDecoration();
        }
    }

    // 인벤토리와 테스트 데이터가 준비되었을 때 아이템을 지급한다.
    private void GiveTestDecoration()
    {
        // 인벤토리 싱글턴이 없으면 지급할 수 없다.
        if (DecorationInventory.Instance == null)
        {
            Debug.LogError("DecorationInventory가 없습니다.");
            return;
        }

        // 지급할 데코레이션 데이터가 연결되어 있는지 검사한다.
        if (testDecoration == null)
        {
            Debug.LogError("Test Decoration이 연결되지 않았습니다.");
            return;
        }

        // 인벤토리에 지정된 데코레이션을 지정된 수량만큼 추가한다.
        DecorationInventory.Instance.AddDecoration(
            testDecoration,
            testAmount
        );

        Debug.Log(
            $"테스트 지급 완료: {testDecoration.buildingName} x{testAmount}"
        );
    }
}
