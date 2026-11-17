using System;
using System.Collections.Generic;
using UnityEngine;

// 플레이어가 보유한 장비 목록과 부위별 장착 상태를 관리한다.
public class PlayerEquipment : MonoBehaviour
{
    // 다른 스크립트에서 접근하는 싱글턴 참조
    public static PlayerEquipment Instance { get; private set; }

    // 게임 시작 시 보유하고 있는 장비
    [Header("Starting Items")]
    [SerializeField] private List<EquipmentItemData> startingItems =
        new List<EquipmentItemData>();

    // 게임 시작 시 장착하고 있는 장비 (보유 목록에 없으면 자동으로 추가)
    [SerializeField] private List<EquipmentItemData> startingEquipped =
        new List<EquipmentItemData>();

    // 보유 장비 목록
    private readonly List<EquipmentItemData> ownedItems =
        new List<EquipmentItemData>();

    // 부위별 장착 장비
    private readonly Dictionary<EquipmentSlot, EquipmentItemData> equipped =
        new Dictionary<EquipmentSlot, EquipmentItemData>();

    // 보유 목록이나 장착 상태가 바뀌면 호출된다.
    public event Action OnEquipmentChanged;

    // 보유 장비 목록 (읽기 전용)
    public IReadOnlyList<EquipmentItemData> OwnedItems => ownedItems;

    // 중복 인스턴스를 제거하고 시작 장비를 등록한다.
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        foreach (EquipmentItemData item in startingItems)
        {
            if (item != null && !ownedItems.Contains(item))
                ownedItems.Add(item);
        }

        foreach (EquipmentItemData item in startingEquipped)
        {
            if (item == null)
                continue;

            if (!ownedItems.Contains(item))
                ownedItems.Add(item);

            equipped[item.slot] = item;
        }
    }

    // 파괴될 때 싱글턴 참조를 정리한다.
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // 해당 부위에 장착된 장비를 반환한다. 없으면 null.
    public EquipmentItemData GetEquipped(EquipmentSlot slot)
    {
        equipped.TryGetValue(slot, out EquipmentItemData item);
        return item;
    }

    // 아이템이 현재 장착 중인지 확인한다.
    public bool IsEquipped(EquipmentItemData item)
    {
        return item != null && GetEquipped(item.slot) == item;
    }

    // 보유 중인 장비를 해당 부위에 장착한다. 같은 부위의 기존 장비는 해제된다.
    public bool Equip(EquipmentItemData item)
    {
        if (item == null || !ownedItems.Contains(item))
            return false;

        equipped[item.slot] = item;
        OnEquipmentChanged?.Invoke();
        return true;
    }

    // 해당 부위의 장비를 해제한다.
    public void Unequip(EquipmentSlot slot)
    {
        if (equipped.Remove(slot))
            OnEquipmentChanged?.Invoke();
    }

    // 모든 부위의 장비를 해제한다.
    public void UnequipAll()
    {
        if (equipped.Count == 0)
            return;

        equipped.Clear();
        OnEquipmentChanged?.Invoke();
    }

    // 새 장비를 보유 목록에 추가한다.
    public void AddItem(EquipmentItemData item)
    {
        if (item == null || ownedItems.Contains(item))
            return;

        ownedItems.Add(item);
        OnEquipmentChanged?.Invoke();
    }

    // 장비를 보유 목록에서 제거한다. 장착 중이면 먼저 해제한다.
    public bool RemoveItem(EquipmentItemData item)
    {
        if (item == null || !ownedItems.Remove(item))
            return false;

        if (IsEquipped(item))
            equipped.Remove(item.slot);

        OnEquipmentChanged?.Invoke();
        return true;
    }
}
