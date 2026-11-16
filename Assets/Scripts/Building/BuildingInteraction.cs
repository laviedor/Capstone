using UnityEngine;

// 스크립트 설명: 건물 상호작용 시 호출할 기본 함수를 제공한다.
public class BuildingInteraction : MonoBehaviour
{
    // 변수 설명: 상호작용 로그에 표시할 건물 이름
    [SerializeField] private string buildingName = "Building";

    // 함수 설명: 건물 상호작용 호출 시 건물 이름을 콘솔에 출력한다. 현재 파일에는 별도의 입력 감지 코드가 없다.
    public void Interact()
    {
        Debug.Log(buildingName + " 과(와) 상호작용했습니다.");
    }
}
