Shader "Bao/MiniGame/ArchDoorwayPortal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        [Header(Color Scheme)]
        [HDR] _NeonColor ("Neon Border Glow (Image 2 Style)", Color) = (2.5, 0.45, 0.1, 1.0)
        _InnerColorA ("Upper Portal Flame", Color) = (1.0, 0.22, 0.04, 1.0)
        _InnerColorB ("Deep Crimson Abyss", Color) = (0.55, 0.02, 0.06, 1.0)
        [HDR] _CoreLightColor ("Core Blinding Light", Color) = (2.5, 2.1, 1.4, 1.0)
        _FrameColor ("Doorway Outer Frame Tint", Color) = (0.16, 0.04, 0.02, 0.95)

        [Header(Arch Geometry and Frame)]
        _ArchRoundness ("Arch Top Roundness", Range(1.5, 4.0)) = 2.25
        _FrameThickness ("Outer Frame Width", Range(0.02, 0.25)) = 0.085
        _NeonThickness ("Neon Line Sharpness", Range(0.005, 0.08)) = 0.02
        _NeonGlowSpread ("Neon Glow Spread", Range(0.01, 0.25)) = 0.075
        _BottomLine ("Bottom Floor Level", Range(0.0, 0.2)) = 0.035

        [Header(Volumetric Rays and Light Flooding)]
        _RayDensity ("God Rays Density", Range(2.0, 30.0)) = 14.0
        _RaySpeed ("Rays Shimmer Speed", Range(-5.0, 5.0)) = 1.6
        _RayStrength ("Rays Intensity", Range(0.0, 3.0)) = 1.3
        _CoreFloodHeight ("Core Light Spread Height", Range(0.1, 1.0)) = 0.55

        [Header(Ethereal Dust and Sparks)]
        _SparkleSpeed ("Sparks Drift Speed", Range(0.1, 5.0)) = 1.8
        _SparkleAmount ("Sparks Amount", Range(0.0, 3.0)) = 1.5
        _OuterBurstSpread ("Outer Radiance Burst", Range(0.0, 0.35)) = 0.16

        [Header(Master Controls)]
        _Alpha ("Master Opacity", Range(0.0, 1.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
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
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            float4 _NeonColor;
            float4 _InnerColorA;
            float4 _InnerColorB;
            float4 _CoreLightColor;
            float4 _FrameColor;

            float _ArchRoundness;
            float _FrameThickness;
            float _NeonThickness;
            float _NeonGlowSpread;
            float _BottomLine;

            float _RayDensity;
            float _RaySpeed;
            float _RayStrength;
            float _CoreFloodHeight;

            float _SparkleSpeed;
            float _SparkleAmount;
            float _OuterBurstSpread;

            float _Alpha;

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

            // Pseudo-random hash
            float hash21(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            // Smooth noise
            float snoise(float2 p)
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

            // Fractal noise
            float fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int j = 0; j < 3; j++)
                {
                    v += snoise(p) * a;
                    p *= 2.1;
                    a *= 0.48;
                }
                return v;
            }

            // Helper function declared before frag
            float SafeCoreHeight(float h)
            {
                return max(0.05, h);
            }

            // Tính toán khoảng cách tới đường viền vòm cổng (Signed Arch Profile)
            float2 EvaluateArchDistance(float2 uv, float frameThick)
            {
                // Chuẩn hóa X quanh tâm 0.0, phạm vi [-1, 1]
                float normX = (uv.x - 0.5) * 2.0;
                float absX = abs(normX);

                // Đường cong vòm ngoài đỉnh
                float archTopOuter = 0.98 - pow(saturate(absX), _ArchRoundness) * 0.98;
                float distOuterTop = archTopOuter - uv.y;
                float distOuterSide = 0.98 - absX;
                float distOuterBottom = uv.y - _BottomLine;
                float distOuter = min(distOuterTop, min(distOuterSide, distOuterBottom));

                // Đường viền bên trong (Inner Neon Border)
                float innerNormX = absX / max(0.01, 1.0 - frameThick * 1.8);
                float archTopInner = (0.98 - frameThick) - pow(saturate(innerNormX), _ArchRoundness) * (0.98 - frameThick);
                float distInnerTop = archTopInner - uv.y;
                float distInnerSide = (1.0 - frameThick * 1.8) - absX;
                float distInnerBottom = uv.y - (_BottomLine + frameThick * 0.5);
                float distInner = min(distInnerTop, min(distInnerSide, distInnerBottom));

                return float2(distOuter, distInner);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float timeVal = _Time.y;

                // 1. TÍNH TOÁN CÁC MIỀN HÌNH HỌC (ARCH GEOMETRY)
                float2 archDist = EvaluateArchDistance(uv, _FrameThickness);
                float distOuter = archDist.x;
                float distInner = archDist.y;

                // Mask vùng trong lòng cổng (Door Opening)
                float innerMask = smoothstep(-0.005, 0.01, distInner);

                // Mask vùng khung cổng (Outer Frame)
                float outerMask = smoothstep(-0.005, 0.01, distOuter);
                float frameMask = saturate(outerMask - innerMask);

                // 2. VIỀN NEON RỰC SÁNG (NEON OUTLINE BORDER NHƯ IMAGE #2)
                float absDistToNeon = abs(distInner);
                
                // Tia lõi neon sáng chói
                float neonCore = exp(-pow(absDistToNeon / max(0.001, _NeonThickness), 2.0));
                
                // Quầng sáng tỏa ra (Bloom Halo) quanh viền neon
                float neonGlow = exp(-pow(absDistToNeon / max(0.001, _NeonGlowSpread), 1.5)) * 0.9;
                float totalNeon = neonCore * 1.8 + neonGlow;

                // 3. LÒNG CỔNG PHÁT SÁNG & TIA SÁNG THẦN THÁNH (GOD RAYS & LIGHT FLOOD)
                float normInnerX = (uv.x - 0.5) * 2.0;

                // God Rays chiếu dọc từ dưới lên
                float rayAngle = normInnerX * _RayDensity + snoise(float2(normInnerX * 4.0, timeVal * _RaySpeed * 0.5)) * 1.5;
                float rays = sin(rayAngle + timeVal * _RaySpeed) * 0.5 + 0.5;
                rays = pow(rays, 2.5) * _RayStrength;

                // Sương khói chuyển động FBM
                float2 smokeUV = float2(uv.x * 3.5, uv.y * 2.0 - timeVal * 0.6);
                float smoke = fbm(smokeUV);

                // Nguồn sáng cực mạnh từ đáy cổng tỏa lên (Bottom Core Light Flooding)
                float coreHeight = saturate((uv.y - _BottomLine) / SafeCoreHeight(_CoreFloodHeight));
                float coreGlow = pow(1.0 - coreHeight, 1.8) * 1.6;
                
                // Chiều ngang nguồn sáng tâm
                float coreCenter = exp(-pow(normInnerX / 0.55, 2.0));
                float totalCoreLight = coreGlow * coreCenter;

                // Năng lượng tổng thể trong lòng cổng
                float interiorEnergy = saturate(smoke * 0.7 + rays * 0.55 + totalCoreLight * 0.9);

                // Màu sắc chuyển tầng trong lòng cổng (Đỏ thẫm -> Đỏ cam rực)
                float4 innerCol = lerp(_InnerColorB, _InnerColorA, saturate(interiorEnergy + (1.0 - coreHeight) * 0.5));
                // Hòa trộn với luồng sáng chói trắng vàng ở đáy
                innerCol = lerp(innerCol, _CoreLightColor, saturate(totalCoreLight * 1.3));

                // 4. BỤI SÁNG & TIA SÁNG BẮN RA NGOÀI (SPARKS & RADIANCE BURST)
                float2 sparkUV = float2(uv.x * 14.0, uv.y * 14.0 - timeVal * _SparkleSpeed);
                float sparks = pow(hash21(floor(sparkUV)), 16.0) * _SparkleAmount;
                sparks *= sin(timeVal * 8.0 + uv.x * 20.0) * 0.5 + 0.5;

                // Quầng sáng bắn tia ra ngoài khung cổng (Radiance Burst như Image #2)
                float outerBurst = smoothstep(0.0, -_OuterBurstSpread, distOuter);
                outerBurst = pow(1.0 - outerBurst, 3.0) * snoise(float2(normInnerX * 8.0, timeVal * 2.0)) * 0.7;

                // 5. TỔNG HỢP MÀU SẮC CUỐI CÙNG (COMPOSITING)
                float4 finalColor = float4(0, 0, 0, 0);

                // Nền khung cửa
                finalColor += _FrameColor * frameMask;

                // Lòng cổng năng lượng
                finalColor += innerCol * innerMask;

                // Viền Neon rực rỡ
                finalColor += _NeonColor * totalNeon;

                // Hạt bụi sáng (Sparks)
                finalColor += _CoreLightColor * sparks * (innerMask + frameMask * 0.5);

                // Quầng sáng tỏa ra ngoài cửa
                finalColor += _NeonColor * outerBurst;

                // 6. TÍNH TOÁN ALPHA CHUẨN XÁC
                float masterMask = saturate(outerMask + totalNeon * 0.85 + outerBurst * 0.65);
                float finalAlpha = masterMask * _Alpha;

                // Nhân alpha nếu có texture sprite ngoài
                fixed4 tex = tex2D(_MainTex, uv);
                if (tex.a > 0.01)
                {
                    finalAlpha *= tex.a;
                }

                finalColor.a = saturate(finalAlpha);
                finalColor *= i.color;

                return finalColor;
            }
            ENDCG
        }
    }
}
