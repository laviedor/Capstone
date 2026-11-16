using System;

// Unity Inspector에서 생산 항목을 편집할 수 있도록 직렬화한다.
[Serializable]
// 건물이 한 번의 생산 주기마다 지급할 자원 종류와 수량을 정의한다.
public class ResourceProduction
{
    // 생산할 자원 종류.
    public ResourceType resourceType;

    // 생산 주기 한 번에 지급할 수량. 기본값은 1이다.
    public int amount = 1;
}