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
    // 순서 변경 단계 제한 시간(초). 만료 시 플레이어 현재 배치 고정 + AI 무작위 셔플 + 결과 팝업.
    private const float OrderingTimeLimit = 30f;
    // 슬롯 수: 1~5번(정렬용) + 미출전 2개 = 7
    private const int MainOrderSlotCount = 5;
    private const int BenchSlotCount = 2;
    private const int TotalOrderSlotCount = MainOrderSlotCount + BenchSlotCount;

    private TMP_FontAsset font;

    // UI 참조 (모두 BuildUi에서 새로 생성됨)
    private readonly List<TMP_Text> playerSlotLabels = new List<TMP_Text>();
    private readonly List<TMP_Text> aiSlotLabels = new List<TMP_Text>();
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

    // 순서 변경 단계용 상태/UI
    private RectTransform centerColTransform;            // 픽/순서변경 단계에서 공통 재사용하는 중앙 컬럼
    private readonly List<ElementType> playerSlotOrder = new List<ElementType>(); // 7칸: 0~4 = 1~5번, 5~6 = 미출전
    private readonly List<ElementType> aiSlotOrder = new List<ElementType>();
    private readonly List<TMP_Text> orderSlotLabels = new List<TMP_Text>();      // 슬롯별 텍스트 라벨
    private readonly List<OrderSlotDragHandler> orderSlotHandlers = new List<OrderSlotDragHandler>();
    private TMP_Text orderingTitleLabel;
    private TMP_Text orderingTimerLabel;
    private Coroutine orderingTimerCoroutine;

    // 시리즈(다판제) 흐름을 위해 PracticeCardController 참조와 최종 팝업 GameObject를 보관
    private PracticeCardController practiceController;
    private GameObject finalOrderOverlay;

    // 카드 색상 상수
    private static readonly Color CardDefaultColor = new Color(0.86f, 0.86f, 0.86f, 1f);
    private static readonly Color CardSelectedColor = new Color(1f, 0.85f, 0.3f, 1f); // 노란 하이라이트

    // 외부(PracticeCardController)에서 호출. 픽 순서와 카드 수, "다음 라운드"용 컨트롤러 참조를 받는다.
    // 매 라운드 다시 호출 가능 — BuildUi가 자식과 리스트를 모두 비우므로 동일 인스턴스에서 N라운드 반복 OK.
    public void StartDraft(bool playerGoesFirst, int rpsCount, TMP_FontAsset fontAsset, PracticeCardController controller = null)
    {
        font = fontAsset;
        practiceController = controller;

        // 이전 라운드의 최종 결과 오버레이가 캔버스에 남아 있으면 정리
        if (finalOrderOverlay != null)
        {
            Destroy(finalOrderOverlay);
            finalOrderOverlay = null;
        }
        // 순서 변경 단계 잔여 상태 정리
        playerSlotOrder.Clear();
        aiSlotOrder.Clear();
        orderSlotLabels.Clear();
        orderSlotHandlers.Clear();

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
            // 드래프트 종료 → 순서 변경 단계로 진입 (중앙 UI 교체 + 30초 타이머 시작)
            EnterOrderingPhase();
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
        if (isPlayer)
        {
            if (playerCursor < playerSlotLabels.Count)
            {
                playerSlotLabels[playerCursor].text = element.ToString();
                playerCursor++;
            }
            playerPickHistory.Add(element);
        }
        else
        {
            if (aiCursor < aiSlotLabels.Count)
            {
                aiSlotLabels[aiCursor].text = element.ToString();
                aiCursor++;
            }
            aiPickHistory.Add(element);
        }
        currentPickIndex++;
        AdvanceTurn();
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

        BuildSidePanel(left, "A\n(Player)", playerSlotLabels);
        BuildSidePanel(right, "B\n(Other Player)", aiSlotLabels);
        BuildCenter(center, rpsCount);
    }

    private RectTransform MakeColumn(string name, float xMin, float xMax)
    {
        var rt = MakeRect(name, (RectTransform)transform, new Vector2(xMin, 0f), new Vector2(xMax, 1f));
        return rt;
    }

    // 측면 패널: 상단 헤더(이름) + 아래 7개 슬롯(번갈아 음영)
    private void BuildSidePanel(RectTransform col, string headerText, List<TMP_Text> slotOut)
    {
        const float headerHeightRatio = 0.12f;
        const float slotsTopRatio = 1f - headerHeightRatio;

        // 헤더
        var header = MakeRect("Header", col, new Vector2(0f, slotsTopRatio), new Vector2(1f, 1f));
        AddImage(header, new Color(0.78f, 0.78f, 0.78f, 1f));
        var headerLabel = AddTmpLabel(header, headerText, 26f, TextAlignmentOptions.Center);
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
            var lbl = AddTmpLabel(slot, "", 22f, TextAlignmentOptions.Center);
            lbl.color = Color.black;
            slotOut.Add(lbl);
        }
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
                var img = AddImage(cardRect, CardDefaultColor);
                var lbl = AddTmpLabel(cardRect, cardElements[elemIdx].ToString(), 28f, TextAlignmentOptions.Center);
                lbl.color = Color.black;

                var btn = cardRect.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;
                int captured = elemIdx; // 클로저 캡쳐 — 각 버튼이 자기 인덱스를 보존
                btn.onClick.AddListener(() => OnCardClicked(captured));
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

    // ── 순서 변경 단계 ─────────────────────────────────────────────────────

    // 14턴 드래프트가 끝나면 호출. 중앙 컬럼을 비우고 1~5번/미출전 슬롯을 새로 그린 뒤 30초 타이머 시작.
    private void EnterOrderingPhase()
    {
        // 초기 배치: 픽 순서대로 → 1~5번 슬롯은 1~5번째 픽, 미출전은 6~7번째 픽
        playerSlotOrder.Clear();
        aiSlotOrder.Clear();
        for (int i = 0; i < TotalOrderSlotCount; i++)
        {
            playerSlotOrder.Add(i < playerPickHistory.Count ? playerPickHistory[i] : default);
            aiSlotOrder.Add(i < aiPickHistory.Count ? aiPickHistory[i] : default);
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
        // 픽 타이머도 정리 (이미 AdvanceTurn 진입부에서 멈췄지만 방어적으로)
        if (turnTimerCoroutine != null) { StopCoroutine(turnTimerCoroutine); turnTimerCoroutine = null; }

        BuildOrderingCenter();

        if (orderingTimerCoroutine != null) StopCoroutine(orderingTimerCoroutine);
        orderingTimerCoroutine = StartCoroutine(OrderingTimerRoutine());
    }

    // 순서 변경 단계 중앙 UI 빌드: 타이틀/타이머 + 1~5번 가로 슬롯 5개 + 미출전 슬롯 2개
    private void BuildOrderingCenter()
    {
        if (centerColTransform == null) return;
        orderSlotLabels.Clear();
        orderSlotHandlers.Clear();

        // 타이틀
        var titleRect = MakeRect("OrderingTitle", centerColTransform, new Vector2(0f, 0.90f), new Vector2(1f, 1f));
        orderingTitleLabel = AddTmpLabel(titleRect, "순서 변경 진행", 34f, TextAlignmentOptions.Center);
        orderingTitleLabel.color = Color.black;

        // 타이머
        var timerRect = MakeRect("OrderingTimer", centerColTransform, new Vector2(0f, 0.80f), new Vector2(1f, 0.89f));
        orderingTimerLabel = AddTmpLabel(timerRect, $"남은 시간 : {Mathf.CeilToInt(OrderingTimeLimit)}초", 22f, TextAlignmentOptions.Center);
        orderingTimerLabel.color = Color.black;

        // 상단 5개 슬롯 행
        var topRow = MakeRect("OrderTopRow", centerColTransform, new Vector2(0.05f, 0.42f), new Vector2(0.95f, 0.72f));
        for (int i = 0; i < MainOrderSlotCount; i++)
        {
            float xMin = (float)i / MainOrderSlotCount;
            float xMax = (float)(i + 1) / MainOrderSlotCount;
            var slot = MakeRect("Slot" + (i + 1), topRow,
                new Vector2(xMin + 0.02f, 0f), new Vector2(xMax - 0.02f, 1f));
            BuildOrderSlot(slot, slotIndex: i, header: (i + 1).ToString());
        }

        // 하단 미출전 슬롯 2개 (가운데 정렬)
        var benchRow = MakeRect("OrderBenchRow", centerColTransform, new Vector2(0.30f, 0.08f), new Vector2(0.70f, 0.38f));
        for (int i = 0; i < BenchSlotCount; i++)
        {
            float xMin = (float)i / BenchSlotCount;
            float xMax = (float)(i + 1) / BenchSlotCount;
            var slot = MakeRect("Bench" + (i + 1), benchRow,
                new Vector2(xMin + 0.05f, 0f), new Vector2(xMax - 0.05f, 1f));
            BuildOrderSlot(slot, slotIndex: MainOrderSlotCount + i, header: "미출전");
        }
    }

    // 단일 슬롯: 상단 헤더(번호 또는 "미출전") + 하단 카드(드래그 핸들러 부착)
    private void BuildOrderSlot(RectTransform container, int slotIndex, string header)
    {
        // 헤더 라벨
        var headerRect = MakeRect("Header", container, new Vector2(0f, 0.78f), new Vector2(1f, 1f));
        var headerLbl = AddTmpLabel(headerRect, header, 22f, TextAlignmentOptions.Center);
        headerLbl.color = Color.black;

        // 카드 — Image + Text + DragHandler
        var card = MakeRect("Card", container, new Vector2(0f, 0f), new Vector2(1f, 0.75f));
        AddImage(card, CardDefaultColor);
        var lbl = AddTmpLabel(card, playerSlotOrder[slotIndex].ToString(), 24f, TextAlignmentOptions.Center);
        lbl.color = Color.black;

        var handler = card.gameObject.AddComponent<OrderSlotDragHandler>();
        handler.slotIndex = slotIndex;
        handler.onSwap = OnOrderSlotSwap;

        orderSlotLabels.Add(lbl);
        orderSlotHandlers.Add(handler);
    }

    // 드래그 핸들러 콜백: 두 슬롯의 ElementType과 화면 라벨을 동시에 교환
    private void OnOrderSlotSwap(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex) return;
        if (fromIndex < 0 || fromIndex >= playerSlotOrder.Count) return;
        if (toIndex < 0 || toIndex >= playerSlotOrder.Count) return;

        (playerSlotOrder[fromIndex], playerSlotOrder[toIndex]) = (playerSlotOrder[toIndex], playerSlotOrder[fromIndex]);
        orderSlotLabels[fromIndex].text = playerSlotOrder[fromIndex].ToString();
        orderSlotLabels[toIndex].text = playerSlotOrder[toIndex].ToString();
    }

    // 30초 카운트다운 — 매 프레임 라벨 갱신, 만료 시 FinalizeOrdering 호출
    private IEnumerator OrderingTimerRoutine()
    {
        float remaining = OrderingTimeLimit;
        while (remaining > 0f)
        {
            if (orderingTimerLabel != null)
            {
                int secs = Mathf.CeilToInt(Mathf.Max(0f, remaining));
                orderingTimerLabel.text = $"남은 시간 : {secs}초";
            }
            yield return null;
            remaining -= Time.deltaTime;
        }
        if (orderingTimerLabel != null) orderingTimerLabel.text = "남은 시간 : 0초";
        FinalizeOrdering();
    }

    // 타이머 만료: 드래그 잠금 + AI 무작위 셔플 + 라운드 결과 시리즈에 반영 + 결과 팝업
    private void FinalizeOrdering()
    {
        foreach (var h in orderSlotHandlers)
        {
            if (h != null) h.SetDragEnabled(false);
        }
        ShuffleAiOrder();
        UpdateSeriesScoreFromRound();
        ShowFinalOrderPopup();
    }

    // 1~5번 슬롯끼리 대결한 결과(승/패/무)로 라운드 우열을 결정해 시리즈 점수에 반영.
    //   wins  > losses : 플레이어가 라운드 승 → PlayerScore++
    //   wins  < losses : AI 승                 → AiScore++
    //   wins == losses : 라운드 동점 → 시리즈 점수는 그대로 두고 LastRoundTied=true.
    //                    실제 결판은 다음 라운드 진입 시 PracticeCardController가 카드 뒤집기로 해결.
    private void UpdateSeriesScoreFromRound()
    {
        int wins = 0, losses = 0;
        for (int i = 0; i < MainOrderSlotCount; i++)
        {
            var outcome = TypeChart.GetOutcome(playerSlotOrder[i], aiSlotOrder[i]);
            if (outcome == MatchOutcome.Win) wins++;
            else if (outcome == MatchOutcome.Lose) losses++;
        }

        if (wins > losses)
        {
            SeriesState.LastRoundTied = false;
            SeriesState.LastRoundPlayerWon = true;
            SeriesState.PlayerScore++;
        }
        else if (wins < losses)
        {
            SeriesState.LastRoundTied = false;
            SeriesState.LastRoundPlayerWon = false;
            SeriesState.AiScore++;
        }
        else
        {
            SeriesState.LastRoundTied = true;
            SeriesState.LastRoundPlayerWon = false; // 의미 없음(동점), 결판전이 결정함
        }
    }

    // AI의 7개 픽을 Fisher-Yates로 무작위 셔플 → 1~5번 슬롯과 미출전이 모두 임의로 재배치됨
    private void ShuffleAiOrder()
    {
        for (int i = aiSlotOrder.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (aiSlotOrder[i], aiSlotOrder[j]) = (aiSlotOrder[j], aiSlotOrder[i]);
        }
    }

    // 최종 순서/대결 결과/시리즈 점수를 보여주는 팝업.
    // 시리즈가 진행 중이면 하단에 "다음 라운드" 버튼이 표시되고, 시리즈가 끝났으면 "시리즈 종료" 안내가 뜬다.
    private void ShowFinalOrderPopup()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        finalOrderOverlay = new GameObject("FinalOrderOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        finalOrderOverlay.transform.SetParent(canvas.transform, false);
        var overlayRt = (RectTransform)finalOrderOverlay.transform;
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;
        finalOrderOverlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);
        finalOrderOverlay.transform.SetAsLastSibling();

        var box = new GameObject("FinalOrderBox", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        box.transform.SetParent(finalOrderOverlay.transform, false);
        var boxRt = (RectTransform)box.transform;
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.pivot = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(820f, 680f);
        boxRt.anchoredPosition = Vector2.zero;
        box.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.97f);

        // 본문 텍스트 (라운드 결과 + 시리즈 점수)
        var textRect = MakeRect("Content", boxRt, new Vector2(0f, 0.18f), new Vector2(1f, 1f));
        var content = AddTmpLabel(textRect, BuildOrderSummaryText(), 22f, TextAlignmentOptions.Center);
        content.color = Color.white;

        // 하단 버튼/안내 영역
        var bottomRect = MakeRect("Bottom", boxRt, new Vector2(0f, 0.02f), new Vector2(1f, 0.16f));
        if (SeriesState.IsSeriesOver)
        {
            // 시리즈 종료 안내만
            string winner = SeriesState.PlayerWonSeries ? "내가" : "상대가";
            var endLbl = AddTmpLabel(bottomRect,
                $"시리즈 종료 — {winner} 우승!",
                28f, TextAlignmentOptions.Center);
            endLbl.color = new Color(1f, 0.85f, 0.4f, 1f);
        }
        else
        {
            // "다음 라운드" 버튼
            var btnRect = MakeRect("NextRoundButton", bottomRect, new Vector2(0.35f, 0.1f), new Vector2(0.65f, 0.9f));
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

    // "다음 라운드" 버튼 → 결정 그룹으로 복귀해 PracticeCardController가 라운드를 시작하도록 위임.
    // 결판전(직전 라운드 동점) 또는 일반 라운드(패자가 선/후픽 선택) 분기는 PracticeCardController가 처리.
    private void OnNextRoundClicked()
    {
        if (finalOrderOverlay != null)
        {
            Destroy(finalOrderOverlay);
            finalOrderOverlay = null;
        }
        if (practiceController == null)
        {
            // 백업 탐색 — 인스펙터/씬 구성에 따라 컨트롤러 참조를 못 받은 경우
            practiceController = FindObjectOfType<PracticeCardController>(true);
        }
        if (practiceController != null) practiceController.BeginNextRound();
    }

    // 1~5번 슬롯끼리 대결시킨 결과 + 라운드 요약 + 시리즈 점수 + 미출전 카드를 텍스트로 정리
    // 각 슬롯의 매치는 TypeChart.GetOutcome(나, 상대)로 판정한다.
    private string BuildOrderSummaryText()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"== 라운드 {SeriesState.CurrentRound} / {SeriesState.TotalRounds} 결과 ==");
        sb.AppendLine();

        int wins = 0, losses = 0, ties = 0;
        for (int i = 0; i < MainOrderSlotCount; i++)
        {
            var me = playerSlotOrder[i];
            var op = aiSlotOrder[i];
            var outcome = TypeChart.GetOutcome(me, op);
            switch (outcome)
            {
                case MatchOutcome.Win: wins++; break;
                case MatchOutcome.Lose: losses++; break;
                case MatchOutcome.Tie: ties++; break;
            }
            sb.AppendLine($"  {i + 1}번: 나 {me} vs 상대 {op} → {OutcomeKor(outcome)}");
        }
        sb.AppendLine();
        sb.AppendLine($"  라운드 전적: {wins}승 {losses}패 {ties}무");

        // 라운드 우열 요약
        string roundResult;
        if (wins > losses) roundResult = "이번 라운드 → 나의 승!";
        else if (wins < losses) roundResult = "이번 라운드 → 상대 승!";
        else roundResult = "이번 라운드 → 동점 (다음 라운드는 결판전 카드 뒤집기로 시작)";
        sb.AppendLine($"  {roundResult}");
        sb.AppendLine();

        // 시리즈 누적 점수
        sb.AppendLine($"  시리즈 점수: 나 {SeriesState.PlayerScore} - {SeriesState.AiScore} 상대 (선승 {SeriesState.RoundsToWin}점)");
        sb.AppendLine();

        sb.AppendLine("[ 미출전 ]");
        sb.AppendLine($"  나:   {playerSlotOrder[MainOrderSlotCount]}, {playerSlotOrder[MainOrderSlotCount + 1]}");
        sb.AppendLine($"  상대: {aiSlotOrder[MainOrderSlotCount]}, {aiSlotOrder[MainOrderSlotCount + 1]}");
        return sb.ToString();
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
