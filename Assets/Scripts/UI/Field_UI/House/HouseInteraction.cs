using UnityEngine;

// 집 입구(마을의 집 건물) 또는 출구(집 안의 문).
//   예전에는 플레이어가 가까이 가서 F 키를 눌러야 했지만, 이제는 위치와 상관없이
//   클릭하면 뜨는 '이동' 버튼을 누르거나 더블클릭해서 들어가고 나온다 (HouseDoor, HouseMovePopup)
public class HouseInteraction : HouseDoor
{
    [Header("상호작용 종류")]
    // true면 퇴장 지점, false면 입장 지점으로 동작한다.
    [SerializeField] private bool isExitPoint = false;

    public override bool CanUse => isExitPoint ? CanExitHouse() : CanEnterHouse();

    public override void Use()
    {
        if (!CanUse)
            return;

        if (isExitPoint)
            HouseSceneManager.Instance.ExitHouse();
        else
            HouseSceneManager.Instance.EnterHouse();
    }
}
