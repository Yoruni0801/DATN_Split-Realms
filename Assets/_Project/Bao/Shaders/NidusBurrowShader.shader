Shader "Bao/NidusBurrowShader"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Burrow Dissolve)]
        _BurrowAmount ("Burrow Amount (0=visible 1=underground)", Range(0, 1)) = 0.0
        [HDR] _BurrowEdgeColor ("Burrow Edge Glow", Color) = (0.3, 1.2, 0.0, 1.0)
        _BurrowEdgeWidth ("Edge Width", Range(0, 0.15)) = 0.06
        _BurrowNoiseScale ("Noise Scale", Float) = 8.0
        _BurrowNoiseSpeed ("Noise Speed", Float) = 1.5

        [Header(Underground Shimmer)]
        [HDR] _UndergroundGlow ("Underground Glow", Color) = (0.5, 1.5, 0.1, 1.0)
        _ShimmerSpeed ("Shimmer Speed", Float) = 4.0

        [Header(Emerge)]
        _EmergeAmount ("Emerge Amount (0=underground 1=visible)", Range(0, 1)) = 1.0
        [HDR] _EmergeEdgeColor ("Emerge Edge Glow", Color) = (1.0, 2.0, 0.1, 1.0)
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
            float _BurrowAmount;
            float4 _BurrowEdgeColor;
            float _BurrowEdgeWidth;
            float _BurrowNoiseScale;
            float _BurrowNoiseSpeed;
            float4 _UndergroundGlow;
            float _ShimmerSpeed;
            float _EmergeAmount;
            float4 _EmergeEdgeColor;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            float hash2D(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            float smoothNoise(float2 uv)
            {
                float2 ip = floor(uv);
                float2 fp = frac(uv);
                float a = hash2D(ip);
                float b = hash2D(ip + float2(1.0, 0.0));
                float c2 = hash2D(ip + float2(0.0, 1.0));
                float d = hash2D(ip + float2(1.0, 1.0));
                float2 u = fp * fp * (3.0 - 2.0 * fp);
                return lerp(a, b, u.x) + (c2 - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                fixed4 c = tex2D(_MainTex, uv) * IN.color;
                if (c.a <= 0.01) return fixed4(0,0,0,0);

                // ── Noise mask for organic-looking dissolve ──
                float2 noiseUV = uv * _BurrowNoiseScale + float2(0, _Time.y * _BurrowNoiseSpeed);
                float noiseA = smoothNoise(noiseUV);
                float noiseB = smoothNoise(noiseUV * 1.7 + float2(13.5, 7.3));
                float organicNoise = noiseA * 0.6 + noiseB * 0.4;

                // ── Burrow dissolve: bottom-first (boss digs DOWN) ──
                if (_BurrowAmount > 0.01)
                {
                    // The soil line climbs from the roots toward the cocoon.
                    float dissolveHeight = uv.y * 1.05 + organicNoise * 0.12;
                    float threshold = _BurrowAmount * 1.18;

                    if (dissolveHeight < threshold - _BurrowEdgeWidth)
                    {
                        discard;
                    }
                    else if (dissolveHeight < threshold)
                    {
                        float edgeFactor = (threshold - dissolveHeight) / _BurrowEdgeWidth;
                        float edgePulse = sin(_Time.y * 8.0) * 0.2 + 0.8;
                        c.rgb = _BurrowEdgeColor.rgb * edgeFactor * edgePulse * 2.5;
                        c.a *= edgeFactor;
                    }
                    else
                    {
                        // Rest of body during burrow: add green underground tint
                        float shimmer = sin(_Time.y * _ShimmerSpeed + uv.y * 12.0) * 0.2 + 0.8;
                        c.rgb += _UndergroundGlow.rgb * 0.2 * shimmer * c.a;
                    }
                }

                // ── Emerge: reverse dissolve, top-first (boss bursts UP from ground) ──
                if (_EmergeAmount < 0.99)
                {
                    // Top comes first when emerging
                    float emergeHeight = uv.y * 1.05 + organicNoise * 0.12;
                    float threshold = (1.0 - _EmergeAmount) * 1.14;

                    if (emergeHeight < threshold - _BurrowEdgeWidth)
                    {
                        discard;
                    }
                    else if (emergeHeight < threshold)
                    {
                        float edgeFactor = (threshold - emergeHeight) / _BurrowEdgeWidth;
                        float edgePulse = sin(_Time.y * 12.0) * 0.3 + 0.7;
                        // Emerge edge is more dramatic - hotter, brighter
                        c.rgb = _EmergeEdgeColor.rgb * edgeFactor * edgePulse * 3.5;
                        c.a *= edgeFactor;
                    }
                }

                c.rgb *= c.a;
                return c;
            }
        ENDCG
        }
    }
}
