Shader "Bao/IceDoorShatter"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        [Header(Hieu Ung Nut Vo)]
        _DissolveAmount ("Shatter Progress", Range(0, 1.1)) = 0.0
        _ShatterScale ("Shatter Detail", Float) = 20.0
        
        [Header(Hieu Ung Vien Phep Thuat)]
        [HDR] _EdgeColor ("Glow Color", Color) = (0, 0.8, 1, 1)
        _EdgeWidth ("Glow Width", Float) = 0.05
        
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
            float _DissolveAmount;
            float4 _EdgeColor;
            float _EdgeWidth;
            float _ShatterScale;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            sampler2D _MainTex;

            // Hàm Random
            float2 random2( float2 p ) {
                return frac(sin(float2(dot(p,float2(127.1,311.7)),dot(p,float2(269.5,183.3))))*43758.5453);
            }

            // Hàm tạo vân nứt tinh th băng (Voronoi Cellular Noise)
            float voronoi(float2 x) {
                float2 n = floor(x);
                float2 f = frac(x);
                float m = 8.0;
                for( int j=-1; j<=1; j++ )
                for( int i=-1; i<=1; i++ ) {
                    float2 g = float2(float(i),float(j));
                    float2 o = random2( n + g );
                    float d = distance(f, g + o);
                    m = min(m, d);
                }
                return m;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 c = tex2D (_MainTex, IN.texcoord) * IN.color;
                
                // KHẮC PHỤC LỖI NHÒE VIỀN (Premultiplied Alpha):
                // Unity 2D dùng Blend One OneMinusSrcAlpha nên phải nhân RGB với Alpha trước khi in ra
                if (_DissolveAmount <= 0.001) 
                {
                    c.rgb *= c.a;
                    return c;
                }

                // Bỏ qua các viền trong suốt
                if (c.a < 0.05) return c;

                // 1. Tự động sinh bản đồ mảnh vỡ (Cracks Map)
                // Kéo dãn theo trục Y để vết nứt trông dọc dọc giống cánh cửa
                float2 uv = IN.texcoord * float2(1.0, 2.0) * _ShatterScale;
                float cracks = voronoi(uv);
                
                // Đảo ngược: Tâm các mảnh vỡ sẽ là 1 (trắng), rãnh nứt là 0 (đen)
                cracks = 1.0 - cracks; 

                // 2. Logic Tan vỡ (Dissolve)
                // Khi viên đá chạm vào, _DissolveAmount tăng dần
                // Những chỗ có giá trị cracks thấp (rãnh nứt) sẽ bị âm trước -> nứt từ ngoài vào giữa mảnh
                float dissolveFactor = cracks - _DissolveAmount;
                
                // 3. Viền sáng rực tại các mép nứt
                float edgeGlow = smoothstep(0.0, _EdgeWidth, dissolveFactor);
                edgeGlow = 1.0 - edgeGlow; // Phần sắp biến mất sẽ sáng gắt lên
                
                float3 emission = _EdgeColor.rgb * edgeGlow * 4.0; // X4 để chói lòa
                
                // 4. Nếu pixel đã vỡ nát hoàn toàn -> Xóa
                if (dissolveFactor < 0.0)
                {
                    c.a = 0.0;
                }
                else
                {
                    // Cộng màu phát sáng vào mảnh băng đang nứt
                    c.rgb = c.rgb * c.a + emission * c.a;
                }
                
                return c;
            }
            ENDCG
        }
    }
}
