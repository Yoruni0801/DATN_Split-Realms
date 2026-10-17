Shader "Custom/2D/Petrify Bottom Up Group"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Petrify)]
        _PetrifyAmount ("Petrify Amount", Range(0,1)) = 0
        _EdgeSoftness ("Edge Softness", Range(0.001,0.3)) = 0.08

        [Header(Stone)]
        _StoneColor ("Stone Color", Color) = (0.58, 0.58, 0.54, 1)
        _StoneDarkColor ("Stone Dark Color", Color) = (0.25, 0.26, 0.24, 1)

        _NoiseStrength ("Noise Strength", Range(0,1)) = 0.28
        _NoiseScale ("Noise Scale", Range(1,100)) = 35

        _CrackStrength ("Crack Strength", Range(0,1)) = 0.55
        _CrackScale ("Crack Scale", Range(1,100)) = 22

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

            fixed4 _StoneColor;
            fixed4 _StoneDarkColor;

            float _NoiseStrength;
            float _NoiseScale;

            float _CrackStrength;
            float _CrackScale;

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

            float StoneCrackMask(float2 uv)
            {
                float noiseA = ValueNoise(uv);
                float noiseB = ValueNoise(uv * 1.7 + 12.45);

                float crackLineValue = abs(noiseA - noiseB);

                float crackMask = 1.0 - smoothstep(0.02, 0.08, crackLineValue);

                return crackMask;
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

                // Mask hóa đá từ dưới lên.
                // height01 thấp hơn _PetrifyAmount thì bị hóa đá.
                float stoneMask = 1.0 - smoothstep
                (
                    _PetrifyAmount - _EdgeSoftness,
                    _PetrifyAmount + _EdgeSoftness,
                    height01
                );

                // Khi PetrifyAmount = 0 thì không hóa đá.
                stoneMask *= step(0.001, _PetrifyAmount);

                // Khi PetrifyAmount = 1 thì hóa đá toàn bộ.
                stoneMask = max(stoneMask, step(0.999, _PetrifyAmount));

                float2 worldUV = i.worldPos.xy;

                float gray = dot(src.rgb, float3(0.299, 0.587, 0.114));

                float stoneNoiseA = ValueNoise(worldUV * _NoiseScale);
                float stoneNoiseB = ValueNoise(worldUV * _NoiseScale * 0.35);
                float finalStoneNoise = stoneNoiseA * 0.7 + stoneNoiseB * 0.3;

                fixed3 stoneColor = lerp(_StoneDarkColor.rgb, _StoneColor.rgb, gray);

                stoneColor += (finalStoneNoise - 0.5) * _NoiseStrength;

                float crackValue = StoneCrackMask(worldUV * _CrackScale);
                crackValue *= _CrackStrength;

                stoneColor = lerp(stoneColor, _StoneDarkColor.rgb, crackValue);

                // Mép lan hóa đá hơi sáng nhẹ.
                float edgeDistance = abs(height01 - _PetrifyAmount);
                float edgeMask = 1.0 - edgeDistance / max(0.001, _EdgeSoftness);
                edgeMask = saturate(edgeMask);

                stoneColor += edgeMask * 0.08 * stoneMask;

                fixed3 finalColor = lerp(src.rgb, stoneColor, stoneMask);

                return fixed4(finalColor, src.a);
            }

            ENDCG
        }
    }

    FallBack "Sprites/Default"
}