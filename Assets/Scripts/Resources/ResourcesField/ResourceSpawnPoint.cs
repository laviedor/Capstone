
using System.Collections;
using UnityEngine;

// 자원 프리팹을 지정 위치에 생성하고 채집 후 일정 시간이 지나면 다시 활성화한다.
public class ResourceSpawnPoint : MonoBehaviour
{
    [Header("생성할 자원 Prefab")]
    [SerializeField]
    // 생성할 자원 오브젝트의 프리팹.
    private GameObject resourcePrefab;

    // 이미 생성된 자원을 기억해 중복 생성을 방지한다.
    private GameObject spawnedResource;
    // 생성한 자원의 채집 가능 상태를 변경하기 위한 컴포넌트 참조.
    private ResourceNode resourceNode;

    // 현재 실행 중인 재생성 대기 코루틴을 추적한다.
    private Coroutine respawnCoroutine;

    // 씬 시작 시 자원 오브젝트를 최초 1회 생성한다.
    private void Start()
    {
        SpawnResource();
    }

    // 프리팹 유효성과 기존 생성 여부를 확인한 후 자원을 자식 오브젝트로 생성한다.
    public void SpawnResource()
    {
        if (resourcePrefab == null)
        {
            Debug.LogWarning(
                gameObject.name +
                ": Resource Prefab이 없습니다."
            );
            return;
        }

        // 이미 자원이 존재한다면 중복 생성 방지
        if (spawnedResource != null)
            return;

        // 지정된 위치에 자원 생성
        // 이 생성 지점의 위치에 프리팹을 생성하고 부모를 현재 Transform으로 지정한다.
        spawnedResource = Instantiate(
            resourcePrefab,
            transform.position,
            Quaternion.identity,
            transform
        );

        // ResourceNode 연결
        // 생성된 오브젝트에서 ResourceNode를 찾아 생성 지점과 연결한다.
        resourceNode =
            spawnedResource.GetComponent<ResourceNode>();

        if (resourceNode != null)
        {
            resourceNode.SetSpawnPoint(this);
            // 오브젝트를 새로 만들지 않고 기존 자원을 다시 채집 가능한 상태로 만든다.
            resourceNode.SetDepleted(false);
        }
        else
        {
            Debug.LogWarning(
                resourcePrefab.name +
                "에 ResourceNode가 없습니다."
            );
        }

        Debug.Log(
            "자원 생성 완료: " +
            resourcePrefab.name
        );
    }

    // 자원이 채집되었을 때 호출
    // 채집 완료 통지를 받으면 기존 타이머를 중지하고 새 재생성 대기를 시작한다.
    public void OnResourceGathered(float respawnTime)
    {
        // 기존 재생성 작업이 있다면 중지
        if (respawnCoroutine != null)
        {
            StopCoroutine(respawnCoroutine);
        }

        // 재생성 대기 시작
        respawnCoroutine =
            StartCoroutine(RespawnRoutine(respawnTime));
    }

    // 지정된 시간만큼 기다렸다가 기존 자원의 채집 가능 상태와 색상을 복구한다.
    private IEnumerator RespawnRoutine(float delay)
    {
        Debug.Log(
            "자원 재생성 대기: " +
            delay +
            "초"
        );

        // 음수 대기 시간이 전달되더라도 최소 0초로 보정한다.
        yield return new WaitForSeconds(
            Mathf.Max(0f, delay)
        );

        // 자원 색상 및 채집 가능 상태 복구
        if (resourceNode != null)
        {
            // 오브젝트를 새로 만들지 않고 기존 자원을 다시 채집 가능한 상태로 만든다.
            resourceNode.SetDepleted(false);

            Debug.Log(
                "자원 재생성 완료: " +
                resourceNode.Kind
            );
        }

        respawnCoroutine = null;
    }
}
