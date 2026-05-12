using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Practice 씬의 카드 매니저.
// 사용자가 PracticeMode에서 고른 RPS 종류(3/5/7)에 따라
// - 표시할 카드 수를 결정하고
// - 각 카드에 속성 텍스처를 랜덤하게 배분한다.
// 진행 흐름: 씬 시작과 동시에 유저는 자유롭게 클릭 가능, AI도 무작위 딜레이 후 한 장을 자동으로 뒤집는다.
// "먼저 선택하는 사람이 임자" — 유저와 AI가 동시에 픽을 노리고, 먼저 클릭한 카드가 그 사람의 것으로 확정된다.
// 각자 한 장만 뽑을 수 있으며, 자신의 픽이 확정되면 더는 다른 카드를 클릭할 수 없다.
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

    // 드래프트 사이드 패널(픽 슬롯)에 표시할 와이드(3:1) Pick 카드 스프라이트.
    // 와이어 순서는 elementTextures와 동일: Fire_Pick, Water_Pick, Nature_Pick, Wind_Pick, Electric_Pick, Ice_Pick, Magic_Pick
    // 비어 있으면 DraftController가 GetCardSprite()로 자동 폴백한다.
    [SerializeField] private List<Sprite> elementPickSprites = new List<Sprite>();

    // 카드 길게 누름 시 표시할 상성표 스프라이트(1536x1024). 와이어 순서는 elementTextures와 동일.
    // 에디터에서는 미와이어 시 Assets/Image/Relationship/{element}_Rela.png에서 자동 로드한다.
    [SerializeField] private List<Sprite> elementRelaSprites = new List<Sprite>();

    // 매치 단계 듀얼 플립에서 카드 뒷면(=뒤집기 전 모습)으로 사용. 빌드본 호환을 위해 인스펙터에 와이어해야 함.
    // 에디터에서는 미와이어 시 Assets/Image/Card/Card_Back.png에서 자동 로드.
    [SerializeField] private Sprite cardBackSprite;

    // 드래프트 단계 UI(Image)에 쓸 Sprite 캐시. elementTextures가 Sprite 임포트 설정이므로 런타임에 Texture2D → Sprite로 1회 변환.
    private Sprite[] cachedCardSprites;

    // DraftController 등 외부에서 카드 이미지를 가져갈 때 사용. 인덱스가 범위를 벗어나면 null 반환.
    public Sprite GetCardSprite(ElementType element)
    {
        int idx = (int)element;
        if (idx < 0 || idx >= elementTextures.Count) return null;
        if (cachedCardSprites == null || cachedCardSprites.Length != elementTextures.Count)
            cachedCardSprites = new Sprite[elementTextures.Count];
        if (cachedCardSprites[idx] != null) return cachedCardSprites[idx];
        var tex2d = elementTextures[idx] as Texture2D;
        if (tex2d == null) return null;
        cachedCardSprites[idx] = Sprite.Create(
            tex2d,
            new Rect(0f, 0f, tex2d.width, tex2d.height),
            new Vector2(0.5f, 0.5f));
        return cachedCardSprites[idx];
    }

    // 사이드 패널 픽 슬롯용 와이드 Pick 카드 스프라이트.
    // 인스펙터 와이어가 있으면 그것을 우선 사용; 에디터에서는 미와이어 시 알려진 경로에서 자동 로드 → 와이어 없이 바로 테스트 가능.
    // 빌드 환경에서는 와이어가 없으면 null 반환 → 호출 측에서 Card 스프라이트로 폴백.
    public Sprite GetPickSprite(ElementType element)
    {
        int idx = (int)element;
        if (idx < 0) return null;

        if (idx < elementPickSprites.Count && elementPickSprites[idx] != null)
            return elementPickSprites[idx];

#if UNITY_EDITOR
        // 에디터 전용 자동 로드: Assets/Image/Draft_Image/Pick_Card/{element}_Pick.png
        var loaded = LoadPickSpriteFromAssetDatabase(element);
        if (loaded != null)
        {
            while (elementPickSprites.Count <= idx) elementPickSprites.Add(null);
            elementPickSprites[idx] = loaded;
            return loaded;
        }
#endif
        return null;
    }

#if UNITY_EDITOR
    // Sprite 모드(Multiple)로 임포트된 PNG에서 첫 Sprite 서브에셋을 꺼내 반환.
    private static Sprite LoadPickSpriteFromAssetDatabase(ElementType element)
    {
        return LoadSpriteAtPath($"Assets/Image/Draft_Image/Pick_Card/{element}_Pick.png");
    }

    private static Sprite LoadRelaSpriteFromAssetDatabase(ElementType element)
    {
        return LoadSpriteAtPath($"Assets/Image/Relationship/{element}_Rela.png");
    }

    private static Sprite LoadSpriteAtPath(string path)
    {
        var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
        if (assets != null)
        {
            foreach (var a in assets)
            {
                if (a is Sprite s) return s;
            }
        }
        // spriteMode가 Single이면 LoadAssetAtPath로 한 번 더 시도
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
#endif

    // 듀얼 플립용 카드 뒷면 스프라이트. 와이어 우선, 에디터에서는 미와이어 시 자동 로드.
    // 빌드본에서는 와이어가 없으면 null 반환 → 호출 측에서 폴백 처리 필요.
    public Sprite GetCardBackSprite()
    {
        if (cardBackSprite != null) return cardBackSprite;
#if UNITY_EDITOR
        var loaded = LoadSpriteAtPath("Assets/Image/Card/Card_Back.png");
        if (loaded != null)
        {
            cardBackSprite = loaded;
            return loaded;
        }
#endif
        return null;
    }

    // 카드 길게 누름 → 상성표 표시용 스프라이트. 와이어 우선, 에디터에서는 미와이어 시 자동 로드.
    public Sprite GetRelationshipSprite(ElementType element)
    {
        int idx = (int)element;
        if (idx < 0) return null;

        if (idx < elementRelaSprites.Count && elementRelaSprites[idx] != null)
            return elementRelaSprites[idx];

#if UNITY_EDITOR
        var loaded = LoadRelaSpriteFromAssetDatabase(element);
        if (loaded != null)
        {
            while (elementRelaSprites.Count <= idx) elementRelaSprites.Add(null);
            elementRelaSprites[idx] = loaded;
            return loaded;
        }
#endif
        return null;
    }

    // 매칭 결과 팝업 (Practice 씬에 미리 배치, 시작 시 비활성)
    [SerializeField] private GameObject resultPopup;
    [SerializeField] private TMP_Text resultLabel;
    [SerializeField] private Button confirmButton;

    // 선/후픽 선택 팝업 (유저 승리 시 표시). 두 버튼을 눌러 선픽 또는 후픽 결정.
    [SerializeField] private GameObject pickChoicePopup;
    [SerializeField] private Button firstPickButton;   // 선픽
    [SerializeField] private Button secondPickButton;  // 후픽

    // "상대가 선픽, 후픽 고르는 중..." 팝업 (유저 패배 시 표시).
    [SerializeField] private GameObject aiChoosingPopup;
    [SerializeField] private TMP_Text aiChoosingLabel; // 없어도 동작은 함 (인스펙터 텍스트로 대체 가능)

    // "드래프트로 이동중..." 팝업 (선/후픽 확정 직후 표시, 5초 후 자동 닫힘).
    [SerializeField] private GameObject draftTransitionPopup;
    [SerializeField] private TMP_Text draftTransitionLabel;

    // 씬 상단 Canvas 아래 두 그룹. 선/후픽 결정 단계와 드래프트 단계를 분리하여
    // 활성/비활성으로 화면 전환을 수행한다. 인스펙터에서 미리 만들어두면 그것을 사용,
    // 비어 있으면 EnsureSceneGroups에서 런타임에 자동 생성한다.
    [SerializeField] private GameObject decidingGroup; // Deciding_pick_order
    [SerializeField] private GameObject draftGroup;    // Draft

    // AI가 자동으로 카드를 뽑기까지 대기하는 무작위 딜레이 범위(초).
    // 매 라운드마다 [min, max)에서 균등 분포로 한 값을 뽑아 사용 → 유저가 이길 때도 있고 질 때도 있는 레이스가 된다.
    [SerializeField] private float aiPickDelayMin = 0.4f;
    [SerializeField] private float aiPickDelayMax = 1.2f;

    // AI가 선/후픽을 고르는 척하는 연출 딜레이(초)
    [SerializeField] private float aiChooseDelay = 1.5f;
    // 드래프트 이동 팝업이 화면에 떠 있는 시간(초)
    [SerializeField] private float draftTransitionAutoCloseSeconds = 5f;

    // 양 쪽 픽 추적
    private CardFlip userPick;
    private CardFlip aiPick;
    // AI가 이번 라운드에 뽑기로 결정한 카드 (Flip 호출 직전에 기록 → OnFlipStarted에서 AI/유저 구분에 사용)
    private CardFlip aiChosenCard;
    // 결과 표시용: 유저가 먼저 클릭했는지 여부
    private bool userPickedFirst;
    // 드래프트로 넘어갈 때 유저가 선픽인지 (선/후픽 결정 단계에서 확정된 값)
    private bool userTakesFirstPickInDraft;
    // 이번 라운드에 활성화된 카드들
    private readonly List<CardFlip> activeCardFlips = new List<CardFlip>();

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
                // 클릭 즉시(=뒤집기 시작 시) 픽 확정 / 애니메이션 완료 시 결과 표시 체크
                flip.OnFlipStarted += HandleCardFlipStarted;
                flip.OnFlipped += HandleCardFlipped;
                activeCardFlips.Add(flip);
            }
        }

        // Canvas 아래 Deciding_pick_order / Draft 두 그룹 구조를 보장.
        // 기존 자식들(카드/결과 팝업 등)은 Deciding_pick_order로 묶이고, Draft는 비활성으로 대기.
        EnsureSceneGroups();

        // 인스펙터에서 새 팝업들을 안 꽂아도 테스트 가능하도록 빠진 것들은 런타임 생성
        // (위에서 만들어진 Deciding_pick_order의 자식으로 들어간다)
        EnsurePopups();

        // 모든 팝업은 시작 시 숨김
        if (resultPopup != null) resultPopup.SetActive(false);
        if (pickChoicePopup != null) pickChoicePopup.SetActive(false);
        if (aiChoosingPopup != null) aiChoosingPopup.SetActive(false);
        if (draftTransitionPopup != null) draftTransitionPopup.SetActive(false);

        // 결과 팝업 확인 → outcome에 따라 다음 페이즈(선/후픽 결정)로 진입
        if (confirmButton != null) confirmButton.onClick.AddListener(OnResultConfirmed);
        // 유저가 승리 시 누르는 선/후픽 버튼 (EnsurePopups에서 자동 생성된 버튼도 동일하게 후킹)
        if (firstPickButton != null) firstPickButton.onClick.AddListener(() => OnUserChosePickOrder(true));
        if (secondPickButton != null) secondPickButton.onClick.AddListener(() => OnUserChosePickOrder(false));

        // 시리즈 상태 초기화 (다판제 점수/라운드)
        SeriesState.Reset(PracticeSettings.Format);

        // 유저는 처음부터 자유롭게 클릭 가능. AI는 무작위 딜레이 후 한 장을 자동으로 뒤집는다.
        // 둘 중 누가 먼저 클릭하는지에 따라 픽 소유권이 결정된다.
        StartCoroutine(AiPickRoutine());
    }

    // 카드의 Flip()이 호출된 *순간* 동기적으로 실행 (애니메이션 시작 직전).
    // 여기서 픽 소유권을 즉시 확정하므로, 애니메이션 0.3초 동안 유저가 다른 카드를 추가로 클릭해도 무효.
    // aiChosenCard와 일치하면 AI의 픽, 그 외에는 유저가 클릭한 픽으로 본다.
    private void HandleCardFlipStarted(CardFlip flip)
    {
        bool isAi = flip == aiChosenCard;
        if (isAi)
        {
            if (aiPick != null) return; // AI는 한 번만 픽
            if (userPick == null) userPickedFirst = false; // AI가 먼저
            aiPick = flip;
        }
        else
        {
            if (userPick != null) return; // 유저도 한 번만 픽
            if (aiPick == null) userPickedFirst = true; // 유저가 먼저
            userPick = flip;
            // 유저는 한 장만 가질 수 있으므로, 자기 픽이 확정되는 즉시 다른 카드 버튼을 잠근다.
            LockRemainingUserClicks();
        }
    }

    // 유저가 픽한 직후 호출: 아직 뒤집히지 않은 활성 카드들의 Button을 비활성화.
    // (AI가 이미 뽑아 둔 카드는 flipped=true라서 CardFlip 자체 가드가 막아준다.)
    private void LockRemainingUserClicks()
    {
        foreach (var f in activeCardFlips)
        {
            if (f == null || f.IsFlipped) continue;
            var btn = f.GetComponent<Button>();
            if (btn != null) btn.interactable = false;
        }
    }

    // 카드 뒤집기 애니메이션이 끝났을 때 호출. 양쪽 픽이 모두 결정되고 두 카드 모두 뒤집힘 완료면 결과 표시.
    private void HandleCardFlipped(CardFlip flip)
    {
        if (userPick != null && aiPick != null && userPick.IsFlipped && aiPick.IsFlipped)
        {
            ShowResultPopup();
        }
    }

    // 무작위 딜레이 후, 아직 뒤집히지 않은 카드 중 하나를 AI가 자동으로 뒤집는다.
    // 유저가 이미 한 장을 가져갔다면 자연스럽게 남은 카드들 중에서만 고르게 된다.
    private IEnumerator AiPickRoutine()
    {
        float delay = Random.Range(aiPickDelayMin, aiPickDelayMax);
        yield return new WaitForSeconds(delay);

        var candidates = new List<CardFlip>();
        foreach (var f in activeCardFlips)
        {
            if (f == null || f.IsFlipped) continue;
            candidates.Add(f);
        }
        if (candidates.Count == 0) yield break;

        aiChosenCard = candidates[Random.Range(0, candidates.Count)];
        aiChosenCard.Flip();
    }

    private void ShowResultPopup()
    {
        if (resultPopup == null) return;

        var outcome = TypeChart.GetOutcome(userPick.Element, aiPick.Element);
        if (resultLabel != null)
        {
            string firstPicker = userPickedFirst ? "내가 먼저" : "AI가 먼저";
            resultLabel.text =
                $"[{firstPicker}]\n나: {userPick.Element}\nAI: {aiPick.Element}\n결과: {OutcomeKor(outcome)}";
        }
        resultPopup.SetActive(true);
    }

    // 결과 팝업 확인 버튼.
    //  [일반 라운드 1 흐름]
    //    Win  → 유저가 선/후픽 선택 팝업
    //    Lose → AI가 무작위로 선/후픽 (AiChoosing 팝업)
    //    Tie  → 씬 리로드 없이 카드 상태 초기화 후 재시도 (시리즈 점수 보존)
    //
    //  [결판전 흐름 (SeriesState.TiebreakerInProgress == true)]
    //    카드 뒤집기 승자가 +1 시리즈 점수를 얻고, 카드 뒤집기 패자가 다음 라운드 선/후픽을 선택한다.
    //    Win  → 유저 +1점 → AI가 선/후픽 선택 (AiChoosing 팝업)
    //    Lose → AI +1점   → 유저가 선/후픽 선택 (PickChoice 팝업)
    //    Tie  → 결판 안 났으니 카드 다시 뒤집기
    public void OnResultConfirmed()
    {
        if (userPick == null || aiPick == null) return;
        if (resultPopup != null) resultPopup.SetActive(false);

        var outcome = TypeChart.GetOutcome(userPick.Element, aiPick.Element);

        if (SeriesState.TiebreakerInProgress)
        {
            switch (outcome)
            {
                case MatchOutcome.Win:
                    SeriesState.PlayerScore++;
                    SeriesState.TiebreakerInProgress = false;
                    if (SeriesState.IsSeriesOver) ShowSeriesEndIndicator();
                    else ShowAiChoosingFlow(); // AI가 결판전 패자 → AI가 선/후픽 선택
                    break;
                case MatchOutcome.Lose:
                    SeriesState.AiScore++;
                    SeriesState.TiebreakerInProgress = false;
                    if (SeriesState.IsSeriesOver) ShowSeriesEndIndicator();
                    else ShowPickChoiceFlow(); // 유저가 결판전 패자 → 유저가 선/후픽 선택
                    break;
                case MatchOutcome.Tie:
                default:
                    StartCoroutine(RetryCardFlipAfterDelay()); // 결판 안 남, 재시도
                    break;
            }
            return;
        }

        // 일반 모드 (라운드 1: 승자가 선/후픽 선택)
        switch (outcome)
        {
            case MatchOutcome.Win:
                ShowPickChoiceFlow();
                break;
            case MatchOutcome.Lose:
                ShowAiChoosingFlow();
                break;
            case MatchOutcome.Tie:
            default:
                StartCoroutine(RetryCardFlipAfterDelay());
                break;
        }
    }

    // 선/후픽 선택 팝업 표시 (유저가 직접 선택)
    private void ShowPickChoiceFlow()
    {
        if (pickChoicePopup != null) pickChoicePopup.SetActive(true);
    }

    // AI가 선/후픽을 고르는 척하는 연출 팝업 + 코루틴
    private void ShowAiChoosingFlow()
    {
        if (aiChoosingPopup != null)
        {
            aiChoosingPopup.SetActive(true);
            if (aiChoosingLabel != null)
                aiChoosingLabel.text = "상대가 선픽, 후픽 고르는 중...";
        }
        StartCoroutine(AiChooseRoutine());
    }

    // 결판전이 시리즈를 결정지은 경우(예: BO1에서 1라운드 동점 → 결판전이 곧 시리즈 종료) 호출.
    // 결과 팝업을 종료 메시지로 재활용하고 확인 버튼을 잠가 더 이상 라운드가 진행되지 않게 한다.
    private void ShowSeriesEndIndicator()
    {
        if (resultLabel != null)
        {
            string winner = SeriesState.PlayerWonSeries ? "내가" : "상대가";
            resultLabel.text =
                $"시리즈 종료\n\n{winner} 우승!\n\n최종 점수: 나 {SeriesState.PlayerScore} - {SeriesState.AiScore} 상대";
        }
        if (resultPopup != null) resultPopup.SetActive(true);
        if (confirmButton != null) confirmButton.interactable = false;
    }

    // 카드 뒤집기 동점 시 짧은 딜레이 후 카드 상태를 초기화하고 새 카드 뒤집기 시도 (씬 리로드 X)
    private IEnumerator RetryCardFlipAfterDelay()
    {
        yield return new WaitForSeconds(0.6f);
        ResetCardFlipState();
        StartCoroutine(AiPickRoutine());
    }

    // userPick/aiPick/aiChosenCard 등 카드 뒤집기 상태를 초기 상태로 되돌린다.
    // CardFlip.ResetFlip()으로 뒷면 → 앞면 시각 복귀 + Button.interactable 복원도 함께 수행.
    private void ResetCardFlipState()
    {
        userPick = null;
        aiPick = null;
        aiChosenCard = null;
        userPickedFirst = false;
        foreach (var f in activeCardFlips)
        {
            if (f == null) continue;
            f.ResetFlip();
            var btn = f.GetComponent<Button>();
            if (btn != null) btn.interactable = true;
        }
    }

    // DraftController가 "다음 라운드" 버튼을 누른 사용자에게서 호출.
    // 시리즈가 이미 끝났으면 아무 일도 안 하고, 아니면 카드 뒤집기 상태 초기화 + 그룹 전환 + 적절한 모드로 진입.
    public void BeginNextRound()
    {
        if (SeriesState.IsSeriesOver) return;

        SeriesState.CurrentRound++;

        // 두 그룹 활성/비활성 스왑: 결정 단계 표시, 드래프트 숨김
        if (decidingGroup != null) decidingGroup.SetActive(true);
        if (draftGroup != null) draftGroup.SetActive(false);

        // 모든 팝업 숨김 (이전 라운드 잔재 제거)
        if (resultPopup != null) resultPopup.SetActive(false);
        if (pickChoicePopup != null) pickChoicePopup.SetActive(false);
        if (aiChoosingPopup != null) aiChoosingPopup.SetActive(false);
        if (draftTransitionPopup != null) draftTransitionPopup.SetActive(false);

        // 카드/픽 상태 초기화 + 다음 라운드를 위해 카드 재배치(셔플)
        ResetCardFlipState();
        ReshuffleCards();

        if (SeriesState.LastRoundTied)
        {
            // 결판전: 카드 뒤집기 재진행 (승자 +1점, 패자가 선/후픽)
            SeriesState.TiebreakerInProgress = true;
            StartCoroutine(AiPickRoutine());
        }
        else
        {
            // 카드 뒤집기 스킵: 직전 라운드 패자가 곧바로 선/후픽
            SeriesState.TiebreakerInProgress = false;
            bool playerWasLoser = !SeriesState.LastRoundPlayerWon;
            if (playerWasLoser) ShowPickChoiceFlow();
            else ShowAiChoosingFlow();
        }
    }

    // 카드 위치(속성)를 매 라운드 새로 무작위 배분 — 결판전/일반 라운드 모두 같은 방식
    private void ReshuffleCards()
    {
        int count = ResolveCount(PracticeSettings.Rps);
        var elementPool = new List<ElementType>(count);
        for (int i = 0; i < count; i++) elementPool.Add((ElementType)i);
        Shuffle(elementPool);

        int poolIdx = 0;
        for (int i = 0; i < cards.Count; i++)
        {
            bool active = i < count;
            if (cards[i] != null) cards[i].SetActive(active);
            if (!active) continue;
            var flip = cards[i].GetComponent<CardFlip>();
            if (flip != null && poolIdx < elementPool.Count)
            {
                var element = elementPool[poolIdx];
                Texture tex = ((int)element) < elementTextures.Count
                    ? elementTextures[(int)element]
                    : null;
                flip.SetCard(element, tex);
                poolIdx++;
            }
        }
    }

    // 유저가 승리 후 선/후픽을 직접 선택했을 때 호출 (firstPickButton / secondPickButton에서 연결)
    private void OnUserChosePickOrder(bool userTakesFirstPick)
    {
        if (pickChoicePopup != null) pickChoicePopup.SetActive(false);
        ShowDraftTransitionPopup(userTakesFirstPick);
    }

    // AI가 선/후픽을 무작위로 결정하는 연출 코루틴 (유저 패배 시 사용)
    private IEnumerator AiChooseRoutine()
    {
        yield return new WaitForSeconds(aiChooseDelay);
        // 50/50로 AI가 선픽 또는 후픽 선택 → 유저는 반대
        bool aiTakesFirstPick = Random.value < 0.5f;
        if (aiChoosingPopup != null) aiChoosingPopup.SetActive(false);
        ShowDraftTransitionPopup(userTakesFirstPick: !aiTakesFirstPick);
    }

    // 선/후픽이 확정된 후 양쪽의 픽 순서를 보여주는 팝업. 일정 시간 후 자동으로 닫힌다.
    private void ShowDraftTransitionPopup(bool userTakesFirstPick)
    {
        // 드래프트 진입 시 DraftController에 넘길 값 저장
        userTakesFirstPickInDraft = userTakesFirstPick;
        if (draftTransitionPopup == null) return;
        draftTransitionPopup.SetActive(true);
        if (draftTransitionLabel != null)
        {
            string userOrder = userTakesFirstPick ? "선픽" : "후픽";
            string aiOrder = userTakesFirstPick ? "후픽" : "선픽";
            draftTransitionLabel.text =
                $"상대: {aiOrder}\n나: {userOrder}\n\n드래프트로 이동중...";
        }
        StartCoroutine(AutoCloseDraftTransition());
    }

    private IEnumerator AutoCloseDraftTransition()
    {
        yield return new WaitForSeconds(draftTransitionAutoCloseSeconds);
        if (draftTransitionPopup != null) draftTransitionPopup.SetActive(false);

        // 드래프트 단계로 전환: Deciding_pick_order 그룹 전체를 끄고 Draft 그룹을 켠다
        if (decidingGroup != null) decidingGroup.SetActive(false);
        if (draftGroup != null)
        {
            draftGroup.SetActive(true);
            // DraftController에 RPS 카드 수와 선픽 정보를 넘겨 UI 빌드 + 첫 턴 시작
            var draft = draftGroup.GetComponent<DraftController>();
            if (draft != null)
            {
                int rpsCount = ResolveCount(PracticeSettings.Rps);
                TMP_FontAsset font = resultLabel != null ? resultLabel.font : null;
                // DraftController에 self 참조를 넘겨, "다음 라운드" 버튼에서 BeginNextRound를 호출할 수 있게 함
                draft.StartDraft(userTakesFirstPickInDraft, rpsCount, font, this);
            }
        }
    }

    // Canvas 아래에 'Deciding_pick_order'와 'Draft' 두 그룹을 보장한다.
    // - 두 그룹은 화면 전체를 덮는 RectTransform 컨테이너
    // - Canvas의 기존 자식(카드/결과 팝업 등)은 모두 Deciding_pick_order 안으로 이동
    // - Draft는 비활성 상태로 대기 (드래프트 이동 시 활성화)
    private void EnsureSceneGroups()
    {
        var canvas = FindUiCanvas();
        if (canvas == null) return;

        // 인스펙터에 미리 만들어져 있으면 그것 사용, 없으면 이름으로 검색, 그래도 없으면 생성
        if (decidingGroup == null)
        {
            var found = canvas.transform.Find("Deciding_pick_order");
            decidingGroup = found != null ? found.gameObject : CreateFullScreenChild(canvas.transform, "Deciding_pick_order");
        }
        if (draftGroup == null)
        {
            var found = canvas.transform.Find("Draft");
            draftGroup = found != null ? found.gameObject : CreateFullScreenChild(canvas.transform, "Draft");
        }

        // Canvas의 직속 자식 중 두 그룹 외의 모든 것을 Deciding_pick_order 안으로 이동
        var toMove = new List<Transform>();
        foreach (Transform child in canvas.transform)
        {
            if (child == decidingGroup.transform) continue;
            if (child == draftGroup.transform) continue;
            toMove.Add(child);
        }
        foreach (var t in toMove)
        {
            // worldPositionStays=false: 부모(decidingGroup)가 Canvas와 동일 full-screen RT라 anchor 기반 위치가 그대로 유지된다
            t.SetParent(decidingGroup.transform, false);
        }

        // 초기 활성 상태 보장: 결정 그룹은 활성, Draft 그룹은 비활성
        decidingGroup.SetActive(true);
        draftGroup.SetActive(false);

        // Draft 그룹에 DraftController가 없으면 자동 부착 — 전환 시 StartDraft 호출만으로 UI가 구성됨
        if (draftGroup.GetComponent<DraftController>() == null)
        {
            draftGroup.AddComponent<DraftController>();
        }
    }

    // 인스펙터에서 와이어링되지 않은 팝업/버튼을 런타임에 즉석으로 생성한다.
    // 별도 작업 없이 씬을 실행해서 전체 흐름을 테스트할 수 있도록 하는 안전장치.
    // 부모는 Deciding_pick_order로 두어, 드래프트 진입 시 그룹 비활성으로 한꺼번에 사라지게 한다.
    private void EnsurePopups()
    {
        Transform parent = decidingGroup != null ? decidingGroup.transform : (FindUiCanvas() != null ? FindUiCanvas().transform : null);
        if (parent == null) return;
        // 기존 resultLabel과 동일한 폰트(=온글잎 긍정 SDF 등)를 자동 상속
        TMP_FontAsset font = resultLabel != null ? resultLabel.font : null;

        if (pickChoicePopup == null)
        {
            pickChoicePopup = CreatePopupPanel("PickChoicePopup_Auto", parent);
            var title = CreateTmpLabel(pickChoicePopup.transform, "선/후픽을 선택하세요", font, 44f);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 0.55f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.offsetMin = new Vector2(24f, 0f);
            titleRt.offsetMax = new Vector2(-24f, -24f);

            if (firstPickButton == null)
                firstPickButton = CreateUiButton(pickChoicePopup.transform, "선픽", new Vector2(-140f, -70f), font);
            if (secondPickButton == null)
                secondPickButton = CreateUiButton(pickChoicePopup.transform, "후픽", new Vector2(140f, -70f), font);
        }

        if (aiChoosingPopup == null)
        {
            aiChoosingPopup = CreatePopupPanel("AiChoosingPopup_Auto", parent);
            aiChoosingLabel = CreateTmpLabel(aiChoosingPopup.transform, "상대가 선픽, 후픽 고르는 중...", font, 40f);
        }

        if (draftTransitionPopup == null)
        {
            draftTransitionPopup = CreatePopupPanel("DraftTransitionPopup_Auto", parent);
            draftTransitionLabel = CreateTmpLabel(draftTransitionPopup.transform, "드래프트로 이동중...", font, 40f);
        }
    }

    // Canvas의 자식으로 화면 전체를 덮는 빈 컨테이너(RectTransform)를 생성
    private static GameObject CreateFullScreenChild(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return go;
    }

    // 씬에 이미 있는 Canvas 찾기 — resultPopup의 부모 Canvas를 우선 사용
    private Canvas FindUiCanvas()
    {
        if (resultPopup != null)
        {
            var c = resultPopup.GetComponentInParent<Canvas>();
            if (c != null) return c;
        }
        return FindObjectOfType<Canvas>();
    }

    // 중앙에 배치된 반투명 어두운 패널 (모든 팝업의 공통 배경)
    private static GameObject CreatePopupPanel(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(720f, 360f);
        rt.anchoredPosition = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.85f);

        go.transform.SetAsLastSibling(); // 다른 UI 위에 오도록
        return go;
    }

    // 부모 영역을 가득 채우는 TMP 라벨을 생성하고 폰트/크기/정렬을 세팅
    private static TextMeshProUGUI CreateTmpLabel(Transform parent, string text, TMP_FontAsset font, float fontSize)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        if (font != null) tmp.font = font;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        var rt = tmp.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(24f, 24f);
        rt.offsetMax = new Vector2(-24f, -24f);
        return tmp;
    }

    // 흰색 배경 + 검은 글씨의 단순한 클릭 가능 버튼 생성 (Hover/Pressed 컬러 포함)
    private static Button CreateUiButton(Transform parent, string label, Vector2 anchoredPosition, TMP_FontAsset font)
    {
        var go = new GameObject("Button_" + label,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(220f, 90f);
        rt.anchoredPosition = anchoredPosition;

        var img = go.GetComponent<Image>();
        img.color = Color.white;

        var btn = go.GetComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        btn.colors = colors;

        var labelTmp = CreateTmpLabel(go.transform, label, font, 36f);
        labelTmp.color = Color.black;
        return btn;
    }

    private static string OutcomeKor(MatchOutcome o) => o switch
    {
        MatchOutcome.Win => "내 승리",
        MatchOutcome.Lose => "AI 승리",
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
