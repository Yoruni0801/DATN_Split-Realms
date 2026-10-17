Shader "Custom/2D/Ancient Temple Barrier"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _VeilColor ("Deep Ward Color", Color) = (0.018, 0.18, 0.10, 1)
        [HDR] _EnergyColor ("Living Energy Color", Color) = (0.035, 0.58, 0.31, 1)
        [HDR] _RuneColor ("Ancient Amber", Color) = (0.82, 0.27, 0.025, 1)

        _Opacity ("Master Opacity", Range(0, 1)) = 0.88
        _VeilOpacity ("Veil Opacity", Range(0, 1)) = 0.34
        _Glow ("Glow", Range(0.5, 4)) = 1.10
        _Radius ("Radius", Range(0.42, 0.5)) = 0.486
        _EdgeWidth ("Edge Width", Range(0.003, 0.06)) = 0.020

        _RuneStrength ("Rune Strength", Range(0, 1)) = 0.95
        _RuneSpeed ("Rune Drift", Range(-0.3, 0.3)) = 0.018
        _PulseSpeed ("Ward Pulse Speed", Range(0, 3)) = 0.52
        _FlowSpeed ("Living Flow Speed", Range(0, 3)) = 0.34
        _Distortion ("Organic Distortion", Range(0, 1)) = 0.72
        _MoteStrength ("Ancient Motes", Range(0, 1)) = 0.38
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _VeilColor;
            float4 _EnergyColor;
            float4 _RuneColor;
            float _Opacity;
            float _VeilOpacity;
            float _Glow;
            float _Radius;
            float _EdgeWidth;
            float _RuneStrength;
            float _RuneSpeed;
            float _PulseSpeed;
            float _FlowSpeed;
            float _Distortion;
            float _MoteStrength;

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
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float noise21(float2 p)
            {
                float2 id = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(id);
                float b = hash21(id + float2(1.0, 0.0));
                float c = hash21(id + float2(0.0, 1.0));
                float d = hash21(id + 1.0);
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float ring(float distanceFromCenter, float radius, float halfWidth, float aa)
            {
                return 1.0 - smoothstep(halfWidth, halfWidth + aa,
                                        abs(distanceFromCenter - radius));
            }

            float lineSegment(float2 p, float2 a, float2 b, float width, float aa)
            {
                float2 pa = p - a;
                float2 ba = b - a;
                float h = saturate(dot(pa, ba) / max(dot(ba, ba), 0.000001));
                float distanceToLine = length(pa - ba * h);
                return 1.0 - smoothstep(width, width + aa, distanceToLine);
            }

            float runeGlyph(float2 p, float seed, float aa)
            {
                float variantFork = 1.0 - step(0.34, seed);
                float variantEye = step(0.34, seed) * (1.0 - step(0.68, seed));
                float variantBranch = step(0.68, seed);

                float fork = lineSegment(p, float2(0.0, -0.82), float2(0.0, 0.72), 0.095, aa);
                fork = max(fork, lineSegment(p, float2(0.0, 0.32), float2(-0.48, 0.75), 0.09, aa));
                fork = max(fork, lineSegment(p, float2(0.0, 0.32), float2(0.45, 0.65), 0.09, aa));
                fork = max(fork, lineSegment(p, float2(0.0, -0.20), float2(0.42, -0.48), 0.09, aa));

                float eye = lineSegment(p, float2(-0.52, 0.0), float2(0.0, 0.60), 0.09, aa);
                eye = max(eye, lineSegment(p, float2(0.0, 0.60), float2(0.52, 0.0), 0.09, aa));
                eye = max(eye, lineSegment(p, float2(0.52, 0.0), float2(0.0, -0.60), 0.09, aa));
                eye = max(eye, lineSegment(p, float2(0.0, -0.60), float2(-0.52, 0.0), 0.09, aa));
                eye = max(eye, 1.0 - smoothstep(0.12, 0.12 + aa, length(p)));

                float side = lerp(-1.0, 1.0, step(0.84, frac(seed * 7.91)));
                float branch = lineSegment(p, float2(-0.42, -0.70), float2(0.12, -0.10), 0.095, aa);
                branch = max(branch, lineSegment(p, float2(0.12, -0.10), float2(-0.18, 0.72), 0.095, aa));
                branch = max(branch, lineSegment(p, float2(0.12, -0.10), float2(0.58, 0.28), 0.095, aa));
                branch = max(branch, lineSegment(p, float2(-0.08, 0.20), float2(side * 0.48, 0.58), 0.085, aa));

                return saturate(fork * variantFork + eye * variantEye + branch * variantBranch);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                const float TAU = 6.283185307;
                const float INV_TAU = 0.159154943;
                float time = _Time.y;
                float spriteAlpha = tex2D(_MainTex, i.uv).a;
                float2 p = i.uv - 0.5;
                float distanceFromCenter = length(p);
                float angle = atan2(p.y, p.x);
                float angle01 = angle * INV_TAU + 0.5;
                float2 angleVector = float2(cos(angle), sin(angle));

                float wardPulse = sin(time * _PulseSpeed) * 0.5 + 0.5;
                float edgeWobble = sin(angle * 5.0 + time * 0.13) * 0.46;
                edgeWobble += sin(angle * 9.0 - time * 0.09 + 1.4) * 0.32;
                edgeWobble += sin(angle * 17.0 + time * 0.07 + 3.1) * 0.22;
                edgeWobble *= _Distortion * 0.0080;
                float radius = _Radius + edgeWobble + (wardPulse - 0.5) * 0.0015;
                float aa = max(fwidth(distanceFromCenter) * 1.15, 0.0012);
                float shieldMask = 1.0 - smoothstep(radius, radius + aa * 2.0,
                                                   distanceFromCenter);
                float normalizedRadius = distanceFromCenter / max(radius, 0.001);
                float edgeFacing = pow(saturate(normalizedRadius), 4.2) * shieldMask;

                // One irregular woven boundary replaces the mechanical concentric rings.
                float edgeNoise = noise21(angleVector * 4.6 + time * 0.035);
                float edgeCore = ring(distanceFromCenter, radius - _EdgeWidth * 0.30,
                                      _EdgeWidth * (0.20 + edgeNoise * 0.09), aa);
                float threadRadius = radius - _EdgeWidth * 1.45 +
                    sin(angle * 7.0 - time * 0.19) * _Distortion * 0.0025;
                float wovenThread = ring(distanceFromCenter, threadRadius,
                                         _EdgeWidth * 0.085, aa);
                float threadBreak = noise21(angleVector * 7.3 +
                                            float2(time * 0.028, -time * 0.021));
                wovenThread *= smoothstep(0.36, 0.62, threadBreak);
                float edgeDistance = abs(distanceFromCenter - (radius - _EdgeWidth * 0.45));
                float edgeHalo = (1.0 - smoothstep(_EdgeWidth * 0.8, _EdgeWidth * 5.2,
                                                   edgeDistance)) * shieldMask;
                float edgeFlicker = 0.68 + edgeNoise * 0.22 +
                                    sin(angle * 13.0 + time * 0.72) * 0.10;

                // Slow organic currents crawl over the ward like illuminated roots.
                float2 flowUV = p * 10.5;
                flowUV.x += sin(flowUV.y * 0.62 + time * 0.22) * _Distortion * 0.75;
                flowUV.y -= time * _FlowSpeed;
                float flowA = noise21(flowUV);
                float flowB = noise21(flowUV * 2.05 + float2(5.7, -time * 0.16));
                float rootVeins = pow(saturate(1.0 - abs(flowA * 2.0 - 1.0)), 7.0);
                rootVeins *= smoothstep(0.30, 0.82, flowB);
                rootVeins *= smoothstep(0.18, 0.82, normalizedRadius) * shieldMask;

                float veilCloud = flowA * 0.62 + flowB * 0.38;
                float veil = (_VeilOpacity * 0.20 + veilCloud * _VeilOpacity * 0.11) * shieldMask;
                veil += rootVeins * _VeilOpacity * 0.34;
                veil += edgeFacing * _VeilOpacity * 0.035;

                // A broad reflection gliding over the surface sells the curved protective shell.
                float2 sweepDirection = normalize(float2(-0.72, 0.69));
                float sweepTravel = sin(time * 0.19) * 0.31;
                float sweepDistance = abs(dot(p, sweepDirection) - sweepTravel);
                float surfaceSweep = 1.0 - smoothstep(0.020, 0.105, sweepDistance);
                surfaceSweep *= shieldMask * (0.24 + edgeFacing * 0.76);
                surfaceSweep *= 0.55 + noise21(p * 7.0 + time * 0.04) * 0.45;

                // Broken ward ripples periodically carry energy from the temple to its shell.
                float ripplePhase = sin(distanceFromCenter * 67.0 - time * 0.82 + flowA * 3.2);
                float wardRipple = pow(saturate(ripplePhase * 0.5 + 0.5), 10.0);
                wardRipple *= rootVeins * (0.32 + wardPulse * 0.68);

                // Seven uneven, non-repeating runes drift slowly along the boundary.
                float runeCount = 7.0;
                float runeCellCoord = frac(angle01 + time * _RuneSpeed) * runeCount;
                float runeId = floor(runeCellCoord);
                float runeLocalAngle = (frac(runeCellCoord) - 0.5) * TAU / runeCount;
                float runeSeed = hash21(float2(runeId, 7.31));
                float runeRadius = radius * (0.76 + runeSeed * 0.12);
                runeRadius += sin(time * (0.18 + runeSeed * 0.12) + runeSeed * 14.0) * 0.0018;
                float runeScale = 0.0180 + runeSeed * 0.0050;
                float2 runeP = float2(runeLocalAngle * runeRadius,
                                      distanceFromCenter - runeRadius) / runeScale;
                float rune = runeGlyph(runeP, runeSeed, aa / runeScale);
                float runeGate = step(0.14, runeSeed);
                float runeBreath = 0.58 + 0.42 *
                    (sin(time * (0.46 + runeSeed * 0.22) + runeSeed * 11.0) * 0.5 + 0.5);
                rune *= runeGate * runeBreath * _RuneStrength * shieldMask;

                // Warm energy gathers at the base, visually grounding the barrier around the shrine.
                float bottomFacing = pow(saturate(-p.y / max(distanceFromCenter, 0.001)), 7.0);
                float baseWard = edgeHalo * bottomFacing * (0.52 + wardPulse * 0.48);
                float baseRoots = rootVeins * bottomFacing * smoothstep(0.42, 0.90,
                                                                      normalizedRadius);

                // Sparse firefly-like motes rise through the old magic.
                float2 moteUV = (p + float2(0.0, time * 0.009)) * 27.0;
                float2 moteCell = floor(moteUV);
                float2 moteLocal = frac(moteUV) - 0.5;
                float moteSeed = hash21(moteCell + 19.4);
                float moteGate = step(0.970, moteSeed);
                float moteBlink = sin(time * (0.8 + moteSeed) + moteSeed * 18.0) * 0.5 + 0.5;
                float mote = 1.0 - smoothstep(0.045, 0.14, length(moteLocal));
                mote *= moteGate * moteBlink * _MoteStrength * shieldMask;
                mote *= smoothstep(0.20, 0.88, normalizedRadius);

                float lifeTint = rootVeins * 0.30 + wardPulse * 0.08;
                float3 veilColor = lerp(_VeilColor.rgb, _EnergyColor.rgb, lifeTint);
                float3 edgeColor = lerp(_EnergyColor.rgb, _RuneColor.rgb,
                                        baseWard * 0.58 + wardRipple * 0.20);

                float alpha = veil;
                alpha += edgeHalo * 0.065;
                alpha += edgeCore * edgeFlicker * 0.72 + wovenThread * 0.26;
                alpha += rootVeins * 0.16 + surfaceSweep * 0.11 + wardRipple * 0.14;
                alpha += rune * 0.82 + baseWard * 0.36 + baseRoots * 0.18;
                alpha += mote * 0.62;
                alpha = saturate(alpha * _Opacity) * spriteAlpha * i.color.a;

                float3 color = veilColor * (0.58 + veilCloud * 0.24);
                color += _EnergyColor.rgb * (rootVeins * 0.68 + surfaceSweep * 0.28);
                color += edgeColor * (edgeCore * edgeFlicker * 0.92 + wovenThread * 0.34);
                color += _EnergyColor.rgb * wardRipple * 0.52;
                color += _RuneColor.rgb * (rune * 1.24 + baseWard * 0.74 + baseRoots * 0.44);
                color += lerp(_EnergyColor.rgb, _RuneColor.rgb, moteSeed) * mote * 1.15;
                color *= _Glow * i.color.rgb;

                return fixed4(color, alpha);
            }
            ENDCG
        }
    }
}
