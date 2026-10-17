Shader "Custom/SpriteAuraGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        _TintColor ("Màu ảnh gốc", Color) = (1,1,1,1)
        _GlowColor ("Màu phát sáng", Color) = (1.0, 0.85, 0.25, 1.0)

        _InnerGlow ("Sáng bên trong sprite", Range(0, 3)) = 1.0
        _OuterGlow ("Sáng viền ngoài", Range(0, 5)) = 2.0
        _GlowSize ("Độ lan sáng", Range(0.001, 0.08)) = 0.015

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
            fixed4 _GlowColor;

            float _InnerGlow;
            float _OuterGlow;
            float _GlowSize;
            float _PulseSpeed;
            float _PulseAmount;

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

                float2 offset = float2(_GlowSize, _GlowSize);

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

                float outerGlowMask = saturate(glowAlpha - alpha);

                fixed3 spriteColor = tex.rgb;
                fixed3 innerGlow = spriteColor + _GlowColor.rgb * alpha * _InnerGlow * pulse;
                fixed3 outerGlow = _GlowColor.rgb * outerGlowMask * _OuterGlow * pulse;

                fixed4 finalColor;
                finalColor.rgb = innerGlow * alpha + outerGlow;
                finalColor.a = saturate(alpha + outerGlowMask * _OuterGlow);

                return finalColor;
            }
            ENDCG
        }
    }
}