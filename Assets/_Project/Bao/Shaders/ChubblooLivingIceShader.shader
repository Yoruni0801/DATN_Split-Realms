Shader "Bao/ChubblooLivingIceShader"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _IceDepthColor ("Ice Depth", Color) = (0.05,0.16,0.42,1)
        _GlacierColor ("Glacier Color", Color) = (0.12,0.74,1,1)
        _FrostColor ("Frost Color", Color) = (0.43,0.97,1,1)
        _SnowColor ("Snow Color", Color) = (0.93,1,1,1)
        _Charge ("Charge", Range(0,1)) = 0
        _AttackLevel ("Attack Level", Range(0,2)) = 0
        _ShotFlash ("Shot Flash", Range(0,1)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
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
            #pragma target 2.0
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color  : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _IceDepthColor;
            fixed4 _GlacierColor;
            fixed4 _FrostColor;
            fixed4 _SnowColor;
            float _Charge;
            float _AttackLevel;
            float _ShotFlash;

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float a = hash21(i);
                float b = hash21(i + float2(1,0));
                float c = hash21(i + float2(0,1));
                float d = hash21(i + float2(1,1));

                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(a, b, u.x) +
                       (c - a) * u.y * (1.0 - u.x) +
                       (d - b) * u.x * u.y;
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.uv = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, IN.uv);
                if (tex.a <= 0.001) discard;

                float2 px = _MainTex_TexelSize.xy;
                float aC = tex.a;
                float aL = tex2D(_MainTex, IN.uv + float2(-px.x, 0)).a;
                float aR = tex2D(_MainTex, IN.uv + float2(px.x, 0)).a;
                float aU = tex2D(_MainTex, IN.uv + float2(0, px.y)).a;
                float aD = tex2D(_MainTex, IN.uv + float2(0, -px.y)).a;

                float edge = saturate((abs(aC - aL) + abs(aC - aR) + abs(aC - aU) + abs(aC - aD)) * 1.8);

                float gradient = saturate(1.0 - IN.uv.y * 0.55 + 0.12);
                float innerNoise = noise(IN.uv * 6.0 + float2(_Time.y * 0.45, -_Time.y * 0.35));

                fixed3 sourceColor = tex.rgb * IN.color.rgb;
                float sourceLight = dot(sourceColor, float3(0.2126, 0.7152, 0.0722));
                fixed3 iceTint = lerp(_IceDepthColor.rgb, _GlacierColor.rgb, gradient);
                float iceWash = 0.10 + gradient * 0.12 + innerNoise * 0.05;
                fixed3 baseCol = lerp(sourceColor, sourceColor * 0.72 + iceTint * 0.28, iceWash);
                baseCol = lerp(baseCol, _FrostColor.rgb, innerNoise * 0.08 + _AttackLevel * 0.06);

                float chargeBand = smoothstep(0.0, 1.0, saturate(IN.uv.y + _Charge * 0.65 - 0.15));
                float rim = edge * (0.08 + _AttackLevel * 0.18 + _Charge * 0.22);
                float coldGlow = (0.025 + innerNoise * 0.055 + chargeBand * _Charge * 0.30) * (0.35 + sourceLight * 0.65);
                float flash = _ShotFlash * (0.25 + edge * 0.45);

                fixed3 highlight = _SnowColor.rgb * flash;
                fixed3 frost = _FrostColor.rgb * (rim + coldGlow);
                fixed3 finalCol = baseCol + frost + highlight;

                return fixed4(finalCol, tex.a * IN.color.a);
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
