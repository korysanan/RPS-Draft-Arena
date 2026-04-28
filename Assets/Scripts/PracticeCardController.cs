using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

    // 매칭 결과 팝업 (Practice 씬에 미리 배치, 시작 시 비활성)
    [SerializeField] private GameObject resultPopup;
    [SerializeField] private TMP_Text resultLabel;
    [SerializeField] private Button confirmButton;

    // 1번째/2번째 픽 추적용
    private CardFlip pickA;
    private CardFlip pickB;

    private void Start()
    {
        // RPS 종류에 따라 사용할 카드 수 결정 (3/5/7)
        int count = ResolveCount(PracticeSettings.Rps);

        // 속성 인덱스 풀: 0..count-1 == ElementType의 앞쪽 N개와 정확히 일치
        // (RPS-3 → Fire/Water/Nature, RPS-5 → +Wind/Electric, RPS-7 → 전부)
        var elementPool = new List<ElementType>(count);
        for (int i = 0; i < count; i++)
            elementPool.Add((ElementType)i);
        // 카드별 위치를 매번 무작위로 만들기 위해 섞기
        Shuffle(elementPool);

        // 각 카드를 순회하며 N개만 활성화하고 속성+텍스처 배분
        for (int i = 0; i < cards.Count; i++)
        {
            bool active = i < count;
            if (cards[i] != null)
                cards[i].SetActive(active);
            if (!active) continue;

            var flip = cards[i].GetComponent<CardFlip>();
            if (flip != null && i < elementPool.Count)
            {
                var element = elementPool[i];
                Texture tex = ((int)element) < elementTextures.Count
                    ? elementTextures[(int)element]
                    : null;
                flip.SetCard(element, tex);
                // 뒤집힘 완료 시 결과 처리 핸들러 등록
                flip.OnFlipped += HandleCardFlipped;
            }
        }

        // 팝업 초기 상태: 숨김 + 확인 버튼은 씬 리로드(=초기화)에 연결
        if (resultPopup != null) resultPopup.SetActive(false);
        if (confirmButton != null) confirmButton.onClick.AddListener(OnResetClicked);
    }

    // CardFlip이 뒤집힘 끝났을 때 호출. 1, 2번째 픽을 추적하다가 2번째에 결과 팝업 표시.
    private void HandleCardFlipped(CardFlip flip)
    {
        if (pickA == null)
        {
            pickA = flip;
            return;
        }
        if (pickB == null && flip != pickA)
        {
            pickB = flip;
            ShowResultPopup();
        }
    }

    private void ShowResultPopup()
    {
        if (resultPopup == null) return;

        var outcome = TypeChart.GetOutcome(pickA.Element, pickB.Element);
        if (resultLabel != null)
        {
            resultLabel.text =
                $"A: {pickA.Element}\nB: {pickB.Element}\n결과: {OutcomeKor(outcome)}";
        }
        resultPopup.SetActive(true);
    }

    // 확인 버튼: 같은 씬을 재로드해서 카드/팝업/픽 상태를 모두 초기화 (셔플도 새로)
    public void OnResetClicked()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private static string OutcomeKor(MatchOutcome o) => o switch
    {
        MatchOutcome.Win => "A 승리",
        MatchOutcome.Lose => "B 승리",
        MatchOutcome.Tie => "무승부",
        _ => "-"
    };

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
