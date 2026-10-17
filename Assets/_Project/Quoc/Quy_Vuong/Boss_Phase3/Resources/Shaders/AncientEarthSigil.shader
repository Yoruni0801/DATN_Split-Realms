Shader "Quoc/Phase2/Ancient Earth Sigil"
{
    Properties
    {
        _BaseColor ("Weathered Edge", Color) = (0.18,0.08,0.025,0.9)
        _CoreColor ("Carved Ochre", Color) = (0.72,0.38,0.10,1)
        _Intensity ("Low Ember", Range(0,3)) = 0.9
        _ChipScale ("Stone Chip Scale", Range(2,48)) = 18
        _Roughness ("Carved Roughness", Range(0,1)) = 0.48
        _BreathSpeed ("Ancient Breath", Range(0,3)) = 0.42
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent+24"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Name "AncientEarthSigil"

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
                half _ChipScale;
                half _Roughness;
                half _BreathSpeed;
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
                p += dot(p, p + 34.45);
                return frac(p.x * p.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half edge = smoothstep(0.015h, 0.22h, input.uv.y)
                    * smoothstep(0.015h, 0.22h, 1.0h - input.uv.y);
                half core = smoothstep(0.30h, 0.49h, input.uv.y)
                    * smoothstep(0.30h, 0.49h, 1.0h - input.uv.y);

                float2 chipCell = floor(float2(input.uv.x * _ChipScale, input.uv.y * 5.0h));
                half chips = Hash21(chipCell);
                half worn = lerp(1.0h, smoothstep(0.10h, 0.72h, chips), _Roughness);
                half grain = 0.84h + Hash21(chipCell + 17.0h) * 0.16h;
                half breath = 0.94h + sin(_Time.y * _BreathSpeed + chips * 2.0h) * 0.06h;

                half3 pigment = lerp(_BaseColor.rgb, _CoreColor.rgb, core * 0.62h);
                pigment *= input.color.rgb * _Intensity * grain * breath;
                half alpha = input.color.a * edge * worn
                    * lerp(_BaseColor.a, _CoreColor.a, core * 0.48h);
                return half4(pigment, saturate(alpha));
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
