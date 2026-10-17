Shader "Bao/IceSigilPulseUI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _GlowColor ("Glow Color", Color) = (0.36, 1, 1, 1)
        _GlowStrength ("Glow Strength", Range(0, 2)) = 0.75
        _Pulse ("Pulse", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Tint;
            fixed4 _GlowColor;
            float _GlowStrength;
            float _Pulse;

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, input.texcoord) * _Tint * input.color;
                float2 centeredUv = input.texcoord * 2.0 - 1.0;
                float radial = saturate(1.0 - length(centeredUv));
                float glow = (0.22 + _Pulse * 0.55) * _GlowStrength;
                tex.rgb += _GlowColor.rgb * radial * glow * tex.a;
                return tex;
            }
            ENDCG
        }
    }
}
