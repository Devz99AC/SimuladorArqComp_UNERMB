using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UISoundController : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    [Header("Sonidos")]
    public AudioClip hoverSound;
    public AudioClip clickSound;

    [Header("Configuración")]
    [Range(0f, 1f)] public float hoverVolume = 0.5f;
    [Range(0f, 1f)] public float clickVolume = 1.0f;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_button != null && _button.interactable && hoverSound != null)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySound(hoverSound, hoverVolume);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_button != null && _button.interactable && clickSound != null)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySound(clickSound, clickVolume);
        }
    }
}
