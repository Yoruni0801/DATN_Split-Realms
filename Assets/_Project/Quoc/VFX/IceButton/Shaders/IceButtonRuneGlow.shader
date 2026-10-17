Shader "DATN/IceButton/RuneGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _GlowColor ("Rune Outer Glow", Color) = (0.03, 0.45, 2.8, 1)
        [HDR] _CoreColor ("Rune White Core", Color) = (1.35, 1.8, 2.2, 1)
        _CyanThreshold ("Cyan Threshold", Range(0, 1)) = 0.34
        _BrightnessThreshold ("Brightness Threshold", Range(0, 1)) = 0.48
        _Intensity ("Intensity", Range(0, 5)) = 1
        _Charge ("Charge", Range(0, 1)) = 0
        _Pulse ("Pulse", Range(0, 1)) = 0
        _ScanSpeed ("Scan Speed", Float) = 0.65
        _Color ("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+20"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        ZWrite Off
        Blend One One

        Pass
        {
            Name "IceRuneGlow"
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
                float4 _GlowColor;
                float4 _CoreColor;
                float4 _Color;
                float _CyanThreshold;
                float _BrightnessThreshold;
                float _Intensity;
                float _Charge;
                float _Pulse;
                float _ScanSpeed;
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
                half cyanDominance = min(source.g, source.b) - source.r * 0.62h;
                half cyanMask = smoothstep(_CyanThreshold, _CyanThreshold + 0.24h, cyanDominance);
                half brightness = max(source.g, source.b);
                half brightMask = smoothstep(_BrightnessThreshold, min(1.0h, _BrightnessThreshold + 0.28h), brightness);

                half scan = 0.76h + 0.24h * sin((input.uv.x * 8.0h + input.uv.y * 26.0h) - _Time.y * _ScanSpeed * 6.28318h);
                half chargeFront = smoothstep(_Charge - 0.22h, _Charge + 0.05h, 1.0h - input.uv.y);
                half chargeLevel = lerp(0.28h, 1.0h, max(_Charge, chargeFront * _Charge));
                half energy = _Intensity * (0.72h + _Pulse * 0.42h) * chargeLevel * scan;
                half mask = source.a * cyanMask * brightMask;
                half whiteCore = source.a * cyanMask * smoothstep(0.72h, 0.96h, brightness);

                half3 glow = _GlowColor.rgb * energy * mask;
                glow += _CoreColor.rgb * whiteCore * (0.42h + energy * 0.45h);
                return half4(glow, saturate(mask * energy + whiteCore));
            }
            ENDHLSL
        }
    }
}
