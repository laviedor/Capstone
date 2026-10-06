using TMPro;
using UnityEngine;

public class DemolitionConfirmUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField]
    private GameObject confirmPanel;

    [SerializeField]
    private TMP_Text messageText;

    [Header("Demolition")]
    [SerializeField]
    private BuildingDemolition buildingDemolition;

    public bool IsOpen
    {
        get
        {
            return confirmPanel != null &&
                   confirmPanel.activeSelf;
        }
    }



    void Start()
    {
        // 게임 시작 시 확인창 숨기기
        confirmPanel.SetActive(false);
    }


    public void Open(Building building)
    {
        if (building == null)
            return;


        confirmPanel.SetActive(true);


        if (messageText != null &&
            building.Data != null)
        {
            messageText.text =
                building.Data.buildingName +
                "을(를) 철거하시겠습니까?";
        }
    }


    public void Cancel()
    {
        confirmPanel.SetActive(false);

        if (buildingDemolition != null)
        {
            buildingDemolition.ClearSelection();
        }

        Debug.Log("철거 취소");
    }





    //철거 확인
    public void Confirm()
    {
        if (buildingDemolition == null)
            return;

        buildingDemolition.ConfirmDemolition();

        confirmPanel.SetActive(false);
    }
}