// Contorno de selección en espacio de pantalla. Lo usa SelectionOutlineFeature (Systems/Rendering):
//  0/1. Máscara (material de reemplazo de los renderers resaltados): R = silueta, G = parte visible.
//  2.   Distancia horizontal (en píxeles) al píxel de silueta más cercano, para R y para G.
//  3.   Composición: distancia euclídea exacta combinando las filas vecinas → anillo con borde suavizado,
//       con un filo oscuro por fuera para que un color claro (el blanco del arrastre) se vea sobre la mesa clara.
// Todo trabaja en píxeles enteros (LOAD), así que la máscara, la profundidad y el destino coinciden sin
// transformar coordenadas: los tres tienen el tamaño de la cámara.
Shader "Hidden/Simulador/SelectionOutline"
{
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
        float4 _OutlineColor;
        float4 _OutlineEdgeColor; // filo exterior (rgb, opacidad)
        float4 _OutlineTexSize;   // ancho, alto (px)
        float _OutlineWidth;      // grosor del color en píxeles de esta imagen
        float _OutlineEdgeWidth;  // grosor del filo exterior en píxeles de esta imagen
        float _OutlineSearch;     // radio de búsqueda = ceil(grosor + filo) + 1
        float _OccludedAlpha;     // opacidad del contorno de la parte tapada
    CBUFFER_END

    // Lo más lejos que se busca: 255 = «nada a tiro» (la distancia se guarda en un canal de 8 bits).
    #define OUTLINE_NONE 255.0

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

    struct MaskAttributes
    {
        float4 positionOS : POSITION;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct MaskVaryings
    {
        float4 positionCS : SV_POSITION;
    };

    MaskVaryings MaskVert(MaskAttributes input)
    {
        MaskVaryings output;
        UNITY_SETUP_INSTANCE_ID(input);
        output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
        return output;
    }

    // Profundidad de vista (m) desde la del buffer, en perspectiva u ortográfica (capturar.sh puede ser ortográfica).
    float EyeDepth(float rawDepth)
    {
        if (unity_OrthoParams.w > 0.5)
        {
            #if UNITY_REVERSED_Z
            rawDepth = 1.0 - rawDepth;
            #endif
            return lerp(_ProjectionParams.y, _ProjectionParams.z, rawDepth);
        }
        return LinearEyeDepth(rawDepth, _ZBufferParams);
    }

    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

    half2 LoadSource(int2 pixel)
    {
        pixel = clamp(pixel, int2(0, 0), int2(_OutlineTexSize.xy) - 1);
        return LOAD_TEXTURE2D_X(_BlitTexture, pixel).rg;
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        // ---------- 0/1. Máscara ----------

        Pass
        {
            Name "Mascara con profundidad"
            BlendOp Max
            Blend One One
            ColorMask RG

            HLSLPROGRAM
            #pragma vertex MaskVert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            half4 Frag(MaskVaryings input) : SV_Target
            {
                // La pieza también está en la profundidad de la escena: sus fragmentos visibles coinciden con ella;
                // los tapados quedan detrás. Con Max, basta un fragmento visible para que el píxel cuente como visible.
                float sceneEye = EyeDepth(LoadSceneDepth(uint2(input.positionCS.xy)));
                float selfEye = EyeDepth(input.positionCS.z);
                half visible = selfEye <= sceneEye + max(0.0005, sceneEye * 0.002) ? 1.0 : 0.0;
                return half4(1.0, visible, 0.0, 0.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Mascara"
            BlendOp Max
            Blend One One
            ColorMask RG

            HLSLPROGRAM
            #pragma vertex MaskVert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            half4 Frag(MaskVaryings input) : SV_Target
            {
                return half4(1.0, 1.0, 0.0, 0.0); // sin profundidad: todo cuenta como visible
            }
            ENDHLSL
        }

        // ---------- 2/3. Pantalla completa ----------

        Pass
        {
            Name "Distancia horizontal"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            half4 Frag(Varyings input) : SV_Target
            {
                int2 pixel = int2(input.positionCS.xy);
                int radius = (int)_OutlineSearch;
                float nearestAny = OUTLINE_NONE;
                float nearestVisible = OUTLINE_NONE;
                for (int dx = -radius; dx <= radius; dx++)
                {
                    half2 mask = LoadSource(pixel + int2(dx, 0));
                    float distance = abs(dx);
                    if (mask.r > 0.5) nearestAny = min(nearestAny, distance);
                    if (mask.g > 0.5) nearestVisible = min(nearestVisible, distance);
                }
                return half4(nearestAny / OUTLINE_NONE, nearestVisible / OUTLINE_NONE, 0.0, 0.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Composicion"
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            // Anillo a d píxeles de la silueta (distancia entre centros: 1 = vecino del borde): color hasta el grosor y
            // luego el filo oscuro, los dos con medio píxel suavizado.
            half4 Ring(float d)
            {
                float inner = saturate(_OutlineWidth + 1.0 - d);
                float outer = saturate(_OutlineWidth + _OutlineEdgeWidth + 1.0 - d);
                float colorAlpha = inner * _OutlineColor.a;
                float edgeAlpha = (outer - inner) * _OutlineEdgeColor.a;
                float alpha = colorAlpha + edgeAlpha;
                half3 rgb = alpha > 0.0 ? (_OutlineColor.rgb * colorAlpha + _OutlineEdgeColor.rgb * edgeAlpha) / alpha : 0.0;
                return half4(rgb, alpha);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                int2 pixel = int2(input.positionCS.xy);
                // Dentro de la silueta no se dibuja: el contorno va por fuera y no tapa la pieza.
                if (LoadSource(pixel).r < 0.5 / OUTLINE_NONE) return half4(0.0, 0.0, 0.0, 0.0);

                int radius = (int)_OutlineSearch;
                float any2 = OUTLINE_NONE * OUTLINE_NONE;
                float visible2 = any2;
                for (int dy = -radius; dy <= radius; dy++)
                {
                    float2 dx = LoadSource(pixel + int2(0, dy)) * OUTLINE_NONE;
                    float dy2 = dy * dy;
                    any2 = min(any2, dx.x * dx.x + dy2);
                    visible2 = min(visible2, dx.y * dx.y + dy2);
                }

                // Junto a la parte visible, sólido; junto a la tapada (detrás de una pared), tenue.
                half4 visible = Ring(sqrt(visible2));
                half4 hidden = Ring(sqrt(any2));
                hidden.a *= _OccludedAlpha;
                return visible.a >= hidden.a ? visible : hidden;
            }
            ENDHLSL
        }
    }
}
