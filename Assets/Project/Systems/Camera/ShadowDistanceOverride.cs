using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Cambia la distancia de sombras del URP activo mientras esta escena está en juego y la restaura al salir.
/// El asset de URP es compartido: AssemblyLab (escala ×3,7) necesita 50 m, pero un laboratorio a escala
/// real, con piezas de pocos centímetros, necesita mucho menos para que el mapa de sombras tenga detalle
/// (50 m en un mapa de 2048 son 2,4 cm por texel: la CPU apenas proyectaría sombra).
/// Solo en Play: en el editor no se toca el asset, para no guardarlo cambiado por accidente.
/// </summary>
public class ShadowDistanceOverride : MonoBehaviour
{
    [Tooltip("Distancia de sombras (m) para esta escena")]
    [SerializeField] private float shadowDistance = 4f;

    private float _previous = -1f;

    private void OnEnable()
    {
        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
        {
            _previous = urp.shadowDistance;
            urp.shadowDistance = shadowDistance;
        }
    }

    private void OnDisable()
    {
        if (_previous >= 0f && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            urp.shadowDistance = _previous;
        _previous = -1f;
    }
}
