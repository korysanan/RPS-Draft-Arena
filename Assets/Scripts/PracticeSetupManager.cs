using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// PracticeMode 씬(연습 설정 화면)의 매니저.
// 난이도/RPS/경기방식 토글 그룹을 관리하고, 확인 버튼 → 팝업 → 게임 시작/취소 흐름을 담당한다.
public class PracticeSetupManager : MonoBehaviour
{
    // 선택 가능한 옵션 enum (None은 "미선택" 상태를 표현하기 위한 기본값)
    public enum AIDifficulty { None, Easy, Normal, Hard }
    public enum RPSType { None, RPS3, RPS5, RPS7 }
    public enum MatchFormat { None, BO1, BO3, BO5, BO7 }

    // 인스펙터에서 (enum 값 ↔ Toggle UI) 쌍으로 묶기 위한 옵션 클래스들
    [Serializable]
    public class DifficultyOption
    {
        public AIDifficulty value;
        public Toggle toggle;
    }

    [Serializable]
    public class RPSOption
    {
        public RPSType value;
        public Toggle toggle;
    }

    [Serializable]
    public class MatchFormatOption
    {
        public MatchFormat value;
        public Toggle toggle;
    }

    // 각 그룹의 토글 리스트 (인스펙터에서 와이어링)
    [SerializeField] private List<DifficultyOption> difficultyOptions = new List<DifficultyOption>();
    [SerializeField] private List<RPSOption> rpsOptions = new List<RPSOption>();
    [SerializeField] private List<MatchFormatOption> matchFormatOptions = new List<MatchFormatOption>();

    [SerializeField] private Button confirmButton;             // 확인 버튼 (3그룹 모두 선택돼야 활성화)
    [SerializeField] private TMP_Text confirmResultLabel;      // 팝업 안에서 선택 내용을 보여주는 텍스트
    [SerializeField] private GameObject popupPanel;            // 게임 시작/취소 팝업
    [SerializeField] private string practiceSceneName = "Practice"; // 게임 시작 시 로드할 씬 이름

    // 현재 선택된 값 (없으면 None)
    private AIDifficulty selectedDifficulty = AIDifficulty.None;
    private RPSType selectedRPS = RPSType.None;
    private MatchFormat selectedFormat = MatchFormat.None;

    private void Start()
    {
        // 각 토글의 onValueChanged 이벤트에 핸들러를 연결.
        // captured 변수로 클로저에 현재 옵션을 캡처해서, 어떤 토글이 바뀌었는지 식별.
        foreach (var opt in difficultyOptions)
        {
            var captured = opt;
            captured.toggle.onValueChanged.AddListener(isOn => OnDifficultyChanged(captured, isOn));
        }
        foreach (var opt in rpsOptions)
        {
            var captured = opt;
            captured.toggle.onValueChanged.AddListener(isOn => OnRPSChanged(captured, isOn));
        }
        foreach (var opt in matchFormatOptions)
        {
            var captured = opt;
            captured.toggle.onValueChanged.AddListener(isOn => OnFormatChanged(captured, isOn));
        }

        // 확인 버튼 클릭 핸들러
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmClicked);

        // 시작 시 팝업은 숨겨둠
        if (popupPanel != null)
            popupPanel.SetActive(false);

        UpdateConfirmButton();
    }

    // 난이도 토글 이벤트: 켜졌으면 그 값을 선택값으로 두고 다른 토글은 끔(단일 선택).
    // 꺼진 이벤트일 때는 현재 선택값과 같으면 None으로 리셋.
    private void OnDifficultyChanged(DifficultyOption changed, bool isOn)
    {
        if (!isOn)
        {
            if (selectedDifficulty == changed.value)
                selectedDifficulty = AIDifficulty.None;
            UpdateConfirmButton();
            return;
        }
        selectedDifficulty = changed.value;
        // 같은 그룹의 다른 토글들을 꺼서 단일 선택을 강제.
        // isOn = false 로 셋하면 onValueChanged가 다시 발생 → ToggleVisualSwap도 갱신됨.
        foreach (var opt in difficultyOptions)
            if (opt != changed && opt.toggle.isOn) opt.toggle.isOn = false;
        UpdateConfirmButton();
    }

    // RPS 토글 이벤트 (위와 동일 패턴)
    private void OnRPSChanged(RPSOption changed, bool isOn)
    {
        if (!isOn)
        {
            if (selectedRPS == changed.value)
                selectedRPS = RPSType.None;
            UpdateConfirmButton();
            return;
        }
        selectedRPS = changed.value;
        foreach (var opt in rpsOptions)
            if (opt != changed && opt.toggle.isOn) opt.toggle.isOn = false;
        UpdateConfirmButton();
    }

    // 경기 방식 토글 이벤트 (위와 동일 패턴)
    private void OnFormatChanged(MatchFormatOption changed, bool isOn)
    {
        if (!isOn)
        {
            if (selectedFormat == changed.value)
                selectedFormat = MatchFormat.None;
            UpdateConfirmButton();
            return;
        }
        selectedFormat = changed.value;
        foreach (var opt in matchFormatOptions)
            if (opt != changed && opt.toggle.isOn) opt.toggle.isOn = false;
        UpdateConfirmButton();
    }

    // 3그룹이 전부 선택돼야만 확인 버튼을 누를 수 있게 함.
    private void UpdateConfirmButton()
    {
        if (confirmButton == null) return;
        confirmButton.interactable =
            selectedDifficulty != AIDifficulty.None &&
            selectedRPS != RPSType.None &&
            selectedFormat != MatchFormat.None;
    }

    // 확인 버튼 클릭: 선택 내용을 팝업 라벨에 출력 + 팝업 활성화
    public void OnConfirmClicked()
    {
        if (confirmResultLabel != null)
        {
            confirmResultLabel.text =
                $"난이도: {DifficultyKor(selectedDifficulty)}\n종류: {RPSKor(selectedRPS)}\n경기 방식: {FormatKor(selectedFormat)}";
        }
        if (popupPanel != null)
            popupPanel.SetActive(true);
    }

    // 팝업의 "게임 시작" 버튼: 선택값을 정적 PracticeSettings에 보관 후 Practice 씬 로드
    // (씬이 바뀌어도 static 필드는 유지되므로 Practice 씬에서 읽을 수 있음)
    public void OnStartGameClicked()
    {
        PracticeSettings.Difficulty = selectedDifficulty;
        PracticeSettings.Rps = selectedRPS;
        PracticeSettings.Format = selectedFormat;
        SceneManager.LoadScene(practiceSceneName);
    }

    // 팝업의 "취소" 버튼: 팝업만 닫고 설정 화면으로 복귀
    public void OnCancelClicked()
    {
        if (popupPanel != null)
            popupPanel.SetActive(false);
    }

    // enum → 표시 문자열 변환 헬퍼들 (팝업에 보여줄 한글 라벨)
    private static string DifficultyKor(AIDifficulty d) => d switch
    {
        AIDifficulty.Easy => "Easy",
        AIDifficulty.Normal => "Normal",
        AIDifficulty.Hard => "Hard",
        _ => "-"
    };

    private static string RPSKor(RPSType r) => r switch
    {
        RPSType.RPS3 => "RPS-3",
        RPSType.RPS5 => "RPS-5",
        RPSType.RPS7 => "RPS-7",
        _ => "-"
    };

    private static string FormatKor(MatchFormat f) => f switch
    {
        MatchFormat.BO1 => "단판",
        MatchFormat.BO3 => "3판 2선",
        MatchFormat.BO5 => "5판 3선",
        MatchFormat.BO7 => "7판 4선",
        _ => "-"
    };
}

// 씬 간 데이터 전달용 정적 클래스.
// PracticeMode → Practice 씬 전환 시 사용자가 고른 설정값을 보관/조회.
public static class PracticeSettings
{
    public static PracticeSetupManager.AIDifficulty Difficulty = PracticeSetupManager.AIDifficulty.None;
    public static PracticeSetupManager.RPSType Rps = PracticeSetupManager.RPSType.None;
    public static PracticeSetupManager.MatchFormat Format = PracticeSetupManager.MatchFormat.None;
}
