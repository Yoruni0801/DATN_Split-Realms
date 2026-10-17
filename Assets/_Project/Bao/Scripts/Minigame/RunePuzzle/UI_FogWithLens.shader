Shader "Custom/UI_FogWithLens"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        _FogColor ("Fog Base Color", Color) = (0.05, 0.1, 0.15, 0.95)
        _FogHighlight ("Fog Highlight", Color) = (0.2, 0.4, 0.5, 1.0)
        _FogSpeed ("Fog Speed", Float) = 0.15
        _FogScale ("Fog Scale (Noise Density)", Float) = 3.0
        
        [Header(Fog Controls)]
        [Range(0, 1)] _FogOpacity ("Fog Opacity", Float) = 1.3
        [Range(0, 0.5)] _EdgeFadeFactor ("Edge Fade Amount", Float) = 0.15
        [Header(Panel Shape)]
        [Range(0, 0.5)] _CornerRadius ("Rounded Corner Radius", Float) = 0.22
        [Range(0, 0.05)] _CornerSoftness ("Rounded Corner Softness", Float) = 0.008
        
        _LensWorldPos ("Lens World Position", Vector) = (0,0,0,0)
        _LensRadius ("Lens Radius", Float) = 100.0
        _LensSoftness ("Lens Softness", Float) = 20.0
        
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
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

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

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
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            
            fixed4 _FogColor;
            fixed4 _FogHighlight;
            float _FogSpeed;
            float _FogScale;
            float _FogOpacity;
            float _EdgeFadeFactor;
            float _CornerRadius;
            float _CornerSoftness;
            
            float4 _LensWorldPos;
            float _LensRadius;
            float _LensSoftness;

            // --- HÀM TẠO NOISE SƯƠNG MÙ ---
            float hash(float2 p) {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453123);
            }
            float noise(float2 p) {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }
            float fbm(float2 p) {
                float v = 0.0;
                float a = 0.5;
                for (int i = 0; i < 4; i++) {
                    v += a * noise(p);
                    p = p * 2.0;
                    a *= 0.5;
                }
                return v;
            }

            // Signed-distance mask for a rounded UI rectangle. The calculation uses the
            // rect's aspect ratio, so the four corners stay circular on a wide panel.
            float roundedRectMask(float2 uv, float radius, float softness)
            {
                // Derive this particular Image's aspect ratio from its interpolated UVs;
                // using screen aspect would stretch the corner radius when the panel is wide.
                float aspect = fwidth(uv.y) / max(fwidth(uv.x), 0.000001);
                float2 shapeCoord = (uv - 0.5) * float2(aspect, 1.0);
                float2 bounds = float2(aspect, 1.0) * 0.5 - radius;
                float distanceToShape = length(max(abs(shapeCoord) - bounds, 0.0)) - radius;
                return 1.0 - smoothstep(0.0, max(softness, 0.0001), distanceToShape);
            }

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                
                // Lấy Screen Position thực tế
                OUT.worldPosition = ComputeScreenPos(OUT.vertex);

                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // Texture gốc (thường là sprite trắng)
                half4 baseColor = tex2D(_MainTex, IN.texcoord) * IN.color;
                
                // --- TÍNH TOÁN HIỆU ỨNG SƯƠNG MÙ ---
                float2 uv = IN.texcoord * _FogScale;
                uv.x += _Time.y * _FogSpeed * 0.8;
                uv.y += _Time.y * _FogSpeed * 0.5;
                
                // Thêm độ cuộn mây xoắn (swirl)
                float n = fbm(uv + fbm(uv + _Time.y * _FogSpeed));
                
                // Trộn màu sương mù mờ ảo
                half4 fogMixed = lerp(_FogColor, _FogHighlight, n);
                // Người dùng tùy chỉnh độ mờ chung của sương
                float fogAlpha = lerp(_FogOpacity * 0.8, _FogOpacity, n);
                
                // --- LÀM MỜ BIÊN (EDGE FADE) ---
                float edgeX = min(IN.texcoord.x, 1.0 - IN.texcoord.x);
                float edgeY = min(IN.texcoord.y, 1.0 - IN.texcoord.y);
                
                float fadeOut = 1.0;
                if (_EdgeFadeFactor > 0.0)
                {
                    fadeOut = smoothstep(0.0, _EdgeFadeFactor, edgeX) * smoothstep(0.0, _EdgeFadeFactor, edgeY);
                }
                fogAlpha *= fadeOut;

                // Match the fog silhouette to the rounded opening of the panel.
                fogAlpha *= roundedRectMask(IN.texcoord, _CornerRadius, _CornerSoftness);

                // Áp dụng màu sương vào nền. MÀU được nhân với Sprite. NHƯNG ALPHA chỉ lấy từ CanvasGroup (IN.color.a)
                half4 finalColor;
                finalColor.rgb = baseColor.rgb * fogMixed.rgb;
                
                // ÉP ALPHA CHẮC CHẮN ĐỤC, KHÔNG BỊ TRONG SUỐT BỞI SPRITE
                finalColor.a = fogAlpha * IN.color.a;
                
                // Bóp (clamp) alpha lại tối đa là 1
                finalColor.a = saturate(finalColor.a);
                
                // --- KÍNH LÚP (MASK) ---
                float2 screenPixelPos = (IN.worldPosition.xy / IN.worldPosition.w) * _ScreenParams.xy;
                float dist = distance(screenPixelPos, _LensWorldPos.xy);
                
                // Tạo hiệu ứng lủng lỗ (mask)
                float mask = smoothstep(_LensRadius, _LensRadius + _LensSoftness, dist);
                
                finalColor.a *= mask;
                return finalColor;
            }
            ENDCG
        }
    }
}
