Shader "Custom/2D/DirectionalBurnDissolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _NoiseTex ("Noise Texture", 2D) = "white" {}

        [Header(BurnGeometry)]
        _NoiseScale ("Burn Noise Scale", Range(0.1, 5.0)) = 1.4
        _NoiseStrength ("Burn Jagged Strength", Range(0.0, 2.0)) = 0.8
        _BurnEdgeWidth ("Flame Rim Width", Range(0.02, 0.5)) = 0.16
        _CharWidth ("Charred Wood Width", Range(0.05, 0.8)) = 0.42
        _AshWidth ("Ash Fringe Width", Range(0.01, 0.2)) = 0.05
        _UpwardBuoyancy ("Flame Upward Buoyancy", Range(0.0, 1.5)) = 0.35

        [Header(RealisticFireColors)]
        _CharColor ("Charred Wood Color", Color) = (0.05, 0.015, 0.008, 1)
        _FireColorDark ("Deep Ember Red", Color) = (0.85, 0.08, 0.01, 1)
        _FireColorHot ("Vivid Fire Orange", Color) = (1.0, 0.38, 0.02, 1)
        _FireColorWarm ("Golden Yellow Flame", Color) = (1.0, 0.82, 0.12, 1)
        _FireColorCore ("Hot White Core", Color) = (1.0, 0.98, 0.85, 1)
        _AshColor ("Ash Gray Color", Color) = (0.24, 0.22, 0.20, 1)

        [Header(FlameAnimationAndEmission)]
        _FlameFlickerSpeed ("Flame Motion Speed", Range(0.5, 8.0)) = 3.5
        _FlameTurbulence ("Flame Turbulence", Range(0.1, 2.0)) = 0.75
        _FlameGlow ("Emission Intensity", Range(0.5, 5.0)) = 2.2
        _VoronoiScale ("Molten Vein Scale", Range(2.0, 30.0)) = 12.0
    }

    SubShader
    {
        Tags
        {
            "Queue"="AlphaTest"
            "IgnoreProjector"="True"
            "RenderType"="TransparentCutout"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
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

            #define MAX_BURN_CENTERS 8

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
                float3 worldPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            sampler2D _NoiseTex;

            fixed4 _Color;
            float _NoiseScale;
            float _NoiseStrength;
            float _BurnEdgeWidth;
            float _CharWidth;
            float _AshWidth;
            float _UpwardBuoyancy;

            fixed4 _CharColor;
            fixed4 _FireColorDark;
            fixed4 _FireColorHot;
            fixed4 _FireColorWarm;
            fixed4 _FireColorCore;
            fixed4 _AshColor;

            float _FlameFlickerSpeed;
            float _FlameTurbulence;
            float _FlameGlow;
            float _VoronoiScale;

            // Burn Segments: xy = StartPoint, zw = EndPoint (Laser Head)
            float4 _BurnSegments[MAX_BURN_CENTERS];
            // Burn Segment Data: x = Radius, y = Heat, z = MaxRadius, w = Active (1/0)
            float4 _BurnSegmentData[MAX_BURN_CENTERS];
            int _BurnCenterCount;

            // Superheated active laser incision point: xy = Laser Head Pos, z = Radius, w = Heat
            float4 _LaserHitPoint;

            float hash21(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            float2 hash22(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(443.897, 441.423, 437.195));
                p3 += dot(p3, p3.yzx + 19.19);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            float valNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbmNoise(float2 p, int octaves)
            {
                float v = 0.0;
                float a = 0.5;
                float2 shift = float2(100.0, 100.0);
                for (int i = 0; i < 4; ++i)
                {
                    if (i >= octaves) break;
                    v += a * valNoise(p);
                    p = p * 2.05 + shift;
                    a *= 0.5;
                }
                return v;
            }

            // Voronoi Cellular Cracks (Mạng lưới rãnh nung chảy AAA)
            float voronoiCracks(float2 p, float time)
            {
                float2 n = floor(p);
                float2 f = frac(p);

                float m_dist = 1.0;
                float m_dist2 = 1.0;

                for (int j = -1; j <= 1; j++)
                {
                    for (int i = -1; i <= 1; i++)
                    {
                        float2 g = float2(float(i), float(j));
                        float2 o = hash22(n + g);
                        o = 0.5 + 0.4 * sin(time + 6.2831 * o);
                        float2 r = g + o - f;
                        float d = dot(r, r);

                        if (d < m_dist)
                        {
                            m_dist2 = m_dist;
                            m_dist = d;
                        }
                        else if (d < m_dist2)
                        {
                            m_dist2 = d;
                        }
                    }
                }

                // Độ rộng của rãnh nứt giữa 2 tế bào
                return sqrt(m_dist2) - sqrt(m_dist);
            }

            float warpedFlame(float2 p, float time)
            {
                float2 q = float2(
                    fbmNoise(p + float2(0.0, -time * 0.8), 3),
                    fbmNoise(p + float2(5.2, 1.3 - time * 0.6), 3)
                );

                float2 r = float2(
                    fbmNoise(p + 3.0 * q + float2(1.7, 9.2), 3),
                    fbmNoise(p + 3.0 * q + float2(8.3, 2.8), 3)
                );

                return fbmNoise(p + 2.0 * r + float2(0.0, time * 0.5), 3);
            }

            float distToSegment(float2 p, float2 a, float2 b, out float2 closestPt)
            {
                float2 ba = b - a;
                float l2 = dot(ba, ba);
                if (l2 < 0.0001)
                {
                    closestPt = a;
                    return length(p - a);
                }
                float t = saturate(dot(p - a, ba) / l2);
                closestPt = a + t * ba;
                return length(p - closestPt);
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                OUT.worldPos = mul(unity_ObjectToWorld, IN.vertex).xyz;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 orig = tex2D(_MainTex, IN.texcoord) * IN.color;
                if (orig.a <= 0.005)
                    discard;

                float2 wpos = IN.worldPos.xy;

                if (_BurnCenterCount <= 0 && _LaserHitPoint.w <= 0.001)
                    return orig;

                float time = _Time.y * _FlameFlickerSpeed;

                float bestDiff = 1e9;
                float bestHeat = 0.0;
                float2 bestCenter = wpos;
                bool hasActiveCutout = false;

                for (int k = 0; k < MAX_BURN_CENTERS; k++)
                {
                    if (k >= _BurnCenterCount) break;
                    float4 seg = _BurnSegments[k];
                    float4 data = _BurnSegmentData[k]; // x: radius, y: heat, z: maxRadius, w: active

                    if (data.x <= 0.001 && data.y <= 0.001) continue;
                    hasActiveCutout = true;

                    float2 closestPt;
                    float dist = distToSegment(wpos, seg.xy, seg.zw, closestPt);

                    float2 delta = wpos - closestPt;
                    float buoyancy = delta.y * _UpwardBuoyancy;

                    float2 noiseCoord = delta * _NoiseScale;
                    float organicJag = fbmNoise(noiseCoord + float2(0.0, -time * 0.15), 3) * _NoiseStrength;
                    float woodGrain = valNoise(float2(delta.x * 4.5, delta.y * 1.2)) * (_NoiseStrength * 0.35);

                    float diff = (dist - buoyancy * 0.25) - (data.x + organicJag + woodGrain);

                    if (diff < bestDiff)
                    {
                        bestDiff = diff;
                        bestHeat = data.y;
                        bestCenter = closestPt;
                    }
                }

                if (!hasActiveCutout && _LaserHitPoint.w <= 0.001)
                    return orig;

                // 1. Cắt rã tro tàn (DISCARD VÙNG ĐÃ BỊ KHOAN THỦNG)
                if (hasActiveCutout)
                {
                    float ashNoise = valNoise(wpos * 12.0 + float2(0.0, time * 0.3)) * 0.03;
                    if (bestDiff < -_AshWidth - ashNoise)
                    {
                        discard;
                    }
                }

                float d = bestDiff;

                // 2. Sém than (Charred Wood)
                float charFactor = saturate(1.0 - (d / max(_CharWidth, 0.01)));
                charFactor = pow(charFactor, 1.6);

                // 3. Viền ngọn lửa (Flame Rim)
                float flameBand = saturate(1.0 - (abs(d) / max(_BurnEdgeWidth, 0.01)));

                // Lưỡi lửa cuộn xoáy tự nhiên
                float2 flameUV = (wpos - bestCenter) * 2.8;
                float flameCurl = warpedFlame(flameUV + float2(0.0, -time * 0.8), time);
                float flameTongues = saturate(flameCurl * 0.5 + 0.5);

                float flameIntensity = saturate(flameBand * (0.45 + 0.85 * flameTongues)) * bestHeat;

                fixed3 color = orig.rgb;

                // Gỗ hóa than nâu đen nung đỏ
                fixed3 charredWood = lerp(_CharColor.rgb, fixed3(0.015, 0.008, 0.004), valNoise(wpos * 8.0));
                color = lerp(color, charredWood, charFactor * 0.95);

                // 4. Mạng lưới nứt dung nham nung chảy (Voronoi Molten Veins AAA)
                if (bestHeat > 0.01 && charFactor > 0.05)
                {
                    float veinVal = voronoiCracks(wpos * _VoronoiScale, time * 0.4);
                    // Rãnh càng hẹp thì nhiệt độ càng cao
                    float crackIntensity = smoothstep(0.12, 0.01, veinVal) * charFactor * bestHeat;

                    // Nhịp đập nhiệt lượng (Thermal Pulse)
                    float thermalPulse = 0.85 + 0.25 * sin(time * 4.0 + wpos.x * 6.0 + wpos.y * 6.0);
                    crackIntensity *= thermalPulse;

                    fixed3 moltenVeinColor = lerp(_FireColorHot.rgb, _FireColorCore.rgb, crackIntensity);
                    color += moltenVeinColor * (crackIntensity * _FlameGlow * 2.0);

                    // Than nứt âm ỉ phụ
                    float emberNoise = valNoise((wpos - bestCenter) * 14.0);
                    float emberCracks = smoothstep(0.45, 0.75, emberNoise) * charFactor * (1.0 - flameIntensity);
                    fixed3 emberColor = _FireColorDark.rgb * (1.2 + 0.5 * sin(time * 5.0 + wpos.x * 10.0));
                    color += emberColor * emberCracks * _FlameGlow * bestHeat;
                }

                // 5. Gradient màu ngọn lửa chuẩn AAA: Đỏ -> Cam -> Vàng -> Lõi trắng
                if (flameIntensity > 0.001)
                {
                    fixed3 flameCol;
                    if (flameIntensity < 0.45)
                    {
                        float tGrad = flameIntensity / 0.45;
                        flameCol = lerp(_FireColorDark.rgb, _FireColorHot.rgb, tGrad);
                    }
                    else if (flameIntensity < 0.82)
                    {
                        float tGrad = (flameIntensity - 0.45) / 0.37;
                        flameCol = lerp(_FireColorHot.rgb, _FireColorWarm.rgb, tGrad);
                    }
                    else
                    {
                        float tGrad = (flameIntensity - 0.82) / 0.18;
                        flameCol = lerp(_FireColorWarm.rgb, _FireColorCore.rgb, pow(tGrad, 1.4));
                    }

                    flameCol *= (0.85 + 0.3 * valNoise(wpos * 5.0 + float2(time, time)));
                    color = lerp(color, flameCol, flameIntensity);
                    color += flameCol * (flameIntensity * _FlameGlow * 1.0);
                }

                // 6. Điểm nung nóng Laser Incision Hotspot (HDR Emission Overdrive)
                if (_LaserHitPoint.w > 0.01)
                {
                    float2 headPos = _LaserHitPoint.xy;
                    float headRadius = max(_LaserHitPoint.z, 0.25);
                    float distHead = length(wpos - headPos);
                    if (distHead < headRadius * 2.8)
                    {
                        float hotFactor = saturate(1.0 - (distHead / (headRadius * 2.8)));
                        hotFactor = pow(hotFactor, 1.4) * _LaserHitPoint.w;

                        // Tầng lõi trắng chói lòa
                        fixed3 moltenCol = lerp(_FireColorHot.rgb, _FireColorCore.rgb, pow(hotFactor, 1.2));
                        color = lerp(color, moltenCol, hotFactor * 0.92);
                        color += moltenCol * (hotFactor * _FlameGlow * 3.2); // HDR Bloom Overdrive
                    }
                }

                // 7. Tro tàn xám ở mép
                if (d < 0.0)
                {
                    float ashBlend = saturate((-d) / max(_AshWidth, 0.005));
                    color = lerp(color, _AshColor.rgb, ashBlend * 0.88);
                }

                float alpha = orig.a;
                if (d < 0.0)
                {
                    alpha *= saturate(1.0 - ((-d) / max(_AshWidth, 0.005)));
                }

                return fixed4(color, alpha);
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}