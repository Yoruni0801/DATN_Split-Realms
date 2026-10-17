Shader "Custom/Spine/WindSlotAlphaClip"
{
    Properties
    {
        [MainTexture] _MainTex ("Atlas Texture", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        _WindDeep ("Wind Deep", Color) = (0.08, 0.45, 0.18, 1)
        _WindMid ("Wind Mid", Color) = (0.35, 0.85, 0.35, 1)
        _WindLight ("Wind Light", Color) = (0.78, 1.0, 0.72, 1)
        _WhiteColor ("White Air Color", Color) = (1.0, 1.0, 0.92, 1)
        _FlowSpeed ("Flow Speed", Range(0, 5)) = 1.8
        _WaveScale ("Wave Scale", Range(1, 60)) = 18
        _Distortion ("Distortion", Range(0, 0.15)) = 0.03
        _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.03
        _AlphaSoftness ("Alpha Softness", Range(0, 0.5)) = 0.06
        _Opacity ("Opacity", Range(0, 1)) = 0.9
        _SceneLightInfluence ("Scene Light Influence", Range(0, 1)) = 0
        _OriginalDetail ("Keep Original Detail", Range(0, 1)) = 0.55
        _HollowAmount ("Air Transparency Inside", Range(0, 1)) = 0.38
        _RimStrength ("Rim Strength", Range(0, 5)) = 2.4
        _FlowLineStrength ("Flow Line Strength", Range(0, 3)) = 1.55
        _SparkleStrength ("Sparkle Strength", Range(0, 3)) = 0.35
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
                half4 _WindDeep;
                half4 _WindMid;
                half4 _WindLight;
                half4 _WhiteColor;
                float _FlowSpeed;
                float _WaveScale;
                float _Distortion;
                float _AlphaCutoff;
                float _AlphaSoftness;
                float _Opacity;
                float _SceneLightInfluence;
                float _OriginalDetail;
                float _HollowAmount;
                float _RimStrength;
                float _FlowLineStrength;
                float _SparkleStrength;
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
                float edge = pow(saturate(alphaMask * 4.0 - aL - aR - aD - aU), 0.55);

                float2 gustUv = uv + float2(t * _FlowSpeed * 0.12, -t * _FlowSpeed * 0.2);
                float n1 = fbm(gustUv * _WaveScale + float2(t * _FlowSpeed, 0));
                float n2 = fbm(uv * (_WaveScale * 1.6) + float2(-t * _FlowSpeed * 0.45, t * 0.3));
                float2 distortion = float2(n1 - 0.5, n2 - 0.5) * _Distortion;
                float windNoiseA = fbm((uv + distortion) * _WaveScale + float2(t * _FlowSpeed, t * 0.2));
                float windNoiseB = fbm((uv - distortion) * (_WaveScale * 0.72) + float2(-t * 0.35, t * _FlowSpeed * 0.65));
                float windMix = saturate(windNoiseA * 0.58 + windNoiseB * 0.42);

                float flowLine1 = sin((uv.x + windNoiseA * 0.28 + t * _FlowSpeed * 0.9) * 42.0);
                flowLine1 = pow(saturate(flowLine1 * 0.5 + 0.5), 11.0);
                float flowLine2 = sin((uv.y + windNoiseB * 0.2 - t * _FlowSpeed * 0.55) * 28.0);
                flowLine2 = pow(saturate(flowLine2 * 0.5 + 0.5), 13.0);
                float flowLines = saturate(flowLine1 + flowLine2 * 0.5) * _FlowLineStrength;

                float detail = saturate(lerp(1.0, originalLuma * 1.35, _OriginalDetail));
                half3 windColor = lerp(_WindDeep.rgb, _WindMid.rgb, windMix);
                windColor = lerp(windColor, _WindLight.rgb, saturate(flowLines * 0.55 + edge * 0.7));
                windColor = lerp(windColor, _WhiteColor.rgb, saturate(edge * 0.5 + flowLines * 0.25));
                windColor += _WhiteColor.rgb * edge * _RimStrength;
                windColor += _WhiteColor.rgb * flowLines * 0.28;

                float sparkleNoise = fbm((uv + float2(-t * 0.2, t * 0.12)) * 58.0);
                float sparkle = pow(saturate(sparkleNoise), 24.0) * _SparkleStrength;
                windColor += _WhiteColor.rgb * sparkle;

                float bodyDensity = lerp(1.0, saturate(originalLuma + windMix * 0.4), _HollowAmount);
                float finalAlpha = alphaMask;
                finalAlpha *= lerp(1.0, bodyDensity, _HollowAmount);
                finalAlpha += edge * 0.35;
                finalAlpha += flowLines * 0.12;
                finalAlpha = saturate(finalAlpha) * _Opacity * vertexColor.a;

                half4 selfLit = half4(windColor * detail * vertexColor.rgb, finalAlpha);
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
