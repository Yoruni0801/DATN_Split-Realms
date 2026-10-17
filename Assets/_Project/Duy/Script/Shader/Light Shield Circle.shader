Shader "Custom/2D/Light Shield Circle"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        _BaseColor ("Base Light Color", Color) = (0.75, 0.95, 1.0, 1)
        _GreenColor ("Gentle Green Color", Color) = (0.35, 1.0, 0.75, 1)

        _Opacity ("Opacity", Range(0,1)) = 0.65
        _Glow ("Glow Intensity", Float) = 1.8

        _Radius ("Shield Radius", Range(0.1,0.8)) = 0.47
        _EdgeSoftness ("Edge Softness", Range(0.001,0.2)) = 0.06
        _RimWidth ("Rim Width", Range(0.001,0.2)) = 0.045
        _RimPower ("Rim Power", Float) = 2.2

        _WaveSpeed ("Wave Speed", Float) = 1.2
        _WaveStrength ("Wave Strength", Range(0,1)) = 0.35
        _WaveScale ("Wave Scale", Float) = 18.0

        _GreenPulseSpeed ("Green Pulse Speed", Float) = 0.35
        _GreenAmount ("Green Amount", Range(0,1)) = 0.45

        _BreathSpeed ("Breath Speed", Float) = 1.0
        _BreathAmount ("Breath Amount", Range(0,0.5)) = 0.12
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
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

            float4 _BaseColor;
            float4 _GreenColor;

            float _Opacity;
            float _Glow;
            float _Radius;
            float _EdgeSoftness;
            float _RimWidth;
            float _RimPower;

            float _WaveSpeed;
            float _WaveStrength;
            float _WaveScale;

            float _GreenPulseSpeed;
            float _GreenAmount;

            float _BreathSpeed;
            float _BreathAmount;

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
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float spriteAlpha = tex2D(_MainTex, i.uv).a;

                float2 uv = i.uv;
                float2 center = float2(0.5, 0.5);
                float2 p = uv - center;

                float dist = length(p);
                float angle = atan2(p.y, p.x);

                float time = _Time.y;

                // Hơi thở nhẹ: khiên phồng/thu nhỏ rất nhẹ
                float breath = sin(time * _BreathSpeed) * _BreathAmount;
                float radius = _Radius + breath * 0.025;

                // Mask hình tròn
                float circleMask = 1.0 - smoothstep(radius, radius + _EdgeSoftness, dist);

                // Viền ngoài sáng
                float rimOuter = 1.0 - smoothstep(radius, radius + _EdgeSoftness, dist);
                float rimInner = 1.0 - smoothstep(radius - _RimWidth, radius, dist);
                float rim = saturate(rimOuter - rimInner);
                rim = pow(rim, 1.0 / max(_RimPower, 0.001));

                // Ánh sáng mềm bên trong
                float innerGlow = 1.0 - saturate(dist / radius);
                innerGlow = pow(innerGlow, 1.6);

                // Sóng năng lượng xoay bên trong khiên
                float wave1 = sin(dist * _WaveScale - time * _WaveSpeed * 4.0);
                float wave2 = sin(angle * 6.0 + time * _WaveSpeed * 1.5);
                float wave = (wave1 * 0.5 + 0.5) * (wave2 * 0.5 + 0.5);
                wave *= _WaveStrength;
                wave *= circleMask;

                // Lâu lâu chuyển xanh nhẹ
                float pulseRaw = sin(time * _GreenPulseSpeed) * 0.5 + 0.5;
                float greenPulse = smoothstep(0.78, 1.0, pulseRaw) * _GreenAmount;

                float3 shieldColor = lerp(_BaseColor.rgb, _GreenColor.rgb, greenPulse);

                // Alpha tổng
                float alpha = 0.0;
                alpha += circleMask * innerGlow * 0.25;
                alpha += rim * 0.9;
                alpha += wave * 0.35;

                alpha *= _Opacity;
                alpha *= spriteAlpha;
                alpha *= i.color.a;

                // Màu tổng
                float3 col = shieldColor;
                col *= _Glow;
                col += shieldColor * rim * 1.2;
                col += shieldColor * wave * 0.6;

                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
}