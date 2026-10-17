Shader "Bao/UI/NidusCocoonHealthBar"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FillAmount ("Fill Amount", Range(0,1)) = 1
        _PhaseAmount ("Phase Amount", Range(0,1)) = 0
        _DamageFlash ("Damage Flash", Range(0,1)) = 0
        _TransitionProgress ("Transition Progress", Range(0,1)) = 0
        _CriticalPulse ("Critical Pulse", Range(0,1)) = 0
        _CoreColor ("Core Color", Color) = (0.34, 1, 0.06, 1)
        _ToxicColor ("Toxic Color", Color) = (0.85, 1, 0.02, 1)
        _PhaseColor ("Phase Color", Color) = (1, 0.12, 0.02, 1)
        _VeinColor ("Vein Color", Color) = (0.08, 0.32, 0.01, 1)
        _GlowColor ("Glow Color", Color) = (0.74, 1, 0.2, 1)
        _FlowSpeed ("Flow Speed", Range(0,8)) = 2.4
        _VeinScale ("Vein Scale", Range(4,40)) = 18
        _GlowWidth ("Glow Width", Range(0.002,0.12)) = 0.025
        _GlowIntensity ("Glow Intensity", Range(0,5)) = 2.2
        _PulseSpeed ("Pulse Speed", Range(0,10)) = 3.5
        _PulseStrength ("Pulse Strength", Range(0,1)) = 0.24
        _CriticalThreshold ("Critical Threshold", Range(0,1)) = 0.3
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
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "NidusCocoonHealthBar"
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
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float _FillAmount;
            float _PhaseAmount;
            float _DamageFlash;
            float _TransitionProgress;
            float _CriticalPulse;
            fixed4 _CoreColor;
            fixed4 _ToxicColor;
            fixed4 _PhaseColor;
            fixed4 _VeinColor;
            fixed4 _GlowColor;
            float _FlowSpeed;
            float _VeinScale;
            float _GlowWidth;
            float _GlowIntensity;
            float _PulseSpeed;
            float _PulseStrength;
            float _CriticalThreshold;

            v2f vert(appdata v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float noise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1, 0)), f.x), lerp(hash21(i + float2(0, 1)), hash21(i + float2(1, 1)), f.x), f.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half4 source = (tex2D(_MainTex, i.texcoord) + _TextureSampleAdd) * i.color;
                float time = _Time.y * _FlowSpeed;
                float veinNoise = noise(float2(i.texcoord.x * _VeinScale - time, i.texcoord.y * 7.0 + time * 0.35));
                float veins = smoothstep(0.56, 0.78, veinNoise) * (0.34 + 0.66 * sin((i.texcoord.x * _VeinScale + i.texcoord.y * 4.0 - time) * 2.2) * 0.5 + 0.5);
                float bubble = smoothstep(0.72, 0.94, noise(float2(i.texcoord.x * 9.0 + time * 0.55, i.texcoord.y * 12.0 - time)));
                float phaseMix = saturate(_PhaseAmount);
                float critical = 1.0 - step(_CriticalThreshold, _FillAmount);
                float pulse = 0.5 + 0.5 * sin(_Time.y * _PulseSpeed);
                fixed3 phaseToxicColor = fixed3(1.32, 0.16, 0.008);
                fixed3 phaseVeinColor = fixed3(0.24, 0.003, 0.001);
                fixed3 phaseGlowColor = fixed3(1.45, 0.095, 0.008);
                fixed3 activeCoreColor = lerp(_CoreColor.rgb, _PhaseColor.rgb, phaseMix);
                fixed3 activeToxicColor = lerp(_ToxicColor.rgb, phaseToxicColor, phaseMix);
                fixed3 activeVeinColor = lerp(_VeinColor.rgb, phaseVeinColor, phaseMix);
                fixed3 activeGlowColor = lerp(_GlowColor.rgb, phaseGlowColor, phaseMix);
                fixed3 baseColor = lerp(activeCoreColor, activeToxicColor, veinNoise * 0.55 + bubble * 0.18);
                baseColor = lerp(baseColor, _PhaseColor.rgb, critical * pulse * (_PulseStrength + _CriticalPulse * 0.45));
                baseColor = lerp(baseColor, activeVeinColor, veins * 0.52);
                float verticalLight = lerp(0.68, 1.2, smoothstep(0.0, 0.88, i.texcoord.y));
                baseColor *= verticalLight;
                float edge = 1.0 - smoothstep(0.0, _GlowWidth, abs(i.texcoord.x - 1.0));
                float shimmer = smoothstep(0.82, 1.0, sin((i.texcoord.x * 9.0 - _Time.y * 2.0) + i.texcoord.y * 5.0) * 0.5 + 0.5);
                baseColor += activeGlowColor * (edge * _GlowIntensity + shimmer * 0.16 + bubble * 0.12);
                float transitionWave = 1.0 - smoothstep(0.025, 0.11, abs(i.texcoord.x - _TransitionProgress));
                baseColor += activeGlowColor * transitionWave * _TransitionProgress * 1.8;
                baseColor = lerp(baseColor, float3(1.0, 0.92, 0.54), _DamageFlash * (0.64 + pulse * 0.36));
                // The health fill must be unmistakably red in phase two. This final branch
                // prevents the green baked sprite/palette from bleeding back into the Fill.
                if (phaseMix >= 0.5)
                {
                    fixed3 bloodRed = fixed3(1.22, 0.025, 0.006);
                    fixed3 emberRed = fixed3(1.35, 0.22, 0.025);
                    baseColor = bloodRed + emberRed * (bubble * 0.22 + shimmer * 0.10 + edge * 0.55);
                }
                float spriteLight = lerp(0.72, 1.18, dot(source.rgb, float3(0.299, 0.587, 0.114)));
                half4 color = half4(baseColor * spriteLight, source.a);
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
