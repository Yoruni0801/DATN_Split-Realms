Shader "Bao/DreadwormCrystalBossShader"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        [Header(Boss Energy Glow)]
        [HDR] _CrystalColor ("Crystal Glow", Color) = (0.45, 1.85, 2.35, 1)
        [HDR] _StormColor ("Storm Glow", Color) = (0.12, 0.35, 1.8, 1)
        [HDR] _EnrageVeinColor ("Enrage Vein Glow", Color) = (2.35, 0.45, 0.22, 1)
        [HDR] _EnrageRimColor ("Enrage Rim Glow", Color) = (2.15, 0.24, 0.38, 1)

        [Header(Living Glacial Ice Palette)]
        [HDR] _GlacialDeepNavy ("Glacial Deep Navy (Shadows)", Color) = (0.02, 0.08, 0.24, 1)
        [HDR] _GlacialMidArctic ("Glacial Mid Arctic (Body)", Color) = (0.15, 0.78, 1.35, 1)
        [HDR] _GlacialApexDiamond ("Glacial Apex Diamond (Highlights)", Color) = (1.45, 1.95, 2.45, 1)

        [Header(State Controller)]
        _StateIntensity ("Skill Intensity", Range(0, 2)) = 0
        _FrozenArmor ("Frozen Armor Sheen", Range(0, 1)) = 0
        _ChargeProgress ("Charge Progress", Range(0, 1)) = 0
        _PhaseLevel ("Phase Level", Range(1, 2)) = 1
        _HitFlash ("Hit Flash", Range(0, 1)) = 0
        _PulseSpeed ("Pulse Speed", Range(0, 15)) = 3.8
        _RimStrength ("Rim Strength", Range(0, 5)) = 1.45
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
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _CrystalColor;
            fixed4 _StormColor;
            fixed4 _EnrageVeinColor;
            fixed4 _EnrageRimColor;

            fixed4 _GlacialDeepNavy;
            fixed4 _GlacialMidArctic;
            fixed4 _GlacialApexDiamond;

            float _StateIntensity;
            float _FrozenArmor;
            float _ChargeProgress;
            float _PhaseLevel;
            float _HitFlash;
            float _PulseSpeed;
            float _RimStrength;

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            // Smooth Continuous 2D Value Noise (No TV Static / No Blocky Grid)
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float smoothNoise(float2 p)
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

            // 2-Octave Domain-Warped FBM for Organic Crystal Frost Veins
            float domainWarpFBM(float2 p, float time)
            {
                float2 q = float2(
                    smoothNoise(p + float2(0.0, 0.0) + time * 0.08),
                    smoothNoise(p + float2(5.2, 1.3) - time * 0.06)
                );

                float2 r = float2(
                    smoothNoise(p + 2.4 * q + float2(1.7, 9.2) + time * 0.12),
                    smoothNoise(p + 2.4 * q + float2(8.3, 2.8) - time * 0.10)
                );

                float f = smoothNoise(p + 2.8 * r);
                return f;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 uv = input.texcoord;
                fixed4 sprite = tex2D(_MainTex, uv) * input.color;
                if (sprite.a <= 0.001)
                    return fixed4(0, 0, 0, 0);

                // --- 1. Sắc Nét 8-Sample Kernel Cho Inner Rim (Khử Răng Cưa Hoàn Toàn) ---
                float2 texel = _MainTex_TexelSize.xy * 1.8;
                float aR  = tex2D(_MainTex, uv + float2(texel.x, 0)).a;
                float aL  = tex2D(_MainTex, uv - float2(texel.x, 0)).a;
                float aU  = tex2D(_MainTex, uv + float2(0, texel.y)).a;
                float aD  = tex2D(_MainTex, uv - float2(0, texel.y)).a;
                float aUR = tex2D(_MainTex, uv + float2(texel.x, texel.y)).a;
                float aUL = tex2D(_MainTex, uv + float2(-texel.x, texel.y)).a;
                float aDR = tex2D(_MainTex, uv + float2(texel.x, -texel.y)).a;
                float aDL = tex2D(_MainTex, uv + float2(-texel.x, -texel.y)).a;

                float minNeighbourAlpha = min(min(min(aR, aL), min(aU, aD)), min(min(aUR, aUL), min(aDR, aDL)));
                float innerRim = saturate((sprite.a - minNeighbourAlpha) * 4.5);

                float phaseTwo = saturate(_PhaseLevel - 1.0);
                float speedMultiplier = lerp(1.0, 1.65, phaseTwo);
                float time = _Time.y * _PulseSpeed * speedMultiplier;

                // --- 2. Boss Normal State Energy Calculations ---
                float power = saturate(_StateIntensity + _ChargeProgress * 0.55);
                float crystalMask = saturate((sprite.g + sprite.b - sprite.r * 0.55) * 0.78);
                float bellyMask = saturate((sprite.r - sprite.b * 0.6) * 1.2);

                fixed3 activeCrystalColor = lerp(_CrystalColor.rgb, _EnrageVeinColor.rgb, phaseTwo);
                fixed3 activeRimColor = lerp(_CrystalColor.rgb, _EnrageRimColor.rgb, phaseTwo);
                float rimGlow = innerRim * _RimStrength * (0.35 + power * 1.1 + phaseTwo * 0.45);
                float enrageHeartbeat = pow(sin(time * 1.8) * 0.5 + 0.5, 4.0) * phaseTwo * (0.2 + bellyMask * 0.4);
                fixed3 standardGlow = activeRimColor * rimGlow + activeCrystalColor * enrageHeartbeat;

                fixed3 baseColor = sprite.rgb;

                // --- 3. AAA Living Crystal Ice Sculpture (Frozen Armor / Frozen Sanctuary) ---
                float frozen = saturate(_FrozenArmor);
                if (frozen > 0.001)
                {
                    // [A] Trích xuất độ sáng Luminance bảo tồn nét vẽ gốc của Artist
                    float lum = dot(baseColor, fixed3(0.299, 0.587, 0.114));

                    // [B] Tri-Tone Glacial Color Grading
                    // Shadow: Deep Glacial Navy -> Mid: Rich Arctic Cyan -> High: Diamond Apex White
                    fixed3 shadowTone = _GlacialDeepNavy.rgb * (lum * 1.8 + 0.1);
                    fixed3 midTone = _GlacialMidArctic.rgb;
                    fixed3 highTone = _GlacialApexDiamond.rgb;

                    fixed3 gradedIce;
                    if (lum < 0.45)
                    {
                        gradedIce = lerp(shadowTone, midTone, lum / 0.45);
                    }
                    else
                    {
                        gradedIce = lerp(midTone, highTone, (lum - 0.45) / 0.55);
                    }

                    // [C] Tái tạo Pháp tuyến 3D Sobel Height Relief từ cấu trúc cơ bắp của Sprite
                    float lumR = dot(tex2D(_MainTex, uv + float2(texel.x * 2.0, 0)).rgb, fixed3(0.299, 0.587, 0.114));
                    float lumL = dot(tex2D(_MainTex, uv - float2(texel.x * 2.0, 0)).rgb, fixed3(0.299, 0.587, 0.114));
                    float lumU = dot(tex2D(_MainTex, uv + float2(0, texel.y * 2.0)).rgb, fixed3(0.299, 0.587, 0.114));
                    float lumD = dot(tex2D(_MainTex, uv - float2(0, texel.y * 2.0)).rgb, fixed3(0.299, 0.587, 0.114));

                    float3 normalRelief = normalize(float3((lumR - lumL) * 2.5, (lumU - lumD) * 2.5, 0.65));

                    // [D] Cấu trúc Gân Băng Nhánh Cây Hữu Cơ (Domain-Warped Dendritic Veins)
                    float veinFBM = domainWarpFBM(uv * 12.0, _Time.y * 0.5);
                    float crystalVeins = pow(1.0 - abs(sin(veinFBM * 3.14159 * 2.2)), 4.0);

                    // [E] Vệt Quét Sáng Lăng Kính & Tán Sắc Quang Phổ (Prismatic Caustic Sheen)
                    float sweepProgress = uv.x * 3.5 + uv.y * 1.5 - _Time.y * 2.0;
                    float sheen = pow(max(0.0, sin(sweepProgress) * 0.5 + 0.5), 14.0);

                    // Specular Highlight phản xạ theo độ cong bề mặt
                    float3 lightDir = normalize(float3(sin(_Time.y * 1.5), 0.8, 0.6));
                    float spec = pow(max(0.0, dot(normalRelief, lightDir)), 18.0) * 0.85;

                    // Prismatic Spectral Dispersion (Tán sắc cầu vồng kim cương)
                    fixed3 prismaticColor = fixed3(
                        sin(sweepProgress + 0.0) * 0.5 + 0.5,
                        sin(sweepProgress + 2.094) * 0.5 + 0.5,
                        sin(sweepProgress + 4.188) * 0.5 + 0.5
                    );
                    fixed3 sheenDispersion = lerp(_GlacialApexDiamond.rgb, prismaticColor * 1.8, 0.35) * (sheen + spec);

                    // [F] Nhịp Thở Thấu Quang Lõi Băng (Cryogenic Bioluminescent Core)
                    float cryoPulse = 0.82 + sin(_Time.y * 3.2 - uv.x * 6.0) * 0.18;

                    // [G] Hòa trộn Băng Sống Giữ Nét Vẽ (Detail-Preserving Luminosity Blend)
                    // Giữ lại 100% chi tiết mắt, vảy, hoa văn qua độ sáng gốc
                    fixed3 livingCrystalIce = gradedIce * (0.45 + 0.55 * baseColor * 1.35);
                    livingCrystalIce += crystalVeins * _GlacialApexDiamond.rgb * 0.45 * cryoPulse;
                    livingCrystalIce += sheenDispersion;

                    // [H] Viền Băng Pha Lê Sắc Lạnh (Crisp Frosted Rim)
                    fixed3 frostRimColor = _GlacialApexDiamond.rgb * pow(innerRim, 1.4) * 1.6;

                    // [I] Biên Độ Đóng Băng Lan Tỏa Đột Phá (Freeze-Crawl Front)
                    float freezeNoise = smoothNoise(uv * 8.0) * 0.22;
                    float freezeFront = smoothstep(0.0, 0.28, frozen * 1.3 - (1.0 - uv.y * 0.75 + freezeNoise));
                    float crystallizationEdge = smoothstep(0.0, 0.08, freezeFront) * (1.0 - smoothstep(0.08, 0.32, freezeFront));

                    // [J] Phản hồi Chấn Động Rạn Nứt Khi Bị Đánh (Hit Fracture Resonance)
                    float hitFlare = saturate(_HitFlash);
                    fixed3 hitFractureGlow = _GlacialApexDiamond.rgb * crystalVeins * hitFlare * 3.5;

                    // Áp dụng hòa trộn mượt mà vào sprite
                    baseColor = lerp(baseColor, livingCrystalIce, freezeFront * 0.94);
                    standardGlow = lerp(standardGlow, frostRimColor + hitFractureGlow, freezeFront * 0.88);
                    standardGlow += crystallizationEdge * _GlacialApexDiamond.rgb * 2.2;
                }

                // Phase 2 Enrage Body Tint (Khi không đóng băng)
                baseColor = lerp(
                    baseColor,
                    baseColor * fixed3(1.15, 0.78, 0.82) + fixed3(0.08, 0.01, 0.02),
                    phaseTwo * 0.42 * (1.0 - frozen * 0.85));

                baseColor += standardGlow * sprite.a;

                // Hit Flash tổng thể
                float flash = saturate(_HitFlash);
                baseColor = lerp(baseColor, fixed3(1.8, 2.2, 2.5), flash * 0.65 * sprite.a);

                fixed4 result = fixed4(baseColor, sprite.a);
                result.rgb *= result.a;
                return result;
            }
            ENDCG
        }
    }
}
