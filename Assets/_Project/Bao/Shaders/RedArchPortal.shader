Shader "Bao/MiniGame/RedArchPortal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        [Header(Color Palette)]
        _ColorA ("Fiery Red/Orange Flame", Color) = (1.0, 0.25, 0.03, 1.0)
        _ColorB ("Deep Crimson Void", Color) = (0.55, 0.02, 0.05, 1.0)
        _ColorCore ("Molten Golden Core", Color) = (1.0, 0.85, 0.25, 1.0)
        _CenterColor ("Abyss Dark Center", Color) = (0.05, 0.0, 0.01, 1.0)

        [Header(Arch Shape)]
        _ArchWidth ("Arch Width Ratio", Range(0.5, 1.5)) = 0.92
        _ArchHeight ("Arch Height Peak", Range(0.5, 1.5)) = 0.96
        _ArchRoundness ("Arch Curve Exponent", Range(1.0, 4.0)) = 2.1
        _EdgeSoftness ("Edge Softness", Range(0.01, 0.3)) = 0.07
        _BottomCutoff ("Bottom Cutoff", Range(0.0, 0.2)) = 0.02

        [Header(Swirl and Flames Dynamics)]
        _Alpha ("Master Alpha", Range(0, 1)) = 1.0
        _Speed ("Vortex Speed", Range(-10, 10)) = 2.8
        _SwirlStrength ("Swirl Strength", Range(0, 20)) = 7.0
        _SpiralAmount ("Spiral Tendrils", Range(1, 30)) = 12.0
        _NoiseScale ("Flame Noise Scale", Range(1, 30)) = 8.5
        _CenterDarkSize ("Center Dark Size", Range(0.01, 0.5)) = 0.16
        _GlowStrength ("Glow Strength", Range(0, 5)) = 2.2
        _FlamePulseSpeed ("Flame Pulse Speed", Range(0, 10)) = 3.5
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

            float4 _ColorA;
            float4 _ColorB;
            float4 _ColorCore;
            float4 _CenterColor;

            float _ArchWidth;
            float _ArchHeight;
            float _ArchRoundness;
            float _EdgeSoftness;
            float _BottomCutoff;

            float _Alpha;
            float _Speed;
            float _SwirlStrength;
            float _SpiralAmount;
            float _NoiseScale;
            float _CenterDarkSize;
            float _GlowStrength;
            float _FlamePulseSpeed;

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

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));

                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(a, b, u.x)
                     + (c - a) * u.y * (1.0 - u.x)
                     + (d - b) * u.x * u.y;
            }

            float fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;

                for (int i = 0; i < 4; i++)
                {
                    value += noise(p) * amplitude;
                    p *= 2.1;
                    amplitude *= 0.48;
                }

                return value;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;

                // 1. TÍNH TOÁN ARCH SHAPE MASK (KHUÔN VÒM CỬA HANG)
                float xNorm = (uv.x - 0.5) / (_ArchWidth * 0.5);
                float absX = abs(xNorm);

                // Đường cong vòm Parabol / Arch Profile
                float archTop = _ArchHeight * max(0.0, 1.0 - pow(saturate(absX), _ArchRoundness));
                
                // Khoảng cách từ pixel hiện tại tới mép vòm trên
                float distToTop = archTop - uv.y;
                float topMask = smoothstep(0.0, _EdgeSoftness, distToTop);

                // Mép đáy và mép hông
                float bottomMask = smoothstep(0.0, _EdgeSoftness, uv.y - _BottomCutoff);
                float sideMask = smoothstep(1.0, 1.0 - _EdgeSoftness, absX);

                float archMask = topMask * bottomMask * sideMask;

                // 2. VORTEX VÀ TIA LỬA XOÁY (FIERY SWIRL DYNAMICS)
                float2 vortexCenter = float2(0.5, 0.45);
                float2 centered = uv - vortexCenter;
                // Co giãn trục Y một chút để tâm xoáy hòa quyện với vòm
                centered.y *= 1.15;

                float r = length(centered);
                float angle = atan2(centered.y, centered.x);

                float timeVal = _Time.y * _Speed;
                float pulse = 1.0 + sin(_Time.y * _FlamePulseSpeed) * 0.06;

                // Xoáy không gian
                float swirlAngle = angle + timeVal + (1.0 - saturate(r)) * _SwirlStrength;

                // Vân xoắn năng lượng lửa
                float spiral = sin(swirlAngle * _SpiralAmount - r * 24.0 + timeVal * 2.2);
                spiral = spiral * 0.5 + 0.5;

                // Chuyển động bốc cháy FBM Noise
                float2 noiseUV;
                noiseUV.x = cos(swirlAngle) * r * _NoiseScale + timeVal * 0.35;
                noiseUV.y = sin(swirlAngle) * r * _NoiseScale - timeVal * 0.3;
                float n = fbm(noiseUV);

                // Tổng hợp cường độ ngọn lửa
                float fireEnergy = saturate(n * 0.75 + spiral * 0.55) * pulse;

                // 3. TÔ MÀU ĐỎ LỬA RỰC RỠ (RED INFERNO PALETTE)
                // Lớp nền chuyển từ Đỏ thẫm sang Đỏ Cam Lửa
                float4 col = lerp(_ColorB, _ColorA, fireEnergy);

                // Điểm nóng vàng rực (Molten Gold Sparks) ở các đường vân xoáy
                float coreSparks = smoothstep(0.65, 0.95, fireEnergy);
                col = lerp(col, _ColorCore, coreSparks * 0.85);

                // Hố đen tâm cổng (Black Hole Core)
                float centerDark = 1.0 - smoothstep(0.01, _CenterDarkSize, r);
                col = lerp(col, _CenterColor, centerDark * 0.92);

                // Quầng sáng viền cổng (Glow Edge)
                float edgeGlow = smoothstep(0.0, _EdgeSoftness * 2.5, distToTop) * (1.0 - smoothstep(_EdgeSoftness * 2.5, _EdgeSoftness * 5.0, distToTop));
                col.rgb += _ColorA.rgb * edgeGlow * _GlowStrength;

                // 4. TÍNH TOÁN ĐỘ TRONG SUỐT ALPHA
                float alpha = archMask * saturate(0.35 + fireEnergy * 0.85) * _Alpha;
                
                // Tâm hố đen vẫn giữ độ tối rõ nét
                alpha = max(alpha, archMask * centerDark * _Alpha);

                // Nhân alpha texture nếu có
                fixed4 tex = tex2D(_MainTex, uv);
                if (tex.a > 0.01)
                {
                    alpha *= tex.a;
                }

                col.a = alpha;
                col *= i.color;

                return col;
            }
            ENDCG
        }
    }
}
