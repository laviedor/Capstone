using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 채팅 버튼 위의 [회원 탈퇴] 버튼: 확인을 받은 뒤 계정을 지우고 처음(로그인) 화면으로 돌아감
public class WithdrawUI : MonoBehaviour
{
    [SerializeField] private Button withdrawButton;
    [SerializeField] private ConfirmDialog dialog;

    void Awake()
    {
        withdrawButton.gameObject.SetActive(false); // 로그인 후에 표시
        withdrawButton.onClick.AddListener(Ask);
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
        withdrawButton.gameObject.SetActive(true);
    }

    void Ask()
    {
        dialog.Show("정말 탈퇴하시겠습니까?\n마을, 건물, 아이템 등 모든 데이터가\n삭제되며 되돌릴 수 없습니다.",
            "탈퇴", Withdraw, "취소");
    }

    async void Withdraw()
    {
        withdrawButton.interactable = false;
        int status = await AuthApi.DeleteAccount(AuthApi.SavedToken);
        if (this == null) return;

        if (status == 200)
        {
            AuthApi.SavedToken = null;
            dialog.Show("탈퇴되었습니다.\n그동안 이용해 주셔서 감사합니다.", "확인", Restart);
        }
        else if (status == 401)
        {
            AuthApi.SavedToken = null;
            dialog.Show("로그인이 만료되었습니다.\n다시 로그인한 뒤 탈퇴해 주세요.", "확인", Restart);
        }
        else
        {
            withdrawButton.interactable = true;
            dialog.Show(status == 0 ? "서버에 연결할 수 없습니다.\n잠시 후 다시 시도해 주세요." : "탈퇴하지 못했습니다.\n잠시 후 다시 시도해 주세요.", "확인", null);
        }
    }

    // 씬을 다시 불러와 로그인 화면부터 시작 (채팅, 좌표 전송 연결도 새로 만들어짐)
    static void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
