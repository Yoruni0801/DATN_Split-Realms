Shader "Custom/2D/Fantasy Waterfall"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0

        [Header(Mau Sac Nuoc)]
        _DeepWaterColor ("Màu Nước Sâu (Đáy)", Color) = (0.04, 0.20, 0.40, 0.85)
        _WaterColor ("Màu Thân Nước Chính", Color) = (0.08, 0.55, 0.82, 0.9)
        _HighlightColor ("Màu Sáng Cyan (Viền & Lấp Lánh)", Color) = (0.40, 0.95, 1.0, 1.0)
        _FoamColor ("Màu Bọt Trắng", Color) = (0.94, 0.98, 1.0, 1.0)

        [Header(1 Toc Do Va Dong Chay Doc)]
        _FlowSpeed ("Tốc Độ Chảy Xuống", Range(0.5, 15.0)) = 5.0
        _GravityStretch ("Gia Tốc Kéo Dãn Trọng Lực", Range(0, 1.5)) = 0.5

        [Header(2 Cac Dai Bot Nuoc Doc)]
        _StreakDensity ("Mật Độ Cột Nước Dọc", Range(2, 30)) = 14.0
        _StreakLength ("Độ Dài Dải Nước (Kéo Dãn Y)", Range(0.05, 1.5)) = 0.35
        _StreakCover ("Độ Bao Phủ Bọt Trắng", Range(0.1, 0.9)) = 0.48
        _StreakSoftness ("Độ Mềm Của Dải Bọt", Range(0.01, 0.5)) = 0.20
        _FastLayerSpeed ("Hệ Số Tốc Độ Lớp Bọt Phụ", Range(1.0, 3.0)) = 1.45

        [Header(3 Gan Nuoc Lop Lanh Voronoi)]
        _RibbonScale ("Mật Độ Gân Nước", Range(2.0, 30.0)) = 12.0
        _RibbonStretch ("Độ Kéo Dài Gân Nước Y", Range(0.05, 1.0)) = 0.25
        _RibbonStrength ("Cường Độ Gân Bọt Sáng", Range(0, 2.0)) = 1.0

        [Header(4 Uon Luon Than Va Vien Thac)]
        _SilhouetteSpeed ("Tốc Độ Uốn Lượn Viền", Range(0.1, 4.0)) = 1.2
        _BodySwayAmp ("Độ Đung Đưa Thân Thác (Ngang)", Range(0, 0.1)) = 0.025
        _BodySwayFreq ("Tần Số Đung Đưa Thân Thác", Range(0.2, 5.0)) = 1.5
        _LeftEdgeAmp ("Độ Gợn Mép Trái", Range(0, 0.1)) = 0.03
        _RightEdgeAmp ("Độ Gợn Mép Phải", Range(0, 0.1)) = 0.03
        _EdgeWobbleFreq ("Tần Số Gợn Mép", Range(0.5, 8.0)) = 2.5
        _BottomSpread ("Độ Xòe Nước Chân Thác", Range(0, 0.2)) = 0.05
        _EdgeSoftness ("Độ Mềm Mép Ngoài", Range(0.005, 0.15)) = 0.03

        [Header(5 Phat Sang Vien Va Do Trong Suot)]
        _WaterOpacity ("Độ Đậm Nước Tổng Thể", Range(0, 1.0)) = 0.85
        _TransparencyVar ("Biến Thiên Trong Suốt (Lồi Lõm)", Range(0, 1.0)) = 0.30
        _GlowStrength ("Cường Độ Viền Phát Sáng Cyan", Range(0, 2.0)) = 0.80
        _EdgeGlowWidth ("Độ Rộng Viền Phát Sáng", Range(0.01, 0.3)) = 0.12

        [Header(6 Bot Dinh Va Chan Thac)]
        _TopLipHeight ("Độ Cao Bọt Đỉnh Thác", Range(0, 0.2)) = 0.05
        _BottomSplashHeight ("Độ Cao Bọt Chân Thác Va Đập", Range(0, 0.35)) = 0.08
        _FoamIntensity ("Cường Độ Bọt Trắng Va Đập", Range(0, 2.0)) = 1.2

        [Header(7 Do Sang Tong The)]
        _MasterBrightness ("Độ Sáng Tổng Thể", Range(0.5, 2.5)) = 1.15
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
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA

            #include "UnityCG.cginc"

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
                float2 worldPos : TEXCOORD1;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            sampler2D _AlphaTex;
            float4 _MainTex_ST;
            float _EnableExternalAlpha;

            fixed4 _DeepWaterColor;
            fixed4 _WaterColor;
            fixed4 _HighlightColor;
            fixed4 _FoamColor;

            float _FlowSpeed;
            float _GravityStretch;

            float _StreakDensity;
            float _StreakLength;
            float _StreakCover;
            float _StreakSoftness;
            float _FastLayerSpeed;

            float _RibbonScale;
            float _RibbonStretch;
            float _RibbonStrength;

            float _SilhouetteSpeed;
            float _BodySwayAmp;
            float _BodySwayFreq;
            float _LeftEdgeAmp;
            float _RightEdgeAmp;
            float _EdgeWobbleFreq;
            float _BottomSpread;
            float _EdgeSoftness;

            float _WaterOpacity;
            float _TransparencyVar;
            float _GlowStrength;
            float _EdgeGlowWidth;

            float _TopLipHeight;
            float _BottomSplashHeight;
            float _FoamIntensity;

            float _MasterBrightness;

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

            float fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.52;
                for (int i = 0; i < 3; i++)
                {
                    value += smoothNoise(p) * amplitude;
                    p = p * 2.08 + float2(1.7, 9.2);
                    amplitude *= 0.48;
                }
                return value;
            }

            // Stretched Voronoi for vertical crystalline water ribbons
            float StretchedVoronoi(float2 p)
            {
                float2 n = floor(p);
                float2 f = frac(p);
                float md = 8.0;

                for (int j = -1; j <= 1; j++)
                {
                    for (int i = -1; i <= 1; i++)
                    {
                        float2 g = float2(float(i), float(j));
                        float2 o = float2(hash21(n + g), hash21(n + g + float2(4.1, 7.3)));
                        float2 r = g + o - f;
                        float d = dot(r, r);
                        md = min(md, d);
                    }
                }
                return sqrt(md);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xy;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.uv;
                float2 wPos = IN.worldPos;

                float tSlow = _Time.y * _SilhouetteSpeed;
                float tFast = _Time.y * _FlowSpeed;

                // -------------------------------------------------------------
                // 1. ORGANIC SILHOUETTE (Viền trái phải uốn lượn tự nhiên)
                // -------------------------------------------------------------
                float heightFactor = 1.0 - uv.y; // 0 ở đỉnh thác -> 1 ở đáy thác

                // Uốn lượn nhẹ toàn thân thác theo chiều ngang
                float bodySway = sin(uv.y * _BodySwayFreq + tSlow * 1.5) * _BodySwayAmp * heightFactor;

                // Mép trái & phải dao động độc lập
                float leftNoise = (smoothNoise(float2(uv.y * _EdgeWobbleFreq, tSlow * 0.8)) - 0.5) * _LeftEdgeAmp;
                float rightNoise = (smoothNoise(float2(uv.y * _EdgeWobbleFreq + 5.12, tSlow * 0.8 + 2.4)) - 0.5) * _RightEdgeAmp;

                float bottomExpand = heightFactor * _BottomSpread;

                float leftBorder = 0.0 - leftNoise - bodySway - bottomExpand * 0.5;
                float rightBorder = 1.0 + rightNoise - bodySway + bottomExpand * 0.5;

                float deformedX = (uv.x - leftBorder) / max(0.001, (rightBorder - leftBorder));
                float edgeDist = min(deformedX, 1.0 - deformedX);

                float silhouetteMask = smoothstep(0.0, _EdgeSoftness, deformedX) * smoothstep(1.0, 1.0 - _EdgeSoftness, deformedX);
                if (silhouetteMask <= 0.001) discard;

                fixed4 spriteMask = tex2D(_MainTex, uv);
                #if ETC1_EXTERNAL_ALPHA
                if (_EnableExternalAlpha > 0)
                {
                    spriteMask.a = tex2D(_AlphaTex, uv).r;
                }
                #endif
                if (spriteMask.a < 0.01) discard;

                // -------------------------------------------------------------
                // 2. VERTICAL FLOW COORDINATES (Chảy từ trên xuống dưới)
                // -------------------------------------------------------------
                float accel = 1.0 + _GravityStretch * heightFactor;
                float flowY = uv.y + tFast * accel;

                // Rung nhẹ toạ độ X để dòng nước uốn lượn bên trong
                float internalWobble = (smoothNoise(float2(deformedX * 4.0, flowY * 0.4)) - 0.5) * 0.04;
                float flowX = deformedX + internalWobble;

                // -------------------------------------------------------------
                // 3. VERTICAL FOAM STREAKS (Dải bọt nước kéo dài dọc theo trục Y)
                // -------------------------------------------------------------
                float2 sUV1 = float2(flowX * _StreakDensity, flowY * _StreakLength);
                float n1 = fbm(sUV1);

                float2 sUV2 = float2(flowX * (_StreakDensity * 1.5) + 3.7, (uv.y + tFast * _FastLayerSpeed * accel) * (_StreakLength * 1.2));
                float n2 = fbm(sUV2);

                float cutoff = 1.0 - _StreakCover;
                float streak1 = smoothstep(cutoff - _StreakSoftness, cutoff + _StreakSoftness, n1);
                float streak2 = smoothstep(cutoff - _StreakSoftness * 0.8, cutoff + _StreakSoftness * 0.8, n2);
                float verticalStreaks = saturate(streak1 * 0.45 + streak2 * 0.7 + streak1 * streak2 * 0.85);

                // -------------------------------------------------------------
                // 4. CRYSTALLINE VERTICAL RIBBONS (Gân nước / sợi nước lấp lánh)
                // -------------------------------------------------------------
                float2 ribbonUV = float2(flowX * _RibbonScale, (uv.y + tFast * 1.2 * accel) * _RibbonStretch);
                float vDist = StretchedVoronoi(ribbonUV);
                float ribbonFoam = smoothstep(0.4, 0.9, 1.0 - vDist) * _RibbonStrength;

                float totalWhiteFlow = saturate(verticalStreaks * 0.65 + ribbonFoam * 0.75);

                // -------------------------------------------------------------
                // 5. COLOR COMPOSITING & CYAN GLOW
                // -------------------------------------------------------------
                float3 waterBase = lerp(_DeepWaterColor.rgb, _WaterColor.rgb, n1 * 0.8 + 0.2);

                float edgeGlow = smoothstep(_EdgeGlowWidth, 0.0, saturate(edgeDist)) * _GlowStrength;
                waterBase += _HighlightColor.rgb * edgeGlow;

                float3 finalRGB = lerp(waterBase, _HighlightColor.rgb, saturate(totalWhiteFlow * 0.6));
                finalRGB = lerp(finalRGB, _FoamColor.rgb, saturate(totalWhiteFlow * 0.95));

                // -------------------------------------------------------------
                // 6. TOP LIP & BOTTOM SPLASH (Bọt đỉnh và chân thác va chạm)
                // -------------------------------------------------------------
                float lipNoise = fbm(float2(flowX * 6.0, tFast * 0.3));
                float topLip = smoothstep(1.0 - _TopLipHeight, 1.0, uv.y) * saturate(lipNoise * 1.5);

                float bottomSplashNoise = fbm(float2(flowX * 8.0, tFast * 0.6));
                float bottomSplash = smoothstep(_BottomSplashHeight, 0.0, uv.y) * saturate(bottomSplashNoise * 1.8 + ribbonFoam * 0.5);

                float totalEdgeFoam = saturate((topLip + bottomSplash) * _FoamIntensity);
                finalRGB = lerp(finalRGB, _FoamColor.rgb, totalEdgeFoam);

                finalRGB *= _MasterBrightness * IN.color.rgb;

                // -------------------------------------------------------------
                // 7. VARIABLE ALPHA TRANSPARENCY
                // -------------------------------------------------------------
                float alphaNoise = fbm(float2(flowX * 3.0, flowY * 0.3));
                float varAlpha = lerp(1.0 - _TransparencyVar, 1.0, alphaNoise);

                float baseAlpha = _WaterOpacity * spriteMask.a * IN.color.a * silhouetteMask * varAlpha;
                float finalAlpha = saturate(lerp(baseAlpha, 1.0, saturate(totalWhiteFlow * 0.4 + totalEdgeFoam * 0.85)));

                return fixed4(finalRGB, finalAlpha);
            }
            ENDCG
        }
    }
    FallBack "Sprites/Default"
}
