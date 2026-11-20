using UnityEngine;

// 마을에 미리 놓아 둔 시험용 집의 입구.
//   클릭하면 뜨는 '이동' 버튼을 누르거나 더블클릭하면 집 안으로 들어간다 (플레이어 위치와 상관없음)
public class TestHouseInteraction : HouseDoor
{
    public override bool CanUse => CanEnterHouse();

    public override void Use()
    {
        if (!CanUse)
            return;

        Debug.Log("임시 집 입장");
        HouseSceneManager.Instance.EnterHouse();
    }
}
