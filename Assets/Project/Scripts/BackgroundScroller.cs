using UnityEngine;
using UnityEngine.UI;

// Este atributo asegura que no puedas poner este script en un objeto sin RawImage
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
        // Obtenemos la referencia automáticamente
        _rawImage = GetComponent<RawImage>();
    }

    private void Update()
    {
        // Obtenemos el rectángulo actual
        _currentUV = _rawImage.uvRect;

        // Movemos la posición basándonos en el tiempo (frame-rate independent)
        _currentUV.x += scrollSpeedX * Time.deltaTime;
        _currentUV.y += scrollSpeedY * Time.deltaTime;

        // Aplicamos el cambio
        _rawImage.uvRect = _currentUV;
    }
}