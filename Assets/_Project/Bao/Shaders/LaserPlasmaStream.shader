Shader "Custom/LaserPlasmaStream"
{
    Properties
    {
        [Header(Color and Energy)]
        [HDR] _NeonColor ("Neon Color", Color) = (1.05, 0.2, 0.62, 1.0)
        [HDR] _CoreColor ("Core Color", Color) = (1.3, 0.85, 0.95, 1.0)
        _AlphaBoost ("Alpha Boost", Range(0.5, 3.0)) = 1.25

        [Header(Stream Flow)]
        _FlowSpeed ("Flow Speed", Range(0.5, 25.0)) = 7.5
        _WaveTiling ("Wave Tiling", Range(1.0, 30.0)) = 9.0
        _CoreNarrowness ("Core Narrowness", Range(2.0, 25.0)) = 9.5
        _PulseSpeed ("Pulse Speed", Range(0.0, 15.0)) = 4.5
        _PulseAmount ("Pulse Amount", Range(0.0, 0.5)) = 0.12
        _JitterIntensity ("Jitter Intensity", Range(0.0, 0.2)) = 0.045

        [Header(Secondary Arcing Lightning)]
        _ArcIntensity ("Lightning Arc Intensity", Range(0.0, 3.0)) = 1.6
        _ArcSpeed ("Lightning Arc Speed", Range(5.0, 40.0)) = 22.0
        _ArcTiling ("Lightning Arc Tiling", Range(2.0, 30.0)) = 12.0
        _ArcWidth ("Lightning Arc Sharpness", Range(5.0, 60.0)) = 28.0

        [Header(Heat Shimmer Wave)]
        _HeatShimmerStrength ("Heat Shimmer Intensity", Range(0.0, 1.0)) = 0.45
        _HeatShimmerSpeed ("Heat Shimmer Speed", Range(1.0, 20.0)) = 8.0
        _HeatShimmerTiling ("Heat Shimmer Tiling", Range(2.0, 40.0)) = 16.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+10"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline" = "UniversalPipeline"
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
            #pragma target 3.0
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

            fixed4 _NeonColor;
            fixed4 _CoreColor;
            half _AlphaBoost;
            half _FlowSpeed;
            half _WaveTiling;
            half _CoreNarrowness;
            half _PulseSpeed;
            half _PulseAmount;
            half _JitterIntensity;

            half _ArcIntensity;
            half _ArcSpeed;
            half _ArcTiling;
            half _ArcWidth;

            half _HeatShimmerStrength;
            half _HeatShimmerSpeed;
            half _HeatShimmerTiling;

            // ── Noise & Hash Helpers ──────────────────────────────
            float hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float noise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = hash12(i);
                float b = hash12(i + float2(1.0, 0.0));
                float c = hash12(i + float2(0.0, 1.0));
                float d = hash12(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int i = 0; i < 3; i++)
                {
                    v += noise2D(p) * a;
                    p *= 2.15;
                    a *= 0.45;
                }
                return v;
            }

            float CalculateArcLightning(float2 uv, float time, float arcOffset)
            {
                float arcTime = time * _ArcSpeed + arcOffset;
                float x = uv.x * _ArcTiling;

                float warp1 = noise2D(float2(x - arcTime * 0.8, arcOffset)) * 2.0 - 1.0;
                float warp2 = noise2D(float2(x * 2.3 + arcTime * 1.2, arcOffset + 15.3)) * 2.0 - 1.0;
                float lightningCenter = 0.5 + (warp1 * 0.28 + warp2 * 0.14);

                float dist = abs(uv.y - lightningCenter);
                float arcLine = exp(-dist * _ArcWidth);

                float flicker = step(0.38, hash12(float2(floor(x * 1.5), floor(arcTime * 2.5))));
                return arcLine * flicker;
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float time = _Time.y;

                // ═══════════════════════════════════════════════
                // 1. PROCEDURAL HEAT SHIMMER WARPING
                // ═══════════════════════════════════════════════
                float heatNoise = fbm(float2(uv.x * _HeatShimmerTiling - time * _HeatShimmerSpeed, uv.y * 6.0));
                uv.y += (heatNoise - 0.5) * 0.08 * _HeatShimmerStrength;

                float distFromCenter = abs(uv.y - 0.5) * 2.0;

                // ═══════════════════════════════════════════════
                // 2. PRIMARY PLASMA STREAM FLOW
                // ═══════════════════════════════════════════════
                float flowUV = uv.x * _WaveTiling - time * _FlowSpeed;
                float wave1 = sin(flowUV) * 0.5 + 0.5;
                float wave2 = cos(flowUV * 1.73 - time * _FlowSpeed * 0.8) * 0.5 + 0.5;
                float wave3 = sin(flowUV * 3.41 + time * _FlowSpeed * 1.2) * 0.5 + 0.5;
                float energyFlow = lerp(0.7, 1.35, wave1 * 0.5 + wave2 * 0.3 + wave3 * 0.2);

                float jitter = sin(time * 38.0 + uv.x * 45.0) * _JitterIntensity;
                float centerOffset = distFromCenter + jitter * (1.0 - distFromCenter);

                float pulse = 1.0 + sin(time * _PulseSpeed) * _PulseAmount;

                float coreIntensity = saturate(1.0 - centerOffset * _CoreNarrowness * pulse);
                coreIntensity = pow(coreIntensity, 1.85) * energyFlow;

                float glowIntensity = saturate(1.0 - centerOffset);
                glowIntensity = pow(glowIntensity, 2.1);

                // ═══════════════════════════════════════════════
                // 3. SECONDARY ARCING LIGHTNING
                // ═══════════════════════════════════════════════
                float arc1 = CalculateArcLightning(uv, time, 0.0);
                float arc2 = CalculateArcLightning(uv, time, 42.17);
                float totalArc = (arc1 + arc2 * 0.8) * _ArcIntensity * (1.0 - coreIntensity * 0.4);

                // ═══════════════════════════════════════════════
                // 4. COLOR COMPOSITION
                // ═══════════════════════════════════════════════
                half4 col = lerp(_NeonColor * glowIntensity, _CoreColor, coreIntensity);
                col.rgb += (_CoreColor.rgb * 1.4 + _NeonColor.rgb * 0.6) * totalArc;

                col.rgb *= energyFlow * _AlphaBoost;
                col.a = saturate((glowIntensity * 0.8 + coreIntensity * 0.9 + totalArc * 0.9) * IN.color.a * _AlphaBoost);

                float capFade = smoothstep(0.0, 0.02, uv.x) * smoothstep(1.0, 0.98, uv.x);
                col *= capFade;

                return col;
            }
            ENDCG
        }
    }
}
