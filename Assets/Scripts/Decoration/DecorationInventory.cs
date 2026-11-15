using System;
using System.Collections.Generic;
using UnityEngine;

// 데코레이션의 보유 수량을 관리하고 UI 갱신 이벤트를 알리는 컴포넌트.
public class DecorationInventory : MonoBehaviour
{
    // 다른 스크립트에서 접근하는 인벤토리 싱글턴 참조.
    public static DecorationInventory Instance { get; private set; }

    // 데코레이션 데이터와 보유 수량
    // BuildingData를 키로 사용해 데코레이션별 보유 수량을 저장한다.
    private Dictionary<BuildingData, int> decorations
        = new Dictionary<BuildingData, int>();

    // 건설 탭 갱신을 위한 이벤트
    // 아이템을 얻거나 사용할 때 구독 중인 UI에 변경 사실을 알린다.
    public event Action OnInventoryChanged;

    // 중복 인스턴스를 제거하고 현재 인스턴스를 등록한다.
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // 데코레이션 획득
    // 유효한 데코레이션을 지정된 수량만큼 인벤토리에 추가한다.
    public void AddDecoration(BuildingData data, int amount = 1)
    {
        // 데코레이션 타입이 아니거나 지급 수량이 0 이하라면 중단한다.
        if (!IsValidDecoration(data) || amount <= 0)
            return;

        // 처음 획득하는 종류라면 수량을 0으로 초기화한다.
        if (!decorations.ContainsKey(data))
            decorations[data] = 0;

        // 기존 보유 수량에 지급 수량을 더한다.
        decorations[data] += amount;

        Debug.Log(
            $"데코레이션 획득: {data.buildingName} x{amount}"
        );

        // 이벤트 구독자가 있을 때만 인벤토리 변경 이벤트를 호출한다.
        OnInventoryChanged?.Invoke();
    }

    // 현재 보유 수량
    // 해당 데이터의 보유 수량을 반환하며, 없으면 0을 반환한다.
    public int GetCount(BuildingData data)
    {
        if (data == null)
            return 0;

        // 딕셔너리에서 데이터를 찾으면 저장된 수량을 반환한다.
        if (decorations.TryGetValue(data, out int count))
            return count;

        return 0;
    }

    // 데코레이션 사용
    // 데코레이션을 1개 소비하고 성공 여부를 반환한다.
    public bool TryConsumeDecoration(BuildingData data)
    {
        if (!IsValidDecoration(data))
            return false;

        // 보유 수량이 없으면 소비에 실패한다.
        if (GetCount(data) <= 0)
            return false;

        // 보유 수량을 1 감소시킨다.
        decorations[data]--;

        // 남은 수량이 없다면 목록에서 항목 자체를 제거한다.
        if (decorations[data] <= 0)
            decorations.Remove(data);

        Debug.Log(
            $"데코레이션 사용: {data.buildingName}"
        );

        // 이벤트 구독자가 있을 때만 인벤토리 변경 이벤트를 호출한다.
        OnInventoryChanged?.Invoke();

        return true;
    }

    // 보유 중인 데코레이션 목록
    // 현재 1개 이상 보유 중인 데코레이션 데이터 목록을 만든다.
    public List<BuildingData> GetOwnedDecorations()
    {
        List<BuildingData> result = new List<BuildingData>();

        // 등록된 모든 데코레이션과 수량을 순회한다.
        foreach (var pair in decorations)
        {
            if (pair.Value > 0)
                result.Add(pair.Key);
        }

        return result;
    }

    // 데이터가 존재하며 Decoration 타입인지 검사한다.
    private bool IsValidDecoration(BuildingData data)
    {
        if (data == null)
            return false;

        if (data.buildingType != BuildingType.Decoration)
        {
            Debug.LogWarning(
                $"{data.buildingName}은 데코레이션이 아닙니다."
            );

            return false;
        }

        return true;
    }
}
