using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Toggle))]
public class ToggleVisualSwap : MonoBehaviour
{
    [SerializeField] private GameObject offGraphic;
    [SerializeField] private GameObject onGraphic;

    private void Awake()
    {
        var toggle = GetComponent<Toggle>();
        Apply(toggle.isOn);
        toggle.onValueChanged.AddListener(Apply);
    }

    private void Apply(bool isOn)
    {
        if (offGraphic != null) offGraphic.SetActive(!isOn);
        if (onGraphic != null) onGraphic.SetActive(isOn);
    }
}
