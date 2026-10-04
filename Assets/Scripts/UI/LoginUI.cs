using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// Play 시작 시 사용자명/패스워드 입력창 표시
// 입력값은 아래 변수에만 보관하고 디스크나 서버에는 저장하지 않음
public class LoginUI : MonoBehaviour
{
    public static string Username { get; private set; }
    public static string Password { get; private set; }

    public static event System.Action LoggedIn; // 확인 버튼으로 로그인했을 때

    [SerializeField] private GameObject loginPanel;
    [SerializeField] private InputField usernameInput;
    [SerializeField] private InputField passwordInput;
    [SerializeField] private Button confirmButton;
    [SerializeField] private PlayerNameLabel playerNameLabel; // 내 캐릭터 머리 위 이름

    // 확인 전까지 꺼둘 스크립트 (입력창 클릭이 캐릭터 이동/건물 설치로 이어지지 않도록)
    [SerializeField] private Behaviour[] disableUntilLogin;

    private const string Chars = "abcdefghijklmnopqrstuvwxyz0123456789";

    void Awake()
    {
        foreach (Behaviour b in disableUntilLogin)
            b.enabled = false;

        loginPanel.SetActive(true);

        usernameInput.characterLimit = NetMessage.MaxNameLength;
        usernameInput.text = RandomUsername();
        passwordInput.text = "";

        usernameInput.onValueChanged.AddListener(v => confirmButton.interactable = v.Trim().Length > 0);
        confirmButton.onClick.AddListener(Confirm);
    }

    void Confirm()
    {
        Username = usernameInput.text.Trim();
        Password = passwordInput.text;

        loginPanel.SetActive(false);
        playerNameLabel.SetName(Username);

        foreach (Behaviour b in disableUntilLogin)
            b.enabled = true;

        LoggedIn?.Invoke();
    }

    // 영문 소문자와 숫자가 섞인 6자리
    static string RandomUsername()
    {
        string name;
        do
        {
            char[] c = new char[6];
            for (int i = 0; i < c.Length; i++)
                c[i] = Chars[Random.Range(0, Chars.Length)];
            name = new string(c);
        } while (!name.Any(char.IsLetter) || !name.Any(char.IsDigit));

        return name;
    }
}
