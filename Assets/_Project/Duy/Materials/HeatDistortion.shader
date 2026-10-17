Shader "Custom/URP/HeatHazeDistortion_Smooth"
{
    Properties
    {
        _TintColor ("Haze Tint", Color) = (1.0, 0.55, 0.25, 0.25)

        _Intensity ("Distortion Intensity", Range(0, 0.08)) = 0.025
        _Speed ("Rising Speed", Range(0.1, 5)) = 1.5

        _NoiseScale ("Noise Scale", Float) = 6
        _HorizontalStrength ("Horizontal Strength", Range(0, 5)) = 2.0
        _VerticalStrength ("Vertical Strength", Range(0, 2)) = 0.5

        _HazeStrength ("Haze Strength", Range(0, 1)) = 0.18
        _Alpha ("Alpha", Range(0, 1)) = 0.65

        _EdgeSoftness ("Edge Softness", Range(0, 0.5)) = 0.18
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent+10"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        Pass
        {
            Name "HeatHazeSmooth"

            ZWrite Off
            ZTest LEqual
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _TintColor;

                float _Intensity;
                float _Speed;
                float _NoiseScale;

                float _HorizontalStrength;
                float _VerticalStrength;

                float _HazeStrength;
                float _Alpha;
                float _EdgeSoftness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 screenPos   : TEXCOORD1;
            };

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float smoothNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));

                // Mượt hơn f*f*(3-2*f), giảm cảm giác ô vuông
                float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;

                // Ma trận xoay nhẹ để noise không bị thẳng hàng thành ô
                float2x2 rot = float2x2(
                    0.80, -0.60,
                    0.60,  0.80
                );

                for (int i = 0; i < 5; i++)
                {
                    value += amplitude * smoothNoise(p);
                    p = mul(rot, p * 2.05);
                    amplitude *= 0.5;
                }

                return value;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);

                output.positionHCS = posInputs.positionCS;
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(output.positionHCS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float time = _Time.y * _Speed;

                float2 screenUV = input.screenPos.xy / input.screenPos.w;

                // Mask mềm để không thấy biên hình vuông của Quad
                float edgeX = smoothstep(0.0, _EdgeSoftness, input.uv.x) *
                            smoothstep(0.0, _EdgeSoftness, 1.0 - input.uv.x);

                float edgeY = smoothstep(0.0, _EdgeSoftness, input.uv.y) *
                            smoothstep(0.0, _EdgeSoftness, 1.0 - input.uv.y);

                float softEdgeMask = edgeX * edgeY;

                // Không chia trên/dưới nữa.
                // Hiệu ứng mạnh đều toàn vùng Quad, chỉ mềm ở rìa.
                float heatFactor = softEdgeMask;

                float2 p1 = screenUV * _NoiseScale;
                p1.y -= time * 0.8;

                float2 p2 = screenUV * (_NoiseScale * 1.7);
                p2.y -= time * 1.25;
                p2.x += 13.27;

                float n1 = fbm(p1);
                float n2 = fbm(p2);

                float2 distortion = float2(
                    (n1 - 0.5) * _HorizontalStrength,
                    (n2 - 0.5) * _VerticalStrength
                );

                distortion *= _Intensity * heatFactor;

                float2 distortedScreenUV = screenUV + distortion;

                half4 sceneColor = SAMPLE_TEXTURE2D(
                    _CameraOpaqueTexture,
                    sampler_CameraOpaqueTexture,
                    distortedScreenUV
                );

                float hazeNoise = fbm(screenUV * 4.0 - float2(0.0, time * 0.35));
                float hazeAmount = hazeNoise * _HazeStrength * heatFactor;

                half3 finalRGB = lerp(sceneColor.rgb, _TintColor.rgb, hazeAmount);

                return half4(finalRGB, _Alpha * softEdgeMask);
            }

            ENDHLSL
        }
    }
}