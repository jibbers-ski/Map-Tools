Shader "Custom/CustomObjectLit"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1,1,1,1)

        [Space(10)]
        _RoughnessMap("Roughness Map", 2D) = "white" {}
        _Smoothness("Smoothness", Range(0,1)) = 0
        _MetallicMap("Metallic Map", 2D) = "black" {}
        _Metallic("Metallic", Range(0,1)) = 0
        _NormalMap("Normal Map", 2D) = "bump" {}

        [Space(10)]
        _EmissionMap("Emission Map", 2D) = "white" {}
        [HDR] _EmissionColor("Emission Color", Color) = (0,0,0,1)

        [Space(10)]
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Float) = 2

        [HideInInspector] _RenderMode ("__mode", Float) = 0.0
        [HideInInspector] _SrcBlend ("__src", Float) = 1.0
        [HideInInspector] _DstBlend ("__dst", Float) = 0.0
        [HideInInspector] _ZWrite ("__zw", Float) = 1.0

        [HideInInspector] _UseOverlay ("__use_overlay", Float) = 0
        [NoScaleOffset] _OverlayMap ("Overlay Map", 2D) = "white" {}
        _OverlayColor ("Overlay Color", Color) = (1,1,1,1)
        _OverlayDirection ("Overlay Direction", Vector) = (0,1,0,0)
        _OverlayTiling ("Overlay Tiling", Float) = 1
        _OverlayIntensity ("Overlay Intensity", Range(0,1)) = 1
        _OverlayAmount ("Overlay Coverage", Range(-1,1)) = 0.4
        _OverlaySharpness ("Overlay Sharpness", Range(0.001,2)) = 0.3
        _OverlaySmoothness ("Overlay Smoothness", Range(0,1)) = 0.3
        _OverlayNormalBlend ("Overlay Normal Blend", Range(0,1)) = 0.5

        [HideInInspector] _UseOverlayFade ("__use_overlay_fade", Float) = 0
        _OverlayFadeAxis ("Overlay Fade Axis", Vector) = (0,1,0,0)
        _OverlayFadeMin ("Overlay Fade Min", Float) = 0
        _OverlayFadeMax ("Overlay Fade Max", Float) = 1
        [Toggle] _OverlayFadeObjectSpace ("Overlay Fade Object Space", Float) = 1

        [HideInInspector] _UseLiquid ("__use_liquid", Float) = 0
        _LiquidTiling ("Liquid Tiling", Float) = 10
        _LiquidScroll1 ("Liquid Scroll 1", Vector) = (0.05,0.03,0,0)
        _LiquidScroll2 ("Liquid Scroll 2", Vector) = (-0.04,0.06,0,0)
        _LiquidWaveHeight ("Liquid Wave Height", Range(0,2)) = 0.5
        _LiquidSmoothness ("Liquid Smoothness", Range(0,1)) = 0.95
        _LiquidFresnelPower ("Liquid Fresnel Power", Range(0.5,8)) = 4
        _LiquidFresnelStrength ("Liquid Fresnel Strength", Range(0,2)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "Queue"="Geometry"
        }

        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _EmissionColor;
                float _Smoothness;
                float _Metallic;
                float _Cutoff;
                float4 _OverlayColor;
                float4 _OverlayDirection;
                float _OverlayTiling;
                float _OverlayIntensity;
                float _OverlayAmount;
                float _OverlaySharpness;
                float _OverlaySmoothness;
                float _OverlayNormalBlend;
                float4 _OverlayFadeAxis;
                float _OverlayFadeMin;
                float _OverlayFadeMax;
                float _OverlayFadeObjectSpace;
                float4 _LiquidScroll1;
                float4 _LiquidScroll2;
                float _LiquidTiling;
                float _LiquidWaveHeight;
                float _LiquidSmoothness;
                float _LiquidFresnelPower;
                float _LiquidFresnelStrength;
            CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Cull [_Cull]
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            HLSLPROGRAM
            #pragma target 3.0

            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fragment _ _CLUSTERED_RENDERING
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #pragma multi_compile_local _ _ALPHATEST_ON
            #pragma multi_compile_local _ _RECEIVE_SHADOWS_OFF
            #pragma multi_compile_local _ _OVERLAY
            #pragma multi_compile_local _ _OVERLAY_FADE
            #pragma multi_compile_local _ _LIQUID

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            TEXTURE2D(_RoughnessMap);
            SAMPLER(sampler_RoughnessMap);

            TEXTURE2D(_MetallicMap);
            SAMPLER(sampler_MetallicMap);

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);

            TEXTURE2D(_OverlayMap);
            SAMPLER(sampler_OverlayMap);

            float LiquidHash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float LiquidNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = LiquidHash(i);
                float b = LiquidHash(i + float2(1, 0));
                float c = LiquidHash(i + float2(0, 1));
                float d = LiquidHash(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float LiquidHeight(float2 uv)
            {
                float2 uv1 = uv * _LiquidTiling + _Time.y * _LiquidScroll1.xy;
                float2 uv2 = uv * _LiquidTiling * 1.7 + _Time.y * _LiquidScroll2.xy;
                return LiquidNoise(uv1) * 0.6 + LiquidNoise(uv2) * 0.4;
            }

            half3 LiquidNormalTS(float2 uv)
            {
                float eps = 0.01;
                float h0 = LiquidHeight(uv);
                float hx = LiquidHeight(uv + float2(eps, 0));
                float hy = LiquidHeight(uv + float2(0, eps));
                half dx = (half)((h0 - hx) * _LiquidWaveHeight / eps);
                half dy = (half)((h0 - hy) * _LiquidWaveHeight / eps);
                return normalize(half3(dx, dy, 1.0));
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;

                float3 positionWS : TEXCOORD1;
                half3  normalWS   : TEXCOORD2;
                half4  tangentWS  : TEXCOORD3;

                half   fogFactor  : TEXCOORD4;
                float3 positionOS : TEXCOORD5;

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs  = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.positionOS = input.positionOS.xyz;

                output.normalWS  = normInputs.normalWS;
                output.tangentWS = half4(normInputs.tangentWS, input.tangentOS.w);

                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);

                return output;
            }

            half4 frag(Varyings input, half facing : VFACE) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                half metallicSample;
                half smoothnessSample;
                half3 normalTS;

                #if defined(_LIQUID)
                    metallicSample   = 0.0h;
                    smoothnessSample = _LiquidSmoothness;
                    normalTS         = LiquidNormalTS(input.uv);
                #else
                    metallicSample   = saturate(SAMPLE_TEXTURE2D(_MetallicMap, sampler_MetallicMap, input.uv).x + _Metallic);
                    smoothnessSample = saturate(1.0 - SAMPLE_TEXTURE2D(_RoughnessMap, sampler_RoughnessMap, input.uv).x + _Smoothness);
                    normalTS         = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv));
                #endif

                #if defined(_ALPHATEST_ON)
                    clip(baseSample.a - _Cutoff);
                #endif

                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                half3 tangentWS = normalize(input.tangentWS.xyz);
                half tangentSign = input.tangentWS.w * GetOddNegativeScale();
                half3 bitangentWS = cross(normalWS, tangentWS) * tangentSign;

                half3x3 TBN = half3x3(tangentWS, bitangentWS, normalWS);
                half3 finalNormalWS = normalize(mul(normalTS, TBN));
                finalNormalWS *= (facing > 0) ? 1.0 : -1.0;

                #if defined(_OVERLAY)
                    half3 overlayDir = normalize(_OverlayDirection.xyz);
                    half overlayDot = dot(finalNormalWS, overlayDir);
                    half overlayFactor = smoothstep(_OverlayAmount - _OverlaySharpness, _OverlayAmount + _OverlaySharpness, overlayDot);
                    overlayFactor *= _OverlayIntensity;

                    #if defined(_OVERLAY_FADE)
                        half3 fadePos = lerp(input.positionWS, input.positionOS, _OverlayFadeObjectSpace);
                        half fadeProj = dot(fadePos, normalize(_OverlayFadeAxis.xyz));
                        half fadeT = saturate((fadeProj - _OverlayFadeMin) / (_OverlayFadeMax - _OverlayFadeMin + 0.0001));
                        half fadeMask = fadeT * fadeT * (3.0 - 2.0 * fadeT);
                        overlayFactor *= fadeMask;
                    #endif

                    half3 overlayAlbedo = SAMPLE_TEXTURE2D(_OverlayMap, sampler_OverlayMap, input.uv * _OverlayTiling).rgb * _OverlayColor.rgb;

                    baseSample.rgb   = lerp(baseSample.rgb,   overlayAlbedo,      overlayFactor);
                    metallicSample   = lerp(metallicSample,   0.0h,               overlayFactor);
                    smoothnessSample = lerp(smoothnessSample, _OverlaySmoothness, overlayFactor);

                    finalNormalWS = normalize(lerp(finalNormalWS, overlayDir, overlayFactor * _OverlayNormalBlend));
                #endif

                InputData inputData;
                ZERO_INITIALIZE(InputData, inputData);

                inputData.positionWS = input.positionWS;
                inputData.normalWS = finalNormalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.vertexLighting = VertexLighting(input.positionWS, normalWS);
                inputData.bakedGI = SampleSH(normalWS);

                SurfaceData surfaceData;
                ZERO_INITIALIZE(SurfaceData, surfaceData);

                half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;
                #if defined(_LIQUID)
                    half liquidFresnel = pow(1.0h - saturate(dot(finalNormalWS, inputData.viewDirectionWS)), _LiquidFresnelPower);
                    emission += liquidFresnel * _LiquidFresnelStrength;
                #endif

                surfaceData.albedo = baseSample.rgb;
                surfaceData.metallic = metallicSample;
                surfaceData.smoothness = smoothnessSample;
                surfaceData.normalTS = half3(0,0,1);
                surfaceData.occlusion = 1.0h;
                surfaceData.emission = emission;
                surfaceData.alpha = baseSample.a;

                return UniversalFragmentPBR(inputData, surfaceData);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ _ALPHATEST_ON

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                #if defined(_ALPHATEST_ON)
                    half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                    clip(baseSample.a - _Cutoff);
                #endif

                return 0;
            }

            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }

            ZWrite On
            ColorMask 0

            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0

            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_instancing
            #pragma multi_compile_local _ _ALPHATEST_ON

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                #if defined(_ALPHATEST_ON)
                    half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                    clip(baseSample.a - _Cutoff);
                #endif

                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "Jibbers.MapTools.CustomObjectLitShaderGUI"
}
