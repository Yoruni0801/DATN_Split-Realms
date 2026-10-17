Shader "Custom/Spine/SlotOverrideFireWeaponShaped"
{
    Properties
    {
        [NoScaleOffset]
        [MainTexture] _MainTex ("Atlas Texture", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0

        [HDR] _CoreColor ("Hot Core Color", Color) = (1.5, 1.2, 0.45, 1)
        [HDR] _FlameColor ("Main Flame Color", Color) = (1.2, 0.30, 0.03, 1)
        [HDR] _EdgeColor ("Outer Flame Color", Color) = (0.7, 0.05, 0.01, 1)
        [HDR] _GlowColor ("Glow Color", Color) = (1.0, 0.55, 0.08, 1)

        _Intensity ("Fire Intensity", Range(0, 5)) = 1.8
        _GlowIntensity ("Glow Intensity", Range(0, 4)) = 1.1

        _Speed ("Flame Speed", Range(0, 10)) = 3.0
        _NoiseScale ("Noise Scale", Range(0.1, 30)) = 6.0
        _DetailScale ("Detail Scale", Range(0.1, 50)) = 16.0

        _Distortion ("Edge Distortion (Pixels)", Range(0, 8)) = 1.8
        _Flicker ("Flicker Amount", Range(0, 1)) = 0.2

        _OuterFlameMin ("Outer Flame Min (Pixels)", Range(0, 12)) = 1.5
        _OuterFlameMax ("Outer Flame Max (Pixels)", Range(0, 24)) = 6.0
        _FlameHeight ("Upward Flame Height (Pixels)", Range(0, 24)) = 7.0
        _OuterOpacity ("Outer Flame Opacity", Range(0, 1)) = 0.75

        _EdgeWidth ("Hot Edge Width", Range(0.5, 8)) = 2.0
        _ThicknessProbe ("Thickness Probe (Pixels)", Range(1, 16)) = 4.0

        _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.015
        _Opacity ("Opacity", Range(0, 1)) = 1.0
        _SceneLightInfluence ("Scene Light Influence", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _MainTex_TexelSize;
                half4 _Color;
                half4 _RendererColor;
                float _EnableExternalAlpha;

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _FlameColor;
                half4 _EdgeColor;
                half4 _GlowColor;

                float _Intensity;
                float _GlowIntensity;

                float _Speed;
                float _NoiseScale;
                float _DetailScale;

                float _Distortion;
                float _Flicker;

                float _OuterFlameMin;
                float _OuterFlameMax;
                float _FlameHeight;
                float _OuterOpacity;

                float _EdgeWidth;
                float _ThicknessProbe;

                float _AlphaCutoff;
                float _Opacity;
                float _SceneLightInfluence;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half4 color : COLOR;

                UNITY_VERTEX_OUTPUT_STEREO
            };

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float noise21(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));

                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;

                v += noise21(p) * a; p = p * 2.03 + 17.13; a *= 0.5;
                v += noise21(p) * a; p = p * 2.01 + 11.71; a *= 0.5;
                v += noise21(p) * a;

                return v;
            }

            float sampleMask(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
            }

            float sampleDilated8(float2 uv, float2 radius)
            {
                float m = 0.0;

                m = max(m, sampleMask(uv));
                m = max(m, sampleMask(uv + float2( radius.x, 0.0)));
                m = max(m, sampleMask(uv + float2(-radius.x, 0.0)));
                m = max(m, sampleMask(uv + float2(0.0,  radius.y)));
                m = max(m, sampleMask(uv + float2(0.0, -radius.y)));

                float2 d = radius * 0.7071;
                m = max(m, sampleMask(uv + d));
                m = max(m, sampleMask(uv - d));
                m = max(m, sampleMask(uv + float2( d.x, -d.y)));
                m = max(m, sampleMask(uv + float2(-d.x,  d.y)));

                return m;
            }

            float estimateThickness(float2 uv, float2 probe)
            {
                float c  = sampleMask(uv);
                float x1 = sampleMask(uv + float2( probe.x, 0.0));
                float x2 = sampleMask(uv + float2(-probe.x, 0.0));
                float y1 = sampleMask(uv + float2(0.0,  probe.y));
                float y2 = sampleMask(uv + float2(0.0, -probe.y));

                float d1 = sampleMask(uv + probe);
                float d2 = sampleMask(uv - probe);
                float d3 = sampleMask(uv + float2( probe.x, -probe.y));
                float d4 = sampleMask(uv + float2(-probe.x,  probe.y));

                float sum = c + x1 + x2 + y1 + y2 + d1 + d2 + d3 + d4;

                // 0..1, vùng dày sẽ cao hơn
                return saturate(sum / 9.0);
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS);
                output.positionHCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 vertexColor = input.color * _Color * _RendererColor;
                float t = _Time.y * _Speed;

                float2 firePos = input.positionWS.xy * _NoiseScale;

                float largeNoise = fbm(firePos + float2(sin(t * 0.37) * 0.25, -t));
                float detailNoise = fbm(input.positionWS.xy * _DetailScale + float2(t * 0.35, -t * 1.75));
                float combinedNoise = saturate(largeNoise * 0.72 + detailNoise * 0.28);

                float2 distortionNoise;
                distortionNoise.x = noise21(firePos * 1.37 + float2(t, -t * 0.4)) - 0.5;
                distortionNoise.y = noise21(firePos * 1.91 + float2(-t * 0.45, t)) - 0.5;

                float2 distortedUV = input.uv + distortionNoise * _MainTex_TexelSize.xy * _Distortion;

                float coreMask = sampleMask(distortedUV);

                // ===== 1) Ước lượng vùng dày / mỏng của silhouette =====
                float2 thicknessProbe = _MainTex_TexelSize.xy * _ThicknessProbe;
                float thickness = estimateThickness(distortedUV, thicknessProbe);

                // Nhấn chênh lệch để chỗ dày nở rõ hơn, chỗ mỏng nở ít
                float shapeFactor = smoothstep(0.18, 0.85, thickness);

                // ===== 2) Bán kính lan ra biến thiên theo shape =====
                float localRadiusPx = lerp(_OuterFlameMin, _OuterFlameMax, shapeFactor);
                float2 localRadius = _MainTex_TexelSize.xy * localRadiusPx;

                // Dilation nhiều vòng để ra cảm giác lan xa/lan gần mềm hơn
                float dilateNear = sampleDilated8(distortedUV, localRadius * 0.45);
                float dilateMid  = sampleDilated8(distortedUV, localRadius * 0.75);
                float dilateFar  = sampleDilated8(distortedUV, localRadius);

                float expandedMask =
                    dilateNear * 0.55 +
                    dilateMid  * 0.30 +
                    dilateFar  * 0.15;

                expandedMask = saturate(expandedMask);

                // ===== 3) Kéo ngọn lửa hướng lên nhưng vẫn bám silhouette =====
                float wobble = (combinedNoise - 0.5) * localRadiusPx * 0.4;

                float2 upwardOffset = float2(
                    wobble * _MainTex_TexelSize.x,
                    _FlameHeight * _MainTex_TexelSize.y
                );

                float trail1 = sampleDilated8(distortedUV - upwardOffset * 0.35, localRadius * 0.35) * 0.75;
                float trail2 = sampleDilated8(distortedUV - upwardOffset * 0.70, localRadius * 0.25) * 0.45;
                float trail3 = sampleDilated8(distortedUV - upwardOffset * 1.00, localRadius * 0.18) * 0.25;

                // Chỉ cho trail mạnh hơn ở nơi shape đủ dày
                float trailMask = (trail1 + trail2 + trail3) * shapeFactor;

                float extendedMask = max(expandedMask, trailMask);

                // Outer chỉ là phần ngoài lõi
                float outerMask = saturate(extendedMask - coreMask);

                // Noise breakup để outer flame không đều tăm tắp
                float breakup = smoothstep(0.25, 0.85, combinedNoise + detailNoise * 0.35);
                outerMask *= breakup;
                outerMask *= _OuterOpacity;

                // ===== 4) Tìm mép để làm nóng rực phần rìa của lõi =====
                float2 edgeTexel = _MainTex_TexelSize.xy * _EdgeWidth;

                float neighborMin = 1.0;
                neighborMin = min(neighborMin, sampleMask(distortedUV + float2( edgeTexel.x, 0.0)));
                neighborMin = min(neighborMin, sampleMask(distortedUV + float2(-edgeTexel.x, 0.0)));
                neighborMin = min(neighborMin, sampleMask(distortedUV + float2(0.0,  edgeTexel.y)));
                neighborMin = min(neighborMin, sampleMask(distortedUV + float2(0.0, -edgeTexel.y)));

                float edge = saturate((coreMask - neighborMin) * 5.0);
                float hotCore = saturate((1.0 - edge) * 0.9 + combinedNoise * 0.25);

                half3 midColor = lerp(_EdgeColor.rgb, _FlameColor.rgb, combinedNoise);
                half3 coreColor = lerp(midColor, _CoreColor.rgb, hotCore);
                half3 outerColor = lerp(_EdgeColor.rgb, _FlameColor.rgb, combinedNoise);

                coreColor += _GlowColor.rgb * _GlowIntensity * (0.10 + combinedNoise * 0.22);
                outerColor += _GlowColor.rgb * _GlowIntensity * 0.16;

                coreColor *= _Intensity;
                outerColor *= _Intensity;

                float flicker = lerp(1.0 - _Flicker, 1.0, combinedNoise);

                half coreAlpha = coreMask * flicker * vertexColor.a * _Opacity;
                half outerAlpha = outerMask * vertexColor.a * _Opacity;

                half alpha = saturate(coreAlpha + outerAlpha * (1.0 - coreAlpha));
                clip(alpha - _AlphaCutoff);

                half3 finalRGB =
                    coreColor * coreAlpha +
                    outerColor * outerAlpha * (1.0 - coreAlpha);

                half4 selfLit = half4(finalRGB, alpha);
                return selfLit;
            }
            ENDHLSL

        Pass
        {
            Name "Universal2D"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY
            ENDHLSL
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" "Queue" = "Transparent" "RenderType" = "Transparent" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}