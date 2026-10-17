Shader "Bao/Phase4/Magma Bomb Surface"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _Color ("Tint", Color) = (1,1,1,1)
        [HDR] _HotColor ("Molten Emission", Color) = (1,0.24,0.015,1)
        _EmissionIntensity ("Emission Intensity", Range(0,8)) = 2.8
        _HotThreshold ("Hot Threshold", Range(0,1)) = 0.46
        _StoneContrast ("Stone Contrast", Range(0.5,2)) = 1.08
        _PulseSpeed ("Pulse Speed", Range(0,12)) = 5.5
        _PulseAmount ("Pulse Amount", Range(0,0.5)) = 0.12
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "CanUseSpriteAtlas"="True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "MagmaBombSurface"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _HotColor;
                float4 _MainTex_ST;
                half _EmissionIntensity;
                half _HotThreshold;
                half _StoneContrast;
                half _PulseSpeed;
                half _PulseAmount;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half luminance = dot(tex.rgb, half3(0.299h, 0.587h, 0.114h));
                half moltenMask = smoothstep(_HotThreshold, 0.92h, max(tex.r, luminance));
                moltenMask *= smoothstep(0.08h, 0.5h, tex.g + tex.r * 0.35h);

                half pulse = 1.0h + sin(_Time.y * _PulseSpeed + input.uv.x * 8.0h - input.uv.y * 6.0h)
                    * _PulseAmount;
                half3 stone = pow(max(tex.rgb, 0.001h), _StoneContrast);
                half3 molten = _HotColor.rgb * moltenMask * _EmissionIntensity * pulse;

                half3 finalColor = (stone * _Color.rgb + molten) * input.color.rgb;
                return half4(finalColor, tex.a * _Color.a * input.color.a);
            }
            ENDHLSL
        }
    }
}
