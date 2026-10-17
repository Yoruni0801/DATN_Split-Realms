Shader "Bao/UI/RedSlimePanel"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        [HDR] _PrimaryColor ("Primary Gel", Color) = (1.0,0.05,0.03,1)
        [HDR] _SecondaryColor ("Secondary Gel", Color) = (1.35,0.12,0.03,1)
        [HDR] _AccentColor ("Accent", Color) = (1.55,0.62,0.22,1)
        _DeepColor ("Deep Gel", Color) = (0.11,0.005,0.003,1)
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
            Name "RedSlimeLivingPanel"
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
            half4 _PrimaryColor;
            half4 _SecondaryColor;
            half4 _AccentColor;
            half4 _DeepColor;
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
                float2 cell = floor(p);
                float2 local = frac(p);
                local = local * local * (3.0 - 2.0 * local);
                return lerp(
                    lerp(hash21(cell), hash21(cell + float2(1, 0)), local.x),
                    lerp(hash21(cell + float2(0, 1)), hash21(cell + float2(1, 1)), local.x),
                    local.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 p = uv - 0.5;
                float t = _Time.y * lerp(0.55, 1.25, _PhaseAmount);
                float coarse = noise(float2(uv.x * 8.0 - t * 0.16, uv.y * 5.0 + t * 0.08));
                float fine = noise(float2(uv.x * 19.0 + t * 0.22, uv.y * 9.0 - t * 0.14));
                float edgeWobble = (coarse - 0.5) * 0.055 + sin(uv.x * 21.0 + t) * 0.012;
                float horizontalFade = 1.0 - smoothstep(0.40 + edgeWobble, 0.50 + edgeWobble, abs(p.x));
                float verticalFade = 1.0 - smoothstep(0.25 + edgeWobble, 0.48 + edgeWobble, abs(p.y));
                float bodyMask = saturate(horizontalFade * verticalFade);
                float centerBand = exp(-abs(p.y + 0.06) * 7.0);
                float vein = 1.0 - smoothstep(0.025, 0.09, abs(frac(coarse * 4.2 + fine * 2.1) - 0.5));
                float lowerPool = (1.0 - smoothstep(-0.35, 0.20, p.y)) * horizontalFade;
                float pulse = 0.5 + 0.5 * sin(_Time.y * 5.0 + coarse * 4.0);

                half3 color = lerp(_DeepColor.rgb, _PrimaryColor.rgb * 0.34, coarse * 0.65 + centerBand * 0.18);
                color += _SecondaryColor.rgb * fine * centerBand * 0.08;
                color += _AccentColor.rgb * vein * centerBand * 0.08;
                color += _PrimaryColor.rgb * lowerPool * 0.07;
                color += _AccentColor.rgb * _CriticalPulse * pulse * centerBand * 0.16;
                color *= _Intensity;

                float sweep = 1.0 - smoothstep(0.018, 0.08, abs(uv.x - _TransitionProgress));
                color += _AccentColor.rgb * sweep * saturate(_TransitionProgress) * 0.92;
                color = lerp(color, half3(1.0, 0.83, 0.82), _DamageFlash * 0.20);

                float alpha = bodyMask * (0.34 + centerBand * 0.18 + vein * 0.055 + lowerPool * 0.08);
                fixed4 result = fixed4(color * i.color.rgb, saturate(alpha) * i.color.a);
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
