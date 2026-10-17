Shader "Bao/ChubblooBlizzardShader"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _SnowGlow ("Blizzard Blue Glow", Color) = (0.3,0.7,1,1)
        _SlamLevel ("Slam / Blizzard Level", Range(0, 10)) = 0.0
        _FlashColor ("Flash Color", Color) = (1,1,1,1)
        _FlashAmount ("Flash Amount", Range(0, 1)) = 0.0
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
            fixed4 _SnowGlow;
            float _SlamLevel;
            fixed4 _FlashColor;
            float _FlashAmount;

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

                float attackLevel = saturate((_SlamLevel - 1.0) / 7.5);

                // A permanent snow cap that thickens and brightens during attacks.
                float snowLine = lerp(0.84, 0.48, attackLevel);
                float topSnow = smoothstep(snowLine - 0.12, snowLine + 0.08, uv.y);
                fixed3 snowWhite = fixed3(1.0, 1.0, 1.0);
                float snowStrength = lerp(0.22, 0.72, attackLevel);
                c.rgb = lerp(c.rgb, snowWhite, topSnow * snowStrength);

                // Pulsing freezing core blue glow inside the sprite.
                float corePulse = sin(_Time.y * 5.0) * 0.2 + 0.8;
                float coreMask = smoothstep(0.4, 0.0, length(uv - 0.5));
                float coreStrength = lerp(0.08, 0.75, attackLevel);
                c.rgb += _SnowGlow.rgb * coreMask * corePulse * coreStrength;

                // Ethereal outline glow and an attack-only brightness lift.
                float edge = smoothstep(0.35, 0.0, abs(c.a - 0.5));
                float edgeStrength = lerp(0.08, 0.55, attackLevel);
                c.rgb += _SnowGlow.rgb * edge * edgeStrength;
                c.rgb *= lerp(1.0, 1.28, attackLevel);

                // Apply solid flash on top of everything (preserving alpha)
                c.rgb = lerp(c.rgb, _FlashColor.rgb, _FlashAmount);

                c.rgb *= c.a;
                return c;
            }
        ENDCG
        }
    }
}
