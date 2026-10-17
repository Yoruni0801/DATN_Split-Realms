Shader "Custom/2D/MonsterMaterialize"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _AppearAmount ("Appear Amount", Range(0, 1)) = 0
        [HDR] _EdgeColor ("Edge Color", Color) = (1.65, 0.08, 0.65, 1)
        [HDR] _GlowColor ("Glow Color", Color) = (0.35, 0.02, 0.40, 1)
        [HDR] _SparkColor ("Spark Color", Color) = (2.8, 1.05, 0.08, 1)
        _NoiseScale ("Noise Scale", Float) = 8.0
        _EdgeWidth ("Edge Width", Range(0.001, 0.3)) = 0.08
        _GlowIntensity ("Glow Intensity", Range(0, 5)) = 1.5
        _Alpha ("Alpha", Range(0, 1)) = 1.0
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
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _AppearAmount;
            float4 _EdgeColor;
            float4 _GlowColor;
            float4 _SparkColor;
            float _NoiseScale;
            float _EdgeWidth;
            float _GlowIntensity;
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
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            float Hash21(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                float2 blend = local * local * (3.0 - 2.0 * local);

                float bottom = lerp(Hash21(cell), Hash21(cell + float2(1.0, 0.0)), blend.x);
                float top = lerp(Hash21(cell + float2(0.0, 1.0)), Hash21(cell + float2(1.0, 1.0)), blend.x);
                return lerp(bottom, top, blend.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 sprite = tex2D(_MainTex, i.uv) * i.color;
                if (sprite.a <= 0.001)
                {
                    return fixed4(0, 0, 0, 0);
                }

                float appearProgress = saturate(_AppearAmount);
                if (appearProgress >= 0.999)
                {
                    return sprite * _Alpha * _GlowIntensity;
                }

                float time = _Time.y * 2.0;
                float2 noiseUv = i.uv * _NoiseScale + float2(0.0, -time * 0.3);
                float noise = ValueNoise(noiseUv);

                float dissolveCoord = i.uv.y * 0.7 + noise * 0.3;
                float threshold = appearProgress * 1.1 - 0.05;

                float distanceToEdge = dissolveCoord - threshold;

                if (distanceToEdge > _EdgeWidth)
                {
                    return fixed4(0, 0, 0, 0);
                }
                else if (distanceToEdge > 0.0)
                {
                    float edgeFactor = 1.0 - (distanceToEdge / max(0.0001, _EdgeWidth));
                    float sparkNoise = Hash21(i.uv * 40.0 + float2(_Time.y * 5.0, _Time.y * 3.0));
                    
                    float3 rimCol = lerp(_GlowColor.rgb, _EdgeColor.rgb, edgeFactor);
                    if (sparkNoise > 0.82)
                    {
                        rimCol += _SparkColor.rgb * 1.8;
                    }

                    float3 col = rimCol * _GlowIntensity;
                    float a = sprite.a * _Alpha * lerp(0.2, 1.0, edgeFactor);
                    return fixed4(col, a);
                }
                else
                {
                    float innerGlow = smoothstep(-_EdgeWidth * 1.5, 0.0, distanceToEdge);
                    float3 darkVioletBody = lerp(sprite.rgb, _GlowColor.rgb * sprite.rgb * 1.2, innerGlow * 0.5);
                    float3 col = lerp(darkVioletBody, darkVioletBody + _EdgeColor.rgb * 0.4 * _GlowIntensity, innerGlow);
                    float a = sprite.a * _Alpha;
                    return fixed4(col, a);
                }
            }
            ENDCG
        }
    }
}
