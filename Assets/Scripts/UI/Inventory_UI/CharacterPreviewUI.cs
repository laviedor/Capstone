using UnityEngine;
using UnityEngine.UI;

// 플레이어 탭의 캐릭터 미리보기.
//   마을의 실제 캐릭터를 비추는 카메라(PlayerPortrait)가 있으면 그 화면을 그대로 띄운다 (인게임과 똑같이 보임).
//   카메라가 없을 때만 예전처럼 캐릭터 그림 위에 장비 그림을 겹쳐 그린다.
public class CharacterPreviewUI : MonoBehaviour
{
    [SerializeField] private Image hatLayer;
    [SerializeField] private Image topLayer;
    [SerializeField] private Image bottomLayer;
    [SerializeField] private Image toolLayer;

    // 실제 캐릭터 화면을 띄우는 그림 (처음 쓸 때 캐릭터 칸 안에 만든다)
    private RawImage portraitImage;
    private bool usingPortrait;

    // 장비 탭이 보이는 동안만 캐릭터를 찍는다
    private void OnEnable()
    {
        if (PlayerPortrait.Local != null && !usingPortrait)
        {
            PlayerPortrait.Local.Use();
            usingPortrait = true;
        }
    }

    private void OnDisable()
    {
        if (usingPortrait && PlayerPortrait.Local != null)
            PlayerPortrait.Local.Release();

        usingPortrait = false;
    }

    // 실제 캐릭터 화면을 캐릭터 칸에 띄우고, 겹쳐 그리던 그림들은 숨긴다. 띄웠으면 true
    private bool ShowPortrait(Image characterImage)
    {
        PlayerPortrait portrait = PlayerPortrait.Local;

        if (portrait == null || characterImage == null)
            return false;

        if (!usingPortrait && isActiveAndEnabled)
        {
            portrait.Use();
            usingPortrait = true;
        }

        if (portraitImage == null)
        {
            GameObject go = new GameObject("LivePortrait", typeof(RectTransform));
            go.transform.SetParent(characterImage.transform, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            portraitImage = go.AddComponent<RawImage>();
            portraitImage.raycastTarget = false;
        }

        portraitImage.texture = portrait.Texture;
        portraitImage.enabled = true;
        portraitImage.transform.SetAsLastSibling();

        characterImage.enabled = false;

        foreach (Image layer in new[] { hatLayer, topLayer, bottomLayer, toolLayer })
        {
            if (layer != null)
                layer.enabled = false;
        }

        return true;
    }

    // 장착 상태에 맞춰 각 레이어를 갱신한다. 착용 이미지가 없는 장비는 표시하지 않는다.
    //   그리는 순서는 마을의 캐릭터(PlayerEquipmentVisual)와 같다: 상의 < 하의 < 모자 < 도구
    //   캐릭터 그림도 마을의 플레이어와 같은 그림(머리카락·얼굴이 있는 그림)으로 맞춘다
    public void Refresh(PlayerEquipment equipment)
    {
        if (topLayer != null) topLayer.transform.SetSiblingIndex(0);
        if (bottomLayer != null) bottomLayer.transform.SetSiblingIndex(1);
        if (hatLayer != null) hatLayer.transform.SetSiblingIndex(2);
        if (toolLayer != null) toolLayer.transform.SetSiblingIndex(3);

        PlayerEquipmentVisual player = PlayerEquipmentVisual.Local;
        Image characterImage = hatLayer != null ? hatLayer.transform.parent.GetComponent<Image>() : null;

        // 마을의 실제 캐릭터 화면을 쓸 수 있으면 그것을 띄운다
        if (ShowPortrait(characterImage))
            return;

        if (characterImage != null)
            characterImage.enabled = true;

        if (player != null && player.BodySprite != null && characterImage != null)
        {
            characterImage.sprite = player.BodySprite;
            characterImage.preserveAspect = true;
        }

        Apply(hatLayer, equipment, EquipmentSlot.Hat, characterImage);
        Apply(topLayer, equipment, EquipmentSlot.Top, characterImage);
        Apply(bottomLayer, equipment, EquipmentSlot.Bottom, characterImage);
        Apply(toolLayer, equipment, EquipmentSlot.Tool, characterImage);
    }

    private static void Apply(Image layer, PlayerEquipment equipment, EquipmentSlot slot, Image characterImage)
    {
        if (layer == null)
            return;

        EquipmentItemData item =
            equipment != null ? equipment.GetEquipped(slot) : null;

        Sprite sprite = item != null ? item.wornSprite : null;

        layer.sprite = sprite;
        layer.enabled = sprite != null;

        if (sprite != null)
            FitToCharacter(layer, characterImage);
    }

    // 장비 그림을 캐릭터 그림과 같은 배율, 같은 위치에 놓는다.
    //   캐릭터 스프라이트는 64 x 64 그림의 가운데 40 x 64 조각이고 장비 그림은 64 x 64 전체라서,
    //   각자 칸에 맞추면(preserveAspect) 배율이 달라져 어긋난다. 그래서 캐릭터가 실제로 그려진 크기에서
    //   '그림 1픽셀 = 화면 몇 단위'를 구해 장비 그림의 크기와 위치를 직접 정한다 (마을 화면에서처럼 그림 좌표가 일치).
    private static void FitToCharacter(Image layer, Image characterImage)
    {
        RectTransform rt = layer.rectTransform;
        Sprite body = characterImage != null ? characterImage.sprite : null;

        if (body == null)
        {
            // 캐릭터 그림을 모르면 예전처럼 칸에 맞춘다
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            layer.preserveAspect = true;
            return;
        }

        // 캐릭터가 칸 안에 실제로 그려진 크기 (preserveAspect: 가운데에 비율을 지켜 맞춤)
        Rect box = characterImage.rectTransform.rect;
        Rect bodyRect = body.rect;
        float scale = Mathf.Min(box.width / bodyRect.width, box.height / bodyRect.height);

        if (!characterImage.preserveAspect)
            scale = box.height / bodyRect.height;

        // 두 그림의 중심 차이(그림 픽셀) -> 화면 단위
        Rect wearRect = layer.sprite.rect;
        Vector2 offset = (wearRect.center - bodyRect.center) * scale;
        Vector2 boxCenterFromPivot = box.center; // 피벗 기준 칸의 가운데

        layer.preserveAspect = false;
        rt.anchorMin = rt.anchorMax = characterImage.rectTransform.pivot;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = wearRect.size * scale;
        rt.anchoredPosition = boxCenterFromPivot + offset;
    }
}
