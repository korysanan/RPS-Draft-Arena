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
    private const int PicksPerSide = 7;
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
    private const int TotalMatches = 5;
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
    // 카드 길게 누름 시 표시하는 상성표 오버레이 (떼면 자동 닫힘)
    private GameObject relationshipOverlay;

    // 베팅(포인트) 시스템
    private const int StartingPoints = 100;
    private const int MinBetPerMatch = 5;
    // wallet: 베팅 가능 풀 (시작 100, 최대 100). 베팅 시 차감, 무승부/승리 시 회수, 패배 시 영구 손실.
    private int playerWallet;
    private int aiWallet;
    // earnings: 베팅 수익 누적. 승리 시 +bet, 패배/무 시 변화 없음. 베팅에는 사용 불가.
    private int playerEarnings;
    private int aiEarnings;
    // 매 매치 베팅 기록 (라운드 요약용)
    private readonly List<int> playerBetHistory = new List<int>();
    private readonly List<int> aiBetHistory = new List<int>();
    // 베팅 팝업 상태/UI
    private GameObject betPopup;
    private TMP_Text betValueLabel;
    private Button betMinusBtn;
    private Button betPlusBtn;
    private int currentBetValue;
    private int currentBetMin;
    private int currentBetMax;
    private int pendingPlayerSlotIdx; // 픽 버튼 직후 보관 (베팅 확정 시 사용)
    // 사이드 패널 헤더 라벨 — 포인트 갱신용
    private TMP_Text playerHeaderLabel;
    private TMP_Text aiHeaderLabel;

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

        // 이전 라운드의 잔여 오버레이/팝업 정리
        if (finalOrderOverlay != null) { Destroy(finalOrderOverlay); finalOrderOverlay = null; }
        if (matchStartPopup != null) { Destroy(matchStartPopup); matchStartPopup = null; }
        if (matchResultPopup != null) { Destroy(matchResultPopup); matchResultPopup = null; }
        if (betPopup != null) { Destroy(betPopup); betPopup = null; }
        if (relationshipOverlay != null) { Destroy(relationshipOverlay); relationshipOverlay = null; }
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
        var pick = ChooseAiPick();
        RecordPick(isPlayer: false, pick);
    }

    // 난이도에 따른 AI 픽 결정:
    //  - Easy   : 완전 무작위
    //  - Normal : 50% 확률로 전략적, 50% 무작위 (절반쯤 실수하는 사람 느낌)
    //  - Hard   : 항상 전략적 — 상대 픽들을 상성표로 평가해 최대 점수의 카드를 픽
    private ElementType ChooseAiPick()
    {
        var difficulty = PracticeSettings.Difficulty;
        switch (difficulty)
        {
            case PracticeSetupManager.AIDifficulty.Hard:
                return ChooseStrategicPick();
            case PracticeSetupManager.AIDifficulty.Normal:
                return Random.value < 0.5f ? ChooseStrategicPick() : ChooseRandomPick();
            case PracticeSetupManager.AIDifficulty.Easy:
            default:
                return ChooseRandomPick();
        }
    }

    private ElementType ChooseRandomPick()
    {
        return cardElements[Random.Range(0, cardElements.Count)];
    }

    // 각 카드에 점수를 매기고 최고 점수 후보 중 하나를 무작위로 픽 (인간 같은 변동성)
    private ElementType ChooseStrategicPick()
    {
        int bestScore = int.MinValue;
        var bestCandidates = new List<ElementType>();
        foreach (var e in cardElements)
        {
            int score = ScoreElement(e);
            if (score > bestScore)
            {
                bestScore = score;
                bestCandidates.Clear();
                bestCandidates.Add(e);
            }
            else if (score == bestScore)
            {
                bestCandidates.Add(e);
            }
        }
        return bestCandidates[Random.Range(0, bestCandidates.Count)];
    }

    // 상성 기반 점수 함수.
    //  +2 : 후보가 상대(플레이어)의 픽 하나를 이김
    //  -2 : 후보가 상대 픽 하나에게 짐
    //   0 : 무승부(같은 속성)
    //  +1 : 다양성 보너스 — AI가 아직 안 뽑은 속성에 가산점 (같은 카드 너무 쌓이지 않게)
    // 결과적으로 상대 픽 분포를 카운터하는 동시에 자기 로스터가 한 가지에 치우치지 않도록 유도.
    private int ScoreElement(ElementType candidate)
    {
        int score = 0;
        foreach (var opp in playerPickHistory)
        {
            if (TypeChart.Beats(candidate, opp)) score += 2;
            else if (TypeChart.Beats(opp, candidate)) score -= 2;
            // 동일 속성(Tie)이면 0
        }
        if (!aiPickHistory.Contains(candidate)) score += 1;
        return score;
    }

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
        // 사이드 패널 슬롯에는 와이드 Pick 이미지를 표시 (없으면 Card 이미지로 폴백)
        var slotSprite = GetPickSprite(element) ?? GetCardSprite(element);
        if (isPlayer)
        {
            if (playerCursor < playerSlotLabels.Count)
            {
                playerSlotLabels[playerCursor].text = string.Empty; // 카드 이미지가 자리하므로 텍스트는 비움
                if (playerCursor < playerSlotCards.Count && playerSlotCards[playerCursor] != null)
                    playerSlotCards[playerCursor].sprite = slotSprite;
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
                    aiSlotCards[aiCursor].sprite = slotSprite;
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

        // 전체 배경 (흰색)
        var bg = MakeRect("Background", rootRt, Vector2.zero, Vector2.one);
        AddImage(bg, Color.white);

        var left = MakeColumn("LeftColumn", 0f, 0.2f);
        var center = MakeColumn("CenterColumn", 0.2f, 0.8f);
        var right = MakeColumn("RightColumn", 0.8f, 1f);
        centerColTransform = center; // 순서변경 단계에서 재사용

        playerHeaderLabel = BuildSidePanel(left, "A (Player)", playerSlotLabels, playerSlotCards);
        aiHeaderLabel = BuildSidePanel(right, "B (Other Player)", aiSlotLabels, aiSlotCards);
        BuildCenter(center, rpsCount);
        UpdateSidePointsDisplay();
    }

    // 좌/우 사이드 패널 헤더에 시리즈 승수 + wallet/earnings를 표시. 매치 진행마다 호출되어 갱신된다.
    private void UpdateSidePointsDisplay()
    {
        if (playerHeaderLabel != null)
            playerHeaderLabel.text = $"A (Player)\n시리즈 {SeriesState.PlayerScore}승  (목표 {SeriesState.RoundsToWin}승)\n보유 {playerWallet}pt  수익 {FormatSigned(playerEarnings)}";
        if (aiHeaderLabel != null)
            aiHeaderLabel.text = $"B (Other Player)\n시리즈 {SeriesState.AiScore}승  (목표 {SeriesState.RoundsToWin}승)\n보유 {aiWallet}pt  수익 {FormatSigned(aiEarnings)}";
    }

    private static string FormatSigned(int v) => v > 0 ? "+" + v : v.ToString();

    private RectTransform MakeColumn(string name, float xMin, float xMax)
    {
        var rt = MakeRect(name, (RectTransform)transform, new Vector2(xMin, 0f), new Vector2(xMax, 1f));
        return rt;
    }

    // 측면 패널: 상단 헤더(이름 + 시리즈 + 포인트) + 아래 7개 슬롯(번갈아 음영). 헤더 라벨은 포인트 갱신용으로 반환.
    // 픽 시점에 슬롯 내부의 카드 이미지(child Image)에 sprite를 채워 넣는다.
    private TMP_Text BuildSidePanel(RectTransform col, string headerText, List<TMP_Text> slotOut, List<Image> slotCardOut)
    {
        const float headerHeightRatio = 0.20f;
        const float slotsTopRatio = 1f - headerHeightRatio;

        // 헤더
        var header = MakeRect("Header", col, new Vector2(0f, slotsTopRatio), new Vector2(1f, 1f));
        AddImage(header, new Color(0.78f, 0.78f, 0.78f, 1f));
        var headerLabel = AddTmpLabel(header, headerText, 18f, TextAlignmentOptions.Center);
        headerLabel.color = Color.black;

        // 슬롯 7개 (헤더 아래 영역을 균등 분할)
        float slotH = slotsTopRatio / PicksPerSide;
        for (int i = 0; i < PicksPerSide; i++)
        {
            float top = slotsTopRatio - i * slotH;
            float bot = top - slotH;
            var slot = MakeRect("Slot" + i, col, new Vector2(0f, bot), new Vector2(1f, top));
            var color = i % 2 == 0
                ? new Color(0.92f, 0.92f, 0.92f, 1f)
                : new Color(0.82f, 0.82f, 0.82f, 1f);
            AddImage(slot, color);

            // 카드 이미지 자리(child) — 픽 전엔 sprite=null이라 자연스럽게 안 보임. preserveAspect로 카드 비율 유지.
            var cardRt = MakeRect("CardImage", slot, Vector2.zero, Vector2.one);
            cardRt.offsetMin = new Vector2(6f, 4f);
            cardRt.offsetMax = new Vector2(-6f, -4f);
            var cardImg = cardRt.gameObject.AddComponent<Image>();
            cardImg.preserveAspect = true;
            cardImg.raycastTarget = false;
            cardImg.color = Color.white;
            cardImg.sprite = null;
            slotCardOut.Add(cardImg);

            // 라벨은 빈 문자열로 유지 (사용됨 오버레이 부착 시 slot RT를 찾는 앵커 역할만 함)
            var lbl = AddTmpLabel(slot, "", 22f, TextAlignmentOptions.Center);
            lbl.color = Color.black;
            slotOut.Add(lbl);
        }
        return headerLabel;
    }

    // 중앙: 타이틀("밴픽 시스템") + 정보 라벨("남은 시간 / 차례") + 가운데 카드 영역
    private void BuildCenter(RectTransform col, int rpsCount)
    {
        // 타이틀
        var titleRect = MakeRect("Title", col, new Vector2(0f, 0.92f), new Vector2(1f, 1f));
        var titleLbl = AddTmpLabel(titleRect, "밴픽 시스템", 36f, TextAlignmentOptions.Center);
        titleLbl.color = Color.black;

        // 타이머/턴 라벨 (현재는 턴 안내로 사용; 타이머 텍스트는 placeholder)
        var infoRect = MakeRect("Info", col, new Vector2(0f, 0.84f), new Vector2(1f, 0.92f));
        turnLabel = AddTmpLabel(infoRect, "남은 시간 : 00s", 24f, TextAlignmentOptions.Center);
        turnLabel.color = Color.black;

        // 난이도 표시 (테스트 편의용)
        var diffRect = MakeRect("Difficulty", col, new Vector2(0f, 0.78f), new Vector2(1f, 0.84f));
        var diffLbl = AddTmpLabel(diffRect, $"AI 난이도: {DifficultyKor(PracticeSettings.Difficulty)}", 20f, TextAlignmentOptions.Center);
        diffLbl.color = new Color(0.3f, 0.3f, 0.3f, 1f);

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

        // 하단 중앙 "결정" 버튼: 플레이어가 카드를 선택한 뒤 눌러야 픽이 확정된다.
        // 카드 영역(y 0.1) 아래 빈 공간(0.0~0.1)에 가로 30% 폭으로 배치.
        var confirmRect = MakeRect("ConfirmPickButton", col, new Vector2(0.35f, 0.02f), new Vector2(0.65f, 0.1f));
        var confirmImg = AddImage(confirmRect, Color.white);
        var confirmLbl = AddTmpLabel(confirmRect, "결정", 28f, TextAlignmentOptions.Center);
        confirmLbl.color = Color.black;

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
    }

    // ── 패 확인 + 5번의 1:1 매치 단계 ─────────────────────────────────────

    // 14턴 드래프트가 끝나면 호출. 중앙 컬럼을 비우고 "패 확인" UI를 그린 뒤 30초 카운트다운.
    private void EnterHandReviewPhase()
    {
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
        matchTimerLabel.color = Color.black;

        var infoRect = MakeRect("HandReviewInfo", centerColTransform, new Vector2(0.05f, 0.30f), new Vector2(0.95f, 0.70f));
        var infoLbl = AddTmpLabel(infoRect,
            $"라운드 {SeriesState.CurrentRound}/{SeriesState.TotalRounds}   시리즈 스코어 {SeriesState.PlayerScore} - {SeriesState.AiScore}\n\n30초 후 시합이 시작됩니다.\n좌/우의 내 패와 상대 패를 확인하세요.",
            24f, TextAlignmentOptions.Center);
        infoLbl.color = new Color(0.3f, 0.3f, 0.3f, 1f);
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
        ShowMatchStartPopup();
    }

    // "시합을 시작합니다" 팝업 — 확인 누르면 첫 매치 진입
    private void ShowMatchStartPopup()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        matchStartPopup = new GameObject("MatchStartPopup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        matchStartPopup.transform.SetParent(canvas.transform, false);
        var overlayRt = (RectTransform)matchStartPopup.transform;
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;
        matchStartPopup.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);
        matchStartPopup.transform.SetAsLastSibling();

        var box = new GameObject("Box", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        box.transform.SetParent(matchStartPopup.transform, false);
        var boxRt = (RectTransform)box.transform;
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.pivot = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(700f, 360f);
        boxRt.anchoredPosition = Vector2.zero;
        box.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.97f);

        var titleRect = MakeRect("Title", boxRt, new Vector2(0f, 0.45f), new Vector2(1f, 0.95f));
        var title = AddTmpLabel(titleRect, "시합을 시작합니다", 40f, TextAlignmentOptions.Center);
        title.color = Color.white;

        var btnRect = MakeRect("ConfirmButton", boxRt, new Vector2(0.30f, 0.10f), new Vector2(0.70f, 0.38f));
        var btnImg = AddImage(btnRect, Color.white);
        var btnLbl = AddTmpLabel(btnRect, "확인", 28f, TextAlignmentOptions.Center);
        btnLbl.color = Color.black;
        var startBtn = btnRect.gameObject.AddComponent<Button>();
        startBtn.targetGraphic = btnImg;
        startBtn.onClick.AddListener(OnMatchStartConfirmed);
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
        matchTimerLabel.color = Color.black;

        var infoRect = MakeRect("MatchInfo", centerColTransform, new Vector2(0f, 0.76f), new Vector2(1f, 0.84f));
        var infoLbl = AddTmpLabel(infoRect,
            $"라운드 {SeriesState.CurrentRound}/{SeriesState.TotalRounds}   시리즈 스코어 {SeriesState.PlayerScore} - {SeriesState.AiScore}\n낼 카드 한 장을 고른 뒤 픽 버튼을 누르세요.",
            18f, TextAlignmentOptions.Center);
        infoLbl.color = new Color(0.3f, 0.3f, 0.3f, 1f);

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
                btn.onClick.AddListener(() => OnMatchCardClicked(captured));
                matchCardButtons.Add(btn);
                matchCardImages.Add(img);

                bool used = slotIdx < playerUsed.Length && playerUsed[slotIdx];
                btn.interactable = !used;
                if (used) AddUsedOverlayOnRect(cardRect);

                picked++;
            }
        }

        // 픽 버튼 (드래프트 단계의 "결정" 버튼과 동일 위치)
        var pickRect = MakeRect("MatchPickButton", centerColTransform, new Vector2(0.35f, 0.02f), new Vector2(0.65f, 0.10f));
        var pickImg = AddImage(pickRect, Color.white);
        var pickLbl = AddTmpLabel(pickRect, "픽", 28f, TextAlignmentOptions.Center);
        pickLbl.color = Color.black;
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
        if (matchTimerCoroutine != null) { StopCoroutine(matchTimerCoroutine); matchTimerCoroutine = null; }
        foreach (var b in matchCardButtons) if (b != null) b.interactable = false;
        if (matchPickButton != null) matchPickButton.interactable = false;
        pendingPlayerSlotIdx = selectedMatchSlotIndex;
        ShowBetPopup();
    }

    // 20초 카운트다운 — 만료 시 자동 픽 (선택된 카드 또는 미사용 중 무작위)
    private IEnumerator MatchPickTimerRoutine()
    {
        float remaining = MatchPickTimeLimit;
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
        AutoMatchPick();
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
        SubmitMatchPick(idx, autoBet);
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
        box.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.97f);

        // 타이틀
        var titleRect = MakeRect("Title", boxRt, new Vector2(0f, 0.80f), new Vector2(1f, 0.95f));
        var title = AddTmpLabel(titleRect,
            forced ? $"최종 매치 — 남은 포인트 전액 베팅" : $"포인트 베팅 — 매치 {currentMatchIndex + 1}/{TotalMatches}",
            30f, TextAlignmentOptions.Center);
        title.color = new Color(1f, 0.85f, 0.4f, 1f);

        // 정보
        var infoRect = MakeRect("Info", boxRt, new Vector2(0.05f, 0.58f), new Vector2(0.95f, 0.79f));
        string infoText = forced
            ? $"마지막 매치 — 남은 보유 포인트를 자동으로 전액 베팅합니다."
            : $"보유 포인트: {playerWallet}pt\n베팅 범위: {currentBetMin} ~ {currentBetMax}pt\n(남은 매치 {remainingAfter}회 × 최소 {MinBetPerMatch}pt 확보)";
        var infoLbl = AddTmpLabel(infoRect, infoText, 22f, TextAlignmentOptions.Center);
        infoLbl.color = Color.white;

        // 값 표시
        var valueRect = MakeRect("Value", boxRt, new Vector2(0.22f, 0.32f), new Vector2(0.78f, 0.54f));
        AddImage(valueRect, new Color(0.2f, 0.2f, 0.2f, 1f));
        betValueLabel = AddTmpLabel(valueRect, $"{currentBetValue}pt", 50f, TextAlignmentOptions.Center);
        betValueLabel.color = Color.white;

        if (!forced)
        {
            var minusRect = MakeRect("MinusBtn", boxRt, new Vector2(0.06f, 0.32f), new Vector2(0.20f, 0.54f));
            var minusImg = AddImage(minusRect, Color.white);
            var minusLbl = AddTmpLabel(minusRect, "-5", 32f, TextAlignmentOptions.Center);
            minusLbl.color = Color.black;
            betMinusBtn = minusRect.gameObject.AddComponent<Button>();
            betMinusBtn.targetGraphic = minusImg;
            var mc = betMinusBtn.colors;
            mc.disabledColor = new Color(0.78f, 0.78f, 0.78f, 0.6f);
            betMinusBtn.colors = mc;
            betMinusBtn.onClick.AddListener(OnBetMinus);

            var plusRect = MakeRect("PlusBtn", boxRt, new Vector2(0.80f, 0.32f), new Vector2(0.94f, 0.54f));
            var plusImg = AddImage(plusRect, Color.white);
            var plusLbl = AddTmpLabel(plusRect, "+5", 32f, TextAlignmentOptions.Center);
            plusLbl.color = Color.black;
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

        // 확정 버튼
        var confirmRect = MakeRect("ConfirmBtn", boxRt, new Vector2(0.30f, 0.06f), new Vector2(0.70f, 0.26f));
        var confirmImg = AddImage(confirmRect, Color.white);
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
        if (betPopup != null) { Destroy(betPopup); betPopup = null; }
        SubmitMatchPick(pendingPlayerSlotIdx, bet);
    }

    // AI 베팅 결정 — 합리적 범위에서 5pt 단위 무작위. 마지막 매치는 강제 전액.
    private int DecideAiBet()
    {
        int remainingAfter = TotalMatches - currentMatchIndex - 1;
        if (remainingAfter <= 0) return Mathf.Max(0, aiWallet);
        int min = MinBetPerMatch;
        int max = Mathf.Max(MinBetPerMatch, aiWallet - remainingAfter * MinBetPerMatch);
        if (max <= min) return min;
        int steps = (max - min) / MinBetPerMatch;
        int randSteps = Random.Range(0, steps + 1);
        return min + randSteps * MinBetPerMatch;
    }

    // 양쪽 픽/베팅 동시 확정 — 베팅 정산 + 사용 표시 + 결과 팝업.
    // 베팅 정산 규칙 (보유 포인트는 베팅 시 빠지면 그대로 유지, 회수는 수익으로 표시):
    //   - 베팅 시 wallet -= bet
    //   - 승리 시 earnings += bet*2  (수익에 베팅의 두 배 적립)
    //   - 패배 시 변화 없음 (잠긴 베팅 영구 손실)
    //   - 무승부 시 earnings += bet  (수익에 베팅액만큼 적립 — 보유에서 빠진 만큼 수익으로 돌아옴)
    private void SubmitMatchPick(int playerSlotIdx, int playerBet)
    {
        if (matchTimerCoroutine != null) { StopCoroutine(matchTimerCoroutine); matchTimerCoroutine = null; }
        foreach (var b in matchCardButtons) if (b != null) b.interactable = false;
        if (matchPickButton != null) matchPickButton.interactable = false;

        var aiCandidates = new List<int>();
        for (int i = 0; i < aiUsed.Length; i++) if (!aiUsed[i]) aiCandidates.Add(i);
        if (aiCandidates.Count == 0) return;
        int aiSlotIdx = aiCandidates[Random.Range(0, aiCandidates.Count)];
        int aiBet = DecideAiBet();

        playerUsed[playerSlotIdx] = true;
        aiUsed[aiSlotIdx] = true;

        var playerElem = playerPickHistory[playerSlotIdx];
        var aiElem = aiPickHistory[aiSlotIdx];
        playerMatchHistory.Add(playerElem);
        aiMatchHistory.Add(aiElem);
        playerBetHistory.Add(playerBet);
        aiBetHistory.Add(aiBet);

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

        matchResultPopup = new GameObject("MatchResultPopup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        matchResultPopup.transform.SetParent(canvas.transform, false);
        var overlayRt = (RectTransform)matchResultPopup.transform;
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;
        matchResultPopup.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);
        matchResultPopup.transform.SetAsLastSibling();

        var box = new GameObject("Box", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        box.transform.SetParent(matchResultPopup.transform, false);
        var boxRt = (RectTransform)box.transform;
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.pivot = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(760f, 480f);
        boxRt.anchoredPosition = Vector2.zero;
        box.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.97f);

        var titleRect = MakeRect("Title", boxRt, new Vector2(0f, 0.80f), new Vector2(1f, 0.96f));
        var title = AddTmpLabel(titleRect, $"매치 {currentMatchIndex + 1} 결과", 30f, TextAlignmentOptions.Center);
        title.color = new Color(1f, 0.85f, 0.4f, 1f);

        int playerDelta = outcome == MatchOutcome.Win ? +playerBet : outcome == MatchOutcome.Lose ? -playerBet : 0;
        int aiDelta = outcome == MatchOutcome.Win ? -aiBet : outcome == MatchOutcome.Lose ? +aiBet : 0;
        string body =
            $"나: {playerElem}  (베팅 {playerBet}pt)\n" +
            $"AI: {aiElem}  (베팅 {aiBet}pt)\n\n" +
            $"→ {OutcomeKor(outcome)}\n" +
            $"포인트: 나 {FormatSigned(playerDelta)}pt,  AI {FormatSigned(aiDelta)}pt";

        var contentRect = MakeRect("Content", boxRt, new Vector2(0f, 0.28f), new Vector2(1f, 0.80f));
        var content = AddTmpLabel(contentRect, body, 24f, TextAlignmentOptions.Center);
        content.color = Color.white;

        var btnRect = MakeRect("ConfirmButton", boxRt, new Vector2(0.35f, 0.06f), new Vector2(0.65f, 0.24f));
        var btnImg = AddImage(btnRect, Color.white);
        var btnLbl = AddTmpLabel(btnRect, "확인", 26f, TextAlignmentOptions.Center);
        btnLbl.color = Color.black;
        var btn = btnRect.gameObject.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.onClick.AddListener(OnMatchResultConfirmed);
    }

    private void OnMatchResultConfirmed()
    {
        if (matchResultPopup != null) { Destroy(matchResultPopup); matchResultPopup = null; }
        int next = currentMatchIndex + 1;
        if (next >= TotalMatches) FinalizeMatches();
        else BeginMatch(next);
    }

    // 5번 매치 종료: 시리즈 점수 반영 + 최종 결과 팝업
    private void FinalizeMatches()
    {
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
        boxRt.sizeDelta = new Vector2(820f, 760f);
        boxRt.anchoredPosition = Vector2.zero;
        box.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.97f);

        var textRect = MakeRect("Content", boxRt, new Vector2(0f, 0.26f), new Vector2(1f, 1f));
        var content = AddTmpLabel(textRect, BuildMatchSummaryText(), 22f, TextAlignmentOptions.Center);
        content.color = Color.white;

        var bottomRect = MakeRect("Bottom", boxRt, new Vector2(0f, 0.02f), new Vector2(1f, 0.24f));
        if (SeriesState.IsSeriesOver)
        {
            string winner = SeriesState.PlayerWonSeries ? "내가" : "상대가";
            var endRect = MakeRect("EndLabel", bottomRect, new Vector2(0f, 0.55f), new Vector2(1f, 1f));
            var endLbl = AddTmpLabel(endRect,
                $"시리즈 종료 — {winner} 우승!",
                28f, TextAlignmentOptions.Center);
            endLbl.color = new Color(1f, 0.85f, 0.4f, 1f);

            // 홈으로 / 재시작 두 버튼 (좌/우)
            var homeRect = MakeRect("HomeButton", bottomRect, new Vector2(0.10f, 0.05f), new Vector2(0.46f, 0.50f));
            var homeImg = AddImage(homeRect, Color.white);
            var homeLbl = AddTmpLabel(homeRect, "홈으로", 24f, TextAlignmentOptions.Center);
            homeLbl.color = Color.black;
            var homeBtn = homeRect.gameObject.AddComponent<Button>();
            homeBtn.targetGraphic = homeImg;
            var hc = homeBtn.colors;
            hc.normalColor = Color.white;
            hc.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            hc.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            homeBtn.colors = hc;
            homeBtn.onClick.AddListener(OnSeriesEndHomeClicked);

            var restartRect = MakeRect("RestartButton", bottomRect, new Vector2(0.54f, 0.05f), new Vector2(0.90f, 0.50f));
            var restartImg = AddImage(restartRect, Color.white);
            var restartLbl = AddTmpLabel(restartRect, "재시작", 24f, TextAlignmentOptions.Center);
            restartLbl.color = Color.black;
            var restartBtn = restartRect.gameObject.AddComponent<Button>();
            restartBtn.targetGraphic = restartImg;
            var rc = restartBtn.colors;
            rc.normalColor = Color.white;
            rc.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            rc.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            restartBtn.colors = rc;
            restartBtn.onClick.AddListener(OnSeriesEndRestartClicked);
        }
        else
        {
            var btnRect = MakeRect("NextRoundButton", bottomRect, new Vector2(0.35f, 0.20f), new Vector2(0.65f, 0.80f));
            var btnImg = AddImage(btnRect, Color.white);
            var btnLbl = AddTmpLabel(btnRect, "다음 라운드", 26f, TextAlignmentOptions.Center);
            btnLbl.color = Color.black;
            var btn = btnRect.gameObject.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(OnNextRoundClicked);
        }
    }

    // 시리즈 종료 후 메인 홈 씬으로 이동 (다른 홈 버튼들과 일관)
    private void OnSeriesEndHomeClicked()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("Main_Home");
    }

    // 시리즈 종료 후 현재 PracticeSettings 그대로 Practice 씬 재로드 (= 같은 설정으로 다시 시작)
    private void OnSeriesEndRestartClicked()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("Practice");
    }

    // "다음 라운드" 버튼 → PracticeCardController가 다음 라운드를 시작하도록 위임
    private void OnNextRoundClicked()
    {
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

    // 좌/우 슬롯 패널 위에 "사용됨" 오버레이(어두운 박스 + 체크) 추가. raw 이미지 자리는 텍스트 체크(✓)로 대체.
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
        overlayGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        overlayGo.transform.SetAsLastSibling();

        var checkLbl = AddTmpLabel((RectTransform)overlayGo.transform, "✓", 48f, TextAlignmentOptions.Center);
        checkLbl.color = new Color(0.4f, 1f, 0.5f, 1f);
        checkLbl.fontStyle = FontStyles.Bold;

        overlays.Add(overlayGo);
    }

    // 매치 단계 중앙 카드 위에 "사용됨" 오버레이(어두운 박스 + 체크) — 다음 매치 빌드 시 이미 사용된 카드 시각화
    private void AddUsedOverlayOnRect(RectTransform card)
    {
        var overlayGo = new GameObject("UsedOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        overlayGo.transform.SetParent(card, false);
        var rt = (RectTransform)overlayGo.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        overlayGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        overlayGo.transform.SetAsLastSibling();
        var checkLbl = AddTmpLabel((RectTransform)overlayGo.transform, "✓", 38f, TextAlignmentOptions.Center);
        checkLbl.color = new Color(0.4f, 1f, 0.5f, 1f);
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

    private static string DifficultyKor(PracticeSetupManager.AIDifficulty d) => d switch
    {
        PracticeSetupManager.AIDifficulty.Easy => "쉬움",
        PracticeSetupManager.AIDifficulty.Normal => "중간",
        PracticeSetupManager.AIDifficulty.Hard => "어려움",
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
