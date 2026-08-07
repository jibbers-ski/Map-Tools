Shader "Custom/BasicSnowTerrain"
{
    Properties
    {
        _BaseColor ("Snow Color", Color) = (1,1,1,1)
        _RockColor ("Rock Color", Color) = (0.35, 0.3, 0.28, 1)
        _PowderMinColor ("Powder Min Color (depth 0)", Color) = (0.93, 0.95, 1, 1)
        _PowderMaxColor ("Powder Max Color (depth 1)", Color) = (0.55, 0.72, 1, 1)
        _SnowMask ("Snow Mask", 2D) = "white" {}
        _MarkingTint ("Marking Tint Strength", Range(0, 1)) = 0.7
        [Toggle] _SnowMask4Channel ("Snow Mask 4-Channel", Float) = 0
        [Toggle] _ThirdFromAlpha ("Powder From SnowMask Alpha", Float) = 0
        _PowderMaxHeight ("Powder Max Height (m)", Float) = 1
        _ThirdCoverageDepth ("Powder Full Coverage Depth (alpha)", Range(0.01, 1)) = 0.1
        [Toggle] _FlowFromMask2 ("Flow From SnowMask2 (R = angle)", Float) = 0
        _SnowMask2 ("Snow Mask 2 (R = flow angle 0..1 = 0..360)", 2D) = "black" {}
        _PisteTiling ("Groove Frequency (World)", Float) = 10.0
        _GrooveStrength ("Groove Strength", Range(0, 1)) = 0.4
        [Enum(Off,0,Solid,1,Normals,2,UV,3,Mask,4,Powder,5,Flow,6)] _DebugMode ("Debug View", Float) = 0
        [Enum(Off,0,Snow,1,Markings,2,Powder,3,Flow,4)] _PaintView ("Paint Highlight (set by Better Terrain Editor)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" "TerrainCompatible"="True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor, _RockColor, _PowderMinColor, _PowderMaxColor;
            float4 _SnowMask2_TexelSize;
            float _MarkingTint;
            float _SnowMask4Channel;
            float _ThirdFromAlpha, _PowderMaxHeight, _ThirdCoverageDepth;
            float _FlowFromMask2;
            float _PisteTiling, _GrooveStrength;
            float _DebugMode;
            float _PaintView;
        CBUFFER_END

        TEXTURE2D(_SnowMask); SAMPLER(sampler_SnowMask);
        TEXTURE2D(_SnowMask2);
        SamplerState sampler_point_clamp;

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

        float PowderDepth(float4 mask)
        {
            return (_SnowMask4Channel > 0.5 && _ThirdFromAlpha > 0.5) ? saturate(mask.a) : 0.0;
        }

        float2 DecodeFlowDir(float a01)
        {
            float a = a01 * 6.28318530718;
            return float2(sin(a), cos(a));
        }

        float2 SampleFlowDirSmooth(float2 uv)
        {
            float2 tc = uv * _SnowMask2_TexelSize.zw - 0.5;
            float2 f = frac(tc);
            float2 uv00 = (floor(tc) + 0.5) * _SnowMask2_TexelSize.xy;
            float2 d00 = DecodeFlowDir(SAMPLE_TEXTURE2D_LOD(_SnowMask2, sampler_point_clamp, uv00, 0).r);
            float2 d10 = DecodeFlowDir(SAMPLE_TEXTURE2D_LOD(_SnowMask2, sampler_point_clamp, uv00 + float2(_SnowMask2_TexelSize.x, 0.0), 0).r);
            float2 d01 = DecodeFlowDir(SAMPLE_TEXTURE2D_LOD(_SnowMask2, sampler_point_clamp, uv00 + float2(0.0, _SnowMask2_TexelSize.y), 0).r);
            float2 d11 = DecodeFlowDir(SAMPLE_TEXTURE2D_LOD(_SnowMask2, sampler_point_clamp, uv00 + _SnowMask2_TexelSize.xy, 0).r);
            float2 d = lerp(lerp(d00, d10, f.x), lerp(d01, d11, f.x), f.y);
            float len = length(d);
            return len > 1e-4 ? d / len : d00;
        }

        float3 GroovePerturb(float3 positionWS, float2 maskUv, float amount)
        {
            UNITY_BRANCH
            if (_FlowFromMask2 > 0.5)
            {
                float2 cells = max(_SnowMask2_TexelSize.zw * 0.25, 1.0);
                float2 q = maskUv * cells;
                float2 fA = frac(q) - 0.5;
                float2 fB = frac(q - 0.5) - 0.5;
                float2 dA = SampleFlowDirSmooth((floor(q) + 0.5) / cells);
                float2 dB = SampleFlowDirSmooth((floor(q - 0.5) + 1.0) / cells);
                float gA = sin((positionWS.x * dA.y - positionWS.z * dA.x) * _PisteTiling);
                float gB = sin((positionWS.x * dB.y - positionWS.z * dB.x) * _PisteTiling);
                float wA = (1.0 - 2.0 * abs(fA.x)) * (1.0 - 2.0 * abs(fA.y));
                float wB = (1.0 - 2.0 * abs(fB.x)) * (1.0 - 2.0 * abs(fB.y));
                float blend = saturate(0.5 + (wB - wA) * 2.0);
                float3 pA = float3(dA.y, 0, -dA.x) * gA;
                float3 pB = float3(dB.y, 0, -dB.x) * gB;
                return lerp(pA, pB, blend) * amount;
            }
            return float3(sin(positionWS.x * _PisteTiling), 0, 0) * amount;
        }

        float3 HsvToRgb(float h, float s, float v)
        {
            float3 rgb = saturate(abs(fmod(h * 6.0 + float3(0, 4, 2), 6.0) - 3.0) - 1.0);
            return v * lerp(float3(1,1,1), rgb, s);
        }

        Varyings vert (Attributes v)
        {
            Varyings o;
            VertexPositionInputs posInputs = GetVertexPositionInputs(v.positionOS.xyz);
            float3 positionWS = posInputs.positionWS;
            o.positionHCS = posInputs.positionCS;
            float3 normalWS = TransformObjectToWorldNormal(v.normalOS, false);
            float len2 = dot(normalWS, normalWS);
            normalWS = len2 > 1e-8 ? normalWS * rsqrt(len2) : float3(0, 1, 0);
            UNITY_BRANCH
            if (_DebugMode < 0.5 && _SnowMask4Channel > 0.5 && _ThirdFromAlpha > 0.5 && abs(_PowderMaxHeight) > 0.0001)
            {
                float2 maskUv = float2(v.uv.x, 1 - v.uv.y);
                float4 mask = SAMPLE_TEXTURE2D_LOD(_SnowMask, sampler_SnowMask, maskUv, 0);
                positionWS += normalWS * (saturate(mask.a) * mask.r * _PowderMaxHeight);
                o.positionHCS = TransformWorldToHClip(positionWS);
            }
            o.positionWS = positionWS;
            o.uv = v.uv;
            o.normalWS = normalWS;
            return o;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float4 frag (Varyings i) : SV_Target
            {
                int dbgMode = (int)(_DebugMode + 0.5);
                if (dbgMode == 1)
                    return float4(1, 0, 1, 1);

                float3 N = normalize(i.normalWS);
                if (dbgMode == 2)
                    return float4(N * 0.5 + 0.5, 1);
                if (dbgMode == 3)
                    return float4(frac(i.uv), 0, 1);

                float2 maskUv = float2(i.uv.x, 1 - i.uv.y);
                float4 maskBilinear = SAMPLE_TEXTURE2D(_SnowMask, sampler_SnowMask, maskUv);
                if (dbgMode == 4)
                    return float4(maskBilinear.rgb, 1);
                if (dbgMode == 5)
                    return float4(saturate(maskBilinear.a), saturate(PowderDepth(maskBilinear) / max(0.01, _ThirdCoverageDepth)), 0, 1);
                if (dbgMode == 6)
                {
                    float2 fd = SampleFlowDirSmooth(maskUv);
                    return float4(HsvToRgb(frac(atan2(fd.x, fd.y) * 0.15915494309 + 1.0), 1.0, 1.0), 1.0);
                }

                // Paint highlight views (unlit, high-contrast; driven by the Better Terrain Editor).
                int paintView = (int)(_PaintView + 0.5);
                if (paintView == 1)
                    return float4(lerp(float3(0.75, 0.08, 0.08), float3(1, 1, 1), maskBilinear.r), 1.0);
                if (paintView == 2)
                {
                    float pvCover = _SnowMask4Channel > 0.5 ? maskBilinear.b : 0;
                    float pvMark  = _SnowMask4Channel > 0.5 ? SAMPLE_TEXTURE2D_LOD(_SnowMask, sampler_point_clamp, maskUv, 0).g : 0;
                    float pvSnapped = round(pvMark * 20.0) / 20.0;
                    float3 pvBg = lerp(float3(0.12, 0.12, 0.12), float3(0.35, 0.35, 0.35), maskBilinear.r);
                    float3 pvCol = (pvSnapped > 0 && pvSnapped < 1.0) ? HsvToRgb(pvSnapped, 1.0, 1.0) : pvBg;
                    return float4(lerp(pvBg, pvCol, saturate(pvCover * 4.0)), 1.0);
                }
                if (paintView == 3)
                {
                    float pvDepth = PowderDepth(maskBilinear);
                    float3 pvRamp = HsvToRgb((1.0 - saturate(pvDepth)) * 0.6667, 1.0, 1.0);
                    return float4(pvDepth > 0.002 ? pvRamp : float3(0.07, 0.07, 0.07), 1.0);
                }
                if (paintView == 4)
                {
                    float2 pvFd = SampleFlowDirSmooth(maskUv);
                    return float4(HsvToRgb(frac(atan2(pvFd.x, pvFd.y) * 0.15915494309 + 1.0), 1.0, 1.0), 1.0);
                }

                float snow = maskBilinear.r;
                float3 col = lerp(_RockColor.rgb, _BaseColor.rgb, snow);

                float powderDepth = PowderDepth(maskBilinear);
                float powderCoverage = saturate(powderDepth / max(0.01, _ThirdCoverageDepth)) * snow;
                col = lerp(col, lerp(_PowderMinColor.rgb, _PowderMaxColor.rgb, powderDepth), powderCoverage);

                float coverage = _SnowMask4Channel > 0.5 ? maskBilinear.b : 0;
                float marking  = _SnowMask4Channel > 0.5 ? SAMPLE_TEXTURE2D_LOD(_SnowMask, sampler_point_clamp, maskUv, 0).g : 0;
                float snapped  = round(marking * 20.0) / 20.0;
                if (snapped > 0 && snapped < 1.0 && coverage > 0)
                {
                    float3 tint = HsvToRgb(snapped, 1.0, 1.0);
                    float luminance = dot(col, float3(0.299, 0.587, 0.114));
                    float3 markingCol = tint * max(luminance, 0.5);
                    col = lerp(col, markingCol, _MarkingTint * coverage);
                }

                float grooveAmount = _GrooveStrength * snow * (1.0 - powderCoverage);
                if (grooveAmount > 0.001)
                {
                    float fade = saturate(1.0 - distance(i.positionWS, _WorldSpaceCameraPos) * 0.02);
                    N = normalize(N + GroovePerturb(i.positionWS, maskUv, grooveAmount * fade));
                }

                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(N, normalize(mainLight.direction)));
                col *= mainLight.color * NdotL + 0.3;

                return float4(col, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment fragDepth

            half4 fragDepth (Varyings i) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }

            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment fragDepthNormals

            half4 fragDepthNormals (Varyings i) : SV_Target
            {
                return half4(normalize(i.normalWS), 0);
            }
            ENDHLSL
        }
    }

    CustomEditor "Jibbers.MapTools.BasicSnowTerrainShaderGUI"
}
