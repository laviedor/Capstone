using UnityEngine;
using System;

// Unity Inspector에서 이 일반 C# 클래스의 필드를 표시하고 저장할 수 있게 한다.
[Serializable]
// 자원 종류와 해당 자원의 수량을 한 묶음으로 저장한다.
public class ResourceAmount
{
    // 자원의 종류(나무, 돌, 골드 등).
    public ResourceType resourceType;
    // 해당 자원의 보유량 또는 필요한 수량.
    public int amount;
}

