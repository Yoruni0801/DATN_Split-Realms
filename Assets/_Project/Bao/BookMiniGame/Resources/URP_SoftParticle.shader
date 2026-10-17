Shader "Custom/URP_SoftParticle"
{
    Properties
    {
        [HDR] _Color("Tint Color", Color) = (1, 1, 1, 1)
        _Softness("Edge Softness", Range(0.01, 1)) = 1.0
        _CoreIntensity("Core Intensity", Range(0, 5)) = 2.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "PreviewType"="Plane" }
        LOD 100
        
        Blend SrcAlpha One // Additive Blending
        ZWrite Off
        Cull Off
        ZTest LEqual

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_particles
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Softness;
                float _CoreIntensity;
            CBUFFER_END

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                // Distance from center (0.5, 0.5)
                float2 center = i.uv - 0.5;
                float dist = length(center);
                
                // Create a soft circle
                // dist goes from 0 at center to 0.5 at edge
                // We want alpha 1 at center, 0 at edge.
                float outerFade = smoothstep(0.5, 0.5 - (_Softness * 0.5), dist);
                
                // Create a bright hot core in the middle
                float core = smoothstep(0.2, 0.0, dist) * _CoreIntensity;
                
                // Combine and apply vertex color alpha
                float finalAlpha = (outerFade + core * outerFade) * i.color.a;

                // Return final HDR color
                return half4(i.color.rgb, finalAlpha);
            }
            ENDHLSL
        }
    }
}
