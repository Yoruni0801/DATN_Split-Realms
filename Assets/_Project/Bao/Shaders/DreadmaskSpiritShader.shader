Shader "Bao/DreadmaskSpiritShader"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _GhostGlow ("Ghostly Aura Glow", Color) = (0.2,0.8,0.7,1)
        _SpiritIntensity ("Spirit Intensity", Range(0, 10)) = 0.0
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
            fixed4 _GhostGlow;
            float _SpiritIntensity;

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
                // Spirit/Ghostly warp waves
                float2 uv = IN.texcoord;
                uv.x += sin(uv.y * 10.0 + _Time.y * 4.0) * 0.02 * saturate(_SpiritIntensity * 0.2);
                uv.y += cos(uv.x * 10.0 + _Time.y * 3.0) * 0.015 * saturate(_SpiritIntensity * 0.2);

                fixed4 c = tex2D(_MainTex, uv) * IN.color;
                if (c.a < 0.01) discard;

                // Ethereal transparency when spirit level is high
                c.a = lerp(c.a, c.a * 0.6, saturate(_SpiritIntensity * 0.25));

                // Pulsing ghost aura glow
                float pulse = sin(_Time.z * 1.5) * 0.25 + 0.75;
                float edge = smoothstep(0.4, 0.0, abs(c.a - 0.5));
                c.rgb += _GhostGlow.rgb * edge * _SpiritIntensity * 0.5 * pulse;

                c.rgb *= c.a;
                return c;
            }
        ENDCG
        }
    }
}
