Shader "Bao/UI/DreadwormPanel"
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
            Name "DreadwormFrozenStormPanel"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 worldPosition : TEXCOORD1; };

            fixed4 _Color;
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
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 37.19);
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

            float lightning(float2 uv, float t)
            {
                float path = 0.5 + sin(uv.x * 16.0 + t * 3.1) * 0.055 + sin(uv.x * 41.0 - t * 1.7) * 0.024;
                float branch = abs(uv.y - path);
                return 1.0 - smoothstep(0.006, 0.022, branch);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 p = uv - 0.5;
                float phase = saturate(_PhaseAmount);
                float t = _Time.y * lerp(0.65, 1.7, phase);

                float horizontalFade = 1.0 - smoothstep(0.39, 0.50, abs(p.x));
                float verticalFade = 1.0 - smoothstep(0.27, 0.50, abs(p.y));
                float bodyMask = horizontalFade * verticalFade;
                float centerBand = exp(-abs(p.y + 0.06) * 6.3);
                float frost = noise(float2(uv.x * 11.0 - t * 0.12, uv.y * 7.0 + t * 0.08));
                float facet = abs(frac((uv.x * 13.0 + uv.y * 7.0) + frost * 1.8) - 0.5);
                float iceVeins = 1.0 - smoothstep(0.035, 0.10, facet);
                float arcs = lightning(uv, t) * phase;
                float branchArcs = lightning(float2(uv.x, uv.y * 1.8 + 0.18), -t * 1.3) * phase * 0.65;

                fixed3 frostBase = fixed3(0.012, 0.095, 0.14);
                fixed3 stormBase = fixed3(0.045, 0.004, 0.15);
                fixed3 frostLight = fixed3(0.17, 0.92, 1.25);
                fixed3 stormLight = fixed3(0.72, 0.16, 1.55);
                fixed3 baseColor = lerp(frostBase, stormBase, phase);
                fixed3 lightColor = lerp(frostLight, stormLight, phase);

                baseColor *= (0.66 + frost * 0.55) * _Intensity;
                baseColor += lightColor * iceVeins * centerBand * lerp(0.11, 0.22, phase);
                baseColor += lightColor * (arcs + branchArcs) * lerp(0.35, 1.25, phase);
                float pulse = 0.5 + 0.5 * sin(_Time.y * lerp(3.0, 8.5, phase));
                baseColor += lightColor * _CriticalPulse * pulse * centerBand * 0.25;
                float sweep = 1.0 - smoothstep(0.015, 0.07, abs(uv.x - _TransitionProgress));
                baseColor += fixed3(0.9, 0.75, 1.5) * sweep * saturate(_TransitionProgress) * 1.25;
                baseColor = lerp(baseColor, fixed3(0.82, 0.96, 1.0), _DamageFlash * 0.2);

                float alpha = bodyMask * (0.43 + centerBand * 0.25 + iceVeins * 0.08 + (arcs + branchArcs) * 0.16);
                fixed4 color = fixed4(baseColor * i.color.rgb, saturate(alpha) * i.color.a);
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
