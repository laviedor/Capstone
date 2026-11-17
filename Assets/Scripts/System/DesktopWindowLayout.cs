using System.Collections;
using UnityEngine;

/// <summary>
/// 데스크톱 컴패니언용 창 배치.
/// 창 높이는 작업 영역(작업표시줄·Dock 제외)의 일정 비율로 정하고,
/// 가로는 기준 비율(1920x270)까지만 늘려 모니터 하단 가운데에 붙인다.
/// 가로가 기준보다 좁은 모니터(노트북 등)는 모니터 폭 전체를 쓰고,
/// 보이지 않는 부분은 CameraFollow의 드래그로 둘러본다.
/// 빌드에서만 자동 실행되며, 에디터에서는 Game 창 해상도로 테스트한다.
/// </summary>
public class DesktopWindowLayout : MonoBehaviour
{
    // 기준 화면 비율: 이 비율의 창에서 맵 가로 전체가 딱 맞게 보인다
    public const float ReferenceAspect = 1920f / 270f;

    // 작업 영역 높이 대비 창 높이 비율
    [SerializeField] private float heightRatio = 0.25f;
    // 창 높이 최소값(px)
    [SerializeField] private int minHeight = 200;
    // 작업 영역 높이 대비 창 높이 최대 비율
    [SerializeField] private float maxHeightRatio = 0.4f;

#if !UNITY_EDITOR && (UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX)
    /// <summary>씬에 따로 배치하지 않아도 게임 시작 시 자동으로 생성한다.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateOnStartup()
    {
        GameObject layoutObject = new GameObject(nameof(DesktopWindowLayout));
        DontDestroyOnLoad(layoutObject);
        layoutObject.AddComponent<DesktopWindowLayout>();
    }
#endif

    /// <summary>창 크기를 계산해 적용한 뒤 모니터 하단 가운데로 옮긴다.</summary>
    private IEnumerator Start()
    {
        // 컴패니언 게임은 다른 창을 쓰는 동안에도 계속 돌아가야 한다
        Application.runInBackground = true;

        DisplayInfo display = Screen.mainWindowDisplayInfo;

        // 작업표시줄·Dock을 제외한 실제 사용 가능 영역
        RectInt workArea = display.workArea;

        if (workArea.width <= 0 || workArea.height <= 0)
        {
            workArea = new RectInt(0, 0, display.width, display.height);
        }

        int maxHeight = Mathf.Max(
            minHeight,
            Mathf.RoundToInt(workArea.height * maxHeightRatio)
        );

        int height = Mathf.Clamp(
            Mathf.RoundToInt(workArea.height * heightRatio),
            minHeight,
            maxHeight
        );

        // 가로는 기준 비율까지만. 모니터가 더 좁으면 모니터 폭 전체를 사용
        int width = Mathf.Min(
            workArea.width,
            Mathf.RoundToInt(height * ReferenceAspect)
        );

        Screen.SetResolution(width, height, FullScreenMode.Windowed);

        // 해상도 변경은 다음 프레임 이후에 적용된다
        yield return null;
        yield return null;

        // 작업 영역 하단 가운데 (좌표는 디스플레이 왼쪽 위 기준)
        Vector2Int position = new Vector2Int(
            workArea.x + (workArea.width - width) / 2,
            workArea.y + workArea.height - height
        );

        AsyncOperation moveOperation =
            Screen.MoveMainWindowTo(display, position);

        yield return moveOperation;

        Debug.Log(
            "창 배치 완료: " + width + "x" + height +
            " @ " + position + " (작업 영역 " + workArea + ")"
        );
    }
}
