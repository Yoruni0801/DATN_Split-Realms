Shader "Custom/Element Fairy Recolor URP 2D"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}

        [Enum(Water,0,Wind,1,Fire,2,Earth,3)]
        _Element ("Element", Float) = 0

        _EffectStrength ("Effect Strength", Range(0, 2)) = 0.7
        _EffectSpeed ("Effect Speed", Range(0, 6)) = 1.5

        [Header(Water)]
        _DeepColor ("Water Dark", Color) = (0.004, 0.035, 0.16, 1)
        _MidColor ("Water Mid", Color) = (0.015, 0.28, 0.66, 1)
        _BrightColor ("Water Highlight", Color) = (0.10, 0.56, 0.82, 1)
        _FoamColor ("Water Foam", Color) = (0.22, 0.72, 0.90, 1)

        [Header(Wind Dark Forest Green)]
        _WindDarkColor (
            "Wind Dark",
            Color
        ) = (0.012, 0.075, 0.028, 1)

        _WindMidColor (
            "Wind Mid",
            Color
        ) = (0.030, 0.260, 0.105, 1)

        _WindBrightColor (
            "Wind Highlight",
            Color
        ) = (0.160, 0.480, 0.215, 1)

        _WindGustColor (
            "Wind Gust",
            Color
        ) = (0.390, 0.680, 0.260, 1)

        _WindContrast (
            "Wind Contrast",
            Range(0.5, 2)
        ) = 1.42

        _WindGlowStrength (
            "Wind Glow Strength",
            Range(0, 2)
        ) = 0.20

        [Header(Fire)]
        _FireDarkColor ("Fire Dark", Color) = (0.32, 0.02, 0.01, 1)
        _FireMidColor ("Fire Mid", Color) = (1, 0.22, 0.02, 1)
        _FireBrightColor ("Fire Highlight", Color) = (1, 0.92, 0.22, 1)
        _FireEmberColor ("Fire Ember", Color) = (1, 0.42, 0.02, 1)

        [Header(Lighting)]
        _RimColor ("Rim Color", Color) = (0.25, 0.85, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.55
        _Smoothness ("Glow Softness", Range(0, 1)) = 0.35
        _MeshShading ("3D Form Shading", Range(0, 1)) = 0.35

        _MinimumBrightness (
            "Minimum Form Brightness",
            Range(0, 1)
        ) = 0.55

        _SceneLightInfluence (
            "Scene Light Influence",
            Range(0, 1)
        ) = 0

        _SelfEmissionIntensity (
            "Self Emission Intensity",
            Range(0, 3)
        ) = 1.25

        _GlowColor (
            "Glow Color",
            Color
        ) = (1, 0.9, 0.55, 1)

        _GlowIntensity (
            "Glow Intensity",
            Range(0, 3)
        ) = 0.6

        _GlowPulseAmount (
            "Glow Pulse Amount",
            Range(0, 1)
        ) = 0.15

        _GlowPulseSpeed (
            "Glow Pulse Speed",
            Range(0, 8)
        ) = 2

        _OuterGlowSize (
            "Outer Glow Size",
            Range(0, 0.2)
        ) = 0.03

        [Header(Rendering)]
        [Toggle] _ZWrite ("Write Depth", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ZWrite [_ZWrite]
        Cull Off

        // ============================================================
        // UNIVERSAL 2D
        // ============================================================

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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/LightingUtility.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;

                half _Element;
                half _EffectStrength;
                half _EffectSpeed;

                half4 _DeepColor;
                half4 _MidColor;
                half4 _BrightColor;
                half4 _FoamColor;

                half4 _WindDarkColor;
                half4 _WindMidColor;
                half4 _WindBrightColor;
                half4 _WindGustColor;
                half _WindContrast;
                half _WindGlowStrength;

                half4 _FireDarkColor;
                half4 _FireMidColor;
                half4 _FireBrightColor;
                half4 _FireEmberColor;

                half4 _RimColor;
                half _RimPower;
                half _RimStrength;
                half _Smoothness;
                half _MeshShading;
                half _MinimumBrightness;

                half _SceneLightInfluence;
                half _SelfEmissionIntensity;

                half4 _GlowColor;
                half _GlowIntensity;
                half _GlowPulseAmount;
                half _GlowPulseSpeed;
                half _OuterGlowSize;

                half _ZWrite;
            CBUFFER_END

            #if USE_SHAPE_LIGHT_TYPE_0
            SHAPE_LIGHT(0)
            #endif

            #if USE_SHAPE_LIGHT_TYPE_1
            SHAPE_LIGHT(1)
            #endif

            #if USE_SHAPE_LIGHT_TYPE_2
            SHAPE_LIGHT(2)
            #endif

            #if USE_SHAPE_LIGHT_TYPE_3
            SHAPE_LIGHT(3)
            #endif

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half2 lightingUV : TEXCOORD3;
                half4 color : COLOR;

                UNITY_VERTEX_OUTPUT_STEREO
            };

            half IsEarthElement()
            {
                return step(2.5h, round(_Element));
            }

            half Wave(
                float2 uv,
                half speed,
                half scale
            )
            {
                return sin(
                    (
                        uv.x * scale +
                        uv.y * scale * 0.45h +
                        _Time.y * speed
                    ) * 6.28318h
                ) * 0.5h + 0.5h;
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                half element = round(_Element);
                float3 positionOS = input.positionOS;

                half windMask =
                    step(0.5h, element) *
                    step(element, 1.5h);

                half fireMask =
                    step(1.5h, element) *
                    step(element, 2.5h);

                half flutter = sin(
                    (
                        _Time.y * _EffectSpeed +
                        input.uv.y * 5.0h
                    ) * 6.28318h
                );

                positionOS.x +=
                    windMask *
                    flutter *
                    0.015h *
                    _EffectStrength;

                positionOS.y +=
                    fireMask *
                    abs(flutter) *
                    0.01h *
                    _EffectStrength;

                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(positionOS);

                VertexNormalInputs normalInputs =
                    GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;

                output.normalWS =
                    NormalizeNormalPerVertex(
                        normalInputs.normalWS
                    );

                output.uv = TRANSFORM_TEX(
                    input.uv,
                    _BaseMap
                );

                output.color = input.color;

                output.lightingUV = half2(
                    ComputeScreenPos(
                        output.positionCS /
                        output.positionCS.w
                    ).xy
                );

                return output;
            }

            half3 RemapPalette(
                half luminance,
                half4 darkColor,
                half4 midColor,
                half4 brightColor,
                half4 accentColor,
                half accentMask
            )
            {
                half midMask = smoothstep(
                    0.08h,
                    0.58h,
                    luminance
                );

                half brightMask = smoothstep(
                    0.55h,
                    0.92h,
                    luminance
                );

                half3 color = lerp(
                    darkColor.rgb,
                    midColor.rgb,
                    midMask
                );

                color = lerp(
                    color,
                    brightColor.rgb,
                    brightMask
                );

                color +=
                    accentColor.rgb *
                    accentMask;

                return color;
            }

            half3 BuildWindColor(
                half luminance,
                half highlight,
                half fineWave
            )
            {
                // Giữ phần lớn thân ở xanh rừng đậm.
                half midMask = smoothstep(
                    0.12h,
                    0.64h,
                    luminance
                );

                // Chỉ vùng rất sáng của texture mới nhận màu highlight.
                half brightMask = smoothstep(
                    0.72h,
                    0.97h,
                    luminance
                );

                half3 windColor = lerp(
                    _WindDarkColor.rgb,
                    _WindMidColor.rgb,
                    midMask
                );

                windColor = lerp(
                    windColor,
                    _WindBrightColor.rgb,
                    brightMask * 0.70h
                );

                // Điểm nhấn xanh non rất nhẹ, tránh phủ sáng toàn thân.
                half gustPattern = saturate(
                    fineWave * 0.58h +
                    highlight * 0.35h
                );

                gustPattern = pow(
                    gustPattern,
                    3.0h
                );

                windColor +=
                    _WindGustColor.rgb *
                    gustPattern *
                    0.045h *
                    _EffectStrength;

                windColor =
                    (windColor - 0.5h) *
                    _WindContrast +
                    0.5h;

                // Giảm riêng độ sáng của hệ Gió.
                windColor *= 0.76h;

                return saturate(windColor);
            }

            half3 BuildElementColor(
                half3 source,
                float2 uv,
                out half3 rimColor,
                out half extraGlow
            )
            {
                half luminance = dot(
                    source,
                    half3(0.299h, 0.587h, 0.114h)
                );

                half element = round(_Element);

                half wave = Wave(
                    uv,
                    _EffectSpeed,
                    3.0h
                );

                half fineWave = Wave(
                    uv.yx + 0.37h,
                    _EffectSpeed * 1.7h,
                    9.0h
                );

                half highlight = saturate(
                    pow(luminance, 4.0h)
                );

                half3 water = RemapPalette(
                    luminance,
                    _DeepColor,
                    _MidColor,
                    _BrightColor,
                    _FoamColor,
                    highlight * 0.20h +
                    wave *
                    0.04h *
                    _EffectStrength
                );

                half3 wind = BuildWindColor(
                    luminance,
                    highlight,
                    fineWave
                );

                half3 fire = RemapPalette(
                    luminance,
                    _FireDarkColor,
                    _FireMidColor,
                    _FireBrightColor,
                    _FireEmberColor,
                    (
                        wave * 0.35h +
                        highlight * 0.45h
                    ) *
                    _EffectStrength
                );

                rimColor = _RimColor.rgb;
                extraGlow = 0.0h;

                half3 color = water;

                if (
                    element > 0.5h &&
                    element < 1.5h
                )
                {
                    color = wind;

                    // Viền xanh rừng tối giúp tách khỏi aura sáng.
                    rimColor = lerp(
                        _WindDarkColor.rgb,
                        _WindMidColor.rgb,
                        0.28h
                    );

                    extraGlow =
                        (
                            0.004h +
                            fineWave * 0.010h
                        ) *
                        _WindGlowStrength *
                        _EffectStrength;
                }
                else if (
                    element > 1.5h &&
                    element < 2.5h
                )
                {
                    color = fire;
                    rimColor = _FireBrightColor.rgb;

                    extraGlow =
                        (
                            wave * 0.22h +
                            highlight * 0.18h
                        ) *
                        _EffectStrength;
                }

                return color;
            }

            half4 BuildDefaultEarthColor(
                half4 source,
                float2 uv,
                half2 lightingUV
            )
            {
                SurfaceData2D earthSurfaceData;
                InputData2D earthInputData;

                InitializeSurfaceData(
                    source.rgb,
                    source.a,
                    half4(1, 1, 1, 1),
                    earthSurfaceData
                );

                InitializeInputData(
                    uv,
                    lightingUV,
                    earthInputData
                );

                half4 litEarth =
                    CombinedShapeLightShared(
                        earthSurfaceData,
                        earthInputData
                    );

                return lerp(
                    source,
                    litEarth,
                    _SceneLightInfluence
                );
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(
                    input
                );

                half4 source = SAMPLE_TEXTURE2D(
                    _BaseMap,
                    sampler_BaseMap,
                    input.uv
                );

                source *= input.color;

                if (source.a <= 0.001h)
                {
                    discard;
                }

                // Earth giữ nguyên texture và màu của Spine/GameObject.
                if (IsEarthElement() > 0.5h)
                {
                    return BuildDefaultEarthColor(
                        source,
                        input.uv,
                        input.lightingUV
                    );
                }

                half3 normalWS =
                    normalize(input.normalWS);

                half3 viewDirWS = SafeNormalize(
                    GetWorldSpaceViewDir(
                        input.positionWS
                    )
                );

                half3 rimPalette;
                half extraGlow;

                half3 elementColor =
                    BuildElementColor(
                        source.rgb,
                        input.uv,
                        rimPalette,
                        extraGlow
                    );

                const half3 formLightDirection =
                    half3(-0.35h, 0.65h, 0.68h);

                half formNdotL = saturate(
                    dot(
                        normalWS,
                        normalize(formLightDirection)
                    )
                );

                half formLight = lerp(
                    1.0h,
                    lerp(
                        _MinimumBrightness,
                        1.0h,
                        formNdotL
                    ),
                    _MeshShading
                );

                half rim = pow(
                    1.0h -
                    saturate(
                        dot(normalWS, viewDirWS)
                    ),
                    _RimPower
                ) * _RimStrength;

                half3 color =
                    elementColor *
                    formLight;

                color +=
                    rimPalette *
                    rim;

                color = lerp(
                    color,
                    color +
                    rimPalette *
                    (0.2h + extraGlow),
                    _Smoothness
                );

                color +=
                    rimPalette *
                    extraGlow;

                half pulse =
                    1.0h +
                    sin(
                        _Time.y *
                        _GlowPulseSpeed
                    ) *
                    _GlowPulseAmount;

                half elementMask = round(_Element);
                half isWind =
                    step(0.5h, elementMask) *
                    step(elementMask, 1.5h);

                // Hệ Gió giữ màu tối, không nhân emission quá mạnh.
                half effectiveEmission = lerp(
                    _SelfEmissionIntensity,
                    min(_SelfEmissionIntensity, 0.82h),
                    isWind
                );

                half glowMultiplier = lerp(
                    1.0h,
                    0.24h,
                    isWind
                );

                half3 emissionColor = lerp(
                    lerp(
                        rimPalette,
                        _GlowColor.rgb,
                        0.35h
                    ),
                    _WindMidColor.rgb,
                    isWind
                );

                half3 selfLitColor =
                    color *
                    effectiveEmission;

                selfLitColor +=
                    emissionColor *
                    _GlowIntensity *
                    glowMultiplier *
                    pulse *
                    saturate(
                        rim +
                        extraGlow +
                        0.08h
                    );

                SurfaceData2D surfaceData;
                InputData2D inputData;

                InitializeSurfaceData(
                    color,
                    source.a,
                    half4(1, 1, 1, 1),
                    surfaceData
                );

                InitializeInputData(
                    input.uv,
                    input.lightingUV,
                    inputData
                );

                half4 litColor =
                    CombinedShapeLightShared(
                        surfaceData,
                        inputData
                    );

                half4 selfLit = half4(
                    selfLitColor,
                    source.a
                );

                return lerp(
                    selfLit,
                    litColor,
                    _SceneLightInfluence
                );
            }

            ENDHLSL
        }

        // ============================================================
        // SELF GLOW
        // ============================================================

        Pass
        {
            Name "SelfGlow"
            Tags { "LightMode" = "Universal2D" }

            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM

            #pragma target 3.0
            #pragma vertex glowVert
            #pragma fragment glowFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;

                half _Element;
                half _EffectStrength;
                half _EffectSpeed;

                half4 _DeepColor;
                half4 _MidColor;
                half4 _BrightColor;
                half4 _FoamColor;

                half4 _WindDarkColor;
                half4 _WindMidColor;
                half4 _WindBrightColor;
                half4 _WindGustColor;
                half _WindContrast;
                half _WindGlowStrength;

                half4 _FireDarkColor;
                half4 _FireMidColor;
                half4 _FireBrightColor;
                half4 _FireEmberColor;

                half4 _RimColor;
                half _RimPower;
                half _RimStrength;
                half _Smoothness;
                half _MeshShading;
                half _MinimumBrightness;

                half _SceneLightInfluence;
                half _SelfEmissionIntensity;

                half4 _GlowColor;
                half _GlowIntensity;
                half _GlowPulseAmount;
                half _GlowPulseSpeed;
                half _OuterGlowSize;

                half _ZWrite;
            CBUFFER_END

            struct GlowAttributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct GlowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;

                UNITY_VERTEX_OUTPUT_STEREO
            };

            half GlowWave(
                float2 uv,
                half speed,
                half scale
            )
            {
                return sin(
                    (
                        uv.x * scale +
                        uv.y * scale * 0.45h +
                        _Time.y * speed
                    ) * 6.28318h
                ) * 0.5h + 0.5h;
            }

            half3 ResolveElementGlowColor(
                float2 uv
            )
            {
                half element = round(_Element);

                half wave = GlowWave(
                    uv,
                    _EffectSpeed,
                    3.0h
                );

                half fineWave = GlowWave(
                    uv.yx + 0.37h,
                    _EffectSpeed * 1.7h,
                    9.0h
                );

                half3 color = lerp(
                    _MidColor.rgb,
                    _BrightColor.rgb,
                    wave
                );

                half globalGlowBlend = 0.25h;

                if (element < 0.5h)
                {
                    // Glow nước dùng xanh lam đậm, tránh bị cháy sáng.
                    color = lerp(
                        _DeepColor.rgb,
                        _MidColor.rgb,
                        0.22h + wave * 0.46h
                    );

                    globalGlowBlend = 0.04h;
                }
                else if (
                    element > 0.5h &&
                    element < 1.5h
                )
                {
                    // Glow hệ Gió dùng xanh rừng đậm và giữ cường độ thấp.
                    half3 forestGlow = lerp(
                        _WindDarkColor.rgb,
                        _WindMidColor.rgb,
                        0.18h + fineWave * 0.42h
                    );

                    half gustAmount =
                        0.025h +
                        wave * 0.045h;

                    color = lerp(
                        forestGlow,
                        _WindGustColor.rgb,
                        gustAmount
                    );

                    // Không pha với Glow Color chung vì màu đó quá sáng.
                    globalGlowBlend = 0.0h;
                }
                else if (
                    element > 1.5h &&
                    element < 2.5h
                )
                {
                    color = lerp(
                        _FireMidColor.rgb,
                        _FireBrightColor.rgb,
                        wave
                    );
                }

                return lerp(
                    color,
                    _GlowColor.rgb,
                    globalGlowBlend
                );
            }

            GlowVaryings glowVert(
                GlowAttributes input
            )
            {
                GlowVaryings output =
                    (GlowVaryings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(
                    output
                );

                half element = round(_Element);
                float3 positionOS = input.positionOS;

                half windMask =
                    step(0.5h, element) *
                    step(element, 1.5h);

                half fireMask =
                    step(1.5h, element) *
                    step(element, 2.5h);

                half flutter = sin(
                    (
                        _Time.y * _EffectSpeed +
                        input.uv.y * 5.0h
                    ) * 6.28318h
                );

                positionOS.x +=
                    windMask *
                    flutter *
                    0.015h *
                    _EffectStrength;

                positionOS.y +=
                    fireMask *
                    abs(flutter) *
                    0.01h *
                    _EffectStrength;

                positionOS +=
                    input.normalOS *
                    _OuterGlowSize;

                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(positionOS);

                output.positionCS =
                    positionInputs.positionCS;

                output.uv = TRANSFORM_TEX(
                    input.uv,
                    _BaseMap
                );

                output.color = input.color;

                return output;
            }

            half4 glowFrag(
                GlowVaryings input
            ) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(
                    input
                );

                half element = round(_Element);

                // Earth không dùng glow để giữ màu gốc.
                if (element > 2.5h)
                {
                    return half4(0, 0, 0, 0);
                }

                half4 source = SAMPLE_TEXTURE2D(
                    _BaseMap,
                    sampler_BaseMap,
                    input.uv
                );

                source *= input.color;

                if (source.a <= 0.001h)
                {
                    discard;
                }

                half pulse =
                    1.0h +
                    sin(
                        _Time.y *
                        _GlowPulseSpeed
                    ) *
                    _GlowPulseAmount;

                half3 glowColor =
                    ResolveElementGlowColor(
                        input.uv
                    );

                half alpha =
                    source.a *
                    _GlowIntensity *
                    pulse *
                    saturate(
                        _OuterGlowSize *
                        18.0h
                    );

                half isWind =
                    step(0.5h, element) *
                    step(element, 1.5h);

                // Hạn chế glow gió phủ trắng toàn bộ sprite.
                alpha *= lerp(
                    1.0h,
                    _WindGlowStrength,
                    isWind
                );

                return half4(
                    glowColor,
                    alpha
                );
            }

            ENDHLSL
        }

        // ============================================================
        // UNIVERSAL FORWARD FALLBACK
        // ============================================================

        Pass
        {
            Name "UniversalForwardFallback"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM

            #pragma target 3.0
            #pragma vertex fallbackVert
            #pragma fragment fallbackFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;

                half _Element;
                half _EffectStrength;
                half _EffectSpeed;

                half4 _DeepColor;
                half4 _MidColor;
                half4 _BrightColor;
                half4 _FoamColor;

                half4 _WindDarkColor;
                half4 _WindMidColor;
                half4 _WindBrightColor;
                half4 _WindGustColor;
                half _WindContrast;
                half _WindGlowStrength;

                half4 _FireDarkColor;
                half4 _FireMidColor;
                half4 _FireBrightColor;
                half4 _FireEmberColor;

                half4 _RimColor;
                half _RimPower;
                half _RimStrength;
                half _Smoothness;
                half _MeshShading;
                half _MinimumBrightness;

                half _SceneLightInfluence;
                half _SelfEmissionIntensity;

                half4 _GlowColor;
                half _GlowIntensity;
                half _GlowPulseAmount;
                half _GlowPulseSpeed;
                half _OuterGlowSize;

                half _ZWrite;
            CBUFFER_END

            struct FallbackAttributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct FallbackVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half4 color : COLOR;

                UNITY_VERTEX_OUTPUT_STEREO
            };

            half FallbackWave(
                float2 uv,
                half speed,
                half scale
            )
            {
                return sin(
                    (
                        uv.x * scale +
                        uv.y * scale * 0.45h +
                        _Time.y * speed
                    ) * 6.28318h
                ) * 0.5h + 0.5h;
            }

            FallbackVaryings fallbackVert(
                FallbackAttributes input
            )
            {
                FallbackVaryings output =
                    (FallbackVaryings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(
                    output
                );

                half element = round(_Element);
                float3 positionOS = input.positionOS;

                half windMask =
                    step(0.5h, element) *
                    step(element, 1.5h);

                half fireMask =
                    step(1.5h, element) *
                    step(element, 2.5h);

                half flutter = sin(
                    (
                        _Time.y * _EffectSpeed +
                        input.uv.y * 5.0h
                    ) * 6.28318h
                );

                positionOS.x +=
                    windMask *
                    flutter *
                    0.015h *
                    _EffectStrength;

                positionOS.y +=
                    fireMask *
                    abs(flutter) *
                    0.01h *
                    _EffectStrength;

                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(positionOS);

                VertexNormalInputs normalInputs =
                    GetVertexNormalInputs(input.normalOS);

                output.positionCS =
                    positionInputs.positionCS;

                output.positionWS =
                    positionInputs.positionWS;

                output.normalWS =
                    NormalizeNormalPerVertex(
                        normalInputs.normalWS
                    );

                output.uv = TRANSFORM_TEX(
                    input.uv,
                    _BaseMap
                );

                output.color = input.color;

                return output;
            }

            half3 FallbackRemapPalette(
                half luminance,
                half4 darkColor,
                half4 midColor,
                half4 brightColor,
                half4 accentColor,
                half accentMask
            )
            {
                half midMask = smoothstep(
                    0.08h,
                    0.58h,
                    luminance
                );

                half brightMask = smoothstep(
                    0.55h,
                    0.92h,
                    luminance
                );

                half3 color = lerp(
                    darkColor.rgb,
                    midColor.rgb,
                    midMask
                );

                color = lerp(
                    color,
                    brightColor.rgb,
                    brightMask
                );

                color +=
                    accentColor.rgb *
                    accentMask;

                return color;
            }

            half3 FallbackBuildWindColor(
                half luminance,
                half highlight,
                half fineWave
            )
            {
                // Giữ phần lớn thân ở xanh rừng đậm.
                half midMask = smoothstep(
                    0.12h,
                    0.64h,
                    luminance
                );

                // Chỉ vùng rất sáng của texture mới nhận màu highlight.
                half brightMask = smoothstep(
                    0.72h,
                    0.97h,
                    luminance
                );

                half3 windColor = lerp(
                    _WindDarkColor.rgb,
                    _WindMidColor.rgb,
                    midMask
                );

                windColor = lerp(
                    windColor,
                    _WindBrightColor.rgb,
                    brightMask * 0.70h
                );

                // Điểm nhấn xanh non rất nhẹ, tránh phủ sáng toàn thân.
                half gustPattern = saturate(
                    fineWave * 0.58h +
                    highlight * 0.35h
                );

                gustPattern = pow(
                    gustPattern,
                    3.0h
                );

                windColor +=
                    _WindGustColor.rgb *
                    gustPattern *
                    0.045h *
                    _EffectStrength;

                windColor =
                    (windColor - 0.5h) *
                    _WindContrast +
                    0.5h;

                // Giảm riêng độ sáng của hệ Gió.
                windColor *= 0.76h;

                return saturate(windColor);
            }

            half3 FallbackBuildElementColor(
                half3 source,
                float2 uv,
                out half3 rimColor,
                out half extraGlow
            )
            {
                half luminance = dot(
                    source,
                    half3(0.299h, 0.587h, 0.114h)
                );

                half element = round(_Element);

                half wave = FallbackWave(
                    uv,
                    _EffectSpeed,
                    3.0h
                );

                half fineWave = FallbackWave(
                    uv.yx + 0.37h,
                    _EffectSpeed * 1.7h,
                    9.0h
                );

                half highlight = saturate(
                    pow(luminance, 4.0h)
                );

                half3 water = FallbackRemapPalette(
                    luminance,
                    _DeepColor,
                    _MidColor,
                    _BrightColor,
                    _FoamColor,
                    highlight * 0.20h +
                    wave *
                    0.04h *
                    _EffectStrength
                );

                half3 wind = FallbackBuildWindColor(
                    luminance,
                    highlight,
                    fineWave
                );

                half3 fire = FallbackRemapPalette(
                    luminance,
                    _FireDarkColor,
                    _FireMidColor,
                    _FireBrightColor,
                    _FireEmberColor,
                    (
                        wave * 0.35h +
                        highlight * 0.45h
                    ) *
                    _EffectStrength
                );

                rimColor = _RimColor.rgb;
                extraGlow = 0.0h;

                half3 color = water;

                if (
                    element > 0.5h &&
                    element < 1.5h
                )
                {
                    color = wind;
                    rimColor = lerp(
                        _WindDarkColor.rgb,
                        _WindMidColor.rgb,
                        0.28h
                    );

                    extraGlow =
                        (
                            0.004h +
                            fineWave * 0.010h
                        ) *
                        _WindGlowStrength *
                        _EffectStrength;
                }
                else if (
                    element > 1.5h &&
                    element < 2.5h
                )
                {
                    color = fire;
                    rimColor = _FireBrightColor.rgb;

                    extraGlow =
                        (
                            wave * 0.22h +
                            highlight * 0.18h
                        ) *
                        _EffectStrength;
                }

                return color;
            }

            half4 fallbackFrag(
                FallbackVaryings input
            ) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(
                    input
                );

                half4 source = SAMPLE_TEXTURE2D(
                    _BaseMap,
                    sampler_BaseMap,
                    input.uv
                );

                source *= input.color;

                if (source.a <= 0.001h)
                {
                    discard;
                }

                // Earth giữ màu nguyên bản.
                if (round(_Element) > 2.5h)
                {
                    return source;
                }

                half3 normalWS =
                    normalize(input.normalWS);

                half3 viewDirWS = SafeNormalize(
                    GetWorldSpaceViewDir(
                        input.positionWS
                    )
                );

                half3 rimPalette;
                half extraGlow;

                half3 elementColor =
                    FallbackBuildElementColor(
                        source.rgb,
                        input.uv,
                        rimPalette,
                        extraGlow
                    );

                const half3 formLightDirection =
                    half3(-0.35h, 0.65h, 0.68h);

                half formNdotL = saturate(
                    dot(
                        normalWS,
                        normalize(formLightDirection)
                    )
                );

                half formLight = lerp(
                    1.0h,
                    lerp(
                        _MinimumBrightness,
                        1.0h,
                        formNdotL
                    ),
                    _MeshShading
                );

                half rim = pow(
                    1.0h -
                    saturate(
                        dot(normalWS, viewDirWS)
                    ),
                    _RimPower
                ) * _RimStrength;

                half3 color =
                    elementColor *
                    formLight;

                color +=
                    rimPalette *
                    rim;

                color = lerp(
                    color,
                    color +
                    rimPalette *
                    (0.2h + extraGlow),
                    _Smoothness
                );

                color +=
                    rimPalette *
                    extraGlow;

                half pulse =
                    1.0h +
                    sin(
                        _Time.y *
                        _GlowPulseSpeed
                    ) *
                    _GlowPulseAmount;

                half elementMask = round(_Element);
                half isWind =
                    step(0.5h, elementMask) *
                    step(elementMask, 1.5h);

                half effectiveEmission = lerp(
                    _SelfEmissionIntensity,
                    min(_SelfEmissionIntensity, 0.82h),
                    isWind
                );

                half glowMultiplier = lerp(
                    1.0h,
                    0.24h,
                    isWind
                );

                half3 emissionColor = lerp(
                    lerp(
                        rimPalette,
                        _GlowColor.rgb,
                        0.35h
                    ),
                    _WindMidColor.rgb,
                    isWind
                );

                color *= effectiveEmission;

                color +=
                    emissionColor *
                    _GlowIntensity *
                    glowMultiplier *
                    pulse *
                    saturate(
                        rim +
                        extraGlow +
                        0.08h
                    );

                return half4(
                    color,
                    source.a
                );
            }

            ENDHLSL
        }
    }
}