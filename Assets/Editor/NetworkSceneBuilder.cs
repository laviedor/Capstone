using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 네트워크 씬(SampleScene)을 팀의 마을 씬(Village)에 맞춰 다시 만든다: 메뉴 Tools > Rebuild Network Scene From Village
//   팀이 만드는 Village.unity 는 건드리지 않는다. 그 복사본에, 지금 SampleScene 에 있는 네트워크 오브젝트
//   (LoginCanvas, Network, HUDCanvas, DialogCanvas, 내 캐릭터의 이름표와 LocalPlayerSync)를 옮겨 붙이고
//   마을 쪽 오브젝트를 가리키던 연결(플레이어, 건물 설치, 그리드 등)을 새 씬의 같은 오브젝트로 바꾼 뒤 SampleScene.unity 로 저장한다
//   main 을 병합해서 Village 씬이 바뀌면 이 메뉴를 다시 실행하면 된다 (네트워크 쪽 UI 는 SampleScene 에서 고친 그대로 유지된다)
public static class NetworkSceneBuilder
{
    private const string VillagePath = "Assets/Scenes/Village.unity";
    private const string NetworkPath = "Assets/Scenes/SampleScene.unity";
    private const string TempPath = "Assets/Scenes/SampleScene_rebuild.unity";

    // SampleScene 에서 통째로 옮겨 올 네트워크 오브젝트 (씬 맨 위에 있는 것)
    private static readonly string[] NetworkRoots = { "LoginCanvas", "Network", "HUDCanvas", "DialogCanvas" };

    // 게임이 이름으로 불러오는 씬: 빌드 목록에 있어야 불러올 수 있다 (FieldSceneManager, HouseSceneManager)
    private static readonly string[] LoadedByName =
    {
        "Assets/Scenes/ResourceField.unity",
        "Assets/Scenes/HouseInteriorScene.unity",
    };

    [MenuItem("Tools/Rebuild Network Scene From Village")]
    public static void Rebuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Play 모드에서는 실행할 수 없습니다.");
            return;
        }
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty)
            {
                Debug.LogError("저장하지 않은 씬이 있습니다. 먼저 저장하거나 되돌린 뒤 다시 실행해 주세요.");
                return;
            }
        }

        AssetDatabase.DeleteAsset(TempPath);
        if (!AssetDatabase.CopyAsset(VillagePath, TempPath))
        {
            Debug.LogError("Village 씬을 복사하지 못했습니다: " + VillagePath);
            return;
        }

        Scene oldScene = EditorSceneManager.OpenScene(NetworkPath, OpenSceneMode.Single);
        Scene newScene = EditorSceneManager.OpenScene(TempPath, OpenSceneMode.Additive);
        var problems = new List<string>();
        try
        {
            Build(oldScene, newScene, problems);
        }
        catch (System.Exception e)
        {
            problems.Add(e.ToString());
        }

        if (problems.Count > 0)
        {
            // 하나라도 맞추지 못했으면 아무것도 저장하지 않고 원래 SampleScene 으로 돌아간다
            EditorSceneManager.OpenScene(NetworkPath, OpenSceneMode.Single);
            AssetDatabase.DeleteAsset(TempPath);
            Debug.LogError("네트워크 씬을 다시 만들지 못했습니다 (SampleScene 은 그대로입니다):\n" + string.Join("\n", problems));
            return;
        }

        SceneManager.SetActiveScene(newScene);
        EditorSceneManager.CloseScene(oldScene, true); // 예전 SampleScene 은 저장하지 않고 닫는다
        EditorSceneManager.SaveScene(newScene, NetworkPath);
        AssetDatabase.DeleteAsset(TempPath);
        UpdateBuildSettings();
        Debug.Log("네트워크 씬(SampleScene)을 Village 씬에 맞춰 다시 만들었습니다.");
    }

    private static void Build(Scene oldScene, Scene newScene, List<string> problems)
    {
        GameObject oldPlayer = FindRoot(oldScene, "Player");
        GameObject newPlayer = FindRoot(newScene, "Player");
        if (oldPlayer == null || newPlayer == null)
        {
            problems.Add("두 씬에서 Player 오브젝트를 찾지 못했습니다.");
            return;
        }

        // ---- 1) 네트워크 오브젝트를 새 씬으로 옮김
        var moved = new List<GameObject>();
        foreach (string name in NetworkRoots)
        {
            GameObject root = FindRoot(oldScene, name);
            if (root == null)
            {
                problems.Add("SampleScene 에 " + name + " 오브젝트가 없습니다.");
                return;
            }
            SceneManager.MoveGameObjectToScene(root, newScene);
            moved.Add(root);
        }

        // 내 캐릭터 머리 위 이름표 (Player 의 자식), 좌표 전송 스크립트
        Transform label = oldPlayer.transform.Find("NameLabel");
        if (label != null)
        {
            Vector3 position = label.localPosition;
            Quaternion rotation = label.localRotation;
            Vector3 scale = label.localScale;
            label.SetParent(null, false);
            SceneManager.MoveGameObjectToScene(label.gameObject, newScene);
            label.SetParent(newPlayer.transform, false);
            label.localPosition = position;
            label.localRotation = rotation;
            label.localScale = scale;
            moved.Add(label.gameObject);
        }
        if (newPlayer.GetComponent<LocalPlayerSync>() == null)
        {
            LocalPlayerSync sync = newPlayer.AddComponent<LocalPlayerSync>();
            LocalPlayerSync oldSync = oldPlayer.GetComponent<LocalPlayerSync>();
            if (oldSync != null) EditorUtility.CopySerialized(oldSync, sync);
        }

        // ---- 2) 예전 씬의 마을 쪽 오브젝트를 가리키던 연결을 새 씬의 같은 오브젝트로
        foreach (GameObject root in moved)
        {
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component is Transform) continue;

                var serialized = new SerializedObject(component);
                SerializedProperty property = serialized.GetIterator();
                bool changed = false;
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    Object target = property.objectReferenceValue;
                    GameObject targetObject = target is GameObject go ? go : target is Component c ? c.gameObject : null;
                    if (targetObject == null || targetObject.scene != oldScene) continue; // 에셋이거나 같이 옮겨 온 오브젝트

                    Object counterpart = FindCounterpart(target, targetObject, newScene);
                    if (counterpart == null)
                    {
                        problems.Add($"{component.GetType().Name}.{property.propertyPath} ({PathOf(component.transform)}) 가 가리키는 " +
                                     $"{target.GetType().Name} '{PathOf(targetObject.transform)}' 를 Village 씬에서 찾지 못했습니다.");
                        continue;
                    }
                    property.objectReferenceValue = counterpart;
                    changed = true;
                }
                if (changed) serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        if (problems.Count > 0) return;

        // ---- 3) Village 씬에 새로 생긴 것들에 맞춘 연결
        GameObject network = moved.First(go => go.name == "Network");
        GameObject hud = moved.First(go => go.name == "HUDCanvas");
        GameObject login = moved.First(go => go.name == "LoginCanvas");

        // 채집을 서버와 맞추는 스크립트
        if (network.GetComponent<GatherSync>() == null)
        {
            GatherSync gather = network.AddComponent<GatherSync>();
            SetReference(gather, "chatClient", network.GetComponent<ChatClient>());
        }

        // 건물 종류를 빠짐없이 등록 (서버에서 받은 건물을 화면에 만들 때, 가진 장식물 수를 맞출 때 쓴다)
        RegisterBuildingPrefabs(network.GetComponent<BuildingSync>(), newScene);

        // 집 드나들기(HouseSceneManager, 집 프리팹의 HouseInteraction)는 플레이어가 연결되어 있어야 동작한다
        // Village 씬에서 이 칸이 비어 있으면 F 키로 집에 들어갈 수 없으므로 여기서 채운다
        foreach (HouseSceneManager house in FindInScene<HouseSceneManager>(newScene))
            SetReferenceIfEmpty(house, "player", newPlayer.transform);

        // Tab 으로 여는 인벤토리: 로그인 전과 글자를 치는 동안에는 열리지 않게
        InventoryUI inventory = FindInScene<InventoryUI>(newScene).FirstOrDefault(i => i.gameObject.activeInHierarchy);
        if (inventory != null)
        {
            AddToArray(login.GetComponent<LoginUI>(), "disableUntilLogin", inventory);
            AddToArray(hud.GetComponent<TypingGuard>(), "hotkeyScripts", inventory);
        }

        // 테스트용 장식물 지급(T 키)은 서버가 모르는 장식물을 만들어 내므로 네트워크 씬에서는 끈다
        foreach (DecorationInventoryTester tester in FindInScene<DecorationInventoryTester>(newScene))
            tester.gameObject.SetActive(false);
    }

    // 예전 씬의 오브젝트에 해당하는 새 씬의 오브젝트: 같은 경로의 같은 종류, 없으면 그 종류가 새 씬에 하나뿐일 때 그것
    private static Object FindCounterpart(Object target, GameObject targetObject, Scene newScene)
    {
        Transform match = FindByPath(newScene, PathOf(targetObject.transform));
        if (target is GameObject) return match != null ? match.gameObject : null;

        System.Type type = target.GetType();
        if (match != null)
        {
            Component component = match.GetComponent(type);
            if (component != null) return component;
        }

        var candidates = new List<Component>();
        foreach (GameObject root in newScene.GetRootGameObjects())
            candidates.AddRange(root.GetComponentsInChildren(type, true));
        var active = candidates.Where(c => c.gameObject.activeInHierarchy).ToList();
        if (active.Count == 1) return active[0];
        return candidates.Count == 1 ? candidates[0] : null;
    }

    // 같은 건물 ID 를 쓰는 프리팹이 여럿일 수 있다 (예: 집은 'Player House' 와 예전 '1x1').
    // 이미 등록된 것은 그대로 두고, 새로 등록할 때는 팀이 실제로 쓰는 프리팹(건설 목록, 장식물 목록)을 먼저 고른다
    private static void RegisterBuildingPrefabs(BuildingSync sync, Scene newScene)
    {
        var serialized = new SerializedObject(sync);
        SerializedProperty prefabs = serialized.FindProperty("buildingPrefabs");
        var known = new HashSet<string>();
        for (int i = 0; i < prefabs.arraySize; i++)
        {
            var building = prefabs.GetArrayElementAtIndex(i).objectReferenceValue as Building;
            if (building != null) known.Add(building.BuildingID);
        }

        var candidates = new List<Building>();
        foreach (ConstructionListUI list in FindInScene<ConstructionListUI>(newScene))
            candidates.AddRange(ReferencedBuildingPrefabs(list));
        foreach (string guid in AssetDatabase.FindAssets("t:DecorationDatabase"))
            candidates.AddRange(ReferencedBuildingPrefabs(AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(guid))));
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefab" }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab != null && prefab.GetComponent<Building>() != null) candidates.Add(prefab.GetComponent<Building>());
        }

        foreach (Building building in candidates)
        {
            if (string.IsNullOrEmpty(building.BuildingID) || !known.Add(building.BuildingID)) continue;
            prefabs.arraySize++;
            prefabs.GetArrayElementAtIndex(prefabs.arraySize - 1).objectReferenceValue = building;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // 이 오브젝트(컴포넌트, 에셋)에 연결되어 있는 건물 프리팹들
    private static List<Building> ReferencedBuildingPrefabs(Object owner)
    {
        var found = new List<Building>();
        if (owner == null) return found;

        SerializedProperty property = new SerializedObject(owner).GetIterator();
        while (property.Next(true))
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
            Object target = property.objectReferenceValue;
            GameObject prefab = target is GameObject go ? go : target is Component c ? c.gameObject : null;
            if (prefab == null || !EditorUtility.IsPersistent(prefab)) continue; // 씬 안의 오브젝트가 아니라 프리팹 에셋만

            Building building = prefab.GetComponent<Building>();
            if (building != null && !found.Contains(building)) found.Add(building);
        }
        return found;
    }

    // 빌드 목록: 네트워크 씬을 맨 앞에 (로그아웃, 회원 탈퇴가 빌드 번호로 이 씬을 다시 불러온다), 이름으로 불러오는 씬 추가
    private static void UpdateBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(NetworkPath, true) };
        scenes.AddRange(EditorBuildSettings.scenes.Where(s => s.path != NetworkPath));
        foreach (string path in LoadedByName)
        {
            if (scenes.All(s => s.path != path) && AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                scenes.Add(new EditorBuildSettingsScene(path, true));
        }
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        return scene.GetRootGameObjects().FirstOrDefault(go => go.name == name);
    }

    private static Transform FindByPath(Scene scene, string path)
    {
        int slash = path.IndexOf('/');
        string rootName = slash < 0 ? path : path.Substring(0, slash);

        // 이름이 같은 최상위 오브젝트가 여럿일 수 있다 (Village 씬에는 Canvas 가 둘): 그 아래에 경로가 있는 것을 고른다
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name != rootName) continue;
            Transform found = slash < 0 ? root.transform : root.transform.Find(path.Substring(slash + 1));
            if (found != null) return found;
        }
        return null;
    }

    private static string PathOf(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }

    private static IEnumerable<T> FindInScene<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));
    }

    private static void SetReference(Object component, string property, Object value)
    {
        var serialized = new SerializedObject(component);
        serialized.FindProperty(property).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // 비어 있는 연결만 채운다 (팀이 Village 씬에서 직접 연결해 두었으면 그대로 둔다)
    private static void SetReferenceIfEmpty(Object component, string property, Object value)
    {
        var serialized = new SerializedObject(component);
        SerializedProperty reference = serialized.FindProperty(property);
        if (reference == null || reference.objectReferenceValue != null) return;
        reference.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddToArray(Object component, string property, Object value)
    {
        if (component == null || value == null) return;
        var serialized = new SerializedObject(component);
        SerializedProperty array = serialized.FindProperty(property);
        for (int i = 0; i < array.arraySize; i++)
        {
            if (array.GetArrayElementAtIndex(i).objectReferenceValue == value) return;
        }
        array.arraySize++;
        array.GetArrayElementAtIndex(array.arraySize - 1).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
