using UnityEngine;

public class MoveTarget : MonoBehaviour
{
    public float moveSpeed = 5f;

    private Vector3 targetPosition;
    private bool isMoving = false;

    void Update()
    {
        // 마우스 왼쪽 클릭
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 mousePosition = Input.mousePosition;

            mousePosition.z =
                -Camera.main.transform.position.z;

            targetPosition =
                Camera.main.ScreenToWorldPoint(mousePosition);

            // 2D이므로 Z 좌표 유지
            targetPosition.z = transform.position.z;

            isMoving = true;
        }

        // 목표 위치로 이동
        if (isMoving)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                moveSpeed * Time.deltaTime
            );

            // 목표 위치 도착
            if (Vector3.Distance(
                transform.position,
                targetPosition) < 0.01f)
            {
                transform.position = targetPosition;
                isMoving = false;
            }
        }
    }
}
