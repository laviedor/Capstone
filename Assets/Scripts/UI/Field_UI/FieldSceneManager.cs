using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FieldSceneManager : MonoBehaviour
{
    public static FieldSceneManager Instance { get; private set; }

    [Header("마을에서 숨길 오브젝트")]
    [SerializeField] private GameObject villageVisualRoot;

    [Header("마을 카메라")]
    [SerializeField] private Camera villageCamera;

    private bool isTransitioning = false;
    private string currentFieldScene = "";

    public bool IsInField
    {
        get { return !string.IsNullOrEmpty(currentFieldScene); }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void MoveToField(string sceneName)
    {
        if (isTransitioning || !string.IsNullOrEmpty(currentFieldScene))
            return;

        if (string.IsNullOrEmpty(sceneName))
            return;

        StartCoroutine(LoadFieldRoutine(sceneName));
    }

    private IEnumerator LoadFieldRoutine(string sceneName)
    {
        isTransitioning = true;

        // 숲 Scene을 추가로 불러온다.
        AsyncOperation operation =
            SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

        if (operation == null)
        {
            Debug.LogError("필드 Scene 로드 실패: " + sceneName);
            isTransitioning = false;
            yield break;
        }

        while (!operation.isDone)
            yield return null;

        // 마을의 화면 표시만 끈다.
        if (villageVisualRoot != null)
            villageVisualRoot.SetActive(false);

        // 기존 추적 카메라를 계속 사용한다.
        // if (villageCamera != null)
        //     villageCamera.gameObject.SetActive(false);

        currentFieldScene = sceneName;

        Debug.Log("필드 이동 완료: " + sceneName);

        isTransitioning = false;
    }

    public void ReturnToVillage()
    {
        if (isTransitioning || string.IsNullOrEmpty(currentFieldScene))
            return;

        StartCoroutine(ReturnRoutine());
    }

    private IEnumerator ReturnRoutine()
    {
        isTransitioning = true;

        string sceneToUnload = currentFieldScene;

        AsyncOperation operation =
            SceneManager.UnloadSceneAsync(sceneToUnload);

        if (operation == null)
        {
            Debug.LogError("필드 Scene 언로드 실패: " + sceneToUnload);
            isTransitioning = false;
            yield break;
        }

        while (!operation.isDone)
            yield return null;

        if (villageVisualRoot != null)
            villageVisualRoot.SetActive(true);

        //if (villageCamera != null)
        //    villageCamera.gameObject.SetActive(true);

        currentFieldScene = "";

        Debug.Log("마을 복귀 완료");

        isTransitioning = false;
    }
}
