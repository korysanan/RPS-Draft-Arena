using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// RPS Draft Arena 의 AI 의사결정 모듈 — DraftController 에서 분리(추출)한 순수 자문(advisor) 클래스.
// 게임 상태를 변경하지 않고 "어떤 카드를 픽/출전할지, 얼마를 베팅할지"만 계산해 반환한다.
//
// 게임 상태는 ctrl(역참조)을 통해 읽기 전용 접근자로만 읽는다.
// AI 가 직접 소유/변경하는 상태는 베이지안 상대 모델(playerHonesty/playerAggression/bayesObservationCount)과
// 매치 카드 후보 버퍼(matchPickCandidates)뿐 — DraftController 한 번 생성 후 시리즈 내내 재사용되므로
// 이 상태들은 라운드 간 지속된다(베이지안 누적 학습이 가능한 이유).
//
// 난이도 요약:
//   Easy   = 완전 무작위
//   Normal = 50%(드래프트)/60%(매치) 전략 + 나머지 무작위
//   Hard   = 항상 전략적 — EV(기대값) 기반 + 시리즈 메타 + 베이지안 상대 모델 + 리저브/블러프
public class DraftAI
{
    // 게임 상태(픽 이력/지갑/사용 여부 등)를 읽기 위한 역참조. AI 는 이 상태를 절대 변경하지 않는다.
    private readonly DraftController ctrl;

    public DraftAI(DraftController controller)
    {
        ctrl = controller;
    }

    // ── AI 베팅 튜닝 상수 (Phase 1). 상수명은 스펙에 따라 SCREAMING_SNAKE_CASE 사용. ──
    private const int BET_STEP = 5;
    private const int NEUTRAL_BET_PER_MATCH = 20;  // 기준 베이스라인 = StartingPoints / TotalMatches. 방어용 fallback에서만 사용.

    // confidence weight: 매치업 우위(edge)가 베이스라인 베팅을 얼마나 흔드는지의 강도.
    // 0 = edge 무시 (Easy 같은 동작), 1 = edge [-1, +1] 범위에서 베이스라인 0배 ~ 2배까지 풀스윙.
    private const float NORMAL_CONFIDENCE_WEIGHT = 0.6f;
    private const float HARD_CONFIDENCE_WEIGHT = 0.85f;

    // Hard 전용: 매치업이 불리한 상황에서도 가끔 큰 베팅을 질러서 플레이어의 confidence 신호 읽기를 교란.
    private const float HARD_BLUFF_CHANCE = 0.10f;
    private const float HARD_BLUFF_BET_RATIO = 0.4f;

    // Hard 전용 시리즈 단위 메타 배수 (TotalRounds > 1 일 때만 적용).
    private const float SERIES_BEHIND_MULTIPLIER = 1.25f;
    private const float SERIES_AHEAD_MULTIPLIER = 0.8f;
    private const float SERIES_MATCH_POINT_MULTIPLIER = 1.4f;

    // AI 매치 카드 선택 튜닝 (Phase 2).
    private const float NORMAL_STRATEGIC_RATIO = 0.6f;

    // Hard 전용: 강력한 카드(EV >= 임계값)를 뒷판 큰 베팅용으로 리저브.
    private const float HARD_RESERVE_EV_THRESHOLD = 0.7f;
    private const int HARD_RESERVE_MIN_MATCHES_LEFT = 2;

    // Phase 3: 드래프트 픽 점수 가중치 (난이도별 튜닝 가능).
    // Normal 값은 원본 하드코딩 값과 정확히 일치 — Phase 3 적용 후에도 Normal 동작은 그대로 유지됨.
    private const int SCORE_WIN_NORMAL = 2;
    private const int SCORE_LOSE_NORMAL = -2;
    private const int SCORE_DIVERSITY_NORMAL = 1;

    private const int SCORE_WIN_HARD = 3;
    private const int SCORE_LOSE_HARD = -3;
    private const int SCORE_DIVERSITY_HARD = 1;

    // Hard 전용: 같은 원소를 이미 임계값만큼 드래프트했을 때 후보 점수에 적용하는 페널티.
    // raw 매치업 점수가 비슷할 때 스택되지 않은 원소 쪽으로 동점을 깨주는 역할.
    private const int HARD_OVERSTACK_THRESHOLD = 3;
    private const int HARD_OVERSTACK_PENALTY = -2;

    // Phase 4: 베이지안 상대 모델 파라미터 (Hard + 다판제에서만 동작).
    private const float BAYES_INITIAL_HONESTY = 0.5f;       // 정직도 사전확률 (0 = 항상 블러프, 1 = 항상 정직)
    private const float BAYES_INITIAL_AGGRESSION = 0.5f;    // 공격성 사전확률 (0 = 항상 미니멈 베팅, 1 = 항상 올인)
    private const float BAYES_LEARNING_RATE = 0.3f;         // 매 관측마다 Lerp 갱신 계수
    private const float BAYES_ROUND_DECAY = 0.7f;           // 라운드 사이 중립값 쪽으로 끌어당기는 강도 (1 = 그대로 유지, 0 = 완전 리셋)
    private const int BAYES_MIN_OBSERVATIONS = 3;           // 신뢰도 1.0 에 도달하는 관측 수

    // 모델이 AI 베팅 베이스라인에 영향을 주는 강도.
    private const float BAYES_AGGRESSION_INFLUENCE = 0.3f;
    private const float BAYES_HONESTY_INFLUENCE = 0.2f;

    // 매치 카드 선택 시마다 호출 단위 할당을 피하려고 재사용하는 후보 버퍼. capacity = aiUsed.Length (PicksPerSide).
    private readonly List<int> matchPickCandidates = new List<int>(DraftController.PicksPerSide);

    // Phase 4: 베이지안 상대 모델 상태. 시리즈 단위로 누적, 라운드 사이에 중립값 쪽으로 부드럽게 감쇠.
    // Hard + 다판제에서만 갱신/사용. 다른 모드에서는 초기값으로 고정.
    private float playerHonesty = BAYES_INITIAL_HONESTY;
    private float playerAggression = BAYES_INITIAL_AGGRESSION;
    private int bayesObservationCount = 0;

    // ── 드래프트 픽 ─────────────────────────────────────────────────

    // 난이도에 따른 AI 픽 결정:
    //  - Easy   : 완전 무작위
    //  - Normal : 50% 확률로 전략적, 50% 무작위 (절반쯤 실수하는 사람 느낌)
    //  - Hard   : 항상 전략적 — 상대 픽들을 상성표로 평가해 최대 점수의 카드를 픽
    public ElementType ChooseAiPick()
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
        return ctrl.CardElements[Random.Range(0, ctrl.CardElements.Count)];
    }

    // 각 카드에 점수를 매기고 최고 점수 후보 중 하나를 무작위로 픽 (인간 같은 변동성)
    private ElementType ChooseStrategicPick()
    {
        int bestScore = int.MinValue;
        var bestCandidates = new List<ElementType>();
        foreach (var e in ctrl.CardElements)
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

    // 상성 기반 점수 함수 (Phase 3: 난이도별 가중치 튜닝 가능).
    //  winScore       : 후보가 상대 픽 하나를 이김
    //  loseScore      : 후보가 상대 픽 하나에게 짐
    //  0              : 같은 속성(Tie)
    //  diversityScore : AI가 아직 안 뽑은 속성에 가산 (binary — 첫 보유 여부만 판정)
    //
    // Normal 가중치(2 / -2 / +1)는 원본 하드코딩 값과 정확히 일치 → Phase 3 적용 후에도 Normal 동작 그대로.
    // Hard 가중치(3 / -3 / +1)는 카운터픽 가중치를 증폭하고, 같은 속성이 HARD_OVERSTACK_THRESHOLD 이상
    // 쌓이면 오버스택 페널티까지 추가로 부여.
    //
    // 호출 경로: ChooseStrategicPick → Hard 는 항상, Normal 은 픽의 50%. Easy 는 절대 도달 안 함.
    private int ScoreElement(ElementType candidate)
    {
        var difficulty = PracticeSettings.Difficulty;

        int winScore, loseScore, diversityScore;
        bool applyOverstackPenalty;

        if (difficulty == PracticeSetupManager.AIDifficulty.Hard)
        {
            winScore = SCORE_WIN_HARD;
            loseScore = SCORE_LOSE_HARD;
            diversityScore = SCORE_DIVERSITY_HARD;
            applyOverstackPenalty = true;
        }
        else // Normal (예상 외 난이도가 들어와도 Normal 동작 — Easy 는 여기로 안 옴)
        {
            winScore = SCORE_WIN_NORMAL;
            loseScore = SCORE_LOSE_NORMAL;
            diversityScore = SCORE_DIVERSITY_NORMAL;
            applyOverstackPenalty = false;
        }

        // 루프 구조는 원본 그대로, 숫자 상수만 변수로 치환.
        int score = 0;
        foreach (var opp in ctrl.PlayerPickHistory)
        {
            if (TypeChart.Beats(candidate, opp)) score += winScore;
            else if (TypeChart.Beats(opp, candidate)) score += loseScore;
            // 같은 속성(Tie)이면 0
        }
        if (!ctrl.AiPickHistory.Contains(candidate)) score += diversityScore;

        // Hard 전용: 같은 속성을 임계값 이상 쌓고 있으면 페널티 부여.
        if (applyOverstackPenalty)
        {
            int existingCount = 0;
            for (int i = 0; i < ctrl.AiPickHistory.Count; i++)
            {
                if (ctrl.AiPickHistory[i] == candidate) existingCount++;
            }
            if (existingCount >= HARD_OVERSTACK_THRESHOLD)
            {
                score += HARD_OVERSTACK_PENALTY;
            }
        }

        return score;
    }

    // ── 베팅 ────────────────────────────────────────────────────────

    // AI 베팅 결정 (Phase 1).
    //   Easy   : 기존 균등 무작위 동작 그대로
    //   Normal : 상대 남은 풀 대비 confidence 가중 베팅
    //   Hard   : Normal + 시리즈 단위 메타 배수 + 가끔 블러프
    // 정보 제약: playerPickHistory + playerUsed + 자기 aiPickHistory 만 읽음.
    // 상대의 현재 매치 픽이나 현재 베팅은 절대 보지 않음.
    public int DecideAiBet(int aiSlotIdx)
    {
        int remainingAfter = DraftController.TotalMatches - ctrl.CurrentMatchIndex - 1;
        if (remainingAfter <= 0) return Mathf.Max(0, ctrl.AiWallet); // 마지막 매치: 강제 전액 소진

        var difficulty = PracticeSettings.Difficulty;
        if (difficulty == PracticeSetupManager.AIDifficulty.Easy)
        {
            return DecideRandomBet(remainingAfter);
        }

        // Normal / Hard: 매치업 edge 기반 사이즈 결정.
        var aiCard = ctrl.AiPickHistory[aiSlotIdx];
        float edge = ComputeMatchupEdge(aiCard);

        int remainingMatches = DraftController.TotalMatches - ctrl.CurrentMatchIndex;
        int baseBet = remainingMatches > 0
            ? ctrl.AiWallet / remainingMatches
            : NEUTRAL_BET_PER_MATCH;
        float weight = (difficulty == PracticeSetupManager.AIDifficulty.Hard)
            ? HARD_CONFIDENCE_WEIGHT
            : NORMAL_CONFIDENCE_WEIGHT;
        float desiredBet = baseBet * (1f + edge * weight);

        // Hard 전용: 시리즈 메타는 다판제일 때만 의미가 있음.
        if (difficulty == PracticeSetupManager.AIDifficulty.Hard && SeriesState.TotalRounds > 1)
        {
            desiredBet *= GetSeriesMultiplier();
        }

        // Phase 4 (Hard + 다판제): 베이지안 모델로 베이스라인 미세 조정.
        // confidence 가중치를 곱해서 초반 관측 부족 시에는 영향을 거의 안 주도록 함.
        if (difficulty == PracticeSetupManager.AIDifficulty.Hard && SeriesState.TotalRounds > 1)
        {
            float bayesConfidence = GetBayesConfidence();

            // 공격성 시프트: 상대가 평균보다 크게 베팅하는 성향이면 AI 도 따라 키움.
            float aggressionShift = (playerAggression - BAYES_INITIAL_AGGRESSION)
                                    * BAYES_AGGRESSION_INFLUENCE
                                    * bayesConfidence;
            desiredBet *= (1f + aggressionShift);

            // 정직도 조정은 AI 가 유리한 매치업일 때만 적용.
            //   정직한 상대 = 예측 가능 → AI 가 우위에서 더 크게 누르기 좋음
            //   기만적 상대 = 블러프 가능성 → AI 도 우위에서 베팅을 살짝 줄여 헷지
            if (edge > 0f)
            {
                float honestyAdjust = (playerHonesty - BAYES_INITIAL_HONESTY)
                                      * BAYES_HONESTY_INFLUENCE
                                      * bayesConfidence;
                desiredBet *= (1f + honestyAdjust);
            }
        }

        // Hard 전용: 매치업이 불리할 때 낮은 확률로 블러프.
        if (difficulty == PracticeSetupManager.AIDifficulty.Hard
            && edge < 0f
            && Random.value < HARD_BLUFF_CHANCE)
        {
            desiredBet = ctrl.AiWallet * HARD_BLUFF_BET_RATIO;
        }

        int finalBet = ClampAndSnapBet(desiredBet, remainingAfter);

#if UNITY_EDITOR
        Debug.Log($"[AI/{difficulty}] match={ctrl.CurrentMatchIndex + 1} card={aiCard} edge={edge:F2} bet={finalBet}");
#endif
        return finalBet;
    }

    // 매치업 edge ([-1, +1] 범위). +1 = aiCard 가 상대 남은 카드를 전부 이김,
    // -1 = aiCard 가 상대 남은 카드 전부에게 짐. 같은 속성(Tie)은 점수 기여 없음.
    // 상대 남은 카드가 0장이면 0 반환 (방어용 — 정상 흐름에서는 라운드 중에 발생 불가).
    private float ComputeMatchupEdge(ElementType aiCard)
    {
        int score = 0;
        int totalRemaining = 0;
        for (int i = 0; i < ctrl.PlayerPickHistory.Count; i++)
        {
            if (ctrl.PlayerUsed != null && i < ctrl.PlayerUsed.Length && ctrl.PlayerUsed[i]) continue;
            var opponentCard = ctrl.PlayerPickHistory[i];
            if (TypeChart.Beats(aiCard, opponentCard)) score++;
            else if (TypeChart.Beats(opponentCard, aiCard)) score--;
            totalRemaining++;
        }
        if (totalRemaining == 0) return 0f;
        return (float)score / totalRemaining;
    }

    // Hard 전용 시리즈 메타 배수:
    //   시리즈 매치 포인트 (어느 쪽이든) : 최대 공격성
    //   시리즈 점수 뒤처짐 : 약간 공격적
    //   시리즈 점수 앞섬   : 약간 보수적
    //   동점               : 중립
    private float GetSeriesMultiplier()
    {
        bool aiAtMatchPoint = SeriesState.AiScore == SeriesState.RoundsToWin - 1;
        bool playerAtMatchPoint = SeriesState.PlayerScore == SeriesState.RoundsToWin - 1;
        if (aiAtMatchPoint || playerAtMatchPoint)
            return SERIES_MATCH_POINT_MULTIPLIER;

        int diff = SeriesState.AiScore - SeriesState.PlayerScore;
        if (diff < 0) return SERIES_BEHIND_MULTIPLIER;
        if (diff > 0) return SERIES_AHEAD_MULTIPLIER;
        return 1f;
    }

    // 원하는 베팅을 BET_STEP 단위로 스냅한 뒤 [MinBetPerMatch, max] 로 clamp.
    // max 는 남은 매치 수 × MinBetPerMatch 만큼 예비 자금을 남겨두는 식으로 산정 (원본 random clamp 와 동일).
    // → Easy 경로와 confidence 경로가 모두 같은 합법 범위 규칙을 따름.
    private int ClampAndSnapBet(float desiredBet, int remainingAfter)
    {
        int min = DraftController.MinBetPerMatch;
        int max = Mathf.Max(DraftController.MinBetPerMatch, ctrl.AiWallet - remainingAfter * DraftController.MinBetPerMatch);
        int snapped = Mathf.RoundToInt(desiredBet / BET_STEP) * BET_STEP;
        return Mathf.Clamp(snapped, min, max);
    }

    // Easy: 원본 균등 무작위 동작 그대로.
    private int DecideRandomBet(int remainingAfter)
    {
        int min = DraftController.MinBetPerMatch;
        int max = Mathf.Max(DraftController.MinBetPerMatch, ctrl.AiWallet - remainingAfter * DraftController.MinBetPerMatch);
        if (max <= min) return min;
        int steps = (max - min) / DraftController.MinBetPerMatch;
        int randSteps = Random.Range(0, steps + 1);
        return min + randSteps * DraftController.MinBetPerMatch;
    }

    // ── Phase 4: 베이지안 상대 모델 ──────────────────────────────────────
    // Hard + 다판제에서만 동작. Easy/Normal/단판제에서는 모든 메서드가 early-return 하므로
    // 상태값이 초기값으로 고정되고 DecideAiBet 의 Bayesian 조정 블록도 효과가 0.

    // 방금 끝난 매치 데이터로 모델 갱신.
    // matchIndex: 방금 끝난 매치의 인덱스 (호출 시점의 currentMatchIndex).
    public void UpdateBayesianModel(int matchIndex)
    {
        if (PracticeSettings.Difficulty != PracticeSetupManager.AIDifficulty.Hard) return;
        if (SeriesState.TotalRounds <= 1) return;  // 단판제는 학습 가치 부족 (최대 5샘플 + 마지막 매치는 어차피 강제 전액)

        if (matchIndex < 0 || matchIndex >= ctrl.PlayerMatchHistory.Count) return;  // 방어용
        if (matchIndex >= ctrl.PlayerBetHistory.Count) return;                       // 방어용

        ElementType playerCard = ctrl.PlayerMatchHistory[matchIndex];
        int playerBet = ctrl.PlayerBetHistory[matchIndex];

        // 1. 이 매치 시점에서 상대 카드가 AI 풀 대비 어떤 edge 였는지 재구성
        float playerEdge = EstimateHistoricalPlayerEdge(matchIndex, playerCard);

        // 2. 베팅이 중립 베이스라인(NEUTRAL_BET_PER_MATCH = 20pt) 대비 얼마나 큰지 정규화
        float betDeviation = (playerBet - NEUTRAL_BET_PER_MATCH) / (float)NEUTRAL_BET_PER_MATCH;
        betDeviation = Mathf.Clamp(betDeviation, -1f, 1f);

        // 3. 정직도 신호:
        //    edge 부호 == 베팅 편차 부호 → 정직 (강한 카드를 크게 베팅 / 약한 카드를 작게 베팅)
        //    부호가 다름 → 기만적 (강한 카드를 작게 / 약한 카드를 크게)
        float honestySignal;
        if (Mathf.Sign(playerEdge) == Mathf.Sign(betDeviation))
            honestySignal = Mathf.Abs(playerEdge * betDeviation);  // 양쪽 모두 극단이면 강한 신호
        else
            honestySignal = -Mathf.Abs(playerEdge * betDeviation);
        honestySignal = (honestySignal + 1f) * 0.5f;  // [-1, +1] → [0, 1]

        // 4. Lerp 증분 갱신
        playerHonesty = Mathf.Lerp(playerHonesty, honestySignal, BAYES_LEARNING_RATE);

        // 5. 공격성: 베팅의 절대 크기를 시작 자금 대비 비율로 본 이동평균
        float observedAggression = playerBet / (float)DraftController.StartingPoints;
        playerAggression = Mathf.Lerp(playerAggression, observedAggression, BAYES_LEARNING_RATE);

        bayesObservationCount++;

#if UNITY_EDITOR
        Debug.Log($"[Bayes] match={matchIndex + 1} playerEdge={playerEdge:F2} " +
                  $"betDev={betDeviation:F2} → honesty={playerHonesty:F2} " +
                  $"aggr={playerAggression:F2} conf={GetBayesConfidence():F2}");
#endif
    }

    // matchIndex 매치 시점에서 상대 카드(playerCard)가 AI 의 "그 시점 남은 풀" 대비 어떤 edge 였는지 재구성.
    // 모델이 매치 종료 후에 갱신되므로 카드가 이미 소모된 뒤라 이렇게 거꾸로 계산해야 함.
    // 한계: AI 가 같은 원소를 중복 드래프트한 경우 (예: Fire ×2), 소모 추적이 슬롯이 아닌 원소 기준이라
    //       이전 매치에서 Fire 하나가 소모됐다면 남은 Fire 도 소모된 것으로 잡힘. v1 허용 오차로 둠 — 모델을
    //       망가뜨리진 않고 중복 사용 케이스에서만 살짝 편향됨.
    private float EstimateHistoricalPlayerEdge(int matchIndex, ElementType playerCard)
    {
        int totalRemaining = 0;
        int score = 0;

        for (int s = 0; s < ctrl.AiPickHistory.Count; s++)
        {
            ElementType aiCard = ctrl.AiPickHistory[s];

            // 이 AI 카드가 matchIndex 이전 매치에서 이미 소모됐는가?
            bool consumedBefore = false;
            for (int m = 0; m < matchIndex; m++)
            {
                if (m < ctrl.AiMatchHistory.Count && ctrl.AiMatchHistory[m] == aiCard)
                {
                    consumedBefore = true;
                    break;
                }
            }
            if (consumedBefore) continue;

            totalRemaining++;
            if (TypeChart.Beats(playerCard, aiCard)) score++;
            else if (TypeChart.Beats(aiCard, playerCard)) score--;
            // 같은 속성(Tie)이면 점수 변화 없음
        }

        return totalRemaining > 0 ? (float)score / totalRemaining : 0f;
    }

    // 신뢰도 가중치: 관측 수가 0 이면 0, BAYES_MIN_OBSERVATIONS 도달 시 1.0.
    // 초반 표본 부족 시 모델 영향력을 자동으로 깎는 역할.
    private float GetBayesConfidence()
    {
        return Mathf.Clamp01(bayesObservationCount / (float)BAYES_MIN_OBSERVATIONS);
    }

    // 라운드 사이에 모델을 중립값 쪽으로 부드럽게 감쇠 (라운드 2 이상부터).
    // observationCount 는 일부러 보존 → 시리즈 전체에 걸쳐 신뢰도는 계속 누적.
    public void DecayBayesianModelForNewRound()
    {
        if (PracticeSettings.Difficulty != PracticeSetupManager.AIDifficulty.Hard) return;
        if (SeriesState.TotalRounds <= 1) return;
        if (SeriesState.CurrentRound <= 1) return;  // 라운드 1: 아직 감쇠할 데이터 없음

        playerHonesty = Mathf.Lerp(BAYES_INITIAL_HONESTY, playerHonesty, BAYES_ROUND_DECAY);
        playerAggression = Mathf.Lerp(BAYES_INITIAL_AGGRESSION, playerAggression, BAYES_ROUND_DECAY);

#if UNITY_EDITOR
        Debug.Log($"[Bayes] round-decay → honesty={playerHonesty:F2} aggr={playerAggression:F2} (round {SeriesState.CurrentRound})");
#endif
    }

    // 시리즈 첫 라운드 시작 시 모델을 사전확률로 완전 초기화.
    // 시리즈 재시작 (Practice 씬 재로드) 시에는 DraftController(및 DraftAI) 자체가 새로 생성되므로 필드 이니셜라이저가 같은 효과를 냄.
    public void ResetBayesianModel()
    {
        playerHonesty = BAYES_INITIAL_HONESTY;
        playerAggression = BAYES_INITIAL_AGGRESSION;
        bayesObservationCount = 0;
    }

    // ── 매치 카드 출전 선택 ──────────────────────────────────────────

    // 이번 매치에 낼 AI 카드(슬롯)를 선택.
    // Easy = 균등 무작위, Normal = 60% 전략 / 40% 무작위, Hard = best EV + 리저브 로직.
    // 반환값: aiPickHistory 의 슬롯 인덱스, 사용 가능한 슬롯이 없으면 -1 (방어용).
    // EV 계산은 Phase 1 의 ComputeMatchupEdge 를 그대로 재사용 — edge 와 EV 는 같은 [-1, +1] 값.
    public int ChooseAiMatchPick()
    {
        matchPickCandidates.Clear();
        for (int i = 0; i < ctrl.AiUsed.Length; i++)
        {
            if (!ctrl.AiUsed[i]) matchPickCandidates.Add(i);
        }

        if (matchPickCandidates.Count == 0) return -1;          // 방어용: 라운드 중에는 발생하면 안 되는 상태
        if (matchPickCandidates.Count == 1) return matchPickCandidates[0]; // 강제 단일 선택

        var difficulty = PracticeSettings.Difficulty;
        switch (difficulty)
        {
            case PracticeSetupManager.AIDifficulty.Hard:
                return ChooseHardMatchPick();

            case PracticeSetupManager.AIDifficulty.Normal:
                if (Random.value < NORMAL_STRATEGIC_RATIO)
                    return ChooseBestEvSlot(out _);
                return matchPickCandidates[Random.Range(0, matchPickCandidates.Count)];

            case PracticeSetupManager.AIDifficulty.Easy:
            default:
                return matchPickCandidates[Random.Range(0, matchPickCandidates.Count)];
        }
    }

    // Hard 난이도: best EV 를 고르되, 강력한 카드는 뒷판 큰 베팅용으로 리저브.
    // "라운드 점수에서 뒤처지지 않음" 판정은 (wallet + earnings) 로 — 라운드 결과 집계 방식과 동일
    // (L~1739: `playerFinal = playerWallet + playerEarnings`).
    // wallet 만 비교하면 "베팅으로 묶인 금액"과 "베팅으로 잃은 금액"이 구분되지 않아 의미가 흐려짐.
    private int ChooseHardMatchPick()
    {
        int bestSlot = ChooseBestEvSlot(out float bestEv);

        int remainingMatches = DraftController.TotalMatches - ctrl.CurrentMatchIndex;
        int aiRoundPoints = ctrl.AiWallet + ctrl.AiEarnings;
        int playerRoundPoints = ctrl.PlayerWallet + ctrl.PlayerEarnings;

        bool shouldReserve =
            bestEv >= HARD_RESERVE_EV_THRESHOLD
            && remainingMatches >= HARD_RESERVE_MIN_MATCHES_LEFT
            && aiRoundPoints >= playerRoundPoints;

        if (!shouldReserve) return bestSlot;

        int secondBestSlot = ChooseSecondBestEvSlot(bestSlot);
        return secondBestSlot >= 0 ? secondBestSlot : bestSlot;
    }

    // matchPickCandidates 를 훑어서 상대 남은 풀 대비 EV 가 가장 높은 슬롯 반환.
    // EV 는 Phase 1 의 ComputeMatchupEdge 그대로 사용 ([-1, +1] 범위, 상대 카드 0장이면 0).
    private int ChooseBestEvSlot(out float bestEv)
    {
        bestEv = float.MinValue;
        int bestSlot = matchPickCandidates[0];

        for (int c = 0; c < matchPickCandidates.Count; c++)
        {
            int candidateSlot = matchPickCandidates[c];
            float ev = ComputeMatchupEdge(ctrl.AiPickHistory[candidateSlot]);
            if (ev > bestEv)
            {
                bestEv = ev;
                bestSlot = candidateSlot;
            }
        }
        return bestSlot;
    }

    // excludeSlot 을 제외한 후보 중 EV 차순위 슬롯 반환. 대안이 없으면 -1.
    private int ChooseSecondBestEvSlot(int excludeSlot)
    {
        float secondBestEv = float.MinValue;
        int secondBestSlot = -1;

        for (int c = 0; c < matchPickCandidates.Count; c++)
        {
            int candidateSlot = matchPickCandidates[c];
            if (candidateSlot == excludeSlot) continue;

            float ev = ComputeMatchupEdge(ctrl.AiPickHistory[candidateSlot]);
            if (ev > secondBestEv)
            {
                secondBestEv = ev;
                secondBestSlot = candidateSlot;
            }
        }
        return secondBestSlot;
    }
}
