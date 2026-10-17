Shader "Custom/Spine/EarthSlotAlphaClip"
{
    Properties
    {
        [MainTexture] _MainTex ("Atlas Texture", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        _EarthDeep ("Earth Deep", Color) = (0.18, 0.10, 0.045, 1)
        _EarthMid ("Earth Mid", Color) = (0.42, 0.25, 0.10, 1)
        _MossDark ("Dark Moss", Color) = (0.04, 0.22, 0.08, 1)
        _MossLight ("Moss Edge", Color) = (0.22, 0.48, 0.16, 1)
        _DustColor ("Dust / Rim Color", Color) = (0.72, 0.58, 0.36, 1)
        _FlowSpeed ("Flow Speed", Range(0, 5)) = 0.8
        _RockScale ("Rock Scale", Range(1, 80)) = 28
        _CrackScale ("Crack Scale", Range(1, 100)) = 48
        _Distortion ("Distortion", Range(0, 0.15)) = 0.018
        _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.03
        _AlphaSoftness ("Alpha Softness", Range(0, 0.5)) = 0.04
        _Opacity ("Opacity", Range(0, 1)) = 0.92
        _SceneLightInfluence ("Scene Light Influence", Range(0, 1)) = 0
        _OriginalDetail ("Keep Original Detail", Range(0, 1)) = 0.7
        _RimStrength ("Rim Strength", Range(0, 5)) = 1.85
        _MossStrength ("Moss Strength", Range(0, 3)) = 1.2
        _CrackStrength ("Crack Strength", Range(0, 3)) = 1.15
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        Cull Off
        ZWrite Off
        Blend [_SrcBlend] [_DstBlend]

        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                half4 _Color;
                half4 _RendererColor;
                float _EnableExternalAlpha;
                half4 _EarthDeep;
                half4 _EarthMid;
                half4 _MossDark;
                half4 _MossLight;
                half4 _DustColor;
                float _FlowSpeed;
                float _RockScale;
                float _CrackScale;
                float _Distortion;
                float _AlphaCutoff;
                float _AlphaSoftness;
                float _Opacity;
                float _SceneLightInfluence;
                float _OriginalDetail;
                float _RimStrength;
                float _MossStrength;
                float _CrackStrength;
                float _SrcBlend;
                float _DstBlend;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;

                UNITY_VERTEX_OUTPUT_STEREO
            };

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise21(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                v += noise21(p) * a; p *= 2.02; a *= 0.5;
                v += noise21(p) * a; p *= 2.03; a *= 0.5;
                v += noise21(p) * a; p *= 2.01; a *= 0.5;
                v += noise21(p) * a;
                return v;
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionHCS = TransformObjectToHClip(input.positionOS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            float getAlpha(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 vertexColor = input.color * _Color * _RendererColor;
                float2 uv = input.uv;
                float t = _Time.y;
                half4 atlas = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                float alphaMask = smoothstep(_AlphaCutoff, _AlphaCutoff + _AlphaSoftness, atlas.a);
                clip(alphaMask - 0.001);

                float originalLuma = saturate(dot(atlas.rgb, float3(0.299, 0.587, 0.114)));
                float2 px = _MainTex_TexelSize.xy;
                float aL = getAlpha(uv - float2(px.x, 0));
                float aR = getAlpha(uv + float2(px.x, 0));
                float aD = getAlpha(uv - float2(0, px.y));
                float aU = getAlpha(uv + float2(0, px.y));
                float edge = pow(saturate(alphaMask * 4.0 - aL - aR - aD - aU), 0.6);

                float2 drift = float2(t * _FlowSpeed * 0.05, -t * _FlowSpeed * 0.035);
                float stoneA = fbm((uv + drift) * _RockScale);
                float stoneB = fbm((uv - drift * 1.6) * (_RockScale * 1.9));
                float2 distortion = float2(stoneA - 0.5, stoneB - 0.5) * _Distortion;
                float rockMix = saturate(stoneA * 0.6 + stoneB * 0.4);

                float crackNoise = fbm((uv + distortion) * _CrackScale + float2(t * 0.05, 0));
                float crackLines = 1.0 - smoothstep(0.42, 0.58, abs(crackNoise - 0.5) * 2.0);
                crackLines = pow(saturate(crackLines), 3.0) * _CrackStrength;

                float mossNoise = fbm((uv - distortion) * (_RockScale * 0.8) + float2(0, t * _FlowSpeed * 0.08));
                float mossMask = smoothstep(0.48, 0.82, mossNoise + edge * 0.35) * _MossStrength;

                float detail = saturate(lerp(1.0, originalLuma * 1.35, _OriginalDetail));
                half3 earthColor = lerp(_EarthDeep.rgb, _EarthMid.rgb, rockMix);
                half3 mossColor = lerp(_MossDark.rgb, _MossLight.rgb, saturate(mossNoise + edge * 0.4));
                earthColor = lerp(earthColor, mossColor, saturate(mossMask * 0.65));
                earthColor = lerp(earthColor, _DustColor.rgb, saturate(crackLines * 0.35 + edge * 0.25));
                earthColor += _DustColor.rgb * edge * _RimStrength;
                earthColor *= 1.0 - crackLines * 0.22;

                float finalAlpha = alphaMask;
                finalAlpha += edge * 0.25;
                finalAlpha += crackLines * 0.08;
                finalAlpha = saturate(finalAlpha) * _Opacity * vertexColor.a;

                half4 selfLit = half4(earthColor * detail * vertexColor.rgb, finalAlpha);
                return selfLit;
            }
            ENDHLSL

        Pass
        {
            Name "Universal2D"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY
            ENDHLSL
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" "Queue" = "Transparent" "RenderType" = "Transparent" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
