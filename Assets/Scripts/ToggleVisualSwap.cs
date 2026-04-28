using UnityEngine;
using UnityEngine.UI;

// 토글의 ON/OFF 상태에 따라 두 GameObject 중 하나만 활성화시키는 헬퍼.
// (체크 표시를 덧입히는 기본 Toggle 동작과 달리, 두 그래픽을 완전히 교체하는 형태로 보여주고 싶을 때 사용.)
// AI 난이도 / RPS 종류 / 경기 방식 버튼이 이 컴포넌트를 사용한다.
[RequireComponent(typeof(Toggle))]
public class ToggleVisualSwap : MonoBehaviour
{
    // 토글 OFF일 때 보여줄 GameObject (예: Basic_Button)
    [SerializeField] private GameObject offGraphic;
    // 토글 ON일 때 보여줄 GameObject (예: Check_Button)
    [SerializeField] private GameObject onGraphic;

    private void Awake()
    {
        var toggle = GetComponent<Toggle>();
        // 시작할 때 현재 isOn 상태에 맞춰 즉시 표시 갱신
        Apply(toggle.isOn);
        // 이후 토글 변경 시마다 자동 갱신되도록 리스너 등록
        toggle.onValueChanged.AddListener(Apply);
    }

    // ON이면 onGraphic만, OFF면 offGraphic만 활성화 (한쪽만 보이도록 배타적 처리)
    private void Apply(bool isOn)
    {
        if (offGraphic != null) offGraphic.SetActive(!isOn);
        if (onGraphic != null) onGraphic.SetActive(isOn);
    }
}
