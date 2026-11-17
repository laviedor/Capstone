using System.Collections.Generic;
using UnityEngine;

// Unity의 Create 메뉴에서 데코레이션 데이터베이스 에셋을 생성할 수 있게 한다.
[CreateAssetMenu(
    fileName = "DecorationDatabase",
    menuName = "Building/Decoration Database"
)]
// 데코레이션 데이터와 실제 배치할 프리팹의 대응 관계를 저장하는 ScriptableObject.
public class DecorationDatabase : ScriptableObject
{
    // 내부 항목을 Inspector에서 편집할 수 있도록 직렬화한다.
    [System.Serializable]
    // 데코레이션 데이터 하나와 대응 프리팹 하나를 묶는 항목.
    public class DecorationEntry
    {
        // 데코레이션을 식별하는 BuildingData 에셋.
        public BuildingData buildingData;
        // 해당 데코레이션을 설치할 때 사용할 프리팹.
        public GameObject buildingPrefab;
    }

    [Header("Registered Decorations")]
    // Inspector에 등록한 모든 데코레이션 항목 목록.
    public List<DecorationEntry> decorations =
        new List<DecorationEntry>();

    // 주어진 BuildingData에 연결된 프리팹을 검색한다.
    public GameObject GetPrefab(BuildingData data)
    {
        // 검색 대상 데이터가 없으면 결과도 없다.
        if (data == null)
            return null;

        // 등록된 항목을 순서대로 확인한다.
        foreach (DecorationEntry entry in decorations)
        {
            // 동일한 BuildingData 참조를 가진 항목의 프리팹을 반환한다.
            if (entry.buildingData == data)
                return entry.buildingPrefab;
        }

        return null;
    }
}
