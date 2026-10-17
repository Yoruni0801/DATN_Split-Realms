Shader "Bao/Phase4/Magma Bomb Trail"
{
    Properties
    {
        [HDR] _BaseColor ("Base Color", Color) = (1,0.35,0.02,1)
        _Intensity ("Intensity", Range(0,12)) = 4
        _EdgeSoftness ("Edge Softness", Range(0.01,0.45)) = 0.14
        _FlowSpeed ("Flow Speed", Range(-20,20)) = 8
        _StreakScale ("Streak Scale", Range(1,30)) = 11
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent+15"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Blend SrcAlpha One
        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Name "MagmaBombTrail"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Intensity;
                half _EdgeSoftness;
                half _FlowSpeed;
                half _StreakScale;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half edge = smoothstep(0.0h, _EdgeSoftness, input.uv.y)
                    * smoothstep(0.0h, _EdgeSoftness, 1.0h - input.uv.y);
                half flowA = sin(input.uv.x * _StreakScale - _Time.y * _FlowSpeed + input.uv.y * 8.0h) * 0.5h + 0.5h;
                half flowB = sin(input.uv.x * (_StreakScale * 1.83h) - _Time.y * (_FlowSpeed * 1.37h)) * 0.5h + 0.5h;
                half flow = 0.62h + flowA * 0.24h + flowB * 0.14h;
                half alpha = input.color.a * _BaseColor.a * edge * flow;
                half3 color = input.color.rgb * _BaseColor.rgb * _Intensity * (0.86h + flow * 0.3h);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
