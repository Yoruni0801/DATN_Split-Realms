Shader "Quoc/Phase2/Ground Rift Additive"
{
    Properties
    {
        [HDR] _BaseColor ("Edge Color", Color) = (1,0.2,0.015,1)
        [HDR] _CoreColor ("Core Color", Color) = (1,0.92,0.35,1)
        _Intensity ("Intensity", Range(0,12)) = 4
        _FlowSpeed ("Flow Speed", Range(-20,20)) = 7
        _NoiseScale ("Noise Scale", Range(1,40)) = 13
        _EdgeSoftness ("Edge Softness", Range(0.01,0.49)) = 0.16
        _PulseSpeed ("Pulse Speed", Range(0,20)) = 8
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent+25"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        // Alpha blend giữ màu đất/đá, tránh chồng hạt thành một ống neon cháy sáng.
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Name "GroundRiftAdditive"

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
                half4 _CoreColor;
                half _Intensity;
                half _FlowSpeed;
                half _NoiseScale;
                half _EdgeSoftness;
                half _PulseSpeed;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half edgeMask = smoothstep(0.0h, _EdgeSoftness, input.uv.y)
                    * smoothstep(0.0h, _EdgeSoftness, 1.0h - input.uv.y);
                half coreMask = smoothstep(0.34h, 0.5h, input.uv.y)
                    * smoothstep(0.34h, 0.5h, 1.0h - input.uv.y);

                float2 flowUv = float2(input.uv.x * _NoiseScale - _Time.y * _FlowSpeed,
                                       input.uv.y * 5.0);
                half noiseA = Hash21(floor(flowUv * 2.0));
                half wave = sin(flowUv.x + sin(flowUv.y * 2.3h)) * 0.5h + 0.5h;
                half pulse = 0.86h + 0.14h * sin(_Time.y * _PulseSpeed + input.uv.x * 9.0h);
                half energy = saturate(0.58h + wave * 0.28h + noiseA * 0.22h) * pulse;

                half3 color = lerp(_BaseColor.rgb, _CoreColor.rgb, coreMask);
                color *= input.color.rgb * _Intensity * energy;
                half alpha = input.color.a * edgeMask * (_BaseColor.a + coreMask * 0.35h);
                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
