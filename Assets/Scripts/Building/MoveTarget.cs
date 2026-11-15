using UnityEngine;

// 스크립트 설명: 마우스로 클릭한 위치까지 오브젝트를 직접 이동시킨다.
public class MoveTarget : MonoBehaviour
{
    // 변수 설명: 목표 지점으로 이동하는 속도
    public float moveSpeed = 5f;

    // 변수 설명: 마우스로 지정한 이동 목표 월드 좌표
    private Vector3 targetPosition;
    // 변수 설명: 현재 목표 위치로 이동 중인지 나타내는 값
    private bool isMoving = false;

    // 함수 설명: 마우스 클릭 좌표를 월드 좌표로 바꾸고 목표 위치까지 매 프레임 이동한다.
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
