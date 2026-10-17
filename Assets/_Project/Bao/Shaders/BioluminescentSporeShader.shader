Shader "Bao/BioluminescentSporeShader"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BiolumColor ("Base Biolum Color", Color) = (0.1, 0.9, 0.5, 1.0)
        _GoldColor ("Spore Gold Color", Color) = (0.95, 0.75, 0.1, 1.0)
        _PulseRate ("Heartbeat Pulse Rate", Float) = 3.0
        _NoiseScale ("Organic Noise Scale", Float) = 4.0
        _ChargeGlow ("Charge Glow Power", Range(0, 5)) = 0.0
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
            fixed4 _BiolumColor;
            fixed4 _GoldColor;
            float _PulseRate;
            float _NoiseScale;
            float _ChargeGlow;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            // High compatibility noise generator (No bitwise operations)
            float hash2D(float2 co)
            {
                return frac(sin(dot(co.xy, float2(12.9898, 78.233))) * 43758.5453);
            }

            float bilinearNoise(float2 uv)
            {
                float2 ip = floor(uv);
                float2 fp = frac(uv);
                
                float a = hash2D(ip);
                float b = hash2D(ip + float2(1.0, 0.0));
                float c = hash2D(ip + float2(0.0, 1.0));
                float d = hash2D(ip + float2(1.0, 1.0));

                float2 u = fp * fp * (3.0 - 2.0 * fp);
                return lerp(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                fixed4 mainColor = tex2D(_MainTex, uv) * IN.color;

                if (mainColor.a <= 0.01)
                    return fixed4(0,0,0,0);

                // Bioluminescent double-pump breathing pulse
                float t = _Time.y * _PulseRate;
                float pulse = sin(t) * 0.35 + 0.65;
                
                // Scrolling organic noise (looks like bioluminescent cells moving inside the mushroom cap)
                float2 noiseUV = uv * _NoiseScale;
                noiseUV.y += _Time.y * 0.15; // float upward slowly
                
                float cellNoise = bilinearNoise(noiseUV);
                
                // Blend colors based on cellular noise
                fixed4 biolumColor = lerp(_BiolumColor, _GoldColor, cellNoise * 0.5);

                // Bioluminescence inside the core texture
                fixed4 cellGlow = biolumColor * (cellNoise * pulse * _ChargeGlow) * mainColor.a;

                // Soft background ambient glow
                fixed4 ambientGlow = _BiolumColor * (0.12 * _ChargeGlow) * mainColor.a;

                fixed4 finalColor = mainColor + cellGlow + ambientGlow;
                finalColor.rgb *= finalColor.a;

                return finalColor;
            }
        ENDCG
        }
    }
}
