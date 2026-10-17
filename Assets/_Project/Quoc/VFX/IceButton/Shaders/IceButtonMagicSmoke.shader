Shader "DATN/IceButton/MagicSmoke"
{
    Properties
    {
        _MainTex ("Smoke Texture", 2D) = "white" {}
        [HDR] _TintColor ("Smoke Tint", Color) = (1, 1, 1, 1)
        _Softness ("Edge Softness", Range(0.25, 2.5)) = 0.75
        _Opacity ("Opacity", Range(0, 1)) = 0.82
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+31"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "GoldenMagicSmoke"
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
                float4 _TintColor;
                float _Softness;
                float _Opacity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 smoke = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half density = pow(saturate(smoke.a), _Softness);
                half alpha = density * input.color.a * _TintColor.a * _Opacity;
                half3 baseColor = smoke.rgb * input.color.rgb * _TintColor.rgb;
                half3 shadedSmoke = lerp(baseColor * 0.28h, baseColor, saturate(density * 1.4h));
                return half4(shadedSmoke, alpha);
            }
            ENDHLSL
        }
    }
}
