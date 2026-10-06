using UnityEngine;

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

public class BuildingData : ScriptableObject
{
    [Header("Identity")]
    public string buildingID;
    public string buildingName;

    [Header("Type")]
    public BuildingType buildingType;

    [Header("Grid Size")]
    public int width = 1;
    public int height = 1;

    [Header("Construction Cost")]
    public ResourceAmount[] constructionCosts;

    [Header("Interaction")]
    public bool canInteract;
}
