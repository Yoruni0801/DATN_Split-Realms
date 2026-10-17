Shader "DATN/Duy/VillageFire/ProceduralParticle"
{
    Properties
    {
        _ColorA ("Inner Color", Color) = (1, 0.28, 0.04, 0.2)
        _ColorB ("Outer Color", Color) = (0.2, 0.12, 0.08, 0)
        _Mode ("Mode: Glow 0, Smoke 1, Ember 2", Range(0, 2)) = 0
        _Softness ("Softness", Range(0.1, 8)) = 3
        _NoiseScale ("Noise Scale", Range(0, 12)) = 4
        _Emission ("Emission", Range(0, 4)) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Float) = 10
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "Village Fire Procedural Particle"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend [_SrcBlend] [_DstBlend]
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorA;
                half4 _ColorB;
                float _Mode;
                float _Softness;
                float _NoiseScale;
                float _Emission;
            CBUFFER_END

            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash12(i);
                float b = Hash12(i + float2(1, 0));
                float c = Hash12(i + float2(0, 1));
                float d = Hash12(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float2 p = input.uv * 2.0 - 1.0;
                float shape;
                half3 color;

                if (_Mode < 0.5)
                {
                    // A wide, feathered light wash for roofs and walls.
                    p.y *= 1.8;
                    float radial = saturate(1.0 - dot(p, p));
                    shape = pow(radial, max(_Softness, 0.1));
                    color = lerp(_ColorB.rgb, _ColorA.rgb, radial);
                }
                else if (_Mode < 1.5)
                {
                    // Soft soot puff with animated breakup; deliberately low contrast.
                    p.x += sin((p.y + _Time.y * 0.07) * 5.0 + input.positionWS.x) * 0.08;
                    p.y *= 0.92;
                    float radial = saturate(1.0 - dot(p, p));
                    float noiseA = ValueNoise(input.uv * _NoiseScale + _Time.y * float2(0.09, 0.13));
                    float noiseB = ValueNoise(input.uv * (_NoiseScale * 1.91) - _Time.y * float2(0.05, 0.08));
                    float breakup = saturate(noiseA * 0.7 + noiseB * 0.3);
                    shape = smoothstep(0.05, 0.72, radial * (0.58 + breakup * 0.55));
                    shape *= smoothstep(0.0, 0.18, radial);
                    color = lerp(_ColorB.rgb, _ColorA.rgb, breakup * 0.55 + radial * 0.45);
                }
                else
                {
                    // Tiny elongated ember, rendered without a texture dependency.
                    p.x *= 3.7;
                    p.y *= 1.15;
                    float radial = saturate(1.0 - dot(p, p));
                    shape = pow(radial, max(_Softness, 0.1));
                    color = lerp(_ColorB.rgb, _ColorA.rgb, radial);
                }

                half alpha = saturate(shape * input.color.a * lerp(_ColorB.a, _ColorA.a, shape));
                clip(alpha - 0.001h);
                color *= input.color.rgb * _Emission;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
