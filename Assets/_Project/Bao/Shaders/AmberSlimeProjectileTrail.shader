Shader "Custom/2D/AmberSlimeProjectileTrail"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite/Trail Texture", 2D) = "white" {}
        [HDR] _CoreColor ("Inner Core Hotspot (Tâm sáng rực)", Color) = (3.5, 3.0, 1.2, 1.0)
        [HDR] _GlowColor ("Amber Glow (Hổ phách phát quang)", Color) = (2.8, 1.4, 0.2, 1.0)
        [HDR] _OuterColor ("Molten Edge (Rìa mật cam lửa)", Color) = (1.8, 0.4, 0.05, 1.0)
        _Intensity ("HDR Glow Intensity", Range(0.5, 5.0)) = 2.4
        _PulseSpeed ("Energy Pulse Speed", Float) = 8.0
        _NoiseDistort ("Heat Haze Distortion", Range(0.0, 0.5)) = 0.12
        _CometFade ("Comet Tail Exponent", Range(0.5, 4.0)) = 1.6
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "CanUseSpriteAtlas"="True"
            "RenderPipeline"="UniversalPipeline"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha // Pre-multiplied / HDR Additive-Alpha hybrid

        Pass
        {
            Name "AmberTrailGlow"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _CoreColor;
                float4 _GlowColor;
                float4 _OuterColor;
                float _Intensity;
                float _PulseSpeed;
                float _NoiseDistort;
                float _CometFade;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                // 1. Uốn lượn năng lượng theo thời gian (Plasma Heat Haze)
                float wave = sin(uv.x * 16.0 - _Time.y * _PulseSpeed) * _NoiseDistort;
                float distortedY = abs((uv.y + wave - 0.5) * 2.0); // 0 ở tâm, 1 ở 2 mép biên vệt đuôi

                // 2. Độ sắc nét của trục lõi (Core Beam Profile)
                float coreMask = saturate(1.0 - distortedY * 1.8);
                coreMask = pow(coreMask, 2.5);

                float auraMask = saturate(1.0 - distortedY);
                auraMask = pow(auraMask, 1.2);

                // 3. Hiệu ứng vuốt nhọn sao chổi theo chiều dài (Comet Gradient)
                float tailFade = saturate(1.0 - uv.x);
                tailFade = pow(tailFade, _CometFade);

                // 4. Kết hợp 3 dải màu HDR: Tâm vàng kim -> Thân hổ phách -> Mép lửa cam
                float3 finalColor = lerp(_OuterColor.rgb, _GlowColor.rgb, auraMask);
                finalColor = lerp(finalColor, _CoreColor.rgb, coreMask);

                // 5. Nhấp nháy năng lượng vi thể (Sparkle Noise)
                float sparkle = Hash21(floor(uv * float2(24.0, 8.0)) + floor(_Time.yy * 12.0));
                finalColor += _CoreColor.rgb * (sparkle * 0.35 * coreMask);

                finalColor *= _Intensity * input.color.rgb;
                float finalAlpha = (auraMask * 0.75 + coreMask * 0.25) * tailFade * input.color.a;

                return float4(finalColor * finalAlpha, finalAlpha);
            }
            ENDHLSL
        }
    }
}
