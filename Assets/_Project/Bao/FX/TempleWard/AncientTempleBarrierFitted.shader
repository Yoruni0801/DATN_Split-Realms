Shader "Custom/2D/Ancient Temple Barrier Fitted"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _SDFTex ("Temple SDF", 2D) = "gray" {}
        [HDR] _VeilColor ("Deep Ward Color", Color) = (0.018, 0.18, 0.10, 1)
        [HDR] _EnergyColor ("Living Energy Color", Color) = (0.035, 0.58, 0.31, 1)
        [HDR] _RuneColor ("Ancient Amber", Color) = (0.82, 0.27, 0.025, 1)

        _Opacity ("Master Opacity", Range(0, 1)) = 0.88
        _VeilOpacity ("Veil Opacity", Range(0, 1)) = 0.34
        _Glow ("Glow", Range(0.5, 4)) = 1.10
        _Standoff ("Surface Standoff", Range(0.0, 0.08)) = 0.036
        _SDFRange ("SDF Encode Range", Float) = 0.16
        _EdgeWidth ("Edge Width", Range(0.003, 0.06)) = 0.016
        _EdgeFalloff ("Edge Facing Falloff", Range(0.03, 0.4)) = 0.13
        _VeinDepth ("Vein Depth", Range(0.05, 0.5)) = 0.22

        _RuneStrength ("Rune Strength", Range(0, 1)) = 0.95
        _RuneSpeed ("Rune Bob Speed", Range(0, 2)) = 0.35
        _PulseSpeed ("Ward Pulse Speed", Range(0, 3)) = 0.52
        _FlowSpeed ("Living Flow Speed", Range(0, 3)) = 0.34
        _Distortion ("Organic Distortion", Range(0, 1)) = 0.72
        _MoteStrength ("Ancient Motes", Range(0, 1)) = 0.38

        _Rune0 ("Rune 0 (xy uv, z tangent, w seed)", Vector) = (0, 0, 0, 0)
        _Rune1 ("Rune 1", Vector) = (0, 0, 0, 0)
        _Rune2 ("Rune 2", Vector) = (0, 0, 0, 0)
        _Rune3 ("Rune 3", Vector) = (0, 0, 0, 0)
        _Rune4 ("Rune 4", Vector) = (0, 0, 0, 0)
        _Rune5 ("Rune 5", Vector) = (0, 0, 0, 0)
        _Rune6 ("Rune 6", Vector) = (0, 0, 0, 0)

        _GroundFade ("Ground Fade (y0 y1)", Vector) = (0.025, 0.06, 0, 0)
        _Centroid ("Silhouette Centroid UV", Vector) = (0.493, 0.312, 0, 0)
        _TopDownCut ("Top Down Emergence Cut", Range(0, 1)) = 1.0
        _EmergenceGlow ("Emergence Glow Width", Range(0.01, 0.2)) = 0.06
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
            sampler2D _SDFTex;
            float4 _VeilColor;
            float4 _EnergyColor;
            float4 _RuneColor;
            float _Opacity;
            float _VeilOpacity;
            float _Glow;
            float _Standoff;
            float _SDFRange;
            float _EdgeWidth;
            float _EdgeFalloff;
            float _VeinDepth;
            float _RuneStrength;
            float _RuneSpeed;
            float _PulseSpeed;
            float _FlowSpeed;
            float _Distortion;
            float _MoteStrength;
            float4 _Rune0, _Rune1, _Rune2, _Rune3, _Rune4, _Rune5, _Rune6;
            float4 _GroundFade;
            float4 _Centroid;
            float _TopDownCut;
            float _EmergenceGlow;

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

            float ring(float signedDist, float target, float halfWidth, float aa)
            {
                return 1.0 - smoothstep(halfWidth, halfWidth + aa, abs(signedDist - target));
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

            float runeLayer(float2 uv, float time, float aa)
            {
                float4 runes[7] = { _Rune0, _Rune1, _Rune2, _Rune3, _Rune4, _Rune5, _Rune6 };
                float total = 0.0;
                [unroll]
                for (int i = 0; i < 7; i++)
                {
                    float4 rd = runes[i];
                    float seed = rd.w;
                    // gentle bob along the surface normal
                    float2 nrm = float2(cos(rd.z + 1.5708), sin(rd.z + 1.5708));
                    float2 center = rd.xy + nrm * sin(time * (0.25 + seed * 0.35) * _RuneSpeed * 2.86 + seed * 17.0) * 0.004;
                    float2 rp = uv - center;
                    // rotate into the rune's tangent frame
                    float cs = cos(-rd.z);
                    float sn = sin(-rd.z);
                    rp = float2(rp.x * cs - rp.y * sn, rp.x * sn + rp.y * cs);
                    // tangent frame: x along contour, y along normal -> glyph upright = swap
                    rp = float2(rp.x, rp.y);
                    float scale = 0.0145 + seed * 0.0045;
                    float g = runeGlyph(rp / scale, seed, aa / scale);
                    float breath = 0.58 + 0.42 * (sin(time * (0.46 + seed * 0.22) + seed * 11.0) * 0.5 + 0.5);
                    total = max(total, g * breath);
                }
                return total;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float time = _Time.y;
                float spriteAlpha = tex2D(_MainTex, i.uv).a;
                float2 p = i.uv - 0.5;
                float2 pc = i.uv - _Centroid.xy;
                float angle = atan2(pc.y, pc.x);

                // signed distance to the temple surface (negative inside)
                float sd = (tex2D(_SDFTex, i.uv).r - 0.5) * 2.0 * _SDFRange;

                float wardPulse = sin(time * _PulseSpeed) * 0.5 + 0.5;
                float edgeWobble = sin(angle * 5.0 + time * 0.13) * 0.46;
                edgeWobble += sin(angle * 9.0 - time * 0.09 + 1.4) * 0.32;
                edgeWobble += sin(angle * 17.0 + time * 0.07 + 3.1) * 0.22;
                edgeWobble *= _Distortion * 0.0080;

                // d = 0 on the wobbling barrier surface, negative inside
                float d = sd - _Standoff - edgeWobble - (wardPulse - 0.5) * 0.0015;
                float aa = max(fwidth(d) * 1.15, 0.0012);
                float shieldMask = 1.0 - smoothstep(0.0, aa * 2.0, d);
                float edgeFacing = pow(saturate(1.0 + d / _EdgeFalloff), 4.2) * shieldMask;

                float2 angleVector = float2(cos(angle), sin(angle));
                float edgeNoise = noise21(angleVector * 4.6 + time * 0.035);
                float edgeCore = ring(d, -_EdgeWidth * 0.30,
                                      _EdgeWidth * (0.20 + edgeNoise * 0.09), aa);
                float threadOffset = -_EdgeWidth * 1.45 +
                    sin(angle * 7.0 - time * 0.19) * _Distortion * 0.0025;
                float wovenThread = ring(d, threadOffset, _EdgeWidth * 0.085, aa);
                float threadBreak = noise21(angleVector * 7.3 +
                                            float2(time * 0.028, -time * 0.021));
                wovenThread *= smoothstep(0.36, 0.62, threadBreak);
                float edgeDistance = abs(d + _EdgeWidth * 0.45);
                float edgeHalo = (1.0 - smoothstep(_EdgeWidth * 0.8, _EdgeWidth * 5.2,
                                                   edgeDistance)) * shieldMask;
                float edgeFlicker = 0.68 + edgeNoise * 0.22 +
                                    sin(angle * 13.0 + time * 0.72) * 0.10;

                // organic currents crawling over the ward shell
                float2 flowUV = p * 13.0;
                flowUV.x += sin(flowUV.y * 0.62 + time * 0.22) * _Distortion * 0.75;
                flowUV.y -= time * _FlowSpeed;
                float flowA = noise21(flowUV);
                float flowB = noise21(flowUV * 2.05 + float2(5.7, -time * 0.16));
                float rootVeins = pow(saturate(1.0 - abs(flowA * 2.0 - 1.0)), 7.0);
                rootVeins *= smoothstep(0.30, 0.82, flowB);
                float shellDepth = saturate(1.0 + d / _VeinDepth);
                rootVeins *= smoothstep(0.18, 0.82, shellDepth) * shieldMask;

                float veilCloud = flowA * 0.62 + flowB * 0.38;
                float veil = (_VeilOpacity * 0.20 + veilCloud * _VeilOpacity * 0.11) * shieldMask;
                veil += rootVeins * _VeilOpacity * 0.34;
                veil += edgeFacing * _VeilOpacity * 0.035;

                // gliding reflection over the shell
                float2 sweepDirection = normalize(float2(-0.72, 0.69));
                float sweepTravel = sin(time * 0.19) * 0.31;
                float sweepDistance = abs(dot(p, sweepDirection) - sweepTravel);
                float surfaceSweep = 1.0 - smoothstep(0.020, 0.105, sweepDistance);
                surfaceSweep *= shieldMask * (0.24 + edgeFacing * 0.76);
                surfaceSweep *= 0.55 + noise21(p * 7.0 + time * 0.04) * 0.45;

                // ward ripples flowing outward, parallel to the temple contour
                float ripplePhase = sin(-d * 67.0 - time * 0.82 + flowA * 3.2);
                float wardRipple = pow(saturate(ripplePhase * 0.5 + 0.5), 10.0);
                wardRipple *= rootVeins * (0.32 + wardPulse * 0.68);

                // runes stationed along the real contour
                float rune = runeLayer(i.uv, time, aa) * _RuneStrength * shieldMask;

                // warm energy grounding the barrier at its base
                float bottomFacing = pow(saturate(-pc.y / max(length(pc), 0.001)), 7.0);
                float baseWard = edgeHalo * bottomFacing * (0.52 + wardPulse * 0.48);
                float baseRoots = rootVeins * bottomFacing * smoothstep(0.42, 0.90, shellDepth);

                // sparse motes rising through the old magic
                float2 moteUV = (p + float2(0.0, time * 0.009)) * 34.0;
                float2 moteCell = floor(moteUV);
                float2 moteLocal = frac(moteUV) - 0.5;
                float moteSeed = hash21(moteCell + 19.4);
                float moteGate = step(0.970, moteSeed);
                float moteBlink = sin(time * (0.8 + moteSeed) + moteSeed * 18.0) * 0.5 + 0.5;
                float mote = 1.0 - smoothstep(0.045, 0.14, length(moteLocal));
                mote *= moteGate * moteBlink * _MoteStrength * shieldMask;
                mote *= smoothstep(0.20, 0.88, shellDepth);

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

                // sink softly into the ground line
                alpha *= smoothstep(_GroundFade.x, _GroundFade.y, i.uv.y);

                // Top-down emergence cutoff (1.0 = fully revealed, 0.0 = completely hidden from top)
                // When _TopDownCut is 0, cutoffLine is at 1.0 (top). As it grows to 1, line reaches 0.0 (bottom).
                float cutoffLine = 1.0 - saturate(_TopDownCut);
                float wipeDiff = i.uv.y - cutoffLine;
                // Soft alpha mask without hard line
                float emergenceMask = smoothstep(-0.12, 0.03, wipeDiff);

                // Multi-layered organic falling firefly motes along the leading edge (No hard beam/line)
                float edgeProximity = 1.0 - smoothstep(0.0, 0.22, abs(wipeDiff));

                // Layer 1: Medium floating firefly motes falling gently
                float2 fireflyUV1 = float2(i.uv.x * 32.0 + sin(time * 0.8 + i.uv.y * 14.0) * 0.7, (i.uv.y - cutoffLine + time * 0.12) * 26.0);
                float2 cell1 = floor(fireflyUV1);
                float2 local1 = frac(fireflyUV1) - 0.5;
                float seed1 = hash21(cell1 + 73.1);
                float gate1 = step(0.82, seed1);
                float blink1 = sin(time * (2.0 + seed1 * 2.5) + seed1 * 28.0) * 0.5 + 0.5;
                float motes1 = (1.0 - smoothstep(0.02, 0.42, length(local1))) * gate1 * blink1;

                // Layer 2: Fine sparkling dust motes drifting down
                float2 fireflyUV2 = float2(i.uv.x * 54.0 - cos(time * 1.1 + i.uv.y * 20.0) * 0.5, (i.uv.y - cutoffLine + time * 0.18) * 40.0);
                float2 cell2 = floor(fireflyUV2);
                float2 local2 = frac(fireflyUV2) - 0.5;
                float seed2 = hash21(cell2 + 139.7);
                float gate2 = step(0.85, seed2);
                float blink2 = sin(time * (2.8 + seed2 * 3.0) + seed2 * 45.0) * 0.5 + 0.5;
                float motes2 = (1.0 - smoothstep(0.01, 0.35, length(local2))) * gate2 * blink2;

                float fallingMotes = (motes1 * 0.85 + motes2 * 0.65) * edgeProximity * shieldMask * (1.0 - step(0.999, _TopDownCut));

                alpha *= emergenceMask;
                alpha = saturate(alpha + fallingMotes * 0.9);

                float3 color = veilColor * (0.58 + veilCloud * 0.24);
                color += _EnergyColor.rgb * (rootVeins * 0.68 + surfaceSweep * 0.28);
                color += edgeColor * (edgeCore * edgeFlicker * 0.92 + wovenThread * 0.34);
                color += _EnergyColor.rgb * wardRipple * 0.52;
                color += _RuneColor.rgb * (rune * 1.24 + baseWard * 0.74 + baseRoots * 0.44);
                color += lerp(_EnergyColor.rgb, _RuneColor.rgb, moteSeed) * mote * 1.15;
                color += lerp(_EnergyColor.rgb, _RuneColor.rgb, seed1) * (motes1 * edgeProximity * 4.2);
                color += _EnergyColor.rgb * (motes2 * edgeProximity * 3.5);
                color *= _Glow * i.color.rgb;

                return fixed4(color, alpha);
            }
            ENDCG
        }
    }
}
