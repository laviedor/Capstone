using UnityEngine;

public class BuildingInteraction : MonoBehaviour
{
    [SerializeField] private string buildingName = "Building";

    public void Interact()
    {
        Debug.Log(buildingName + " 과(와) 상호작용했습니다.");
    }
}
