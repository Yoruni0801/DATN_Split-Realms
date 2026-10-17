Shader "Custom/SpriteDarkAura"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        _TintColor ("Màu ảnh gốc", Color) = (1,1,1,1)

        _DarkColor ("Màu Đen Tối", Color) = (0.05, 0.0, 0.1, 1.0)
        _PurpleColor ("Màu Tím Ác Quỷ", Color) = (0.35, 0.0, 0.6, 1.0)

        _DarkAmount ("Độ ám tối bên trong", Range(0, 1)) = 0.45
        _InnerAura ("Hắc khí bên trong sprite", Range(0, 3)) = 1.0
        _OuterAura ("Hắc khí viền ngoài", Range(0, 5)) = 2.0

        _AuraSize ("Độ lan hắc khí", Range(0.001, 0.08)) = 0.02
        _NoiseScale ("Độ chi tiết khói", Float) = 4.0
        _Speed ("Tốc độ cuộn", Float) = 1.5
        _Intensity ("Độ nhiễu", Float) = 1.5

        _PulseSpeed ("Tốc độ nhấp nháy", Float) = 1.5
        _PulseAmount ("Độ nhấp nháy", Range(0, 1)) = 0.25
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

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;

            fixed4 _TintColor;

            fixed4 _DarkColor;
            fixed4 _PurpleColor;

            float _DarkAmount;
            float _InnerAura;
            float _OuterAura;

            float _AuraSize;
            float _NoiseScale;
            float _Speed;
            float _Intensity;

            float _PulseSpeed;
            float _PulseAmount;

            float hash(float2 p)
            {
                return frac(1e4 * sin(17.0 * p.x + p.y * 0.1) * (0.1 + abs(sin(p.y * 13.0 + p.x))));
            }

            float noise(float2 x)
            {
                float2 i = floor(x);
                float2 f = frac(x);

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
                float v = 0.0;
                v += noise(p * 2.0) * 0.5;
                v += noise(p * 4.0) * 0.25;
                v += noise(p * 8.0) * 0.125;
                v += noise(p * 16.0) * 0.0625;
                return v;
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv) * i.color * _TintColor;

                float alpha = tex.a;

                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;

                // Noise khói cuộn theo UV
                float2 noiseUV = i.uv * _NoiseScale;
                noiseUV.y -= _Time.y * _Speed;
                noiseUV.x += sin(_Time.y * _Speed * 0.5) * 0.2;

                float n = fbm(noiseUV * _Intensity);

                fixed3 auraColor = lerp(_DarkColor.rgb, _PurpleColor.rgb, n);

                // Lấy alpha xung quanh sprite để tạo viền hắc khí theo silhouette
                float2 offset = float2(_AuraSize, _AuraSize);

                float glowAlpha = 0.0;

                glowAlpha += tex2D(_MainTex, i.uv + float2( offset.x, 0)).a;
                glowAlpha += tex2D(_MainTex, i.uv + float2(-offset.x, 0)).a;
                glowAlpha += tex2D(_MainTex, i.uv + float2(0,  offset.y)).a;
                glowAlpha += tex2D(_MainTex, i.uv + float2(0, -offset.y)).a;

                glowAlpha += tex2D(_MainTex, i.uv + float2( offset.x,  offset.y)).a;
                glowAlpha += tex2D(_MainTex, i.uv + float2(-offset.x,  offset.y)).a;
                glowAlpha += tex2D(_MainTex, i.uv + float2( offset.x, -offset.y)).a;
                glowAlpha += tex2D(_MainTex, i.uv + float2(-offset.x, -offset.y)).a;

                glowAlpha /= 8.0;

                // Vùng viền ngoài sprite
                float outerMask = saturate(glowAlpha - alpha);

                // Làm viền bị rách / khói loang bằng noise
                outerMask *= smoothstep(0.15, 0.95, n);
                outerMask *= pulse;

                // Làm sprite bị ám tối bên trong
                fixed3 darkenedSprite = lerp(tex.rgb, tex.rgb * _DarkColor.rgb, _DarkAmount);

                // Hắc khí bên trong thân sprite
                fixed3 innerAura = auraColor * alpha * n * _InnerAura * pulse;

                // Hắc khí ngoài viền
                fixed3 outerAura = auraColor * outerMask * _OuterAura;

                fixed4 finalColor;

                finalColor.rgb = darkenedSprite * alpha + innerAura + outerAura;
                finalColor.a = saturate(alpha + outerMask * _OuterAura);

                return finalColor;
            }
            ENDCG
        }
    }
}