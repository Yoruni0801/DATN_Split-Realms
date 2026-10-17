Shader "Bao/AshwingCinderShader"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _GlowColor ("Fire Glow Color", Color) = (1,0.3,0,1)
        _HeatSpeed ("Heat Speed", Float) = 3.0
        _CinderThreshold ("Cinder Burning", Range(0, 10)) = 0.0
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
            fixed4 _GlowColor;
            float _HeatSpeed;
            float _CinderThreshold;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            float pseudo_noise(float2 co)
            {
                return frac(sin(dot(co.xy, float2(12.9898, 78.233))) * 43758.5453);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                
                // Very subtle heat distortion to keep the sprite shape clear and readable
                // _CinderThreshold goes from 1.0 (idle) to 8.0 (attacking)
                float attackFactor = saturate((_CinderThreshold - 1.0) / 7.0);
                
                // Distortion is minimal when idle (0.001) and slightly active when attacking (0.004)
                float distStrength = lerp(0.001, 0.004, attackFactor);
                uv.x += sin(uv.y * 12.0 + _Time.y * _HeatSpeed) * distStrength;
                uv.y += cos(uv.x * 10.0 + _Time.y * _HeatSpeed) * (distStrength * 0.8);

                fixed4 c = tex2D(_MainTex, uv) * IN.color;
                if (c.a < 0.01) discard;

                // Noise-based glow pattern
                float noise = pseudo_noise(uv * 10.0 + float2(_Time.x * 1.5, _Time.x * 3.0));
                float glowMask = smoothstep(0.4, 0.85, noise);

                // Glow is very subtle when idle (0.05 brightness) and glows brightly when attacking (1.2)
                float glowIntensity = lerp(0.05, 1.2, attackFactor);
                fixed3 fireGlow = _GlowColor.rgb * glowMask * glowIntensity;

                // Add the fire glow but blend it with the sprite's original color to preserve details
                // This ensures texture details (eyes, feathers, shading) are always visible!
                c.rgb = lerp(c.rgb, c.rgb + fireGlow, 0.4 * c.a);

                // Burning edges (glowing outline) - only prominent when attacking
                float edge = smoothstep(0.15, 0.0, abs(c.a - 0.5));
                c.rgb = lerp(c.rgb, _GlowColor.rgb * 2.0, edge * attackFactor * 0.4);

                c.rgb *= c.a;
                return c;
            }
        ENDCG
        }
    }
}
