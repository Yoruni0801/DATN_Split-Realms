Shader "Custom/Book/Page Magic Overlay"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 0.1, 0.02, 1)
        _EffectMode ("Effect Mode", Float) = 0
        _PageSide ("Page Side", Float) = 0
        _Visibility ("Visibility", Range(0, 1)) = 0
        _PetrifyAmount ("Petrify Amount", Range(0, 1)) = 0
        _Phase ("Phase", Float) = 0
        _Intensity ("Intensity", Range(0, 3)) = 1
        _PageTexture ("Page Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+25"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "PageMagicOverlay"
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
                float _PageSide;
                float _Visibility;
                float _PetrifyAmount;
                float _Phase;
                float _Intensity;
                float4 _PageTexture_ST;
            CBUFFER_END

            TEXTURE2D(_PageTexture);
            SAMPLER(sampler_PageTexture);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

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
                coords = coords * 2.04 + 5.37;
                amplitude *= 0.5;
                value += ValueNoise(coords) * amplitude;
                return value;
            }

            float ThinLine(float value, float width)
            {
                return 1.0 - smoothstep(width, width * 2.2, abs(value));
            }

            float SoftRadial(float2 uv, float2 center, float radius)
            {
                float distanceValue = length((uv - center) / radius);
                return smoothstep(1.0, 0.0, distanceValue);
            }

            float SoftEllipse(float2 uv, float2 center, float2 radius)
            {
                float distanceValue = length((uv - center) / radius);
                return smoothstep(1.0, 0.0, distanceValue);
            }

            float PageEdgeMask(float2 uv)
            {
                float horizontal = smoothstep(0.0, 0.045, uv.x) * smoothstep(1.0, 0.955, uv.x);
                float vertical = smoothstep(0.0, 0.035, uv.y) * smoothstep(1.0, 0.965, uv.y);
                return horizontal * vertical;
            }

            float2 NormalizePageUv(float2 uv)
            {
                return float2(saturate(uv.x / 0.80371), saturate(uv.y));
            }

            half4 ForbiddenInk(float2 sourceUv)
            {
                if (_PageSide >= 0.5)
                    return half4(0.0, 0.0, 0.0, 0.0);

                float2 textureUv = sourceUv * _PageTexture_ST.xy + _PageTexture_ST.zw;
                float3 pageColor = SAMPLE_TEXTURE2D(_PageTexture, sampler_PageTexture, textureUv).rgb;
                float brightest = max(pageColor.r, max(pageColor.g, pageColor.b));
                float darkest = min(pageColor.r, min(pageColor.g, pageColor.b));
                float saturation = brightest - darkest;
                float redDominance = pageColor.r - max(pageColor.g, pageColor.b);
                float purpleDominance = min(pageColor.r - pageColor.g, pageColor.b - pageColor.g);

                float headAndTorso = SoftEllipse(textureUv, float2(0.555, 0.505), float2(0.255, 0.285));
                float leftShoulder = SoftEllipse(textureUv, float2(0.385, 0.485), float2(0.165, 0.19));
                float lowerBody = SoftEllipse(textureUv, float2(0.535, 0.335), float2(0.235, 0.17));
                float rightArm = SoftEllipse(textureUv, float2(0.705, 0.425), float2(0.145, 0.16));
                float bossRegion = saturate(max(max(headAndTorso, leftShoulder), max(lowerBody, rightArm)) * 1.35);

                float redPigment = smoothstep(0.018, 0.14, redDominance) * smoothstep(0.075, 0.30, saturation);
                float purplePigment = smoothstep(0.012, 0.10, purpleDominance) * smoothstep(0.065, 0.25, saturation);
                float bossPigment = saturate(redPigment + purplePigment * 0.72) * bossRegion;

                float pulse = 0.72 + 0.28 * sin(_Phase * 2.6);
                float organic = Fbm(textureUv * 15.0 + float2(_Phase * 0.055, -_Phase * 0.025));
                float veinField = abs(sin(textureUv.x * 27.0 + textureUv.y * 17.0 + organic * 5.2 - _Phase * 0.75));
                float emberVeins = 1.0 - smoothstep(0.035, 0.13, veinField);
                emberVeins *= bossPigment * smoothstep(0.38, 0.72, organic);

                float faceRegion = SoftEllipse(textureUv, float2(0.575, 0.525), float2(0.13, 0.105));
                float warmEyes = smoothstep(0.025, 0.19, min(pageColor.r, pageColor.g) - pageColor.b);
                warmEyes *= smoothstep(0.32, 0.72, max(pageColor.r, pageColor.g)) * faceRegion;
                warmEyes = pow(saturate(warmEyes), 1.45);

                float skinPulse = bossPigment * (0.075 + pulse * 0.065);
                float glowMask = skinPulse + emberVeins * (0.34 + pulse * 0.22) + warmEyes * (0.72 + pulse * 0.48);
                float3 emberColor = lerp(float3(0.62, 0.018, 0.004), float3(1.0, 0.22, 0.008), saturate(emberVeins + warmEyes));
                emberColor += float3(1.0, 0.48, 0.035) * warmEyes * 1.65;

                return half4(emberColor, saturate(glowMask));
            }
            half4 Petrification(float2 uv)
            {
                float broadNoise = Fbm(uv * 8.0 + float2(0.0, _Phase * 0.018));
                float fineNoise = Fbm(uv * 27.0 - float2(_Phase * 0.03, 0.0));
                float height = 1.0 - uv.y;
                float boundary = _PetrifyAmount + (broadNoise - 0.5) * 0.10;
                float stoneMask = 1.0 - smoothstep(boundary - 0.04, boundary + 0.04, height);

                float crackA = abs(frac((uv.x + broadNoise * 0.11) * 5.2 + uv.y * 1.35) - 0.5);
                float crackB = abs(frac((uv.y - fineNoise * 0.06) * 7.4 - uv.x * 1.7) - 0.5);
                float largeCracks = (1.0 - smoothstep(0.009, 0.031, min(crackA, crackB))) * smoothstep(0.4, 0.72, broadNoise);
                float smallCracks = (1.0 - smoothstep(0.005, 0.018, abs(frac((uv.x + uv.y) * 17.0 + fineNoise * 1.7) - 0.5))) * smoothstep(0.63, 0.84, fineNoise);
                float cracks = saturate(largeCracks + smallCracks * 0.28) * stoneMask;
                float pores = smoothstep(0.58, 0.86, Fbm(uv * 42.0 + 3.4)) * stoneMask;

                float3 stoneDark = float3(0.12, 0.16, 0.15);
                float3 stoneMid = float3(0.34, 0.40, 0.36);
                float3 stoneLight = float3(0.56, 0.62, 0.53);
                float3 stoneColor = lerp(stoneDark, stoneMid, saturate(broadNoise * 1.35));
                stoneColor = lerp(stoneColor, stoneLight, saturate(fineNoise - 0.48) * 0.9);
                stoneColor *= 1.0 - cracks * 0.62;
                stoneColor *= 1.0 - pores * 0.08;

                float transitionEdge = 1.0 - smoothstep(0.0, 0.028, abs(height - boundary));
                float3 edgeColor = float3(0.62, 0.82, 0.62) * transitionEdge * stoneMask;
                float alpha = stoneMask * (0.48 + broadNoise * 0.11);
                return half4(stoneColor + edgeColor * 0.5, alpha);
            }

            half4 CrystalRitual(float2 uv)
            {
                float2 center = _PageSide < 0.5 ? float2(0.60, 0.50) : float2(0.56, 0.39);
                float2 coords = (uv - center) * float2(1.35, 1.0);
                float radius = length(coords);
                float angle = atan2(coords.y, coords.x);
                float pulse = 0.68 + 0.32 * sin(_Phase * 2.7);
                float energyNoise = Fbm(uv * 8.0 + float2(-_Phase * 0.045, _Phase * 0.065));

                float ringA = ThinLine(radius - 0.19 - sin(_Phase * 1.6) * 0.008, 0.0045);
                float ringB = ThinLine(radius - 0.30, 0.004) * (0.55 + 0.45 * sin(angle * 12.0 - _Phase * 1.8));
                float ringC = ThinLine(radius - 0.42, 0.0035) * (0.5 + 0.5 * sin(angle * 18.0 + _Phase * 2.1));
                float diamond = ThinLine(abs(coords.x) + abs(coords.y) - 0.27, 0.005);
                float rays = pow(saturate(0.04 / max(0.001, abs(sin(angle * 8.0 - _Phase * 0.5)))), 2.0);
                rays *= smoothstep(0.62, 0.12, radius) * smoothstep(0.05, 0.15, radius);
                float sparks = pow(saturate(energyNoise - 0.7), 4.0) * 8.0;
                float glint = pow(saturate(SoftRadial(uv, center + float2(0.04, 0.02), 0.13)), 3.0) * (0.25 + pulse * 0.2);
                float innerAura = SoftRadial(uv, center, 0.23) * (0.18 + pulse * 0.12);

                float mask = saturate(ringA * 0.42 + ringB * 0.31 + ringC * 0.24 + diamond * 0.22 + rays * 0.17 + sparks * 0.14);
                float alpha = mask * (0.13 + pulse * 0.15) + glint * 0.13 + innerAura * 0.045;
                float3 color = _Tint.rgb * (1.45 + pulse * 1.0) + float3(0.94, 0.72, 1.0) * (ringA + sparks * 0.14 + glint) * 0.7;
                return half4(color, alpha);
            }

            half4 CaveAtmosphere(float2 uv)
            {
                float2 drift = float2(_Phase * 0.022, -_Phase * 0.016);
                float fogA = Fbm(uv * float2(4.0, 6.0) + drift);
                float fogB = Fbm(uv * float2(8.0, 3.0) - drift * 1.6 + 4.2);
                float fog = saturate(fogA * 0.7 + fogB * 0.42 - 0.43);
                float shafts = pow(saturate(sin(uv.x * 21.0 + fogA * 4.0 + _Phase * 0.14) * 0.5 + 0.5), 8.0);
                float caveMask = _PageSide < 0.5 ? smoothstep(0.02, 0.3, uv.y) : smoothstep(0.02, 0.5, uv.y);
                float route = 0.0;

                if (_PageSide >= 0.5)
                {
                    float routeY = 0.76 - uv.x * 0.42 + sin(uv.x * 4.2) * 0.04;
                    float routeDistance = abs(uv.y - routeY);
                    route = 1.0 - smoothstep(0.008, 0.024, routeDistance);
                    float traveler = 1.0 - smoothstep(0.0, 0.08, abs(uv.x - frac(_Phase * 0.055)));
                    route *= 0.22 + traveler * 0.62;
                }

                float mask = fog * 0.35 * caveMask + shafts * 0.08 + route;
                float pulse = 0.82 + 0.18 * sin(_Phase * 1.05);
                float3 color = lerp(float3(0.025, 0.065, 0.14), _Tint.rgb, fog) * (1.08 + pulse * 0.3);
                return half4(color, mask * 0.42);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 pageUv = NormalizePageUv(input.uv);
                half4 result;
                if (_EffectMode < 0.5)
                    result = ForbiddenInk(input.uv);
                else if (_EffectMode < 1.5)
                    result = Petrification(pageUv);
                else if (_EffectMode < 2.5)
                    result = CrystalRitual(pageUv);
                else
                    result = CaveAtmosphere(pageUv);

                result.rgb *= _Intensity;
                result.a = saturate(result.a * _Visibility * PageEdgeMask(pageUv));
                return result;
            }
            ENDHLSL
        }
    }
}

