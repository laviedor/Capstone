using UnityEngine;
using UnityEngine.UI;

// 화면 오른쪽 위 톱니바퀴 버튼과 계정 메뉴: 로그인한 유저명, [로그아웃], [회원 탈퇴]
//   두 버튼이 하는 일은 LogoutUI, WithdrawUI 가 맡고, 여기서는 메뉴를 여닫고 유저명을 보여 주기만 한다
//   채팅창, 친구 창과 같은 자리에 뜨므로 셋 중 하나만 열려 있게 한다
public class AccountMenuUI : MonoBehaviour
{
    [SerializeField] private Button gearButton;
    [SerializeField] private GameObject menu;
    [SerializeField] private Text nameText;             // 로그인한 유저명
    [SerializeField] private GameObject[] otherPanels;  // 같은 자리에 뜨는 창 (채팅창, 친구 창)

    void Awake()
    {
        gearButton.gameObject.SetActive(false); // 로그인 후에 표시
        menu.SetActive(false);
        gearButton.onClick.AddListener(Toggle);

        // 메뉴의 버튼을 누르면 확인 창이 뜨므로 메뉴는 닫는다
        foreach (Button button in menu.GetComponentsInChildren<Button>(true))
            button.onClick.AddListener(Close);
    }

    void OnEnable()
    {
        LoginUI.LoggedIn += ShowButton;
    }

    void OnDisable()
    {
        LoginUI.LoggedIn -= ShowButton;
    }

    void ShowButton()
    {
        gearButton.gameObject.SetActive(true);
        nameText.text = LoginUI.Username;
    }

    void Toggle()
    {
        bool open = !menu.activeSelf;
        menu.SetActive(open);
        if (!open) return;

        foreach (GameObject panel in otherPanels)
            panel.SetActive(false);
    }

    void Close()
    {
        menu.SetActive(false);
    }

    void Update()
    {
        if (!menu.activeSelf) return;

        // 채팅창이나 친구 창이 열리면 메뉴는 닫는다
        foreach (GameObject panel in otherPanels)
        {
            if (panel.activeSelf)
            {
                Close();
                return;
            }
        }
    }
}
