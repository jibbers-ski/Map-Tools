Shader "Custom/BasicSnowTerrain"
{
    Properties
    {
        _BaseColor ("Snow Color", Color) = (1,1,1,1)
        _RockColor ("Rock Color", Color) = (0.35, 0.3, 0.28, 1)
        _SnowMask ("Snow Mask", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
            };

            TEXTURE2D(_SnowMask); SAMPLER(sampler_SnowMask);
            float4 _BaseColor, _RockColor;

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs posInputs = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionHCS = posInputs.positionCS;
                o.positionWS = posInputs.positionWS;
                o.uv = v.uv;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            float4 frag (Varyings i) : SV_Target
            {
                float snow = SAMPLE_TEXTURE2D(_SnowMask, sampler_SnowMask, float2(i.uv.x, 1 - i.uv.y)).r;
                float3 col = lerp(_RockColor.rgb, _BaseColor.rgb, snow);

                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(normalize(i.normalWS), normalize(mainLight.direction)));
                col *= mainLight.color * NdotL + 0.3;

                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
