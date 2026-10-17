Shader "Bao/ChubblooProjectileIceShader"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _IceDepthColor ("Ice Depth", Color) = (0.24,0.08,0.58,1)
        _GlacierColor ("Glacier Color", Color) = (0.85,0.40,1.0,1)
        _FrostColor ("Frost Color", Color) = (1.25,0.78,0.08,1)
        _SnowColor ("Snow Color", Color) = (1.0,0.95,0.85,1)
        _Pulse ("Pulse", Range(0,1)) = 0
        _Speed01 ("Speed", Range(0,1)) = 1
        _Direction ("Direction", Vector) = (1,0,0,0)
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
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            fixed4 _IceDepthColor;
            fixed4 _GlacierColor;
            fixed4 _FrostColor;
            fixed4 _SnowColor;
            float _Pulse;
            float _Speed01;
            float4 _Direction;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.uv = IN.texcoord;
                OUT.color = IN.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.uv * 2.0 - 1.0;
                float stretch = lerp(1.0, 1.28, _Speed01);
                uv.x /= stretch;

                float dist = length(uv);
                float body = saturate(1.0 - dist * 1.05);
                body = pow(body, 1.25);

                float rim = saturate(1.0 - abs(dist - 0.72) / 0.20);
                float core = saturate(1.0 - dist * 2.4);
                float pulse = lerp(0.92, 1.14, _Pulse);

                fixed3 col = lerp(_IceDepthColor.rgb, _GlacierColor.rgb, body);
                col = lerp(col, _FrostColor.rgb, rim * 0.62);
                col += _SnowColor.rgb * core * pulse * 0.85;

                float alpha = saturate(body + rim * 0.42);
                return fixed4(col * alpha, alpha);
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
