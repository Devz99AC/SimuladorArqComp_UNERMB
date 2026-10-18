Shader "Custom/GhostPreview"
{
    // Vista previa translúcida del lugar de instalación (PlacementPreview).
    // Relleno tenue + borde Fresnel más opaco: se lee la forma de la pieza sin tapar lo de detrás.
    // _BaseColor.a controla la intensidad global (reposo tenue / objetivo intenso, vía MaterialPropertyBlock).
    Properties
    {
        _BaseColor ("Color", Color) = (0.35, 0.85, 1, 0.25)
        _RimColor ("Color del borde", Color) = (0.75, 0.97, 1, 1)
        _RimPower ("Finura del borde", Range(0.5, 8)) = 2.5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        LOD 100

        Pass
        {
            Name "Ghost"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

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
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _RimColor;
                float _RimPower;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(positions.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 v = normalize(input.viewDirWS);
                float rim = pow(1.0 - saturate(abs(dot(n, v))), _RimPower);

                float3 color = lerp(_BaseColor.rgb, _RimColor.rgb, rim);
                float alpha = saturate(_BaseColor.a * (0.6 + 2.0 * rim));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
