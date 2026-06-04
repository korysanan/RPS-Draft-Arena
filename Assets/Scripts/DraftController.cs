using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 드래프트(밴픽) 화면 컨트롤러.
// - 좌측 'A (Player)' 슬롯 7칸, 우측 'B (Other Player)' 슬롯 7칸
// - 가운데 RPS 카운트만큼의 속성 카드 (각 카드는 여러 번 픽 가능 — 사라지지 않음)
// - 픽 순서는 스네이크 드래프트: 선픽이 A면 A B B A A B B A A B B A A B (총 14턴)
// - 내 차례엔 카드 클릭, AI 차례엔 일정 딜레이 후 자동 픽
// - 픽한 속성 이름이 해당 측 슬롯 라벨에 텍스트로 표시됨
public class DraftController : MonoBehaviour
{
    internal const int PicksPerSide = 7;   // DraftAI 도 후보 버퍼 capacity 로 참조
    private const float AiPickDelay = 1.5f;
    // 한 턴당 픽 제한 시간(초). 시간 초과 시 플레이어는 선택된 카드(없으면 무작위)로 자동 픽.
    private const float PickTimeLimit = 15f;

    private TMP_FontAsset font;

    // UI 참조 (모두 BuildUi에서 새로 생성됨)
    private readonly List<TMP_Text> playerSlotLabels = new List<TMP_Text>();
    private readonly List<TMP_Text> aiSlotLabels = new List<TMP_Text>();
    // 사이드 패널 슬롯 안에 들어가는 카드 이미지 (픽 전까지 sprite=null, 픽 시점에 채워짐)
    private readonly List<Image> playerSlotCards = new List<Image>();
    private readonly List<Image> aiSlotCards = new List<Image>();
    private readonly List<Button> cardButtons = new List<Button>();
    private readonly List<ElementType> cardElements = new List<ElementType>();
    private TMP_Text turnLabel;

    // 픽 진행 상태
    private bool[] pickOrder; // true=player, false=AI
    private int currentPickIndex;
    private int playerCursor;
    private int aiCursor;

    // 픽 이력 — 난이도 Hard/Normal AI의 상성 평가에 사용
    private readonly List<ElementType> playerPickHistory = new List<ElementType>();
    private readonly List<ElementType> aiPickHistory = new List<ElementType>();

    // 현재 플레이어가 클릭해서 "선택만" 한 카드 (아직 픽 확정 전).
    // -1이면 선택 없음. 결정 버튼은 이 값이 유효할 때만 활성화된다.
    private int selectedCardIndex = -1;
    // 하단 중앙 "결정" 버튼 (BuildCenter에서 생성)
    private Button confirmPickButton;
    // 현재 턴의 카운트다운 코루틴 (다음 턴 시작 시 StopCoroutine으로 중단)
    private Coroutine turnTimerCoroutine;

    // 매치 단계용 상태/UI
    private RectTransform centerColTransform;            // 픽/매치 단계에서 공통 재사용하는 중앙 컬럼
    // 패 확인(30초) / 매 매치 픽 제한(20초) 시간, 총 매치 수(5)
    private const float HandReviewTimeLimit = 30f;
    private const float MatchPickTimeLimit = 20f;
    internal const int TotalMatches = 5;   // DraftAI 가 남은 매치 계산에 참조
    // 사용 여부 추적 (PicksPerSide 길이, 슬롯 인덱스 기준)
    private bool[] playerUsed;
    private bool[] aiUsed;
    // 5번의 1:1 매치에서 양쪽이 낸 카드 기록
    private readonly List<ElementType> playerMatchHistory = new List<ElementType>();
    private readonly List<ElementType> aiMatchHistory = new List<ElementType>();
    // 좌/우 슬롯 패널에 누적되는 "사용됨" 오버레이 (다음 라운드 시 정리용)
    private readonly List<GameObject> playerSlotOverlays = new List<GameObject>();
    private readonly List<GameObject> aiSlotOverlays = new List<GameObject>();
    // 매치 단계 UI 참조
    private TMP_Text matchTitleLabel;
    private TMP_Text matchTimerLabel;
    private Button matchPickButton;
    private readonly List<Button> matchCardButtons = new List<Button>();
    private readonly List<Image> matchCardImages = new List<Image>();
    // 매치 진행 상태
    private int currentMatchIndex;        // 0~4
    private int selectedMatchSlotIndex;   // 내가 클릭한 카드의 슬롯 인덱스(=playerPickHistory[i]); -1=없음
    private Coroutine matchTimerCoroutine;

    // 시리즈(다판제) 흐름을 위해 PracticeCardController 참조와 팝업 GameObject들을 보관
    private PracticeCardController practiceController;
    private GameObject finalOrderOverlay;
    private GameObject matchStartPopup;
    private GameObject matchResultPopup;
    // 라운드 결과 팝업의 "세부 결과" 버튼으로 여는 상세 요약 팝업 (finalOrderOverlay 자식 → 함께 정리됨)
    private GameObject roundDetailPopup;
    // 카드 길게 누름 시 표시하는 상성표 오버레이 (떼면 자동 닫힘)
    private GameObject relationshipOverlay;
    // 매치 단계 듀얼 플립 오버레이 (베팅 확정 후 카드 뒤집기 연출용; 라운드 시작 시 정리)
    private GameObject duelFlipOverlay;
    // 드래프트 단계 CenterColumn 좌하단의 작은 버튼 + 누르고 있는 동안 표시되는 팝업 (떼면 자동 닫힘)
    private GameObject draftInfoButton;
    private GameObject draftInfoPopup;
    private int currentRpsCount;   // 이번 라운드 RPS 수(3/5/7) — 안내 팝업 이미지 선택용

    // 베팅(포인트) 시스템. StartingPoints/MinBetPerMatch 는 DraftAI 도 참조하므로 internal.
    internal const int StartingPoints = 100;
    internal const int MinBetPerMatch = 5;

    // AI 베팅/픽/베이지안 튜닝 상수는 모두 DraftAI 로 이동했다 (난이도별 전략은 DraftAI 가 담당).

    // wallet: 베팅 가능 풀 (시작 100, 최대 100). 베팅 시 차감, 무승부/승리 시 회수, 패배 시 영구 손실.
    private int playerWallet;
    private int aiWallet;
    // earnings: 베팅 수익 누적. 승리 시 +bet, 패배/무 시 변화 없음. 베팅에는 사용 불가.
    private int playerEarnings;
    private int aiEarnings;
    // 매 매치 베팅 기록 (라운드 요약용)
    private readonly List<int> playerBetHistory = new List<int>();
    private readonly List<int> aiBetHistory = new List<int>();

    // AI 의사결정 모듈 (드래프트/매치/베팅/베이지안). DraftController 가 한 번 생성되어 시리즈 내내
    // 재사용되므로, ai 도 최초 StartDraft 에서 한 번만 생성해 베이지안 학습 상태가 라운드 간 지속되게 한다.
    private DraftAI ai;

    // ── DraftAI 가 읽는 게임 상태 (읽기 전용 접근자) ──────────────────────────
    // AI 는 게임 상태를 변경하지 않고 이 접근자들로 읽기만 한다. 값 타입(지갑/매치 인덱스)은
    // 호출 시점의 라이브 값을, 컬렉션은 IReadOnlyList 로 노출해 외부 변경을 막는다.
    internal IReadOnlyList<ElementType> CardElements => cardElements;
    internal IReadOnlyList<ElementType> PlayerPickHistory => playerPickHistory;
    internal IReadOnlyList<ElementType> AiPickHistory => aiPickHistory;
    internal IReadOnlyList<ElementType> PlayerMatchHistory => playerMatchHistory;
    internal IReadOnlyList<ElementType> AiMatchHistory => aiMatchHistory;
    internal IReadOnlyList<int> PlayerBetHistory => playerBetHistory;
    internal bool[] PlayerUsed => playerUsed;
    internal bool[] AiUsed => aiUsed;
    internal int AiWallet => aiWallet;
    internal int AiEarnings => aiEarnings;
    internal int PlayerWallet => playerWallet;
    internal int PlayerEarnings => playerEarnings;
    internal int CurrentMatchIndex => currentMatchIndex;

    // 베팅 팝업 상태/UI
    private GameObject betPopup;
    private TMP_Text betValueLabel;
    private TMP_Text betTimerLabel;   // 베팅 팝업 안의 남은 시간 표시 (픽~베팅 공통 20초 타이머)
    private Button betMinusBtn;
    private Button betPlusBtn;
    private int currentBetValue;
    private int currentBetMin;
    private int currentBetMax;
    private int pendingPlayerSlotIdx; // 픽 버튼 직후 보관 (베팅 확정 시 사용)
    // 사이드 패널 헤더 라벨 — 이제 이름만 표시 (BuildUi에서 1회 설정, 변동 없음)
    private TMP_Text playerHeaderLabel;
    private TMP_Text aiHeaderLabel;

    // 시리즈 승 박스 + 수익 위젯 — 중앙 컬럼 상단 좌/우 코너에 배치.
    // 홈 버튼처럼 Draft 루트(transform)에 부착되어 패 확인/매치 단계의 중앙 컬럼 재빌드에도 유지된다.
    // 박스는 RoundsToWin개를 만들어 시리즈 승수만큼 채운다(예: BO5 → 3개). 수익은 그 아래 큰 숫자.
    private readonly List<Image> playerSeriesBoxes = new List<Image>();
    private readonly List<Image> aiSeriesBoxes = new List<Image>();
    private TMP_Text playerEarningsLabel;
    private TMP_Text aiEarningsLabel;
    // 시리즈 승 박스 색: 빈 칸(어두운 반투명, 흰 외곽선으로 가시성 확보) / 채운 칸(왼쪽=파랑, 오른쪽=빨강)
    private static readonly Color SeriesBoxEmptyColor = new Color(0f, 0f, 0f, 0.35f);
    private static readonly Color SeriesBoxPlayerFilledColor = new Color(0.25f, 0.5f, 1f, 1f); // 왼쪽(플레이어) 승: 파랑
    private static readonly Color SeriesBoxAiFilledColor = new Color(1f, 0.3f, 0.3f, 1f);       // 오른쪽(AI) 승: 빨강

    // 카드 색상 상수 — 이제 카드 이미지(Sprite)에 색을 입혀 표현.
    //   기본은 흰색(원본 색 그대로), 선택 시 노란 틴트가 카드 위에 살짝 깔린다.
    private static readonly Color CardDefaultColor = Color.white;
    private static readonly Color CardSelectedColor = new Color(1f, 0.85f, 0.3f, 1f); // 노란 하이라이트

    // 외부(PracticeCardController)에서 호출. 픽 순서와 카드 수, "다음 라운드"용 컨트롤러 참조를 받는다.
    // 매 라운드 다시 호출 가능 — BuildUi가 자식과 리스트를 모두 비우므로 동일 인스턴스에서 N라운드 반복 OK.
    public void StartDraft(bool playerGoesFirst, int rpsCount, TMP_FontAsset fontAsset, PracticeCardController controller = null)
    {
        font = fontAsset;
        practiceController = controller;

        // AI 모듈은 시리즈 전체에서 한 번만 생성 (베이지안 학습 상태를 라운드 간 보존).
        if (ai == null) ai = new DraftAI(this);

        // Phase 4: 라운드 1 (= 시리즈 첫 라운드) → 베이지안 모델 완전 초기화. 그 외 라운드 → 중립값 쪽으로 감쇠.
        // Hard + 다판제가 아니면 두 메서드 모두 내부에서 early-return.
        if (SeriesState.CurrentRound <= 1)
            ai.ResetBayesianModel();
        else
            ai.DecayBayesianModelForNewRound();

        // 이전 라운드의 잔여 오버레이/팝업 정리
        if (finalOrderOverlay != null) { Destroy(finalOrderOverlay); finalOrderOverlay = null; }
        if (matchStartPopup != null) { Destroy(matchStartPopup); matchStartPopup = null; }
        if (matchResultPopup != null) { Destroy(matchResultPopup); matchResultPopup = null; }
        if (betPopup != null) { Destroy(betPopup); betPopup = null; }
        betTimerLabel = null;
        if (relationshipOverlay != null) { Destroy(relationshipOverlay); relationshipOverlay = null; }
        if (duelFlipOverlay != null) { Destroy(duelFlipOverlay); duelFlipOverlay = null; }
        if (draftInfoPopup != null) { Destroy(draftInfoPopup); draftInfoPopup = null; }
        // 매치 단계 잔여 상태 정리 (좌/우 슬롯 오버레이는 BuildUi의 자식 destroy에 함께 사라지므로 리스트만 비움)
        playerMatchHistory.Clear();
        aiMatchHistory.Clear();
        playerSlotOverlays.Clear();
        aiSlotOverlays.Clear();
        matchCardButtons.Clear();
        matchCardImages.Clear();
        currentMatchIndex = 0;
        selectedMatchSlotIndex = -1;
        // 베팅 시스템 라운드 초기화 — 매 라운드 wallet 100/earnings 0부터 새로 시작
        playerWallet = StartingPoints;
        aiWallet = StartingPoints;
        playerEarnings = 0;
        aiEarnings = 0;
        playerBetHistory.Clear();
        aiBetHistory.Clear();

        BuildUi(rpsCount);
        pickOrder = BuildPickOrder(PicksPerSide * 2, playerGoesFirst);
        currentPickIndex = 0;
        playerCursor = 0;
        aiCursor = 0;
        if (UIClickAudio.Instance != null) UIClickAudio.Instance.PlayDraftBgm();
        AdvanceTurn();
    }

    // 스네이크 드래프트 순서. 4턴 단위로 [first, !first, !first, first] 패턴 반복.
    // (예: 선픽=Player → P A A P P A A P P A A P P A — 각 측 7회씩 총 14회)
    private static bool[] BuildPickOrder(int totalPicks, bool firstIsPlayer)
    {
        var group = new[] { firstIsPlayer, !firstIsPlayer, !firstIsPlayer, firstIsPlayer };
        var order = new bool[totalPicks];
        for (int i = 0; i < totalPicks; i++) order[i] = group[i % 4];
        return order;
    }

    private void AdvanceTurn()
    {
        // 매 턴 시작 시 이전 턴의 선택/버튼 상태는 항상 초기화
        ClearSelection();
        if (confirmPickButton != null) confirmPickButton.interactable = false;

        // 이전 턴 카운트다운 중단 (이미 종료된 경우엔 no-op)
        if (turnTimerCoroutine != null)
        {
            StopCoroutine(turnTimerCoroutine);
        }

        if (currentPickIndex >= pickOrder.Length)
        {
            // 드래프트 종료 → 패 확인 단계로 진입 (중앙 UI 교체 + 30초 타이머 시작)
            EnterHandReviewPhase();
            return;
        }
        bool playerTurn = pickOrder[currentPickIndex];
        // 카드 클릭은 내 차례에만 허용
        SetCardsInteractable(playerTurn);
        // 15초 카운트다운 시작 — 라벨 텍스트도 이 코루틴이 갱신
        turnTimerCoroutine = StartCoroutine(TurnTimerRoutine(playerTurn));
        if (!playerTurn)
        {
            StartCoroutine(AiPickRoutine());
        }
    }

    // 매 프레임 남은 시간을 줄이며 턴 라벨에 표시. 0이 되면 플레이어에 한해 자동 픽 호출.
    // (AI는 AiPickRoutine이 1.5초 후 픽하므로 사실상 타임아웃이 나지 않음)
    private IEnumerator TurnTimerRoutine(bool playerTurn)
    {
        float remaining = PickTimeLimit;
        while (remaining > 0f)
        {
            UpdateTurnLabel(playerTurn, remaining);
            yield return null;
            remaining -= Time.deltaTime;
        }
        UpdateTurnLabel(playerTurn, 0f);
        if (playerTurn)
        {
            AutoPickPlayer();
        }
        // turnTimerCoroutine 필드는 null로 비우지 않음 — 다음 AdvanceTurn에서 StartCoroutine으로 덮어쓰므로
        // 여기서 null을 대입하면 자동 픽 → AdvanceTurn 흐름이 막 시작한 새 코루틴 핸들을 지워버릴 위험이 있음.
    }

    // 턴 라벨에 누구 차례인지와 남은 시간을 함께 표시
    private void UpdateTurnLabel(bool playerTurn, float remaining)
    {
        if (turnLabel == null) return;
        int secs = Mathf.CeilToInt(Mathf.Max(0f, remaining));
        string who = playerTurn ? "내 차례 — 카드 선택 후 결정" : "상대(AI) 차례...";
        turnLabel.text = $"{who}  (남은 시간: {secs}초)";
    }

    // 시간 초과 시 플레이어 자동 픽.
    //  - 이미 선택된 카드(하이라이트)가 있으면 그 카드를 픽
    //  - 선택이 없으면 카드 풀에서 무작위 픽
    private void AutoPickPlayer()
    {
        if (pickOrder == null || currentPickIndex >= pickOrder.Length) return;
        if (cardElements.Count == 0) return;

        int idx = (selectedCardIndex >= 0 && selectedCardIndex < cardElements.Count)
            ? selectedCardIndex
            : Random.Range(0, cardElements.Count);
        var element = cardElements[idx];

        // 시각/상태 정리 후 픽 확정
        SetCardHighlight(selectedCardIndex, false);
        selectedCardIndex = -1;
        if (confirmPickButton != null) confirmPickButton.interactable = false;
        RecordPick(isPlayer: true, element);
    }

    private IEnumerator AiPickRoutine()
    {
        yield return new WaitForSeconds(AiPickDelay);
        if (cardElements.Count == 0) yield break;
        var pick = ai.ChooseAiPick();
        RecordPick(isPlayer: false, pick);
    }

    // (AI 픽 결정 로직 ChooseAiPick/ChooseStrategicPick/ScoreElement 등은 DraftAI 로 이동)

    // 플레이어가 카드 클릭 → 즉시 픽 확정이 아니라 "선택" 상태로만 두고, 결정 버튼이 활성화된다.
    // 다른 카드를 클릭하면 선택이 그쪽으로 옮겨가고, 이전 카드의 하이라이트는 제거된다.
    private void OnCardClicked(int cardIndex)
    {
        if (pickOrder == null || currentPickIndex >= pickOrder.Length) return;
        if (!pickOrder[currentPickIndex]) return; // 플레이어 차례가 아니면 무시
        if (cardIndex < 0 || cardIndex >= cardButtons.Count) return;

        // 이전 선택 카드의 하이라이트 원복
        SetCardHighlight(selectedCardIndex, false);
        // 새 카드 하이라이트
        selectedCardIndex = cardIndex;
        SetCardHighlight(selectedCardIndex, true);
        // 결정 버튼 활성화 — 이제 결정 누를 수 있음
        if (confirmPickButton != null) confirmPickButton.interactable = true;
    }

    // 결정 버튼 클릭 → 현재 선택된 카드를 픽으로 확정한다.
    private void OnConfirmPickClicked()
    {
        if (selectedCardIndex < 0 || selectedCardIndex >= cardElements.Count) return;
        if (pickOrder == null || currentPickIndex >= pickOrder.Length) return;
        if (!pickOrder[currentPickIndex]) return; // 안전: 플레이어 차례에서만 동작

        var element = cardElements[selectedCardIndex];
        // 시각/상태 정리
        SetCardHighlight(selectedCardIndex, false);
        selectedCardIndex = -1;
        if (confirmPickButton != null) confirmPickButton.interactable = false;

        RecordPick(isPlayer: true, element);
    }

    // 카드 이미지 색상을 선택/기본 상태로 토글
    private void SetCardHighlight(int idx, bool highlighted)
    {
        if (idx < 0 || idx >= cardButtons.Count) return;
        var img = cardButtons[idx].targetGraphic as Image;
        if (img != null) img.color = highlighted ? CardSelectedColor : CardDefaultColor;
    }

    // 현재 선택 상태를 비우고 모든 카드 하이라이트를 기본 색으로 되돌림
    private void ClearSelection()
    {
        if (selectedCardIndex >= 0) SetCardHighlight(selectedCardIndex, false);
        selectedCardIndex = -1;
    }

    private void RecordPick(bool isPlayer, ElementType element)
    {
        if (UIClickAudio.Instance != null) UIClickAudio.Instance.PlayDraftCardPick();
        // 사이드 패널 슬롯에는 와이드 Pick 이미지를 표시 (없으면 Card 이미지로 폴백)
        var slotSprite = GetPickSprite(element) ?? GetCardSprite(element);
        if (isPlayer)
        {
            if (playerCursor < playerSlotLabels.Count)
            {
                playerSlotLabels[playerCursor].text = string.Empty; // 카드 이미지가 자리하므로 텍스트는 비움
                if (playerCursor < playerSlotCards.Count && playerSlotCards[playerCursor] != null)
                {
                    playerSlotCards[playerCursor].sprite = slotSprite;
                    playerSlotCards[playerCursor].enabled = slotSprite != null;
                }
                playerCursor++;
            }
            playerPickHistory.Add(element);
        }
        else
        {
            if (aiCursor < aiSlotLabels.Count)
            {
                aiSlotLabels[aiCursor].text = string.Empty;
                if (aiCursor < aiSlotCards.Count && aiSlotCards[aiCursor] != null)
                {
                    aiSlotCards[aiCursor].sprite = slotSprite;
                    aiSlotCards[aiCursor].enabled = slotSprite != null;
                }
                aiCursor++;
            }
            aiPickHistory.Add(element);
        }
        currentPickIndex++;
        AdvanceTurn();
    }

    // PracticeCardController가 주입한 카드 스프라이트 조회. controller가 없으면 null 반환 (text-only fallback)
    private Sprite GetCardSprite(ElementType element)
    {
        return practiceController != null ? practiceController.GetCardSprite(element) : null;
    }

    // 사이드 패널 픽 슬롯에 쓸 와이드(3:1) Pick 스프라이트. 미와이어/미존재 시 null → 호출 측에서 Card로 폴백.
    private Sprite GetPickSprite(ElementType element)
    {
        return practiceController != null ? practiceController.GetPickSprite(element) : null;
    }

    // 카드 길게 누름 시 표시할 상성표 스프라이트.
    private Sprite GetRelationshipSprite(ElementType element)
    {
        return practiceController != null ? practiceController.GetRelationshipSprite(element) : null;
    }

    // 카드를 1초 이상 누른 순간 호출 — 캔버스 위에 전체 화면 오버레이로 상성표를 띄운다.
    // 오버레이는 raycastTarget=false라 카드의 PointerExit이 잘못 발동되지 않는다.
    private void ShowRelationshipChart(ElementType element)
    {
        if (relationshipOverlay != null) { Destroy(relationshipOverlay); relationshipOverlay = null; }

        var sprite = GetRelationshipSprite(element);
        if (sprite == null) return;

        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        relationshipOverlay = new GameObject("RelationshipOverlay",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        relationshipOverlay.transform.SetParent(canvas.transform, false);
        var overlayRt = (RectTransform)relationshipOverlay.transform;
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;
        var bgImg = relationshipOverlay.GetComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.6f);
        bgImg.raycastTarget = false; // 카드가 계속 포인터 이벤트를 받게 함
        relationshipOverlay.transform.SetAsLastSibling();

        // 가운데에 상성표 이미지 (3:2 비율 1536x1024 → preserveAspect로 비율 유지)
        var chartGo = new GameObject("Chart",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        chartGo.transform.SetParent(relationshipOverlay.transform, false);
        var chartRt = (RectTransform)chartGo.transform;
        chartRt.anchorMin = new Vector2(0.1f, 0.1f);
        chartRt.anchorMax = new Vector2(0.9f, 0.9f);
        chartRt.offsetMin = Vector2.zero;
        chartRt.offsetMax = Vector2.zero;
        var chartImg = chartGo.GetComponent<Image>();
        chartImg.sprite = sprite;
        chartImg.preserveAspect = true;
        chartImg.raycastTarget = false;
    }

    // 카드에서 손을 떼는(또는 카드 밖으로 벗어나는) 즉시 오버레이를 닫는다.
    private void HideRelationshipChart()
    {
        if (relationshipOverlay != null) { Destroy(relationshipOverlay); relationshipOverlay = null; }
    }

    private void SetCardsInteractable(bool interactable)
    {
        foreach (var b in cardButtons)
        {
            if (b != null) b.interactable = interactable;
        }
    }

    // 기존 자식(placeholder 등) 제거 후 좌/우/중앙 3열 레이아웃을 새로 만든다
    private void BuildUi(int rpsCount)
    {
        // 자식 모두 제거 (placeholder 라벨이나 이전 빌드 잔재)
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
        playerSlotLabels.Clear();
        aiSlotLabels.Clear();
        playerSlotCards.Clear();
        aiSlotCards.Clear();
        cardButtons.Clear();
        cardElements.Clear();
        playerPickHistory.Clear();
        aiPickHistory.Clear();

        var rootRt = (RectTransform)transform;

        // 전체 배경 — PracticeCardController에서 받은 sprite를 사용, 없으면 흰색 폴백
        var bg = MakeRect("Background", rootRt, Vector2.zero, Vector2.one);
        var bgImg = AddImage(bg, Color.white);
        var bgSprite = practiceController != null ? practiceController.DraftPlayBackgroundSprite : null;
        if (bgSprite != null)
        {
            bgImg.sprite = bgSprite;
            // 배경은 화면을 가득 채워야 하므로 preserveAspect는 끔
        }

        var left = MakeColumn("LeftColumn", 0f, 0.2f);
        var center = MakeColumn("CenterColumn", 0.2f, 0.8f);
        var right = MakeColumn("RightColumn", 0.8f, 1f);
        centerColTransform = center; // 순서변경 단계에서 재사용

        // LeftColumn Header (inspector: Left=50.16815, Top=103.5002, Right=-3.163649, Bottom=42.03275)
        // 헤더에는 플레이어 이름만 표시 (시리즈 승/수익은 중앙 컬럼 위젯으로 분리됨)
        playerHeaderLabel = BuildSidePanel(
            left, "Player", playerSlotLabels, playerSlotCards,
            slotHorizontalInset: 31f,
            headerOffsetMin: new Vector2(50.16815f, 42.03275f),
            headerOffsetMax: new Vector2(3.163649f, -103.5002f));
        // RightColumn Header (inspector: Left=-4.067551, Top=97.6246, Right=58.75545, Bottom=40.2248)
        // 이름 + 괄호 안 AI 난이도(영문)를 헤더에 표기
        aiHeaderLabel = BuildSidePanel(
            right, $"AI ({DifficultyEng(PracticeSettings.Difficulty)})", aiSlotLabels, aiSlotCards,
            slotHorizontalInset: -31f,
            headerOffsetMin: new Vector2(-4.067551f, 40.2248f),
            headerOffsetMax: new Vector2(-58.75545f, -97.6246f));
        BuildCenter(center, rpsCount);
        // 시리즈 승 박스 + 수익 위젯 (중앙 컬럼 상단 좌/우). UpdateSidePointsDisplay보다 먼저 만들어야 채워진다.
        BuildSeriesWidgets();
        UpdateSidePointsDisplay();
    }

    // 중앙 컬럼 위젯의 시리즈 승 박스와 수익 숫자를 갱신. 매치 진행마다 호출된다.
    // (시리즈 점수는 라운드 종료 시에만 바뀌고, 수익은 매치마다 바뀐다.)
    private void UpdateSidePointsDisplay()
    {
        UpdateSeriesBoxes(playerSeriesBoxes, SeriesState.PlayerScore, SeriesBoxPlayerFilledColor);
        UpdateSeriesBoxes(aiSeriesBoxes, SeriesState.AiScore, SeriesBoxAiFilledColor);
        SetEarningsLabel(playerEarningsLabel, playerEarnings);
        SetEarningsLabel(aiEarningsLabel, aiEarnings);
    }

    // 앞에서부터 filled개의 박스를 filledColor로 채우고 나머지는 빈 칸 색으로 되돌린다.
    private static void UpdateSeriesBoxes(List<Image> boxes, int filled, Color filledColor)
    {
        for (int i = 0; i < boxes.Count; i++)
        {
            if (boxes[i] != null)
                boxes[i].color = i < filled ? filledColor : SeriesBoxEmptyColor;
        }
    }

    // 수익 숫자 — 부호 없이 숫자 그대로, 색은 항상 흰색 (수익은 음수가 되지 않음)
    private static void SetEarningsLabel(TMP_Text label, int earnings)
    {
        if (label == null) return;
        label.text = earnings.ToString();
        label.color = Color.white;
    }

    private static string FormatSigned(int v) => v > 0 ? "+" + v : v.ToString();

    // 중앙 컬럼 상단 좌/우 코너에 "시리즈 승 박스 + 수익" 위젯 2개를 만든다.
    // Draft 루트(transform)에 직접 부착 → 패 확인/매치 단계에서 중앙 컬럼이 비워져도 유지된다.
    // 루트 x 0.2~0.8 구간이 중앙 컬럼이고, x 0.44~0.56엔 홈 버튼이 있으므로 그 양옆 코너를 사용한다.
    private void BuildSeriesWidgets()
    {
        var rootRt = (RectTransform)transform;
        playerSeriesBoxes.Clear();
        aiSeriesBoxes.Clear();
        // 인스펙터 Left/Top/Right/Bottom → offsetMin=(Left,Bottom), offsetMax=(-Right,-Top)
        // 좌측(플레이어): Left=69, Top=27, Right=-69, Bottom=-27
        playerEarningsLabel = BuildSeriesWidget(rootRt, new Vector2(0.205f, 0.85f), new Vector2(0.40f, 0.99f),
            offsetMin: new Vector2(69f, -27f), offsetMax: new Vector2(69f, -27f), boxesOut: playerSeriesBoxes);
        // 우측(AI): Left=-69, Top=27, Right=69, Bottom=-27
        aiEarningsLabel = BuildSeriesWidget(rootRt, new Vector2(0.60f, 0.85f), new Vector2(0.795f, 0.99f),
            offsetMin: new Vector2(-69f, -27f), offsetMax: new Vector2(-69f, -27f), boxesOut: aiSeriesBoxes);
    }

    // 한쪽 위젯: 위 절반에 시리즈 승 박스 N개(가로 나열, N=RoundsToWin), 아래 절반에 큰 수익 숫자.
    private TMP_Text BuildSeriesWidget(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, List<Image> boxesOut)
    {
        var container = MakeRect("SeriesWidget", parent, anchorMin, anchorMax);
        container.offsetMin = offsetMin;
        container.offsetMax = offsetMax;

        int boxCount = Mathf.Max(1, SeriesState.RoundsToWin);
        // 박스 줄 (위쪽 절반)
        var boxRow = MakeRect("SeriesBoxes", container, new Vector2(0f, 0.5f), new Vector2(1f, 1f));

        // 개수와 무관하게 항상 같은 정사각형(고정 px) 으로, 가운데 기준 가로 정렬.
        // 1920x1080 레퍼런스 기준 값 — 캔버스 스케일러가 해상도에 맞게 함께 스케일한다.
        const float boxSize = 75f;   // 정사각형 한 변(px) — 박스 줄 높이와 비슷한 크기
        const float boxScale = 0.7f; // 박스 축소 비율
        const float boxGap = 12f;    // 렌더 크기 기준 박스 사이 간격(px)
        float step = boxSize * boxScale + boxGap; // 박스 중심 간 간격
        for (int i = 0; i < boxCount; i++)
        {
            // 박스 줄 정중앙에 점 앵커로 두고, 중심에서 좌우로 대칭 배치
            var box = MakeRect("Box" + i, boxRow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            box.pivot = new Vector2(0.5f, 0.5f);
            box.sizeDelta = new Vector2(boxSize, boxSize);
            box.anchoredPosition = new Vector2((i - (boxCount - 1) / 2f) * step, 0f);
            box.localScale = new Vector3(boxScale, boxScale, 1f);
            var boxImg = AddImage(box, SeriesBoxEmptyColor);
            // 어두운 빈 칸이 잘 보이도록 흰색 외곽선 추가
            var outline = box.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(2f, 2f);
            boxesOut.Add(boxImg);
        }

        // 수익 숫자 (아래쪽 절반, 크게)
        var earnRect = MakeRect("Earnings", container, new Vector2(0f, 0f), new Vector2(1f, 0.5f));
        var earnLbl = AddTmpLabel(earnRect, "0", 40f, TextAlignmentOptions.Center);
        earnLbl.color = Color.white;
        earnLbl.fontStyle = FontStyles.Bold;
        earnLbl.enableWordWrapping = false;
        earnLbl.enableAutoSizing = true;
        earnLbl.fontSizeMin = 18f;
        earnLbl.fontSizeMax = 48f;
        return earnLbl;
    }

    private RectTransform MakeColumn(string name, float xMin, float xMax)
    {
        var rt = MakeRect(name, (RectTransform)transform, new Vector2(xMin, 0f), new Vector2(xMax, 1f));
        return rt;
    }

    // 측면 패널: 상단 헤더(이름 + 시리즈 + 포인트) + 아래 7개 슬롯(번갈아 음영). 헤더 라벨은 포인트 갱신용으로 반환.
    // 픽 시점에 슬롯 내부의 카드 이미지(child Image)에 sprite를 채워 넣는다.
    private TMP_Text BuildSidePanel(RectTransform col, string headerText, List<TMP_Text> slotOut, List<Image> slotCardOut, float slotHorizontalInset = 0f, Vector2 headerOffsetMin = default, Vector2 headerOffsetMax = default)
    {
        const float headerHeightRatio = 0.20f;
        const float slotsTopRatio = 1f - headerHeightRatio;

        // 헤더
        var header = MakeRect("Header", col, new Vector2(0f, slotsTopRatio), new Vector2(1f, 1f));
        header.offsetMin = headerOffsetMin;
        header.offsetMax = headerOffsetMax;
        AddImage(header, new Color(0.78f, 0.78f, 0.78f, 1f)).enabled = false;
        var headerLabel = AddTmpLabel(header, headerText, 18f, TextAlignmentOptions.Center);
        headerLabel.color = Color.white;

        // 슬롯 7개 (헤더 아래 영역을 균등 분할)
        float slotH = slotsTopRatio / PicksPerSide;
        for (int i = 0; i < PicksPerSide; i++)
        {
            float top = slotsTopRatio - i * slotH;
            float bot = top - slotH;
            var slot = MakeRect("Slot" + i, col, new Vector2(0f, bot), new Vector2(1f, top));
            // 컬럼 안에서 좌/우로 밀고(slotHorizontalInset) 위아래로 50px 안쪽으로 당김 (inspector: Left/Right=±31, Top=-50, Bottom=50)
            slot.offsetMin = new Vector2(slotHorizontalInset, 50f);
            slot.offsetMax = new Vector2(slotHorizontalInset, 50f);
            var color = i % 2 == 0
                ? new Color(0.92f, 0.92f, 0.92f, 1f)
                : new Color(0.82f, 0.82f, 0.82f, 1f);
            AddImage(slot, color).enabled = false;

            // 카드 이미지 자리(child) — 픽 전엔 enabled=false라 흰 박스가 보이지 않음. 픽 확정 시 sprite 주입과 함께 켜진다.
            var cardRt = MakeRect("CardImage", slot, Vector2.zero, Vector2.one);
            cardRt.offsetMin = new Vector2(6f, 4f);
            cardRt.offsetMax = new Vector2(-6f, -4f);
            var cardImg = cardRt.gameObject.AddComponent<Image>();
            cardImg.preserveAspect = true;
            cardImg.raycastTarget = false;
            cardImg.color = Color.white;
            cardImg.sprite = null;
            cardImg.enabled = false;
            slotCardOut.Add(cardImg);

            // 라벨은 빈 문자열로 유지 (사용됨 오버레이 부착 시 slot RT를 찾는 앵커 역할만 함)
            var lbl = AddTmpLabel(slot, "", 22f, TextAlignmentOptions.Center);
            lbl.color = Color.black;
            slotOut.Add(lbl);
        }
        return headerLabel;
    }

    // 중앙: 홈 버튼 + 정보 라벨("남은 시간 / 차례") + 가운데 카드 영역
    private void BuildCenter(RectTransform col, int rpsCount)
    {
        currentRpsCount = rpsCount;

        // 최상단 중앙: 홈 버튼 — 누르면 확인 팝업 표시. Draft 루트에 부착되어 단계 전환 후에도 유지된다.
        BuildHomeButton();

        // 타이머/턴 라벨 (현재는 턴 안내로 사용; 타이머 텍스트는 placeholder)
        var infoRect = MakeRect("Info", col, new Vector2(0f, 0.84f), new Vector2(1f, 0.92f));
        turnLabel = AddTmpLabel(infoRect, "남은 시간 : 00s", 24f, TextAlignmentOptions.Center);
        turnLabel.color = Color.white;
        // 인스펙터 기준 Top=100, Bottom=-90 (= offsetMax.y=-100, offsetMin.y=-90) — 카드 영역 쪽으로 내려서 배치
        var turnLabelRt = turnLabel.rectTransform;
        turnLabelRt.offsetMin = new Vector2(turnLabelRt.offsetMin.x, -90f);
        turnLabelRt.offsetMax = new Vector2(turnLabelRt.offsetMax.x, -100f);

        // 난이도는 우측 B 헤더에 표기됨 → 별도 라벨 제거.

        // 카드 영역
        var cardArea = MakeRect("Cards", col, new Vector2(0.05f, 0.1f), new Vector2(0.95f, 0.82f));

        // 속성 풀: 앞쪽 rpsCount개 (Fire/Water/Nature/...)
        for (int i = 0; i < rpsCount; i++) cardElements.Add((ElementType)i);

        // 행 분배: RPS3 → 1+2 / RPS5 → 3+2 / RPS7 → 3+4 (피라미드)
        int[] rowCounts = rpsCount switch
        {
            3 => new[] { 1, 2 },
            5 => new[] { 3, 2 },
            7 => new[] { 3, 4 },
            _ => new[] { rpsCount }
        };

        int elemIdx = 0;
        int totalRows = rowCounts.Length;
        for (int r = 0; r < totalRows; r++)
        {
            int count = rowCounts[r];
            float rowH = 1f / totalRows;
            float rowTop = 1f - r * rowH;
            float rowBot = rowTop - rowH;
            for (int c = 0; c < count; c++)
            {
                float colW = 1f / count;
                float xMin = c * colW + colW * 0.18f;
                float xMax = (c + 1) * colW - colW * 0.18f;
                var cardRect = MakeRect(
                    "Card_" + cardElements[elemIdx],
                    cardArea,
                    new Vector2(xMin, rowBot + rowH * 0.12f),
                    new Vector2(xMax, rowTop - rowH * 0.12f));
                // Image에 카드 스프라이트를 채우고 preserveAspect로 카드 비율 유지(=선픽/후픽 패와 동일한 느낌).
                var img = AddImage(cardRect, CardDefaultColor);
                img.sprite = GetCardSprite(cardElements[elemIdx]);
                img.preserveAspect = true;

                // Button은 interactable 게이팅과 색상 틴트(=targetGraphic)용으로만 유지하고,
                // 실제 입력은 CardPressHandler로 라우팅 → 짧은 클릭=선택 / 1초 길게 누름=상성표 표시.
                var btn = cardRect.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;
                int captured = elemIdx; // 클로저 캡쳐 — 각 버튼이 자기 인덱스를 보존
                var element = cardElements[captured];
                var press = cardRect.gameObject.AddComponent<CardPressHandler>();
                press.longPressDuration = 1f;
                press.onClick = () => OnCardClicked(captured);
                press.onLongPressStart = () => ShowRelationshipChart(element);
                press.onLongPressEnd = HideRelationshipChart;
                cardButtons.Add(btn);
                elemIdx++;
            }
        }

        // 하단 중앙 결정 버튼: 플레이어가 카드를 선택한 뒤 눌러야 픽이 확정된다.
        // 카드 영역(y 0.1) 아래 빈 공간(0.0~0.1)에 가로 30% 폭으로 배치.
        var confirmRect = MakeRect("ConfirmPickButton", col, new Vector2(0.35f, 0.02f), new Vector2(0.65f, 0.1f));
        var confirmImg = AddImage(confirmRect, Color.white);
        var selectSprite = practiceController != null ? practiceController.SelectButtonSprite : null;
        if (selectSprite != null)
        {
            confirmImg.sprite = selectSprite;
            confirmImg.preserveAspect = true;
        }
        else
        {
            // sprite 미지정 시에만 텍스트 폴백 — sprite 있으면 이미지에 텍스트 포함되어 있다고 가정하고 라벨 생략
            var confirmLbl = AddTmpLabel(confirmRect, "결정", 28f, TextAlignmentOptions.Center);
            confirmLbl.color = Color.black;
        }

        confirmPickButton = confirmRect.gameObject.AddComponent<Button>();
        confirmPickButton.targetGraphic = confirmImg;
        // 비활성 상태에서도 시각 차이가 나도록 disabledColor를 살짝 흐리게 지정
        var colors = confirmPickButton.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        colors.disabledColor = new Color(0.78f, 0.78f, 0.78f, 0.6f);
        confirmPickButton.colors = colors;
        confirmPickButton.onClick.AddListener(OnConfirmPickClicked);
        confirmPickButton.interactable = false; // 시작 시: 선택된 카드가 없으므로 비활성

        // 상성표(차트) 버튼 — 홈 버튼처럼 Draft 루트에 부착해 단계 전환(패 확인/매치)에도 유지된다.
        BuildChartButton();
    }

    // 상성표 버튼: 누르고 있는 동안 RPS 안내 팝업을 표시. 홈 버튼과 동일하게 Draft 루트(transform)에
    // 직접 부착해 centerColTransform 자식이 통째로 파괴되는 단계 전환에도 사라지지 않게 한다.
    private void BuildChartButton()
    {
        // BuildHomeButton과 동일한 이유로 가드 없이 매 라운드 새로 만든다(루트 자식이라 단계 전환엔 유지됨).
        var rootRt = (RectTransform)transform;
        // CenterColumn(루트 x 0.2~0.8, y 0~1)의 좌하단 0.02~0.12 / 0.02~0.11에 해당하는 루트 좌표로 환산.
        var infoBtnRect = MakeRect("DraftInfoButton", rootRt, new Vector2(0.212f, 0.02f), new Vector2(0.272f, 0.11f));
        // 인스펙터 기준 Left=27, Top=-19, Right=-27, Bottom=19
        infoBtnRect.offsetMin = new Vector2(27f, 19f);
        infoBtnRect.offsetMax = new Vector2(27f, 19f);
        infoBtnRect.SetAsLastSibling(); // 다른 단계 UI 위에 그려지도록
        var infoBtnImg = AddImage(infoBtnRect, Color.white);
        var infoBtnSprite = LoadCompetitionSprite("btn_chart_circle.png");
        if (infoBtnSprite != null) infoBtnImg.sprite = infoBtnSprite;
        infoBtnImg.preserveAspect = true;
        var infoBtn = infoBtnRect.gameObject.AddComponent<Button>();
        infoBtn.targetGraphic = infoBtnImg;
        var infoColors = infoBtn.colors;
        infoColors.normalColor = Color.white;
        infoColors.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        infoColors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        infoBtn.colors = infoColors;
        // duration 0 → 누르는 즉시 onLongPressStart, 떼면 onLongPressEnd (= 누르고 있는 동안만 팝업 표시)
        var infoPress = infoBtnRect.gameObject.AddComponent<CardPressHandler>();
        infoPress.longPressDuration = 0f;
        infoPress.onLongPressStart = ShowDraftInfoPopup;
        infoPress.onLongPressEnd = HideDraftInfoPopup;
        draftInfoButton = infoBtnRect.gameObject;
    }

    // 좌하단 버튼을 누르고 있는 동안 표시되는 팝업. 내용 이미지는 추후 적용 예정 — 지금은 자리만 잡은 빈 박스.
    private void ShowDraftInfoPopup()
    {
        if (draftInfoPopup != null) { Destroy(draftInfoPopup); draftInfoPopup = null; }

        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        draftInfoPopup = new GameObject("DraftInfoPopup",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        draftInfoPopup.transform.SetParent(canvas.transform, false);
        var overlayRt = (RectTransform)draftInfoPopup.transform;
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;
        var bgImg = draftInfoPopup.GetComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.6f);
        bgImg.raycastTarget = false; // 버튼이 계속 포인터 이벤트(뗌 감지)를 받도록 함
        draftInfoPopup.transform.SetAsLastSibling();

        // 가운데에 선택한 RPS 수에 맞는 안내 이미지 (rps3/5/7.png). 비율 유지.
        var boxGo = new GameObject("Box", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        boxGo.transform.SetParent(draftInfoPopup.transform, false);
        var boxRt = (RectTransform)boxGo.transform;
        boxRt.anchorMin = new Vector2(0.15f, 0.15f);
        boxRt.anchorMax = new Vector2(0.85f, 0.85f);
        boxRt.offsetMin = Vector2.zero;
        boxRt.offsetMax = Vector2.zero;
        var boxImg = boxGo.GetComponent<Image>();
        var rpsSprite = LoadCompetitionSprite($"rps{currentRpsCount}.png");
        if (rpsSprite != null)
        {
            boxImg.sprite = rpsSprite;
            boxImg.color = Color.white;
            boxImg.preserveAspect = true;
        }
        else
        {
            boxImg.color = new Color(0.12f, 0.12f, 0.12f, 0.97f); // 스프라이트 미발견 시 폴백
        }
        boxImg.raycastTarget = false;
    }

    // 버튼에서 손을 떼는(또는 영역을 벗어나는) 즉시 팝업을 닫는다.
    private void HideDraftInfoPopup()
    {
        if (draftInfoPopup != null) { Destroy(draftInfoPopup); draftInfoPopup = null; }
    }

    // 드래프트 화면 최상단 중앙의 홈 버튼. 클릭 시 확인 팝업을 띄우고, "돌아가기"면 PracticeMode 씬으로 복귀.
    // Draft 루트(transform)에 직접 부착해서 centerColTransform 자식이 통째로 파괴되는
    // EnterHandReviewPhase / BeginMatch 사이에도 유지되도록 한다.
    private GameObject draftHomeConfirmPopup;
    private GameObject draftHomeButton;

    private void BuildHomeButton()
    {
        // 매 라운드 BuildUi가 루트 자식을 모두 Destroy(프레임 끝 지연)하므로, 여기서 옛 버튼 참조를
        // != null 로 체크해 재사용하려 하면 "아직 파괴 전" 이라 새 버튼을 못 만들고 → 다음 라운드에
        // 홈 버튼이 사라진다. BuildHomeButton은 라운드당 한 번만 호출되고 단계 전환 중엔 재호출되지
        // 않으므로(루트 자식이라 단계 전환에도 유지됨), 가드 없이 매 라운드 새로 만드는 게 맞다.

        var rootRt = (RectTransform)transform;
        // 중앙 컬럼이 화면의 x 0.2~0.8(폭 60%)에 있으므로, 그 안에서 x 0.4~0.6에 해당하는
        // 루트 좌표 x 0.44~0.56로 배치하면 동일한 가운데 위치가 된다.
        var rect = MakeRect("HomeButton", rootRt, new Vector2(0.44f, 0.93f), new Vector2(0.56f, 1.00f));
        // 인스펙터 기준 Top=35, Bottom=-35 (= offsetMax.y=-35, offsetMin.y=-35)
        rect.offsetMin = new Vector2(rect.offsetMin.x, -35f);
        rect.offsetMax = new Vector2(rect.offsetMax.x, -35f);
        // 살짝 축소해서 다른 UI와 균형 (Scale 0.85)
        rect.localScale = new Vector3(0.85f, 0.85f, 0.85f);
        rect.SetAsLastSibling(); // 다른 단계 UI 위에 그려지도록
        var sprite = practiceController != null ? practiceController.HomeButtonSprite : null;
        var img = AddImage(rect, Color.white);
        if (sprite != null)
        {
            img.sprite = sprite;
            img.preserveAspect = true;
        }
        else
        {
            // 스프라이트가 없으면 기존의 흰색 박스 + "홈" 텍스트 폴백
            img.color = new Color(0.95f, 0.95f, 0.95f, 1f);
            var lbl = AddTmpLabel(rect, "홈", 24f, TextAlignmentOptions.Center);
            lbl.color = Color.black;
        }
        var btn = rect.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(OnDraftHomeButtonClicked);
        draftHomeButton = rect.gameObject;
    }

    private void OnDraftHomeButtonClicked()
    {
        EnsureDraftHomeConfirmPopup();
        if (draftHomeConfirmPopup != null) draftHomeConfirmPopup.SetActive(true);
    }

    // 확인 팝업을 한 번만 생성해서 재사용. Draft 루트(transform) 직속 자식으로 두어 모든 단계에서 위에 뜨도록 함.
    private void EnsureDraftHomeConfirmPopup()
    {
        if (draftHomeConfirmPopup != null) return;

        var rootRt = (RectTransform)transform;
        var overlayRt = MakeRect("HomeConfirmPopup", rootRt, Vector2.zero, Vector2.one);
        AddImage(overlayRt, new Color(0f, 0f, 0f, 0.6f));

        // 픽 단계의 HomeReturnPopup과 동일한 "고정 픽셀" 레이아웃으로 맞춘다.
        // (이전엔 비율 앵커 + preserveAspect라 화면 비율/해상도에 따라 크기가 깨졌다.
        //  HomeReturnPopup은 RawImage가 고정 rect를 꽉 채우는 방식이라 preserveAspect=false로 동일하게 채운다.)
        var dialogRt = MakeRect("Dialog", overlayRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        dialogRt.sizeDelta = new Vector2(800f, 400f);
        dialogRt.anchoredPosition = Vector2.zero;
        var dialogImg = AddImage(dialogRt, Color.white);
        var bgSprite = practiceController != null ? practiceController.HomeConfirmBgSprite : null;
        if (bgSprite != null) { dialogImg.sprite = bgSprite; dialogImg.preserveAspect = false; }
        else { dialogImg.color = new Color(0.95f, 0.95f, 0.95f, 1f); }

        var msgRt = MakeRect("Message", dialogRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        msgRt.sizeDelta = new Vector2(720f, 160f);
        msgRt.anchoredPosition = new Vector2(0f, -120f);
        var msgLbl = AddTmpLabel(msgRt, "홈으로 돌아가시겠습니까?\n(진행 중인 라운드는 사라집니다)", 48f, TextAlignmentOptions.Center);
        msgLbl.color = Color.white;

        var returnRt = MakeRect("Return", dialogRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
        returnRt.sizeDelta = new Vector2(280f, 100f);
        returnRt.anchoredPosition = new Vector2(-180f, 90f);
        var returnImg = AddImage(returnRt, Color.white);
        var returnSprite = practiceController != null ? practiceController.HomeConfirmReturnSprite : null;
        if (returnSprite != null) { returnImg.sprite = returnSprite; returnImg.preserveAspect = false; }
        else { returnImg.color = new Color(0.85f, 0.25f, 0.25f, 1f); }
        var returnBtn = returnRt.gameObject.AddComponent<Button>();
        returnBtn.targetGraphic = returnImg;
        var returnLbl = AddTmpLabel(returnRt, "돌아가기", 40f, TextAlignmentOptions.Center);
        returnLbl.color = Color.white;
        returnBtn.onClick.AddListener(() => UnityEngine.SceneManagement.SceneManager.LoadScene("PracticeMode"));

        var cancelRt = MakeRect("Cancel", dialogRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
        cancelRt.sizeDelta = new Vector2(280f, 100f);
        cancelRt.anchoredPosition = new Vector2(180f, 90f);
        var cancelImg = AddImage(cancelRt, Color.white);
        var cancelSprite = practiceController != null ? practiceController.HomeConfirmCancelSprite : null;
        if (cancelSprite != null) { cancelImg.sprite = cancelSprite; cancelImg.preserveAspect = false; }
        else { cancelImg.color = new Color(0.6f, 0.6f, 0.6f, 1f); }
        var cancelBtn = cancelRt.gameObject.AddComponent<Button>();
        cancelBtn.targetGraphic = cancelImg;
        var cancelLbl = AddTmpLabel(cancelRt, "취소", 40f, TextAlignmentOptions.Center);
        cancelLbl.color = Color.white;
        cancelBtn.onClick.AddListener(() => { if (draftHomeConfirmPopup != null) draftHomeConfirmPopup.SetActive(false); });

        draftHomeConfirmPopup = overlayRt.gameObject;
        overlayRt.SetAsLastSibling(); // 다른 UI 위에 그려지도록
        draftHomeConfirmPopup.SetActive(false);
    }

    // ── 패 확인 + 5번의 1:1 매치 단계 ─────────────────────────────────────

    // 14턴 드래프트가 끝나면 호출. 중앙 컬럼을 비우고 "패 확인" UI를 그린 뒤 30초 카운트다운.
    private void EnterHandReviewPhase()
    {
        if (UIClickAudio.Instance != null)
        {
            UIClickAudio.Instance.StopDraftBgm();
            UIClickAudio.Instance.PlayMatchPrev();
        }
        // 픽 단계 UI 모두 제거 + 잔여 참조 정리
        if (centerColTransform != null)
        {
            for (int i = centerColTransform.childCount - 1; i >= 0; i--)
            {
                Destroy(centerColTransform.GetChild(i).gameObject);
            }
        }
        turnLabel = null;
        confirmPickButton = null;
        cardButtons.Clear();
        selectedCardIndex = -1;
        if (turnTimerCoroutine != null) { StopCoroutine(turnTimerCoroutine); turnTimerCoroutine = null; }
        // 길게 누름 중이던 카드가 위 destroy로 사라지면 PointerUp이 안 와서 오버레이가 남을 수 있음 → 강제 정리
        HideRelationshipChart();
        HideDraftInfoPopup();

        // 매치 상태 초기화
        playerUsed = new bool[PicksPerSide];
        aiUsed = new bool[PicksPerSide];
        playerMatchHistory.Clear();
        aiMatchHistory.Clear();
        currentMatchIndex = 0;
        selectedMatchSlotIndex = -1;

        BuildHandReviewCenter();
        if (matchTimerCoroutine != null) StopCoroutine(matchTimerCoroutine);
        matchTimerCoroutine = StartCoroutine(HandReviewTimerRoutine());
    }

    // 패 확인 단계 중앙: 타이틀 + 30초 타이머 + 안내문
    private void BuildHandReviewCenter()
    {
        if (centerColTransform == null) return;

        var titleRect = MakeRect("HandReviewTitle", centerColTransform, new Vector2(0f, 0.86f), new Vector2(1f, 0.98f));
        matchTitleLabel = AddTmpLabel(titleRect, "패 확인 시간", 38f, TextAlignmentOptions.Center);
        matchTitleLabel.color = Color.black;

        var timerRect = MakeRect("HandReviewTimer", centerColTransform, new Vector2(0f, 0.74f), new Vector2(1f, 0.84f));
        matchTimerLabel = AddTmpLabel(timerRect, $"남은 시간 : {Mathf.CeilToInt(HandReviewTimeLimit)}초", 26f, TextAlignmentOptions.Center);
        matchTimerLabel.color = Color.white;

        var infoRect = MakeRect("HandReviewInfo", centerColTransform, new Vector2(0.05f, 0.30f), new Vector2(0.95f, 0.70f));
        var infoLbl = AddTmpLabel(infoRect,
            $"라운드 {SeriesState.CurrentRound}/{SeriesState.TotalRounds}   시리즈 스코어 {SeriesState.PlayerScore} - {SeriesState.AiScore}\n\n30초 후 시합이 시작됩니다.\n좌/우의 내 패와 상대 패를 확인하세요.",
            24f, TextAlignmentOptions.Center);
        infoLbl.color = Color.white;
    }

    private IEnumerator HandReviewTimerRoutine()
    {
        float remaining = HandReviewTimeLimit;
        while (remaining > 0f)
        {
            if (matchTimerLabel != null)
            {
                int secs = Mathf.CeilToInt(Mathf.Max(0f, remaining));
                matchTimerLabel.text = $"남은 시간 : {secs}초";
            }
            yield return null;
            remaining -= Time.deltaTime;
        }
        if (matchTimerLabel != null) matchTimerLabel.text = "남은 시간 : 0초";
        if (UIClickAudio.Instance != null) UIClickAudio.Instance.StopMatchPrev();
        ShowMatchStartPopup();
    }

    // "시합을 시작합니다" 팝업 — 확인 버튼 없이 3초 후 자동으로 첫 매치 진입.
    private const float MatchStartAutoCloseSeconds = 3f;

    private void ShowMatchStartPopup()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        // 풀스크린 컨테이너 — 딤/박스/문구 없이 투명하게 두고, 자동 닫힘 동안 입력만 가로막는다.
        matchStartPopup = new GameObject("MatchStartPopup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        matchStartPopup.transform.SetParent(canvas.transform, false);
        var overlayRt = (RectTransform)matchStartPopup.transform;
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;
        matchStartPopup.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // 투명 (보이는 배경 없음, 클릭만 차단)
        matchStartPopup.transform.SetAsLastSibling();

        // 팝업 박스/텍스트 없이 Match_Start_Popup 이미지만 중앙에 표시.
        var imageGo = new GameObject("MatchStartImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageGo.transform.SetParent(matchStartPopup.transform, false);
        var imgRt = (RectTransform)imageGo.transform;
        imgRt.anchorMin = new Vector2(0.5f, 0.5f);
        imgRt.anchorMax = new Vector2(0.5f, 0.5f);
        imgRt.pivot = new Vector2(0.5f, 0.5f);
        imgRt.sizeDelta = new Vector2(1000f, 513f); // Match_Start_Popup.png 비율(약 1.95:1) 유지
        imgRt.anchoredPosition = Vector2.zero;
        var splashImg = imageGo.GetComponent<Image>();
        var splashSprite = practiceController != null ? practiceController.MatchStartPopupSprite : null;
        if (splashSprite != null)
        {
            splashImg.sprite = splashSprite;
            splashImg.preserveAspect = true;
            splashImg.color = Color.white;
        }
        else
        {
            // 스프라이트 미지정 시에만 기존 텍스트 폴백
            splashImg.color = new Color(0.12f, 0.12f, 0.12f, 0.97f);
            var title = AddTmpLabel(imgRt, "시합을 시작합니다", 40f, TextAlignmentOptions.Center);
            title.color = Color.white;
        }

        StartCoroutine(AutoCloseMatchStartPopupRoutine());
    }

    private IEnumerator AutoCloseMatchStartPopupRoutine()
    {
        yield return new WaitForSeconds(MatchStartAutoCloseSeconds);
        OnMatchStartConfirmed();
    }

    private void OnMatchStartConfirmed()
    {
        if (matchStartPopup != null) { Destroy(matchStartPopup); matchStartPopup = null; }
        BeginMatch(0);
    }

    // matchIndex번째 1:1 매치를 시작 — 중앙 UI를 카드 선택 + 픽 버튼으로 다시 빌드하고 20초 타이머.
    private void BeginMatch(int matchIndex)
    {
        currentMatchIndex = matchIndex;
        selectedMatchSlotIndex = -1;
        if (UIClickAudio.Instance != null) UIClickAudio.Instance.PlayMatchBgm();

        if (centerColTransform != null)
        {
            for (int i = centerColTransform.childCount - 1; i >= 0; i--)
            {
                Destroy(centerColTransform.GetChild(i).gameObject);
            }
        }
        matchCardButtons.Clear();
        matchCardImages.Clear();
        matchPickButton = null;
        matchTimerLabel = null;
        matchTitleLabel = null;

        BuildMatchCenter();
        if (matchTimerCoroutine != null) StopCoroutine(matchTimerCoroutine);
        matchTimerCoroutine = StartCoroutine(MatchPickTimerRoutine());
    }

    // 매치 단계 중앙 UI: 매치 번호/타이머/안내 + 내 7장 표시(사용된 카드는 어두운 오버레이+체크) + 픽 버튼
    private void BuildMatchCenter()
    {
        if (centerColTransform == null) return;

        var titleRect = MakeRect("MatchTitle", centerColTransform, new Vector2(0f, 0.92f), new Vector2(1f, 1f));
        matchTitleLabel = AddTmpLabel(titleRect, $"매치 {currentMatchIndex + 1} / {TotalMatches}", 36f, TextAlignmentOptions.Center);
        matchTitleLabel.color = Color.black;

        var timerRect = MakeRect("MatchTimer", centerColTransform, new Vector2(0f, 0.84f), new Vector2(1f, 0.92f));
        matchTimerLabel = AddTmpLabel(timerRect, $"남은 시간 : {Mathf.CeilToInt(MatchPickTimeLimit)}초", 24f, TextAlignmentOptions.Center);
        matchTimerLabel.color = Color.white;

        var infoRect = MakeRect("MatchInfo", centerColTransform, new Vector2(0f, 0.76f), new Vector2(1f, 0.84f));
        var infoLbl = AddTmpLabel(infoRect,
            $"라운드 {SeriesState.CurrentRound}/{SeriesState.TotalRounds}   시리즈 스코어 {SeriesState.PlayerScore} - {SeriesState.AiScore}\n낼 카드 한 장을 고른 뒤 픽 버튼을 누르세요.",
            18f, TextAlignmentOptions.Center);
        infoLbl.color = Color.white;

        // 카드 영역 (내 패 7장을 3+4 피라미드로)
        var cardArea = MakeRect("Cards", centerColTransform, new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.78f));
        int[] rowCounts = { 3, 4 };
        int picked = 0;
        int totalRows = rowCounts.Length;
        for (int r = 0; r < totalRows; r++)
        {
            int count = rowCounts[r];
            float rowH = 1f / totalRows;
            float rowTop = 1f - r * rowH;
            float rowBot = rowTop - rowH;
            for (int c = 0; c < count; c++)
            {
                int slotIdx = picked;
                float colW = 1f / count;
                float xMin = c * colW + colW * 0.18f;
                float xMax = (c + 1) * colW - colW * 0.18f;
                var cardRect = MakeRect(
                    "Card_" + slotIdx,
                    cardArea,
                    new Vector2(xMin, rowBot + rowH * 0.12f),
                    new Vector2(xMax, rowTop - rowH * 0.12f));
                var img = AddImage(cardRect, CardDefaultColor);
                var element = slotIdx < playerPickHistory.Count ? playerPickHistory[slotIdx] : default;
                // 매치 단계 카드도 동일한 카드 스프라이트로 표시 (텍스트 라벨 없음)
                img.sprite = GetCardSprite(element);
                img.preserveAspect = true;

                var btn = cardRect.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;
                int captured = slotIdx;
                var capturedElement = element;
                // 드래프트와 동일하게: 짧게 클릭=선택 / 1초 길게 누름=상성표 표시. interactable 게이팅은 Button을 통해 그대로 작동한다.
                var press = cardRect.gameObject.AddComponent<CardPressHandler>();
                press.longPressDuration = 1f;
                press.onClick = () => OnMatchCardClicked(captured);
                press.onLongPressStart = () => ShowRelationshipChart(capturedElement);
                press.onLongPressEnd = HideRelationshipChart;
                matchCardButtons.Add(btn);
                matchCardImages.Add(img);

                bool used = slotIdx < playerUsed.Length && playerUsed[slotIdx];
                btn.interactable = !used;
                if (used) AddUsedOverlayOnRect(cardRect);

                picked++;
            }
        }

        // 픽 버튼 (드래프트 단계의 "결정" 버튼과 동일 위치). pick_button 이미지만 표시(라벨 없음).
        var pickRect = MakeRect("MatchPickButton", centerColTransform, new Vector2(0.35f, 0.02f), new Vector2(0.65f, 0.10f));
        var pickImg = AddImage(pickRect, Color.white);
        var pickSprite = practiceController != null ? practiceController.PickButtonSprite : null;
        if (pickSprite != null) { pickImg.sprite = pickSprite; pickImg.preserveAspect = true; }
        matchPickButton = pickRect.gameObject.AddComponent<Button>();
        matchPickButton.targetGraphic = pickImg;
        var colors = matchPickButton.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        colors.disabledColor = new Color(0.78f, 0.78f, 0.78f, 0.6f);
        matchPickButton.colors = colors;
        matchPickButton.onClick.AddListener(OnMatchPickClicked);
        matchPickButton.interactable = false;
    }

    private void OnMatchCardClicked(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= playerUsed.Length) return;
        if (playerUsed[slotIndex]) return;

        // 이전 선택 카드 하이라이트 원복
        if (selectedMatchSlotIndex >= 0
            && selectedMatchSlotIndex < matchCardImages.Count
            && !playerUsed[selectedMatchSlotIndex])
        {
            matchCardImages[selectedMatchSlotIndex].color = CardDefaultColor;
        }
        selectedMatchSlotIndex = slotIndex;
        if (slotIndex < matchCardImages.Count)
            matchCardImages[slotIndex].color = CardSelectedColor;
        if (matchPickButton != null) matchPickButton.interactable = true;
    }

    private void OnMatchPickClicked()
    {
        if (selectedMatchSlotIndex < 0) return;
        // 타이머는 멈추지 않는다 — 픽~베팅을 합쳐 20초 안에 끝내야 하므로 베팅 동안에도 계속 흐른다.
        foreach (var b in matchCardButtons) if (b != null) b.interactable = false;
        if (matchPickButton != null) matchPickButton.interactable = false;
        pendingPlayerSlotIdx = selectedMatchSlotIndex;
        ShowBetPopup();
    }

    // 20초 카운트다운 — 픽~베팅 전 과정을 포함한다. 만료 시:
    //   베팅 팝업이 떠 있으면(픽 완료) → 현재 적용된 베팅 값으로 자동 확정
    //   아직 픽 전이면 → 자동 픽(선택 카드 또는 미사용 중 무작위) + 최소 베팅
    private IEnumerator MatchPickTimerRoutine()
    {
        float remaining = MatchPickTimeLimit;
        while (remaining > 0f)
        {
            int secs = Mathf.CeilToInt(Mathf.Max(0f, remaining));
            if (matchTimerLabel != null) matchTimerLabel.text = $"남은 시간 : {secs}초";
            if (betTimerLabel != null) betTimerLabel.text = $"남은 시간 : {secs}초";
            yield return null;
            remaining -= Time.deltaTime;
        }
        if (matchTimerLabel != null) matchTimerLabel.text = "남은 시간 : 0초";
        if (betTimerLabel != null) betTimerLabel.text = "남은 시간 : 0초";

        if (betPopup != null)
        {
            // 베팅 중 시간 초과 → 현재 적용된 베팅 값 그대로 확정
            int bet = currentBetValue;
            Destroy(betPopup); betPopup = null;
            betTimerLabel = null;
            StartMatchResolution(pendingPlayerSlotIdx, bet);
        }
        else
        {
            AutoMatchPick();
        }
    }

    // 시간 초과: 카드는 (선택 있으면 그것, 없으면 미사용 중 무작위), 베팅은 (마지막이면 전액, 아니면 최소 5pt)
    private void AutoMatchPick()
    {
        int idx = selectedMatchSlotIndex;
        if (idx < 0 || idx >= playerUsed.Length || playerUsed[idx])
        {
            var unused = new List<int>();
            for (int i = 0; i < playerUsed.Length; i++) if (!playerUsed[i]) unused.Add(i);
            if (unused.Count == 0) return;
            idx = unused[Random.Range(0, unused.Count)];
        }
        int remainingAfter = TotalMatches - currentMatchIndex - 1;
        int autoBet = (remainingAfter <= 0) ? playerWallet : Mathf.Min(MinBetPerMatch, playerWallet);
        StartMatchResolution(idx, autoBet);
    }

    // 베팅 팝업 표시. 마지막 매치(remainingAfter==0)면 강제 전액 모드(조정 버튼 없음).
    private void ShowBetPopup()
    {
        int remainingAfter = TotalMatches - currentMatchIndex - 1;
        bool forced = remainingAfter == 0;
        if (forced)
        {
            currentBetMin = currentBetMax = currentBetValue = Mathf.Max(0, playerWallet);
        }
        else
        {
            currentBetMin = MinBetPerMatch;
            currentBetMax = Mathf.Max(MinBetPerMatch, playerWallet - remainingAfter * MinBetPerMatch);
            currentBetValue = currentBetMin;
        }

        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        betPopup = new GameObject("BetPopup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        betPopup.transform.SetParent(canvas.transform, false);
        var overlayRt = (RectTransform)betPopup.transform;
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;
        betPopup.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);
        betPopup.transform.SetAsLastSibling();

        var box = new GameObject("Box", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        box.transform.SetParent(betPopup.transform, false);
        var boxRt = (RectTransform)box.transform;
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.pivot = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(760f, 520f);
        boxRt.anchoredPosition = Vector2.zero;
        var boxImg = box.GetComponent<Image>();
        var battingBg = practiceController != null ? practiceController.BattingBackgroundSprite : null;
        if (battingBg != null)
        {
            boxImg.sprite = battingBg;
            boxImg.color = Color.white;
            boxImg.preserveAspect = true;
        }
        else
        {
            boxImg.color = new Color(0.12f, 0.12f, 0.12f, 0.97f);
        }

        // 타이틀 (inspector: Top=60, Bottom=-60)
        var titleRect = MakeRect("Title", boxRt, new Vector2(0f, 0.80f), new Vector2(1f, 0.95f));
        titleRect.offsetMin = new Vector2(0f, -60f);
        titleRect.offsetMax = new Vector2(0f, -60f);
        var title = AddTmpLabel(titleRect,
            forced ? $"최종 매치 — 남은 포인트 전액 베팅" : $"포인트 베팅 — 매치 {currentMatchIndex + 1}/{TotalMatches}",
            30f, TextAlignmentOptions.Center);
        title.color = new Color(1f, 0.85f, 0.4f, 1f);

        // 남은 시간 — 픽~베팅 공통 20초 타이머. MatchPickTimerRoutine이 매 프레임 갱신한다.
        var betTimerRect = MakeRect("BetTimer", boxRt, new Vector2(0f, 0.93f), new Vector2(1f, 1f));
        betTimerLabel = AddTmpLabel(betTimerRect, "남은 시간 : --초", 22f, TextAlignmentOptions.Center);
        betTimerLabel.color = new Color(1f, 0.6f, 0.6f, 1f);

        // 정보 (inspector: Top=28.4, Bottom=-28.4)
        var infoRect = MakeRect("Info", boxRt, new Vector2(0.05f, 0.58f), new Vector2(0.95f, 0.79f));
        infoRect.offsetMin = new Vector2(0f, -28.4f);
        infoRect.offsetMax = new Vector2(0f, -28.4f);
        string infoText = forced
            ? $"마지막 매치 — 남은 보유 포인트를 자동으로 전액 베팅합니다."
            : $"보유 포인트: {playerWallet}pt\n베팅 범위: {currentBetMin} ~ {currentBetMax}pt\n(남은 매치 {remainingAfter}회 × 최소 {MinBetPerMatch}pt 확보)";
        var infoLbl = AddTmpLabel(infoRect, infoText, 22f, TextAlignmentOptions.Center);
        infoLbl.color = Color.white;

        // 값 표시 (inspector: Left=55.3593, Top=12.59815, Right=57.3463, Bottom=31.38655 / 배경 Image는 끔)
        var valueRect = MakeRect("Value", boxRt, new Vector2(0.22f, 0.32f), new Vector2(0.78f, 0.54f));
        valueRect.offsetMin = new Vector2(55.3593f, 31.38655f);
        valueRect.offsetMax = new Vector2(-57.3463f, -12.59815f);
        AddImage(valueRect, new Color(0.2f, 0.2f, 0.2f, 1f)).enabled = false;
        betValueLabel = AddTmpLabel(valueRect, $"{currentBetValue}pt", 50f, TextAlignmentOptions.Center);
        betValueLabel.color = Color.white;

        // 스프라이트 미리 캐시
        var minusSprite = practiceController != null ? practiceController.Minus5ButtonSprite : null;
        var plusSprite = practiceController != null ? practiceController.Plus5ButtonSprite : null;
        var betConfirmSprite = practiceController != null ? practiceController.BattingSelectButtonSprite : null;

        if (!forced)
        {
            // MinusBtn (inspector: Left=84.1705, Top=12.59815, Right=-63.3031, Bottom=28.85715 / 라벨 없음)
            var minusRect = MakeRect("MinusBtn", boxRt, new Vector2(0.06f, 0.32f), new Vector2(0.20f, 0.54f));
            minusRect.offsetMin = new Vector2(84.1705f, 28.85715f);
            minusRect.offsetMax = new Vector2(63.3031f, -12.59815f);
            var minusImg = AddImage(minusRect, Color.white);
            if (minusSprite != null) { minusImg.sprite = minusSprite; minusImg.preserveAspect = true; }
            betMinusBtn = minusRect.gameObject.AddComponent<Button>();
            betMinusBtn.targetGraphic = minusImg;
            var mc = betMinusBtn.colors;
            mc.disabledColor = new Color(0.78f, 0.78f, 0.78f, 0.6f);
            betMinusBtn.colors = mc;
            betMinusBtn.onClick.AddListener(OnBetMinus);

            // PlusBtn (inspector: Left=-61.7, Top=7.6, Right=89.922, Bottom=24.6952 / 라벨 없음)
            var plusRect = MakeRect("PlusBtn", boxRt, new Vector2(0.80f, 0.32f), new Vector2(0.94f, 0.54f));
            plusRect.offsetMin = new Vector2(-61.7f, 24.6952f);
            plusRect.offsetMax = new Vector2(-89.922f, -7.6f);
            var plusImg = AddImage(plusRect, Color.white);
            if (plusSprite != null) { plusImg.sprite = plusSprite; plusImg.preserveAspect = true; }
            betPlusBtn = plusRect.gameObject.AddComponent<Button>();
            betPlusBtn.targetGraphic = plusImg;
            var pc = betPlusBtn.colors;
            pc.disabledColor = new Color(0.78f, 0.78f, 0.78f, 0.6f);
            betPlusBtn.colors = pc;
            betPlusBtn.onClick.AddListener(OnBetPlus);

            UpdateBetButtonStates();
        }
        else
        {
            betMinusBtn = null;
            betPlusBtn = null;
        }

        // 확정 버튼 (inspector: Left=-3.0388, Top=-72.70477, Right=-1.7516, Bottom=62.19138)
        var confirmRect = MakeRect("ConfirmBtn", boxRt, new Vector2(0.30f, 0.06f), new Vector2(0.70f, 0.26f));
        confirmRect.offsetMin = new Vector2(-3.0388f, 62.19138f);
        confirmRect.offsetMax = new Vector2(1.7516f, 72.70477f);
        var confirmImg = AddImage(confirmRect, Color.white);
        if (betConfirmSprite != null) { confirmImg.sprite = betConfirmSprite; confirmImg.preserveAspect = true; }
        var confirmLbl = AddTmpLabel(confirmRect, "확정", 30f, TextAlignmentOptions.Center);
        confirmLbl.color = Color.black;
        var confirmBtn = confirmRect.gameObject.AddComponent<Button>();
        confirmBtn.targetGraphic = confirmImg;
        confirmBtn.onClick.AddListener(OnBetConfirmed);
    }

    private void OnBetMinus()
    {
        int next = currentBetValue - MinBetPerMatch;
        if (next < currentBetMin) next = currentBetMin;
        currentBetValue = next;
        if (betValueLabel != null) betValueLabel.text = $"{currentBetValue}pt";
        UpdateBetButtonStates();
    }

    private void OnBetPlus()
    {
        int next = currentBetValue + MinBetPerMatch;
        if (next > currentBetMax) next = currentBetMax;
        currentBetValue = next;
        if (betValueLabel != null) betValueLabel.text = $"{currentBetValue}pt";
        UpdateBetButtonStates();
    }

    private void UpdateBetButtonStates()
    {
        if (betMinusBtn != null) betMinusBtn.interactable = currentBetValue > currentBetMin;
        if (betPlusBtn != null) betPlusBtn.interactable = currentBetValue < currentBetMax;
    }

    private void OnBetConfirmed()
    {
        int bet = currentBetValue;
        if (matchTimerCoroutine != null) { StopCoroutine(matchTimerCoroutine); matchTimerCoroutine = null; }
        if (betPopup != null) { Destroy(betPopup); betPopup = null; }
        betTimerLabel = null;
        StartMatchResolution(pendingPlayerSlotIdx, bet);
    }

    // (AI 베팅 결정 DecideAiBet / 매치 카드 선택 ChooseAiMatchPick / EV 계산 ComputeMatchupEdge /
    //  베이지안 상대 모델 UpdateBayesianModel 등은 DraftAI 로 이동)

    // 베팅 확정/타임아웃 직후 호출. AI의 픽/베팅을 결정하고, 듀얼 플립 연출을 거친 뒤 정산을 진행한다.
    // SubmitMatchPick은 연출 종료 후 호출되어 즉시 정산 + 사용 표시 + 결과 팝업까지 한 번에 처리한다.
    private void StartMatchResolution(int playerSlotIdx, int playerBet)
    {
        if (matchTimerCoroutine != null) { StopCoroutine(matchTimerCoroutine); matchTimerCoroutine = null; }
        foreach (var b in matchCardButtons) if (b != null) b.interactable = false;
        if (matchPickButton != null) matchPickButton.interactable = false;

        int aiSlotIdx = ai.ChooseAiMatchPick();
        if (aiSlotIdx < 0) return;
        int aiBet = ai.DecideAiBet(aiSlotIdx);

        var playerElem = playerPickHistory[playerSlotIdx];
        var aiElem = aiPickHistory[aiSlotIdx];

        StartCoroutine(DuelFlipRoutine(playerSlotIdx, playerBet, aiSlotIdx, aiBet, playerElem, aiElem));
    }

    // 베팅 확정 후 연출: 좌측(플레이어 픽창 옆)/우측(상대 픽창 옆)에 카드 뒷면 등장 → 짧은 긴장 → 동시 플립 → 잠시 표시 → 정산.
    private IEnumerator DuelFlipRoutine(
        int playerSlotIdx, int playerBet,
        int aiSlotIdx, int aiBet,
        ElementType playerElem, ElementType aiElem)
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            // 캔버스를 못 찾으면 연출을 건너뛰고 즉시 정산 (시스템적 보호)
            SubmitMatchPick(playerSlotIdx, playerBet, aiSlotIdx, aiBet);
            yield break;
        }

        // 이전 오버레이 잔재 정리 (이론상 없어야 하지만 안전망)
        if (duelFlipOverlay != null) { Destroy(duelFlipOverlay); duelFlipOverlay = null; }

        duelFlipOverlay = new GameObject("DuelFlipOverlay",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        duelFlipOverlay.transform.SetParent(canvas.transform, false);
        var overlayRt = (RectTransform)duelFlipOverlay.transform;
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;
        duelFlipOverlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.68f);
        duelFlipOverlay.transform.SetAsLastSibling();

        // 가운데 단계 안내 라벨 — "베팅 공개" → "카드 공개" 순으로 텍스트가 바뀐다
        var phaseRect = MakeRect("PhaseLabel", overlayRt, new Vector2(0.36f, 0.45f), new Vector2(0.64f, 0.58f));
        var phaseLbl = AddTmpLabel(phaseRect, "베팅 공개", 56f, TextAlignmentOptions.Center);
        phaseLbl.color = new Color(1f, 0.85f, 0.4f, 1f);
        phaseLbl.fontStyle = FontStyles.Bold;

        // 플레이어 카드 — 화면 좌측, 플레이어 픽창(LeftColumn=0~0.2) 바로 옆
        var playerCardRefs = BuildDuelCard(
            "PlayerDuelCard", overlayRt,
            new Vector2(0.22f, 0.28f), new Vector2(0.42f, 0.78f),
            playerElem);

        // 상대 카드 — 화면 우측, AI 픽창(RightColumn=0.8~1.0) 바로 옆
        var aiCardRefs = BuildDuelCard(
            "AiDuelCard", overlayRt,
            new Vector2(0.58f, 0.28f), new Vector2(0.78f, 0.78f),
            aiElem);

        // 누구 카드인지 표시 (상단)
        var pLblRect = MakeRect("PlayerLabel", overlayRt, new Vector2(0.22f, 0.80f), new Vector2(0.42f, 0.86f));
        var pLbl = AddTmpLabel(pLblRect, "나", 28f, TextAlignmentOptions.Center);
        pLbl.color = Color.white;
        var aLblRect = MakeRect("AiLabel", overlayRt, new Vector2(0.58f, 0.80f), new Vector2(0.78f, 0.86f));
        var aLbl = AddTmpLabel(aLblRect, "상대", 28f, TextAlignmentOptions.Center);
        aLbl.color = Color.white;

        // 베팅 금액 라벨 (각 카드 하단) — 초기엔 "00pt"로 가려놓고 잠시 뒤 실제 액수로 바뀜
        var pBetRect = MakeRect("PlayerBet", overlayRt, new Vector2(0.22f, 0.19f), new Vector2(0.42f, 0.26f));
        var pBetLbl = AddTmpLabel(pBetRect, "베팅 00pt", 34f, TextAlignmentOptions.Center);
        pBetLbl.color = new Color(1f, 0.85f, 0.4f, 1f);
        pBetLbl.fontStyle = FontStyles.Bold;
        var aBetRect = MakeRect("AiBet", overlayRt, new Vector2(0.58f, 0.19f), new Vector2(0.78f, 0.26f));
        var aBetLbl = AddTmpLabel(aBetRect, "베팅 00pt", 34f, TextAlignmentOptions.Center);
        aBetLbl.color = new Color(1f, 0.85f, 0.4f, 1f);
        aBetLbl.fontStyle = FontStyles.Bold;

        // 1단계: 가려진 "00pt" 상태로 잠깐 대기 (긴장감)
        yield return new WaitForSeconds(0.5f);

        // 2단계: 실제 베팅 금액 공개 — 카드는 뒷면 그대로, 베팅액 숫자만 바뀜
        if (pBetLbl != null) pBetLbl.text = $"베팅 {playerBet}pt";
        if (aBetLbl != null) aBetLbl.text = $"베팅 {aiBet}pt";
        yield return new WaitForSeconds(1.3f);

        // 3단계: 카드 공개로 전환
        if (phaseLbl != null) phaseLbl.text = "카드 공개";
        yield return new WaitForSeconds(0.4f);

        // 동시 플립
        yield return StartCoroutine(FlipBothCardsRoutine(playerCardRefs, aiCardRefs, 0.35f));

        // 4단계: 공개 직후 짧은 홀드 — 양쪽 속성을 인지할 시간
        yield return new WaitForSeconds(0.6f);

        // 5단계: 공격 모션 — 승패면 한쪽이 돌진, 무승부면 양쪽이 가운데서 부딪히는 연출.
        var attackOutcome = TypeChart.GetOutcome(playerElem, aiElem);
        if (attackOutcome == MatchOutcome.Win)
        {
            yield return StartCoroutine(AttackAnimationRoutine(playerCardRefs, aiCardRefs, attackerIsOnLeft: true));
        }
        else if (attackOutcome == MatchOutcome.Lose)
        {
            yield return StartCoroutine(AttackAnimationRoutine(aiCardRefs, playerCardRefs, attackerIsOnLeft: false));
        }
        else // Tie
        {
            yield return StartCoroutine(TieClashAnimationRoutine(playerCardRefs, aiCardRefs));
        }

        // 마무리 홀드 — 결과 팝업 직전 잠시 카드 상태 보기
        yield return new WaitForSeconds(0.5f);

        if (duelFlipOverlay != null) { Destroy(duelFlipOverlay); duelFlipOverlay = null; }
        SubmitMatchPick(playerSlotIdx, playerBet, aiSlotIdx, aiBet);
    }

    // 공격 모션: 공격자가 살짝 뒤로 윈드업 → 상대 쪽으로 돌진 → 상대 카드 흔들림 + 붉은 틴트 + 임팩트 플래시 → 공격자 복귀.
    // attackerIsOnLeft: 공격자가 화면 좌측(플레이어 자리)이면 true → 오른쪽으로 돌진. 우측 공격자면 false → 왼쪽으로 돌진.
    private IEnumerator AttackAnimationRoutine(DuelCardRefs attacker, DuelCardRefs defender, bool attackerIsOnLeft)
    {
        if (attacker.rt == null || defender.rt == null) yield break;

        Vector2 attackerHome = attacker.rt.anchoredPosition;
        Vector2 defenderHome = defender.rt.anchoredPosition;
        float dir = attackerIsOnLeft ? 1f : -1f;

        // 1) 윈드업: 반대 방향으로 살짝 (긴장감)
        const float windupDist = 30f;
        const float windupTime = 0.12f;
        float t = 0f;
        while (t < windupTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / windupTime);
            float a = Mathf.Lerp(0f, -windupDist * dir, k);
            attacker.rt.anchoredPosition = attackerHome + new Vector2(a, 0f);
            yield return null;
        }

        // 2) 돌진: 상대 쪽으로 큰 이동 (가속감을 위해 ease-in)
        const float lungeDist = 220f;
        const float lungeTime = 0.18f;
        t = 0f;
        while (t < lungeTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / lungeTime);
            float ease = k * k; // ease-in
            float a = Mathf.Lerp(-windupDist * dir, lungeDist * dir, ease);
            attacker.rt.anchoredPosition = attackerHome + new Vector2(a, 0f);
            yield return null;
        }
        Vector2 lungeEnd = attackerHome + new Vector2(lungeDist * dir, 0f);
        attacker.rt.anchoredPosition = lungeEnd;
        if (UIClickAudio.Instance != null) UIClickAudio.Instance.PlayCardAttack();

        // 3) 충돌 연출: 방어자 흔들기 + 붉은 틴트 + 임팩트 플래시 (병행 진행)
        var defenderImg = defender.backGo != null ? defender.backGo.GetComponent<Image>() : null;
        Color defenderOriginalColor = defenderImg != null ? defenderImg.color : Color.white;

        GameObject flash = new GameObject("ImpactFlash",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        flash.transform.SetParent(defender.rt, false);
        var fRt = (RectTransform)flash.transform;
        fRt.anchorMin = Vector2.zero;
        fRt.anchorMax = Vector2.one;
        fRt.offsetMin = Vector2.zero;
        fRt.offsetMax = Vector2.zero;
        var fImg = flash.GetComponent<Image>();
        // 진 카드를 때릴 때 임팩트 이미지: card_crack 스프라이트가 있으면 사용, 없으면 기존 흰색 플래시.
        // card_crack은 페이드아웃하지 않고 카드(오버레이)가 사라질 때까지 진 카드 위에 유지한다.
        var crackSprite = practiceController != null ? practiceController.CardCrackSprite : null;
        bool useCrack = crackSprite != null;
        if (useCrack)
        {
            fImg.sprite = crackSprite;
            fImg.preserveAspect = true;
            fImg.color = Color.white;
        }
        else
        {
            fImg.color = new Color(1f, 1f, 1f, 0.85f);
        }
        fImg.raycastTarget = false;
        flash.transform.SetAsLastSibling();

        const float impactTime = 0.4f;
        const float shakeAmplitude = 14f;
        const float flashFadeTime = 0.18f;
        t = 0f;
        while (t < impactTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / impactTime);
            // 방어자 좌우 흔들기 — 감쇠 사인파
            float shake = Mathf.Sin(t * 60f) * shakeAmplitude * (1f - k);
            defender.rt.anchoredPosition = defenderHome + new Vector2(shake, 0f);
            // 붉은 틴트 → 원래 색으로 페이드
            if (defenderImg != null)
            {
                defenderImg.color = Color.Lerp(new Color(1f, 0.3f, 0.3f, 1f), defenderOriginalColor, k);
            }
            // 임팩트 플래시 페이드아웃 — 흰색 플래시 폴백일 때만. card_crack은 그대로 유지.
            if (fImg != null && !useCrack)
            {
                float fk = Mathf.Clamp01(t / flashFadeTime);
                fImg.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.85f, 0f, fk));
            }
            yield return null;
        }
        defender.rt.anchoredPosition = defenderHome;
        if (defenderImg != null) defenderImg.color = defenderOriginalColor;
        // card_crack은 카드(오버레이)가 사라질 때 함께 정리되므로 여기서 제거하지 않는다.
        if (flash != null && !useCrack) Destroy(flash);

        // 4) 공격자 복귀
        const float returnTime = 0.2f;
        t = 0f;
        while (t < returnTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / returnTime);
            attacker.rt.anchoredPosition = Vector2.Lerp(lungeEnd, attackerHome, k);
            yield return null;
        }
        attacker.rt.anchoredPosition = attackerHome;
    }

    // 무승부 충돌 모션: 양쪽이 동시에 윈드업 → 가운데로 동시 돌진 → 충돌 시 "띵!" 정지 + 플래시 → 반동으로 튕기며 복귀.
    // 좌측 카드(left)는 우측으로, 우측 카드(right)는 좌측으로 이동. 둘 다 손상 효과(붉은 틴트)는 없음.
    private IEnumerator TieClashAnimationRoutine(DuelCardRefs left, DuelCardRefs right)
    {
        if (left.rt == null || right.rt == null) yield break;

        Vector2 leftHome = left.rt.anchoredPosition;
        Vector2 rightHome = right.rt.anchoredPosition;

        // 1) 윈드업: 좌우로 동시에 살짝 뒤로
        const float windupDist = 30f;
        const float windupTime = 0.12f;
        float t = 0f;
        while (t < windupTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / windupTime);
            float a = Mathf.Lerp(0f, windupDist, k);
            left.rt.anchoredPosition = leftHome + new Vector2(-a, 0f);
            right.rt.anchoredPosition = rightHome + new Vector2(a, 0f);
            yield return null;
        }

        // 2) 동시 돌진: 가운데로 ease-in (양쪽이 정확히 마주보며 가속)
        const float lungeDist = 180f;
        const float lungeTime = 0.18f;
        t = 0f;
        while (t < lungeTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / lungeTime);
            float ease = k * k;
            float a = Mathf.Lerp(-windupDist, lungeDist, ease);
            left.rt.anchoredPosition = leftHome + new Vector2(a, 0f);
            right.rt.anchoredPosition = rightHome + new Vector2(-a, 0f);
            yield return null;
        }
        Vector2 leftMeet = leftHome + new Vector2(lungeDist, 0f);
        Vector2 rightMeet = rightHome + new Vector2(-lungeDist, 0f);
        left.rt.anchoredPosition = leftMeet;
        right.rt.anchoredPosition = rightMeet;
        if (UIClickAudio.Instance != null) UIClickAudio.Instance.PlayCardTie();

        // 3) 충돌 정지("띵!") — 가운데에 임팩트 플래시 + 짧은 멈춤
        GameObject flash = null;
        if (duelFlipOverlay != null)
        {
            flash = new GameObject("TieImpactFlash",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            flash.transform.SetParent(duelFlipOverlay.transform, false);
            var fRt = (RectTransform)flash.transform;
            var fImg = flash.GetComponent<Image>();
            // 무승부 충돌 이미지: draw_clash 스프라이트가 있으면 그것을 가운데에 표시, 없으면 기존 흰색 플래시.
            var drawSprite = practiceController != null ? practiceController.DrawClashSprite : null;
            if (drawSprite != null)
            {
                fRt.anchorMin = new Vector2(0.38f, 0.34f);
                fRt.anchorMax = new Vector2(0.62f, 0.66f);
                fImg.sprite = drawSprite;
                fImg.preserveAspect = true;
                fImg.color = Color.white;
            }
            else
            {
                fRt.anchorMin = new Vector2(0.42f, 0.40f);
                fRt.anchorMax = new Vector2(0.58f, 0.66f);
                fImg.color = new Color(1f, 1f, 1f, 0.9f);
            }
            fRt.offsetMin = Vector2.zero;
            fRt.offsetMax = Vector2.zero;
            fImg.raycastTarget = false;
            flash.transform.SetAsLastSibling();
        }

        // 충돌 순간 짧은 정지감
        yield return new WaitForSeconds(0.08f);

        // 4) 반동: 양쪽 카드가 동시에 뒤로 튕기며 흔들림, 점차 홈으로 복귀
        const float reboundTime = 0.45f;
        const float reboundPeak = 70f; // 반동 최대 거리(meet 위치 기준 추가로 뒤로)
        const float shakeAmp = 8f;
        t = 0f;
        while (t < reboundTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / reboundTime);
            // 부드러운 반동 곡선: 처음엔 더 뒤로 튕긴 뒤(0→peak) 점차 0으로 수렴
            float bounce = Mathf.Sin(k * Mathf.PI) * reboundPeak * (1f - k * 0.5f);
            // meet에서 홈까지 보간 + 반동 추가
            float leftX = Mathf.Lerp(lungeDist, 0f, k) - bounce;
            float rightX = -(Mathf.Lerp(lungeDist, 0f, k) - bounce);
            // 미세 흔들기 (감쇠 사인파)
            float shake = Mathf.Sin(t * 60f) * shakeAmp * (1f - k);
            left.rt.anchoredPosition = leftHome + new Vector2(leftX, shake);
            right.rt.anchoredPosition = rightHome + new Vector2(rightX, -shake);
            // 플래시 페이드아웃 (앞 0.18s)
            if (flash != null)
            {
                var img = flash.GetComponent<Image>();
                float fk = Mathf.Clamp01(t / 0.18f);
                img.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.9f, 0f, fk));
            }
            yield return null;
        }
        left.rt.anchoredPosition = leftHome;
        right.rt.anchoredPosition = rightHome;
        if (flash != null) Destroy(flash);
    }

    // 듀얼 카드 한 장 빌드: 앞면=Card_Back, 뒷면=속성 카드(초기 비활성). 플립 중간에 스왑된다.
    private DuelCardRefs BuildDuelCard(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, ElementType element)
    {
        var cardRt = MakeRect(name, parent, anchorMin, anchorMax);

        // 앞면 (Card_Back)
        var frontRt = MakeRect("Front", cardRt, Vector2.zero, Vector2.one);
        var frontImg = frontRt.gameObject.AddComponent<Image>();
        var backSprite = practiceController != null ? practiceController.GetCardBackSprite() : null;
        if (backSprite != null) frontImg.sprite = backSprite;
        else frontImg.color = new Color(0.18f, 0.18f, 0.22f, 1f); // 폴백: 짙은 색
        frontImg.preserveAspect = true;
        frontImg.raycastTarget = false;

        // 뒷면 (속성 카드) — 초기 비활성
        var backRt = MakeRect("Back", cardRt, Vector2.zero, Vector2.one);
        var backImg = backRt.gameObject.AddComponent<Image>();
        backImg.sprite = GetCardSprite(element);
        backImg.preserveAspect = true;
        backImg.raycastTarget = false;
        backRt.gameObject.SetActive(false);

        return new DuelCardRefs { rt = cardRt, frontGo = frontRt.gameObject, backGo = backRt.gameObject };
    }

    private struct DuelCardRefs
    {
        public RectTransform rt;
        public GameObject frontGo;
        public GameObject backGo;
    }

    // 두 카드를 동시에 가로 압축 → 스왑 → 펼침. CardFlip.cs의 단일 카드 플립을 두 장 동시 진행하도록 재구성.
    private IEnumerator FlipBothCardsRoutine(DuelCardRefs a, DuelCardRefs b, float duration)
    {
        if (UIClickAudio.Instance != null) UIClickAudio.Instance.PlayCardFlip();
        float half = duration * 0.5f;
        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(1f, 0f, t / half);
            if (a.rt != null) a.rt.localScale = new Vector3(s, 1f, 1f);
            if (b.rt != null) b.rt.localScale = new Vector3(s, 1f, 1f);
            yield return null;
        }
        if (a.rt != null) a.rt.localScale = new Vector3(0f, 1f, 1f);
        if (b.rt != null) b.rt.localScale = new Vector3(0f, 1f, 1f);

        // 가로 폭 0인 순간 앞/뒷면 스왑
        if (a.frontGo != null) a.frontGo.SetActive(false);
        if (a.backGo != null) a.backGo.SetActive(true);
        if (b.frontGo != null) b.frontGo.SetActive(false);
        if (b.backGo != null) b.backGo.SetActive(true);

        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(0f, 1f, t / half);
            if (a.rt != null) a.rt.localScale = new Vector3(s, 1f, 1f);
            if (b.rt != null) b.rt.localScale = new Vector3(s, 1f, 1f);
            yield return null;
        }
        if (a.rt != null) a.rt.localScale = Vector3.one;
        if (b.rt != null) b.rt.localScale = Vector3.one;
    }

    // 양쪽 픽/베팅 동시 확정 — 베팅 정산 + 사용 표시 + 결과 팝업.
    // 베팅 정산 규칙 (보유 포인트는 베팅 시 빠지면 그대로 유지, 회수는 수익으로 표시):
    //   - 베팅 시 wallet -= bet
    //   - 승리 시 earnings += bet*2  (수익에 베팅의 두 배 적립)
    //   - 패배 시 변화 없음 (잠긴 베팅 영구 손실)
    //   - 무승부 시 earnings += bet  (수익에 베팅액만큼 적립 — 보유에서 빠진 만큼 수익으로 돌아옴)
    private void SubmitMatchPick(int playerSlotIdx, int playerBet, int aiSlotIdx, int aiBet)
    {
        playerUsed[playerSlotIdx] = true;
        aiUsed[aiSlotIdx] = true;

        var playerElem = playerPickHistory[playerSlotIdx];
        var aiElem = aiPickHistory[aiSlotIdx];
        playerMatchHistory.Add(playerElem);
        aiMatchHistory.Add(aiElem);
        playerBetHistory.Add(playerBet);
        aiBetHistory.Add(aiBet);

        // Phase 4: 방금 끝난 매치 데이터로 베이지안 모델 갱신.
        // currentMatchIndex 는 이 시점에 "방금 끝난 매치"의 인덱스 (BeginMatch 에서 세팅된 값, 다음 BeginMatch 전까지 증가 안 됨).
        ai.UpdateBayesianModel(currentMatchIndex);

        // 베팅 잠금
        playerWallet -= playerBet;
        aiWallet -= aiBet;

        var outcome = TypeChart.GetOutcome(playerElem, aiElem);
        switch (outcome)
        {
            case MatchOutcome.Win:
                playerEarnings += playerBet * 2;  // 보유는 그대로, 수익에 2배 적립
                // AI는 잠긴 베팅 영구 손실
                break;
            case MatchOutcome.Lose:
                aiEarnings += aiBet * 2;
                // 내 잠긴 베팅 영구 손실
                break;
            case MatchOutcome.Tie:
                playerEarnings += playerBet;
                aiEarnings += aiBet;
                break;
        }

        AddUsedOverlayOnSlot(playerSide: true, playerSlotIdx);
        AddUsedOverlayOnSlot(playerSide: false, aiSlotIdx);
        UpdateSidePointsDisplay();

        ShowMatchResultPopup(playerElem, aiElem, outcome, playerBet, aiBet);
    }

    private void ShowMatchResultPopup(ElementType playerElem, ElementType aiElem, MatchOutcome outcome, int playerBet, int aiBet)
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        if (UIClickAudio.Instance != null)
        {
            if (outcome == MatchOutcome.Win) UIClickAudio.Instance.PlayMatchVictory();
            else if (outcome == MatchOutcome.Lose) UIClickAudio.Instance.PlayMatchDefeat();
            else UIClickAudio.Instance.PlayMatchDraw();
        }

        matchResultPopup = new GameObject("MatchResultPopup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        matchResultPopup.transform.SetParent(canvas.transform, false);
        var overlayRt = (RectTransform)matchResultPopup.transform;
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;
        matchResultPopup.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);
        matchResultPopup.transform.SetAsLastSibling();

        // 매치 결과(승/패/무)를 텍스트/확인 버튼 없이 이미지로만 표시. 확인 버튼 대신 5초 후 자동으로 다음 매치.
        if (practiceController == null)
            practiceController = FindObjectOfType<PracticeCardController>(true);
        Sprite resultSprite = null;
        if (practiceController != null)
        {
            if (outcome == MatchOutcome.Win) resultSprite = practiceController.PickVictorySprite;
            else if (outcome == MatchOutcome.Lose) resultSprite = practiceController.PickDefeatSprite;
            else resultSprite = practiceController.DrawSprite;
        }

        var imgGo = new GameObject("MatchResultImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imgGo.transform.SetParent(matchResultPopup.transform, false);
        var imgRt = (RectTransform)imgGo.transform;
        imgRt.anchorMin = new Vector2(0.5f, 0.5f);
        imgRt.anchorMax = new Vector2(0.5f, 0.5f);
        imgRt.pivot = new Vector2(0.5f, 0.5f);
        imgRt.sizeDelta = new Vector2(760f, 620f);
        imgRt.anchoredPosition = Vector2.zero;
        var rImg = imgGo.GetComponent<Image>();
        if (resultSprite != null)
        {
            rImg.sprite = resultSprite;
            rImg.preserveAspect = true;
            rImg.color = Color.white;
        }
        else
        {
            // 스프라이트 미할당 시에만 간단한 텍스트 폴백
            rImg.color = new Color(0f, 0f, 0f, 0f);
            var lbl = AddTmpLabel(imgRt, OutcomeKor(outcome), 48f, TextAlignmentOptions.Center);
            lbl.color = Color.white;
        }

        if (matchResultAutoAdvanceCoroutine != null) StopCoroutine(matchResultAutoAdvanceCoroutine);
        matchResultAutoAdvanceCoroutine = StartCoroutine(AutoAdvanceMatchResultRoutine());
    }

    // 매치 결과 이미지를 5초간 보여준 뒤 자동으로 다음 매치로 진행 (확인 버튼 대체).
    private const float MatchResultAutoAdvanceSeconds = 3f;
    private Coroutine matchResultAutoAdvanceCoroutine;
    private IEnumerator AutoAdvanceMatchResultRoutine()
    {
        yield return new WaitForSeconds(MatchResultAutoAdvanceSeconds);
        matchResultAutoAdvanceCoroutine = null;
        OnMatchResultConfirmed();
    }

    private void OnMatchResultConfirmed()
    {
        if (matchResultAutoAdvanceCoroutine != null) { StopCoroutine(matchResultAutoAdvanceCoroutine); matchResultAutoAdvanceCoroutine = null; }
        if (matchResultPopup != null) { Destroy(matchResultPopup); matchResultPopup = null; }
        int next = currentMatchIndex + 1;
        if (next >= TotalMatches) FinalizeMatches();
        else BeginMatch(next);
    }

    // 5번 매치 종료: 시리즈 점수 반영 + 최종 결과 팝업
    private void FinalizeMatches()
    {
        if (UIClickAudio.Instance != null) UIClickAudio.Instance.StopMatchBgm();
        UpdateSeriesScoreFromMatches();
        ShowFinalMatchPopup();
    }

    // 5번 매치 종료 시점의 최종 포인트(wallet+earnings)로 라운드 우열 결정 → 시리즈 점수 반영.
    // 동점이면 SeriesState.LastRoundTied=true 로 두고, 다음 라운드는 카드 뒤집기 결판전으로 진입.
    private void UpdateSeriesScoreFromMatches()
    {
        int playerFinal = playerWallet + playerEarnings;
        int aiFinal = aiWallet + aiEarnings;
        if (playerFinal > aiFinal)
        {
            SeriesState.LastRoundTied = false;
            SeriesState.LastRoundPlayerWon = true;
            SeriesState.PlayerScore++;
        }
        else if (playerFinal < aiFinal)
        {
            SeriesState.LastRoundTied = false;
            SeriesState.LastRoundPlayerWon = false;
            SeriesState.AiScore++;
        }
        else
        {
            SeriesState.LastRoundTied = true;
            SeriesState.LastRoundPlayerWon = false; // 동점, 결판전이 결정
        }
    }

    private void ShowFinalMatchPopup()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        if (UIClickAudio.Instance != null)
        {
            int pFinal = playerWallet + playerEarnings;
            int aFinal = aiWallet + aiEarnings;
            if (pFinal > aFinal) UIClickAudio.Instance.PlaySeriesVictory();
            else if (pFinal < aFinal) UIClickAudio.Instance.PlaySeriesDefeat();
        }

        finalOrderOverlay = new GameObject("FinalMatchOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        finalOrderOverlay.transform.SetParent(canvas.transform, false);
        var overlayRt = (RectTransform)finalOrderOverlay.transform;
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;
        finalOrderOverlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);
        finalOrderOverlay.transform.SetAsLastSibling();

        var box = new GameObject("FinalMatchBox", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        box.transform.SetParent(finalOrderOverlay.transform, false);
        var boxRt = (RectTransform)box.transform;
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.pivot = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(720f, 806f);
        boxRt.anchoredPosition = Vector2.zero;

        // 기본 화면: 이번 라운드 승/패만 크게 표시 (보유+수익 비교로 결정 — BuildMatchSummaryText와 동일 기준)
        int playerFinal = playerWallet + playerEarnings;
        int aiFinal = aiWallet + aiEarnings;

        // 팝업 배경 프레임: 승리=popup_win, 패배=popup_loss. 무승부는 전용 이미지가 없어 어두운 박스로 폴백.
        var boxImg = box.GetComponent<Image>();
        Sprite frameSprite = null;
        if (playerFinal > aiFinal) frameSprite = LoadRoundResultSprite("popup_win.png");
        else if (playerFinal < aiFinal) frameSprite = LoadRoundResultSprite("popup_loss.png");
        if (frameSprite != null)
        {
            boxImg.sprite = frameSprite;
            boxImg.color = Color.white;
            boxImg.preserveAspect = true;
        }
        else
        {
            boxImg.color = new Color(0.12f, 0.12f, 0.12f, 0.97f);
        }

        string roundResultText;
        Color roundResultColor;
        if (playerFinal > aiFinal) { roundResultText = "라운드 승리!"; roundResultColor = new Color(0.4f, 0.8f, 1f, 1f); }
        else if (playerFinal < aiFinal) { roundResultText = "라운드 패배"; roundResultColor = new Color(1f, 0.45f, 0.45f, 1f); }
        else { roundResultText = "무승부"; roundResultColor = new Color(1f, 0.9f, 0.5f, 1f); }

        // 승/패는 이미지로 표시 (승=Round_Win, 패=Round_Loss). 무승부는 전용 이미지가 없어 텍스트로 폴백.
        if (practiceController == null)
            practiceController = FindObjectOfType<PracticeCardController>(true);
        Sprite roundResultSprite = null;
        if (practiceController != null)
        {
            if (playerFinal > aiFinal) roundResultSprite = practiceController.RoundWinSprite;
            else if (playerFinal < aiFinal) roundResultSprite = practiceController.RoundLossSprite;
        }

        var resultRect = MakeRect("RoundResult", boxRt, new Vector2(0.12f, 0.50f), new Vector2(0.88f, 0.92f));
        if (roundResultSprite != null)
        {
            var resultImg = AddImage(resultRect, Color.white);
            resultImg.sprite = roundResultSprite;
            resultImg.preserveAspect = true;
        }
        else
        {
            var resultLbl = AddTmpLabel(resultRect, roundResultText, 52f, TextAlignmentOptions.Center);
            resultLbl.color = roundResultColor;
            resultLbl.fontStyle = FontStyles.Bold;
        }

        // 세부 결과 버튼 (btn_detail) — 누르면 매치별 상세 + 포인트 요약 팝업을 띄운다
        var detailBtn = AddImageButton(boxRt, "DetailButton", new Vector2(0.19f, 0.34f), new Vector2(0.81f, 0.50f), LoadRoundResultSprite("btn_detail.png"));
        detailBtn.onClick.AddListener(ShowRoundDetailPopup);

        if (SeriesState.IsSeriesOver)
        {
            string winner = SeriesState.PlayerWonSeries ? "Player 승리!" : "Player 패배!";
            var endRect = MakeRect("EndLabel", boxRt, new Vector2(0.08f, 0.26f), new Vector2(0.92f, 0.33f));
            var endLbl = AddTmpLabel(endRect,
                $"시리즈 종료 — {winner}",
                28f, TextAlignmentOptions.Center);
            endLbl.color = new Color(1f, 0.85f, 0.4f, 1f);

            // 설정 이동(btn_settings) / 재시작(btn_restart) 두 버튼 (좌/우)
            var homeBtn = AddImageButton(boxRt, "HomeButton", new Vector2(0.07f, 0.12f), new Vector2(0.49f, 0.23f), LoadRoundResultSprite("btn_settings.png"));
            homeBtn.onClick.AddListener(OnSeriesEndHomeClicked);

            var restartBtn = AddImageButton(boxRt, "RestartButton", new Vector2(0.51f, 0.12f), new Vector2(0.93f, 0.23f), LoadRoundResultSprite("btn_restart.png"));
            restartBtn.onClick.AddListener(OnSeriesEndRestartClicked);
        }
        else
        {
            // 다음 라운드 버튼 (btn_next)
            var nextBtn = AddImageButton(boxRt, "NextRoundButton", new Vector2(0.19f, 0.12f), new Vector2(0.81f, 0.28f), LoadRoundResultSprite("btn_next.png"));
            nextBtn.onClick.AddListener(OnNextRoundClicked);
        }
    }

    // "세부 결과" 버튼 → 매치별 상세 + 포인트 요약을 별도 팝업으로 표시. 닫기 버튼으로 닫는다.
    // finalOrderOverlay 자식으로 올려 라운드 결과 위에 겹쳐 뜨고, 다음 라운드 진입 시 함께 정리된다.
    private void ShowRoundDetailPopup()
    {
        if (finalOrderOverlay == null) return;
        if (roundDetailPopup != null) Destroy(roundDetailPopup);

        var overlay = new GameObject("RoundDetailPopup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlay.transform.SetParent(finalOrderOverlay.transform, false);
        var oRt = (RectTransform)overlay.transform;
        oRt.anchorMin = Vector2.zero;
        oRt.anchorMax = Vector2.one;
        oRt.offsetMin = Vector2.zero;
        oRt.offsetMax = Vector2.zero;
        overlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.88f);
        overlay.transform.SetAsLastSibling();

        var box = new GameObject("DetailBox", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        box.transform.SetParent(overlay.transform, false);
        var boxRt = (RectTransform)box.transform;
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.pivot = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(720f, 806f);
        boxRt.anchoredPosition = Vector2.zero;

        // 세부 결과 팝업도 라운드 결과와 동일한 프레임(승리=popup_win, 패배=popup_loss)을 배경으로 사용. 무승부는 어두운 박스로 폴백.
        int playerFinal = playerWallet + playerEarnings;
        int aiFinal = aiWallet + aiEarnings;
        var boxImg = box.GetComponent<Image>();
        Sprite frameSprite = null;
        if (playerFinal > aiFinal) frameSprite = LoadRoundResultSprite("popup_win.png");
        else if (playerFinal < aiFinal) frameSprite = LoadRoundResultSprite("popup_loss.png");
        if (frameSprite != null)
        {
            boxImg.sprite = frameSprite;
            boxImg.color = Color.white;
            boxImg.preserveAspect = true;
        }
        else
        {
            boxImg.color = new Color(0.12f, 0.12f, 0.12f, 0.98f);
        }

        // 내용은 프레임 안쪽 영역에 배치
        var textRect = MakeRect("Content", boxRt, new Vector2(0.1f, 0.18f), new Vector2(0.9f, 0.92f));
        var content = AddTmpLabel(textRect, BuildMatchSummaryText(), 22f, TextAlignmentOptions.Center);
        content.color = Color.white;

        // 닫기 버튼 (btn_close)
        var closeBtn = AddImageButton(boxRt, "CloseButton", new Vector2(0.30f, 0.06f), new Vector2(0.70f, 0.15f), LoadRoundResultSprite("btn_close.png"));
        closeBtn.onClick.AddListener(() => { if (roundDetailPopup != null) { Destroy(roundDetailPopup); roundDetailPopup = null; } });

        roundDetailPopup = overlay;
    }

    // 시리즈 종료 후 설정(PracticeMode) 씬으로 이동
    private void OnSeriesEndHomeClicked()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("PracticeMode");
    }

    // 시리즈 종료 후 현재 PracticeSettings 그대로 Practice 씬 재로드 (= 같은 설정으로 다시 시작)
    private void OnSeriesEndRestartClicked()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("Practice");
    }

    // "다음 라운드" 버튼 → PracticeCardController가 다음 라운드를 시작하도록 위임
    private void OnNextRoundClicked()
    {
        // 라운드 결과 BGM(승리/패배)을 즉시 정지 — 다음 라운드로 넘어가면 더 이상 필요 없다.
        if (UIClickAudio.Instance != null) UIClickAudio.Instance.StopSeriesResult();
        if (finalOrderOverlay != null) { Destroy(finalOrderOverlay); finalOrderOverlay = null; }
        if (practiceController == null)
            practiceController = FindObjectOfType<PracticeCardController>(true);
        if (practiceController != null) practiceController.BeginNextRound();
    }

    private string BuildMatchSummaryText()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"== 라운드 {SeriesState.CurrentRound} / {SeriesState.TotalRounds} 결과 ==");
        sb.AppendLine();

        int wins = 0, losses = 0, ties = 0;
        for (int i = 0; i < playerMatchHistory.Count; i++)
        {
            var me = playerMatchHistory[i];
            var op = aiMatchHistory[i];
            var outcome = TypeChart.GetOutcome(me, op);
            int pBet = i < playerBetHistory.Count ? playerBetHistory[i] : 0;
            int aBet = i < aiBetHistory.Count ? aiBetHistory[i] : 0;
            switch (outcome)
            {
                case MatchOutcome.Win: wins++; break;
                case MatchOutcome.Lose: losses++; break;
                case MatchOutcome.Tie: ties++; break;
            }
            sb.AppendLine($"  매치 {i + 1}: 나 {me}({pBet}pt) vs 상대 {op}({aBet}pt) → {OutcomeKor(outcome)}");
        }
        sb.AppendLine();
        sb.AppendLine($"  매치 전적: {wins}승 {losses}패 {ties}무");
        sb.AppendLine();

        int playerFinal = playerWallet + playerEarnings;
        int aiFinal = aiWallet + aiEarnings;
        sb.AppendLine($"  최종 포인트: 나 {playerFinal}pt  vs  상대 {aiFinal}pt");
        sb.AppendLine($"   (나: 보유 {playerWallet} / 수익 {FormatSigned(playerEarnings)} | 상대: 보유 {aiWallet} / 수익 {FormatSigned(aiEarnings)})");

        string roundResult;
        if (playerFinal > aiFinal) roundResult = "이번 라운드 → 나의 승!";
        else if (playerFinal < aiFinal) roundResult = "이번 라운드 → 상대 승!";
        else roundResult = "이번 라운드 → 동점 (다음 라운드는 결판전 카드 뒤집기로 시작)";
        sb.AppendLine($"  {roundResult}");
        sb.AppendLine();
        sb.AppendLine($"  시리즈 점수: 나 {SeriesState.PlayerScore} - {SeriesState.AiScore} 상대 (선승 {SeriesState.RoundsToWin}점)");

        // 미사용 카드 (각 측 2장)
        var pUnused = new List<ElementType>();
        var aUnused = new List<ElementType>();
        for (int i = 0; i < PicksPerSide; i++)
        {
            if (i < playerPickHistory.Count && playerUsed != null && i < playerUsed.Length && !playerUsed[i])
                pUnused.Add(playerPickHistory[i]);
            if (i < aiPickHistory.Count && aiUsed != null && i < aiUsed.Length && !aiUsed[i])
                aUnused.Add(aiPickHistory[i]);
        }
        sb.AppendLine();
        sb.AppendLine("[ 미사용 카드 ]");
        sb.AppendLine($"  나:   {string.Join(", ", pUnused)}");
        sb.AppendLine($"  상대: {string.Join(", ", aUnused)}");
        return sb.ToString();
    }

    // 좌/우 슬롯 패널 위에 "사용됨" 오버레이(어두운 박스 + X 표시) 추가. raw 이미지 자리는 텍스트 X로 대체.
    private void AddUsedOverlayOnSlot(bool playerSide, int slotIdx)
    {
        var labels = playerSide ? playerSlotLabels : aiSlotLabels;
        var overlays = playerSide ? playerSlotOverlays : aiSlotOverlays;
        if (slotIdx < 0 || slotIdx >= labels.Count || labels[slotIdx] == null) return;
        var slotTf = labels[slotIdx].transform.parent as RectTransform;
        if (slotTf == null) return;

        var overlayGo = new GameObject("UsedOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlayGo.transform.SetParent(slotTf, false);
        var rt = (RectTransform)overlayGo.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        overlayGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 200f / 255f);
        overlayGo.transform.SetAsLastSibling();

        var checkLbl = AddTmpLabel((RectTransform)overlayGo.transform, "X", 48f, TextAlignmentOptions.Center);
        checkLbl.color = new Color(0.9f, 0.2f, 0.2f, 1f);
        checkLbl.fontStyle = FontStyles.Bold;

        overlays.Add(overlayGo);
    }

    // 매치 단계 중앙 카드 위에 "사용됨" 오버레이(어두운 박스 + X 표시) — 다음 매치 빌드 시 이미 사용된 카드 시각화
    private void AddUsedOverlayOnRect(RectTransform card)
    {
        var overlayGo = new GameObject("UsedOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlayGo.transform.SetParent(card, false);
        var rt = (RectTransform)overlayGo.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        overlayGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 200f / 255f);
        overlayGo.transform.SetAsLastSibling();
        var checkLbl = AddTmpLabel((RectTransform)overlayGo.transform, "X", 38f, TextAlignmentOptions.Center);
        checkLbl.color = new Color(0.9f, 0.2f, 0.2f, 1f);
        checkLbl.fontStyle = FontStyles.Bold;
    }

    private static string OutcomeKor(MatchOutcome o) => o switch
    {
        MatchOutcome.Win => "나의 승",
        MatchOutcome.Lose => "나의 패",
        MatchOutcome.Tie => "무승부",
        _ => "-"
    };

    // ── UI 생성 헬퍼들 ─────────────────────────────────────────────────────

    private static RectTransform MakeRect(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    private static Image AddImage(RectTransform target, Color color)
    {
        var img = target.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    // Resources 경로 → 스프라이트 캐시 (빌드/에디터 공통).
    private static readonly Dictionary<string, Sprite> resourceSpriteCache = new Dictionary<string, Sprite>();

    // Resources 폴더에서 스프라이트를 로드/캐시. 빌드본에서도 동작하도록 AssetDatabase 대신 Resources.Load 사용.
    // PNG가 Multiple(spriteMode=2) 임포트라 우선 LoadAll<Sprite>로 첫 서브 스프라이트를 꺼낸다.
    private static Sprite LoadResourceSprite(string resourcePath)
    {
        if (resourceSpriteCache.TryGetValue(resourcePath, out var cached) && cached != null) return cached;
        var all = Resources.LoadAll<Sprite>(resourcePath);
        Sprite sprite = (all != null && all.Length > 0) ? all[0] : Resources.Load<Sprite>(resourcePath);
        resourceSpriteCache[resourcePath] = sprite;
        return sprite;
    }

    // 파일명(확장자 포함 가능)에서 .png 확장자를 떼어 Resources 경로로 변환.
    private static string StripExtension(string fileName)
        => fileName.EndsWith(".png") ? fileName.Substring(0, fileName.Length - 4) : fileName;

    // 라운드 결과 팝업용 스프라이트 (Assets/Resources/Round_Result/).
    private static Sprite LoadRoundResultSprite(string fileName)
        => LoadResourceSprite($"Round_Result/{StripExtension(fileName)}");

    // 대회/드래프트 안내용 스프라이트 (Assets/Resources/Competition/).
    private static Sprite LoadCompetitionSprite(string fileName)
        => LoadResourceSprite($"Competition/{StripExtension(fileName)}");

    // 텍스트 라벨 없이 스프라이트만 표시하는 이미지 버튼 생성 (preserveAspect로 비율 유지).
    private Button AddImageButton(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Sprite sprite)
    {
        var rect = MakeRect(name, parent, anchorMin, anchorMax);
        var img = AddImage(rect, Color.white);
        img.sprite = sprite;
        img.preserveAspect = true;
        var btn = rect.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var c = btn.colors;
        c.normalColor = Color.white;
        c.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        c.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        btn.colors = c;
        return btn;
    }

    // AI 난이도 영문 표기 (헤더의 "AI (...)" 괄호 안에 사용)
    private static string DifficultyEng(PracticeSetupManager.AIDifficulty d) => d switch
    {
        PracticeSetupManager.AIDifficulty.Easy => "Easy",
        PracticeSetupManager.AIDifficulty.Normal => "Normal",
        PracticeSetupManager.AIDifficulty.Hard => "Hard",
        _ => "-"
    };

    private TextMeshProUGUI AddTmpLabel(RectTransform target, string text, float fontSize, TextAlignmentOptions align)
    {
        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(target, false);
        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        if (font != null) tmp.font = font;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = Color.white;
        var rt = tmp.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(8f, 8f);
        rt.offsetMax = new Vector2(-8f, -8f);
        return tmp;
    }
}
