Shader "DATN/IceButton/TopRing"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _RuneColor ("Rune Energy", Color) = (1.65, 0.72, 0.1, 1)
        [HDR] _EdgeColor ("Golden Ice Core", Color) = (1.45, 1.08, 0.52, 1)
        _AbyssColor ("Well Depth", Color) = (0.006, 0.02, 0.085, 1)
        [HDR] _MistColor ("Magic Smoke", Color) = (1.05, 0.72, 0.24, 1)
        _Contrast ("Surface Contrast", Range(0.5, 2)) = 1.22
        _SurfaceLift ("Surface Lift", Range(0, 1)) = 0.14
        _DepthStrength ("Well Depth Strength", Range(0, 1)) = 0.78
        _RimIntensity ("Rim Intensity", Range(0, 3)) = 1.35
        _RingRect ("Ring UV Rect", Vector) = (0.23177, 0.55469, 0.54167, 0.24609)
        _Pulse ("Pulse", Range(0, 1)) = 0
        _Charge ("Charge", Range(0, 1)) = 0
        _Color ("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+22"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "AnimatedTopRing"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                float4 _RuneColor;
                float4 _EdgeColor;
                float4 _AbyssColor;
                float4 _MistColor;
                float4 _Color;
                float4 _RingRect;
                float _Contrast;
                float _SurfaceLift;
                float _DepthStrength;
                float _RimIntensity;
                float _Pulse;
                float _Charge;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 source = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                half2 ringUV = (input.uv - _RingRect.xy) / _RingRect.zw;
                half insideRect = step(0.0h, ringUV.x) * step(ringUV.x, 1.0h)
                                * step(0.0h, ringUV.y) * step(ringUV.y, 1.0h);
                half2 centered = ringUV * 2.0h - 1.0h;
                half radial = length(centered);
                half ringMask = insideRect * (1.0h - smoothstep(0.9h, 1.02h, radial));
                half cleanAlpha = source.a * ringMask;
                clip(cleanAlpha - 0.005h);
                half luminance = dot(source.rgb, half3(0.2126h, 0.7152h, 0.0722h));

                half3 contrasted = saturate((source.rgb - 0.5h) * _Contrast + 0.5h);
                half3 cooled = contrasted * half3(0.72h, 0.98h, 1.12h);
                half snow = smoothstep(0.62h, 0.94h, luminance);
                half3 surface = lerp(cooled, contrasted * 1.15h, snow);
                surface += _EdgeColor.rgb * snow * _SurfaceLift;

                half angle = atan2(centered.y, centered.x);

                // Dark inner basin gives the well a readable depth against the cyan level.
                half innerDisc = 1.0h - smoothstep(0.58h, 0.69h, radial);
                half centerDepth = 1.0h - smoothstep(0.04h, 0.66h, radial);
                half3 abyss = _AbyssColor.rgb * lerp(1.2h, 0.38h, centerDepth);
                abyss += _MistColor.rgb * centerDepth * (0.025h + _Pulse * 0.018h);
                surface = lerp(surface, abyss, innerDisc * _DepthStrength);

                // Two crisp elliptical rims: a cobalt body and a narrow white ice core.
                half outerRim = 1.0h - smoothstep(0.025h, 0.135h, abs(radial - 0.755h));
                half innerRim = 1.0h - smoothstep(0.01h, 0.068h, abs(radial - 0.665h));
                half coreRim = 1.0h - smoothstep(0.005h, 0.032h, abs(radial - 0.668h));

                // Segmented ancient energy rotates around the mouth without needing another sprite.
                half segment = pow(saturate(0.5h + 0.5h * sin(angle * 12.0h - _Time.y * 1.35h)), 9.0h);
                half travellingArc = pow(saturate(0.5h + 0.5h * sin(angle * 3.0h - _Time.y * 2.15h)), 7.0h);
                half rimEnergy = outerRim * (0.22h + segment * 0.48h + travellingArc * 0.62h);

                // Slow layered magic smoke inside the basin; edge fade keeps the mask perfectly round.
                half mistWaveA = 0.5h + 0.5h * sin(centered.x * 17.0h + centered.y * 8.0h - _Time.y * 1.05h);
                half mistWaveB = 0.5h + 0.5h * sin(centered.x * -11.0h + centered.y * 19.0h + _Time.y * 0.72h);
                half mistShape = smoothstep(0.62h, 0.94h, mistWaveA * 0.56h + mistWaveB * 0.44h);
                half mistMask = innerDisc * mistShape * smoothstep(0.08h, 0.52h, radial) * (1.0h - smoothstep(0.50h, 0.67h, radial));

                half discMask = 1.0h - smoothstep(0.68h, 0.76h, radial);
                half darkCarving = (1.0h - smoothstep(0.2h, 0.52h, luminance)) * discMask * cleanAlpha;

                half chargeFront = smoothstep(_Charge - 0.25h, _Charge + 0.04h, input.uv.x);
                half runeEnergy = darkCarving * (0.20h + _Pulse * 0.24h + chargeFront * _Charge * 1.35h);
                half activeRim = rimEnergy * (0.62h + _Pulse * 0.28h + _Charge * 1.15h) * _RimIntensity;
                half3 emission = _RuneColor.rgb * (runeEnergy + activeRim + innerRim * (0.18h + _Pulse * 0.12h)) * cleanAlpha;
                emission += _EdgeColor.rgb * coreRim * (0.72h + _Pulse * 0.35h + _Charge * 0.8h) * cleanAlpha;
                emission += _MistColor.rgb * mistMask * (0.12h + _Pulse * 0.08h + _Charge * 0.16h) * cleanAlpha;

                half silhouetteEdge = smoothstep(0.02h, 0.42h, source.a) * (1.0h - smoothstep(0.52h, 0.98h, source.a));
                emission += _EdgeColor.rgb * silhouetteEdge * (0.08h + _Charge * 0.18h);

                return half4(surface + emission, cleanAlpha);
            }
            ENDHLSL
        }
    }
}
