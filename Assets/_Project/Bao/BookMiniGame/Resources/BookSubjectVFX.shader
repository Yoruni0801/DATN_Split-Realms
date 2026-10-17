Shader "Custom/Book/Subject VFX"
{
    Properties
    {
        _MainTex ("Subject Sprite", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _EffectMode ("Effect Mode", Float) = 0
        _Progress ("Progress", Range(0, 1)) = 0
        _Phase ("Phase", Float) = 0
        _Visibility ("Visibility", Range(0, 1)) = 0
        _Intensity ("Intensity", Range(0, 3)) = 1
        _SpriteScale ("Sprite Scale", Vector) = (1, 1, 0, 0)
        _SpriteTarget ("Sprite Target", Vector) = (0, 0, 0, 0)
        _SpriteCropMin ("Sprite Crop Min", Vector) = (0, 0, 0, 0)
        _SpriteCropMax ("Sprite Crop Max", Vector) = (1, 1, 0, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+35" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Name "SubjectVFX"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _EffectMode;
                float _Progress;
                float _Phase;
                float _Visibility;
                float _Intensity;
                float4 _SpriteScale;
                float4 _SpriteTarget;
                float4 _SpriteCropMin;
                float4 _SpriteCropMax;
            CBUFFER_END
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            float Hash21(float2 coords)
            {
                coords = frac(coords * float2(123.34, 456.21));
                coords += dot(coords, coords + 45.32);
                return frac(coords.x * coords.y);
            }
            float ValueNoise(float2 uv)
            {
                float2 cell = floor(uv);
                float2 local = frac(uv);
                local = local * local * (3.0 - 2.0 * local);
                float bottomLeft = Hash21(cell);
                float bottomRight = Hash21(cell + float2(1.0, 0.0));
                float topLeft = Hash21(cell + float2(0.0, 1.0));
                float topRight = Hash21(cell + float2(1.0, 1.0));
                return lerp(lerp(bottomLeft, bottomRight, local.x), lerp(topLeft, topRight, local.x), local.y);
            }
            float Fbm(float2 coords)
            {
                float value = 0.0;
                float amplitude = 0.5;
                value += ValueNoise(coords) * amplitude;
                coords = coords * 2.03 + 17.13;
                amplitude *= 0.5;
                value += ValueNoise(coords) * amplitude;
                coords = coords * 2.01 + 9.71;
                amplitude *= 0.5;
                value += ValueNoise(coords) * amplitude;
                return value;
            }
            float VoronoiCracks(float2 coords)
            {
                float2 cell = floor(coords);
                float2 local = frac(coords);
                float nearest = 10.0;
                float secondNearest = 10.0;

                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 offset = float2(x, y);
                        float2 randomPoint = float2(Hash21(cell + offset), Hash21(cell + offset + 31.73));
                        float distanceValue = length(offset + randomPoint - local);
                        if (distanceValue < nearest)
                        {
                            secondNearest = nearest;
                            nearest = distanceValue;
                        }
                        else if (distanceValue < secondNearest)
                        {
                            secondNearest = distanceValue;
                        }
                    }
                }

                return 1.0 - smoothstep(0.018, 0.07, secondNearest - nearest);
            }
            float SoftEllipse(float2 uv, float2 center, float2 radius)
            {
                float distanceValue = length((uv - center) / radius);
                return smoothstep(1.0, 0.0, distanceValue);
            }
            float AlphaAround(float2 uv, float2 offset)
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, saturate(uv + offset)).a;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = saturate(input.uv);
                float2 spriteUvRaw = _SpriteCropMin.xy + (uv - _SpriteTarget.xy) / max(_SpriteScale.xy, float2(0.0001, 0.0001));
                float spriteInBounds = step(0.0, spriteUvRaw.x) * step(spriteUvRaw.x, 1.0) * step(0.0, spriteUvRaw.y) * step(spriteUvRaw.y, 1.0);
                float2 spriteUv = saturate(spriteUvRaw);
                float2 effectUv = saturate((spriteUv - _SpriteCropMin.xy) / max(_SpriteCropMax.xy - _SpriteCropMin.xy, float2(0.0001, 0.0001)));
                float2 texel = 1.0 / 1254.0;
                half4 source = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, spriteUv);
                source.a *= spriteInBounds;
                float alpha = source.a;
                float neighbor = max(max(AlphaAround(spriteUv, float2(texel.x * 3.0, 0.0)), AlphaAround(spriteUv, float2(-texel.x * 3.0, 0.0))), max(AlphaAround(spriteUv, float2(0.0, texel.y * 3.0)), AlphaAround(spriteUv, float2(0.0, -texel.y * 3.0))));
                float innerEdge = saturate(alpha - min(min(AlphaAround(spriteUv, float2(texel.x * 3.0, 0.0)), AlphaAround(spriteUv, float2(-texel.x * 3.0, 0.0))), min(AlphaAround(spriteUv, float2(0.0, texel.y * 3.0)), AlphaAround(spriteUv, float2(0.0, -texel.y * 3.0)))));
                float outerEdge = saturate(neighbor - alpha);
                innerEdge *= spriteInBounds;
                outerEdge *= spriteInBounds;
                float phasePulse = 0.78 + 0.22 * sin(_Phase * 2.2);
                float noise = Fbm(effectUv * 10.0 + float2(_Phase * 0.035, -_Phase * 0.022));
                float3 color = source.rgb;
                float outputAlpha = alpha;
                float3 glow = 0.0;
                if (_EffectMode < 0.5)
                {
                    float redDominance = source.r - max(source.g, source.b);
                    float purpleDominance = min(source.r - source.g, source.b - source.g);
                    float pigment = saturate(smoothstep(0.015, 0.16, redDominance) + smoothstep(0.01, 0.11, purpleDominance) * 0.65) * alpha;
                    float veinWave = 1.0 - smoothstep(0.03, 0.14, abs(sin(effectUv.x * 30.0 + effectUv.y * 17.0 + noise * 5.0 - _Phase * 0.85)));
                    float veins = veinWave * pigment * smoothstep(0.34, 0.74, noise);
                    float face = SoftEllipse(spriteUv, float2(0.59, 0.49), float2(0.095, 0.085));
                    float warm = smoothstep(0.025, 0.2, min(source.r, source.g) - source.b) * smoothstep(0.30, 0.72, max(source.r, source.g)) * face * alpha;
                    float rim = outerEdge * (0.35 + 0.3 * phasePulse);
                    color = float3(0.20, 0.004, 0.001) + source.rgb * 0.16;
                    glow += float3(1.0, 0.08, 0.005) * veins * (0.65 + phasePulse * 0.55);
                    glow += float3(1.0, 0.45, 0.025) * warm * 2.1;
                    glow += float3(0.72, 0.015, 0.002) * rim;
                    outputAlpha = (pigment * 0.10 + veins * 0.62 + warm * 0.72 + rim * 0.42) * spriteInBounds;
                }
                else if (_EffectMode < 1.5)
                {
                    float boundary = saturate(_Progress);
                    float stoneBoundary = boundary + (noise - 0.5) * 0.055;
                    float stone = (1.0 - smoothstep(stoneBoundary - 0.05, stoneBoundary + 0.05, effectUv.y)) * alpha;
                    float transitionActive = smoothstep(0.015, 0.12, boundary) * (1.0 - smoothstep(0.88, 0.995, boundary));
                    float transition = (1.0 - smoothstep(0.0, 0.045, abs(effectUv.y - stoneBoundary))) * alpha * transitionActive;
                    float cracks = saturate(VoronoiCracks(effectUv * 9.0 + noise * 1.7) * 0.9 + VoronoiCracks(effectUv * 17.0 - noise) * 0.35) * stone;
                    float pores = smoothstep(0.62, 0.88, Fbm(effectUv * 34.0 + 4.0)) * stone;
                    float sourceLuma = dot(source.rgb, float3(0.299, 0.587, 0.114));
                    float3 stoneColor = lerp(float3(0.16, 0.19, 0.18), float3(0.58, 0.64, 0.57), saturate(sourceLuma * 0.78 + noise * 0.72));
                    stoneColor *= lerp(0.72, 1.18, sourceLuma);
                    stoneColor *= 1.0 - cracks * 0.62;
                    stoneColor *= 1.0 - pores * 0.12;
                    stoneColor += innerEdge * float3(0.18, 0.23, 0.20);
                    color = lerp(color, stoneColor, stone);
                    glow += float3(0.42, 0.82, 0.52) * transition * (0.55 + 0.35 * sin(_Phase * 2.3));
                    outputAlpha = alpha * (0.42 + stone * 0.53) + transition * 0.16;
                }
                else
                {
                    float crystal = alpha;
                    // The source is deliberately recoloured instead of simply tinted:
                    // this keeps the reference's dark violet facets, hot lavender
                    // fractures and near-white central light on every page lighting setup.
                    float facetNoise = Fbm(effectUv * 13.0 + float2(_Phase * 0.028, -_Phase * 0.018));
                    float fractures = saturate(VoronoiCracks(effectUv * 8.0 + facetNoise * 1.8) * 0.88
                        + VoronoiCracks(effectUv * 17.0 - facetNoise * 1.3) * 0.28) * crystal;
                    float centerBeam = pow(SoftEllipse(effectUv, float2(0.51, 0.53), float2(0.17, 0.58)), 2.0) * crystal;
                    float shimmer = pow(saturate(sin((effectUv.x * 9.5 - effectUv.y * 5.8) + _Phase * 1.55) * 0.5 + 0.5), 12.0) * crystal;
                    float shimmerFine = pow(saturate(sin((effectUv.x * -18.0 - effectUv.y * 7.5) - _Phase * 2.1) * 0.5 + 0.5), 18.0) * crystal;
                    float diagonal = (1.0 - smoothstep(0.012, 0.045, abs(effectUv.x - 0.52 + (effectUv.y - 0.5) * 0.22))) * crystal;
                    float refractionBand = 1.0 - smoothstep(0.015, 0.065, abs(frac(effectUv.y * 3.15 - _Phase * 0.19) - 0.5));
                    refractionBand *= centerBeam * (0.42 + facetNoise * 0.58);
                    float rim = outerEdge * (0.58 + phasePulse * 0.34);
                    float glint = shimmer * 0.72 + shimmerFine * 0.36 + diagonal * 0.3 + refractionBand * 0.24 + innerEdge * 0.75 + rim * 0.48;
                    float3 deepViolet = float3(0.11, 0.008, 0.30);
                    float3 amethyst = float3(0.45, 0.055, 0.92);
                    float3 lavender = float3(0.86, 0.47, 1.0);
                    color = lerp(deepViolet, amethyst, saturate(source.r * 0.42 + facetNoise * 0.72));
                    color = lerp(color, lavender, centerBeam * 0.64 + glint * 0.32);
                    color *= 0.82 + facetNoise * 0.34;
                    color *= 1.0 - fractures * 0.23;
                    glow += lavender * (fractures * 0.92 + glint * 1.42) * (0.72 + phasePulse * 0.68);
                    glow += float3(1.0, 0.93, 1.0) * centerBeam * (0.58 + phasePulse * 0.48);
                    glow += float3(0.58, 0.18, 1.0) * refractionBand * 0.48;
                    outputAlpha = crystal * 0.48 + fractures * 0.16 + glint * 0.34 + rim * 0.18;
                }
                float shadow = AlphaAround(spriteUv + float2(-texel.x * 5.0, texel.y * 7.0), float2(0.0, 0.0)) * (1.0 - alpha) * 0.18 * spriteInBounds;
                color = color * _Tint.rgb + glow * _Intensity;
                outputAlpha = saturate(outputAlpha * _Visibility + shadow * _Visibility);
                return half4(color, outputAlpha);
            }
            ENDHLSL
        }
    }
}
