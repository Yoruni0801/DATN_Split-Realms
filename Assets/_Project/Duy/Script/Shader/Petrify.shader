Shader "Custom/2D/Petrify Bottom Up Group Improved"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Petrify)]
        _PetrifyAmount ("Petrify Amount", Range(0,1)) = 0
        _EdgeSoftness ("Edge Softness", Range(0.001,0.3)) = 0.08

        [Header(Stone Color)]
        _StoneLightColor ("Stone Light Color", Color) = (0.66, 0.66, 0.61, 1)
        _StoneMidColor ("Stone Mid Color", Color) = (0.45, 0.46, 0.42, 1)
        _StoneDarkColor ("Stone Dark Color", Color) = (0.20, 0.21, 0.19, 1)

        [Header(Stone Texture)]
        _SpriteDetailStrength ("Sprite Detail Strength", Range(0,1)) = 0.55
        _NoiseStrength ("Stone Noise Strength", Range(0,1)) = 0.32
        _NoiseScale ("Stone Noise Scale", Range(1,100)) = 38

        [Header(Cracks)]
        _CrackStrength ("Crack Strength", Range(0,1)) = 0.75
        _CrackScale ("Crack Scale", Range(1,100)) = 24
        _CrackWidth ("Crack Width", Range(0.002,0.08)) = 0.025
        _SmallCrackAmount ("Small Crack Amount", Range(0,1)) = 0.65

        [Header(Edge)]
        _EdgeGlowStrength ("Petrify Edge Highlight", Range(0,0.3)) = 0.08

        [HideInInspector] _GroupBottomY ("Group Bottom Y", Float) = 0
        [HideInInspector] _GroupTopY ("Group Top Y", Float) = 1
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
            #pragma target 2.0

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;

            float _PetrifyAmount;
            float _EdgeSoftness;

            fixed4 _StoneLightColor;
            fixed4 _StoneMidColor;
            fixed4 _StoneDarkColor;

            float _SpriteDetailStrength;
            float _NoiseStrength;
            float _NoiseScale;

            float _CrackStrength;
            float _CrackScale;
            float _CrackWidth;
            float _SmallCrackAmount;

            float _EdgeGlowStrength;

            float _GroupBottomY;
            float _GroupTopY;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                float3 worldPos : TEXCOORD1;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 uv)
            {
                float2 cell = floor(uv);
                float2 local = frac(uv);

                float bottomLeft = Hash21(cell);
                float bottomRight = Hash21(cell + float2(1.0, 0.0));
                float topLeft = Hash21(cell + float2(0.0, 1.0));
                float topRight = Hash21(cell + float2(1.0, 1.0));

                float2 smoothLocal = local * local * (3.0 - 2.0 * local);

                float bottom = lerp(bottomLeft, bottomRight, smoothLocal.x);
                float top = lerp(topLeft, topRight, smoothLocal.x);

                return lerp(bottom, top, smoothLocal.y);
            }

            float FBM(float2 p)
            {
                float v = 0.0;
                float a = 0.5;

                v += ValueNoise(p) * a;
                p = p * 2.03 + 17.13;
                a *= 0.5;

                v += ValueNoise(p) * a;
                p = p * 2.01 + 9.71;
                a *= 0.5;

                v += ValueNoise(p) * a;
                p = p * 2.07 + 31.42;
                a *= 0.5;

                v += ValueNoise(p) * a;

                return v;
            }

            float CrackLayer(float2 p, float width, float softness)
            {
                float n1 = FBM(p);
                float n2 = FBM(p * 1.65 + 23.71);

                float crackLine = abs(n1 - n2);

                float crack = 1.0 - smoothstep(width, width + softness, crackLine);

                float breakMask = smoothstep(0.35, 0.75, FBM(p * 0.45 + 11.2));

                return crack * breakMask;
            }

            float StoneCrackMask(float2 worldUV)
            {
                float2 p = worldUV * _CrackScale;

                float bigCrack = CrackLayer(p, _CrackWidth, 0.035);

                float smallCrack = CrackLayer(p * 2.25 + 41.5, _CrackWidth * 0.65, 0.025);
                smallCrack *= _SmallCrackAmount;

                float hairCrack = CrackLayer(p * 4.2 + 7.8, _CrackWidth * 0.35, 0.018);
                hairCrack *= _SmallCrackAmount * 0.55;

                float finalCrack = saturate(bigCrack + smallCrack + hairCrack);

                return finalCrack * _CrackStrength;
            }

            v2f vert(appdata v)
            {
                v2f o;

                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 src = tex2D(_MainTex, i.uv) * i.color;

                if (src.a <= 0.001)
                {
                    discard;
                }

                float groupHeight = max(0.0001, _GroupTopY - _GroupBottomY);
                float height01 = saturate((i.worldPos.y - _GroupBottomY) / groupHeight);

                float stoneMask = 1.0 - smoothstep
                (
                    _PetrifyAmount - _EdgeSoftness,
                    _PetrifyAmount + _EdgeSoftness,
                    height01
                );

                stoneMask *= step(0.001, _PetrifyAmount);
                stoneMask = max(stoneMask, step(0.999, _PetrifyAmount));

                float2 worldUV = i.worldPos.xy;

                // Chỉ lấy sáng/tối của sprite gốc, không lấy màu gốc.
                float spriteGray = dot(src.rgb, float3(0.299, 0.587, 0.114));

                float stoneShade = lerp(0.55, spriteGray, _SpriteDetailStrength);

                fixed3 stoneColor = lerp(_StoneDarkColor.rgb, _StoneMidColor.rgb, stoneShade);
                stoneColor = lerp(stoneColor, _StoneLightColor.rgb, saturate(stoneShade - 0.45));

                float stoneNoiseA = FBM(worldUV * _NoiseScale);
                float stoneNoiseB = FBM(worldUV * _NoiseScale * 0.35 + 18.4);
                float stoneNoiseC = ValueNoise(worldUV * _NoiseScale * 2.8);

                float finalStoneNoise = stoneNoiseA * 0.55 + stoneNoiseB * 0.3 + stoneNoiseC * 0.15;

                stoneColor += (finalStoneNoise - 0.5) * _NoiseStrength;

                float marble = FBM(worldUV * (_NoiseScale * 0.13) + float2(4.2, 9.1));
                stoneColor = lerp(stoneColor, _StoneLightColor.rgb, marble * 0.12);

                float crackValue = StoneCrackMask(worldUV);

                stoneColor = lerp(stoneColor, _StoneDarkColor.rgb * 0.75, crackValue);

                float crackEdge = StoneCrackMask(worldUV + float2(0.015, 0.015)) * 0.35;
                stoneColor += crackEdge * 0.08;

                float edgeDistance = abs(height01 - _PetrifyAmount);
                float edgeMask = 1.0 - edgeDistance / max(0.001, _EdgeSoftness);
                edgeMask = saturate(edgeMask);

                stoneColor += edgeMask * _EdgeGlowStrength * stoneMask;

                stoneColor = saturate(stoneColor);

                fixed3 finalColor = lerp(src.rgb, stoneColor, stoneMask);

                return fixed4(finalColor, src.a);
            }

            ENDCG
        }
    }

    FallBack "Sprites/Default"
}