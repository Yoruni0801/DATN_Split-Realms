Shader "Bao/UI/RedSlimeHealthBar"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HDR] _PrimaryColor ("Primary Gel", Color) = (1.0,0.05,0.03,1)
        [HDR] _SecondaryColor ("Secondary Gel", Color) = (1.35,0.12,0.03,1)
        [HDR] _AccentColor ("Membrane Accent", Color) = (1.55,0.62,0.22,1)
        _DeepColor ("Deep Gel", Color) = (0.11,0.005,0.003,1)
        _FillAmount ("Fill Amount", Range(0,1)) = 1
        _PhaseAmount ("Phase Amount", Range(0,1)) = 0
        _DamageFlash ("Damage Flash", Range(0,1)) = 0
        _TransitionProgress ("Transition Progress", Range(0,1)) = 0
        _CriticalPulse ("Critical Pulse", Range(0,1)) = 0
        _FlowSpeed ("Flow Speed", Range(0,5)) = 2.15
        _CellScale ("Cell Scale", Range(2,32)) = 16
        _MembraneWidth ("Membrane Width", Range(0.005,0.12)) = 0.045
        _GlowIntensity ("Glow Intensity", Range(0,5)) = 2.6
        _PulseSpeed ("Pulse Speed", Range(0,12)) = 4.8
        _PulseStrength ("Pulse Strength", Range(0,1)) = 0.24
        _LowThreshold ("Low Health Threshold", Range(0,1)) = 0.24
        _Intensity ("Layer Intensity", Range(0,2)) = 1
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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
            Name "RedSlimeLivingGel"
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
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            half4 _PrimaryColor;
            half4 _SecondaryColor;
            half4 _AccentColor;
            half4 _DeepColor;
            float _FillAmount;
            float _PhaseAmount;
            float _DamageFlash;
            float _TransitionProgress;
            float _CriticalPulse;
            float _FlowSpeed;
            float _CellScale;
            float _MembraneWidth;
            float _GlowIntensity;
            float _PulseSpeed;
            float _PulseStrength;
            float _LowThreshold;
            float _Intensity;

            v2f vert(appdata v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(hash21(i), hash21(i + float2(1, 0)), f.x),
                    lerp(hash21(i + float2(0, 1)), hash21(i + float2(1, 1)), f.x),
                    f.y);
            }

            float bubbleField(float2 uv, float t)
            {
                float2 a = frac(float2(uv.x * 18.0 + t * 0.13, uv.y * 3.2 - t * 0.42));
                float2 b = frac(float2(uv.x * 11.0 - t * 0.09 + 0.31, uv.y * 4.6 - t * 0.27 + 0.17));
                float bubbleA = 1.0 - smoothstep(0.10, 0.22, length(a - 0.5));
                float bubbleB = 1.0 - smoothstep(0.08, 0.18, length(b - 0.5));
                return max(bubbleA, bubbleB * 0.72);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half4 source = (tex2D(_MainTex, i.uv) + _TextureSampleAdd) * i.color;
                float t = _Time.y * _FlowSpeed;
                float2 uv = i.uv;
                float verticalWarp = sin(uv.x * 17.0 - t * 1.4) * 0.10 + sin(uv.x * 37.0 + t * 0.8) * 0.035;
                float cells = noise(float2(uv.x * _CellScale - t * 0.55, uv.y * 5.4 + verticalWarp));
                float fineCells = noise(float2(uv.x * (_CellScale * 1.9) + t * 0.34, uv.y * 10.0 - t * 0.18));
                float veins = 1.0 - smoothstep(0.035, 0.13, abs(frac(cells * 3.25 + fineCells * 1.7) - 0.5));
                float bubbles = bubbleField(uv + float2(cells * 0.025, 0), t);
                float membraneTop = 1.0 - smoothstep(0.0, _MembraneWidth, abs(uv.y - 0.92));
                float membraneBottom = 1.0 - smoothstep(0.0, _MembraneWidth * 0.72, abs(uv.y - 0.10));
                float fillRim = 1.0 - smoothstep(0.0, _MembraneWidth * 1.4, abs(uv.x - 1.0));
                float pulse = 0.5 + 0.5 * sin(_Time.y * _PulseSpeed + cells * 3.2);
                float critical = 1.0 - step(_LowThreshold, _FillAmount);

                half3 color = lerp(_DeepColor.rgb, _PrimaryColor.rgb, 0.48 + cells * 0.46);
                color = lerp(color, _SecondaryColor.rgb, fineCells * 0.42 + bubbles * 0.18);
                color += _AccentColor.rgb * (veins * 0.11 + bubbles * 0.28);
                color += _AccentColor.rgb * (membraneTop + membraneBottom * 0.35 + fillRim) * _GlowIntensity;
                color += _SecondaryColor.rgb * _CriticalPulse * pulse * _PulseStrength;
                color += _AccentColor.rgb * critical * pulse * _PulseStrength * 0.22;
                color *= lerp(0.78, 1.18, smoothstep(0.0, 1.0, uv.y));
                color *= _Intensity;

                float sweep = 1.0 - smoothstep(0.018, 0.075, abs(uv.x - _TransitionProgress));
                color += _AccentColor.rgb * sweep * saturate(_TransitionProgress) * 1.5;
                color = lerp(color, half3(1.0, 0.92, 0.86), _DamageFlash * (0.68 + pulse * 0.26));

                float spriteLight = lerp(0.78, 1.14, dot(source.rgb, float3(0.299, 0.587, 0.114)));
                half4 result = half4(color * spriteLight, source.a);
                #ifdef UNITY_UI_CLIP_RECT
                result.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - 0.001);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
