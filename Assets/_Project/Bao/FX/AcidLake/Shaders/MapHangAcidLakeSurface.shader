Shader "Bao/Acid Lake Surface"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _MainColor ("Acid Body", Color) = (0.13, 0.52, 0.035, 0.86)
        _DeepColor ("Deep Body", Color) = (0.01, 0.07, 0.02, 0.92)
        [HDR] _EdgeColor ("Corrosive Edge", Color) = (0.52, 1.35, 0.06, 1)
        _Alpha ("Surface Alpha", Range(0, 1)) = 0.84
        _RippleSpeed ("Flow Speed", Range(0, 3)) = 0.85
        _RippleScale ("Flow Scale", Range(1, 30)) = 5.5
        _DistortionStrength ("Distortion", Range(0, 0.08)) = 0.012
        _BubbleStrength ("Hot Pocket Strength", Range(0, 1.5)) = 0.85
        _SurfaceFoam ("Surface Rim", Range(0, 1)) = 0.65
        _EmissionStrength ("Emission", Range(0, 2)) = 0.9
        _CellScale ("Boil Cell Scale", Range(1, 48)) = 13
        _CellSpeed ("Boil Cell Speed", Range(0, 4)) = 1.15
        _RingWidth ("Bubble Ring Width", Range(0.01, 0.35)) = 0.09
        _RingThreshold ("Bubble Ring Density", Range(0, 1)) = 0.55
        _FoamContrast ("Foam Breakup", Range(0.1, 5)) = 1.6
        _HighlightStrength ("Liquid Highlight", Range(0, 2)) = 0.6
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

            CBUFFER_START(UnityPerMaterial)
                float4 _MainColor;
                float4 _DeepColor;
                float4 _EdgeColor;
                float _Alpha;
                float _RippleSpeed;
                float _RippleScale;
                float _DistortionStrength;
                float _BubbleStrength;
                float _SurfaceFoam;
                float _EmissionStrength;
                float _CellScale;
                float _CellSpeed;
                float _RingWidth;
                float _RingThreshold;
                float _FoamContrast;
                float _HighlightStrength;
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

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float time = _Time.y * _RippleSpeed;
                float flowScale = max(1.0, _RippleScale);
                float2 flowP = input.uv * flowScale;
                float warpX = Fbm(flowP * 0.46 + float2(time * 0.11, -time * 0.05));
                float warpY = Fbm(flowP * 0.63 + float2(-time * 0.07, time * 0.08) + 8.1);
                float2 warpedUv = input.uv + (float2(warpX, warpY) - 0.5) * _DistortionStrength;
                half4 sprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, warpedUv);

                float bodyNoise = Fbm(flowP + float2(time * 0.18, -time * 0.11));
                float deepNoise = Fbm(flowP * 0.58 + float2(-time * 0.09, time * 0.05) + 31.0);
                float body = smoothstep(0.18, 0.83, bodyNoise * 0.70 + deepNoise * 0.30);
                float vertical = saturate(input.uv.y * 1.1 - 0.08);

                float2 cellP = warpedUv * max(1.0, _CellScale) + float2(time * 0.34, -time * 0.22);
                float cellA = CellDistance(cellP);
                float cellB = CellDistance(cellP * 0.63 + float2(5.7, 2.2) - time * 0.11);
                float cell = min(cellA, cellB * 1.38);
                float threshold = lerp(0.77, 0.48, saturate(_RingThreshold));
                float bubbleBody = 1.0 - smoothstep(threshold, threshold + 0.18, cell);
                float ringOuter = 1.0 - smoothstep(threshold, threshold + _RingWidth, cell);
                float ringInner = 1.0 - smoothstep(max(0.0, threshold - _RingWidth), threshold, cell);
                float bubbleRing = saturate(ringOuter - ringInner);
                bubbleRing *= (0.38 + vertical * 0.62) * _BubbleStrength;
                bubbleBody *= (0.20 + vertical * 0.35) * _BubbleStrength;

                float foamNoise = Fbm(flowP * 1.45 + float2(-time * 0.15, time * 0.08));
                float topFoam = smoothstep(0.74, 0.99, input.uv.y + (foamNoise - 0.5) * 0.18);
                float alphaEdge = saturate(1.0 - sprite.a * 12.0);
                float brokenFoam = saturate((foamNoise - 0.28) * _FoamContrast);
                float foam = saturate((topFoam + alphaEdge * 0.42) * brokenFoam * _SurfaceFoam);

                float highlightWave = saturate(sin((warpedUv.x * 3.7 + warpedUv.y * 1.3) * 6.2831853 - time * 2.1) * 0.5 + 0.5);
                highlightWave = smoothstep(0.82, 0.98, highlightWave) * Fbm(flowP * 1.9 - time * 0.1);

                float3 acid = lerp(_DeepColor.rgb, _MainColor.rgb, body * 0.72 + vertical * 0.18);
                acid = lerp(acid, _EdgeColor.rgb, foam * 0.46 + bubbleBody * 0.19);
                acid += _EdgeColor.rgb * (bubbleRing * 0.86 + foam * 0.26 + highlightWave * _HighlightStrength * 0.18) * _EmissionStrength;

                float alphaMask = sprite.a * input.color.a * _Alpha;
                return half4(acid * alphaMask, alphaMask);
            }
            ENDHLSL
        }
    }
}
