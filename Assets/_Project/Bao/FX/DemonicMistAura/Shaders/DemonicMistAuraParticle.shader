Shader "Bao/DemonicMistAuraParticle"
{
    Properties
    {
        _MainTex ("Particle Texture", 2D) = "white" {}
        _NoiseTex ("Flow Noise", 2D) = "gray" {}
        [HDR] _TintColor ("Tint", Color) = (0.14,0.02,0.28,1)
        [HDR] _EmissionColor ("Emission", Color) = (0.55,0.05,1.5,1)
        _Intensity ("Intensity", Range(0,4)) = 1
        _GlobalAlpha ("Master Alpha", Range(0,1)) = 1
        _NoiseScale ("Noise Scale", Float) = 2.4
        _NoiseSpeed ("Noise Speed", Float) = 0.11
        _DistortionStrength ("Flow Distortion", Range(0,0.25)) = 0.045
        _EdgeErode ("Edge Erosion", Range(0,0.8)) = 0.22
        _Softness ("Edge Softness", Range(0.02,0.8)) = 0.18
        _CoreGlow ("Core Glow", Range(0,2)) = 0.45
        [HideInInspector] _BlendSrc ("Blend Src", Float) = 5
        [HideInInspector] _BlendDst ("Blend Dst", Float) = 10
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend [_BlendSrc] [_BlendDst]

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);  SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _NoiseTex_ST;
                float4 _TintColor;
                float4 _EmissionColor;
                float _Intensity;
                float _GlobalAlpha;
                float _NoiseScale;
                float _NoiseSpeed;
                float _DistortionStrength;
                float _EdgeErode;
                float _Softness;
                float _CoreGlow;
            CBUFFER_END

            float _BlendSrc;
            float _BlendDst;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float time = _Time.y * _NoiseSpeed;
                float2 flowA = input.uv * _NoiseScale + float2(time, -time * 0.73);
                float2 flowB = input.uv * (_NoiseScale * 1.83) + float2(-time * 0.47, time * 1.19);
                float noiseA = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, flowA).r;
                float noiseB = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, flowB).g;
                float flow = noiseA * 0.68 + noiseB * 0.32;
                float2 warpedUv = input.uv + float2(noiseA - 0.5, noiseB - 0.5) * _DistortionStrength;
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, warpedUv);

                float eroded = tex.a - flow * _EdgeErode;
                float mask = smoothstep(0.0, max(0.02, _Softness), eroded);
                float alpha = mask * input.color.a * _TintColor.a * _GlobalAlpha;
                float inner = pow(saturate(mask), 2.4);
                float detail = lerp(0.72, 1.12, flow);

                half3 source = tex.rgb * input.color.rgb;
                half3 body = source * _TintColor.rgb * detail;
                half3 glow = source * _EmissionColor.rgb * inner * _CoreGlow;
                half3 rgb = (body + glow) * _Intensity;
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
