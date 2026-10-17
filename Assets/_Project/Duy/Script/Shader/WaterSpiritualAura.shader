Shader "Unlit/WaterSpiritualAuraProcedural"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        
        [Header(Aura Settings)]
        _AuraColor ("Aura Color (Mau Hac Khi)", Color) = (0.5, 0, 0.5, 1)
        _AuraIntensity ("Aura Intensity (Cuong do)", Float) = 2.5
        _AuraRadius ("Aura Radius (Kich thuoc)", Float) = 0.05
        _Distortion ("Distortion (Do co gian/Uon luon)", Float) = 1.2
        _NoiseSpeed ("Animation Speed (Toc do chay)", Float) = 2.5
        _NoiseScale ("Noise Scale (Do chi tiet cua khoi)", Float) = 12.0
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
            #pragma target 3.0
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
            float4 _AuraColor;
            float _AuraIntensity;
            float _AuraRadius;
            float _NoiseSpeed;
            float _Distortion;
            float _NoiseScale;

            // Hàm tạo tiếng ồn (Noise) thuật toán tự động, KHÔNG cần Texture ngoài
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash(i + float2(0.0, 0.0)), hash(i + float2(1.0, 0.0)), u.x),
                            lerp(hash(i + float2(0.0, 1.0)), hash(i + float2(1.0, 1.0)), u.x), u.y);
            }

            // Lấy mẫu xung quanh để tạo viền hào quang mờ
            float SampleAuraAlpha(float2 uv, float radius)
            {
                float a = 0;
                float samples = 8;
                for (float i = 0; i < samples; i++)
                {
                    float angle = i * 6.28318 / samples;
                    float2 offset = float2(cos(angle), sin(angle)) * radius;
                    a += tex2D(_MainTex, uv + offset).a;
                }
                return a / samples;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // Gốc ban đầu của Sprite con tinh linh nước
                fixed4 col = tex2D(_MainTex, IN.texcoord);

                float time = _Time.y * _NoiseSpeed;

                // Cuộn UV lên phía trên (trừ time khỏi trục Y) giúp hắc khí luôn chuyển động bay lên
                float2 noiseUV1 = IN.texcoord * _NoiseScale + float2(0.0, -time);
                float2 noiseUV2 = IN.texcoord * (_NoiseScale * 1.5) + float2(time * 0.3, -time * 1.3);

                // Trộn 2 tầng nhiễu thuật toán giúp hắc khí nhìn ngẫu nhiên và mượt mà hơn
                float n1 = noise(noiseUV1);
                float n2 = noise(noiseUV2);
                float finalNoise = (n1 + n2) * 0.5;

                // Biến đổi độ méo dựa trên thời gian
                float2 distortion = float2(finalNoise, noise(noiseUV1 + 4.0)) * 2.0 - 1.0;
                
                // Độ rộng của hào quang co bóp nhẹ theo thời gian (giống nhịp tim/nhịp thở)
                float dynamicRadius = _AuraRadius + sin(time * 2.0) * _AuraRadius * 0.2;
                
                float2 auraUV = IN.texcoord + distortion * _Distortion * 0.04;
                float auraAlpha = SampleAuraAlpha(auraUV, dynamicRadius);

                // Loại bỏ phần đè lên thân tinh linh gốc
                auraAlpha = max(0, auraAlpha - col.a);

                // Dùng chính kết quả Noise để làm hắc khí rách rưới tách biệt từng cụm khói bay lên
                auraAlpha *= finalNoise * 1.5;

                // Tính toán màu sắc cho hắc khí
                fixed4 auraCol = _AuraColor * auraAlpha * _AuraIntensity;

                // Trộn màu cơ thể tinh linh với hắc khí xung quanh
                fixed4 finalCol = (col + auraCol) * IN.color;

                // Thiết lập Alpha tổng hợp
                finalCol.a = col.a + auraAlpha * _AuraIntensity;
                finalCol.a = clamp(finalCol.a, 0.0, 1.0);

                // Khử lỗi rìa đen của Sprite trong Unity (Premultiplied Alpha)
                finalCol.rgb *= finalCol.a;

                return finalCol;
            }
            ENDCG
        }
    }
}