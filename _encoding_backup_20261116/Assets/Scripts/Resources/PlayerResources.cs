using System.Collections.Generic;
using UnityEngine;

public class PlayerResources : MonoBehaviour
{
    [Header("Player Resources")]
    // Inspector에서 자원 목록을 설정할 수 있도록 직렬화한다.
    [SerializeField]
    private List<ResourceAmount> resources = new List<ResourceAmount>();


    // 특정 자원의 현재 수량 가져오기
    // 지정한 자원의 현재 보유량을 반환한다. 목록에 없으면 0을 반환한다.
    public int GetAmount(ResourceType type)
    {
        // 보유 중인 자원 목록에서 요청한 자원 종류를 검색한다.
        foreach (ResourceAmount resource in resources)
        {
            // 요청한 자원 종류와 일치하는 항목을 찾는다.
            if (resource.resourceType == type)
            {
                return resource.amount;
            }
        }

        return 0;
    }


    // 자원을 충분히 가지고 있는지 확인
    // 해당 자원을 요청 수량 이상 보유하고 있는지 확인한다.
    public bool HasResource(
        ResourceType type,
        int amount)
    {
        // 현재 수량과 필요 수량을 비교해 참/거짓을 반환한다.
        return GetAmount(type) >= amount;
    }


    // 자원 사용
    // 자원이 충분하면 요청 수량만큼 차감하고 성공 여부를 반환한다.
    public bool SpendResource(
        ResourceType type,
        int amount)
    {
        // 보유 중인 자원 목록에서 요청한 자원 종류를 검색한다.
        foreach (ResourceAmount resource in resources)
        {
            // 다른 자원 종류는 건너뛴다.
            if (resource.resourceType != type)
                continue;

            // 보유량이 부족하면 차감하지 않고 실패를 반환한다.
            if (resource.amount < amount)
                return false;

            // 실제로 자원 수량을 차감한다.
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
    // 지정한 자원의 수량을 증가시키고, 없으면 새 항목을 등록한다.
    public void AddResource(
        ResourceType type,
        int amount)
    {
        // 보유 중인 자원 목록에서 요청한 자원 종류를 검색한다.
        foreach (ResourceAmount resource in resources)
        {
            // 요청한 자원 종류와 일치하는 항목을 찾는다.
            if (resource.resourceType == type)
            {
                // 이미 보유한 자원 종류라면 기존 수량에 더한다.
                resource.amount += amount;

                Debug.Log(
                    type + " 획득 : " + amount +
                    " / 현재 수량 : " + resource.amount
                );

                return;
            }
        }


        // 아직 가지고 있지 않은 종류라면 새로 추가
        // 아직 목록에 없는 자원 종류를 새 데이터 객체로 만든다.
        ResourceAmount newResource =
            new ResourceAmount();

        newResource.resourceType = type;
        newResource.amount = amount;

        // 새 자원 항목을 보유 목록에 추가한다.
        resources.Add(newResource);
    }
}