Shader "Bao/SubterraneanEnergyShader"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _EnergyColor ("Energy Glow Color", Color) = (1.0, 0.3, 0.0, 1.0)
        _SecondaryColor ("Hot Lava Color", Color) = (1.0, 0.8, 0.0, 1.0)
        _PulseSpeed ("Pulse Speed", Float) = 3.5
        _CrackScale ("Crack Scale", Float) = 12.0
        _Intensity ("Glow Intensity", Range(0, 6)) = 0.0
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
                float2 texcoord  : TEXCOORD0;
            };

            fixed4 _Color;
            sampler2D _MainTex;
            fixed4 _EnergyColor;
            fixed4 _SecondaryColor;
            float _PulseSpeed;
            float _CrackScale;
            float _Intensity;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            // Pseudo-random noise for voronoi/crack cells
            float hash2D(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            // High performance simplified Voronoi/Earth cracks generator
            float earthCracks(float2 p)
            {
                float2 ip = floor(p);
                float2 fp = frac(p);
                float minDist = 1.0;

                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 neighbor = float2(x, y);
                        float2 pointPos = neighbor + float2(hash2D(ip + neighbor), hash2D(ip + neighbor * 1.5)) * 0.5 + 0.25;
                        float d = distance(fp, pointPos);
                        minDist = min(minDist, d);
                    }
                }
                // High contrast crack lines
                return saturate(1.0 - (minDist * 2.2));
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                fixed4 mainColor = tex2D(_MainTex, uv) * IN.color;

                if (mainColor.a <= 0.01)
                    return fixed4(0,0,0,0);

                // Compute pulsing cracks
                float time = _Time.y * _PulseSpeed;
                float pulse = sin(time) * 0.4 + 0.6; // 0.2 to 1.0 range
                
                // Earthy cracking pattern
                float cracks = earthCracks(uv * _CrackScale + float2(time * 0.05, 0.0));
                
                // Secondary layer of high-frequency cracks for internal glowing lines
                float microCracks = earthCracks(uv * _CrackScale * 2.3 - float2(0.0, time * 0.08));
                float combinedCracks = saturate(cracks * 0.7 + microCracks * 0.5);

                // Glow lerp from base energy to hot liquid yellow
                fixed4 glowCol = lerp(_EnergyColor, _SecondaryColor, combinedCracks * pulse);
                fixed4 crackGlow = glowCol * combinedCracks * _Intensity * pulse * mainColor.a;

                // Color tint overlay (subterranean fire inside the shell)
                fixed4 baseGlow = _EnergyColor * (combinedCracks * 0.25) * _Intensity * mainColor.a;

                fixed4 finalColor = mainColor + crackGlow + baseGlow;
                finalColor.rgb *= finalColor.a;

                return finalColor;
            }
        ENDCG
        }
    }
}
