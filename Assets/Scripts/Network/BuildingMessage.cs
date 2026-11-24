using System;

// 건물 메시지 (채팅과 같은 TCP 연결, 한 줄에 JSON 하나, 서버 chatProtocol.js 참고)
//   클라 -> 서버  build { id, type, x, y }   demolish { id }   move { id, x, y }
//   서버 -> 클라  buildings { list }          입장하면 지금 맵에 있는 건물 전부
//                 build { id, type, x, y, owner, zone }, demolish { id }, move { id, x, y }  다른 사람이 바꾼 건물
//   zone: 건물이 놓인 구역 ("" 마을, "house" 집 안). 지을 때는 서버가 지은 사람이 있던 구역으로 정한다
[Serializable]
public class BuildingMessage
{
    public const string Build = "build";
    public const string Demolish = "demolish";
    public const string Move = "move";
    public const string List = "buildings";

    public string t;
    public string id;     // 건물마다 붙는 네트워크 ID (지은 사람이 만듦)
    public string type;   // 건물 종류 (BuildingData.buildingID)
    public int x;         // 기준 칸 (Building.OriginCell)
    public int y;
    public string owner;  // 지은 사람 닉네임
    public string zone;   // 놓인 구역 ("" 또는 null: 마을, "house": 집 안)
    public BuildingInfo[] list;

    public static bool IsBuildingMessage(string t) => t == Build || t == Demolish || t == Move || t == List;
}

[Serializable]
public class BuildingInfo
{
    public string id;
    public string type;
    public int x;
    public int y;
    public string owner;
    public string zone;
}
