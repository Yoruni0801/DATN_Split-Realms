Shader "Custom/2D/Forest Realistic Stylized Waterfall"
{
    Properties
    {
        [PerRendererData] _MainTex ("Water Mask / Sprite", 2D) = "white" {}
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0

        [Header(Water Palette)]
        _DeepColor ("Deep Abyss Color", Color) = (0.06, 0.22, 0.32, 1)
        _BodyTint ("Crystal Water Body", Color) = (0.12, 0.45, 0.55, 1)
        _RimTint ("Sunlit Surface Tint", Color) = (0.35, 0.78, 0.75, 1)
        _CascadeFoam ("Cascade Foam White", Color) = (0.95, 0.99, 0.98, 1)
        _OpacityMaster ("Overall Opacity", Range(0.2, 1.0)) = 0.88

        [Header(Waterfall Dynamics)]
        _FallSpeed ("Main Fall Speed", Range(0.5, 8.0)) = 2.6
        _GravityAccel ("Gravity Acceleration", Range(0.0, 1.5)) = 0.45
        _FastLayerMultiplier ("Fast Strand Multiplier", Range(1.0, 3.0)) = 1.65
        _TurbulenceScale ("Turbulence Scale", Range(0.5, 5.0)) = 2.4
        _WarpStrength ("Flow Curving Strength", Range(0.0, 0.4)) = 0.12

        [Header(Silhouette and Shape)]
        _BodySwayAmp ("Body Sway Amplitude", Range(0.0, 0.04)) = 0.012
        _BodySwayFreq ("Body Sway Frequency", Range(0.5, 5.0)) = 1.8
        _BodySwaySpeed ("Body Sway Speed", Range(0.2, 3.0)) = 1.2
        _EdgeWobbleAmp ("Edge Wave Amplitude", Range(0.0, 0.05)) = 0.015
        _EdgeWobbleFreq ("Edge Wave Frequency", Range(1.0, 6.0)) = 2.5
        _EdgeWobbleSpeed ("Edge Wave Speed", Range(0.5, 4.0)) = 1.5
        _TopPinch ("Top Lip Pinch", Range(0.0, 0.15)) = 0.04
        _BottomSpread ("Bottom Basin Spread", Range(0.0, 0.2)) = 0.06
        _EdgeSoftness ("Edge Softness", Range(0.01, 0.25)) = 0.08

        [Header(Foam Strands and Details)]
        _StreakCols ("Foam Strand Density", Range(3.0, 18.0)) = 8.5
        _StreakLen ("Foam Strand Length", Range(0.5, 4.0)) = 1.4
        _StreakCover ("Foam Coverage", Range(0.1, 0.85)) = 0.42
        _StreakSoftness ("Foam Softness", Range(0.01, 0.4)) = 0.20
        _CausticsIntensity ("Caustics Sparkle", Range(0.0, 2.0)) = 0.85
        _MicroBubblesIntensity ("Micro-bubbles Intensity", Range(0.0, 1.5)) = 0.65

        [Header(Top Crest and Bottom Crash)]
        _LipHeight ("Top Crest Height", Range(0.01, 0.3)) = 0.08
        _SplashHeight ("Bottom Crash Foam Height", Range(0.01, 0.35)) = 0.14
        _BorderWidth ("Side Foam Width", Range(0.01, 0.15)) = 0.045

        [Header(Laser Transmission)]
        _LaserBeamColor ("Laser Glow Color", Color) = (1.0, 0.75, 0.22, 1)
        _LaserGlowRadius ("Laser Glow Radius", Range(0.1, 2.0)) = 0.65
        _LaserGlowIntensity ("Laser Glow Intensity", Range(0.5, 5.0)) = 2.8
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
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _AlphaTex;
            float4 _MainTex_ST;
            float _EnableExternalAlpha;

            fixed4 _DeepColor;
            fixed4 _BodyTint;
            fixed4 _RimTint;
            fixed4 _CascadeFoam;
            float _OpacityMaster;

            float _FallSpeed;
            float _GravityAccel;
            float _FastLayerMultiplier;
            float _TurbulenceScale;
            float _WarpStrength;

            float _BodySwayAmp;
            float _BodySwayFreq;
            float _BodySwaySpeed;
            float _EdgeWobbleAmp;
            float _EdgeWobbleFreq;
            float _EdgeWobbleSpeed;
            float _TopPinch;
            float _BottomSpread;
            float _EdgeSoftness;

            float _StreakCols;
            float _StreakLen;
            float _StreakCover;
            float _StreakSoftness;
            float _CausticsIntensity;
            float _MicroBubblesIntensity;

            float _LipHeight;
            float _SplashHeight;
            float _BorderWidth;

            fixed4 _LaserBeamColor;
            float _LaserGlowRadius;
            float _LaserGlowIntensity;

            // Driven by WaterRefractionDeflector:
            float4 _LaserTransmissionPoint; // xy: Laser World Pos, z: Intensity, w: Active
            float4 _LaserTransmissionDir;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 worldPos : TEXCOORD1;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float SmoothNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float FBM(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                for (int i = 0; i < 4; i++)
                {
                    value += amplitude * SmoothNoise(p);
                    p *= 2.05;
                    amplitude *= 0.5;
                }
                return value;
            }

            // Caustics lấp lánh đáy nước chuẩn AAA
            float WaterCaustics(float2 p, float time)
            {
                float2 p1 = p * 1.6 + float2(time * 0.35, -time * 0.7);
                float2 p2 = p * 2.4 + float2(-time * 0.45, time * 0.5);

                float c1 = abs(sin(p1.x + cos(p1.y * 1.2) * 1.5) * cos(p1.y + sin(p1.x * 1.2) * 1.5));
                float c2 = abs(sin(p2.x + cos(p2.y * 1.3) * 1.4) * cos(p2.y + sin(p2.x * 1.3) * 1.4));

                return pow(saturate(c1 * 0.55 + c2 * 0.55), 2.2);
            }

            fixed4 SampleSpriteTexture(float2 uv)
            {
                fixed4 color = tex2D(_MainTex, uv);
            #if ETC1_EXTERNAL_ALPHA
                if (_EnableExternalAlpha > 0.5)
                    color.a = tex2D(_AlphaTex, uv).r;
            #endif
                return color;
            }

            v2f vert(appdata IN)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(IN.vertex);
                o.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                o.color = IN.color;
                o.worldPos = mul(unity_ObjectToWorld, IN.vertex).xy;
                return o;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.uv;
                float2 worldUV = IN.worldPos;

                // --- TIMING ---
                float tFall = _Time.y * _FallSpeed;
                float tSway = _Time.y * _BodySwaySpeed;
                float tWobble = _Time.y * _EdgeWobbleSpeed;

                // --- 1. THÂN THÁC HỮU CƠ MƯỢT MÀ (Smooth Aerodynamic Silhouette) ---
                float heightFactor = saturate(1.0 - uv.y); // 0 ở đỉnh, 1 ở chân thác

                // Thân thác đung đưa nhịp nhàng
                float bodySway = sin(uv.y * _BodySwayFreq + tSway) * _BodySwayAmp * heightFactor;

                // Gợn sóng êm ái hai bên mép (loại bỏ hoàn toàn răng cưa nhọn)
                float leftEdgeWave = (SmoothNoise(float2(uv.y * _EdgeWobbleFreq, tWobble)) - 0.5) * _EdgeWobbleAmp;
                float rightEdgeWave = (SmoothNoise(float2(uv.y * _EdgeWobbleFreq + 4.3, tWobble + 2.1)) - 0.5) * _EdgeWobbleAmp;

                // Đỉnh thác thon gọn, chân thác xòe rộng
                float widthMod = lerp(-_TopPinch, _BottomSpread, pow(heightFactor, 1.3));

                float leftBound = 0.05 - leftEdgeWave - bodySway - widthMod * 0.5;
                float rightBound = 0.95 + rightEdgeWave - bodySway + widthMod * 0.5;

                // Toạ độ ngang chuẩn hóa theo dòng chảy uốn lượn
                float flowX = saturate((uv.x - leftBound) / max(0.001, (rightBound - leftBound)));
                float distFromCenter = abs(flowX - 0.5) * 2.0; // 0 ở giữa, 1 ở mép
                float edgeDist = 1.0 - distFromCenter; // 1 ở giữa, 0 ở mép

                // Độ mờ viền ngoài cùng mượt mà
                float edgeAlpha = smoothstep(0.0, _EdgeSoftness, edgeDist);
                if (edgeAlpha <= 0.001) discard;

                // Đỉnh và đáy thác bo tròn mềm mại
                float verticalMask = smoothstep(0.0, 0.03, uv.y) * smoothstep(1.0, 0.96, uv.y);

                // --- 2. DÒNG CHẢY TRỌNG LỰC & DOMAIN WARPING ---
                // Gia tốc trọng lực làm dòng nước rơi nhanh và kéo dãn dần về phía chân thác
                float gravityStretch = 1.0 + _GravityAccel * heightFactor;
                float yFall = worldUV.y + tFall * gravityStretch;

                // Uốn lượn dòng nước tự nhiên
                float2 warpUV = float2(flowX * _TurbulenceScale, worldUV.y * 0.5 + tFall * 0.2);
                float2 flowWarp = float2(
                    FBM(warpUV) - 0.5,
                    FBM(warpUV + float2(3.7, 1.8)) - 0.5
                ) * _WarpStrength;

                float2 streamUV = float2(flowX, yFall) + flowWarp;

                // --- 3. PHỐI MÀU THÂN NƯỚC TRONG SUỐT (Crystal Abyss to Sunlit Surface) ---
                // Thân nước luôn LIỀN MẠCH, KHÔNG BỊ RÁCH LỖ ĐEN
                float depthFactor = smoothstep(0.0, 0.6, edgeDist);
                fixed3 waterBase = lerp(_DeepColor.rgb, _BodyTint.rgb, depthFactor);

                // Viền phản quang ánh sáng trời
                float rimGlow = smoothstep(0.4, 0.05, edgeDist) * 0.55;
                rimGlow += FBM(float2(streamUV.x * 3.0, streamUV.y * 0.8 - tFall * 0.3)) * 0.25;
                waterBase = lerp(waterBase, _RimTint.rgb, saturate(rimGlow));

                // Caustics lấp lánh ánh sáng trong dòng nước
                float caustics = WaterCaustics(float2(flowX * 5.0, streamUV.y * 1.8), _Time.y * 2.2);
                waterBase += _RimTint.rgb * (caustics * _CausticsIntensity * depthFactor);

                // --- 4. DẢI BỌT NƯỚC CHẢY XIẾT (Rushing Foam Strands & Filaments) ---
                // Lớp bọt chính
                float2 sUV1 = float2(streamUV.x * _StreakCols, streamUV.y * _StreakLen);
                float n1 = FBM(sUV1);
                float cutoff1 = 1.0 - _StreakCover;
                float strand1 = smoothstep(cutoff1 - _StreakSoftness, cutoff1 + _StreakSoftness, n1);

                // Lớp bọt chạy nhanh x1.65 (Fast turbulent foam)
                float fastY = worldUV.y + tFall * _FastLayerMultiplier * gravityStretch;
                float2 sUV2 = float2(streamUV.x * (_StreakCols * 1.35) + 4.2, (fastY + flowWarp.y * 2.0) * (_StreakLen * 1.15));
                float n2 = FBM(sUV2);
                float cutoff2 = 1.0 - (_StreakCover * 0.75);
                float strand2 = smoothstep(cutoff2 - _StreakSoftness, cutoff2 + _StreakSoftness, n2);

                // Hạt bọt li ti lao nhanh (Micro-bubbles)
                float2 mbUV = float2(flowX * 18.0, (worldUV.y + tFall * 2.2) * 3.5);
                float microBubbles = smoothstep(0.68, 0.88, SmoothNoise(mbUV)) * _MicroBubblesIntensity;

                float foamStrands = saturate(strand1 * 0.55 + strand2 * 0.65 + strand1 * strand2 * 0.8 + microBubbles);

                // --- 5. ĐẦU NGUỒN (Top Crest) & CHÂN THÁC SÔI TRÀO (Bottom Crash) ---
                // Bọt nước tràn đầu thác
                float crestNoise = FBM(float2(flowX * 6.0, worldUV.y * 2.5 + tFall * 0.25));
                float topCrest = smoothstep(1.0 - _LipHeight, 1.0, uv.y) * saturate(crestNoise * 1.4);

                // Bọt sóng chân thác cuộn trào sôi sục
                float crashNoise = FBM(float2(flowX * 8.0, worldUV.y * 3.5 - tFall * 0.4));
                float bottomCrash = smoothstep(_SplashHeight, 0.0, uv.y) * saturate(crashNoise * 1.5);
                // Bọt cuộn phình ra ở chân thác
                bottomCrash += smoothstep(_SplashHeight * 0.5, 0.0, uv.y) * 0.4;

                // Viền bọt hai bên mép thác
                float sideBorderNoise = FBM(float2(flowX * 4.0, streamUV.y * 1.6));
                float sideBorderFoam = smoothstep(_BorderWidth, 0.0, edgeDist) * saturate(sideBorderNoise * 1.25);

                // Tổng hợp bọt nước
                float totalFoam = saturate(foamStrands + topCrest + bottomCrash + sideBorderFoam);
                totalFoam = smoothstep(0.12, 0.72, totalFoam);

                // --- 6. KHÚC XẠ TRUYỀN SÁNG TIA LASER (Laser Transmission Glow) ---
                fixed3 laserScattering = fixed3(0,0,0);
                if (_LaserTransmissionPoint.z > 0.01)
                {
                    float2 laserPos = _LaserTransmissionPoint.xy;
                    float distToLaser = length(worldUV - laserPos);

                    if (distToLaser < _LaserGlowRadius * 2.5)
                    {
                        float laserCore = saturate(1.0 - (distToLaser / (_LaserGlowRadius * 2.5)));
                        laserCore = pow(laserCore, 1.5) * _LaserTransmissionPoint.z;

                        // Nước sôi sùng sục tán xạ ánh sáng vàng rực
                        float boilScatter = SmoothNoise(worldUV * 16.0 + float2(0.0, _Time.y * 9.0));
                        laserCore *= (0.75 + 0.5 * boilScatter);

                        fixed3 glowColor = lerp(_LaserBeamColor.rgb, fixed3(1.0, 0.98, 0.85), laserCore * 0.65);
                        laserScattering = glowColor * (laserCore * _LaserGlowIntensity);
                    }
                }

                // --- 7. TỔNG HỢP MÀU SẮC & ALPHA HOÀN THIỆN ---
                fixed3 finalRGB = lerp(waterBase, _CascadeFoam.rgb, totalFoam);
                finalRGB += laserScattering; // Truyền sáng rực rỡ khi tia laser xuyên qua
                finalRGB *= IN.color.rgb;

                // Alpha mượt mà, thân nước luôn giữ độ đục tự nhiên, bọt nước đậm hơn
                float baseAlpha = _OpacityMaster * IN.color.a * edgeAlpha * verticalMask;
                float finalAlpha = saturate(lerp(baseAlpha * 0.88, baseAlpha * 1.12, totalFoam));

                return fixed4(finalRGB, finalAlpha);
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}