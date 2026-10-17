Shader "DATN/IceButton/ParticleAdditive"
{
    Properties
    {
        _MainTex ("Particle Texture", 2D) = "white" {}
        [HDR] _TintColor ("Tint", Color) = (1, 1, 1, 1)
        _Softness ("Softness", Range(0.2, 3)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+30"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Cull Off
        ZWrite Off
        Blend One One

        Pass
        {
            Name "IceParticleAdditive"
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
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half alpha = pow(saturate(tex.a * input.color.a), _Softness);
                half3 color = tex.rgb * input.color.rgb * _TintColor.rgb * alpha;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
