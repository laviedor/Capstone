using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 회원 탈퇴 버튼 위의 [로그아웃] 버튼: 확인을 받은 뒤 저장된 토큰을 지우고 처음(로그인) 화면으로 돌아감
public class LogoutUI : MonoBehaviour
{
    [SerializeField] private Button logoutButton;
    [SerializeField] private ConfirmDialog dialog;

    void Awake()
    {
        logoutButton.gameObject.SetActive(false); // 로그인 후에 표시
        logoutButton.onClick.AddListener(Ask);
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
        logoutButton.gameObject.SetActive(true);
    }

    void Ask()
    {
        dialog.Show("로그아웃 하시겠습니까?", "로그아웃", Logout, "취소");
    }

    async void Logout()
    {
        logoutButton.interactable = false;

        // 서버에서도 토큰을 무효화 (서버에 연결하지 못해도 이 PC에서는 로그아웃)
        await AuthApi.Logout(AuthApi.SavedToken);
        if (this == null) return;

        AuthApi.SavedToken = null;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); // 로그인 화면부터 다시 시작
    }
}
