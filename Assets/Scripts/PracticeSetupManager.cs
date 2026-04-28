using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class PracticeSetupManager : MonoBehaviour
{
    public enum AIDifficulty { None, Easy, Normal, Hard }
    public enum RPSType { None, RPS3, RPS5, RPS7 }
    public enum MatchFormat { None, BO1, BO3, BO5, BO7 }

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

    [SerializeField] private List<DifficultyOption> difficultyOptions = new List<DifficultyOption>();
    [SerializeField] private List<RPSOption> rpsOptions = new List<RPSOption>();
    [SerializeField] private List<MatchFormatOption> matchFormatOptions = new List<MatchFormatOption>();

    [SerializeField] private Button confirmButton;
    [SerializeField] private TMP_Text confirmResultLabel;
    [SerializeField] private GameObject popupPanel;
    [SerializeField] private string practiceSceneName = "Practice";

    private AIDifficulty selectedDifficulty = AIDifficulty.None;
    private RPSType selectedRPS = RPSType.None;
    private MatchFormat selectedFormat = MatchFormat.None;

    private void Start()
    {
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

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmClicked);

        if (popupPanel != null)
            popupPanel.SetActive(false);

        UpdateConfirmButton();
    }

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
        foreach (var opt in difficultyOptions)
            if (opt != changed && opt.toggle.isOn) opt.toggle.isOn = false;
        UpdateConfirmButton();
    }

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

    private void UpdateConfirmButton()
    {
        if (confirmButton == null) return;
        confirmButton.interactable =
            selectedDifficulty != AIDifficulty.None &&
            selectedRPS != RPSType.None &&
            selectedFormat != MatchFormat.None;
    }

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

    public void OnStartGameClicked()
    {
        PracticeSettings.Difficulty = selectedDifficulty;
        PracticeSettings.Rps = selectedRPS;
        PracticeSettings.Format = selectedFormat;
        SceneManager.LoadScene(practiceSceneName);
    }

    public void OnCancelClicked()
    {
        if (popupPanel != null)
            popupPanel.SetActive(false);
    }

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

public static class PracticeSettings
{
    public static PracticeSetupManager.AIDifficulty Difficulty = PracticeSetupManager.AIDifficulty.None;
    public static PracticeSetupManager.RPSType Rps = PracticeSetupManager.RPSType.None;
    public static PracticeSetupManager.MatchFormat Format = PracticeSetupManager.MatchFormat.None;
}
