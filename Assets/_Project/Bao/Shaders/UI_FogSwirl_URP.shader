Shader "Custom/UI_FogSwirl_URP"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _FogColor ("Fog Color", Color) = (0.02, 0.03, 0.08, 0.9)
        _FogColor2 ("Fog Color 2", Color) = (0.05, 0.08, 0.15, 0.7)
        _NoiseScale ("Noise Scale", Float) = 3.0
        _ScrollSpeed ("Scroll Speed", Float) = 0.15
        _Distortion ("Distortion", Float) = 0.3
        _EdgeSoftness ("Edge Softness", Float) = 0.4
        _Opacity ("Overall Opacity", Range(0, 1)) = 0.85
        
        // Required for UI
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "FogSwirl"
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _FogColor;
                half4 _FogColor2;
                half _NoiseScale;
                half _ScrollSpeed;
                half _Distortion;
                half _EdgeSoftness;
                half _Opacity;
            CBUFFER_END

            // Simple hash-based noise (no texture needed)
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f); // smoothstep

                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;

                for (int i = 0; i < 4; i++)
                {
                    value += amplitude * noise2D(p * frequency);
                    frequency *= 2.0;
                    amplitude *= 0.5;
                }
                return value;
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float time = _Time.y * _ScrollSpeed;

                // Layer 1: sương mù chính cuồn cuộn
                float2 fogUV1 = uv * _NoiseScale + float2(time * 0.7, time * 0.3);
                float fog1 = fbm(fogUV1);

                // Layer 2: sương mù phụ xoáy ngược
                float2 fogUV2 = uv * _NoiseScale * 1.5 + float2(-time * 0.5, time * 0.6);
                float fog2 = fbm(fogUV2 + fog1 * _Distortion);

                // Kết hợp
                float fogDensity = (fog1 + fog2) * 0.5;

                // Edge vignette - viền tối hơn trung tâm
                float2 center = uv - 0.5;
                float vignette = 1.0 - dot(center, center) * 2.0;
                vignette = saturate(vignette);
                vignette = lerp(0.6, 1.0, pow(vignette, _EdgeSoftness));

                // Pha trộn 2 màu fog
                half4 finalColor = lerp(_FogColor, _FogColor2, fogDensity);
                finalColor.a *= fogDensity * vignette * _Opacity;

                // Apply vertex color (để CanvasGroup.alpha hoạt động)
                finalColor *= input.color;

                return finalColor;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
