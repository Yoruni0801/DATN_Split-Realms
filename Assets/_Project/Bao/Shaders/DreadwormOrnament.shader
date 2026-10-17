Shader "Bao/UI/DreadwormOrnament"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
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
            Name "DreadwormOrnamentRecolor"
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
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _PhaseAmount;
            float _CriticalPulse;
            float _TransitionProgress;

            v2f vert(appdata v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 source = tex2D(_MainTex, i.uv) + _TextureSampleAdd;
                float luminance = dot(source.rgb, float3(0.299, 0.587, 0.114));
                fixed3 frost = fixed3(0.14, 0.95, 1.28);
                fixed3 storm = fixed3(0.72, 0.12, 1.38);
                fixed3 frostDark = fixed3(0.008, 0.12, 0.18);
                fixed3 stormDark = fixed3(0.075, 0.002, 0.20);
                fixed3 bright = lerp(frost, storm, _PhaseAmount);
                fixed3 dark = lerp(frostDark, stormDark, _PhaseAmount);
                fixed3 color = lerp(dark, bright, smoothstep(0.05, 0.86, luminance));
                float pulse = 0.5 + 0.5 * sin(_Time.y * lerp(3.2, 8.0, _PhaseAmount));
                color += bright * _CriticalPulse * pulse * 0.28;
                float sweep = 1.0 - smoothstep(0.018, 0.08, abs(i.uv.x - _TransitionProgress));
                color += bright * sweep * saturate(_TransitionProgress) * 0.85;
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
