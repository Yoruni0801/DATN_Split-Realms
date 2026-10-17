Shader "Bao/MiniGame/PortalEmberGlowParticle"
{
    Properties
    {
        _MainTex ("Particle Texture", 2D) = "white" {}
        [HDR] _Color ("Tint / HDR Boost", Color) = (1.5, 1.5, 1.5, 1.0)
        _CoreGlow ("Core Glow Multiplier", Range(1.0, 5.0)) = 2.2
        _OuterSoftness ("Outer Softness Falloff", Range(0.5, 4.0)) = 1.8
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent+50"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="False"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha One // Additive Blending cho đốm sáng ma thuật rực rỡ

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
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
            float _CoreGlow;
            float _OuterSoftness;

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
                // Tính khoảng cách từ tâm UV (0.5, 0.5)
                float2 centeredUV = IN.texcoord - float2(0.5, 0.5);
                float dist = length(centeredUV) * 2.0; // 0 ở tâm, 1 ở mép viền

                // Nếu có gán texture (như đốm sao/glow), lấy mẫu texture
                fixed4 tex = tex2D(_MainTex, IN.texcoord);

                // Tạo hình đốm tròn phát quang mờ viền theo công thức Gaussian/Smoothstep
                float radialShape = saturate(1.0 - dist);
                float smoothShape = pow(radialShape, _OuterSoftness);

                // Tâm sáng chói (Hot Core)
                float hotCore = pow(radialShape, 4.0) * _CoreGlow;

                // Kết hợp texture và hình dạng hạt mờ mềm mịn
                float alpha = (tex.a * smoothShape + hotCore * 0.5) * IN.color.a;

                // Màu sắc HDR cộng hưởng hạt
                fixed3 finalColor = IN.color.rgb * (1.0 + hotCore);

                return fixed4(finalColor, saturate(alpha));
            }
            ENDCG
        }
    }
}
