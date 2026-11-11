
using UnityEngine;

public class Building : MonoBehaviour
{
    [Header("Building Data")]
    [SerializeField] private BuildingData data;

    // 설치된 기준 Cell
    private Vector3Int originCell;

    private bool isPlaced = false;

    // 건물 고유 번호
    private int buildingNumber = 0;

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

    public void Initialize(Vector3Int cell)
    {
        originCell = cell;
        isPlaced = true;
    }

    // 건물 번호 지정
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
