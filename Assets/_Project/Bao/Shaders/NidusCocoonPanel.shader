Shader "Bao/UI/NidusCocoonPanel"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _PhaseAmount ("Phase Amount", Range(0,1)) = 0
        _DamageFlash ("Damage Flash", Range(0,1)) = 0
        _TransitionProgress ("Transition Progress", Range(0,1)) = 0
        _CriticalPulse ("Critical Pulse", Range(0,1)) = 0
        _Intensity ("Intensity", Range(0,2)) = 1
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
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "NidusOrganicPanel"
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
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _PhaseAmount;
            float _DamageFlash;
            float _TransitionProgress;
            float _CriticalPulse;
            float _Intensity;

            v2f vert(appdata v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
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
                return lerp(lerp(hash21(i), hash21(i + float2(1, 0)), f.x),
                            lerp(hash21(i + float2(0, 1)), hash21(i + float2(1, 1)), f.x), f.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 p = uv - 0.5;
                float t = _Time.y;

                float horizontalFade = 1.0 - smoothstep(0.38, 0.50, abs(p.x));
                float verticalFade = 1.0 - smoothstep(0.26, 0.50, abs(p.y));
                float bodyMask = horizontalFade * verticalFade;
                float centerBand = exp(-abs(p.y + 0.08) * 7.0);

                float organicNoise = noise(float2(uv.x * 8.0 - t * 0.16, uv.y * 5.0 + t * 0.08));
                float veinA = abs(sin(uv.x * 34.0 + organicNoise * 8.0 - t * 1.8 + uv.y * 9.0));
                float veinB = abs(sin(uv.x * 19.0 - uv.y * 17.0 + organicNoise * 11.0 + t * 1.1));
                float veins = smoothstep(0.91, 0.99, max(veinA, veinB)) * centerBand;

                float crackNoise = noise(float2(uv.x * 17.0 + t * 0.35, uv.y * 9.0 - t * 0.12));
                float cracks = smoothstep(0.76, 0.91, crackNoise) * smoothstep(0.05, 0.46, abs(p.x));
                cracks *= _PhaseAmount;

                fixed3 dormant = fixed3(0.018, 0.075, 0.025);
                fixed3 rupture = fixed3(0.115, 0.010, 0.006);
                fixed3 veinDormant = fixed3(0.22, 0.95, 0.045);
                fixed3 veinRupture = fixed3(1.20, 0.075, 0.012);
                fixed3 baseColor = lerp(dormant, rupture, _PhaseAmount);
                fixed3 veinColor = lerp(veinDormant, veinRupture, _PhaseAmount);

                float breathing = 0.82 + 0.18 * sin(t * lerp(2.2, 5.8, _PhaseAmount));
                baseColor *= (0.72 + organicNoise * 0.42) * _Intensity;
                baseColor += veinColor * veins * (0.34 + breathing * 0.32);
                baseColor += veinColor * cracks * (0.25 + _PhaseAmount * 0.48);
                baseColor += veinColor * _CriticalPulse * centerBand * 0.16;

                float sweep = 1.0 - smoothstep(0.015, 0.075, abs(uv.x - _TransitionProgress));
                baseColor += lerp(fixed3(0.7, 1.2, 0.2), fixed3(1.4, 0.12, 0.02), _PhaseAmount) * sweep * saturate(_TransitionProgress) * 0.85;
                baseColor = lerp(baseColor, fixed3(1.0, 0.72, 0.26), _DamageFlash * 0.22);

                float alpha = bodyMask * (0.45 + centerBand * 0.30 + veins * 0.10 + cracks * 0.14);
                alpha *= i.color.a;
                fixed4 color = fixed4(baseColor * i.color.rgb, saturate(alpha));
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
