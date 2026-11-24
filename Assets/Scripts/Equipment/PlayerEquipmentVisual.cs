using UnityEngine;

// 장착한 장비를 플레이어 캐릭터 그림 위에 겹쳐 그린다 (모자·상의·하의·도구).
//   장비 그림(EquipmentItemData.wornSprite)은 캐릭터 그림과 같은 64 x 64 도트, 같은 기준점(가운데)으로 그려져 있어서
//   캐릭터와 같은 자리에 겹치기만 하면 몸에 맞는다.
//   그리는 순서(아래 → 위): 캐릭터 < 상의 < 하의(멜빵이 셔츠 위로) < 모자(머리카락 위로) < 도구(손에 쥔 것)
//   PlayerEquipment 가 시작할 때 플레이어에 스스로 붙인다 (씬에 따로 붙이지 않아도 됨)
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerEquipmentVisual : MonoBehaviour
{
    private static readonly EquipmentSlot[] Slots =
    {
        EquipmentSlot.Top, EquipmentSlot.Bottom, EquipmentSlot.Hat, EquipmentSlot.Tool
    };

    // 플레이어 캐릭터 (모든 PlayerEquipmentVisual 중 로컬 플레이어)
    public static PlayerEquipmentVisual Local { get; private set; }

    private SpriteRenderer body;
    private readonly SpriteRenderer[] layers = new SpriteRenderer[Slots.Length];
    private PlayerEquipment subscribed;

    // 캐릭터 맨몸 그림 (인벤토리의 캐릭터 미리보기도 같은 그림을 쓴다)
    public Sprite BodySprite => body != null ? body.sprite : null;

    private void Awake()
    {
        body = GetComponent<SpriteRenderer>();

        for (int i = 0; i < Slots.Length; i++)
        {
            GameObject layer = new GameObject("Wear_" + Slots[i]);
            layer.transform.SetParent(transform, false);
            layers[i] = layer.AddComponent<SpriteRenderer>();
        }

        Local = this;

        // 인벤토리 장비 탭에 이 캐릭터를 그대로 띄우는 카메라
        if (GetComponent<PlayerPortrait>() == null)
            gameObject.AddComponent<PlayerPortrait>();
    }

    private void OnDestroy()
    {
        if (subscribed != null)
            subscribed.OnEquipmentChanged -= Refresh;

        if (Local == this)
            Local = null;
    }

    // 장비 정보와 연결되면 바로 그리고, 이후로는 바뀔 때마다 다시 그린다
    private void Update()
    {
        PlayerEquipment equipment = PlayerEquipment.Instance;

        if (equipment == subscribed)
            return;

        if (subscribed != null)
            subscribed.OnEquipmentChanged -= Refresh;

        subscribed = equipment;

        if (subscribed != null)
            subscribed.OnEquipmentChanged += Refresh;

        Refresh();
    }

    // 캐릭터 그림과 정렬 순서, 뒤집기, 색, 보이기를 맞춘다 (캐릭터 그림이 바뀌어도 장비가 따라가도록)
    private void LateUpdate()
    {
        for (int i = 0; i < layers.Length; i++)
        {
            SpriteRenderer layer = layers[i];
            layer.sortingLayerID = body.sortingLayerID;
            layer.sortingOrder = body.sortingOrder + i + 1;
            layer.flipX = body.flipX;
            layer.flipY = body.flipY;
            layer.color = body.color;
            layer.enabled = body.enabled && layer.sprite != null;
            layer.transform.localPosition = AlignOffset(body.sprite, layer.sprite, body.flipX, body.flipY);
        }
    }

    // 장비 그림을 캐릭터 그림과 같은 그림 좌표에 맞추는 위치.
    //   캐릭터 스프라이트는 64 x 64 그림의 일부(예: 가운데 32 x 51)만 쓰고 장비 그림은 64 x 64 전체라서,
    //   둘의 기준점(pivot)이 그림 안에서 서로 다른 곳에 있다. 그 차이만큼 장비를 옮겨 도트가 정확히 겹치게 한다.
    private static Vector3 AlignOffset(Sprite bodySprite, Sprite wearSprite, bool flipX, bool flipY)
    {
        if (bodySprite == null || wearSprite == null)
            return Vector3.zero;

        // 각 그림의 기준점이 원본 그림(텍스처)에서 몇 번째 픽셀인지
        Vector2 bodyPivot = bodySprite.rect.position + bodySprite.pivot;
        Vector2 wearPivot = wearSprite.rect.position + wearSprite.pivot;
        Vector2 offset = (wearPivot - bodyPivot) / bodySprite.pixelsPerUnit;

        if (flipX) offset.x = -offset.x;
        if (flipY) offset.y = -offset.y;

        return new Vector3(offset.x, offset.y, 0f);
    }

    // 부위마다 장착한 장비의 착용 그림을 놓는다 (장착하지 않았거나 그림이 없으면 비움)
    public void Refresh()
    {
        for (int i = 0; i < Slots.Length; i++)
        {
            EquipmentItemData item = subscribed != null ? subscribed.GetEquipped(Slots[i]) : null;
            layers[i].sprite = item != null ? item.wornSprite : null;
            layers[i].enabled = body.enabled && layers[i].sprite != null;
        }
    }
}
