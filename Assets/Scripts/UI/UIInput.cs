using UnityEngine.EventSystems;

// 마우스가 UI 위에 있는지 확인 (채팅창 등을 클릭할 때 캐릭터 이동/건물 설치가 같이 일어나지 않도록)
public static class UIInput
{
    public static bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
