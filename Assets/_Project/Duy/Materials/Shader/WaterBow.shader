Shader "Custom/Spine/SlotOverrideWaterBlend"
{
    Properties
    {
        [MainTexture] _MainTex ("Atlas Texture", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        _WaterDeep ("Water Deep", Color) = (0.0, 0.22, 0.75, 1)
        _WaterShallow ("Water Shallow", Color) = (0.25, 0.85, 1.0, 1)
        _FoamColor ("Foam Color", Color) = (0.9, 0.98, 1.0, 1)
        _FlowSpeed ("Flow Speed", Range(0, 5)) = 1.4
        _WaveScale ("Wave Scale", Range(1, 40)) = 14
        _Distortion ("Distortion", Range(0, 0.1)) = 0.025
        _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.03
        _AlphaSoftness ("Alpha Softness", Range(0, 0.5)) = 0.06
        _Opacity ("Opacity", Range(0, 1)) = 1
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
                half4 _Color;
                half4 _RendererColor;
                float _EnableExternalAlpha;
                half4 _WaterDeep;
                half4 _WaterShallow;
                half4 _FoamColor;
                float _FlowSpeed;
                float _WaveScale;
                float _Distortion;
                float _AlphaCutoff;
                float _AlphaSoftness;
                float _Opacity;
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
                v += noise21(p) * a; p *= 2.03; a *= 0.5;
                v += noise21(p) * a; p *= 2.01; a *= 0.5;
                v += noise21(p) * a; p *= 2.04; a *= 0.5;
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

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 vertexColor = input.color * _Color * _RendererColor;
                half4 atlas = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float alphaMask = smoothstep(_AlphaCutoff, _AlphaCutoff + _AlphaSoftness, atlas.a);
                clip(alphaMask - 0.001);

                float2 uv = input.uv;
                float t = _Time.y;
                float n1 = fbm(uv * _WaveScale + float2(0, t * _FlowSpeed));
                float n2 = fbm(uv * (_WaveScale * 1.8) + float2(t * 0.35, -t * _FlowSpeed));
                float2 distortion = float2(n1 - 0.5, n2 - 0.5) * _Distortion;
                float waterNoise = fbm((uv + distortion) * _WaveScale + float2(0, t * _FlowSpeed));

                half3 waterColor = lerp(_WaterDeep.rgb, _WaterShallow.rgb, waterNoise);
                float waveLine = sin((uv.y + n2 * 0.25 - t * _FlowSpeed) * 28.0);
                waveLine = pow(saturate(waveLine * 0.5 + 0.5), 8.0);
                waterColor += _FoamColor.rgb * waveLine * 0.25;

                half finalAlpha = alphaMask * _Opacity * vertexColor.a;
                return half4(waterColor * vertexColor.rgb, finalAlpha);
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
