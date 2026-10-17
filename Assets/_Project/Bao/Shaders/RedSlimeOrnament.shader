Shader "Bao/UI/RedSlimeOrnament"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HDR] _PrimaryColor ("Primary", Color) = (1.0,0.05,0.03,1)
        [HDR] _SecondaryColor ("Secondary", Color) = (1.35,0.12,0.03,1)
        [HDR] _AccentColor ("Accent", Color) = (1.55,0.62,0.22,1)
        _DeepColor ("Deep", Color) = (0.11,0.005,0.003,1)
        _PhaseAmount ("Phase Amount", Range(0,1)) = 0
        _CriticalPulse ("Critical Pulse", Range(0,1)) = 0
        _TransitionProgress ("Transition Progress", Range(0,1)) = 0
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
            Name "RedSlimeOrnamentRecolor"
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

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            half4 _PrimaryColor;
            half4 _SecondaryColor;
            half4 _AccentColor;
            half4 _DeepColor;
            float _PhaseAmount;
            float _CriticalPulse;
            float _TransitionProgress;

            v2f vert(appdata v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 source = tex2D(_MainTex, i.uv) + _TextureSampleAdd;
                float luminance = dot(source.rgb, float3(0.299, 0.587, 0.114));
                float shaped = smoothstep(0.045, 0.88, luminance);
                half3 color = lerp(_DeepColor.rgb, _PrimaryColor.rgb, shaped);
                color = lerp(color, _SecondaryColor.rgb, smoothstep(0.52, 0.92, luminance) * 0.62);
                color += _AccentColor.rgb * smoothstep(0.76, 1.0, luminance) * 0.58;

                float pulse = 0.5 + 0.5 * sin(_Time.y * lerp(4.4, 7.8, _PhaseAmount));
                color += _AccentColor.rgb * _CriticalPulse * pulse * 0.24;
                float shimmer = 1.0 - smoothstep(0.018, 0.065, abs(frac(i.uv.x * 1.25 - _Time.y * 0.18) - 0.5));
                color += _SecondaryColor.rgb * shimmer * shaped * 0.09;
                float sweep = 1.0 - smoothstep(0.018, 0.08, abs(i.uv.x - _TransitionProgress));
                color += _AccentColor.rgb * sweep * saturate(_TransitionProgress) * 0.86;

                fixed4 result = fixed4(color * i.color.rgb, source.a * i.color.a);
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
