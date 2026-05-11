// 시리즈(다판제) 상태를 라운드 간 공유하기 위한 정적 저장소.
// 같은 씬 안에서 그룹 활성화만 토글하며 라운드를 반복하므로 인스턴스 필드가 아닌 static을 사용.
// 새 매치(씬 로드)에서 PracticeCardController.Start가 Reset을 호출한다.
public static class SeriesState
{
    public static int PlayerScore;
    public static int AiScore;
    public static int CurrentRound;
    public static int RoundsToWin;     // 시리즈 승리에 필요한 점수: BO1→1, BO3→2, BO5→3, BO7→4
    public static int TotalRounds;     // BO_N의 N (표시용)

    // 직전 라운드 결과 — 다음 라운드 진입 시 어느 흐름을 쓸지 결정한다.
    //   LastRoundTied=true  : 결판전 카드 뒤집기 진행 → 승자 +1점, 결판 패자가 다음 라운드 선픽/후픽 선택
    //   LastRoundTied=false : 결판전 없이 LastRoundPlayerWon에 따라 직전 라운드 패자가 선픽/후픽 선택
    public static bool LastRoundTied;
    public static bool LastRoundPlayerWon;

    // 현재 카드 뒤집기가 결판전 진행 중인지 (결판전이면 OnResultConfirmed에서 점수 가산 + 패자가 선택)
    public static bool TiebreakerInProgress;

    public static void Reset(PracticeSetupManager.MatchFormat format)
    {
        PlayerScore = 0;
        AiScore = 0;
        CurrentRound = 1;
        TotalRounds = format switch
        {
            PracticeSetupManager.MatchFormat.BO1 => 1,
            PracticeSetupManager.MatchFormat.BO3 => 3,
            PracticeSetupManager.MatchFormat.BO5 => 5,
            PracticeSetupManager.MatchFormat.BO7 => 7,
            _ => 1,
        };
        // ceil(N/2) — 시리즈 승리에 필요한 라운드 수
        RoundsToWin = (TotalRounds / 2) + 1;
        LastRoundTied = false;
        LastRoundPlayerWon = false;
        TiebreakerInProgress = false;
    }

    public static bool IsSeriesOver => PlayerScore >= RoundsToWin || AiScore >= RoundsToWin;
    public static bool PlayerWonSeries => PlayerScore >= RoundsToWin;
}
