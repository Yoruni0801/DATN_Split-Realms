Shader "Bao/DusthornBoneArmorShader"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _WarmEdgeColor ("Warm Edge Color", Color) = (1,0.95,0.82,1)
        _SandTint ("Sand Tint", Color) = (0.84,0.68,0.47,1)
        _ShadowTint ("Shadow Tint", Color) = (0.52,0.39,0.28,1)
        _Charge ("Charge", Range(0,1)) = 0
        _Roll ("Roll", Range(0,1)) = 0
        _SweepPos ("Sweep Position", Range(0,1)) = 0
        _HighlightStrength ("Highlight Strength", Range(0,3)) = 1
        _EdgeGlow ("Edge Glow", Range(0,2)) = 0.5
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
            fixed4 _WarmEdgeColor;
            fixed4 _SandTint;
            fixed4 _ShadowTint;
            float _Charge;
            float _Roll;
            float _SweepPos;
            float _HighlightStrength;
            float _EdgeGlow;

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

                float edge = saturate((abs(aC - aL) + abs(aC - aR) + abs(aC - aU) + abs(aC - aD)) * 1.9);

                // Warm top lighting and stronger shadow below.
                float topLight = saturate(1.12 - IN.uv.y * 0.55);
                fixed3 baseCol = tex.rgb * lerp(_ShadowTint.rgb, _SandTint.rgb, topLight);

                // Moving sweep highlight while rolling / charging.
                float sweep = 1.0 - saturate(abs(IN.uv.x - _SweepPos) / 0.12);
                sweep = pow(sweep, 2.2) * (_Charge * 0.6 + _Roll * 1.0);

                float edgeGlow = edge * _EdgeGlow * (0.35 + _Charge * 0.8 + _Roll * 0.9);
                fixed3 highlight = _WarmEdgeColor.rgb * (edgeGlow + sweep) * _HighlightStrength;

                fixed3 finalCol = baseCol + highlight;

                return fixed4(finalCol, tex.a * IN.color.a);
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
