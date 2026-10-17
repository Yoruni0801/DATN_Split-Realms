Shader "Custom/2D/PillarRingOutline2D"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Outline Settings)]
        [HDR] _OutlineColor ("Outline Color", Color) = (1.5, 0.2, 0.8, 1.0)
        _OutlineWidth ("Outline Width", Range(0, 16)) = 3.0
        _OutlineSoftness ("Outline Softness", Range(0.01, 1.0)) = 0.25
        _OutlineOnly ("Outline Only (Hide Base)", Range(0, 1)) = 0.0

        [Header(Glow and Bloom)]
        _GlowIntensity ("Glow Intensity (HDR)", Range(1.0, 5.0)) = 2.2
        _InnerGlowWidth ("Inner Glow Width", Range(0, 8)) = 2.0
        [HDR] _InnerGlowColor ("Inner Glow Color", Color) = (1.2, 1.2, 1.2, 0.6)

        [Header(Pulse and Energy Flow)]
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 3.5
        _PulseAmount ("Pulse Amount", Range(0, 0.5)) = 0.18
        _EnergyFlowSpeed ("Energy Flow Speed", Range(0, 10)) = 2.0
        _EnergyShimmer ("Energy Shimmer Intensity", Range(0, 1)) = 0.35

        [HideInInspector] _FlashBoost ("Flash Boost", Range(0, 3)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
            "RenderPipeline"="UniversalPipeline"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            Name "PillarRingOutline"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float2 worldPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;

            fixed4 _OutlineColor;
            float _OutlineWidth;
            float _OutlineSoftness;
            float _OutlineOnly;

            float _GlowIntensity;
            float _InnerGlowWidth;
            fixed4 _InnerGlowColor;

            float _PulseSpeed;
            float _PulseAmount;
            float _EnergyFlowSpeed;
            float _EnergyShimmer;
            float _FlashBoost;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                OUT.worldPos = mul(unity_ObjectToWorld, IN.vertex).xy;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                fixed4 baseCol = tex2D(_MainTex, uv);

                // 16-tap circular sampling for smooth, high-quality outline
                float maxNeighborAlpha = 0.0;
                float innerSampleAlpha = 0.0;
                float dist = _OutlineWidth * _MainTex_TexelSize.xy;
                float innerDist = _InnerGlowWidth * _MainTex_TexelSize.xy;

                static const float2 directions[16] = {
                    float2( 1.0,  0.0), float2( 0.92,  0.38), float2( 0.71,  0.71), float2( 0.38,  0.92),
                    float2( 0.0,  1.0), float2(-0.38,  0.92), float2(-0.71,  0.71), float2(-0.92,  0.38),
                    float2(-1.0,  0.0), float2(-0.92, -0.38), float2(-0.71, -0.71), float2(-0.38, -0.92),
                    float2( 0.0, -1.0), float2( 0.38, -0.92), float2( 0.71, -0.71), float2( 0.92, -0.38)
                };

                [unroll]
                for (int i = 0; i < 16; i++)
                {
                    float2 sampleUV = uv + directions[i] * dist;
                    maxNeighborAlpha = max(maxNeighborAlpha, tex2D(_MainTex, sampleUV).a);

                    float2 innerUV = uv + directions[i] * innerDist;
                    innerSampleAlpha += tex2D(_MainTex, innerUV).a;
                }
                innerSampleAlpha /= 16.0;

                // Tính toán biên viền ngoài (Outer Outline)
                float outlineAlpha = saturate(maxNeighborAlpha - baseCol.a);
                outlineAlpha = smoothstep(0.01, max(0.02, _OutlineSoftness), outlineAlpha);

                // Tính toán quầng viền nội (Inner Glow Rim)
                float innerGlow = saturate(baseCol.a - innerSampleAlpha) * baseCol.a;

                // Hiệu ứng nhịp đập & dòng năng lượng ma thuật chạy quanh vòng tròn
                float timeVal = _Time.y;
                float pulse = 1.0 + sin(timeVal * _PulseSpeed) * _PulseAmount;

                // Năng lượng xoay quanh tâm sprite (uv relative to center)
                float2 centeredUV = uv - 0.5;
                float angle = atan2(centeredUV.y, centeredUV.x);
                float energyWave = sin(angle * 3.0 - timeVal * _EnergyFlowSpeed) * 0.5 + 0.5;
                float shimmer = 1.0 + energyWave * _EnergyShimmer;

                // Tổng hợp cường độ phát sáng
                float totalGlow = _GlowIntensity * pulse * shimmer + _FlashBoost;

                // Màu viền ngoài (HDR Bloom)
                fixed4 outlineOutput = _OutlineColor * totalGlow;
                outlineOutput.a = outlineAlpha * _OutlineColor.a * IN.color.a;

                // Màu nền sprite gốc (Base)
                fixed4 baseOutput = baseCol * IN.color;
                baseOutput.rgb *= (1.0 - _OutlineOnly);

                // Kết hợp viền nội
                baseOutput.rgb += _InnerGlowColor.rgb * innerGlow * totalGlow * 0.6;

                // Kết hợp Premultiplied Alpha Blending
                fixed4 finalColor;
                finalColor.rgb = baseOutput.rgb * baseOutput.a * (1.0 - outlineAlpha) + outlineOutput.rgb * outlineOutput.a;
                finalColor.a = saturate(baseOutput.a * (1.0 - _OutlineOnly) + outlineOutput.a);

                return finalColor;
            }
            ENDCG
        }
    }
}
