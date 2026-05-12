using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 카드 위에서 "짧은 클릭"과 "길게 누름(1초)"을 구분해 콜백으로 분기한다.
// - 짧게 클릭 → onClick (기존 카드 선택 동작)
// - 길게 1초 이상 누르고 있는 동안 → onLongPressStart (상성표 표시)
// - 마우스를 떼거나 카드 밖으로 나가면 → onLongPressEnd (상성표 닫기)
//
// 같은 GameObject에 Button이 있으면 그 Button.interactable이 false일 땐 모든 입력을 무시한다
// (= AI 턴 카드처럼 클릭 자체가 막혀있는 경우 길게 누름도 막힘).
public class CardPressHandler : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public float longPressDuration = 1f;

    // 외부(예: DraftController)에서 콜백을 주입한다.
    public Action onClick;
    public Action onLongPressStart;
    public Action onLongPressEnd;

    private Button gatingButton;          // 있을 때만 interactable 게이팅
    private bool pressing;
    private bool longPressFired;
    private float pressStartUnscaledTime;

    private void Awake()
    {
        gatingButton = GetComponent<Button>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (gatingButton != null && !gatingButton.interactable) return;

        pressing = true;
        longPressFired = false;
        pressStartUnscaledTime = Time.unscaledTime;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        Release(pointerStillOverTarget: true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // 카드 영역을 벗어나면 길게 누름은 종료, 짧은 클릭은 발동하지 않음
        Release(pointerStillOverTarget: false);
    }

    private void Update()
    {
        if (!pressing || longPressFired) return;
        if (Time.unscaledTime - pressStartUnscaledTime >= longPressDuration)
        {
            longPressFired = true;
            onLongPressStart?.Invoke();
        }
    }

    private void Release(bool pointerStillOverTarget)
    {
        if (!pressing) return;
        bool wasLong = longPressFired;
        pressing = false;
        longPressFired = false;
        if (wasLong)
        {
            onLongPressEnd?.Invoke();
        }
        else if (pointerStillOverTarget)
        {
            // 1초 미만 짧은 클릭만 카드 선택으로 인정
            onClick?.Invoke();
        }
    }
}
