using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 메인 건물의 수확 기능.
/// 설치 후 일정 시간(기본 30초)이 지나면 건물 위에 수확 버튼을 띄우고,
/// 버튼을 누르면 자원을 지급한 뒤 다시 타이머를 시작한다.
/// 지급 자원은 BuildingData의 생산 목록(productions)을 따른다. (예: Blacksmith = 돌 10)
/// 생산 목록이 비어 있으면 기본값(골드 10)을 지급한다.
/// 장식이 아닌 건물을 설치하면 BuildingPlacement가 자동으로 붙인다.
/// </summary>
[RequireComponent(typeof(Building))]
public class BuildingHarvest : MonoBehaviour
{
    [Header("수확 설정")]
    // 수확 버튼이 다시 뜨기까지 걸리는 시간(초)
    [SerializeField] private float harvestInterval = 30f;
    // BuildingData에 생산 목록이 없을 때 지급할 자원 종류
    [SerializeField] private ResourceType defaultRewardType = ResourceType.Gold;
    // BuildingData에 생산 목록이 없을 때 지급할 자원 수량
    [SerializeField] private int defaultRewardAmount = 10;
    // 켜면 같은 건물의 자동 생산(BuildingProduction)을 끄고 수확 버튼으로만 자원을 받는다
    [SerializeField] private bool replaceAutoProduction = true;

    [Header("버튼 모양")]
    // 버튼 크기(월드 단위)
    [SerializeField] private Vector2 buttonSize = new Vector2(1.2f, 0.7f);
    // 건물 위쪽 끝과 버튼 사이 간격
    [SerializeField] private float buttonMargin = 0.3f;
    // 버튼 배경 색
    [SerializeField] private Color buttonColor = new Color(1f, 0.82f, 0.25f, 1f);
    // 버튼 글자 색
    [SerializeField] private Color textColor = new Color(0.3f, 0.18f, 0.05f, 1f);
    // 버튼 정렬 순서 (건물보다 위)
    [SerializeField] private int buttonSortingOrder = 50;
    // 버튼 글꼴 (비어 있으면 프로젝트의 한글 글꼴 D2Coding을 자동으로 찾는다)
    [SerializeField] private TMP_FontAsset labelFont;

    // 같은 오브젝트의 건물 정보
    private Building building;
    // 자원을 지급할 대상
    private PlayerResources playerResources;
    // 마지막 수확 이후 지난 시간
    private float timer;
    // 수확 가능 상태(버튼 표시 중) 여부
    private bool isReady;
    // 생성한 수확 버튼 오브젝트 (건물 크기 배율의 영향을 받지 않도록 건물 밖에 둔다)
    private GameObject buttonObject;
    // 수확 시 지급할 자원 목록
    private readonly List<ResourceAmount> rewards = new List<ResourceAmount>();

    // 버튼 배경용 흰색 스프라이트 (모든 건물이 공유)
    private static Sprite whiteSprite;
    // 찾은 한글 글꼴 (모든 건물이 공유)
    private static TMP_FontAsset cachedFont;

    // 수확 가능 여부
    public bool IsReady => isReady;
    // 다음 수확까지 남은 시간(초)
    public float RemainingTime => Mathf.Max(0f, harvestInterval - timer);

    /// <summary>수확으로 대체할 경우 기존 자동 생산을 끈다.</summary>
    private void Awake()
    {
        if (replaceAutoProduction)
        {
            BuildingProduction autoProduction = GetComponent<BuildingProduction>();

            if (autoProduction != null)
                autoProduction.enabled = false;
        }
    }

    /// <summary>필요한 참조를 찾고 수확 버튼을 만든 뒤 타이머를 시작한다.</summary>
    private void Start()
    {
        building = GetComponent<Building>();

        SetupRewards();

        playerResources = FindFirstObjectByType<PlayerResources>();

        if (playerResources == null)
        {
            Debug.LogError("PlayerResources를 찾을 수 없어 수확 기능을 끕니다.");
            enabled = false;
            return;
        }

        CreateButton();

        timer = 0f;
        SetReady(false);
    }

    /// <summary>설치된 건물만 시간을 누적하고, 시간이 다 되면 수확 버튼을 띄운다.</summary>
    private void Update()
    {
        if (building == null || !building.IsPlaced || isReady)
            return;

        timer += Time.deltaTime;

        if (timer >= harvestInterval)
        {
            SetReady(true);
        }
    }

    /// <summary>버튼을 건물 위에 두고 살짝 위아래로 움직여 눈에 띄게 한다.</summary>
    private void LateUpdate()
    {
        if (isReady)
        {
            UpdateButtonPosition();
        }
    }

    /// <summary>자원을 지급하고 버튼을 숨긴 뒤 타이머를 다시 시작한다.</summary>
    public void Harvest()
    {
        if (!isReady || playerResources == null)
            return;

        // 수확은 서버에 알려야 한다 (재화는 서버가 계산한다). 알리지 못하면 수확하지 않는다
        if (BuildingSync.Instance != null && !BuildingSync.Instance.OnLocalHarvest(this))
            return;

        string buildingName =
            building != null && building.Data != null
                ? building.Data.buildingName
                : gameObject.name;

        foreach (ResourceAmount reward in rewards)
        {
            playerResources.AddResource(reward.resourceType, reward.amount);

            Debug.Log(buildingName + " 수확 : " + reward.resourceType + " +" + reward.amount);
        }

        timer = 0f;
        SetReady(false);
    }

    /// <summary>수확까지 남은 시간을 맞춘다. 서버가 알려 준 수확 타이머를 반영할 때 쓴다. (BuildingSync 에서 호출)</summary>
    public void SetRemainingTime(float seconds)
    {
        timer = Mathf.Clamp(harvestInterval - seconds, 0f, harvestInterval);
        SetReady(timer >= harvestInterval);
    }

    /// <summary>BuildingData의 생산 목록으로 지급 자원을 정하고, 없으면 기본값을 쓴다.</summary>
    private void SetupRewards()
    {
        rewards.Clear();

        BuildingData data = building != null ? building.Data : null;

        if (data != null && data.productions != null)
        {
            foreach (ResourceProduction production in data.productions)
            {
                if (production == null || production.amount <= 0)
                    continue;

                rewards.Add(new ResourceAmount
                {
                    resourceType = production.resourceType,
                    amount = production.amount
                });
            }
        }

        if (rewards.Count == 0)
        {
            rewards.Add(new ResourceAmount
            {
                resourceType = defaultRewardType,
                amount = defaultRewardAmount
            });
        }
    }

    /// <summary>버튼에 표시할 글자. 예) "수확\n돌 +10"</summary>
    private string GetLabelText()
    {
        List<string> parts = new List<string>();

        foreach (ResourceAmount reward in rewards)
        {
            parts.Add(GetResourceName(reward.resourceType) + " +" + reward.amount);
        }

        return "수확\n" + string.Join(" ", parts);
    }

    /// <summary>자원 종류의 한글 이름.</summary>
    private static string GetResourceName(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Gold:
                return "골드";
            case ResourceType.Stone:
                return "돌";
            case ResourceType.Wood:
                return "나무";
            default:
                return type.ToString();
        }
    }

    /// <summary>수확 가능 상태를 바꾸고 버튼 표시 여부를 맞춘다.</summary>
    private void SetReady(bool ready)
    {
        isReady = ready;

        if (buttonObject != null)
        {
            buttonObject.SetActive(ready && isActiveAndEnabled);

            if (ready)
                UpdateButtonPosition();
        }
    }

    /// <summary>배경, 글자, 클릭 판정용 콜라이더로 구성된 수확 버튼을 만든다.</summary>
    private void CreateButton()
    {
        buttonObject = new GameObject(gameObject.name + "_HarvestButton");

        // 클릭 판정
        BoxCollider2D buttonCollider = buttonObject.AddComponent<BoxCollider2D>();
        buttonCollider.size = buttonSize;

        BuildingHarvestButton harvestButton =
            buttonObject.AddComponent<BuildingHarvestButton>();
        harvestButton.Initialize(this);

        // 배경
        GameObject background = new GameObject("Background");
        background.transform.SetParent(buttonObject.transform, false);
        background.transform.localScale = new Vector3(buttonSize.x, buttonSize.y, 1f);

        SpriteRenderer backgroundRenderer = background.AddComponent<SpriteRenderer>();
        backgroundRenderer.sprite = GetWhiteSprite();
        backgroundRenderer.color = buttonColor;
        backgroundRenderer.sortingOrder = buttonSortingOrder;

        // 글자
        GameObject label = new GameObject("Label");
        label.transform.SetParent(buttonObject.transform, false);

        TextMeshPro text = label.AddComponent<TextMeshPro>();
        string labelText = GetLabelText();

        TMP_FontAsset font = labelFont != null ? labelFont : FindFontFor(labelText);
        if (font != null)
        {
            text.font = font;
            text.fontSharedMaterial = font.material;
        }

        text.text = labelText;

        text.color = textColor;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.enableAutoSizing = true;
        text.fontSizeMin = 0.5f;
        text.fontSizeMax = 4f;
        text.rectTransform.sizeDelta = buttonSize * 0.9f;
        text.sortingOrder = buttonSortingOrder + 1;

        buttonObject.SetActive(false);
    }

    /// <summary>건물 그림 위쪽 끝에 버튼을 두되, 화면 위로 벗어나지 않게 한다.</summary>
    private void UpdateButtonPosition()
    {
        if (buttonObject == null)
            return;

        float centerX = transform.position.x;
        float topY = transform.position.y + 1f;

        SpriteRenderer buildingRenderer = GetComponentInChildren<SpriteRenderer>();
        if (buildingRenderer != null)
        {
            centerX = buildingRenderer.bounds.center.x;
            topY = buildingRenderer.bounds.max.y;
        }

        float bob = Mathf.Sin(Time.time * 3f) * 0.05f;
        float y = topY + buttonMargin + buttonSize.y / 2f + bob;

        // 화면 위쪽 밖으로 나가지 않도록 제한
        Camera cam = Camera.main;
        if (cam != null && cam.orthographic)
        {
            float cameraTop = cam.transform.position.y + cam.orthographicSize;
            y = Mathf.Min(y, cameraTop - buttonSize.y / 2f - 0.05f);
        }

        buttonObject.transform.position = new Vector3(centerX, y, 0f);
    }

    /// <summary>건물이 꺼지면(다른 씬으로 이동 등) 버튼도 숨긴다.</summary>
    private void OnDisable()
    {
        if (buttonObject != null)
            buttonObject.SetActive(false);
    }

    /// <summary>건물이 다시 켜지면 수확 가능 상태에 맞춰 버튼을 다시 보인다.</summary>
    private void OnEnable()
    {
        if (buttonObject != null)
        {
            buttonObject.SetActive(isReady);

            if (isReady)
                UpdateButtonPosition();
        }
    }

    /// <summary>건물이 철거되면 버튼도 함께 제거한다.</summary>
    private void OnDestroy()
    {
        if (buttonObject != null)
            Destroy(buttonObject);
    }

    /// <summary>1x1 월드 크기의 흰색 스프라이트를 만든다.</summary>
    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite == null)
        {
            Texture2D texture = Texture2D.whiteTexture;

            whiteSprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                texture.width
            );
        }

        return whiteSprite;
    }

    // Resources 폴더 안의 한글 글꼴 파일 경로 (Assets/Resources/Fonts/D2CodingBold.ttf)
    private const string RuntimeFontResourcePath = "Fonts/D2CodingBold";

    /// <summary>
    /// 버튼 글자를 실제로 그릴 수 있는 글꼴을 찾는다. (한글이 없는 글꼴로 네모가 뜨는 것 방지)
    /// 1순위: Resources의 D2Coding .ttf로 실행 중에 만든 동적 글꼴 (필요한 한글을 자동 추가)
    /// 2순위: 이미 불러와진 TMP 글꼴 중 글자를 모두 그릴 수 있는 것
    /// </summary>
    private static TMP_FontAsset FindFontFor(string text)
    {
        string required = text.Replace("\n", "").Replace(" ", "");

        if (cachedFont != null && CanRender(cachedFont, required))
            return cachedFont;

        // 1순위: .ttf 원본으로 동적 글꼴 생성
        Font sourceFont = Resources.Load<Font>(RuntimeFontResourcePath);

        if (sourceFont != null)
        {
            TMP_FontAsset runtimeFont = TMP_FontAsset.CreateFontAsset(sourceFont);

            if (runtimeFont != null)
            {
                runtimeFont.name = "D2CodingBold (Runtime)";

                if (CanRender(runtimeFont, required))
                {
                    cachedFont = runtimeFont;
                    Debug.Log("수확 버튼 글꼴 : " + runtimeFont.name);
                    return cachedFont;
                }
            }
        }
        else
        {
            Debug.LogWarning("Resources/" + RuntimeFontResourcePath + " 글꼴 파일을 찾을 수 없습니다.");
        }

        // 2순위: 이미 불러와진 글꼴 중 글자를 그릴 수 있는 것
        foreach (TMP_FontAsset font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            if (font != null && CanRender(font, required))
            {
                cachedFont = font;
                Debug.Log("수확 버튼 글꼴 : " + font.name);
                return cachedFont;
            }
        }

        Debug.LogWarning(
            "수확 버튼 글자를 표시할 한글 글꼴을 찾지 못했습니다. " +
            "BuildingHarvest의 Label Font에 한글 TMP 글꼴을 지정해 주세요."
        );

        return TMP_Settings.defaultFontAsset;
    }

    /// <summary>글꼴이 해당 글자를 모두 가지고 있는지 확인한다. (동적 글꼴이면 필요한 글자를 추가)</summary>
    private static bool CanRender(TMP_FontAsset font, string characters)
    {
        return font.HasCharacters(characters, out uint[] _, false, true);
    }
}
