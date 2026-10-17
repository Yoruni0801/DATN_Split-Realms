Shader "DATN/IceButton/FrostHalo"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _HaloColor ("Separation Color", Color) = (0.005, 0.055, 0.16, 0.86)
        _Thickness ("Thickness", Range(0.5, 8)) = 2.5
        _Intensity ("Intensity", Range(0, 4)) = 0.45
        _Pulse ("Pulse", Range(0, 1)) = 0
        _Color ("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+10"
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
            Name "FrostHalo"
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
                float4 _MainTex_TexelSize;
                float4 _HaloColor;
                float4 _Color;
                float _Thickness;
                float _Intensity;
                float _Pulse;
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

            half AlphaAt(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 px = _MainTex_TexelSize.xy * _Thickness;
                half center = AlphaAt(input.uv);
                half around = 0.0h;
                around = max(around, AlphaAt(input.uv + float2(px.x, 0)));
                around = max(around, AlphaAt(input.uv + float2(-px.x, 0)));
                around = max(around, AlphaAt(input.uv + float2(0, px.y)));
                around = max(around, AlphaAt(input.uv + float2(0, -px.y)));
                around = max(around, AlphaAt(input.uv + px));
                around = max(around, AlphaAt(input.uv - px));
                around = max(around, AlphaAt(input.uv + float2(px.x, -px.y)));
                around = max(around, AlphaAt(input.uv + float2(-px.x, px.y)));

                half rim = saturate(around - center * 0.58h);
                half softBody = center * 0.02h;
                half energy = _Intensity * (0.9h + _Pulse * 0.1h);
                half mask = (rim + softBody) * input.color.a;
                half alpha = mask * saturate(energy) * _HaloColor.a;
                return half4(_HaloColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
