Shader "DATN/IceButton/SurfaceGrade"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _ShadowColor ("Deep Ice Shadow", Color) = (0.035, 0.15, 0.33, 1)
        _MidColor ("Ice Midtone", Color) = (0.16, 0.49, 0.72, 1)
        [HDR] _SnowColor ("Snow Highlight", Color) = (0.92, 1.08, 1.18, 1)
        _GradeStrength ("Grade Strength", Range(0, 1)) = 0.72
        _Contrast ("Contrast", Range(0.5, 2)) = 1.18
        _Color ("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "DeepCryoSurface"
            Tags { "LightMode" = "Universal2D" }

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
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _ShadowColor;
                float4 _MidColor;
                float4 _SnowColor;
                float4 _Color;
                float _GradeStrength;
                float _Contrast;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 source = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                half luminance = dot(source.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                half detail = saturate((luminance - 0.5h) * _Contrast + 0.5h);

                half3 deepIce = lerp(_ShadowColor.rgb, _MidColor.rgb, smoothstep(0.08h, 0.68h, detail));
                deepIce *= lerp(0.72h, 1.18h, detail);
                half snowMask = smoothstep(0.63h, 0.93h, luminance);
                half3 graded = lerp(deepIce, _SnowColor.rgb * lerp(0.82h, 1.05h, detail), snowMask);

                half cyanRune = smoothstep(0.18h, 0.55h, min(source.g, source.b) - source.r * 0.58h);
                half3 preserved = lerp(source.rgb, graded, _GradeStrength * (1.0h - cyanRune * 0.42h));
                return half4(preserved, source.a);
            }
            ENDHLSL
        }
    }
}
