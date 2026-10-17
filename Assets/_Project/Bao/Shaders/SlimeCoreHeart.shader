Shader "Custom/2D/SlimeCoreHeart"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _Color ("Tint", Color) = (1, 1, 1, 1)

        [Header(Core Shape Geometry Mode)]
        [Enum(Triangle_A,0, Sphere_B,1, Cube_C,2)] _SigilShape ("Internal Arcane Sigil Shape", Float) = 0.0
        _SigilIntensity ("Sigil Glow Intensity", Range(0.0, 2.0)) = 0.85
        _SigilSpeed ("Sigil Rotation Speed", Float) = 0.8
        _SigilScale ("Sigil Pattern Scale", Range(0.5, 4.0)) = 1.6

        [Header(Texture Detail and Contrast Preservation)]
        _FacetSharpness ("Facet Contrast / Edge Pop", Range(0.5, 2.5)) = 1.45
        _BaseBrightness ("Base Brightness", Range(0.5, 2.0)) = 1.05
        _Saturation ("Color Saturation", Range(0.5, 2.5)) = 1.55

        [Header(Core Heart Palette)]
        [HDR] _CoreInnerColor ("Inner Core Hotspot (Tâm sáng)", Color) = (1.45, 0.45, 0.15, 1.0)
        [HDR] _CoreOuterColor ("Mantle Crystal (Vỏ ngọc)", Color) = (1.15, 0.1, 0.35, 1.0)
        [HDR] _RimGlowColor ("Rim Aura Glow (Hào quang viền)", Color) = (1.35, 0.2, 0.55, 1.0)
        _RimPower ("Rim Power (Độ hẹp viền)", Range(0.8, 8.0)) = 3.2
        _RimIntensity ("Rim Intensity", Range(0.0, 3.0)) = 0.85

        [Header(3D Pseudo Depth Parallax)]
        _ParallaxDepth ("Internal Chamber Depth", Range(0.0, 0.15)) = 0.04
        _ParallaxSpeed ("Chamber Drift Speed", Float) = 0.6

        [Header(Biological Heartbeat Pulse (Nhip Dap Sinh Hoc))]
        _HeartbeatBPM ("Heartbeat BPM", Range(30.0, 240.0)) = 65.0
        _PulseIntensity ("Pulse Intensity (Biên độ đập)", Range(0.0, 2.0)) = 0.65
        _PulseSpeedMultiplier ("Pulse Speed Multiplier", Float) = 1.0
        _VertexBreath ("Vertex Pulse Expansion", Range(0.0, 0.1)) = 0.02

        [Header(Internal Liquid Refraction and Caustics)]
        _PlasmaSpeed ("Liquid Flow Speed", Float) = 1.8
        _PlasmaScale ("Liquid Swirl Scale", Float) = 6.5
        _PlasmaIntensity ("Internal Caustic Glow", Range(0.0, 1.5)) = 0.35
        _RefractionStrength ("Internal Refraction Distortion", Range(0.0, 0.08)) = 0.015

        [Header(Gemstone Prismatic Glint)]
        [HDR] _GlintColor ("Prismatic Glint Color", Color) = (1.8, 1.7, 1.5, 1.0)
        _GlintSpeed ("Glint Sweep Speed", Float) = 1.4
        _GlintWidth ("Glint Band Width", Range(0.01, 0.25)) = 0.045
        _GlintIntensity ("Glint Brightness", Range(0.0, 3.0)) = 1.15

        [Header(Combat Reactivity)]
        _CombatStress ("Combat Stress (Overdrive Glow)", Range(0.0, 1.0)) = 0.0
        _ChargeSuction ("Charge Energy Suction (Gồng chiêu)", Range(0.0, 1.0)) = 0.0

        [Header(Hit Flash and Warning Overdrive)]
        _HitFlash ("Hit Flash", Range(0.0, 1.0)) = 0.0
        [HDR] _HitFlashColor ("Hit Flash Color", Color) = (2.2, 2.2, 2.2, 1.0)
        _WarningOverdrive ("Warning Overdrive", Range(0.0, 1.0)) = 0.0
        [HDR] _WarningColor ("Warning Alarm Color", Color) = (2.4, 0.8, 0.1, 1.0)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "CanUseSpriteAtlas"="True"
            "RenderPipeline"="UniversalPipeline"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "Forward"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
                float3 localPos     : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _SigilShape;
                float _SigilIntensity;
                float _SigilSpeed;
                float _SigilScale;

                float _FacetSharpness;
                float _BaseBrightness;
                float _Saturation;

                float4 _CoreInnerColor;
                float4 _CoreOuterColor;
                float4 _RimGlowColor;
                float _RimPower;
                float _RimIntensity;

                float _ParallaxDepth;
                float _ParallaxSpeed;

                float _HeartbeatBPM;
                float _PulseIntensity;
                float _PulseSpeedMultiplier;
                float _VertexBreath;

                float _PlasmaSpeed;
                float _PlasmaScale;
                float _PlasmaIntensity;
                float _RefractionStrength;

                float4 _GlintColor;
                float _GlintSpeed;
                float _GlintWidth;
                float _GlintIntensity;

                float _CombatStress;
                float _ChargeSuction;

                float _HitFlash;
                float4 _HitFlashColor;
                float _WarningOverdrive;
                float4 _WarningColor;
            CBUFFER_END

            // Smooth simplex-like organic gradient noise
            float2 hash22(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return -1.0 + 2.0 * frac(sin(p) * 43758.5453123);
            }

            float perlinNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(lerp(dot(hash22(i + float2(0.0, 0.0)), f - float2(0.0, 0.0)),
                                 dot(hash22(i + float2(1.0, 0.0)), f - float2(1.0, 0.0)), u.x),
                            lerp(dot(hash22(i + float2(0.0, 1.0)), f - float2(0.0, 1.0)),
                                 dot(hash22(i + float2(1.0, 1.0)), f - float2(1.0, 1.0)), u.x), u.y);
            }

            // 2-Phase Biological Lub-Dub Heartbeat Curve
            float BiologicalHeartbeat(float t)
            {
                float cycle = frac(t);

                // Lub (Atrium contraction): 0.00 -> 0.16
                float lub = 0.0;
                if (cycle < 0.16)
                {
                    float p = cycle / 0.16;
                    lub = sin(p * 3.14159265) * 0.45;
                }

                // Dub (Ventricle surge): 0.20 -> 0.46
                float dub = 0.0;
                if (cycle >= 0.20 && cycle < 0.46)
                {
                    float p = (cycle - 0.20) / 0.26;
                    float s = sin(p * 3.14159265);
                    dub = s * s * 1.0;
                }

                // Smooth wave return during rest
                float restBreath = sin(cycle * 6.2831853) * 0.08;

                return saturate(lub + dub) + restBreath;
            }

            // Internal Arcane Geometric Sigil Generator
            float EvaluateArcaneSigil(float2 p, float shapeType, float time)
            {
                float angle = time * _SigilSpeed;
                float cosA = cos(angle);
                float sinA = sin(angle);
                float2 rotP = float2(p.x * cosA - p.y * sinA, p.x * sinA + p.y * cosA) * _SigilScale;

                float sigil = 0.0;

                if (shapeType < 0.5)
                {
                    // ─── SHAPE 0: TRIANGLE (Triquetra / Arcane Fire Sigil) ───
                    float r = length(rotP);
                    float theta = atan2(rotP.y, rotP.x);
                    float triWave = cos(theta * 3.0);
                    float triRing = abs(r - (0.40 + triWave * 0.10));
                    float innerTri = abs(max(abs(rotP.x) * 0.866025 + rotP.y * 0.5, -rotP.y) - 0.22);
                    sigil = smoothstep(0.04, 0.005, triRing) * 0.65 + smoothstep(0.03, 0.005, innerTri) * 0.8;
                }
                else if (shapeType < 1.5)
                {
                    // ─── SHAPE 1: SPHERE (Solar Orbital Gyroscope Rings) ───
                    float r = length(rotP);
                    float ring1 = abs(r - 0.42);
                    float ring2 = abs(r - 0.22);
                    float2 ell1 = float2(rotP.x, rotP.y * 2.2);
                    float ring3 = abs(length(ell1) - 0.50);
                    float2 ell2 = float2(rotP.x * 2.2, rotP.y);
                    float ring4 = abs(length(ell2) - 0.50);
                    sigil = smoothstep(0.035, 0.005, ring1) * 0.5 +
                            smoothstep(0.03, 0.005, ring2) * 0.7 +
                            smoothstep(0.035, 0.005, ring3) * 0.45 +
                            smoothstep(0.035, 0.005, ring4) * 0.45;
                }
                else
                {
                    // ─── SHAPE 2: CUBE (Isometric Hypercube / Matrix Lattice) ───
                    float2 grid = abs(rotP);
                    float boxOutline = abs(max(grid.x, grid.y) - 0.38);
                    float boxInner = abs(max(grid.x, grid.y) - 0.18);
                    float diag1 = abs(grid.x - grid.y);
                    sigil = smoothstep(0.035, 0.005, boxOutline) * 0.7 +
                            smoothstep(0.03, 0.005, boxInner) * 0.6 +
                            smoothstep(0.025, 0.005, diag1) * 0.4;
                }

                return saturate(sigil);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 pos = input.positionOS.xyz;

                float effectiveBPM = _HeartbeatBPM * (1.0 + _CombatStress * 0.65);
                float bps = (effectiveBPM / 60.0) * _PulseSpeedMultiplier;
                float beat = BiologicalHeartbeat(_Time.y * bps);

                float2 expandDir = normalize(pos.xy + 0.0001);
                expandDir.y *= 1.15;
                float vertexExpansion = beat * _VertexBreath * _PulseIntensity * (1.0 + _CombatStress * 0.5);

                float suctionContract = _ChargeSuction * 0.05;
                pos.xy += expandDir * (vertexExpansion - suctionContract);

                output.positionCS = TransformObjectToHClip(pos);
                output.localPos = pos;
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float2 uv = input.uv;

                // ══════════════════════════════════════════════════════════════
                // 1. BIOLOGICAL HEARTBEAT RHYTHM & COMBAT STRESS
                // ══════════════════════════════════════════════════
                float effectiveBPM = _HeartbeatBPM * (1.0 + _CombatStress * 0.65);
                float bps = (effectiveBPM / 60.0) * _PulseSpeedMultiplier;
                float beat = BiologicalHeartbeat(_Time.y * bps);
                float pulseMod = 1.0 + (beat * 0.35 * _PulseIntensity) + (_CombatStress * 0.25);

                // ══════════════════════════════════════════════════════════════
                // 2. 3D PSEUDO-DEPTH PARALLAX & REFRACTION CHAMBER
                // ══════════════════════════════════════════════════
                float timeVal = _Time.y * _PlasmaSpeed;
                float2 centeredLocal = (uv - 0.5);
                float2 parallaxDrift = centeredLocal * _ParallaxDepth + float2(sin(_Time.y * _ParallaxSpeed), cos(_Time.y * _ParallaxSpeed * 0.8)) * (_ParallaxDepth * 0.25);

                float2 noiseUV1 = uv * _PlasmaScale + float2(timeVal * 0.35, timeVal * 0.5) + parallaxDrift;
                float2 noiseUV2 = uv * (_PlasmaScale * 1.35) - float2(timeVal * 0.45, -timeVal * 0.25) - parallaxDrift;

                float n1 = perlinNoise(noiseUV1);
                float n2 = perlinNoise(noiseUV2);
                float2 refractionOffset = float2(n1, n2) * _RefractionStrength;

                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + refractionOffset);
                if (texColor.a < 0.01)
                {
                    texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                }
                if (texColor.a < 0.01) discard;

                // ══════════════════════════════════════════════════════════════
                // 3. SPRITE LUMINANCE & FACET DETAIL PRESERVATION
                // ══════════════════════════════════════════════════
                float luma = dot(texColor.rgb, half3(0.299, 0.587, 0.114));

                // Retain dark facet ridges and sharp crevices
                half3 facetTone = (texColor.rgb - 0.5) * _FacetSharpness + 0.5;
                facetTone = saturate(facetTone);

                half3 jewelBase = lerp(half3(luma, luma, luma), facetTone, _Saturation) * _BaseBrightness;

                // ══════════════════════════════════════════════════════════════
                // 4. ARCANE GEOMETRIC SIGIL & PARALLAX CHAMBER CORE
                // ══════════════════════════════════════════════════
                float distFromCenter = length(centeredLocal) * 2.0;
                float innerGlowMask = saturate(1.0 - distFromCenter * 1.25);
                innerGlowMask = pow(innerGlowMask, 1.6);

                // Internal Sigil rendered in deep chamber layer
                float2 sigilCoord = centeredLocal + parallaxDrift * 1.8;
                float sigilPattern = EvaluateArcaneSigil(sigilCoord, _SigilShape, _Time.y);
                half3 sigilGlow = _CoreInnerColor.rgb * (sigilPattern * _SigilIntensity * (0.6 + 0.4 * beat) * luma);

                // Controlled inner hotspot without whiteout
                half3 surgeInnerColor = _CoreInnerColor.rgb * (1.0 + beat * 0.45 * _PulseIntensity + _CombatStress * 0.35);
                half3 crystalLight = lerp(_CoreOuterColor.rgb, surgeInnerColor, innerGlowMask * luma);

                // Internal Caustic shimmer
                float causticPattern = saturate(0.5 + 0.5 * (n1 + n2 * 0.6));
                causticPattern = pow(causticPattern, 2.2);
                half3 internalCaustic = _CoreInnerColor.rgb * (causticPattern * _PlasmaIntensity * smoothstep(0.2, 0.85, luma));

                // Multiply-Overlay blending to retain 100% of original facet shadows and bevel ridges!
                half3 crystalCore = jewelBase * (crystalLight + internalCaustic + sigilGlow) * pulseMod;

                // ══════════════════════════════════════════════════════════════
                // 5. TRANSLUCENT FRESNEL RIM SHEEN
                // ══════════════════════════════════════════════════
                float rimMask = pow(saturate(distFromCenter * 0.95), _RimPower);
                half3 rimSheen = _RimGlowColor.rgb * (rimMask * _RimIntensity * (0.3 + 0.7 * luma));

                // ══════════════════════════════════════════════════
                // 6. PRISMATIC GEMSTONE FACET GLINT SWEEP
                // ══════════════════════════════════════════════════
                float glintPhase = frac(_Time.y * _GlintSpeed * 0.25);
                float glintLine = (uv.x * 0.85 + uv.y * 0.55) - (glintPhase * 2.2 - 0.6);
                float glintFalloff = smoothstep(_GlintWidth, 0.0, abs(glintLine));

                // Prismatic chromatic dispersion (RGB split)
                float glintR = smoothstep(_GlintWidth, 0.0, abs(glintLine - 0.012));
                float glintG = glintFalloff;
                float glintB = smoothstep(_GlintWidth, 0.0, abs(glintLine + 0.012));
                half3 prismaticGlint = half3(glintR, glintG, glintB) * _GlintColor.rgb;

                // Glint only catches on specular ridges and bright facets (luma > 0.45)
                float facetSpecularGlintMask = glintFalloff * smoothstep(0.45, 0.9, luma);
                half3 glintRGB = prismaticGlint * (facetSpecularGlintMask * _GlintIntensity);

                // ══════════════════════════════════════════════════════════════
                // 7. COMPOSITE FINAL CORE TONE WITH PEAK COMPRESSION
                // ══════════════════════════════════════════════════
                half3 finalRGB = crystalCore + rimSheen + glintRGB;

                // Soft tone-map compression to guarantee zero flat white blowout
                finalRGB = finalRGB / (1.0 + finalRGB * 0.2);

                // ══════════════════════════════════════════════════════════════
                // 8. CHARGE ENERGY SUCTION (Concentrated vortex burst)
                // ══════════════════════════════════════════════════
                if (_ChargeSuction > 0.001)
                {
                    float suctionVortex = sin(distFromCenter * 14.0 - _Time.y * 12.0) * 0.5 + 0.5;
                    finalRGB += _CoreInnerColor.rgb * (suctionVortex * _ChargeSuction * 0.6);
                }

                // ══════════════════════════════════════════════════
                // 9. WARNING OVERDRIVE (When preparing to merge)
                // ══════════════════════════════════════════════════
                if (_WarningOverdrive > 0.001)
                {
                    float warningPulse = sin(_Time.y * 18.0) * 0.5 + 0.5;
                    half3 warnCol = _WarningColor.rgb * (0.8 + warningPulse * 1.2);
                    finalRGB = lerp(finalRGB, finalRGB * warnCol + warnCol * 0.4, _WarningOverdrive * (0.4 + 0.6 * warningPulse));
                }

                // ══════════════════════════════════════════════════
                // 10. HIT FLASH
                // ══════════════════════════════════════════════════
                if (_HitFlash > 0.001)
                {
                    finalRGB = lerp(finalRGB, _HitFlashColor.rgb, _HitFlash);
                }

                return half4(finalRGB * input.color.rgb, texColor.a * input.color.a);
            }
            ENDHLSL
        }
    }
    FallBack "Sprites/Default"
}
