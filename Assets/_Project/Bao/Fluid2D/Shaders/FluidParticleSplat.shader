Shader "Custom/FluidParticleSplat"
{
    Properties {
        _Color ("Color", Color) = (0.55, 0.88, 0.92, 0.5)
        _ParticleRadius ("Particle Radius", Float) = 0.08
        _CoreStrength ("Core Strength", Range(0.2, 2.0)) = 1.0
        _EdgeFeather ("Edge Feather", Range(0.5, 4.0)) = 2.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Particle {
                float2 position;
                float2 velocity;
                float2 predictedPosition;
                float density;
                float nearDensity;
                float pressure;
                float nearPressure;
                int active;
                int type;
            };

            StructuredBuffer<Particle> Particles;

            float4 _Color;
            float _ParticleRadius;
            float _CoreStrength;
            float _EdgeFeather;

            struct Attributes {
                uint vertexID : SV_VertexID;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings vert(Attributes input) {
                Varyings output;
                
                float2 quad[6] = {
                    float2(-1, -1),
                    float2(-1, 1),
                    float2(1, -1),
                    float2(1, -1),
                    float2(-1, 1),
                    float2(1, 1)
                };
                
                float2 vertexPos = quad[input.vertexID];
                output.uv = vertexPos;

                Particle p = Particles[input.instanceID];
                
                if (p.active == 0) {
                    output.positionCS = float4(0, 0, 0, 0);
                    output.color = float4(0, 0, 0, 0);
                    return output;
                }

                float speed = length(p.velocity);
                float stretch = saturate(speed * 0.08f);
                float2 dir = speed > 0.001f ? normalize(p.velocity) : float2(0, 1);
                float2 tangent = float2(-dir.y, dir.x);
                float2 stretchedVertex = tangent * vertexPos.x * (1.0f - stretch * 0.25f) +
                                         dir * vertexPos.y * (1.0f + stretch * 0.65f);

                // Billboard hơi kéo theo hướng vận tốc giúp dòng nước có cảm giác chảy thay vì chỉ là chấm tròn.
                float visualRadius = _ParticleRadius * lerp(5.2f, 6.4f, saturate(_CoreStrength));
                float3 worldPos = float3(p.position, 0) + float3(stretchedVertex * visualRadius, 0);
                output.positionCS = TransformWorldToHClip(worldPos);
                
                // Mật độ cơ bản đóng góp bởi hạt này
                output.color = float4(stretchedVertex, speed, _Color.a);
                
                return output;
            }

            half4 frag(Varyings input) : SV_Target {
                if (input.color.a <= 0.0f) discard;
                
                float dist = length(input.uv);
                if (dist > 1.0f) discard;
                
                // Mật độ mềm ở rìa và đầy hơn ở lõi để metaball gom thành khối nước liền mạch.
                float core = pow(saturate(1.0f - dist), max(0.5f, _EdgeFeather));
                float shoulder = smoothstep(1.0f, 0.15f, dist) * 0.28f;
                float density = (core * _CoreStrength + shoulder) * input.color.a;
                return float4(0, 0, 0, density);
            }
            ENDHLSL
        }
    }
}
