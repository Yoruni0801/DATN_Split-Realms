Shader "Bao/ShardclawEarthShader"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _CrystalGlow ("Orange Crystal Glow", Color) = (1,0.5,0,1)
        _BurrowState ("Burrow Amount", Range(0, 10)) = 0.0
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
            fixed4 _CrystalGlow;
            float _BurrowState;

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
                
                // Get the charge factor from _BurrowState (goes from 0.5 up to 7.5 during slam charging)
                float chargeFactor = saturate((_BurrowState - 0.5) / 7.0);

                // Read base color first to isolate crystal areas
                fixed4 baseColor = tex2D(_MainTex, uv);
                
                // --- Premium Lava Crystal Detection ---
                // Detect bright yellow, orange, and red tones of the spikes and wing parts.
                // These have high Red, moderate Green, and low Blue.
                float isCrystal = saturate(baseColor.r * 1.8 - baseColor.b * 2.2); 
                isCrystal *= saturate(baseColor.r - 0.35); // Filter out dark red/shadow areas
                
                // Dynamic Heat Wave Shimmer inside the crystals when charging the attack
                float distortion = sin(uv.y * 20.0 + _Time.y * 12.0) * 0.005 * isCrystal * chargeFactor;
                uv.x += distortion;

                // Re-sample with distorted UVs for the wavy liquid lava look
                fixed4 c = tex2D(_MainTex, uv) * IN.color;
                if (c.a < 0.01) discard;

                // --- Flowing Liquid Lava Animation ---
                // Smooth sine-wave layers flowing diagonally to mimic hot magma movement
                float flow1 = sin(uv.x * 6.0 - _Time.y * 2.5) * cos(uv.y * 6.0 + _Time.y * 1.5);
                float flow2 = cos(uv.x * 12.0 + _Time.y * 3.5) * sin(uv.y * 8.0 - _Time.y * 2.0);
                float lavaFlow = saturate((flow1 * 0.5 + flow2 * 0.5) * 0.5 + 0.5);

                // --- Dynamic Breathing Pulse ---
                // Continuous beautiful breathing pulse for the crystal glow
                float pulse = sin(_Time.y * 3.5) * 0.15 + 0.85;

                // --- Lava Energy Surge ---
                // Base glowing intensity when idle (0.4) surges to intense lava brightness (3.0) when charging
                float glowIntensity = lerp(0.4, 3.5, chargeFactor);
                
                // Combine crystal mask, lava flow animation, and breathing pulse
                fixed3 fireGlow = _CrystalGlow.rgb * isCrystal * glowIntensity * pulse * (lavaFlow * 0.5 + 0.5);

                // Apply premium additive blending specifically on the crystal areas
                c.rgb += fireGlow;

                // --- Super-heated Lava Edges ---
                // Add a hot yellow-white core highlight along the edges of the crystals when charging
                float edgeGlow = smoothstep(0.7, 1.0, isCrystal) * chargeFactor;
                c.rgb += fixed3(1.0, 0.9, 0.5) * edgeGlow * 1.5;

                c.rgb *= c.a;
                return c;
            }
        ENDCG
        }
    }
}
