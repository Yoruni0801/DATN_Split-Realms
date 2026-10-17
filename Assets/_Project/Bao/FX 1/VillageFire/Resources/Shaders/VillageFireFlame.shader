Shader "DATN/Duy/VillageFire/Flame"
{
    Properties
    {
        [PerRendererData] _MainTex ("Fire Atlas", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _HotColor ("Hot Core Tint", Color) = (1, 0.82, 0.42, 0.3)
        _EdgeColor ("Sooted Edge Tint", Color) = (0.82, 0.16, 0.02, 0.7)
        _Emission ("Emission", Range(0, 4)) = 1.15
        _FlickerSpeed ("Flicker Speed", Range(0, 12)) = 5.0
        _FlickerAmount ("Flicker Amount", Range(0, 0.08)) = 0.012
        _EdgeStrength ("Sooted Edge Strength", Range(0, 1)) = 0.2
        _SoftClip ("Soft Alpha Cut", Range(0.001, 0.2)) = 0.075
        _AtlasTiles ("Atlas Tiles", Vector) = (4, 2, 0, 0)
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
            Name "Village Fire"
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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Tint;
                half4 _HotColor;
                half4 _EdgeColor;
                float _Emission;
                float _FlickerSpeed;
                float _FlickerAmount;
                float _EdgeStrength;
                float _SoftClip;
                float4 _AtlasTiles;
            CBUFFER_END

            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float2 tileCount = max(_AtlasTiles.xy, float2(1.0, 1.0));
                float2 safeUv = min(input.uv, float2(0.99999, 0.99999));
                float2 scaledUv = safeUv * tileCount;
                float2 tile = floor(scaledUv);
                float2 localUv = frac(scaledUv);

                // Texture-sheet animation already places each particle in one atlas tile.
                // Deriving phase from that tile keeps the whole painted flame coherent.
                float seed = Hash12(tile + float2(17.0, 43.0));
                float verticalMask = sin(saturate(localUv.y) * 3.14159265);
                float wave = sin(localUv.y * 11.0 + _Time.y * _FlickerSpeed + seed * 6.2831853);
                localUv.x += wave * _FlickerAmount * verticalMask;
                float2 tileInset = max(_MainTex_TexelSize.xy * tileCount * 0.65, float2(0.001, 0.001));
                localUv = clamp(localUv, tileInset, 1.0 - tileInset);

                float2 atlasUv = (tile + localUv) / tileCount;
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, atlasUv);

                // A soft threshold removes the cut-out look while preserving a crisp,
                // illustrated silhouette at the outer edge of every atlas frame.
                half atlasAlpha = smoothstep(_SoftClip, _SoftClip + 0.16h, tex.a);
                half alpha = saturate(atlasAlpha * input.color.a * _Tint.a);
                clip(alpha - 0.002h);

                half luminance = dot(tex.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                half hotMask = smoothstep(0.42h, 0.96h, luminance);
                half coreMask = smoothstep(0.34h, 0.86h, atlasAlpha) * hotMask;
                half edgeMask = (1.0h - smoothstep(0.08h, 0.58h, atlasAlpha)) * _EdgeStrength;
                half3 paintedColor = tex.rgb * input.color.rgb * _Tint.rgb;
                paintedColor = lerp(paintedColor, _HotColor.rgb * input.color.rgb, coreMask * _HotColor.a);
                half3 color = lerp(paintedColor, _EdgeColor.rgb * input.color.rgb, edgeMask * _EdgeColor.a);

                // Low-amplitude, per-frame variation makes the roof breathe without
                // the uniform fast pulsing that reads as a plastic VFX overlay.
                half temporalFlicker = 0.982h + 0.018h * sin(_Time.y * (_FlickerSpeed * 1.17h) + seed * 11.0h);
                color *= _Emission * temporalFlicker;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
