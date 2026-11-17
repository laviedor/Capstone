using System;

// 화면 아래쪽에 잠깐 떴다 사라지는 안내 (WorldUI 가 보여 줌). 어느 스크립트에서나 Toast.Show 로 띄운다
public static class Toast
{
    public static event Action<string> Shown;

    public static void Show(string text)
    {
        Shown?.Invoke(text);
    }
}
