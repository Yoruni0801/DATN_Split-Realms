Shader "Custom/2D/Temple Ground Circle"
{
    Properties
    {
        _OuterColor ("Outer Ring Color", Color) = (0.20, 0.95, 0.60, 1)
        _InnerColor ("Inner Ring Color", Color) = (1.0, 0.62, 0.16, 1)
        _CoreColor ("Core Glow Color", Color) = (0.75, 1.0, 0.75, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.62
        _SpinSpeed ("Spin Speed", Range(-0.2, 0.2)) = 0.030
        _PulseSpeed ("Pulse Speed", Range(0, 3)) = 0.52
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _OuterColor;
            float4 _InnerColor;
            float4 _CoreColor;
            float _Opacity;
            float _SpinSpeed;
            float _PulseSpeed;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                return frac(p * (p + p));
            }

            float ringBand(float r, float r0, float halfW, float aa)
            {
                return 1.0 - smoothstep(halfW, halfW + aa, abs(r - r0));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                const float TAU = 6.283185307;
                const float INV_TAU = 0.159154943;
                float time = _Time.y;
                float2 p = i.uv - 0.5;
                float r = length(p) * 2.0;          // 0 at center, 1 at quad edge
                float angle01 = atan2(p.y, p.x) * INV_TAU + 0.5;
                float aa = max(fwidth(r) * 1.4, 0.004);
                float pulse = sin(time * _PulseSpeed) * 0.5 + 0.5;

                // ---- outer rune ring: dashed ticks, spinning forward ----
                float a1 = frac(angle01 + time * _SpinSpeed);
                float seg1 = floor(a1 * 34.0);
                float segF1 = frac(a1 * 34.0);
                float len1 = 0.30 + hash11(seg1 + 11.7) * 0.42;
                float dash1 = step(segF1, len1);
                float outer = ringBand(r, 0.88, 0.018, aa) * dash1;
                outer += ringBand(r, 0.94, 0.006, aa) * 0.75;  // thin guide line

                // ---- inner rune ring: counter-spinning ticks ----
                float a2 = frac(angle01 - time * _SpinSpeed * 1.6);
                float seg2 = floor(a2 * 20.0);
                float segF2 = frac(a2 * 20.0);
                float len2 = 0.32 + hash11(seg2 + 4.3) * 0.38;
                float dash2 = step(segF2, len2);
                float inner = ringBand(r, 0.64, 0.024, aa) * dash2;
                inner += ringBand(r, 0.56, 0.006, aa) * 0.60;

                // ---- rotating spokes bridging the rings ----
                float a3 = frac(angle01 + time * _SpinSpeed * 0.5);
                float spokeF = abs(frac(a3 * 6.0) - 0.5) * 2.0;
                float spoke = 1.0 - smoothstep(0.030, 0.075, spokeF);
                spoke *= smoothstep(0.30, 0.42, r) * (1.0 - smoothstep(0.80, 0.88, r));

                // ---- breathing core glow ----
                float core = pow(saturate(1.0 - r * 1.35), 2.2) * (0.55 + pulse * 0.45);

                // soft global edge fade
                float edgeFade = 1.0 - smoothstep(0.92, 1.0, r);

                float3 col = _OuterColor.rgb * (outer + spoke * 0.6);
                col += _InnerColor.rgb * inner;
                col += _CoreColor.rgb * core * 0.8;

                float alpha = (outer * 0.85 + inner * 0.9 + spoke * 0.5 + core * 0.6);
                alpha *= edgeFade * _Opacity * (0.80 + pulse * 0.20);

                return fixed4(col, saturate(alpha));
            }
            ENDCG
        }
    }
}
