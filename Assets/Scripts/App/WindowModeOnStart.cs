using UnityEngine;

// 빌드 실행 시 항상 창 모드로 시작
// Unity는 마지막 화면 설정(전체화면 여부, 해상도)을 레지스트리에 저장해 다음 실행에 다시 쓰므로,
// 예전에 전체화면으로 실행한 적이 있는 PC에서도 창 모드로 되돌림
public static class WindowModeOnStart
{
    // Player Settings의 기본 창 크기와 같게 유지
    private const int Width = 1280;
    private const int Height = 720;

#if UNITY_STANDALONE
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Apply()
    {
        if (Application.isEditor || Screen.fullScreenMode == FullScreenMode.Windowed) return;

        Screen.SetResolution(Width, Height, FullScreenMode.Windowed);
    }
#endif
}
