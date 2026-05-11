using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// 드래프트 후 "순서 변경" 단계에서 카드 슬롯을 드래그-앤-드롭으로 교환하는 핸들러.
// 드래그 중에는 카드 RectTransform이 커서를 따라가고, EndDrag에서 원위치로 복귀한다.
// 드롭 지점에 다른 슬롯의 핸들러가 감지되면 onSwap 콜백을 호출 → 데이터 교환은 컨트롤러가 처리.
public class OrderSlotDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public int slotIndex;
    public Action<int, int> onSwap; // (fromIndex, toIndex)

    private RectTransform rt;
    private CanvasGroup canvasGroup;
    private Canvas rootCanvas;
    private Vector2 originalAnchoredPosition;
    private bool dragEnabled = true;

    // 외부(컨트롤러)에서 30초 만료 후 드래그를 막을 때 사용
    public void SetDragEnabled(bool value) => dragEnabled = value;

    private void Awake()
    {
        rt = (RectTransform)transform;
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        var parentCanvas = GetComponentInParent<Canvas>();
        rootCanvas = parentCanvas != null ? parentCanvas.rootCanvas : null;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!dragEnabled) return;
        originalAnchoredPosition = rt.anchoredPosition;
        canvasGroup.blocksRaycasts = false; // 드롭 시 raycast가 자기 자신을 지나치도록
        transform.SetAsLastSibling();        // 다른 슬롯 위로 보이도록
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragEnabled) return;
        float scale = rootCanvas != null ? rootCanvas.scaleFactor : 1f;
        if (scale <= 0f) scale = 1f;
        rt.anchoredPosition += eventData.delta / scale;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.blocksRaycasts = true;
        rt.anchoredPosition = originalAnchoredPosition; // 항상 원위치 복귀 (데이터 교환은 콜백이 처리)
        if (!dragEnabled) return;

        // 드롭 지점 아래의 다른 슬롯 찾기
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        foreach (var r in results)
        {
            var other = r.gameObject.GetComponentInParent<OrderSlotDragHandler>();
            if (other != null && other != this)
            {
                onSwap?.Invoke(slotIndex, other.slotIndex);
                return;
            }
        }
    }
}
