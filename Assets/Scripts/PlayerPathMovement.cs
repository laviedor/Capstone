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

        if (moveCoroutine != null)
        {
            StopCoroutine(moveCoroutine);
        }

        moveCoroutine =
            StartCoroutine(FollowPath(path));
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


            // Á¤È®ÇÏ°Ô Cell Áß¾Ó¿¡ ¸ÂÃã
            transform.position =
                targetPosition;
        }


        moveCoroutine = null;
    }
}
