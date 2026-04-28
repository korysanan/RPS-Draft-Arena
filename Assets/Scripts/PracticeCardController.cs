using System.Collections.Generic;
using UnityEngine;

// Practice 씬의 카드 매니저.
// 사용자가 PracticeMode에서 고른 RPS 종류(3/5/7)에 따라
// - 표시할 카드 수를 결정하고
// - 각 카드에 속성 텍스처를 랜덤하게 배분한다.
public class PracticeCardController : MonoBehaviour
{
    // 씬에 배치된 7장의 카드 GameObject 리스트.
    // 인덱스 순서가 곧 "표시 우선순위"가 됨. (앞쪽 N개를 활성화)
    // 현재 와이어링 순서: Card2, Card5, Card6, Card1, Card3, Card4, Card7
    //   - RPS-3: 앞 3개 → 윗줄 가운데 + 아랫줄 안쪽 두 개 (피라미드)
    //   - RPS-5: 앞 5개 → 윗줄 3장 + 아랫줄 안쪽 2장
    //   - RPS-7: 전체 → 윗줄 3 + 아랫줄 4
    [SerializeField] private List<GameObject> cards = new List<GameObject>();

    // 7개 속성 텍스처 (인덱스 0~6).
    // 와이어 순서: Fire, Water, Nature, Wind, Electric, Ice, Magic
    // RPS 카운트에 맞춰 앞쪽 N개를 풀(pool)로 사용한다.
    [SerializeField] private List<Texture> elementTextures = new List<Texture>();

    private void Start()
    {
        // RPS 종류에 따라 사용할 카드 수 결정 (3/5/7)
        int count = ResolveCount(PracticeSettings.Rps);

        // 속성 텍스처 풀에 앞에서부터 count개 담기
        // (RPS-3 → Fire/Water/Nature, RPS-5 → +Wind/Electric, RPS-7 → 전부)
        var pool = new List<Texture>(count);
        for (int i = 0; i < count && i < elementTextures.Count; i++)
            pool.Add(elementTextures[i]);
        // 카드별 위치를 매번 무작위로 만들기 위해 섞기
        Shuffle(pool);

        // 각 카드를 순회하며 N개만 활성화하고 텍스처 배분
        for (int i = 0; i < cards.Count; i++)
        {
            bool active = i < count;
            if (cards[i] != null)
                cards[i].SetActive(active);
            if (!active) continue;

            // 활성 카드의 CardFlip에 뒷면(=뒤집었을 때 보일) 텍스처 주입
            var flip = cards[i].GetComponent<CardFlip>();
            if (flip != null && i < pool.Count)
                flip.SetBackTexture(pool[i]);
        }
    }

    // RPS enum → 카드 개수 매핑. 미선택(None) 등은 7로 처리(보호용 기본값)
    private static int ResolveCount(PracticeSetupManager.RPSType rps)
    {
        return rps switch
        {
            PracticeSetupManager.RPSType.RPS3 => 3,
            PracticeSetupManager.RPSType.RPS5 => 5,
            PracticeSetupManager.RPSType.RPS7 => 7,
            _ => 7
        };
    }

    // Fisher-Yates 셔플: 리스트 원소 위치를 균등 분포로 무작위화
    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
