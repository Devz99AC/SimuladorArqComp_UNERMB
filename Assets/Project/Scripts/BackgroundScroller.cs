using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class BackgroundScroller : MonoBehaviour
{
    [Header("Configuración de Movimiento")]
    [Tooltip("Velocidad horizontal (-1 a 1)")]
    [SerializeField] private float scrollSpeedX = 0.05f;
    
    [Tooltip("Velocidad vertical (-1 a 1)")]
    [SerializeField] private float scrollSpeedY = 0.05f;

    private RawImage _rawImage;
    private Rect _currentUV;

    private void Awake()
    {
        _rawImage = GetComponent<RawImage>();
    }

    private void Update()
    {
        _currentUV = _rawImage.uvRect;
        _currentUV.x += scrollSpeedX * Time.deltaTime;
        _currentUV.y += scrollSpeedY * Time.deltaTime;

        _rawImage.uvRect = _currentUV;
    }
}