using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// 서버가 쓰는 기획 데이터를 Unity 프로젝트에서 내보낸다: 메뉴 Tools > Export Server Data
//   Server/data/buildings.json : 건물 프리팹(BuildingData)의 크기, 건설 비용, 수확량과 수확 주기, 장식물 여부
//   Server/data/gathering.json : 필드 자원 프리팹(ResourceNode)의 채집 보상과 시간, 채집 타이머 보상으로 나오는 장식물
//   Server/data/expeditions.json : 채집 타이머의 장소별 시간, 골드, 장식물 확률 (GatheringTimer.cs, GatheringTimerUI.cs 코드에서 읽음)
//   서버는 이 값으로 건설 비용, 철거 환급, 겹침 검사, 건물 수확, 채집 보상을 계산한다 (서버 catalog.js)
//   건물/자원 프리팹이나 BuildingData, 건물 수확(BuildingHarvest)의 주기와 기본 수확량, 장식물 목록(DecorationDatabase),
//   채집 타이머 코드의 숫자를 바꾸면 다시 내보내고, 서버에 올려 재시작해야 반영된다
//   건물 수확은 게임(BuildingHarvest)과 같게 내보낸다: 장식이 아닌 건물은 모두 수확하고,
//   수확량은 BuildingData 의 생산 목록(productions), 비어 있으면 BuildingHarvest 의 기본값이다
public static class ServerDataExporter
{
    private const string BuildingsPath = "Server/data/buildings.json";
    private const string GatheringPath = "Server/data/gathering.json";
    private const string ExpeditionsPath = "Server/data/expeditions.json";

    [MenuItem("Tools/Export Server Data")]
    public static void Export()
    {
        int buildings = ExportBuildings();
        ExportGathering(out int nodes, out int decorations);
        int areas = ExportExpeditions();
        Debug.Log("서버 데이터를 내보냈습니다: " +
                  (buildings >= 0 ? $"건물 {buildings}종, " : "건물은 수확 설정을 읽지 못해 기존 파일 유지, ") +
                  $"자원 노드 {nodes}종, 보상 장식물 {decorations}종, " +
                  (areas >= 0 ? $"채집 타이머 장소 {areas}곳 " : "채집 타이머는 읽지 못해 기존 파일 유지 ") +
                  "(Server/data, 서버에 올리고 재시작해야 반영됩니다)");
    }

    // ---------- buildings.json ----------

    private static int ExportBuildings()
    {
        // 건물 종류(buildingID)별 BuildingData 와, 그 종류의 프리팹에 붙어 있는 BuildingHarvest (없으면 등록되지 않음)
        var datas = new SortedDictionary<string, BuildingData>(System.StringComparer.Ordinal);
        var harvestOnPrefab = new Dictionary<string, BuildingHarvest>();
        foreach (GameObject prefab in AllPrefabs())
        {
            Building building = prefab.GetComponent<Building>();
            if (building == null || building.Data == null || string.IsNullOrEmpty(building.Data.buildingID))
                continue;

            string id = building.Data.buildingID;
            if (!datas.ContainsKey(id))
                datas.Add(id, building.Data);
            else if (datas[id] != building.Data)
                Debug.LogWarning($"건물 종류 '{id}' 를 쓰는 BuildingData 가 여러 개입니다. {AssetDatabase.GetAssetPath(prefab)} 의 값은 내보내지 않습니다.");
            BuildingHarvest harvest = prefab.GetComponent<BuildingHarvest>();
            if (harvest != null && !harvestOnPrefab.ContainsKey(id))
                harvestOnPrefab.Add(id, harvest);
        }

        // 프리팹에 BuildingHarvest 가 없는 건물은 지을 때 BuildingPlacement 가 붙이므로, 그때 쓰이는 스크립트의 기본값을 읽는다
        var holder = new GameObject("BuildingHarvest defaults") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            HarvestSettings defaults = ReadHarvest(holder.AddComponent<BuildingHarvest>());
            if (defaults == null)
            {
                Debug.LogError("BuildingHarvest 의 수확 설정(harvestInterval, defaultRewardType, defaultRewardAmount)을 읽지 못했습니다. " +
                               "필드 이름이 바뀌었다면 ServerDataExporter.ReadHarvest 를 고쳐 주세요. buildings.json 은 그대로 둡니다.");
                return -1;
            }

            var entries = new List<string>();
            foreach (BuildingData data in datas.Values)
            {
                bool decoration = data.buildingType == BuildingType.Decoration;
                var cost = new SortedDictionary<string, int>(System.StringComparer.Ordinal);
                foreach (ResourceAmount amount in data.constructionCosts ?? new ResourceAmount[0])
                    Add(cost, amount.resourceType, amount.amount);

                // 수확: BuildingHarvest.SetupRewards 와 같은 규칙 (생산 목록, 비어 있으면 기본값). 장식물은 수확하지 않는다
                var harvest = new SortedDictionary<string, int>(System.StringComparer.Ordinal);
                float harvestSeconds = 0f;
                if (!decoration)
                {
                    HarvestSettings settings = harvestOnPrefab.TryGetValue(data.buildingID, out BuildingHarvest onPrefab)
                        ? ReadHarvest(onPrefab) ?? defaults
                        : defaults;
                    foreach (ResourceProduction p in data.productions ?? new ResourceProduction[0])
                    {
                        if (p != null) Add(harvest, p.resourceType, p.amount);
                    }
                    if (harvest.Count == 0) Add(harvest, settings.defaultType, settings.defaultAmount);
                    if (harvest.Count > 0) harvestSeconds = settings.interval;
                }

                var sb = new StringBuilder();
                sb.Append($"  {Quote(data.buildingID)}: {{\n");
                sb.Append($"    \"name\": {Quote(data.buildingName)},\n");
                sb.Append($"    \"width\": {data.width},\n");
                sb.Append($"    \"height\": {data.height},\n");
                sb.Append($"    \"cost\": {Amounts(cost)},\n");
                sb.Append($"    \"harvest\": {Amounts(harvest)},\n");
                sb.Append($"    \"harvestSeconds\": {Number(harvestSeconds)}");
                // 장식물: 서버가 자원 대신 같은 이름의 장식 아이템 1개를 쓰게 한다 (DecorationInventory 와 같은 규칙)
                if (decoration) sb.Append(",\n    \"decoration\": true");
                // 특수 건물: 장식과 함께 집 안에 놓을 수 있다 (서버 buildings.js 가 집 안 건설을 검사)
                if (data.buildingType == BuildingType.Special) sb.Append(",\n    \"interior\": true");
                // 마을당 개수 제한 (예: 플레이어 집 1개). 서버 buildings.js 가 건설할 때 검사한다
                if (data.maxPerVillage > 0) sb.Append($",\n    \"maxPerVillage\": {data.maxPerVillage}");
                // 집 안 전용 장식 (침대·난로 같은 실내 가구). 서버 buildings.js 가 마을에 놓지 못하게 검사한다
                if (data.houseOnly) sb.Append(",\n    \"houseOnly\": true");
                sb.Append("\n  }");
                entries.Add(sb.ToString());
            }

            Write(BuildingsPath, "{\n" + string.Join(",\n", entries) + "\n}\n");
            return entries.Count;
        }
        finally
        {
            Object.DestroyImmediate(holder);
        }
    }

    // BuildingHarvest 의 수확 설정
    private class HarvestSettings
    {
        public float interval;           // 수확 주기 (초)
        public ResourceType defaultType; // 생산 목록이 없는 건물이 받는 자원
        public int defaultAmount;        //   과 그 수량
    }

    // BuildingHarvest 에 저장된 수확 설정을 읽는다 (private 필드라 SerializedObject 로). 필드를 찾지 못하면 null
    private static HarvestSettings ReadHarvest(BuildingHarvest harvest)
    {
        var serialized = new SerializedObject(harvest);
        SerializedProperty interval = serialized.FindProperty("harvestInterval");
        SerializedProperty type = serialized.FindProperty("defaultRewardType");
        SerializedProperty amount = serialized.FindProperty("defaultRewardAmount");
        if (interval == null || type == null || amount == null) return null;

        return new HarvestSettings
        {
            interval = interval.floatValue,
            defaultType = (ResourceType)System.Enum.Parse(typeof(ResourceType), type.enumNames[type.enumValueIndex]),
            defaultAmount = amount.intValue,
        };
    }

    // ---------- gathering.json ----------

    private static void ExportGathering(out int nodeCount, out int decorationCount)
    {
        // 자원 노드 종류 = 자원 프리팹 이름 (GatherSync 가 캔 노드의 프리팹 이름을 서버에 보낸다)
        Dictionary<string, int> spawnCounts = CountSpawnPoints();
        var nodes = new SortedDictionary<string, string>(System.StringComparer.Ordinal);
        foreach (GameObject prefab in AllPrefabs())
        {
            ResourceNode node = prefab.GetComponent<ResourceNode>();
            if (node == null) continue;

            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab));
            int count = spawnCounts.TryGetValue(guid, out int n) ? n : 0;
            if (count == 0)
            {
                Debug.LogWarning($"자원 프리팹 '{prefab.name}' 을 쓰는 ResourceSpawnPoint 가 어느 씬에도 없어 내보내지 않습니다.");
                continue;
            }
            nodes[prefab.name] = "{ " +
                $"\"item\": {Quote(node.Kind.ToString().ToLowerInvariant())}, " +
                $"\"amount\": {node.RewardAmount}, " +
                $"\"gatherSeconds\": {Number(node.GatherDuration)}, " +
                $"\"respawnSeconds\": {Number(node.RespawnDuration)}, " +
                $"\"count\": {count} }}";
        }

        // 채집 타이머 보상으로 나올 수 있는 장식물: DecorationDatabase 에 등록된 것 (GatheringTimer 와 같은 조건)
        var decorations = new SortedSet<string>(System.StringComparer.Ordinal);
        foreach (string guid in AssetDatabase.FindAssets("t:DecorationDatabase"))
        {
            var database = AssetDatabase.LoadAssetAtPath<DecorationDatabase>(AssetDatabase.GUIDToAssetPath(guid));
            if (database == null || database.decorations == null) continue;
            foreach (DecorationDatabase.DecorationEntry entry in database.decorations)
            {
                if (entry == null || entry.buildingData == null || entry.buildingPrefab == null) continue;
                if (entry.buildingData.buildingType != BuildingType.Decoration) continue;
                Building building = entry.buildingPrefab.GetComponent<Building>();
                if (building == null || building.Data != entry.buildingData) continue;
                decorations.Add(entry.buildingData.buildingID);
            }
        }

        var sb = new StringBuilder();
        sb.Append("{\n  \"nodes\": {\n");
        sb.Append(string.Join(",\n", nodes.Select(e => $"    {Quote(e.Key)}: {e.Value}")));
        sb.Append("\n  },\n  \"decorations\": [");
        sb.Append(string.Join(", ", decorations.Select(Quote)));
        sb.Append("]\n}\n");
        Write(GatheringPath, sb.ToString());
        nodeCount = nodes.Count;
        decorationCount = decorations.Count;
    }

    // 자원 프리팹(guid)별로, 한 씬에 그 프리팹을 만드는 ResourceSpawnPoint 가 가장 많을 때의 개수
    //   서버가 "노드 수만큼, 재생성 시간마다" 로 채집 횟수를 제한하는 데 쓴다. 씬을 열지 않고 씬 파일의 글자에서 찾는다
    private static Dictionary<string, int> CountSpawnPoints()
    {
        var result = new Dictionary<string, int>();
        string[] scripts = AssetDatabase.FindAssets("ResourceSpawnPoint t:MonoScript");
        if (scripts.Length == 0) return result;
        string scriptGuid = scripts[0];
        var prefabReference = new Regex(@"resourcePrefab: \{fileID: \d+, guid: ([0-9a-f]{32})");

        foreach (string sceneGuid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
        {
            string text = File.ReadAllText(AssetDatabase.GUIDToAssetPath(sceneGuid));
            var inScene = new Dictionary<string, int>();
            foreach (string block in text.Split(new[] { "--- !u!" }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                if (!block.Contains("guid: " + scriptGuid)) continue;
                Match match = prefabReference.Match(block);
                if (!match.Success) continue;
                string prefabGuid = match.Groups[1].Value;
                inScene[prefabGuid] = (inScene.TryGetValue(prefabGuid, out int n) ? n : 0) + 1;
            }
            foreach (KeyValuePair<string, int> pair in inScene)
            {
                if (!result.TryGetValue(pair.Key, out int best) || pair.Value > best) result[pair.Key] = pair.Value;
            }
        }
        return result;
    }

    // ---------- expeditions.json ----------

    // 채집 타이머의 장소별 시간과 보상은 에셋이 아니라 코드에 숫자로 적혀 있어서, 그 코드의 글자에서 읽어 온다
    //   GatheringTimerUI.cs : OnLocationClicked("마당", 10f)                                    -> 채집 시간(초)
    //   GatheringTimer.cs   : GetGoldReward, GetDecorationChance 의 case "마당": return 100;     -> 골드, 장식물 확률
    //   코드 모양이 달라져서 읽지 못하면 경고만 남기고 기존 파일은 그대로 둔다 (그때는 expeditions.json 을 손으로 고친다)
    //   -> 내보낸 장소 수, 읽지 못했으면 -1
    private static int ExportExpeditions()
    {
        string timer = ReadScript("GatheringTimer");
        string timerUI = ReadScript("GatheringTimerUI");
        if (timer == null || timerUI == null)
        {
            Debug.LogWarning("GatheringTimer.cs / GatheringTimerUI.cs 를 읽지 못해 채집 타이머 데이터는 내보내지 않습니다.");
            return -1;
        }

        var seconds = new List<KeyValuePair<string, float>>();
        foreach (Match m in Regex.Matches(timerUI, @"OnLocationClicked\s*\(\s*""([^""]+)""\s*,\s*([0-9]+(?:\.[0-9]+)?)f?\s*\)"))
            seconds.Add(new KeyValuePair<string, float>(m.Groups[1].Value, ParseNumber(m.Groups[2].Value)));
        Dictionary<string, float> gold = ReadSwitch(timer, "GetGoldReward");
        Dictionary<string, float> chance = ReadSwitch(timer, "GetDecorationChance");

        // 세 곳에 적힌 장소가 서로 같아야 한다
        var problems = new List<string>();
        if (seconds.Count == 0) problems.Add("GatheringTimerUI.cs 에서 채집 시간을 찾지 못했습니다");
        if (seconds.Select(s => s.Key).Distinct().Count() != seconds.Count) problems.Add("같은 장소의 채집 시간이 두 번 나옵니다");
        foreach (KeyValuePair<string, float> area in seconds)
        {
            if (area.Value <= 0f) problems.Add($"'{area.Key}' 의 채집 시간이 0 이하입니다");
            if (!gold.ContainsKey(area.Key)) problems.Add($"'{area.Key}' 의 골드 보상이 GetGoldReward 에 없습니다");
            if (!chance.ContainsKey(area.Key)) problems.Add($"'{area.Key}' 의 장식물 확률이 GetDecorationChance 에 없습니다");
            else if (chance[area.Key] < 0f || chance[area.Key] > 1f) problems.Add($"'{area.Key}' 의 장식물 확률이 0~1 을 벗어납니다");
        }
        foreach (string area in gold.Keys.Concat(chance.Keys).Distinct())
        {
            if (seconds.All(s => s.Key != area)) problems.Add($"'{area}' 는 보상만 있고 GatheringTimerUI.cs 에 채집 버튼이 없습니다");
        }
        if (problems.Count > 0)
        {
            Debug.LogWarning("채집 타이머 코드를 읽지 못해 Server/data/expeditions.json 은 그대로 둡니다 (직접 고쳐 주세요):\n" + string.Join("\n", problems));
            return -1;
        }

        var lines = seconds.Select(area =>
            $"  {Quote(area.Key)}: {{ \"seconds\": {Number(area.Value)}, \"gold\": {Mathf.RoundToInt(gold[area.Key])}, " +
            $"\"decorationChance\": {Number(chance[area.Key])} }}");
        Write(ExpeditionsPath, "{\n" + string.Join(",\n", lines) + "\n}\n");
        return seconds.Count;
    }

    // 코드에서 method(string ...) 본문의 case "이름": return 값; 들
    private static Dictionary<string, float> ReadSwitch(string code, string method)
    {
        var values = new Dictionary<string, float>();
        Match declaration = Regex.Match(code, @"\b" + method + @"\s*\(\s*string\b"); // 호출이 아니라 선언
        int open = declaration.Success ? code.IndexOf('{', declaration.Index) : -1;
        if (open < 0) return values;

        int depth = 0;
        int end = open;
        for (; end < code.Length; end++)
        {
            if (code[end] == '{') depth++;
            else if (code[end] == '}' && --depth == 0) break;
        }
        string body = code.Substring(open, end - open);
        foreach (Match m in Regex.Matches(body, @"case\s+""([^""]+)""\s*:\s*return\s+([0-9]+(?:\.[0-9]+)?)f?\s*;"))
            values[m.Groups[1].Value] = ParseNumber(m.Groups[2].Value);
        return values;
    }

    // 스크립트 파일의 글자 (주석은 뺌). 팀 스크립트는 CP949, 아니면 UTF-8
    private static string ReadScript(string className)
    {
        foreach (string guid in AssetDatabase.FindAssets(className + " t:MonoScript", new[] { "Assets/Scripts" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) != className) continue;

            byte[] bytes = File.ReadAllBytes(path);
            string text;
            try
            {
                text = new UTF8Encoding(false, true).GetString(bytes); // UTF-8 로 읽을 수 없는 글자가 있으면 예외
            }
            catch (System.ArgumentException)
            {
                try
                {
                    text = Encoding.GetEncoding(949).GetString(bytes);
                }
                catch (System.Exception)
                {
                    return null;
                }
            }
            return Regex.Replace(text, @"//[^\n]*", ""); // 주석에 적힌 예전 숫자를 읽지 않게
        }
        return null;
    }

    private static float ParseNumber(string text)
    {
        return float.Parse(text, CultureInfo.InvariantCulture);
    }

    // ---------- 공통 ----------

    private static IEnumerable<GameObject> AllPrefabs()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab != null) yield return prefab;
        }
    }

    // 자원 이름은 서버와 맞춰 소문자 (ResourceType.Wood -> wood). 같은 자원이 여러 번 나오면 더한다
    private static void Add(SortedDictionary<string, int> amounts, ResourceType type, int amount)
    {
        if (amount <= 0) return;
        string key = type.ToString().ToLowerInvariant();
        amounts[key] = (amounts.TryGetValue(key, out int current) ? current : 0) + amount;
    }

    private static string Amounts(SortedDictionary<string, int> amounts)
    {
        if (amounts.Count == 0) return "{}";
        return "{ " + string.Join(", ", amounts.Select(a => $"{Quote(a.Key)}: {a.Value}")) + " }";
    }

    private static string Number(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string Quote(string text)
    {
        return "\"" + (text ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    private static void Write(string relativePath, string json)
    {
        string fullPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        File.WriteAllText(fullPath, json, new UTF8Encoding(false));
    }
}
