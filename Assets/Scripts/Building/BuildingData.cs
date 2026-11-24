using UnityEngine;

// 스크립트 설명: 건물 종류별 설정을 ScriptableObject 에셋으로 저장한다.
public enum BuildingType
{
    Production,     // 자동 생산 건물
    Special,        // 특수 기능 건물
    Decoration      // 장식 건물
}


[CreateAssetMenu(
    fileName = "NewBuildingData",
    menuName = "Building/Building Data"
)]

// 스크립트 설명: 건물 종류별 설정을 ScriptableObject 에셋으로 저장한다.
public class BuildingData : ScriptableObject
{
    [Header("Identity")]
    // 변수 설명: 건물 종류를 식별하는 고유 문자열
    public string buildingID;
    // 변수 설명: 상호작용 로그에 표시할 건물 이름
    public string buildingName;

    [Header("UI")]
    // 변수 설명: 건물 목록 UI에 표시할 아이콘
    public Sprite buildingIcon;

    [Header("Type")]
    // 변수 설명: 생산·특수·장식 중 건물 분류
    public BuildingType buildingType;

    [Header("Grid Size")]
    // 변수 설명: 건물이 가로로 차지하는 격자 칸 수
    public int width = 1;
    // 변수 설명: 건물이 세로로 차지하는 격자 칸 수
    public int height = 1;

    [Header("Construction Cost")]
    // 변수 설명: 건설 시 필요한 자원 종류와 수량 목록
    public ResourceAmount[] constructionCosts;

    [Header("Placement Limit")]
    // 변수 설명: 한 마을에 지을 수 있는 최대 개수 (0 이면 제한 없음). 예) 플레이어 집 = 1
    //           건물은 내 마을에만 지을 수 있으므로(멀티버스, 친구 마을은 건설 불가) 사실상 '내 마을에 N개까지'다
    //           서버도 같은 값으로 검사한다 (Tools > Export Server Data 로 내보내기)
    [Min(0)] public int maxPerVillage = 0;

    // 변수 설명: 집 안에만 놓을 수 있는 장식인지 (예: 침대, 난로처럼 실내 가구). 켜면 마을 바깥에는 놓을 수 없다
    //           서버도 같은 값으로 검사한다 (Tools > Export Server Data 로 내보내기)
    public bool houseOnly;

    [Header("Interaction")]
    // 변수 설명: 상호작용 가능한 건물인지 나타내는 설정값
    public bool canInteract;

    [Header("Production")]
    // 변수 설명: 자동 생산할 자원 정보 목록
    public ResourceProduction[] productions;

    // 변수 설명: 자동 생산 처리 사이의 시간 간격
    public float productionInterval = 10f;
}
