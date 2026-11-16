
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>그리드 경로를 따라 플레이어를 실제로 이동시키는 컴포넌트.</summary>
public class PlayerPathMovement : MonoBehaviour
{
    // 경로 노드의 셀 좌표를 월드 좌표로 변환하는 그리드
    [SerializeField] private Grid grid;
    // 초당 이동 거리
    [SerializeField] private float moveSpeed = 3f;

    // 현재 진행 중인 이동 코루틴 참조
    private Coroutine moveCoroutine;

    // 목적지에 도착했을 때 실행되는 이벤트
    public event Action OnDestinationReached;

    /// <summary>전달받은 A* 경로를 따라 이동을 시작한다. 기존 이동은 먼저 중단한다.</summary>
    public void MoveAlongPath(List<GridNode> path)
    {
        // 기존 이동이 있다면 먼저 중단
        StopMovement();

        // 경로가 없거나 현재 위치가 목적지인 경우
        if (path == null || path.Count == 0)
        {
            OnDestinationReached?.Invoke();
            return;
        }

        moveCoroutine = StartCoroutine(FollowPath(path));
    }

    /// <summary>현재 실행 중인 이동 코루틴이 있다면 중지한다.</summary>
    public void StopMovement()
    {
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
            // 완료 또는 중단 후 코루틴 참조를 비워 둔다
            moveCoroutine = null;

            Debug.Log("플레이어 이동 중지");
        }
    }

    /// <summary>각 경로 노드의 중앙으로 순차 이동하고 마지막에 도착 이벤트를 발생시킨다.</summary>
    private IEnumerator FollowPath(List<GridNode> path)
    {
        foreach (GridNode node in path)
        {
            // 노드의 셀 좌표를 실제 이동할 월드 위치로 변환
            Vector3 targetPosition =
                grid.GetCellCenterWorld(node.cellPosition);

            targetPosition.z = transform.position.z;

            // 목표 위치에 충분히 가까워질 때까지 프레임마다 이동
            while (Vector3.Distance(
                transform.position,
                targetPosition) > 0.01f)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    targetPosition,
                    moveSpeed * Time.deltaTime
                );

                yield return null;
            }

            transform.position = targetPosition;
        }

        moveCoroutine = null;

        // 이동 완료 알림
        OnDestinationReached?.Invoke();
    }
}
