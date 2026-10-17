Shader "Custom/2D/SpriteGlowPulse"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _BaseColor ("Base Color Tint", Color) = (1, 1, 1, 1)

        [Header(Pulse And Flicker)]
        _PulseSpeed ("Pulse Speed", Float) = 2.0
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.3
        _FlickerNoise ("Flicker Noise Amount", Range(0, 1)) = 0.15

        [Header(HDR Emission)]
        _EmissionIntensity ("Emission Intensity", Float) = 3.0

        [Header(Rim Outline Glow)]
        [HDR] _RimColor ("Rim Glow Color", Color) = (2, 2, 2, 1)
        _GlowRimWidth ("Rim Width", Range(0.1, 10.0)) = 2.0
        _RimIntensity ("Rim Intensity", Float) = 2.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            float4 _BaseColor;
            float _PulseSpeed;
            float _PulseAmount;
            float _FlickerNoise;
            float _EmissionIntensity;
            float4 _RimColor;
            float _GlowRimWidth;
            float _RimIntensity;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            float Hash21(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 sprite = tex2D(_MainTex, i.uv) * i.color;
                if (sprite.a <= 0.001)
                {
                    return fixed4(0, 0, 0, 0);
                }

                // 1. Base Color
                float3 shiftedRgb = sprite.rgb * _BaseColor.rgb;

                // 2. Pulse & Subtle Flicker Noise
                float sinePulse = 0.5 + 0.5 * sin(_Time.y * _PulseSpeed * 6.283185);
                float noise = Hash21(float2(floor(_Time.y * 15.0), floor(_Time.y * 15.0))) * 2.0 - 1.0;
                float pulseFactor = 1.0 + (sinePulse - 0.5) * _PulseAmount + noise * _FlickerNoise * 0.5;
                pulseFactor = max(0.2, pulseFactor);

                // 3. Rim / Outline Glow via 4-tap Alpha Boundary Detection
                float2 offset = _MainTex_TexelSize.xy * _GlowRimWidth;
                float aU = tex2D(_MainTex, i.uv + float2(0, offset.y)).a;
                float aD = tex2D(_MainTex, i.uv - float2(0, offset.y)).a;
                float aL = tex2D(_MainTex, i.uv - float2(offset.x, 0)).a;
                float aR = tex2D(_MainTex, i.uv + float2(offset.x, 0)).a;
                float avgAlpha = (aU + aD + aL + aR) * 0.25;

                // Rim factor peaks where inner alpha meets edge transparency
                float rimFactor = saturate((sprite.a - avgAlpha) * 3.0 + (1.0 - avgAlpha) * sprite.a);

                // 4. Combine Body Emission and Rim Glow (HDR output for Bloom)
                float3 bodyEmission = shiftedRgb * pulseFactor * _EmissionIntensity;
                float3 rimGlow = _RimColor.rgb * rimFactor * _RimIntensity * pulseFactor;
                float3 finalRgb = bodyEmission + rimGlow;

                float finalAlpha = sprite.a * _BaseColor.a;
                return fixed4(finalRgb, finalAlpha);
            }
            ENDCG
        }
    }
}
