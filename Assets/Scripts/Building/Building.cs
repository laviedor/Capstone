using UnityEngine;

// 스크립트 설명: 개별 건물의 데이터와 설치 상태, 설치 위치 및 번호를 보관한다.
public class Building : MonoBehaviour
{
    [Header("Building Data")]
    // 변수 설명: 건물의 설정을 담은 BuildingData 에셋
    [SerializeField] private BuildingData data;

    // 설치된 기준 Cell
    // 변수 설명: 건물이 설치된 기준 격자 좌표
    private Vector3Int originCell;

    // 변수 설명: 건물이 실제로 설치 완료되었는지 나타내는 값
    private bool isPlaced = false;

    // 건물 고유 번호
    // 변수 설명: 같은 종류 건물들을 구분하기 위한 번호
    private int buildingNumber = 0;

    // 건물이 놓인 구역: "" 마을, "house" 마을 주인 집 안 (서버 buildings.zone 과 같은 값)
    public const string VillageZone = "";
    public const string HouseZone = "house";

    private string zone = VillageZone;

    public string Zone => zone;

    public bool IsInHouse => zone == HouseZone;

    // 놓인 구역을 정한다 (BuildingPlacement, BuildingSync 가 설치할 때 부름)
    public void SetZone(string newZone)
    {
        zone = newZone == HouseZone ? HouseZone : VillageZone;
    }

    public bool IsPlaced => isPlaced;

    public int BuildingNumber => buildingNumber;

    public BuildingData Data
    {
        get { return data; }
    }

    public string BuildingID
    {
        get
        {
            if (data == null)
                return "";

            return data.buildingID;
        }
    }

    public int Width
    {
        get
        {
            if (data == null)
                return 1;

            return data.width;
        }
    }

    public int Height
    {
        get
        {
            if (data == null)
                return 1;

            return data.height;
        }
    }

    public Vector3Int OriginCell
    {
        get { return originCell; }
    }

    // 함수 설명: 설치 기준 셀을 저장하고 건물의 설치 완료 상태를 설정한다.
    public void Initialize(Vector3Int cell)
    {
        originCell = cell;
        isPlaced = true;
    }

    // 건물 번호 지정
    // 함수 설명: 양수인 고유 번호만 건물에 저장한다.
    public void SetBuildingNumber(int number)
    {
        if (number <= 0)
        {
            Debug.LogWarning("건물 번호는 1 이상이어야 합니다.");
            return;
        }

        buildingNumber = number;
    }
}
