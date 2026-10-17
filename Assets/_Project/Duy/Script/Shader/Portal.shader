Shader "Custom/2D/Animated Purple Portal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        _ColorA ("Purple Color", Color) = (0.35, 0.05, 1.0, 1)
        _ColorB ("Blue Purple Color", Color) = (0.05, 0.0, 0.7, 1)
        _CenterColor ("Dark Center", Color) = (0, 0, 0.03, 1)

        _Alpha ("Alpha", Range(0, 1)) = 1
        _Radius ("Portal Radius", Range(0.1, 0.8)) = 0.48
        _EdgeSoftness ("Edge Softness", Range(0.01, 0.4)) = 0.18

        _Speed ("Swirl Speed", Range(-10, 10)) = 2.5
        _SwirlStrength ("Swirl Strength", Range(0, 20)) = 8
        _SpiralAmount ("Spiral Amount", Range(1, 30)) = 12
        _NoiseScale ("Noise Scale", Range(1, 30)) = 9

        _CenterDarkSize ("Center Dark Size", Range(0.01, 0.5)) = 0.22
        _GlowStrength ("Glow Strength", Range(0, 3)) = 1.5
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
            float4 _CenterColor;

            float _Alpha;
            float _Radius;
            float _EdgeSoftness;

            float _Speed;
            float _SwirlStrength;
            float _SpiralAmount;
            float _NoiseScale;

            float _CenterDarkSize;
            float _GlowStrength;

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
                    p *= 2.0;
                    amplitude *= 0.5;
                }

                return value;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 centered = uv - 0.5;

                float r = length(centered);
                float angle = atan2(centered.y, centered.x);

                float t = _Time.y * _Speed;

                // Xoáy chính
                float swirlAngle = angle;
                swirlAngle += t;
                swirlAngle += (1.0 - r) * _SwirlStrength;

                // Tạo vân xoáy
                float spiral = sin(swirlAngle * _SpiralAmount - r * 28.0 + t * 2.0);
                spiral = spiral * 0.5 + 0.5;

                // Noise chuyển động
                float2 noiseUV;
                noiseUV.x = cos(swirlAngle) * r * _NoiseScale + t * 0.3;
                noiseUV.y = sin(swirlAngle) * r * _NoiseScale - t * 0.25;

                float n = fbm(noiseUV);

                // Mask hình tròn mềm
                float portalMask = 1.0 - smoothstep(_Radius - _EdgeSoftness, _Radius, r);

                // Vùng tâm tối
                float centerDark = 1.0 - smoothstep(0.02, _CenterDarkSize, r);

                // Cường độ vân
                float energy = saturate(n * 0.8 + spiral * 0.65);

                float4 col = lerp(_ColorB, _ColorA, energy);

                // Làm vùng gần tâm đen hơn
                col = lerp(col, _CenterColor, centerDark);

                // Glow quanh cổng
                float glow = smoothstep(_Radius, _Radius - _EdgeSoftness * 1.8, r);
                col.rgb *= 1.0 + glow * _GlowStrength;

                float alpha = portalMask * saturate(0.35 + energy) * _Alpha;
                alpha = max(alpha, centerDark * _Alpha);

                // Nếu có texture thì nhân thêm, nếu không có thì giữ nguyên alpha
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
