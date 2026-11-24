using UnityEngine;
using UnityEngine.Rendering.Universal;

// 플레이어 캐릭터만 비추는 작은 카메라. 그 화면(RenderTexture)을 인벤토리 장비 탭에 그대로 띄운다.
//   UI 에서 캐릭터와 장비 그림을 따로 쌓으면 배율·기준점이 어긋나기 쉬워서,
//   마을에 실제로 그려진 캐릭터(장비 포함)를 찍어 보여 준다: 인게임과 항상 똑같이 보인다.
//   플레이어와 장비 그림을 'PlayerPortrait' 레이어에 두고, 이 카메라는 그 레이어만 그린다.
//   (메인 카메라는 모든 레이어를 그리므로 마을 화면은 그대로다)
//   장비 탭이 열려 있을 때만 찍는다 (CharacterPreviewUI 가 Use / Release 로 알려 줌)
//   조명: URP 2D 조명은 카메라가 비추는 레이어로 걸러지므로, 마을의 Global Light 2D(Default 레이어)는 이 카메라에 닿지 않아
//         캐릭터가 검게 찍힌다. 그래서 그 조명을 복제해 'PlayerPortraitLight' 레이어에 두고 이 카메라만 보게 한다
//         (다른 카메라에서는 그 레이어를 빼서 마을 조명이 두 번 들어가지 않게 한다)
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

    // 마을의 전체 조명(Global Light 2D)을 복제해 이 카메라만 보는 레이어에 둔다
    private void CreateLight()
    {
        Light2D source = null;

        foreach (Light2D light in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
        {
            if (light.lightType == Light2D.LightType.Global && light.gameObject.layer != PortraitLightLayer)
            {
                source = light;
                break;
            }
        }

        GameObject lightObject;

        if (source != null)
        {
            lightObject = Instantiate(source.gameObject);
            portraitLight = lightObject.GetComponent<Light2D>();
        }
        else
        {
            lightObject = new GameObject();
            portraitLight = lightObject.AddComponent<Light2D>();
            portraitLight.lightType = Light2D.LightType.Global;
        }

        lightObject.name = "PlayerPortraitLight";
        lightObject.layer = PortraitLightLayer;
        HideHelper(lightObject);
        HideLightFromOtherCameras();
    }

    // 장비 탭용으로 코드가 만든 카메라·조명은 하이어라키에서 숨긴다.
    //   숨겨 두면 에디터의 Game 창에서 Gizmos 를 켜 두어도 카메라·전구 아이콘이 그려지지 않는다 (빌드에는 원래 안 보임)
    private static void HideHelper(GameObject go)
    {
        go.hideFlags = HideFlags.HideInHierarchy;
    }

    // 다른 카메라(메인 카메라 등)는 초상화 조명 레이어를 보지 않게 한다
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
            HideLightFromOtherCameras(); // 나중에 생긴 카메라도 조명이 두 번 들어가지 않게
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
    }
}
