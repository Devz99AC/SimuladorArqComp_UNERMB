Shader "Custom/URPOutline"
{
    Properties
    {
        _OutlineColor ("Color del Borde", Color) = (1, 0.5, 0, 1) // Naranja por defecto
        _OutlineWidth ("Grosor del Borde", Range(0.0, 0.1)) = 0.005
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "Outline"
            Tags { "LightMode"="UniversalForward" }
            
            // EL SECRETO: Cull Front hace que se dibuje la cara interna (la de atrás)
            // Esto crea el efecto de borde cuando expandimos el objeto.
            Cull Front 
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                // 1. Convertir posición a Espacio de Mundo
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                
                // 2. Expandir el objeto siguiendo sus normales (inflarlo como un globo)
                // Esto es lo que crea el grosor del borde
                float3 normal = TransformObjectToWorldNormal(input.normalOS);
                float3 positionWS = vertexInput.positionWS + normal * _OutlineWidth;

                // 3. Convertir a Espacio de Clip (Pantalla)
                output.positionCS = TransformWorldToHClip(positionWS);
                
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Devolver color sólido
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}