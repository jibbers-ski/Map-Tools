Shader "Custom/BasicSnowTerrain"
{
    Properties
    {
        _BaseColor ("Snow Color", Color) = (1,1,1,1)
        _RockColor ("Rock Color", Color) = (0.35, 0.3, 0.28, 1)
        _SnowMask ("Snow Mask", 2D) = "white" {}
        _MarkingTint ("Marking Tint Strength", Range(0, 1)) = 0.3
        [Toggle] _SnowMask4Channel ("Snow Mask 4-Channel", Float) = 0
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
            SamplerState sampler_point_clamp;
            float4 _BaseColor, _RockColor;
            float _MarkingTint;
            float _SnowMask4Channel;

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

            float3 HsvToRgb(float h, float s, float v)
            {
                float3 rgb = saturate(abs(fmod(h * 6.0 + float3(0, 4, 2), 6.0) - 3.0) - 1.0);
                return v * lerp(float3(1,1,1), rgb, s);
            }

            float4 frag (Varyings i) : SV_Target
            {
                float2 maskUv = float2(i.uv.x, 1 - i.uv.y);
                float4 maskBilinear = SAMPLE_TEXTURE2D(_SnowMask, sampler_SnowMask, maskUv);
                float snow = maskBilinear.r;

                float3 col = lerp(_RockColor.rgb, _BaseColor.rgb, snow);

                float coverage = _SnowMask4Channel > 0.5 ? maskBilinear.b : 0;
                float marking  = _SnowMask4Channel > 0.5 ? SAMPLE_TEXTURE2D_LOD(_SnowMask, sampler_point_clamp, maskUv, 0).g : 0;
                float snapped  = round(marking * 5.0) / 5.0;
                if (snapped > 0 && snapped < 1.0 && coverage > 0)
                {
                    float3 tint = HsvToRgb(snapped, 1.0, 1.0);
                    col = lerp(col, col * tint, _MarkingTint * coverage);
                }

                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(normalize(i.normalWS), normalize(mainLight.direction)));
                col *= mainLight.color * NdotL + 0.3;

                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
