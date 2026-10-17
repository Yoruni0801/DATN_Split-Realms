Shader "Bao/IceCrystalGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HDR] _GlowColor ("Glow Color", Color) = (0, 0.8, 1, 1)
        _GlowSpeed ("Glow Speed", Float) = 3.0
        _GlowPower ("Glow Power", Float) = 2.0
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
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
            #pragma multi_compile _ PIXELSNAP_ON
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
                float2 texcoord  : TEXCOORD0;
            };

            fixed4 _Color;
            float4 _GlowColor;
            float _GlowSpeed;
            float _GlowPower;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap (OUT.vertex);
                #endif

                return OUT;
            }

            sampler2D _MainTex;

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 c = tex2D (_MainTex, IN.texcoord) * IN.color;
                
                // Thuật toán tạo nhịp thở (Pulse)
                // Dùng hàm sin theo thời gian, map lại để không bao giờ tối đen hoàn toàn (luôn sáng từ 0.3 đến 1.0)
                float pulse = (sin(_Time.y * _GlowSpeed) * 0.5 + 0.5); 
                pulse = lerp(0.3, 1.0, pulse); 
                
                // Tính toán màu phát sáng
                float3 emission = _GlowColor.rgb * pulse * _GlowPower;
                
                // Trộn màu phát sáng vào Sprite (với pre-multiply alpha chuẩn 2D)
                c.rgb = c.rgb * c.a + emission * c.a;
                
                return c;
            }
            ENDCG
        }
    }
}
