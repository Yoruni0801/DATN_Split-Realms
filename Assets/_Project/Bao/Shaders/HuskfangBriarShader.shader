Shader "Bao/HuskfangBriarShader"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _SapColor ("Poison Sap Glow", Color) = (0.4,1,0,1)
        _VenomLevel ("Venom Intensity", Range(0, 10)) = 0.0
        _VeinScale ("Briar Vein Scale", Range(4, 28)) = 15.0
        _PulseSpeed ("Sap Pulse Speed", Range(0, 12)) = 4.5
        _OutlineWidth ("Toxic Rim Width", Range(0.001, 0.03)) = 0.009
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
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _SapColor;
            float _VenomLevel;
            float _VeinScale;
            float _PulseSpeed;
            float _OutlineWidth;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                fixed4 c = tex2D(_MainTex, uv) * IN.color;
                if (c.a < 0.01) discard;

                float attack = saturate((_VenomLevel - 0.65) / 6.0);
                float time = _Time.y * _PulseSpeed;

                // Layered, irregular veins: the slow vertical pulse makes the sap appear to
                // climb through the shell and thorns instead of looking like a flat overlay.
                float coarse = sin(uv.y * _VeinScale - time + sin(uv.x * 9.0 + time * 0.45) * 1.45);
                float fine = sin(uv.x * 20.0 + uv.y * 8.0 + time * 1.35);
                float veinMask = smoothstep(0.68, 0.93, coarse * 0.72 + fine * 0.28);
                float travellingPulse = smoothstep(0.72, 0.98, sin(uv.y * 8.0 - time * 1.8) * 0.5 + 0.5);

                // A restrained wet sheen preserves the original hand-painted greens and reds.
                float sheen = pow(saturate(1.0 - abs(uv.x - 0.32) * 2.4), 5.0) * (0.03 + attack * 0.08);
                c.rgb += _SapColor.rgb * (veinMask * (0.12 + attack * 0.58) + travellingPulse * attack * 0.18);
                c.rgb += _SapColor.rgb * sheen;

                // Sample neighbouring alpha to form a slim toxic rim inside the existing sprite
                // silhouette. This avoids colouring the transparent background.
                float neighborAlpha = max(
                    max(tex2D(_MainTex, uv + float2(_OutlineWidth, 0)).a, tex2D(_MainTex, uv - float2(_OutlineWidth, 0)).a),
                    max(tex2D(_MainTex, uv + float2(0, _OutlineWidth)).a, tex2D(_MainTex, uv - float2(0, _OutlineWidth)).a));
                float rim = saturate(neighborAlpha - c.a) * c.a;
                c.rgb += _SapColor.rgb * rim * (0.35 + attack * 0.95);

                c.rgb *= c.a;
                return c;
            }
        ENDCG
        }
    }
}
