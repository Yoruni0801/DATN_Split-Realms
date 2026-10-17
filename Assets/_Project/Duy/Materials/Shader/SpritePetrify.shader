Shader "Custom/2D/SpritePetrify"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _PetrifyAmount ("Petrify Amount", Range(0,1)) = 0

        _StoneColor ("Stone Color", Color) = (0.55, 0.55, 0.52, 1)
        _DarkStoneColor ("Dark Stone Color", Color) = (0.23, 0.23, 0.22, 1)
        _CrackColor ("Crack Color", Color) = (0.08, 0.08, 0.08, 1)

        _NoiseScale ("Noise Scale", Float) = 35
        _NoiseStrength ("Noise Strength", Range(0,1)) = 0.35

        _CrackScale ("Crack Scale", Float) = 18
        _CrackStrength ("Crack Strength", Range(0,1)) = 0.7
        _CrackWidth ("Crack Width", Range(0.001,0.2)) = 0.035

        _EdgeSoftness ("Petrify Edge Softness", Range(0.001,0.3)) = 0.12
        _Seed ("Random Seed", Float) = 0
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
            fixed4 _Color;

            float _PetrifyAmount;

            fixed4 _StoneColor;
            fixed4 _DarkStoneColor;
            fixed4 _CrackColor;

            float _NoiseScale;
            float _NoiseStrength;
            float _CrackScale;
            float _CrackStrength;
            float _CrackWidth;
            float _EdgeSoftness;
            float _Seed;

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 worldPos : TEXCOORD1;
            };

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));

                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(
                    lerp(a, b, u.x),
                    lerp(c, d, u.x),
                    u.y
                );
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color * _Color;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xy;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv) * i.color;

                if (tex.a <= 0.001)
                    discard;

                float amount = saturate(_PetrifyAmount);

                float grainNoise = noise(i.worldPos * _NoiseScale + _Seed);
                float crackNoise = noise(i.worldPos * _CrackScale + _Seed * 3.17);

                // Hóa đá từ dưới lên trên, có nhiễu nhẹ ở mép
                float noisyY = i.uv.y + (grainNoise - 0.5) * 0.12;
                float progress = amount * (1.0 + _EdgeSoftness * 2.0) - _EdgeSoftness;
                float stoneMask = 1.0 - smoothstep(progress - _EdgeSoftness, progress + _EdgeSoftness, noisyY);
                stoneMask = saturate(stoneMask);

                float luminance = dot(tex.rgb, float3(0.299, 0.587, 0.114));

                float grain = (grainNoise - 0.5) * _NoiseStrength;
                float stoneValue = saturate(luminance + grain);

                float3 stoneRGB = lerp(_DarkStoneColor.rgb, _StoneColor.rgb, stoneValue);

                // Tạo vết nứt mảnh
                float crackLine = 1.0 - smoothstep(0.0, _CrackWidth, abs(crackNoise - 0.5));
                crackLine *= _CrackStrength;

                float3 petrifiedRGB = lerp(stoneRGB, _CrackColor.rgb, crackLine);

                float3 finalRGB = lerp(tex.rgb, petrifiedRGB, stoneMask);

                return fixed4(finalRGB, tex.a);
            }
            ENDCG
        }
    }
}