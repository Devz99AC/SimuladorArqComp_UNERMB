using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Contorno de la pieza resaltada (hover, arrastre, rechazo), dibujado en pantalla con Render Graph:
///  1. Máscara: los renderers con el bit «Resaltado» (<see cref="SelectionOutline.RenderingLayer"/>) en una textura RG8,
///     todos sus submeshes, con un material de reemplazo. R = silueta; G = la parte que se ve (no tapada).
///  2. Distancia horizontal al píxel de silueta más cercano (por fila).
///  3. Composición: distancia euclídea exacta (separable) → anillo de <see cref="Settings.widthAt1080"/> px
///     con borde suavizado y un filo oscuro por fuera (contraste sobre fondos claros), mezclado sobre el color
///     de la cámara. Lo que está detrás de una pared, más tenue.
/// Una sola silueta por pieza aunque tenga cien hijos, y el mismo grosor en píxeles para un tornillo que para la placa.
/// Sin nada resaltado no se encola ningún pase ni se pide la profundidad: coste cero (también en los menús).
/// </summary>
public class SelectionOutlineFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        [Tooltip("Al pasar el ratón: naranja de la marca (--color-accent)")]
        public Color hoverColor = new Color32(0xFF, 0x45, 0x00, 0xFF);
        [Tooltip("Al arrastrar: blanco cálido (no se confunde con los fantasmas cian y verde)")]
        public Color dragColor = new Color32(0xFF, 0xF1, 0xD6, 0xFF);
        [Tooltip("Sobre un lugar donde no encaja: rojo de error (--state-error)")]
        public Color rejectColor = new Color32(0xE5, 0x48, 0x4D, 0xFF);
        [Tooltip("Grosor en píxeles a 1080p; escala con el alto de la imagen")]
        [Range(1f, 8f)] public float widthAt1080 = 3f;
        [Tooltip("Filo oscuro por fuera del color: sin él, el blanco del arrastre no se ve sobre la mesa clara")]
        public Color edgeColor = new Color(0f, 0f, 0f, 0.6f);
        [Tooltip("Grosor del filo oscuro en píxeles a 1080p (0 = sin filo)")]
        [Range(0f, 4f)] public float edgeWidthAt1080 = 1f;
        [Tooltip("Opacidad del contorno donde la pieza queda tapada (dentro del gabinete, bajo otra pieza)")]
        [Range(0f, 1f)] public float occludedAlpha = 0.35f;
    }

    public Settings settings = new Settings();
    [SerializeField] private Shader shader;

    private Material _material;
    private OutlinePass _pass;

    public override void Create()
    {
        if (shader == null) shader = Shader.Find(OutlinePass.ShaderName);
        CoreUtils.Destroy(_material);
        _material = shader != null ? CoreUtils.CreateEngineMaterial(shader) : null;
        _pass = new OutlinePass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!SelectionOutline.IsActive || _material == null) return; // nada resaltado: ni pases ni profundidad

        CameraType type = renderingData.cameraData.cameraType;
        if (type != CameraType.Game && type != CameraType.SceneView) return; // ni previsualizaciones ni reflejos
        if (renderingData.cameraData.renderType == CameraRenderType.Overlay) return;

        _pass.Setup(_material, StyleColor(SelectionOutline.Style), settings);
        renderer.EnqueuePass(_pass);
    }

    private Color StyleColor(OutlineStyle style)
    {
        switch (style)
        {
            case OutlineStyle.Drag: return settings.dragColor;
            case OutlineStyle.Reject: return settings.rejectColor;
            default: return settings.hoverColor;
        }
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(_material);
        _material = null;
    }

    private class OutlinePass : ScriptableRenderPass
    {
        public const string ShaderName = "Hidden/Simulador/SelectionOutline";

        // Pases del shader
        private const int MaskWithDepthPass = 0;
        private const int MaskPass = 1;
        private const int HorizontalPass = 2;
        private const int CompositePass = 3;

        private static readonly int ColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int EdgeColorId = Shader.PropertyToID("_OutlineEdgeColor");
        private static readonly int WidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int EdgeWidthId = Shader.PropertyToID("_OutlineEdgeWidth");
        private static readonly int SearchId = Shader.PropertyToID("_OutlineSearch");
        private static readonly int OccludedId = Shader.PropertyToID("_OccludedAlpha");
        private static readonly int TexSizeId = Shader.PropertyToID("_OutlineTexSize");

        private readonly List<ShaderTagId> _shaderTags = new List<ShaderTagId>
        {
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly"),
            new ShaderTagId("SRPDefaultUnlit"),
        };
        // Reutilizados entre cámaras (sin basura por fotograma): URP graba y ejecuta cada cámara antes de grabar la
        // siguiente, y el blit copia el bloque al encolar el dibujo, así que una cámara no pisa los valores de otra.
        private readonly MaterialPropertyBlock _horizontalProps = new MaterialPropertyBlock();
        private readonly MaterialPropertyBlock _compositeProps = new MaterialPropertyBlock();

        private Material _material;
        private Color _color;
        private Settings _settings;

        private class MaskPassData
        {
            public RendererListHandle renderers;
        }

        public OutlinePass()
        {
            // Antes del posprocesado: el destino aún es la textura intermedia de la cámara (nunca el backbuffer), con la
            // misma orientación que la máscara y la profundidad. El perfil de volumen por defecto no tiene tonemapping
            // ni bloom, así que el color sale tal cual; el antialiasing de la cámara, si lo hay, suaviza el borde.
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            // Solo cuenta mientras el pase está en cola (con algo resaltado): en reposo nadie pide la profundidad.
            ConfigureInput(ScriptableRenderPassInput.Depth);
            profilingSampler = new ProfilingSampler("Contorno: máscara");
        }

        public void Setup(Material material, Color color, Settings settings)
        {
            _material = material;
            _color = color;
            _settings = settings;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer) return; // no pasa con texturas intermedias «Always»

            var renderingData = frameData.Get<UniversalRenderingData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var lightData = frameData.Get<UniversalLightData>();

            // Máscara y distancias: mismo tamaño que el color de la cámara, sin MSAA, dos canales de 8 bits.
            TextureDesc desc = renderGraph.GetTextureDesc(resources.activeColorTexture);
            desc.format = GraphicsFormat.R8G8_UNorm;
            desc.msaaSamples = MSAASamples.None;
            desc.bindTextureMS = false;
            desc.useMipMap = false;
            desc.autoGenerateMips = false;
            desc.enableRandomWrite = false;
            desc.filterMode = FilterMode.Point;
            desc.wrapMode = TextureWrapMode.Clamp;
            desc.name = "_ContornoMascara";
            desc.clearBuffer = true;
            desc.clearColor = Color.clear;
            desc.fallBackToBlackTexture = true; // si la pieza no está en cámara se cancela el pase de máscara
            TextureHandle mask = renderGraph.CreateTexture(desc);

            desc.name = "_ContornoDistanciaX";
            desc.clearBuffer = false; // el pase horizontal escribe todos los píxeles
            desc.fallBackToBlackTexture = false;
            TextureHandle horizontal = renderGraph.CreateTexture(desc);

            // Grosor constante en pantalla: 3 px a 1080p, 2 px a 720p, 6 px a 4K (más el filo oscuro).
            float scale = desc.height / 1080f;
            float width = Mathf.Max(1f, _settings.widthAt1080 * scale);
            float edgeWidth = _settings.edgeWidthAt1080 > 0f ? Mathf.Max(1f, _settings.edgeWidthAt1080 * scale) : 0f;
            float search = Mathf.Ceil(width + edgeWidth) + 1f;
            var texSize = new Vector4(desc.width, desc.height, 0f, 0f);

            // 1. Máscara de la pieza resaltada.
            TextureHandle depth = resources.cameraDepthTexture;
            bool useDepth = depth.IsValid();
            using (var builder = renderGraph.AddRasterRenderPass<MaskPassData>("Contorno: máscara", out var passData, profilingSampler))
            {
                DrawingSettings drawing = RenderingUtils.CreateDrawingSettings(_shaderTags, renderingData, cameraData, lightData, SortingCriteria.None);
                drawing.overrideMaterial = _material;
                drawing.overrideMaterialPassIndex = useDepth ? MaskWithDepthPass : MaskPass;
                var filtering = new FilteringSettings(RenderQueueRange.all, -1, SelectionOutline.RenderingLayer);

                passData.renderers = renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults, drawing, filtering));
                builder.UseRendererList(passData.renderers);
                if (useDepth) builder.UseTexture(depth); // el shader lee _CameraDepthTexture (global de URP)
                builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (MaskPassData data, RasterGraphContext context) =>
                    context.cmd.DrawRendererList(data.renderers));
            }

            // 2. Distancia horizontal a la silueta (y a su parte visible).
            _horizontalProps.SetFloat(SearchId, search);
            _horizontalProps.SetVector(TexSizeId, texSize);
            renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(mask, horizontal, _material, HorizontalPass,
                _horizontalProps, RenderGraphUtils.FullScreenGeometryType.ProceduralTriangle), "Contorno: distancia horizontal");

            // 3. Anillo alrededor de la silueta, sobre el color de la cámara.
            _compositeProps.SetColor(ColorId, _color);
            _compositeProps.SetColor(EdgeColorId, _settings.edgeColor);
            _compositeProps.SetFloat(WidthId, width);
            _compositeProps.SetFloat(EdgeWidthId, edgeWidth);
            _compositeProps.SetFloat(SearchId, search);
            _compositeProps.SetFloat(OccludedId, _settings.occludedAlpha);
            _compositeProps.SetVector(TexSizeId, texSize);
            renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(horizontal, resources.activeColorTexture, _material, CompositePass,
                _compositeProps, RenderGraphUtils.FullScreenGeometryType.ProceduralTriangle), "Contorno: composición");
        }
    }
}
