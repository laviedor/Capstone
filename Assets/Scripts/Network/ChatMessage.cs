using System;

// 채팅 연결로 주고받는 메시지 (TCP, 한 줄에 JSON 하나, 서버 chatProtocol.js 참고). 건물 메시지는 BuildingMessage
//   클라 -> 서버  join    { name, token, world, v }  접속 후 처음 한 번 (JoinMessage, v: 이 클라이언트가 아는 메시지 버전)
//                 pos     { w, zn, x, y, z, n }   내 위치 (PosMessage). UDP(NetMessage)로 보내는 좌표를 가끔씩 이 연결로도 보낸다
//                 ping                            연결 확인 (5초마다). 서버는 pong 으로 답한다
//                 enter   { world }               월드 이동 (EnterMessage)
//                 zone    { zone }                월드 안에서 구역 이동 (ZoneMessage: "" 마을, "field" 필드, "house" 집 안)
//                 gather  { node }                필드의 자원 노드를 캠 (GatherMessage)
//                 exp_start { area }              채집 타이머 시작 (ExpeditionStartMessage)
//                 exp_claim                       끝난 채집 타이머의 보상 수령
//                 harvest { id }                  건물 수확 (HarvestMessage)
//                 discard { item }                장식 아이템 1개 버리기 (DiscardMessage)
//                 chat    { text }                광장 채팅
//   서버 -> 클라  chat    { name, text }          어느 월드에 있든 모든 접속자에게 전달
//                 kick                            같은 계정이 다른 곳에서 로그인(접속)해 끊김: 이쪽은 로그아웃된다 (LoginUI.ForceLogout)
//                 world   { id, name, mine, build }  들어간 월드 (WorldMessage)
//                 left    { name }                같이 보이던 사람이 다른 월드나 구역으로 가거나 접속을 끊음
//                 players { list }                지금 같은 월드, 같은 구역에 있는 사람들과 그 위치 (PlayersMessage), 들어갈 때마다 옴
//                 pos     { name, id, x, y, z, n }  같이 있는 사람의 위치 (PosMessage)
//                 pong                            ping 의 답
//                 res     { items }               내 재화 전부 (ResourceMessage), 바뀔 때마다 옴
//                 harvest { list }                내 건물의 수확 타이머 (HarvestMessage), 월드에 들어갈 때와 수확 요청의 결과로 옴
//                 exp     { area, remaining }     내 채집 타이머 (ExpeditionMessage), 접속할 때와 시작/수령 요청의 결과로 옴
//                 notice  { text }                안내 (요청이 거절된 이유, 받은 보상 등)
//                 dm      { sender }              친구(유저 id sender)가 1:1 채팅을 보냄 (FriendPush, 내용은 FriendApi 로 받음)
//                 friends                         친구 목록, 친구 요청, 친구의 접속 상태가 바뀜 (FriendApi 로 목록을 다시 받음)
[Serializable]
public class ChatMessage
{
    public const string Join = "join";
    public const string Chat = "chat";
    public const string Kick = "kick";
    public const string Dm = "dm";
    public const string Friends = "friends";
    public const string Enter = "enter";
    public const string World = "world";
    public const string Left = "left";
    public const string Resources = "res";
    public const string ServerNotice = "notice";
    public const string Zone = "zone";
    public const string Gather = "gather";
    public const string ExpeditionStart = "exp_start";
    public const string ExpeditionClaim = "exp_claim";
    public const string Expedition = "exp";
    public const string Harvest = "harvest";
    public const string Discard = "discard";
    public const string Pos = "pos";
    public const string Players = "players";
    public const string Ping = "ping";
    public const string Pong = "pong";
    public const string Look = "look";     // 겉모습 (착용한 장비): 보낼 때 내 것, 받을 때 같이 있는 사람의 것
    public const string Craft = "craft";   // 대장간에서 도구 만들기
    public const string Tile = "tile";     // 바닥 한 칸 바꾸기 (보낼 때), 다른 사람이 바꾼 한 칸 (받을 때)
    public const string Tiles = "tiles";   // 지금 월드의 바뀐 바닥 칸 전부
    public const int MaxTextLength = 200; // 서버 config.js의 MAX_CHAT_LENGTH와 같게 유지
    public const int Version = 2;         // 이 클라이언트가 아는 메시지 버전 (2: players, pos, ping/pong). 서버 chatProtocol.js 참고

    public string t;
    public string name;
    public string text;
}

// 입장 메시지: token(로그인 세션 토큰)을 서버가 확인하면 world 로 들어가고, 이 연결로 재화, 1:1 채팅, 친구 알림도 받는다
[Serializable]
public class JoinMessage
{
    public string t = ChatMessage.Join;
    public string name;
    public string token;
    public long world; // 들어갈 월드 id (0: 광장, 내 유저 id: 내 마을, 친구 유저 id: 친구 마을)
    public int v = ChatMessage.Version;
}

// 위치 (pos)
//   클라 -> 서버: 내 위치. w, zn 은 보낼 때 있던 월드와 구역 (서버가 지금 있는 곳과 다르면 버린다)
//   서버 -> 클라: 같이 있는 사람(name, 유저 id)의 위치
//   n 은 보낸 순서: 같은 좌표가 UDP 와 이 연결 두 길로 오가므로, 받는 쪽이 늦게 도착한 예전 것을 버리는 데 쓴다
[Serializable]
public class PosMessage
{
    public string t = ChatMessage.Pos;
    public string name;
    public long id;
    public long w;
    public string zn = "";
    public float x;
    public float y;
    public float z;
    public int n;
}

// 지금 같은 월드, 같은 구역에 있는 사람들 (서버가 알려 줌: 입장, 월드 이동, 구역 이동 직후)
[Serializable]
public class PlayersMessage
{
    public string t;
    public PlayerInfo[] list;
}

[Serializable]
public class PlayerInfo
{
    public string name;
    public long id; // 유저 id (로그인하지 않은 접속이면 0)
    public float x;
    public float y;
    public float z;
    public int n;
    public LookInfo look; // 겉모습 (보낸 적이 없는 사람이면 비어 있음)
}

// 겉모습: 부위마다 착용한 장비 id (EquipmentItemData.itemID, 없으면 "")
[Serializable]
public class LookInfo
{
    public string hat = "";
    public string top = "";
    public string bottom = "";
    public string tool = "";

    public string Get(EquipmentSlot slot)
    {
        switch (slot)
        {
            case EquipmentSlot.Hat: return hat;
            case EquipmentSlot.Top: return top;
            case EquipmentSlot.Bottom: return bottom;
            default: return tool;
        }
    }

    public bool SameAs(LookInfo other)
    {
        return other != null && hat == other.hat && top == other.top && bottom == other.bottom && tool == other.tool;
    }

    // 지금 착용한 장비로 만든다
    public static LookInfo From(PlayerEquipment equipment)
    {
        LookInfo look = new LookInfo();

        if (equipment == null)
            return look;

        look.hat = IdOf(equipment.GetEquipped(EquipmentSlot.Hat));
        look.top = IdOf(equipment.GetEquipped(EquipmentSlot.Top));
        look.bottom = IdOf(equipment.GetEquipped(EquipmentSlot.Bottom));
        look.tool = IdOf(equipment.GetEquipped(EquipmentSlot.Tool));
        return look;
    }

    private static string IdOf(EquipmentItemData item) => item != null && !string.IsNullOrEmpty(item.itemID) ? item.itemID : "";
}

// 내 겉모습 보내기 (클라 -> 서버)
[Serializable]
public class LookSendMessage
{
    public string t = ChatMessage.Look;
    public string hat = "";
    public string top = "";
    public string bottom = "";
    public string tool = "";
}

// 같이 있는 사람의 겉모습 (서버 -> 클라)
[Serializable]
public class LookMessage
{
    public string t;
    public string name;
    public LookInfo look;
}

// 월드 이동 요청
[Serializable]
public class EnterMessage
{
    public string t = ChatMessage.Enter;
    public long world;
}

// 월드 안에서 구역 이동
[Serializable]
public class ZoneMessage
{
    public string t = ChatMessage.Zone;
    public string zone;
}

// 필드의 자원 노드를 캠
[Serializable]
public class GatherMessage
{
    public string t = ChatMessage.Gather;
    public string node; // 노드 종류 (자원 프리팹 이름)
}

// 채집 타이머 시작
[Serializable]
public class ExpeditionStartMessage
{
    public string t = ChatMessage.ExpeditionStart;
    public string area; // 채집 장소 이름
}

// 내 채집 타이머 (서버가 알려 줌)
[Serializable]
public class ExpeditionMessage
{
    public string t;
    public string area;   // 채집 장소 이름, 진행 중인 것이 없으면 ""
    public int remaining; // 끝날 때까지 남은 시간(초), 0 이면 보상을 받을 수 있음
}

// 들어간 월드 (서버가 알려 줌)
[Serializable]
public class WorldMessage
{
    public string t;
    public long id;     // 월드 id
    public string name; // 마을 주인 닉네임 (광장은 "")
    public bool mine;   // 내 마을인지
    public bool build;  // 여기서 건물을 지을 수 있는지 (친구 마을은 구경만)
}

// 재화 (res: 지금 가진 양 전부)
[Serializable]
public class ResourceMessage
{
    public string t;
    public ResourceItem[] items;
}

[Serializable]
public class ResourceItem
{
    public string id; // 자원 이름 (wood, stone, gold), ResourceType 과 대소문자만 다름
    public int n;     // 수량
}

// 건물 수확
//   클라 -> 서버: id 건물을 수확한다는 요청
//   서버 -> 클라: 내 건물의 수확 타이머 list
//     all 이 true 면 지금 있는 월드의 내 건물 전부 (월드에 들어갈 때 등): 목록에 없는 건물은 수확할 수 없다
//     all 이 false 면 수확 요청의 결과로 오는 그 건물 하나
[Serializable]
public class HarvestMessage
{
    public string t = ChatMessage.Harvest;
    public string id;           // 수확할 건물의 네트워크 ID
    public bool all;
    public HarvestTimer[] list; // 수확 타이머
}

[Serializable]
public class HarvestTimer
{
    public string id;     // 건물의 네트워크 ID
    public int remaining; // 수확할 수 있을 때까지 남은 시간(초), 0 이면 지금 수확할 수 있음
}

// 장식 아이템 1개 버리기
[Serializable]
public class DiscardMessage
{
    public string t = ChatMessage.Discard;
    public string item; // 장식물 건물 종류 (BuildingData.buildingID)
}

// 대장간에서 도구 만들기 (결과는 서버가 재화와 안내로 알려 준다)
[Serializable]
public class CraftMessage
{
    public string t = ChatMessage.Craft;
    public string item; // 만들 도구 (CraftingRecipes 의 id, 예: shovel)
}

// 바닥 한 칸 (보낼 때: 내 마을의 그 칸을 바꿈 / 받을 때: 같은 월드의 다른 사람이 바꿈)
[Serializable]
public class TileMessage
{
    public string t = ChatMessage.Tile;
    public int x, y;    // 바닥 타일맵의 칸 좌표
    public string tile; // 바닥 종류 (GroundTileLayer.Dirt 등, Grass 면 기본 잔디로 되돌림)
}

// 지금 월드의 바뀐 바닥 칸 전부 (목록에 없는 칸은 잔디)
[Serializable]
public class TileListMessage
{
    public string t;
    public TileMessage[] list;
}

// 1:1 채팅 알림 (dm)
[Serializable]
public class FriendPush
{
    public string t;
    public long sender; // 보낸 친구의 유저 id
}
