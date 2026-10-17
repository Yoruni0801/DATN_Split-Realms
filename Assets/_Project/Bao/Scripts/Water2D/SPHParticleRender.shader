Shader "Custom/SPHParticleRender"
{
    Properties {
        _Color ("Color", Color) = (0, 0.5, 1, 1)
        _ParticleRadius ("Particle Radius", Float) = 0.1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
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
                float2 force;
                float density;
                float pressure;
            };
            StructuredBuffer<Particle> Particles;

            float4 _Color;
            float _ParticleRadius;

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
                float3 worldPos = float3(p.position, 0) + float3(vertexPos * _ParticleRadius, 0);
                
                output.positionCS = TransformWorldToHClip(worldPos);
                
                // Color based on density/speed
                float speed = length(p.velocity);
                float3 speedColor = lerp(_Color.rgb, float3(1, 1, 1), saturate(speed * 0.1));
                output.color = float4(speedColor, _Color.a);
                
                return output;
            }

            half4 frag(Varyings input) : SV_Target {
                float dist = length(input.uv);
                if (dist > 1.0) discard;
                
                // Metaball soft edge
                float alpha = 1.0 - smoothstep(0.5, 1.0, dist);
                return float4(input.color.rgb, input.color.a * alpha);
            }
            ENDHLSL
        }
    }
}
