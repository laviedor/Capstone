
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerPathMovement : MonoBehaviour
{
    [SerializeField] private Grid grid;
    [SerializeField] private float moveSpeed = 3f;

    private Coroutine moveCoroutine;

    public void MoveAlongPath(List<GridNode> path)
    {
        if (path == null || path.Count == 0)
            return;

        // 기존 이동 중단
        StopMovement();

        // 새로운 경로 이동 시작
        moveCoroutine =
            StartCoroutine(FollowPath(path));
    }

    // 현재 진행 중인 이동 중지
    public void StopMovement()
    {
        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
            moveCoroutine = null;

            Debug.Log("플레이어 이동 중지");
        }
    }

    IEnumerator FollowPath(List<GridNode> path)
    {
        foreach (GridNode node in path)
        {
            Vector3 targetPosition =
                grid.GetCellCenterWorld(
                    node.cellPosition
                );

            targetPosition.z =
                transform.position.z;

            while (Vector3.Distance(
                transform.position,
                targetPosition) > 0.01f)
            {
                transform.position =
                    Vector3.MoveTowards(
                        transform.position,
                        targetPosition,
                        moveSpeed * Time.deltaTime
                    );

                yield return null;
            }

            // 정확하게 Cell 중앙에 맞춤
            transform.position = targetPosition;
        }

        moveCoroutine = null;
    }
}
