Shader "Bao/MiniGame/CaveArchFirePortal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        [Header(COLOR PALETTE)]
        [HDR] _NeonColor ("Soft Magenta Border", Color) = (1.05, 0.2, 0.62, 1.0)
        _FlameColorA ("Wine Energy Highlight", Color) = (0.62, 0.045, 0.2, 1.0)
        _FlameColorB ("Deep Violet Wine", Color) = (0.16, 0.015, 0.2, 1.0)
        _CoreColor ("Magenta Energy Accent", Color) = (0.88, 0.12, 0.48, 1.0)
        _CenterColor ("Abyss Violet Center", Color) = (0.012, 0.002, 0.035, 1.0)
        _FrameColor ("Dark Wine Doorway Frame", Color) = (0.065, 0.012, 0.055, 0.96)

        [Header(ARCH GEOMETRY)]
        _ArchRoundness ("Arch Curve Power", Range(1.5, 4.0)) = 2.05
        _FrameThickness ("Frame Width", Range(0.02, 0.2)) = 0.065
        _NeonThickness ("Neon Line Sharpness", Range(0.003, 0.06)) = 0.012
        _NeonGlowSpread ("Neon Glow Radius", Range(0.01, 0.2)) = 0.035
        _BottomLine ("Bottom Floor Level", Range(0.0, 0.15)) = 0.03
        _ArchP0 ("Apex Curve Pt", Vector) = (0.0, 0.923, 0, 0)
        _ArchP1 ("High Curve Pt", Vector) = (0.430, 0.761, 0, 0)
        _ArchP2 ("Low Curve Pt", Vector) = (0.760, 0.436, 0, 0)
        _ArchP3 ("Base Curve Pt", Vector) = (0.886, 0.030, 0, 0)

        [Header(PARALLAX VOID DEPTH)]
        _VoidParallaxAmount ("Void Parallax Depth", Range(0.0, 0.5)) = 0.18
        _VoidNebulaScale ("Void Nebula Scale", Range(1.0, 20.0)) = 6.0
        _VoidNebulaSpeed ("Void Nebula Drift Speed", Range(0.0, 5.0)) = 0.85
        [HDR] _VoidStarColor ("Void Stars and Stardust", Color) = (1.2, 0.8, 1.4, 1.0)

        [Header(FIRE VORTEX)]
        _Speed ("Vortex Speed", Range(-10, 10)) = 2.8
        _SwirlStrength ("Swirl Intensity", Range(0, 25)) = 7.0
        _SpiralAmount ("Broken Energy Tendrils", Range(1, 30)) = 8.0
        _NoiseScale ("Energy Noise Detail", Range(1, 30)) = 8.0
        _CenterDarkSize ("Center Void Size", Range(0.01, 0.5)) = 0.25
        _FlamePulseSpeed ("Flame Pulse Speed", Range(0, 10)) = 3.2

        [Header(SOLAR PROMINENCES OR TENDRILS)]
        _TendrilReach ("Tendril Reach Outward", Range(0.0, 0.25)) = 0.12
        _TendrilSpeed ("Tendril Licking Speed", Range(1.0, 15.0)) = 6.5
        _TendrilDensity ("Tendril Frequency", Range(2.0, 25.0)) = 10.0

        [Header(GOD RAYS and LIGHT)]
        _RayDensity ("Energy Ray Density", Range(2.0, 30.0)) = 8.0
        _RaySpeed ("Rays Shimmer Speed", Range(-5.0, 5.0)) = 0.9
        _RayStrength ("Rays Intensity", Range(0.0, 3.0)) = 0.32
        _CoreFloodHeight ("Subtle Floor Energy Height", Range(0.1, 1.0)) = 0.18

        [Header(SPARKS and BURST)]
        _SparkleSpeed ("Sparks Drift Speed", Range(0.1, 5.0)) = 2.0
        _SparkleAmount ("Sparks Density", Range(0.0, 3.0)) = 0.38
        _OuterBurstSpread ("Outer Glow Spread", Range(0.0, 0.3)) = 0.045
        _GlowStrength ("Edge Glow Power", Range(0, 5)) = 0.85

        [Header(MASTER)]
        _Alpha ("Master Opacity", Range(0.0, 1.0)) = 1.0
        _OpenProgress ("Open Formation Progress", Range(0.0, 1.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
            "RenderPipeline"="UniversalPipeline"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            float4 _NeonColor;
            float4 _FlameColorA;
            float4 _FlameColorB;
            float4 _CoreColor;
            float4 _CenterColor;
            float4 _FrameColor;

            float _ArchRoundness;
            float _FrameThickness;
            float _NeonThickness;
            float _NeonGlowSpread;
            float _BottomLine;
            float4 _ArchP0;
            float4 _ArchP1;
            float4 _ArchP2;
            float4 _ArchP3;

            float _VoidParallaxAmount;
            float _VoidNebulaScale;
            float _VoidNebulaSpeed;
            float4 _VoidStarColor;

            float _Speed;
            float _SwirlStrength;
            float _SpiralAmount;
            float _NoiseScale;
            float _CenterDarkSize;
            float _FlamePulseSpeed;

            float _TendrilReach;
            float _TendrilSpeed;
            float _TendrilDensity;

            float _RayDensity;
            float _RaySpeed;
            float _RayStrength;
            float _CoreFloodHeight;

            float _SparkleSpeed;
            float _SparkleAmount;
            float _OuterBurstSpread;
            float _GlowStrength;

            float _Alpha;
            float _OpenProgress;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float2 worldPos : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xy;
                return o;
            }

            // ── Hash & Noise Functions ──────────────────────────

            float hash21(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float snoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int j = 0; j < 4; j++)
                {
                    v += snoise(p) * a;
                    p *= 2.1;
                    a *= 0.48;
                }
                return v;
            }

            // ── Arch Geometry ───────────────────────────────────

            float GetArchCurveHeight(float absX)
            {
                float x0 = max(0.0, _ArchP0.x);
                float y0 = _ArchP0.y > 0.001 ? _ArchP0.y : 0.923;

                float x1 = _ArchP1.y > 0.001 ? max(x0 + 0.01, _ArchP1.x) : 0.430;
                float y1 = _ArchP1.y > 0.001 ? _ArchP1.y : 0.761;

                float x2 = _ArchP2.y > 0.001 ? max(x1 + 0.01, _ArchP2.x) : 0.760;
                float y2 = _ArchP2.y > 0.001 ? _ArchP2.y : 0.436;

                float x3 = _ArchP3.y > 0.0001 ? max(x2 + 0.01, _ArchP3.x) : 0.886;
                float y3 = _ArchP3.y > 0.0001 ? _ArchP3.y : 0.030;

                float s0 = 0.0;
                float s1 = (y2 - y0) / max(0.01, x2 - x0);
                float s2 = (y3 - y1) / max(0.01, x3 - x1);
                float s3 = (y3 - y2) / max(0.01, x3 - x2) * 1.5;

                if (absX <= x1)
                {
                    float dx = max(0.001, x1 - x0);
                    float t = absX / dx;
                    float t2 = t * t;
                    float t3 = t2 * t;
                    float h00 = 2.0 * t3 - 3.0 * t2 + 1.0;
                    float h10 = t3 - 2.0 * t2 + t;
                    float h01 = -2.0 * t3 + 3.0 * t2;
                    float h11 = t3 - t2;
                    return h00 * y0 + h10 * (s0 * dx) + h01 * y1 + h11 * (s1 * dx);
                }
                else if (absX <= x2)
                {
                    float dx = max(0.001, x2 - x1);
                    float t = (absX - x1) / dx;
                    float t2 = t * t;
                    float t3 = t2 * t;
                    float h00 = 2.0 * t3 - 3.0 * t2 + 1.0;
                    float h10 = t3 - 2.0 * t2 + t;
                    float h01 = -2.0 * t3 + 3.0 * t2;
                    float h11 = t3 - t2;
                    return h00 * y1 + h10 * (s1 * dx) + h01 * y2 + h11 * (s2 * dx);
                }
                else if (absX <= x3)
                {
                    float dx = max(0.001, x3 - x2);
                    float t = (absX - x2) / dx;
                    float t2 = t * t;
                    float t3 = t2 * t;
                    float h00 = 2.0 * t3 - 3.0 * t2 + 1.0;
                    float h10 = t3 - 2.0 * t2 + t;
                    float h01 = -2.0 * t3 + 3.0 * t2;
                    float h11 = t3 - t2;
                    return h00 * y2 + h10 * (s2 * dx) + h01 * y3 + h11 * (s3 * dx);
                }
                else
                {
                    return y3 + s3 * (absX - x3);
                }
            }

            float2 EvaluateArchDistance(float2 uv, float frameThick)
            {
                float normX = (uv.x - 0.5) * 2.0;
                float absX = abs(normX);

                float archTopInner = GetArchCurveHeight(absX);
                float innerSideLimit = _ArchP3.x > 0.1 ? _ArchP3.x : 0.886;
                float distInnerTop = archTopInner - uv.y;
                float distInnerSide = innerSideLimit - absX;
                float distInnerBottom = uv.y - _BottomLine;
                float distInner = min(distInnerTop, min(distInnerSide, distInnerBottom));

                float archTopOuter = archTopInner + frameThick * 1.15;
                float outerSideLimit = innerSideLimit + frameThick * 0.95;
                float distOuterTop = archTopOuter - uv.y;
                float distOuterSide = outerSideLimit - absX;
                float distOuterBottom = uv.y - max(0.0, _BottomLine - frameThick * 0.4);
                float distOuter = min(distOuterTop, min(distOuterSide, distOuterBottom));

                return float2(distOuter, distInner);
            }

            // ── Solar Prominence / Flame Tendrils Outward ────────
            float EvaluateSolarTendrils(float2 uv, float timeVal, float distOuter)
            {
                if (distOuter > 0.05) return 0.0;

                float normX = (uv.x - 0.5) * 2.0;
                float angle = atan2(uv.y - 0.45, normX);

                float tendrilWave = sin(angle * _TendrilDensity + timeVal * _TendrilSpeed);
                float tendrilNoise = snoise(float2(angle * 4.0, timeVal * 2.0));
                float reach = max(0.001, _TendrilReach * (tendrilWave * 0.5 + 0.5) * (tendrilNoise * 0.5 + 0.5));

                float outsideDist = -distOuter;
                float tendrilMask = smoothstep(reach, 0.0, outsideDist);
                tendrilMask = pow(tendrilMask, 3.0) * step(0.0, outsideDist);

                return tendrilMask;
            }

            // ── Fragment Shader ─────────────────────────────────

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float timeVal = _Time.y;
                float openProg = saturate(_OpenProgress);

                if (openProg <= 0.001)
                {
                    return fixed4(0, 0, 0, 0);
                }

                // ═══════════════════════════════════════════════
                // 0. ARCH PROGRESSION LOGIC
                // ═══════════════════════════════════════════════
                float normX = (uv.x - 0.5) * 2.0;
                float absX = abs(normX);
                float archHeight = saturate((uv.y - _BottomLine) / max(0.01, 0.92 - _BottomLine));
                float archCurveDist = saturate(archHeight * 0.78 + (1.0 - saturate(absX / 0.88)) * 0.22);

                float archTravelLimit = saturate(openProg / 0.55);
                float archEdgeDist = archTravelLimit - archCurveDist;
                float archGrowthMask = smoothstep(-0.06, 0.08, archEdgeDist);
                float archLeadingGlow = exp(-pow(archEdgeDist / 0.045, 2.0)) * saturate(1.0 - openProg);

                float interiorOpenProg = saturate((openProg - 0.35) / 0.65);
                float interiorGrowthMask = smoothstep(0.0, 0.25, interiorOpenProg);

                // ═══════════════════════════════════════════════
                // 1. ARCH GEOMETRY
                // ═══════════════════════════════════════════════
                float2 archDist = EvaluateArchDistance(uv, _FrameThickness);
                float distOuter = archDist.x;
                float distInner = archDist.y;

                float innerMask = smoothstep(-0.005, 0.012, distInner) * interiorGrowthMask;
                float outerMask = smoothstep(-0.005, 0.012, distOuter) * archGrowthMask;
                float frameMask = saturate(outerMask - innerMask);

                // ═══════════════════════════════════════════════
                // 2. PARALLAX VOID DEPTH (Chiều Sâu Hư Không)
                // ═══════════════════════════════════════════════
                float2 camOffset = (_WorldSpaceCameraPos.xy - i.worldPos) * _VoidParallaxAmount;
                float2 voidUV1 = (uv * _VoidNebulaScale + camOffset * 0.4) + float2(timeVal * _VoidNebulaSpeed * 0.1, -timeVal * _VoidNebulaSpeed * 0.08);
                float2 voidUV2 = (uv * (_VoidNebulaScale * 1.6) + camOffset * 0.8) - float2(timeVal * _VoidNebulaSpeed * 0.15, timeVal * _VoidNebulaSpeed * 0.05);

                float nebulaNoise1 = fbm(voidUV1);
                float nebulaNoise2 = fbm(voidUV2 + 18.3);
                float nebula = saturate(nebulaNoise1 * 0.6 + nebulaNoise2 * 0.4);

                float2 starUV = (uv * 32.0 + camOffset * 1.5);
                float stardust = pow(hash21(floor(starUV)), 28.0) * (sin(timeVal * 6.0 + hash21(floor(starUV)) * 20.0) * 0.5 + 0.5);

                float4 voidSpaceCol = lerp(_CenterColor, _FlameColorB, nebula * 0.6);
                voidSpaceCol += _VoidStarColor * stardust * 1.8;

                // ═══════════════════════════════════════════════
                // 3. NEON BORDER
                // ═══════════════════════════════════════════════
                float absDistToNeon = abs(distInner);
                float neonCore = exp(-pow(absDistToNeon / max(0.001, _NeonThickness), 2.0));
                float neonGlow = exp(-pow(absDistToNeon / max(0.001, _NeonGlowSpread), 2.5));
                float neonFlicker = 1.0 + sin(timeVal * 4.0 + uv.y * 10.0) * 0.03;
                float neonInteriorMask = lerp(1.0, 0.3, innerMask);
                float totalNeon = (neonCore * 1.25 + neonGlow * 0.55) * neonFlicker * neonInteriorMask * archGrowthMask;
                totalNeon += archLeadingGlow * 1.8;

                // ═══════════════════════════════════════════════
                // 4. FIRE VORTEX & SOLAR PROMINENCES
                // ═══════════════════════════════════════════════
                float2 vortexCenter = float2(0.5, 0.42);
                float2 centered = uv - vortexCenter;
                centered.y *= 1.12;
                float r = length(centered);
                float angle = atan2(centered.y, centered.x);

                float t = timeVal * _Speed;
                float pulse = 1.0 + sin(timeVal * _FlamePulseSpeed) * 0.07;
                float swirlAngle = angle + t + (1.0 - saturate(r * 1.8)) * _SwirlStrength;

                float angularNoise = fbm(float2(swirlAngle * 0.7, r * 8.0 - t * 0.35));
                float spiralPhase = swirlAngle * _SpiralAmount - r * 18.0 + t * 1.35;
                spiralPhase += (angularNoise - 0.5) * 5.0;
                float spiralWave = sin(spiralPhase) * 0.5 + 0.5;

                float strandBreakup = fbm(float2(swirlAngle * 1.8 + t * 0.18, r * 13.0 - t * 0.45));
                float brokenStrands = smoothstep(0.48, 0.78, spiralWave + (strandBreakup - 0.5) * 0.7);

                float2 noiseUV;
                noiseUV.x = cos(swirlAngle) * r * _NoiseScale + t * 0.22;
                noiseUV.y = sin(swirlAngle) * r * _NoiseScale - t * 0.2;
                float n = fbm(noiseUV);

                float fireEnergy = saturate(n * 0.58 + brokenStrands * 0.34) * pulse;
                float tendrilLick = EvaluateSolarTendrils(uv, timeVal, distOuter) * archGrowthMask;

                // ═══════════════════════════════════════════════
                // 5. GOD RAYS & ENERGY LIGHT
                // ═══════════════════════════════════════════════
                float normInnerX = (uv.x - 0.5) * 2.0;
                float rayAngle = normInnerX * _RayDensity + snoise(float2(normInnerX * 4.0, timeVal * _RaySpeed * 0.5)) * 1.5;
                float rays = sin(rayAngle + timeVal * _RaySpeed) * 0.5 + 0.5;
                rays = pow(rays, 2.8) * _RayStrength;

                float coreH = max(0.05, _CoreFloodHeight);
                float coreHeight = saturate((uv.y - _BottomLine) / coreH);
                float coreGlow = pow(1.0 - coreHeight, 3.5) * 0.24;
                float coreCenter = exp(-pow(normInnerX / 0.62, 2.0));
                float totalCoreLight = coreGlow * coreCenter;

                float interiorEnergy = saturate(fireEnergy * 0.72 + rays * 0.16 + totalCoreLight * 0.12);

                // ═══════════════════════════════════════════════
                // 6. COLOR COMPOSITION
                // ═══════════════════════════════════════════════
                float4 innerCol = lerp(_FlameColorB, _FlameColorA, interiorEnergy);
                float coreSparks = smoothstep(0.58, 0.9, fireEnergy);
                innerCol = lerp(innerCol, _CoreColor, coreSparks * 0.55);

                float centerDark = 1.0 - smoothstep(_CenterDarkSize * 0.12, _CenterDarkSize, r);
                float centerHalo = 1.0 - smoothstep(_CenterDarkSize, _CenterDarkSize * 1.75, r);
                innerCol.rgb *= 1.0 - centerHalo * 0.28;
                innerCol = lerp(innerCol, voidSpaceCol, centerDark * 0.95);

                float4 coreLightColor = float4(0.72, 0.12, 0.48, 1.0);
                innerCol = lerp(innerCol, coreLightColor, saturate(totalCoreLight * 0.18));

                // ═══════════════════════════════════════════════
                // 7. SPARKS & OUTER GLOW
                // ═══════════════════════════════════════════════
                float2 sparkUV = float2(uv.x * 16.0, uv.y * 16.0 - timeVal * _SparkleSpeed);
                float sparks = pow(hash21(floor(sparkUV)), 18.0) * _SparkleAmount;
                sparks *= sin(timeVal * 9.0 + uv.x * 22.0) * 0.5 + 0.5;

                float outerBurst = smoothstep(0.0, -_OuterBurstSpread, distOuter);
                outerBurst = pow(1.0 - outerBurst, 3.5) * snoise(float2(normInnerX * 9.0, timeVal * 2.2)) * 0.6;

                float edgeGlow = smoothstep(0.0, _FrameThickness * 2.5, distInner)
                               * (1.0 - smoothstep(_FrameThickness * 2.5, _FrameThickness * 5.5, distInner));

                // ═══════════════════════════════════════════════
                // 8. FRAME TEXTURE
                // ═══════════════════════════════════════════════
                float stoneNoise = snoise(uv * 28.0) * 0.12 + snoise(uv * 55.0) * 0.06;
                float4 frameCol = _FrameColor;
                frameCol.rgb += stoneNoise;

                float rimLight = smoothstep(0.0, _FrameThickness * 0.6, distOuter)
                               * (1.0 - smoothstep(0.0, _FrameThickness * 0.6, distInner));
                frameCol.rgb += _NeonColor.rgb * rimLight * 0.15;

                // ═══════════════════════════════════════════════
                // 9. FINAL COMPOSITING
                // ═══════════════════════════════════════════════
                float4 finalColor = float4(0, 0, 0, 0);

                finalColor += frameCol * frameMask;
                finalColor += innerCol * innerMask;
                finalColor += _NeonColor * totalNeon;
                finalColor += _NeonColor * (tendrilLick * 1.5);
                finalColor.rgb += _FlameColorA.rgb * edgeGlow * _GlowStrength * innerMask * 0.55;
                finalColor += coreLightColor * sparks * (innerMask + frameMask * 0.2) * 0.55;
                finalColor += _NeonColor * outerBurst * 0.35;

                // ═══════════════════════════════════════════════
                // 10. ALPHA
                // ═══════════════════════════════════════════════
                float masterMask = saturate(outerMask + totalNeon * 0.9 + outerBurst * 0.55 + tendrilLick * 0.85);

                float edgeFadeX = smoothstep(0.0, 0.05, uv.x) * smoothstep(1.0, 0.95, uv.x);
                float edgeFadeY = smoothstep(0.0, 0.03, uv.y) * smoothstep(1.0, 0.97, uv.y);
                float quadSoftBorder = edgeFadeX * edgeFadeY;

                float finalAlpha = masterMask * _Alpha * quadSoftBorder;

                fixed4 tex = tex2D(_MainTex, uv);
                if (tex.a > 0.01)
                {
                    finalAlpha *= tex.a;
                }

                finalColor.a = saturate(finalAlpha);
                finalColor *= i.color;

                return finalColor;
            }
            ENDCG
        }
    }
}
