Shader "Custom/FluidMetaball"
{
    // Map_IceandSnow glacial water V2 — Game View readable in thin pools.
    // Luminous aqua body + soft deep-teal layer + 2–3 broad stable bands.
    // Top free-surface ribbon only (no full white capsule outline).
    Properties
    {
        _MainTex ("Fluid Density Texture", 2D) = "white" {}
        _WaterColor ("Surface Color", Color) = (0.38, 0.94, 0.97, 0.50)
        _DeepColor ("Deep Color", Color) = (0.03, 0.34, 0.46, 0.92)
        _FoamColor ("Foam Color", Color) = (0.97, 0.995, 1.0, 0.38)
        _Threshold ("Metaball Threshold", Range(0.01, 0.99)) = 0.36
        _OutlineColor ("Edge Highlight", Color) = (0.75, 0.94, 0.97, 0.35)
        _OutlineWidth ("Edge Width", Range(0.0, 0.2)) = 0.022
        _EdgeSoftness ("Edge Softness", Range(0.002, 0.08)) = 0.028
        _DepthStrength ("Depth Strength", Range(0.0, 1.0)) = 0.88
        _HighlightStrength ("Highlight Strength", Range(0.0, 2.0)) = 0.50
        _RippleStrength ("Ripple Distortion", Range(0.0, 1.0)) = 0.02
        _VerticalGradient ("Vertical Gradient", Range(0.0, 1.0)) = 0.90
        _SurfaceBandStrength ("Surface Band", Range(0.0, 2.0)) = 1.65
        _SurfaceBandWidth ("Surface Band Width", Range(0.02, 0.35)) = 0.20
        _NoiseStrength ("Band Strength", Range(0.0, 1.0)) = 0.62
        _NoiseScale ("Band Scale A", Range(1.0, 40.0)) = 2.4
        _NoiseScaleB ("Band Scale B", Range(1.0, 60.0)) = 3.8
        _NoiseSpeed ("Band Drift Speed", Range(0.0, 1.0)) = 0.012
        _FresnelStrength ("Silhouette Fresnel", Range(0.0, 2.0)) = 0.18
        _FresnelPower ("Fresnel Power", Range(0.5, 6.0)) = 2.6
        _FoamCurvature ("Foam Curvature", Range(0.0, 4.0)) = 1.7
        _SparkleStrength ("Sparkle Strength", Range(0.0, 1.0)) = 0.08
        _BodyAlpha ("Body Alpha Scale", Range(0.2, 1.5)) = 1.0
        _BandContrast ("Band Contrast", Range(0.0, 2.0)) = 1.45
        _BottomRimScale ("Bottom/Side Rim Scale", Range(0.0, 1.0)) = 0.12
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;

            float4 _WaterColor;
            float4 _DeepColor;
            float4 _FoamColor;
            float _Threshold;
            float4 _OutlineColor;
            float _OutlineWidth;
            float _EdgeSoftness;
            float _DepthStrength;
            float _HighlightStrength;
            float _RippleStrength;
            float _VerticalGradient;
            float _SurfaceBandStrength;
            float _SurfaceBandWidth;
            float _NoiseStrength;
            float _NoiseScale;
            float _NoiseScaleB;
            float _NoiseSpeed;
            float _FresnelStrength;
            float _FresnelPower;
            float _FoamCurvature;
            float _SparkleStrength;
            float _BodyAlpha;
            float _BandContrast;
            float _BottomRimScale;

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34f, 345.45f));
                p += dot(p, p + 34.345f);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0f - 2.0f * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0f, 0.0f));
                float c = Hash21(i + float2(0.0f, 1.0f));
                float d = Hash21(i + float2(1.0f, 1.0f));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // Domain warp: bends isosurfaces into curved/irregular lobes (not straight glass cuts).
            float2 StableWarp(float2 uv, float time)
            {
                float t = time * _NoiseSpeed;
                float n1 = ValueNoise(uv * 0.80f + float2(t * 0.055f, -t * 0.04f));
                float n2 = ValueNoise(uv * 1.20f + float2(-t * 0.028f, t * 0.048f) + 19.7f);
                float n3 = ValueNoise(uv * 0.45f + float2(t * 0.016f, t * 0.024f) + 7.3f);
                return float2(n1 - 0.5f, n2 - 0.5f) * 0.62f + float2(n3 - 0.5f, n1 - n2) * 0.28f;
            }

            // Low-frequency domain-warped undulations (Map_IceandSnow hand-painted feel).
            // V2 failed with high-freq sin slices; pure mid-noise washed flat — use warped low-freq
            // sin + wide smoothsteps so 2–3 lobes stay readable with feathered boundaries.
            void BroadWaterBands(float2 uv, float time, out float bandA, out float bandB, out float bandC)
            {
                float2 aspect = float2(_MainTex_TexelSize.z * _MainTex_TexelSize.y, 1.0f);
                if (aspect.x < 0.1f) aspect = float2(1.0f, 1.0f);

                float2 p = uv * aspect;
                float2 w1 = StableWarp(p * 0.48f, time);
                float2 w2 = StableWarp(p * 0.72f + float2(2.8f, 1.4f), time * 0.6f);
                float2 q = p + w1 * 1.15f + w2 * 0.75f;

                // Spatial frequency deliberately low (no thinBoost) — few broad lobes per pool.
                float fx = max(0.8f, _NoiseScale) * 0.38f;
                float fy = max(0.8f, _NoiseScaleB) * 0.30f;

                // Phase warp from low-freq noise breaks straight diagonals into curved lobes.
                float phaseA = ValueNoise(q * 0.85f + float2(0.0f, 2.1f)) * 3.4f;
                float a = sin(q.x * fx + q.y * (fx * 0.42f) + phaseA);
                // Wide feather — soft lobe edges, still reaches crest/trough extremes.
                bandA = smoothstep(-0.95f, 0.95f, a);

                float phaseB = ValueNoise(q * 0.70f + float2(8.0f, 3.5f)) * 3.1f;
                float b = sin(q.y * fy - q.x * (fy * 0.38f) + phaseB + 1.6f);
                bandB = smoothstep(-1.0f, 1.0f, b);

                // Asymmetric third lobe: noise-driven blob field (not a third parallel stripe).
                float2 qC = q + StableWarp(q * 0.32f + 11.0f, time * 0.4f) * 0.9f;
                float lobe = ValueNoise(qC * 1.05f + float2(4.2f, 9.1f));
                float lobe2 = ValueNoise(qC * 0.55f + float2(1.3f, 12.0f));
                float cField = sin(lobe * 4.2f + lobe2 * 2.0f + qC.x * 0.85f + qC.y * 0.55f);
                bandC = smoothstep(-0.85f, 0.85f, cField);

                // Moderate contrast for Game View readability (hard glass starts near ~1.7+ with high freq).
                float c = clamp(_BandContrast, 0.75f, 1.55f);
                bandA = saturate((bandA - 0.5f) * c + 0.5f);
                bandB = saturate((bandB - 0.5f) * c + 0.5f);
                bandC = saturate((bandC - 0.5f) * c + 0.5f);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float time = _Time.y;
                float ripple = sin((input.uv.x * 9.0f) + (time * 0.22f)) *
                               sin((input.uv.y * 6.0f) - (time * 0.16f));
                float2 rippleUv = input.uv + float2(ripple, -ripple) * _MainTex_TexelSize.xy * 0.7f * _RippleStrength;

                half4 col = tex2D(_MainTex, rippleUv);
                float density = col.a;

                float soft = max(0.002f, _EdgeSoftness);
                float alpha = smoothstep(_Threshold - soft, _Threshold + soft, density);
                if (alpha <= 0.001f)
                    discard;

                float sampleStep = 1.6f;
                float2 ts = _MainTex_TexelSize.xy * sampleStep;
                float dL = tex2D(_MainTex, rippleUv + float2(-ts.x, 0)).a;
                float dR = tex2D(_MainTex, rippleUv + float2( ts.x, 0)).a;
                float dD = tex2D(_MainTex, rippleUv + float2(0, -ts.y)).a;
                float dU = tex2D(_MainTex, rippleUv + float2(0,  ts.y)).a;
                float dx = dR - dL;
                float dy = dU - dD;
                float2 grad = float2(dx, dy);
                float gradLen = length(grad);
                float edgeEnergy = saturate(gradLen * 3.0f);
                float2 n = gradLen > 1e-5f ? grad / gradLen : float2(0, 1);

                float nearRim = 1.0f - saturate((density - _Threshold) / max(0.0001f, _OutlineWidth + soft * 2.0f));

                // Free-surface direction: density falls upward at top of liquid.
                float freeSurfaceDir = saturate(-n.y);
                float bottomDir = saturate(n.y);
                float sideDir = saturate(1.0f - abs(n.y));

                // --- Depth body: denser = deeper teal; bottom of silhouette darker ---
                float densDepth = saturate((density - _Threshold) * _DepthStrength * 2.8f);
                float verticalBias = saturate(1.0f - input.uv.y);
                float bottomBias = saturate(bottomDir * 0.65f + sideDir * 0.15f + verticalBias * 0.45f);
                float depthBlend = saturate(lerp(densDepth, max(densDepth, bottomBias), _VerticalGradient));
                // Interior of body (away from rim) pulls deeper so thin pools still show cool core.
                float interior = smoothstep(_Threshold + soft, _Threshold + 0.32f, density);
                depthBlend = saturate(depthBlend * 0.55f + interior * 0.45f * _DepthStrength);

                half3 surfaceRgb = _WaterColor.rgb;
                half3 deepRgb = _DeepColor.rgb;
                half3 finalRgb = lerp(surfaceRgb, deepRgb, depthBlend);

                // Keep luminous glacial aqua — lift midtones so body never muddies gray.
                finalRgb = saturate(finalRgb + float3(0.00f, 0.06f, 0.05f) * (1.0f - depthBlend));

                // --- 2–3 broad internal tonal / caustic bands (dominant readable structure) ---
                float bA, bB, bC;
                BroadWaterBands(rippleUv, time, bA, bB, bC);
                // Apply bands across nearly the whole liquid body (thin pools have little "deep interior").
                float bodyMask = smoothstep(_Threshold - soft * 0.5f, _Threshold + soft * 1.2f, density);

                // Crest: luminous cyan. Trough: soft deep teal. Clear chroma delta, soft edges.
                float3 crest = float3(0.62f, 0.99f, 1.00f);
                float3 midAqua = float3(0.10f, 0.62f, 0.72f);
                float3 trough = float3(0.01f, 0.18f, 0.30f);

                float bandMix = saturate(bA * 0.46f + bB * 0.33f + bC * 0.28f);
                // Overlapping wide smoothsteps → 2–3 feathered tonal regions (no hard seams).
                float bandStepped = smoothstep(0.10f, 0.38f, bandMix) * 0.36f
                                  + smoothstep(0.32f, 0.64f, bandMix) * 0.36f
                                  + smoothstep(0.54f, 0.88f, bandMix) * 0.28f;
                // Subtle internal variation inside lobes (low-freq only — not periodic glass).
                float micro = ValueNoise(rippleUv * 1.35f + float2(time * _NoiseSpeed * 0.10f, 0.4f));
                bandStepped = saturate(bandStepped + (micro - 0.5f) * 0.09f);
                float3 bandCol = lerp(trough, crest, bandStepped);
                bandCol = lerp(bandCol, midAqua, 0.12f);

                // Readable painterly structure at gameplay scale.
                float bandAmt = bodyMask * saturate(_NoiseStrength * 1.08f);
                finalRgb = lerp(finalRgb, bandCol, bandAmt * 0.92f);
                float crestMask = smoothstep(0.38f, 0.86f, bandStepped) * bodyMask;
                finalRgb = saturate(finalRgb + crest * crestMask * _NoiseStrength * 0.26f);
                float troughMask = (1.0f - smoothstep(0.08f, 0.44f, bandStepped)) * bodyMask;
                finalRgb = lerp(finalRgb, trough, troughMask * _NoiseStrength * 0.32f);

                // Bottom/side darken using _BottomRimScale so body reads as liquid volume, not sticker.
                float bottomShade = saturate(bottomDir * 0.75f + sideDir * 0.25f + (1.0f - freeSurfaceDir) * 0.2f);
                finalRgb = lerp(finalRgb, deepRgb, bottomShade * _BottomRimScale * 0.85f);

                // --- Top free-surface ribbon only (broken / organic). No full white outline. ---
                float freeSurface = freeSurfaceDir * edgeEnergy;
                float surfaceBand = freeSurface * smoothstep(0.0f, _SurfaceBandWidth, nearRim);
                float ribbonBreak = lerp(0.25f, 1.0f, smoothstep(0.18f, 0.72f, bA));
                surfaceBand = saturate(surfaceBand * _SurfaceBandStrength * ribbonBreak);
                float3 surfaceHi = lerp(surfaceRgb, float3(0.88f, 0.98f, 1.0f), 0.50f);
                finalRgb = lerp(finalRgb, surfaceHi, surfaceBand * 0.75f);

                // NO full-body outline. Soft pale contact only on true free-surface top.
                if (freeSurfaceDir > 0.40f && density < _Threshold + _OutlineWidth * 1.6f)
                {
                    float t = saturate((density - _Threshold) / max(0.0001f, _OutlineWidth + soft));
                    float edgeMix = (1.0f - smoothstep(0.0f, 1.0f, t)) * freeSurfaceDir;
                    edgeMix *= 0.28f * saturate(_OutlineColor.a);
                    finalRgb = lerp(finalRgb, lerp(_OutlineColor.rgb, surfaceRgb, 0.55f), edgeMix);
                }

                // Minimal fresnel — dim, top-weighted; side/bottom scaled by bottom rim.
                float fresnel = pow(saturate(edgeEnergy * nearRim), _FresnelPower) * _FresnelStrength;
                fresnel *= saturate(freeSurfaceDir * 0.95f + _BottomRimScale * 0.15f);
                finalRgb = lerp(finalRgb, lerp(surfaceRgb, float3(0.68f, 0.90f, 0.95f), 0.35f), fresnel * 0.18f);

                // Foam only on high-energy splash crests (top / high curvature).
                float curvature = saturate(edgeEnergy * _FoamCurvature);
                float foamMask = curvature * _HighlightStrength;
                foamMask *= smoothstep(_Threshold - soft, _Threshold + _OutlineWidth, density);
                foamMask *= saturate(freeSurfaceDir * 1.1f + edgeEnergy * 0.25f);
                foamMask *= 1.0f - saturate(depthBlend * 0.9f);
                foamMask = saturate(foamMask);
                float sparkSeed = ValueNoise(rippleUv * 5.5f + float2(time * _NoiseSpeed * 0.2f, 1.7f));
                float sparkle = smoothstep(0.90f, 0.98f, sparkSeed) * curvature * _SparkleStrength * freeSurfaceDir;
                float foamAmt = saturate(foamMask * _FoamColor.a * 0.75f + sparkle);
                finalRgb = lerp(finalRgb, _FoamColor.rgb, foamAmt * 0.48f);

                // Alpha: soft over snow, readable over dark cave.
                float a = lerp(_WaterColor.a, _DeepColor.a, depthBlend);
                a *= alpha * _BodyAlpha;
                a = saturate(a + surfaceBand * 0.04f + crestMask * 0.03f);
                a = max(a, alpha * 0.45f);

                // Guard gray / royal-blue drift.
                float blueDom = saturate(finalRgb.b - finalRgb.g);
                finalRgb.g = saturate(finalRgb.g + blueDom * 0.25f);
                float luma = dot(finalRgb, float3(0.299f, 0.587f, 0.114f));
                // Pull low-chroma pixels toward glacial teal.
                float chroma = length(finalRgb - luma);
                finalRgb = lerp(finalRgb, float3(luma * 0.55f + 0.05f, luma * 0.95f + 0.08f, luma * 1.05f + 0.10f),
                                saturate(0.35f - chroma * 2.0f));

                return half4(finalRgb, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
