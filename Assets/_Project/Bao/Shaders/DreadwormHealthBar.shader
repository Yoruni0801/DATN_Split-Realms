Shader "Bao/UI/DreadwormHealthBar"
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
        _IceColor ("Phase 1 Green", Color) = (0.22, 0.92, 0.035, 1)
        _CoreColor ("Phase 1 Core", Color) = (0.76, 1, 0.08, 1)
        _StormColor ("Phase 1 Dark", Color) = (0.04, 0.31, 0.01, 1)
        _PhaseColor ("Phase 2 Red", Color) = (1, 0.055, 0.012, 1)
        _CrackColor ("Phase 2 Glow", Color) = (1, 0.34, 0.075, 1)
        _FlowSpeed ("Flow Speed", Range(0,10)) = 3.2
        _CrackScale ("Crack Scale", Range(4,45)) = 24
        _LightningWidth ("Lightning Width", Range(0.002,0.12)) = 0.035
        _GlowIntensity ("Glow Intensity", Range(0,7)) = 3.5
        _PulseSpeed ("Pulse Speed", Range(0,12)) = 5
        _PulseStrength ("Pulse Strength", Range(0,1)) = 0.28
        _LowThreshold ("Low Health Threshold", Range(0,1)) = 0.3
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
            Name "DreadwormStormHealthBar"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; float4 worldPosition : TEXCOORD1; };

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
            fixed4 _IceColor;
            fixed4 _CoreColor;
            fixed4 _StormColor;
            fixed4 _PhaseColor;
            fixed4 _CrackColor;
            float _FlowSpeed;
            float _CrackScale;
            float _LightningWidth;
            float _GlowIntensity;
            float _PulseSpeed;
            float _PulseStrength;
            float _LowThreshold;

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
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 37.19);
                return frac(p.x * p.y);
            }

            float noise(float2 uv)
            {
                float2 cell = floor(uv);
                float2 local = frac(uv);
                local = local * local * (3.0 - 2.0 * local);
                return lerp(lerp(hash21(cell), hash21(cell + float2(1, 0)), local.x), lerp(hash21(cell + float2(0, 1)), hash21(cell + float2(1, 1)), local.x), local.y);
            }

            float lightning(float2 uv, float time)
            {
                float wave = uv.x * 8.0 + sin(uv.x * 17.0 + time * 2.0) * 0.45 + sin(uv.x * 39.0 - time * 1.3) * 0.18;
                float branch = abs(frac(uv.y * _CrackScale + wave) - 0.5);
                float crack = 1.0 - smoothstep(0.02, _LightningWidth, branch);
                return crack * (0.45 + 0.55 * noise(float2(uv.x * 12.0 - time, uv.y * 8.0 + time * 0.5)));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half4 source = (tex2D(_MainTex, i.texcoord) + _TextureSampleAdd) * i.color;
                float phase = saturate(_PhaseAmount);
                float time = _Time.y * _FlowSpeed * lerp(1.0, 1.85, phase);
                float storm = noise(float2(i.texcoord.x * 16.0 - time, i.texcoord.y * 9.0 + time * 0.35));
                float arcs = lightning(i.texcoord, time);
                float sparks = smoothstep(0.82, 0.98, noise(float2(i.texcoord.x * 31.0 + time * 1.7, i.texcoord.y * 14.0 - time)));
                float critical = 1.0 - step(_LowThreshold, _FillAmount);
                float pulse = 0.5 + 0.5 * sin(_Time.y * _PulseSpeed);
                fixed3 phaseStormColor = fixed3(0.26, 0.004, 0.002);
                fixed3 phaseCoreColor = fixed3(1.26, 0.20, 0.035);
                fixed3 phaseCrackColor = fixed3(1.35, 0.42, 0.09);
                fixed3 activeIceColor = lerp(_IceColor.rgb, _PhaseColor.rgb, phase);
                fixed3 activeStormColor = lerp(_StormColor.rgb, phaseStormColor, phase);
                fixed3 activeCoreColor = lerp(_CoreColor.rgb, phaseCoreColor, phase);
                fixed3 activeCrackColor = lerp(_CrackColor.rgb, phaseCrackColor, phase);
                fixed3 color = lerp(activeIceColor, activeStormColor, storm * 0.72);
                color = lerp(color, _PhaseColor.rgb, critical * pulse * _PulseStrength);
                color += activeCoreColor * (storm * 0.25 + sparks * 0.32);
                color += activeCrackColor * (arcs * _GlowIntensity * lerp(0.72, 1.35, phase) + sparks * 0.5);
                color *= lerp(0.74, 1.22, smoothstep(0.0, 0.9, i.texcoord.y));
                float rim = 1.0 - smoothstep(0.0, _LightningWidth, abs(i.texcoord.x - 1.0));
                color += activeCrackColor * rim * _GlowIntensity;
                color += activeCrackColor * _CriticalPulse * pulse * 0.38;
                float transitionWave = 1.0 - smoothstep(0.018, 0.085, abs(i.texcoord.x - _TransitionProgress));
                color += activeCrackColor * transitionWave * saturate(_TransitionProgress) * 1.7;
                color = lerp(color, float3(1, 1, 1), _DamageFlash * (0.72 + pulse * 0.28));
                // Match the shared boss-health language: green before phase two, red after it.
                // This is intentionally the final palette decision for the Fill only.
                if (phase >= 0.5)
                {
                    fixed3 bloodRed = fixed3(1.16, 0.02, 0.005);
                    fixed3 emberRed = fixed3(1.32, 0.22, 0.035);
                    color = bloodRed + emberRed * (storm * 0.16 + arcs * 0.35 + sparks * 0.20 + rim * 0.32);
                }
                float spriteLight = lerp(0.72, 1.18, dot(source.rgb, float3(0.299, 0.587, 0.114)));
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
