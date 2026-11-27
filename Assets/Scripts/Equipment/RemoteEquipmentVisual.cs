using UnityEngine;

// 다른 플레이어 캐릭터에 그 사람이 입은 장비(모자·상의·하의·도구)를 겹쳐 그린다.
//   내 캐릭터의 PlayerEquipmentVisual 과 같은 방식: 같은 그림 좌표로 맞추고, 같은 순서(상의 < 하의 < 모자 < 도구)로 그린다
//   무엇을 입었는지는 서버가 알려 준다 (RemotePlayerManager → RemotePlayer.ApplyLook → SetLook)
[RequireComponent(typeof(SpriteRenderer))]
public class RemoteEquipmentVisual : MonoBehaviour
{
    private static readonly EquipmentSlot[] Slots =
    {
        EquipmentSlot.Top, EquipmentSlot.Bottom, EquipmentSlot.Hat, EquipmentSlot.Tool
    };

    private SpriteRenderer body;
    private readonly SpriteRenderer[] layers = new SpriteRenderer[Slots.Length];

    private void Awake()
    {
        body = GetComponent<SpriteRenderer>();

        for (int i = 0; i < Slots.Length; i++)
        {
            GameObject layer = new GameObject("Wear_" + Slots[i]);
            layer.transform.SetParent(transform, false);
            layers[i] = layer.AddComponent<SpriteRenderer>();
            layers[i].sharedMaterial = body.sharedMaterial;
        }
    }

    // 입은 장비를 바꾼다 (모르는 장비 id 는 그리지 않는다)
    public void SetLook(LookInfo look)
    {
        for (int i = 0; i < Slots.Length; i++)
        {
            EquipmentItemData item = look != null ? EquipmentCatalog.Find(look.Get(Slots[i])) : null;
            layers[i].sprite = item != null ? item.wornSprite : null;
        }

        LateUpdate();
    }

    // 몸 그림의 정렬 순서, 뒤집기, 색, 보이기에 맞춘다
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
            layer.transform.localPosition = PlayerEquipmentVisual.AlignOffset(body.sprite, layer.sprite, body.flipX, body.flipY);
        }
    }
}
