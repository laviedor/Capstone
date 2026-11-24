using UnityEngine;
using UnityEngine.Rendering.Universal;

// 플레이어 캐릭터만 비추는 작은 카메라. 그 화면(RenderTexture)을 인벤토리 장비 탭에 그대로 띄운다.
//   UI 에서 캐릭터와 장비 그림을 따로 쌓으면 배율·기준점이 어긋나기 쉬워서,
//   마을에 실제로 그려진 캐릭터(장비 포함)를 찍어 보여 준다: 인게임과 항상 똑같이 보인다.
//   플레이어와 장비 그림을 'PlayerPortrait' 레이어에 두고, 이 카메라는 그 레이어만 그린다.
//   (메인 카메라는 모든 레이어를 그리므로 마을 화면은 그대로다)
//   장비 탭이 열려 있을 때만 찍는다 (CharacterPreviewUI 가 Use / Release 로 알려 줌)
//   조명: URP 2D 는 카메라가 비추는 레이어에 조명이 하나도 없으면 그 카메라의 스프라이트를 검게 그린다. 마을의 Global Light 2D 는
//         Default 레이어라 이 카메라에는 보이지 않으므로, 이 카메라만 보는 'PlayerPortraitLight' 레이어에 밝기 0 인 조명을 하나 둔다.
//         카메라에 조명이 하나라도 보이면 전체 조명(Global Light)의 밝기는 카메라와 상관없이 적용되어서, 캐릭터가 마을에서와
//         같은 밝기로 찍힌다. (전체 조명을 복제해 두면 전체 조명이 둘이 되어 URP 가 "More than one global light" 오류를 낸다)
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerPortrait : MonoBehaviour
{
    public const string LayerName = "PlayerPortrait";
    private const int FallbackLayer = 8;
    public const string LightLayerName = "PlayerPortraitLight";
    private const int FallbackLightLayer = 9;

    // 결과 그림 크기 (장비 탭 캐릭터 칸 110 x 124 와 같은 비율, 2배)
    private const int TextureWidth = 220;
    private const int TextureHeight = 248;

    // 캐릭터 그림 높이 대비 화면에 담을 높이 (모자·도구가 잘리지 않게 여유)
    private const float FrameScale = 1.0f;

    public static PlayerPortrait Local { get; private set; }

    private SpriteRenderer body;
    private Camera portraitCamera;
    private RenderTexture texture;
    private Light2D portraitLight;
    private int lightSortingLayer; // 초상화 조명이 걸려 있는 정렬 레이어 (캐릭터가 그려지는 레이어)
    private int users;

    public static int PortraitLayer
    {
        get
        {
            int layer = LayerMask.NameToLayer(LayerName);
            return layer >= 0 ? layer : FallbackLayer;
        }
    }

    public static int PortraitLightLayer
    {
        get
        {
            int layer = LayerMask.NameToLayer(LightLayerName);
            return layer >= 0 ? layer : FallbackLightLayer;
        }
    }

    // 지금 찍고 있는 캐릭터 화면 (아직 만들지 않았으면 만든다)
    public RenderTexture Texture
    {
        get
        {
            EnsureCamera();
            return texture;
        }
    }

    private void Awake()
    {
        body = GetComponent<SpriteRenderer>();
        Local = this;
    }

    private void OnDestroy()
    {
        if (Local == this)
            Local = null;

        if (portraitCamera != null)
            Destroy(portraitCamera.gameObject);

        if (portraitLight != null)
            Destroy(portraitLight.gameObject);

        if (texture != null)
        {
            texture.Release();
            Destroy(texture);
        }
    }

    // 장비 탭이 열릴 때 부른다: 찍기 시작
    public void Use()
    {
        users++;
        EnsureCamera();
        MatchWorldLight(); // 그 사이 캐릭터의 정렬 레이어나 그곳의 전체 조명이 달라졌을 수 있다
        portraitCamera.enabled = true;
    }

    // 장비 탭이 닫힐 때 부른다: 아무도 보지 않으면 찍지 않는다
    public void Release()
    {
        users = Mathf.Max(0, users - 1);

        if (users == 0 && portraitCamera != null)
            portraitCamera.enabled = false;
    }

    private void EnsureCamera()
    {
        if (portraitCamera != null)
            return;

        texture = new RenderTexture(TextureWidth, TextureHeight, 16, RenderTextureFormat.ARGB32)
        {
            name = "PlayerPortrait",
            filterMode = FilterMode.Bilinear
        };
        texture.Create();

        GameObject holder = new GameObject("PlayerPortraitCamera");
        HideHelper(holder);
        portraitCamera = holder.AddComponent<Camera>();
        portraitCamera.orthographic = true;
        portraitCamera.clearFlags = CameraClearFlags.SolidColor;
        portraitCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        portraitCamera.cullingMask = (1 << PortraitLayer) | (1 << PortraitLightLayer);
        portraitCamera.targetTexture = texture;
        portraitCamera.depth = -100;
        portraitCamera.nearClipPlane = 0.01f;
        portraitCamera.farClipPlane = 100f;
        portraitCamera.enabled = users > 0;

        CreateLight();
        Follow();
    }

    // 이 카메라에만 보이는, 밝기 0 인 조명을 만든다 (맨 위의 '조명' 설명 참고)
    private void CreateLight()
    {
        GameObject lightObject = new GameObject("PlayerPortraitLight");
        lightObject.SetActive(false); // 설정을 마친 뒤 MatchWorldLight 가 켠다 (켜질 때 URP 에 조명으로 등록된다)
        lightObject.layer = PortraitLightLayer;
        HideHelper(lightObject);

        portraitLight = lightObject.AddComponent<Light2D>(); // 새로 붙인 Light2D 는 점 조명(Point)이다
        portraitLight.intensity = 0f; // 아무것도 밝히지 않는다: 전체 조명이 이 카메라에도 들어가게만 한다

        MatchWorldLight();
        HideLightFromOtherCameras();
    }

    // 초상화 조명을 캐릭터를 비추는 전체 조명에 맞춘다: 캐릭터가 그려지는 정렬 레이어에, 그 전체 조명과 같은 블렌드 스타일로 건다.
    //   그런 전체 조명이 없는 곳에서는 꺼 둔다 (조명이 전혀 없는 씬은 URP 가 스프라이트를 조명 없이 그리므로 초상화도 그대로 나온다)
    private void MatchWorldLight()
    {
        if (portraitLight == null)
            return;

        lightSortingLayer = body.sortingLayerID;
        Light2D worldLight = null;

        foreach (Light2D light in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
        {
            if (light != portraitLight &&
                light.isActiveAndEnabled &&
                light.lightType == Light2D.LightType.Global &&
                light.targetSortingLayers != null &&
                System.Array.IndexOf(light.targetSortingLayers, lightSortingLayer) >= 0)
            {
                worldLight = light;
                break;
            }
        }

        if (worldLight != null)
        {
            portraitLight.blendStyleIndex = worldLight.blendStyleIndex;
            portraitLight.targetSortingLayers = new[] { lightSortingLayer };
        }

        if (portraitLight.gameObject.activeSelf != (worldLight != null))
            portraitLight.gameObject.SetActive(worldLight != null);
    }

    // 장비 탭용으로 코드가 만든 카메라·조명은 하이어라키에서 숨긴다.
    //   숨겨 두면 에디터의 Game 창에서 Gizmos 를 켜 두어도 카메라·전구 아이콘이 그려지지 않는다 (빌드에는 원래 안 보임)
    private static void HideHelper(GameObject go)
    {
        go.hideFlags = HideFlags.HideInHierarchy;
    }

    // 다른 카메라(메인 카메라 등)는 초상화 조명 레이어를 보지 않게 한다
    //   (밝기 0 이라 보이는 차이는 없지만, 그 카메라들의 조명 계산에 끼어들지 않도록)
    private void HideLightFromOtherCameras()
    {
        int lightBit = 1 << PortraitLightLayer;

        foreach (Camera cam in Camera.allCameras)
        {
            if (cam != portraitCamera && (cam.cullingMask & lightBit) != 0)
                cam.cullingMask &= ~lightBit;
        }
    }

    // 플레이어와 장비 그림을 찍히는 레이어로 옮기고, 카메라가 캐릭터를 따라가게 한다
    private void LateUpdate()
    {
        int layer = PortraitLayer;

        if (gameObject.layer != layer)
            gameObject.layer = layer;

        foreach (Transform child in transform)
        {
            if (child.gameObject.layer != layer && child.GetComponent<SpriteRenderer>() != null)
                child.gameObject.layer = layer;
        }

        if (portraitCamera != null && portraitCamera.enabled)
        {
            if (body.sortingLayerID != lightSortingLayer)
                MatchWorldLight(); // 캐릭터의 정렬 레이어가 바뀌면 초상화 조명도 그 레이어로

            HideLightFromOtherCameras(); // 나중에 생긴 카메라도 초상화 조명을 보지 않게
            Follow();
        }
    }

    // 캐릭터 그림(64 x 64 원본 전체)이 화면 가운데에 꽉 차게 맞춘다. 장비를 바꿔도 화면이 흔들리지 않도록
    // 장비가 아니라 캐릭터 원본 그림 크기를 기준으로 한다.
    private void Follow()
    {
        Sprite sprite = body.sprite;

        if (sprite == null)
            return;

        float ppu = sprite.pixelsPerUnit;
        Vector2 textureSize = new Vector2(sprite.texture.width, sprite.texture.height);
        Vector2 pivotPixel = sprite.rect.position + sprite.pivot;

        // 원본 그림 가운데의 월드 위치
        Vector2 localCenter = (textureSize / 2f - pivotPixel) / ppu;
        Vector3 center = transform.TransformPoint(localCenter);

        float worldHeight = textureSize.y / ppu * Mathf.Abs(transform.lossyScale.y);

        portraitCamera.orthographicSize = worldHeight / 2f * FrameScale;
        portraitCamera.aspect = (float)TextureWidth / TextureHeight;
        portraitCamera.transform.position = new Vector3(center.x, center.y, transform.position.z - 10f);

        // 초상화 조명도 캐릭터 자리에 둔다 (카메라 시야 안에 있어야 그 카메라의 조명으로 계산된다)
        if (portraitLight != null)
            portraitLight.transform.position = center;
    }
}
