using UnityEngine;

// 다른 플레이어 캐릭터: 서버에서 받은 좌표로 부드럽게 이동
public class RemotePlayer : MonoBehaviour
{
    [SerializeField] private PlayerNameLabel nameLabel;
    [SerializeField] private float smoothing = 15f;

    public float LastReceived { get; private set; }

    private Vector3 target;

    public void Init(string playerName, Vector3 position)
    {
        gameObject.name = "RemotePlayer_" + playerName;
        nameLabel.SetName(playerName);
        transform.position = target = position;
        LastReceived = Time.time;
    }

    public void SetTarget(Vector3 position)
    {
        target = position;
        LastReceived = Time.time;
    }

    void Update()
    {
        transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
    }
}
