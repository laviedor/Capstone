using UnityEngine;

public class Building : MonoBehaviour
{
    [Header("Building Data")]
    [SerializeField] private BuildingData data;

    // 설치된 기준 Cell
    private Vector3Int originCell;

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
    }
}