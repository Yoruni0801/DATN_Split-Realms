// ============================================================================
// SHADER: HealthBarDelayedEffect
// Mô tả: Shader UI cho thanh máu delayed (vệt máu đỏ phía sau thanh chính).
// Hiệu ứng:
//   - Màu đỏ/cam phát sáng
//   - Tan rã (dissolve) ở viền fill
//   - Nhấp nháy nhẹ khi đang giảm
//   - Phát sáng mềm bên trong
// ============================================================================
Shader "Custom/UI/HealthBarDelayedEffect"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // === MÀU VỆT MÁU ===
        [Header(Mau Vet Mau)]
        _TrailColor     ("Trail Color",     Color) = (0.9, 0.2, 0.05, 0.85)
        _TrailGlowColor ("Trail Glow Color", Color) = (1.0, 0.4, 0.1, 1.0)
        _GlowIntensity  ("Glow Intensity",  Range(0, 3)) = 1.2

        // === TAN RÃ (DISSOLVE) ===
        [Header(Dissolve)]
        _DissolveEdgeWidth ("Edge Width",    Range(0.001, 0.1)) = 0.04
        _DissolveSpeed     ("Flicker Speed", Range(0, 10))      = 3.0
        _DissolveScale     ("Noise Scale",   Range(1, 30))      = 12

        // === NHẤP NHÁY ===
        [Header(Flicker)]
        _FlickerSpeed     ("Speed",     Range(0, 15)) = 6.0
        _FlickerIntensity ("Intensity", Range(0, 0.5)) = 0.15

        // === FILL (đồng bộ từ script) ===
        _FillAmount ("Fill Amount", Range(0,1)) = 1

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
            Name "HealthBarDelayed"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

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

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            fixed4 _TrailColor;
            fixed4 _TrailGlowColor;
            float  _GlowIntensity;

            float  _DissolveEdgeWidth;
            float  _DissolveSpeed;
            float  _DissolveScale;

            float  _FlickerSpeed;
            float  _FlickerIntensity;

            float  _FillAmount;

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

            // Simple hash noise
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
                f = f * f * (3.0 - 2.0 * f);

                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x),
                            lerp(c, d, f.x), f.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half4 texCol = (tex2D(_MainTex, i.texcoord) + _TextureSampleAdd) * i.color;

                // Màu cơ bản: đỏ/cam phát sáng
                fixed4 col = texCol * _TrailColor;

                // === PHÁT SÁNG BÊN TRONG ===
                // Sáng hơn ở giữa (theo chiều dọc)
                float centerV = 1.0 - abs(i.texcoord.y - 0.5) * 2.0;
                col.rgb += centerV * _TrailGlowColor.rgb * _GlowIntensity * 0.3;

                // === TAN RÃ Ở VIỀN ===
                float edgeDist = _FillAmount - i.texcoord.x;
                float2 noiseUV = i.texcoord * _DissolveScale;
                noiseUV.x += _Time.y * _DissolveSpeed;
                float dissolveNoise = valueNoise(noiseUV);
                
                // Vùng gần viền fill → tan rã theo noise
                float dissolveMask = smoothstep(0.0, _DissolveEdgeWidth, edgeDist - dissolveNoise * _DissolveEdgeWidth);
                col.a *= dissolveMask;

                // Viền sáng ở chỗ tan rã
                float edgeGlow = smoothstep(_DissolveEdgeWidth, _DissolveEdgeWidth * 0.3, edgeDist);
                col.rgb += edgeGlow * _TrailGlowColor.rgb * _GlowIntensity;

                // === NHẤP NHÁY NHẸ ===
                float flicker = sin(_Time.y * _FlickerSpeed + i.texcoord.x * 10.0) * 0.5 + 0.5;
                col.rgb += flicker * _FlickerIntensity * _TrailGlowColor.rgb;

                // Giữ alpha gốc (nhân với dissolve)
                col.a *= texCol.a;

                // UI Clipping
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
