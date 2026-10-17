Shader "Bao/IceCrystalRepulsor"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HDR] _GlowColor ("Idle Glow Color", Color) = (0, 0.8, 1, 1)
        _GlowSpeed ("Idle Glow Speed", Float) = 3.0
        _GlowPower ("Idle Glow Power", Float) = 1.5
        
        [HDR] _HitColor ("Hit Flash Color", Color) = (1, 1, 1, 1)
        _HitDuration ("Hit Flash Duration", Float) = 0.5
        _HitTime ("Hit Time (Set by Script)", Float) = -100.0
        
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
            
            float4 _HitColor;
            float _HitDuration;
            float _HitTime;

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
                
                // Tính khoảng cách từ tâm hình ảnh (UV 0.5, 0.5) ra viền
                float distToCenter = distance(IN.texcoord, float2(0.5, 0.5)) * 2.0;

                // 1. Hiệu ứng thở lan tỏa (Idle Radial Pulse)
                // Trừ đi (distToCenter * 4.0) để tạo các vòng sóng chạy liên tục từ tâm ra ngoài
                float pulse = sin(_Time.y * _GlowSpeed - distToCenter * 4.0) * 0.5 + 0.5; 
                pulse = lerp(0.2, 1.0, pulse); 
                
                // Giữ cho phần lõi (tâm) luôn có một lớp sáng nền rực rỡ
                float coreGlow = 1.0 - saturate(distToCenter * 1.5);
                pulse = max(pulse, coreGlow * 0.8);
                
                float3 idleEmission = _GlowColor.rgb * pulse * _GlowPower;
                
                // 2. Hiệu ứng lóe sáng lan tỏa từ trong lõi ra (Energy Ripple Blast)
                float timeSinceHit = _Time.y - _HitTime;
                float hitIntensity = 0;
                
                if (timeSinceHit > 0 && timeSinceHit < _HitDuration)
                {
                    float progress = timeSinceHit / _HitDuration; // Tăng dần từ 0 đến 1
                    
                    // Tính khoảng cách từ tâm hình ảnh (UV 0.5, 0.5) ra viền
                    // x2 để mép viền ngoài cùng có giá trị khoảng 1.0
                    float distToCenter = distance(IN.texcoord, float2(0.5, 0.5)) * 2.0;
                    
                    // Sóng lan ra ngoài (Radius to dần)
                    float waveRadius = progress * 1.5; 
                    
                    // Tạo viền sóng (Sáng gắt nhất ở ngay waveRadius)
                    float shockwave = 1.0 - saturate(abs(distToCenter - waveRadius) * 4.0);
                    
                    // Lõi bên trong cũng sáng bung ra theo tiến trình
                    float coreBlast = 1.0 - saturate(distToCenter / (progress + 0.001));
                    
                    // Gom cả 2 lại và mờ dần theo thời gian để tắt hẳn
                    float fade = 1.0 - progress;
                    hitIntensity = (shockwave + coreBlast * 0.5) * fade * fade; 
                }
                
                float3 hitEmission = _HitColor.rgb * hitIntensity * 10.0; // Nhân 10 để chói lòa
                
                // 3. Trộn cả 2 loại phát sáng vào Sprite
                float3 finalEmission = idleEmission + hitEmission;
                c.rgb = c.rgb * c.a + finalEmission * c.a;
                
                return c;
            }
            ENDCG
        }
    }
}
