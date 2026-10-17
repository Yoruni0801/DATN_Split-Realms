Shader "Bao/Phase4/Magma Bomb Procedural Glow"
{
    Properties
    {
        [HDR] _BaseColor ("Base Color", Color) = (1,0.2,0.01,1)
        _Intensity ("Intensity", Range(0,12)) = 3
        _CoreStrength ("Core Strength", Range(0,3)) = 1
        _RingStrength ("Ring Strength", Range(0,4)) = 0.5
        _RingRadius ("Ring Radius", Range(0.05,0.48)) = 0.36
        _RingWidth ("Ring Width", Range(0.005,0.25)) = 0.06
        _Softness ("Edge Softness", Range(0.005,0.35)) = 0.12
        _ArcBreakup ("Arc Breakup", Range(0,1)) = 0.15
        _ArcCount ("Arc Count", Range(1,16)) = 6
        _SpinSpeed ("Spin Speed", Range(-10,10)) = 1.5
        _FlickerSpeed ("Flicker Speed", Range(0,20)) = 7
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent+20"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Blend SrcAlpha One
        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Name "MagmaBombGlow"

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
                half _CoreStrength;
                half _RingStrength;
                half _RingRadius;
                half _RingWidth;
                half _Softness;
                half _ArcBreakup;
                half _ArcCount;
                half _SpinSpeed;
                half _FlickerSpeed;
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
                float2 centered = input.uv - 0.5;
                float radius = length(centered);
                float angle = atan2(centered.y, centered.x);

                half outerMask = 1.0h - smoothstep(0.5h - _Softness, 0.5h, radius);
                half core = pow(saturate(1.0h - radius * 2.0h), 1.65h) * _CoreStrength;
                half ringDistance = abs(radius - _RingRadius);
                half ring = (1.0h - smoothstep(_RingWidth, _RingWidth + _Softness * 0.32h, ringDistance))
                    * _RingStrength;

                half arcWave = sin(angle * _ArcCount - _Time.y * _SpinSpeed + radius * 17.0h) * 0.5h + 0.5h;
                half arcMask = lerp(1.0h, smoothstep(0.24h, 0.72h, arcWave), _ArcBreakup);
                half flicker = 0.88h + sin(_Time.y * _FlickerSpeed + angle * 2.0h) * 0.08h
                    + sin(_Time.y * (_FlickerSpeed * 1.73h) - radius * 21.0h) * 0.04h;

                half alpha = saturate((core + ring * arcMask) * outerMask * input.color.a * _BaseColor.a);
                half3 color = _BaseColor.rgb * input.color.rgb * _Intensity * flicker;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
