using System.Collections.Generic;
using UnityEngine;

public class PlayerResources : MonoBehaviour
{
    [Header("Player Resources")]
    [SerializeField]
    private List<ResourceAmount> resources = new List<ResourceAmount>();


    // 특정 자원의 현재 수량 가져오기
    public int GetAmount(ResourceType type)
    {
        foreach (ResourceAmount resource in resources)
        {
            if (resource.resourceType == type)
            {
                return resource.amount;
            }
        }

        return 0;
    }


    // 자원을 충분히 가지고 있는지 확인
    public bool HasResource(
        ResourceType type,
        int amount)
    {
        return GetAmount(type) >= amount;
    }


    // 자원 사용
    public bool SpendResource(
        ResourceType type,
        int amount)
    {
        foreach (ResourceAmount resource in resources)
        {
            if (resource.resourceType != type)
                continue;

            if (resource.amount < amount)
                return false;

            resource.amount -= amount;

            Debug.Log(
                type + " 사용 : " + amount +
                " / 남은 수량 : " + resource.amount
            );

            return true;
        }

        return false;
    }


    // 자원 추가
    public void AddResource(
        ResourceType type,
        int amount)
    {
        foreach (ResourceAmount resource in resources)
        {
            if (resource.resourceType == type)
            {
                resource.amount += amount;

                Debug.Log(
                    type + " 획득 : " + amount +
                    " / 현재 수량 : " + resource.amount
                );

                return;
            }
        }


        // 아직 가지고 있지 않은 종류라면 새로 추가
        ResourceAmount newResource =
            new ResourceAmount();

        newResource.resourceType = type;
        newResource.amount = amount;

        resources.Add(newResource);
    }
}