Shader "Bao/MoonflutterAstralShader"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _AstralGlow ("Pink Blue Shimmer", Color) = (0.9,0.3,0.8,1)
        _GlowIntensity ("Glow Intensity", Range(0, 10)) = 0.0
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
        Blend One OneMinusSrcAlpha

        Pass
        {
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
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
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _AstralGlow;
            float _GlowIntensity;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                fixed4 c = tex2D(_MainTex, uv) * IN.color;
                if (c.a < 0.01) discard;

                // Flowing holographic spectral color shifting
                float hueShift = sin(uv.x * 5.0 + uv.y * 5.0 + _Time.y * 2.0) * 0.5 + 0.5;
                fixed3 astralPink = _AstralGlow.rgb;
                fixed3 astralCyan = fixed3(0.2, 0.8, 1.0);
                fixed3 iridescentColor = lerp(astralPink, astralCyan, hueShift);

                // Iridescent wing scale shimmer overlay
                c.rgb += iridescentColor * saturate(_GlowIntensity * 0.3) * c.a;

                // Pulsing light burst lines (like starry rays)
                float pulse = abs(sin(_Time.y * 6.0));
                float flare = smoothstep(0.48, 0.52, sin(uv.x * 20.0 + uv.y * 10.0));
                c.rgb += iridescentColor * flare * pulse * saturate(_GlowIntensity * 0.5);

                // Astral outline glow
                float edge = smoothstep(0.35, 0.0, abs(c.a - 0.5));
                c.rgb += iridescentColor * edge * _GlowIntensity * 0.5;

                c.rgb *= c.a;
                return c;
            }
        ENDCG
        }
    }
}
