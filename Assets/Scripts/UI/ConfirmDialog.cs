using System;
using UnityEngine;
using UnityEngine.UI;

// 메시지와 버튼 1~2개로 확인을 받는 창 (회원가입 여부, 회원 탈퇴 확인 등에 같이 사용)
public class ConfirmDialog : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Text messageText;
    [SerializeField] private Button okButton;
    [SerializeField] private Text okButtonText;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Text cancelButtonText;

    private Action onOk;
    private Action onCancel;

    void Awake()
    {
        panel.SetActive(false);
        okButton.onClick.AddListener(() => Close(onOk));
        cancelButton.onClick.AddListener(() => Close(onCancel));
    }

    // cancelLabel이 null이면 버튼을 하나만 표시
    public void Show(string message, string okLabel, Action onOk, string cancelLabel = null, Action onCancel = null)
    {
        messageText.text = message;
        okButtonText.text = okLabel;
        cancelButton.gameObject.SetActive(cancelLabel != null);
        if (cancelLabel != null) cancelButtonText.text = cancelLabel;

        this.onOk = onOk;
        this.onCancel = onCancel;
        panel.SetActive(true);
    }

    void Close(Action callback)
    {
        panel.SetActive(false);
        callback?.Invoke();
    }
}
