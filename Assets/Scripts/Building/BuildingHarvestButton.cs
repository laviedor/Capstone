using UnityEngine;

/// <summary>
/// 건물 위에 뜨는 수확 버튼. 클릭 판정은 MousePathfinding이 콜라이더로 찾아 Click()을 호출한다.
/// </summary>
public class BuildingHarvestButton : MonoBehaviour
{
    // 이 버튼을 소유한 건물의 수확 컴포넌트
    private BuildingHarvest owner;

    /// <summary>버튼을 소유한 수확 컴포넌트를 연결한다.</summary>
    public void Initialize(BuildingHarvest harvest)
    {
        owner = harvest;
    }

    /// <summary>버튼 클릭 시 수확을 실행한다.</summary>
    public void Click()
    {
        if (owner != null)
        {
            owner.Harvest();
        }
    }
}
