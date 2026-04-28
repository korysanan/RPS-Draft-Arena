using System.Collections.Generic;

// 카드 속성 enum.
// 인덱스 순서는 PracticeCardController.elementTextures 와이어 순서와 동일하게 유지한다.
//   0:Fire, 1:Water, 2:Nature, 3:Wind, 4:Electric, 5:Ice, 6:Magic
// 따라서 (int)ElementType.Fire == 0 처럼 캐스팅으로 텍스처/카드 인덱스와 호환된다.
public enum ElementType
{
    Fire = 0,
    Water = 1,
    Nature = 2,
    Wind = 3,
    Electric = 4,
    Ice = 5,
    Magic = 6
}

// 한 번의 매칭(내 패 vs 상대 패) 결과
public enum MatchOutcome
{
    Win,
    Lose,
    Tie
}

// 속성 상성표 (Assets/Image/Competition/Graph.png 기준).
//
// RPS-3 / RPS-5 / RPS-7은 같은 N=7 cyclic 규칙의 부분 집합이다.
//   - RPS-3: Fire, Water, Nature
//   - RPS-5: + Wind, Electric
//   - RPS-7: + Ice, Magic
//
// 규칙: 0..6 순환 순서에서 위치 i 는 (i+2, i+4, i+6) mod 7 위치를 이긴다.
//   Fire(0) → Nature(2), Electric(4), Magic(6) 이김
//   Water(1) → Wind(3), Ice(5), Fire(0)         이김
//   ...
// 같은 속성끼리는 무승부(Tie). 표에 없는 조합은 자동으로 Lose.
public static class TypeChart
{
    // 키 속성이 이기는 상대 속성들의 집합 (Graph.png에서 빨간 칸들)
    private static readonly Dictionary<ElementType, HashSet<ElementType>> winsAgainst =
        new Dictionary<ElementType, HashSet<ElementType>>
        {
            { ElementType.Fire,     new HashSet<ElementType> { ElementType.Nature,   ElementType.Electric, ElementType.Magic } },
            { ElementType.Water,    new HashSet<ElementType> { ElementType.Wind,     ElementType.Ice,      ElementType.Fire } },
            { ElementType.Nature,   new HashSet<ElementType> { ElementType.Electric, ElementType.Magic,    ElementType.Water } },
            { ElementType.Wind,     new HashSet<ElementType> { ElementType.Ice,      ElementType.Fire,     ElementType.Nature } },
            { ElementType.Electric, new HashSet<ElementType> { ElementType.Magic,    ElementType.Water,    ElementType.Wind } },
            { ElementType.Ice,      new HashSet<ElementType> { ElementType.Fire,     ElementType.Nature,   ElementType.Electric } },
            { ElementType.Magic,    new HashSet<ElementType> { ElementType.Water,    ElementType.Wind,     ElementType.Ice } },
        };

    // 내 속성 vs 상대 속성 결과를 반환한다.
    // 시합/판정 로직에서 호출하는 메인 진입점.
    public static MatchOutcome GetOutcome(ElementType me, ElementType opponent)
    {
        if (me == opponent) return MatchOutcome.Tie;
        if (winsAgainst.TryGetValue(me, out var preys) && preys.Contains(opponent))
            return MatchOutcome.Win;
        return MatchOutcome.Lose;
    }

    // 임의의 속성이 다른 속성을 이기는지 단순 bool로 확인 (편의 메서드)
    public static bool Beats(ElementType me, ElementType opponent)
    {
        return GetOutcome(me, opponent) == MatchOutcome.Win;
    }

    // 현재 RPS 모드에서 사용 가능한 속성 목록을 반환한다.
    // 카드 생성/AI 선택 등에서 모드에 맞춰 선택지를 좁힐 때 사용.
    public static IReadOnlyList<ElementType> GetActiveSet(PracticeSetupManager.RPSType rps)
    {
        return rps switch
        {
            PracticeSetupManager.RPSType.RPS3 => new[]
            {
                ElementType.Fire, ElementType.Water, ElementType.Nature
            },
            PracticeSetupManager.RPSType.RPS5 => new[]
            {
                ElementType.Fire, ElementType.Water, ElementType.Nature,
                ElementType.Wind, ElementType.Electric
            },
            PracticeSetupManager.RPSType.RPS7 => new[]
            {
                ElementType.Fire, ElementType.Water, ElementType.Nature,
                ElementType.Wind, ElementType.Electric, ElementType.Ice, ElementType.Magic
            },
            _ => new[]
            {
                ElementType.Fire, ElementType.Water, ElementType.Nature,
                ElementType.Wind, ElementType.Electric, ElementType.Ice, ElementType.Magic
            },
        };
    }
}
