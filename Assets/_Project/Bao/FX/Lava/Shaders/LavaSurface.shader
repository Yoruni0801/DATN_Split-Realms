Shader "Bao/Lava Surface"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [Header(Vibrant Orange Magma Palette)]
        _MainColor ("Lava Body (Vibrant Orange)", Color) = (0.96, 0.38, 0.02, 1.0)
        _DeepColor ("Deep Body (Molten Crimson)", Color) = (0.32, 0.03, 0.01, 1.0)
        [HDR] _EdgeColor ("Superheated Glow Core (Warm Gold)", Color) = (1.4, 0.75, 0.08, 1.0)
        _CrustColor ("Cooling Basalt Crust (Dark Basalt)", Color) = (0.12, 0.03, 0.02, 1.0)
        _Alpha ("Surface Alpha", Range(0, 1)) = 1.0

        [Header(Tornado Color Ramp and Remapping)]
        _ColorRampTex ("Color Ramp Texture", 2D) = "white" {}
        _UseColorRamp ("Use Color Ramp", Range(0, 1)) = 0.0
        _RampPow ("Ramp Curve Power", Range(0.1, 5)) = 1.35
        _GradientBlend ("Gradient Remap Blend", Range(0, 1)) = 0.92
        _Brightness ("Overall Vivid Brightness", Range(0, 3)) = 0.95
        _PosterizeLevels ("Stylized Step Bands (0=Off)", Range(0, 16)) = 0

        [Header(Viscous Slow Flow and Warping)]
        _RippleSpeed ("Slow Viscous Flow Speed", Range(0, 2)) = 0.22
        _RippleScale ("Magma Convection Scale", Range(1, 30)) = 4.0
        _ScrollSpeedX ("Secondary Flow X", Range(-2, 2)) = 0.08
        _ScrollSpeedY ("Secondary Flow Y", Range(-2, 2)) = -0.03
        _DistortionStrength ("Viscous Heat Distortion", Range(0, 0.08)) = 0.015
        _NoiseAmount ("Turbulence Weight", Range(0, 2)) = 0.70

        [Header(Magma Boiling Cells and Pockets)]
        _BubbleStrength ("Boil Bubble Strength", Range(0, 2)) = 1.2
        _CellScale ("Boil Cell Scale", Range(1, 48)) = 9.0
        _CellSpeed ("Boil Speed", Range(0, 4)) = 0.38
        _RingWidth ("Bubble Glowing Rim Width", Range(0.01, 0.35)) = 0.09
        _RingThreshold ("Bubble Density", Range(0, 1)) = 0.48

        [Header(Dense Crust Plates and Molten Fissures)]
        _SurfaceFoam ("Surface Heat Foam Blend", Range(0, 1)) = 0.85
        _EmissionStrength ("HDR Fissure Emission", Range(0, 3)) = 0.55
        _CrustContrast ("Crust Thickness Contrast", Range(0.1, 5)) = 2.0
        _ShapeContrast ("Crust Plate Sharpness", Range(0.1, 4)) = 1.4
        _FissureGlowStrength ("Crust Edge Fissure Glow", Range(0, 2)) = 0.85

        [Header(Vertex Heat Wave Displacement)]
        _VertexWaveAmount ("Vertex Jitter Amount", Range(0, 0.1)) = 0.006
        _VertexWaveSpeed ("Vertex Jitter Speed", Range(0, 6)) = 0.8
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" }
        Blend One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_ColorRampTex);
            SAMPLER(sampler_ColorRampTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainColor;
                float4 _DeepColor;
                float4 _EdgeColor;
                float4 _CrustColor;
                float _Alpha;
                float _UseColorRamp;
                float _RampPow;
                float _GradientBlend;
                float _Brightness;
                float _PosterizeLevels;
                float _RippleSpeed;
                float _RippleScale;
                float _ScrollSpeedX;
                float _ScrollSpeedY;
                float _DistortionStrength;
                float _NoiseAmount;
                float _BubbleStrength;
                float _CellScale;
                float _CellSpeed;
                float _RingWidth;
                float _RingThreshold;
                float _SurfaceFoam;
                float _EmissionStrength;
                float _CrustContrast;
                float _ShapeContrast;
                float _FissureGlowStrength;
                float _VertexWaveAmount;
                float _VertexWaveSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float2 Hash22(float2 p)
            {
                float n = Hash21(p);
                return frac(float2(n, n * 1.371) + float2(0.123, 0.719));
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash21(i), Hash21(i + float2(1, 0)), f.x),
                            lerp(Hash21(i + float2(0, 1)), Hash21(i + float2(1, 1)), f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float value = Noise(p) * 0.54;
                value += Noise(p * 2.03 + 17.2) * 0.29;
                value += Noise(p * 4.08 - 9.5) * 0.17;
                return value;
            }

            float CellDistance(float2 p)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                float nearest = 2.0;
                [unroll] for (int y = -1; y <= 1; y++)
                {
                    [unroll] for (int x = -1; x <= 1; x++)
                    {
                        float2 offset = float2(x, y);
                        float2 seed = Hash22(cell + offset);
                        float wobble = sin(_Time.y * _CellSpeed + Hash21(cell + offset) * 6.2831853) * 0.12;
                        float2 center = offset + seed + wobble;
                        nearest = min(nearest, length(local - center));
                    }
                }
                return nearest;
            }

            // Stylized Posterize function (adapted from AllIn1 Color Effects)
            float3 ApplyPosterize(float3 color, float levels)
            {
                if (levels <= 1.0) return color;
                float numColors = floor(levels);
                return floor(color * numColors + 0.5) / numColors;
            }

            // Procedural Vivid Magma Heat Color Ramp for Map 5
            float3 ProceduralMagmaRamp(float heat)
            {
                heat = saturate(heat);
                float3 c0 = _CrustColor.rgb;                            // 0.00: Basalt crust with ruby edge
                float3 c1 = _DeepColor.rgb;                             // 0.20: Molten crimson body
                float3 c2 = _MainColor.rgb;                             // 0.55: Rich dense orange magma
                float3 c3 = lerp(_MainColor.rgb, _EdgeColor.rgb, 0.60); // 0.85: Warm glowing orange-gold
                float3 c4 = _EdgeColor.rgb * 1.15;                      // 1.00: Warm thermal fissure core

                float3 col;
                if (heat < 0.20)
                    col = lerp(c0, c1, heat / 0.20);
                else if (heat < 0.55)
                    col = lerp(c1, c2, (heat - 0.20) / 0.35);
                else if (heat < 0.85)
                    col = lerp(c2, c3, (heat - 0.55) / 0.30);
                else
                    col = lerp(c3, c4, (heat - 0.85) / 0.15);
                return col;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 pos = input.positionOS.xyz;

                // Subtle Vertex Heat Wave Jitter
                if (_VertexWaveAmount > 0.0001)
                {
                    float waveTime = _Time.y * _VertexWaveSpeed;
                    float waveOffset = sin(pos.x * 3.2 + waveTime) * cos(pos.y * 2.6 + waveTime * 0.7);
                    pos.y += waveOffset * _VertexWaveAmount * saturate(input.uv.y);
                }

                output.positionCS = TransformObjectToHClip(pos);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Slow viscous fluid flow (no specular water reflections, only dense magma currents)
                float time = _Time.y * _RippleSpeed;
                float flowScale = max(1.0, _RippleScale);
                float2 flowP = input.uv * flowScale;

                // Heavy domain warping for thick, sluggish lava convection
                float2 secondaryScroll = float2(_ScrollSpeedX, _ScrollSpeedY) * time;
                float warpX = Fbm(flowP * 0.44 + float2(time * 0.11, -time * 0.05) + secondaryScroll);
                float warpY = Fbm(flowP * 0.60 + float2(-time * 0.07, time * 0.08) - secondaryScroll * 0.6 + 8.1);
                float2 warpVec = (float2(warpX, warpY) - 0.5) * _DistortionStrength * _NoiseAmount;
                float2 warpedUv = input.uv + warpVec;
                half4 sprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, warpedUv);

                // Dense, thick magma body noise
                float bodyNoise = Fbm(flowP + float2(time * 0.14, -time * 0.08));
                float deepNoise = Fbm(flowP * 0.52 + float2(-time * 0.07, time * 0.04) + 31.0);
                float body = smoothstep(0.10, 0.90, bodyNoise * 0.65 + deepNoise * 0.35);
                float vertical = saturate(input.uv.y * 1.15 - 0.04);

                // Voronoi magma bubbling and thick boiling cells
                float2 cellP = warpedUv * max(1.0, _CellScale) + float2(time * 0.25, -time * 0.16);
                float cellA = CellDistance(cellP);
                float cellB = CellDistance(cellP * 0.65 + float2(5.7, 2.2) - time * 0.09);
                float cell = min(cellA, cellB * 1.35);
                float threshold = lerp(0.70, 0.42, saturate(_RingThreshold));
                float bubbleBody = 1.0 - smoothstep(threshold, threshold + 0.18, cell);
                float ringOuter = 1.0 - smoothstep(threshold, threshold + _RingWidth, cell);
                float ringInner = 1.0 - smoothstep(max(0.0, threshold - _RingWidth), threshold, cell);
                float bubbleRing = saturate(ringOuter - ringInner);
                bubbleRing *= (0.35 + vertical * 0.65) * _BubbleStrength;
                bubbleBody *= (0.35 + vertical * 0.45) * _BubbleStrength;

                // Thick solid crust plates and fissure cracks
                float crustNoise = Fbm(flowP * 1.25 + float2(-time * 0.07, time * 0.04));
                float crustCentered = (crustNoise - 0.35) * _CrustContrast;
                float crustPattern = saturate(pow(abs(saturate(crustCentered + 0.5)), _ShapeContrast));
                // Bubbles break through the crust
                float crust = crustPattern * (1.0 - bubbleBody * 0.85);

                // Glowing fissure cracks along crust edges
                float crustEdge = saturate(1.0 - abs(crustPattern - 0.5) * 2.2);
                float fissureGlow = crustEdge * (1.0 - crust) * _FissureGlowStrength;

                // Surface heat crust layer at the top
                float topCrustNoise = Fbm(flowP * 1.45 + float2(-time * 0.12, time * 0.06));
                float topZone = smoothstep(0.60, 0.98, input.uv.y + (topCrustNoise - 0.5) * 0.15);
                float topHeat = saturate(topZone * _SurfaceFoam);

                // Thermal Energy Field calculation (Dense, glowing rich orange from within)
                float heatEnergy = saturate(body * 0.65 + vertical * 0.30 + bubbleBody * 0.50 + fissureGlow * 0.35 - crust * 0.50);
                heatEnergy = pow(heatEnergy, max(0.01, _RampPow));

                // Heat Remapping
                float3 rampColor;
                if (_UseColorRamp > 0.5)
                {
                    float2 rampUv = float2(heatEnergy, 0.5);
                    rampColor = SAMPLE_TEXTURE2D(_ColorRampTex, sampler_ColorRampTex, rampUv).rgb;
                }
                else
                {
                    rampColor = ProceduralMagmaRamp(heatEnergy);
                }

                // Dense direct magma color synthesis
                float3 directColor = lerp(_DeepColor.rgb, _MainColor.rgb, body * 0.75 + vertical * 0.25);
                // Thick cooling crust on surface
                directColor = lerp(directColor, _CrustColor.rgb, crust * 0.65);
                // Internal glow from bubble fissures
                directColor = lerp(directColor, _EdgeColor.rgb, bubbleBody * 0.45 + topHeat * 0.30);

                // Blend direct synthesis with vivid ramp remapping
                float3 lava = lerp(directColor, rampColor, _GradientBlend);

                // Internal Warm Glow from fissure rings, crust cracks and molten core (pure heat emission, NO specular shine lines)
                float3 fissureEmission = _EdgeColor.rgb * (bubbleRing * 0.85 + fissureGlow * 0.65 + (1.0 - crust) * bubbleBody * 0.35) * _EmissionStrength;
                lava += fissureEmission;

                // Vivid Brightness & Stylized Posterization
                lava *= _Brightness;
                if (_PosterizeLevels > 1.0)
                {
                    lava = ApplyPosterize(lava, _PosterizeLevels);
                }

                // Completely opaque, dense magma body
                float alphaMask = sprite.a * input.color.a * _Alpha;
                return half4(lava * alphaMask, alphaMask);
            }
            ENDHLSL
        }
    }
}
