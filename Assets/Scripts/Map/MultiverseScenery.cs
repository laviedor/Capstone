using System.Collections.Generic;
using UnityEngine;

// 광장(모두가 같이 쓰는 공용 공간)의 고정 풍경: 분수 광장 바닥, 마을회관, 시장 거리, 농장·목장 소품.
//   광장에 있을 때만 보이고, 내 마을·친구 마을에서는 숨는다.
//   (마을 화면 묶음 VillageVisualRoot 아래에 있어서 필드나 집 안에 들어가도 함께 숨는다)
//   풍경이 차지하는 칸(건물 자리, 분수)은 길찾기에서 막힌 칸으로 등록해 캐릭터가 지나가지 않게 한다.
//   메뉴 Tools > Cozy UI > 광장 에셋 가져오기 · 꾸미기 가 만든다 (MultiverseBuilder).
public class MultiverseScenery : MonoBehaviour
{
    // 보이고 숨길 풍경 묶음
    [SerializeField] private GameObject content;

    // 풍경이 차지하는 칸 (마을 그리드 기준)
    [SerializeField] private List<Vector3Int> blockedCells = new List<Vector3Int>();

    private BuildingPlacement placement;
    private bool shown;

    private void Awake()
    {
        if (content != null)
            content.SetActive(false);
    }

    private void OnDisable()
    {
        // 마을 화면이 숨는 동안(필드·집 안)에는 그대로 두고, 이 오브젝트가 사라질 때만 칸을 돌려준다
        if (!gameObject.scene.isLoaded)
            SetShown(false);
    }

    private void Update()
    {
        WorldSync world = WorldSync.Instance;
        SetShown(world != null && world.Entered && world.InMultiverse);
    }

    private void SetShown(bool show)
    {
        if (show == shown)
            return;

        shown = show;

        if (content != null)
            content.SetActive(show);

        if (placement == null)
            placement = FindFirstObjectByType<BuildingPlacement>();

        if (placement != null)
            placement.SetSceneryCells(blockedCells, show);

        // 플레이어가 막힌 칸(예: 분수) 위에 서 있으면 가까운 빈칸으로 옮긴다
        BuildingSync.NotifyLayoutChanged();
    }
}
