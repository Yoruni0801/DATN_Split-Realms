// ============================================================================
// SHADER: HealthBarEffect
// Mô tả: Shader UI cho thanh máu chính với hiệu ứng:
//   - Dòng năng lượng chảy bên trong (Energy Flow)
//   - Phát sáng ở rìa fill (Edge Glow)
//   - Flash trắng/đỏ khi nhận sát thương (Damage Flash)
//   - Nhấp nháy cảnh báo khi máu thấp (Low Health Pulse)
//   - Tia sáng lướt qua thanh (Shimmer)
//   - Chiều sâu 3D giả (Vertical Depth)
// Tất cả thông số đều điều chỉnh được trong Inspector.
// ============================================================================
Shader "Custom/UI/HealthBarEffect"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // === FILL (đồng bộ từ script) ===
        _FillAmount ("Fill Amount", Range(0,1)) = 1

        // === MÀU THANH MÁU THEO % ===
        [Header(Mau Thanh Mau)]
        _HealthColorFull ("Full HP Color", Color) = (0.05, 0.9, 0.3, 1)
        _HealthColorMid  ("Mid HP Color",  Color) = (1.0, 0.78, 0.0, 1)
        _HealthColorLow  ("Low HP Color",  Color) = (0.9, 0.15, 0.1, 1)
        _HealthColorCrit ("Critical Color", Color) = (0.55, 0.0, 0.05, 1)

        // === VIỀN PHÁT SÁNG (EDGE GLOW) ===
        [Header(Edge Glow)]
        _EdgeGlowWidth     ("Width",     Range(0.002, 0.15)) = 0.035
        _EdgeGlowIntensity ("Intensity", Range(0, 8))        = 3.5
        _EdgeGlowColor     ("Color",     Color)              = (1, 1, 0.85, 1)

        // === DÒNG NĂNG LƯỢNG (ENERGY FLOW) ===
        [Header(Energy Flow)]
        _FlowSpeed     ("Speed",     Range(0, 10)) = 2.5
        _FlowIntensity ("Intensity", Range(0, 0.8)) = 0.25
        _FlowScale     ("Scale",     Range(1, 30))  = 10

        // === FLASH KHI BỊ ĐÁNH ===
        [Header(Damage Flash)]
        _FlashAmount ("Flash Amount", Range(0, 1))  = 0
        _FlashColor  ("Flash Color",  Color)        = (1, 0.35, 0.15, 1)

        // === NHẤP NHÁY MÁU THẤP ===
        [Header(Low Health Pulse)]
        _PulseSpeed     ("Speed",           Range(0, 12)) = 5
        _PulseIntensity ("Min Brightness",  Range(0, 1))  = 0.35
        _PulseThreshold ("Start Threshold", Range(0, 1))  = 0.3

        // === TIA SÁNG LƯỚT QUA (SHIMMER) ===
        [Header(Shimmer)]
        _ShimmerSpeed     ("Speed",     Range(0, 5))    = 1.2
        _ShimmerWidth     ("Width",     Range(0.02, 0.3)) = 0.08
        _ShimmerIntensity ("Intensity", Range(0, 1))    = 0.35

        // === CHIỀU SÂU 3D (VERTICAL DEPTH) ===
        [Header(Vertical Depth)]
        _DepthHighlight ("Top Highlight",    Range(0, 0.6)) = 0.25
        _DepthShadow    ("Bottom Shadow",    Range(0, 0.5)) = 0.15

        // === SCANLINE (TÙY CHỌN) ===
        [Header(Scanline Optional)]
        _ScanlineIntensity ("Intensity", Range(0, 0.5))  = 0.08
        _ScanlineCount     ("Count",     Range(5, 100))  = 30

        // === UNITY UI MASKING ===
        [Header(UI Stencil)]
        _StencilComp      ("Stencil Comparison", Float) = 8
        _Stencil          ("Stencil ID",         Float) = 0
        _StencilOp        ("Stencil Operation",  Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask  ("Stencil Read Mask",  Float) = 255
        _ColorMask        ("Color Mask",         Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType"      = "Transparent"
            "PreviewType"     = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref       [_Stencil]
            Comp      [_StencilComp]
            Pass      [_StencilOp]
            ReadMask  [_StencilReadMask]
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
            Name "HealthBarMain"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            // ---- Structs ----
            struct appdata
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ---- Uniforms ----
            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            float  _FillAmount;

            fixed4 _HealthColorFull;
            fixed4 _HealthColorMid;
            fixed4 _HealthColorLow;
            fixed4 _HealthColorCrit;

            float  _EdgeGlowWidth;
            float  _EdgeGlowIntensity;
            fixed4 _EdgeGlowColor;

            float  _FlowSpeed;
            float  _FlowIntensity;
            float  _FlowScale;

            float  _FlashAmount;
            fixed4 _FlashColor;

            float  _PulseSpeed;
            float  _PulseIntensity;
            float  _PulseThreshold;

            float  _ShimmerSpeed;
            float  _ShimmerWidth;
            float  _ShimmerIntensity;

            float  _DepthHighlight;
            float  _DepthShadow;

            float  _ScanlineIntensity;
            float  _ScanlineCount;

            // ---- Vertex ----
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex   = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color    = v.color * _Color;
                return o;
            }

            // ==============================
            // NOISE: Fractional Brownian Motion
            // ==============================
            float hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float valueNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                f = f * f * (3.0 - 2.0 * f); // smooth-step

                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x),
                            lerp(c, d, f.x), f.y);
            }

            float fbm(float2 uv)
            {
                float val = 0.0;
                float amp = 0.5;
                for (int j = 0; j < 3; j++)
                {
                    val += amp * valueNoise(uv);
                    uv  *= 2.01;
                    amp *= 0.5;
                }
                return val;
            }

            // ==============================
            // MÀU THEO % MÁU
            // ==============================
            fixed4 getHealthColor(float fill)
            {
                if (fill > 0.5)
                    return lerp(_HealthColorMid, _HealthColorFull, saturate((fill - 0.5) * 2.0));
                else if (fill > 0.2)
                    return lerp(_HealthColorLow, _HealthColorMid, saturate((fill - 0.2) / 0.3));
                else
                    return lerp(_HealthColorCrit, _HealthColorLow, saturate(fill / 0.2));
            }

            // ---- Fragment ----
            fixed4 frag(v2f i) : SV_Target
            {
                // 1. Lấy texture gốc
                half4 texCol = (tex2D(_MainTex, i.texcoord) + _TextureSampleAdd) * i.color;

                // 2. Tính màu thanh máu theo %
                fixed4 hpCol = getHealthColor(_FillAmount);
                fixed4 col   = texCol * hpCol;

                // ======= ENERGY FLOW =======
                float2 flowUV = i.texcoord * _FlowScale;
                flowUV.x -= _Time.y * _FlowSpeed;
                float noise = fbm(flowUV);
                col.rgb += noise * _FlowIntensity * hpCol.rgb;

                // ======= VERTICAL DEPTH (3D giả) =======
                // Phần trên sáng hơn, phần dưới tối hơn
                float vGrad = i.texcoord.y;
                float highlight = smoothstep(0.5, 1.0, vGrad) * _DepthHighlight;
                float shadow    = smoothstep(0.5, 0.0, vGrad) * _DepthShadow;
                col.rgb += highlight;
                col.rgb -= shadow;

                // ======= SCANLINE =======
                float scanline = sin(i.texcoord.y * _ScanlineCount * 3.14159) * 0.5 + 0.5;
                col.rgb -= scanline * _ScanlineIntensity;

                // ======= EDGE GLOW =======
                // Sửa cho Slider: mép phải luôn là UV x = 1.0
                float edgeDist = 1.0 - i.texcoord.x;
                float edgeGlow = 1.0 - smoothstep(0.0, _EdgeGlowWidth, edgeDist);
                // Ẩn edge glow khi máu đầy (không cần viền sáng lúc full)
                float edgeVis  = 1.0 - step(0.98, _FillAmount);
                col.rgb += edgeGlow * edgeVis * _EdgeGlowIntensity * _EdgeGlowColor.rgb;

                // ======= SHIMMER (tia sáng lướt qua) =======
                float shimmerPos  = frac(_Time.y * _ShimmerSpeed);
                // Sửa cho Slider: tia lướt toàn bộ không gian UV hiện tại (vì Slider đã thu nhỏ lại theo máu)
                float shimmerX    = shimmerPos;
                float shimmerDist = abs(i.texcoord.x - shimmerX);
                float shimmer     = (1.0 - smoothstep(0.0, _ShimmerWidth, shimmerDist)) * _ShimmerIntensity;
                // Làm cho shimmer có gradient theo chiều dọc (sáng hơn ở giữa)
                float shimmerV    = 1.0 - abs(i.texcoord.y - 0.5) * 2.0;
                col.rgb += shimmer * shimmerV * float3(1, 1, 1);

                // ======= LOW HEALTH PULSE =======
                float pulseMask = step(_FillAmount, _PulseThreshold) * step(0.001, _FillAmount);
                if (pulseMask > 0.5)
                {
                    float pulseStrength = 1.0 - (_FillAmount / _PulseThreshold);
                    float pulse = lerp(_PulseIntensity, 1.0, sin(_Time.y * _PulseSpeed) * 0.5 + 0.5);
                    col.rgb *= lerp(1.0, pulse, pulseStrength);
                }

                // ======= DAMAGE FLASH =======
                col.rgb = lerp(col.rgb, _FlashColor.rgb, _FlashAmount);

                // Giữ nguyên alpha gốc
                col.a = texCol.a;

                // ======= UI CLIPPING =======
                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
