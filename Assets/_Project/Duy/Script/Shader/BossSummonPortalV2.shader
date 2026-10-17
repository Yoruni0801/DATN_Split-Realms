Shader "Custom/2D/Boss Summon Portal V2"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        [HDR] _VoidColor ("Void Color", Color) = (0.012, 0.001, 0.008, 1)
        [HDR] _DeepColor ("Deep Energy Color", Color) = (0.14, 0.006, 0.16, 1)
        [HDR] _EnergyColor ("Vortex Energy Color", Color) = (1.35, 0.025, 0.65, 1)
        [HDR] _RimColor ("Rim Color", Color) = (1.65, 0.08, 0.65, 1)
        [HDR] _SparkColor ("Spark Color", Color) = (2.8, 1.05, 0.08, 1)

        _Alpha ("Alpha", Range(0, 1)) = 1
        _Radius ("Portal Radius", Range(0.2, 0.48)) = 0.405
        _EdgeSoftness ("Edge Turbulence", Range(0.005, 0.1)) = 0.032
        _CoreSize ("Void Core Size", Range(0.03, 0.25)) = 0.105
        _RingWidth ("Energy Ring Width", Range(0.005, 0.08)) = 0.026

        _Speed ("Flow Speed", Range(-3, 3)) = 0.65
        _Swirl ("Vortex Twist", Range(0, 14)) = 7.5
        _Arms ("Vortex Arms", Range(2, 12)) = 5
        _NoiseScale ("Turbulence Scale", Range(2, 18)) = 6
        _Distortion ("Turbulence Strength", Range(0, 2.5)) = 1.15

        _BodyIntensity ("Body Intensity", Range(0.2, 3)) = 1.2
        _RimIntensity ("Rim Intensity", Range(0, 4)) = 1.35
        _Sparkle ("Spark Intensity", Range(0, 4)) = 1.8
        _ArcDensity ("Rim Arc Density", Range(3, 20)) = 9
        _PulseSpeed ("Pulse Speed", Range(0, 4)) = 0.8
        _PulseAmount ("Pulse Amount", Range(0, 0.1)) = 0.025
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

            #define TAU 6.28318530718

            sampler2D _MainTex;

            float4 _VoidColor;
            float4 _DeepColor;
            float4 _EnergyColor;
            float4 _RimColor;
            float4 _SparkColor;

            float _Alpha;
            float _Radius;
            float _EdgeSoftness;
            float _CoreSize;
            float _RingWidth;

            float _Speed;
            float _Swirl;
            float _Arms;
            float _NoiseScale;
            float _Distortion;

            float _BodyIntensity;
            float _RimIntensity;
            float _Sparkle;
            float _ArcDensity;
            float _PulseSpeed;
            float _PulseAmount;

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

            float2 Rotate2D(float2 p, float angle)
            {
                float s = sin(angle);
                float c = cos(angle);
                return float2(c * p.x - s * p.y, s * p.x + c * p.y);
            }

            float Hash21(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                float2 blend = local * local * (3.0 - 2.0 * local);

                float bottom = lerp(Hash21(cell), Hash21(cell + float2(1.0, 0.0)), blend.x);
                float top = lerp(Hash21(cell + float2(0.0, 1.0)), Hash21(cell + 1.0), blend.x);
                return lerp(bottom, top, blend.y);
            }

            float Fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;

                [unroll]
                for (int octave = 0; octave < 4; octave++)
                {
                    value += ValueNoise(p) * amplitude;
                    p = Rotate2D(p * 2.03, 0.57);
                    amplitude *= 0.5;
                }

                return value;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 sprite = tex2D(_MainTex, i.uv);
                float2 p = i.uv - 0.5;
                float radiusFromCenter = length(p);
                float angle = atan2(p.y, p.x);
                float time = _Time.y * _Speed;

                float pulse = sin(_Time.y * _PulseSpeed * TAU);
                float portalRadius = _Radius * (1.0 + pulse * _PulseAmount);
                float normalizedRadius = radiusFromCenter / max(portalRadius, 0.001);
                float inward = pow(saturate(1.0 - normalizedRadius), 1.2);

                float2 coarseUv = Rotate2D(p, time * 0.16 + inward * _Swirl * 0.11);
                coarseUv = coarseUv * _NoiseScale + float2(time * 0.12, -time * 0.09);
                float coarseNoise = Fbm(coarseUv);

                float2 detailUv = Rotate2D(p, -time * 0.21);
                detailUv = detailUv * (_NoiseScale * 2.15) + float2(-time * 0.16, time * 0.11);
                float detailNoise = Fbm(detailUv);

                float warpedAngle = angle
                    + time * 0.55
                    + inward * _Swirl
                    + (coarseNoise - 0.5) * _Distortion * 2.8;

                float broadWave = 0.5 + 0.5 * sin(
                    warpedAngle * _Arms
                    - normalizedRadius * 16.0
                    + detailNoise * 5.0);
                float broadArm = pow(saturate(broadWave), 3.4);

                float threadWave = 0.5 + 0.5 * sin(
                    warpedAngle * (_Arms * 1.73 + 1.0)
                    - normalizedRadius * 31.0
                    - time * 0.65
                    + coarseNoise * 7.0);
                float thinThreads = pow(saturate(threadWave), 8.0);

                float energy = saturate(
                    coarseNoise * 0.38
                    + broadArm * 0.72
                    + thinThreads * 0.3);

                float2 radialDirection = p / max(radiusFromCenter, 0.0001);
                float edgeNoise = Fbm(
                    radialDirection * 2.7
                    + float2(time * 0.14, -time * 0.1));
                float edgeWave = sin(
                    angle * _ArcDensity
                    - time * 1.7
                    + edgeNoise * 5.0);
                float turbulentRadius = portalRadius
                    + (edgeNoise - 0.5) * _EdgeSoftness * 0.85
                    + edgeWave * _EdgeSoftness * 0.16;

                float bodyMask = 1.0 - smoothstep(
                    turbulentRadius - _EdgeSoftness,
                    turbulentRadius + _EdgeSoftness * 0.35,
                    radiusFromCenter);

                float ringDistance = abs(radiusFromCenter - turbulentRadius);
                float ringCore = 1.0 - smoothstep(
                    _RingWidth * 0.16,
                    _RingWidth,
                    ringDistance);
                float ringGlow = 1.0 - smoothstep(
                    _RingWidth,
                    _RingWidth * 4.0,
                    ringDistance);

                float brokenArc = pow(saturate(
                    0.5 + 0.5 * sin(
                        angle * _ArcDensity
                        - time * 2.3
                        + edgeNoise * 8.0)), 4.0);
                float counterArc = pow(saturate(
                    0.5 + 0.5 * sin(
                        -angle * (_ArcDensity * 0.63 + 2.0)
                        - time * 1.25
                        + detailNoise * 9.0)), 7.0);
                float arcEnergy = 0.45 + brokenArc * 0.8 + counterArc * 0.35;

                float outerBand = smoothstep(
                    turbulentRadius - _EdgeSoftness * 1.6,
                    turbulentRadius + _EdgeSoftness * 0.3,
                    radiusFromCenter);
                outerBand *= 1.0 - smoothstep(
                    turbulentRadius + _EdgeSoftness * 0.5,
                    turbulentRadius + _EdgeSoftness * 3.2,
                    radiusFromCenter);
                float outerWisps = outerBand * pow(saturate(edgeNoise), 2.2);

                float coreSize = _CoreSize * (1.0 - pulse * 0.035);
                float voidCore = 1.0 - smoothstep(
                    coreSize * 0.48,
                    coreSize * 1.12,
                    radiusFromCenter);
                float eventHorizon = smoothstep(
                    coreSize * 0.48,
                    coreSize * 0.9,
                    radiusFromCenter);
                eventHorizon *= 1.0 - smoothstep(
                    coreSize * 1.0,
                    coreSize * 1.75,
                    radiusFromCenter);

                float radialRays = pow(saturate(
                    0.5 + 0.5 * sin(
                        warpedAngle * (_Arms + 3.0)
                        + coarseNoise * 6.0
                        - time * 1.8)), 11.0);
                radialRays *= smoothstep(
                    coreSize * 0.8,
                    coreSize * 1.6,
                    radiusFromCenter);
                radialRays *= 1.0 - smoothstep(
                    coreSize * 2.0,
                    portalRadius * 0.9,
                    radiusFromCenter);

                float colorMix = saturate(
                    broadArm * 0.78
                    + thinThreads * 0.35
                    + coarseNoise * 0.22);
                float3 color = lerp(_DeepColor.rgb, _EnergyColor.rgb, colorMix);
                color *= 0.28 + energy * _BodyIntensity;
                color += _RimColor.rgb * radialRays * 0.32 * _RimIntensity;

                float coreDepth = 1.0 - smoothstep(
                    coreSize * 0.8,
                    coreSize * 2.8,
                    radiusFromCenter);
                color *= 1.0 - coreDepth * 0.48;
                color = lerp(color, _VoidColor.rgb, voidCore * 0.98);
                color += _EnergyColor.rgb
                    * eventHorizon
                    * (0.45 + broadArm * 0.7)
                    * _RimIntensity;

                color += _RimColor.rgb
                    * (ringCore * arcEnergy * 1.15 + ringGlow * 0.2)
                    * _RimIntensity;
                color += _DeepColor.rgb * outerWisps * 1.5;

                float sparkPattern = pow(saturate(
                    0.5 + 0.5 * sin(
                        angle * (_ArcDensity * 2.17)
                        + detailNoise * 10.0
                        - time * 4.0)), 18.0);
                float sparks = ringGlow
                    * sparkPattern
                    * saturate((detailNoise - 0.32) * 2.4);
                color += _SparkColor.rgb * sparks * _Sparkle;

                float interiorAlpha = bodyMask * saturate(0.34 + energy * 0.72);
                float edgeAlpha = saturate(
                    ringCore * 0.95
                    + ringGlow * 0.3
                    + outerWisps * 0.24
                    + sparks * 0.45);
                float alpha = max(interiorAlpha, edgeAlpha);
                alpha = max(alpha, voidCore * 0.96);
                alpha *= sprite.a * _Alpha * i.color.a;

                color *= i.color.rgb;
                return fixed4(color, alpha);
            }
            ENDCG
        }
    }
}
