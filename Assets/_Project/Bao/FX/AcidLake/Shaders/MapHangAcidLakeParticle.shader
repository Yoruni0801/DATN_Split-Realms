Shader "Bao/Acid Lake Particle Alpha"
{
    Properties
    {
        [PerRendererData] _MainTex ("Particle Texture", 2D) = "white" {}
        _TintColor ("Tint", Color) = (0.35, 1, 0.08, 1)
        [HDR] _EmissionColor ("Emission", Color) = (0.3, 1.2, 0.04, 1)
        _GlobalAlpha ("Global Alpha", Range(0, 1)) = 1
        _Intensity ("Intensity", Range(0, 3)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _TintColor;
                float4 _EmissionColor;
                float _GlobalAlpha;
                float _Intensity;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            Varyings vert(Attributes input) { Varyings output; output.positionCS = TransformObjectToHClip(input.positionOS.xyz); output.uv = input.uv; output.color = input.color; return output; }
            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float alpha = tex.a * input.color.a * _TintColor.a * _GlobalAlpha;
                float3 rgb = tex.rgb * input.color.rgb * _TintColor.rgb;
                rgb += _EmissionColor.rgb * tex.a * input.color.a * _Intensity * 0.18;
                return half4(rgb * alpha, alpha);
            }
            ENDHLSL
        }
    }
}