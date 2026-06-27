# RPS Draft Arena
<img width="1824" height="1018" alt="Image" src="https://github.com/user-attachments/assets/2937b64a-28f8-4916-9851-cc0251abc25a" />

> 가위바위보의 상성 관계를 **드래프트(밴픽) + 베팅 메타**로 확장한 1:1 카드 전략 게임
>
> 상대의 행동을 관측해 성향을 추정하고, 게임이 진행될수록 똑똑해지는 **학습형 AI**를 직접 설계했습니다.

---

## 목차

- [한눈에 보기](#한눈에-보기)
- [게임 소개](#게임-소개)
- [게임 플레이](#게임-플레이)
- [7속성 상성 시스템](#7속성-상성-시스템)
- [PART 1 — 학습하는 AI 설계](#part-1--학습하는-ai-설계)
- [PART 2 — Advisor 패턴 아키텍처](#part-2--advisor-패턴-아키텍처)
- [프로젝트 구조](#프로젝트-구조)
- [씬 흐름](#씬-흐름)
- [Download](#Download)
- [자료](#자료)
- [전체 플레이 영상](#전체-플레이-영상)

---

## 한눈에 보기

| 구분 | 내용 |
|---|---|
| **엔진** | Unity 6 (6000.3.11f1) / C# |
| **장르** | 1:1 카드 전략 (드래프트 + 포인트 베팅) |
| **핵심 스코프** | AI 의사결정 설계 & Advisor 패턴 아키텍처 |
| **AI 난이도** | Easy / Normal / Hard (각각 다른 의사결정 모델) |
| **대전 방식** | RPS-3 / 5 / 7 속성 × BO1 ~ BO7 시리즈 |

> 가위바위보라는 가장 단순한 규칙 위에, **드래프트로 패를 짜고 / 베팅으로 심리전을 거는** 전략 레이어를 얹었습니다.
> 그리고 그 심리전의 상대가 될 **"학습하는 AI"** 를 만드는 것이 이 프로젝트의 핵심 목표였습니다.

---

## 게임 소개

7개 속성(**Fire · Water · Nature · Wind · Electric · Ice · Magic**)이 각각 **3승 3패**의 완전 대칭 상성을 가집니다.
플레이어와 AI는 한정된 카드 풀에서 서로 번갈아 카드를 뽑아(드래프트) 자신의 패를 구성하고,
그 패로 5번의 1:1 매치를 치르며 매 매치마다 보유 포인트를 베팅합니다.

단순히 "강한 카드를 내는" 게임이 아니라,

- **드래프트** — 어떤 카드를 가져가고 어떤 카드를 상대에게 넘길 것인가
- **베팅** — 유리한 매치에 얼마나 자신감을 보일 것인가, 불리할 때 블러프를 칠 것인가
- **시리즈** — 한 판이 아니라 BO3/5/7 전체의 점수 상황에 맞춘 위험 관리

이 세 층위의 의사결정이 맞물리는 두뇌 싸움이 핵심입니다.

---

## 게임 플레이

### 라운드 흐름

```
선/후픽 결정  →  스네이크 드래프트(각 7장)  →  패 확인(30초)
      →  5번의 1:1 매치 (카드 + 포인트 베팅)  →  라운드 결과  →  시리즈 진행 (BO1~7)
```

1. **선/후픽 결정** — 카드 뒤집기 미니게임으로 누가 먼저 뽑을지 결정 (동점 시 결판전)
<img width="400" height="225" alt="Image" src="https://github.com/user-attachments/assets/3d09f43a-a418-4e9b-a3f2-402fa96a5a90" />

2. **스네이크 드래프트** — A→B→B→A 순서로 양측이 각 7장의 카드를 픽
<img width="400" height="225" alt="Image" src="https://github.com/user-attachments/assets/98aa544d-08c3-448b-a788-667310b0704d" />

3. **패 확인** — 30초간 내 패와 상대가 가져간 카드를 검토
<img width="400" height="225" alt="Image" src="https://github.com/user-attachments/assets/f6e043a7-e744-41fd-85c5-1248151adbfa" />

4. **5매치 진행** — 매 매치마다 카드 1장 + 포인트를 베팅, 상성으로 승패 판정 후 포인트 정산
<img width="400" height="225" alt="Image" src="https://github.com/user-attachments/assets/73c85fdf-9156-45bc-bd4e-12951ba10afb" />

5. **시리즈 진행** — 라운드 승자에게 +1점, 먼저 `ceil(N/2)` 점에 도달하면 시리즈 승리
<img width="400" height="225" alt="Image" src="https://github.com/user-attachments/assets/fd216e0f-50fa-4c85-abc3-bcec75a8e6d2" />

### 매치 / 베팅 규칙

| 항목 | 값 |
|---|---|
| 라운드당 매치 수 | 5 |
| 측별 픽 수 | 7 |
| 시작 포인트 | 100 |
| 매치당 최소 베팅 | 5 |
| 시리즈 형식 | BO1 / BO3 / BO5 / BO7 (`RoundsToWin = N/2 + 1`) |

---

## 7속성 상성 시스템

RPS-3 / 5 / 7은 모두 **같은 N=7 상성표의 부분 집합**입니다. 각 속성은 정확히 3승 3패로 완전 대칭입니다.

<img width="1536" height="1024" alt="Image" src="https://github.com/user-attachments/assets/443579ab-a037-43e7-9cc4-a20020ca9138" />

| 속성 | 이김 (→) | 짐 |
|---|---|---|
| 🔥 Fire | Nature, Electric, Ice | Water, Wind, Magic |
| 💧 Water | Fire, Wind, Magic | Nature, Electric, Ice |
| 🌿 Nature | Water, Wind, Ice | Fire, Electric, Magic |
| 🌪 Wind | Fire, Electric, Magic | Water, Nature, Ice |
| ⚡ Electric | Water, Nature, Ice | Fire, Wind, Magic |
| ❄️ Ice | Water, Wind, Magic | Fire, Nature, Electric |
| ✨ Magic | Fire, Nature, Electric | Water, Wind, Ice |

- **RPS-3**: Fire · Water · Nature (Water→Fire→Nature→Water 3원 순환)
- **RPS-5**: + Wind · Electric
- **RPS-7**: + Ice · Magic

> 같은 속성끼리는 무승부(Tie). 상성표는 [`TypeChart.cs`](Assets/Scripts/TypeChart.cs)에 단일 진실 원천으로 정의되어 있으며 양방향 일관성이 검증되어 있습니다.

---

## PART 1 — 학습하는 AI 설계

📄 [`Assets/Scripts/DraftAI.cs`](Assets/Scripts/DraftAI.cs)

### 문제의식

게임 AI의 가장 흔한 구현은 `if (Random.value < 0.5f)` 같은 단순 확률입니다.
하지만 **베팅이 핵심인 이 게임에서 그런 AI는 "사람과 두뇌 싸움을 하는 느낌"을 주지 못합니다.**

> 🎯 **목표** — 상대(플레이어)의 행동을 관측해 그 사람의 성향을 추정하고, 게임이 진행될수록 더 똑똑해지는 AI를 만든다.

| 난이도 | 의사결정 모델 |
|---|---|
| **Easy** | 완전 무작위 선택 |
| **Normal** | 기대값(EV) 기반. 단, 50% 확률로 의도적 실수를 넣어 사람 같은 변동성을 부여 |
| **Hard** | EV + 시리즈 메타 전략 + **베이지안 상대 모델** + 블러프(불리할 때 10% 확률로 큰 베팅) |

<br>

### ① 기대값(EV) 기반 매치업 평가

단일 카드끼리 비교하는 게 아니라, AI 카드가 상대의 **"남은 카드 풀 전체"** 를 상대로 얼마나 유리한지를 `[-1, +1]`로 정규화합니다.

```csharp
// 매치업 edge ([-1, +1]). +1 = 상대 남은 카드를 전부 이김, -1 = 전부 짐.
private float ComputeMatchupEdge(ElementType aiCard)
{
    int score = 0;
    int totalRemaining = 0;
    for (int i = 0; i < ctrl.PlayerPickHistory.Count; i++)
    {
        if (ctrl.PlayerUsed[i]) continue;           // 이미 쓴 카드는 제외
        var opponentCard = ctrl.PlayerPickHistory[i];
        if (TypeChart.Beats(aiCard, opponentCard)) score++;
        else if (TypeChart.Beats(opponentCard, aiCard)) score--;
        totalRemaining++;
    }
    if (totalRemaining == 0) return 0f;
    return (float)score / totalRemaining;
}
```

> **설계 의도** — 매치가 진행되며 상대가 카드를 소모하면 `PlayerUsed`로 제외하므로 EV가 매 매치마다 실시간 갱신됩니다. AI는 *"지금 시점에 상대가 들고 있을 수 있는 카드"* 만 고려해 판단합니다.

<br>

### ② EV에 비례하는 베팅 사이징

유리하면 크게, 불리하면 작게 거는 직관을 수식으로 구현했습니다.

```csharp
int baseBet = ctrl.AiWallet / remainingMatches;                    // 남은 매치에 균등 분배한 기준액
float weight = (difficulty == Difficulty.Hard) ? 0.85f : 0.6f;     // edge가 베팅을 흔드는 강도
float desiredBet = baseBet * (1f + edge * weight);
```

- `edge = +1` (완벽 유리) & Hard → 기준액의 약 **1.85배** 베팅
- `edge = -1` (완벽 불리) → 기준액의 약 **0.15배**로 축소

마지막엔 5포인트 단위로 스냅하고, *"남은 매치 수 × 최소 베팅"* 만큼 예비 자금을 남기도록 clamp 합니다.
한 매치에 전부 걸어 다음 매치를 못 하는 상황을 막는 안전장치입니다.

```csharp
int max = Mathf.Max(MinBet, ctrl.AiWallet - remainingAfter * MinBet);
int snapped = Mathf.RoundToInt(desiredBet / 5) * 5;
return Mathf.Clamp(snapped, MinBet, max);
```

<br>

### ③ 베이지안 상대 모델 ★ 핵심

포커의 블러프 탐지와 같은 발상으로, 플레이어의 베팅 로그로부터 두 가지 잠재 성향을 역추정합니다.

- **정직도(honesty)** — 강한 카드에 크게 / 약한 카드에 작게 베팅하는가? (예측 가능한 상대)
- **공격성(aggression)** — 전반적으로 얼마나 크게 베팅하는가?

핵심 아이디어는 **"베팅 방향과 카드 강함의 부호 일치"** 입니다.

| 카드 강함 | 베팅 | 해석 |
|---|---|---|
| 강함 (+) | 크게 (+) | **정직** — 강한 패에 자신 있게 |
| 약함 (−) | 작게 (−) | **정직** — 약한 패에 소극적 |
| 강함 (+) | 작게 (−) | **기만적** — 슬로우 플레이 |
| 약함 (−) | 크게 (+) | **블러프** |

```csharp
public void UpdateBayesianModel(int matchIndex)
{
    // Hard + 다판제에서만 학습 (단판제는 표본 부족)
    if (difficulty != Difficulty.Hard || SeriesState.TotalRounds <= 1) return;

    ElementType playerCard = ctrl.PlayerMatchHistory[matchIndex];
    int playerBet = ctrl.PlayerBetHistory[matchIndex];

    // 1) 그 시점 플레이어 카드의 강함을 사후 재구성
    float playerEdge = EstimateHistoricalPlayerEdge(matchIndex, playerCard);

    // 2) 베팅이 중립 기준선(20pt) 대비 얼마나 큰지 정규화 → [-1, +1]
    float betDeviation = Mathf.Clamp((playerBet - 20) / 20f, -1f, 1f);

    // 3) 정직도 신호: 부호가 같으면 +, 다르면 -
    float honestySignal = (Mathf.Sign(playerEdge) == Mathf.Sign(betDeviation))
        ?  Mathf.Abs(playerEdge * betDeviation)
        : -Mathf.Abs(playerEdge * betDeviation);
    honestySignal = (honestySignal + 1f) * 0.5f;                   // [-1,+1] → [0,1]

    // 4) 지수이동평균으로 점진 갱신 (한 판에 급변하지 않게)
    playerHonesty = Mathf.Lerp(playerHonesty, honestySignal, 0.3f);

    // 5) 공격성: 시작 자금 대비 베팅 비율의 이동평균
    float observedAggression = playerBet / (float)StartingPoints;
    playerAggression = Mathf.Lerp(playerAggression, observedAggression, 0.3f);

    bayesObservationCount++;
}
```

학습한 모델은 다시 베팅 결정에 반영됩니다.

```csharp
// 공격적인 상대면 AI도 베팅을 키움
float aggressionShift = (playerAggression - 0.5f) * 0.3f * bayesConfidence;
desiredBet *= (1f + aggressionShift);

// 유리한 매치업일 때만 정직도 반영:
//   정직한 상대 = 예측 가능 → 우위에서 더 크게 누름
//   기만적 상대 = 블러프 위험 → 우위에서도 살짝 헷지
if (edge > 0f)
{
    float honestyAdjust = (playerHonesty - 0.5f) * 0.2f * bayesConfidence;
    desiredBet *= (1f + honestyAdjust);
}
```

> **사후 재구성에 관하여** — `EstimateHistoricalPlayerEdge`는 모델이 매치 종료 *후* 갱신되기 때문에 필요합니다. 카드가 이미 소모된 뒤라 "그 매치 시점에 그 카드가 얼마나 강했는지"를 거꾸로 계산합니다.

<br>

### ④ 신뢰도 가중 (초반 과적합 방지) ★

베이지안 모델에서 가장 중요한 디테일입니다. 관측이 1~2개뿐일 때 섣불리 판단하면 안 됩니다.

```csharp
// 관측 0회 → 0, 3회 도달 → 1.0
private float GetBayesConfidence()
{
    return Mathf.Clamp01(bayesObservationCount / 3f);
}
```

이 `bayesConfidence`가 위의 모든 조정 항에 곱해집니다. 데이터가 부족한 초반에는 모델 영향력이 자동으로 0에 가깝게 깎이고, 표본이 쌓일수록 점점 강해집니다.
**"관측이 부족하면 함부로 믿지 않는다"** 는 의사결정 원칙을 코드로 표현한 부분입니다.

<br>

### ⑤ 시리즈 단위 메타 전략 + 라운드 간 감쇠

한 판이 아니라 BO3/5/7 시리즈 전체의 점수 상황에 따라 위험 성향을 바꿉니다.

```csharp
private float GetSeriesMultiplier()
{
    // 어느 쪽이든 매치포인트 → 최대 공격
    if (aiAtMatchPoint || playerAtMatchPoint) return 1.4f;

    int diff = SeriesState.AiScore - SeriesState.PlayerScore;
    if (diff < 0) return 1.25f;   // 뒤지면 공격적
    if (diff > 0) return 0.8f;    // 앞서면 보수적
    return 1f;                    // 동점이면 중립
}
```

또한 라운드가 바뀌면 학습한 모델을 중립값 쪽으로 부드럽게 감쇠시킵니다. 사람의 전략도 라운드마다 조금씩 바뀌기 때문에, **과거 학습을 일부 잊되 신뢰도(관측 수)는 보존**합니다.

```csharp
public void DecayBayesianModelForNewRound()
{
    playerHonesty    = Mathf.Lerp(0.5f, playerHonesty, 0.7f);     // 30%만 중립으로
    playerAggression = Mathf.Lerp(0.5f, playerAggression, 0.7f);
    // observationCount는 일부러 유지 → 신뢰도는 시리즈 내내 누적
}
```

---

## PART 2 — Advisor 패턴 아키텍처

📄 [`DraftAI.cs`](Assets/Scripts/DraftAI.cs) ↔ [`DraftController.cs`](Assets/Scripts/DraftController.cs)

### 문제의식

게임 로직 컨트롤러(`DraftController`)는 UI 생성·애니메이션·베팅 정산·팝업 관리까지 떠안아 비대해졌습니다(약 2,400줄). 여기에 AI 의사결정까지 섞이면:

- AI 로직과 게임 진행 로직이 엉켜 테스트·수정이 어려움
- AI가 실수로 게임 상태(지갑, 픽 이력 등)를 직접 변경해버리는 버그 위험

그래서 AI 의사결정(약 546줄)을 별도 클래스로 추출하되, **단순 분리가 아니라 "안전하게" 분리**하는 것을 목표로 했습니다.

### 해결 — Advisor(자문) 패턴

`DraftAI`는 `MonoBehaviour`가 아닌 **순수 C# 클래스**이며 게임 상태를 소유하지 않습니다. 컨트롤러를 역참조(`ctrl`)로 들고 있으면서 상태를 **읽고, 결정값만 반환**합니다. 실제 상태 적용(지갑 차감, 사용 표시 등)은 전부 컨트롤러가 합니다.

```csharp
public class DraftAI
{
    private readonly DraftController ctrl;                 // 역참조 (읽기 전용으로만 사용)
    public DraftAI(DraftController controller) { ctrl = controller; }

    // 결정만 반환하는 공개 API — 상태는 절대 건드리지 않음
    public ElementType ChooseAiPick();        // 드래프트에서 뭘 뽑을지
    public int         ChooseAiMatchPick();   // 이번 매치에 뭘 낼지
    public int         DecideAiBet(int slot); // 얼마를 걸지
    public void        UpdateBayesianModel(int idx); // 상대 모델 학습
}
```

```
┌─────────────────────────┐   읽기 (IReadOnlyList)   ┌────────────────────┐
│     DraftController      │ ──────────────────────▶ │      DraftAI       │
│   상태 소유 · 적용 책임   │ ◀────────────────────── │  advisor · 읽기전용 │
└─────────────────────────┘   결정값 반환 (int 등)    └────────────────────┘

  단방향 데이터 흐름:  상태 → AI → 결정 → 컨트롤러가 적용
```

### 타입 시스템으로 강제한 읽기 전용 캡슐화

*"AI는 읽기만 한다"* 를 주석이나 규칙이 아니라 **컴파일러 수준에서 강제**했습니다. 내부 컬렉션을 `IReadOnlyList<T>`로만 노출하고, 값 타입은 get-only 프로퍼티로 노출합니다.

```csharp
// DraftController가 AI에게 노출하는 읽기 전용 창구
internal IReadOnlyList<ElementType> PlayerPickHistory => playerPickHistory;
internal IReadOnlyList<ElementType> AiPickHistory     => aiPickHistory;
internal IReadOnlyList<int>         PlayerBetHistory   => playerBetHistory;
internal int AiWallet         => aiWallet;
internal int PlayerWallet     => playerWallet;
internal int CurrentMatchIndex => currentMatchIndex;
```

> AI 코드에서 `ctrl.PlayerPickHistory.Add(...)` 같은 변경을 시도하면 **컴파일 에러**가 납니다. *"AI가 게임 상태를 오염시키는 버그"* 가 발생할 수 없는 구조이며, 단방향 데이터 흐름이 타입으로 보장됩니다.

### 라이프사이클 설계 — 학습 상태의 지속

`DraftAI`가 매 라운드 새로 생성되면 베이지안 학습이 매번 초기화되어 무의미해집니다. 그래서 AI 인스턴스를 **시리즈 전체에서 단 한 번만 생성**합니다.

```csharp
public void StartDraft(bool playerGoesFirst, int rpsCount, ...)
{
    // 최초 1회만 생성 → 베이지안 학습이 라운드를 넘어 누적됨
    if (ai == null) ai = new DraftAI(this);

    // 라운드 시작 시: 첫 라운드는 완전 초기화, 이후 라운드는 감쇠
    if (SeriesState.CurrentRound <= 1) ai.ResetBayesianModel();
    else                              ai.DecayBayesianModelForNewRound();
    ...
}
```

> 상태를 유지해야 하는 **학습 모델**과 매 라운드 새로 그려야 하는 **UI**의 수명을 의도적으로 분리했습니다. 단순히 클래스를 나누는 것을 넘어 *상태의 수명까지 설계*한 결정입니다.

### 비용 0의 비활성화 — 방어적 early-return

베이지안 모델은 Hard + 다판제에서만 의미가 있습니다. 다른 모드에서 불필요한 연산이 돌지 않도록 학습 관련 메서드들이 진입 즉시 빠져나옵니다. 한 코드베이스로 4난이도를 모두 처리하면서 불필요한 분기 비용을 제거했습니다.

---

## 프로젝트 구조

```
Assets/
├── Scripts/
│   ├── DraftController.cs        # 코어 — 드래프트/매치/베팅/시리즈 진행 (~2,400줄)
│   ├── DraftAI.cs                # 학습형 AI advisor (순수 C#, ~546줄)
│   ├── PracticeCardController.cs # 드래프트 전 카드 뒤집기 레이스 + 픽 순서 (~1,177줄)
│   ├── TypeChart.cs              # 7속성 상성표 (단일 진실 원천)
│   ├── SeriesState.cs            # BO1~7 시리즈 상태(static)
│   ├── PracticeSetupManager.cs   # 난이도/RPS/형식 설정 화면
│   ├── PracticeSettings.cs       # 씬 간 설정 전달(static)
│   ├── UIClickAudio.cs           # 버튼 클릭 SFX 부트스트랩 싱글톤
│   └── ...                       # 씬 매니저, 카드 플립, 튜토리얼 페이저 등
├── Scenes/                       # MainTitle, Main_Home, Single_Play_*, Practice, Tutorial ...
├── Resources/                    # 빌드에 포함되는 사운드/상성 그래프/스프라이트
├── Font/                         # 온글잎 긍정 SDF (프로젝트 표준 UI 폰트)
├── Image/ · Sound/ · Settings/   # 아트 · 오디오 · URP 설정
```

> 대부분의 인게임 UI는 **프리팹 없이 런타임 코드로 생성**됩니다(`Assets/Prefabs/`는 비어 있음). 게임 전체 로직은 `Practice` 씬 하나에서 동작합니다.

---

## 씬 흐름

```
MainTitle  →  Main_Home  →  Single_Play_Home  →  PracticeMode(설정)  →  Practice(게임 본체)
```

- `PracticeMode`에서 **난이도(Easy/Normal/Hard) · 속성 수(RPS-3/5/7) · 시리즈 형식(BO1~7)** 을 선택
- 설정은 `PracticeSettings`(static)로 전달되고, `Practice` 씬에서 `PracticeCardController`가 게임 루프를 부트스트랩
- Tournament / League / Challenge / PVP 등은 향후 확장용 스텁


---
## Download

- 🪟 **[Windows](https://drive.google.com/file/d/1pIJM3AN8bkTv2EMW1nbrWP10WZqd9UvQ/view)**
- 🍎 **[macOS](https://drive.google.com/file/d/1F-6md9Qol0eWUANIkX_zsWnd6geswsFD/view)**
- 🤖 **[Android (APK)](https://drive.google.com/file/d/1aAH-YBgSOiwq7xHw9mYTQ4ULHTU-Ie3c/view)**

## 자료

- 📄 **[기술 설명서](https://drive.google.com/file/d/1AY2HMUB0QFLoK_HOa3fzCL3yhZexfqza/view)**

## 전체 플레이 영상

▶️ **[영상 보기](https://drive.google.com/file/d/1Cw4bsWG0E3lGZjqeKBqycPVkRhS5xaoJ/view)**
