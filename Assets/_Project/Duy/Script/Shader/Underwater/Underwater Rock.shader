Shader "Custom/2D/Underwater Rock"
{
    Properties
    {
        [PerRendererData]
        _MainTex ("Sprite Texture", 2D) = "white" {}

        _Color ("Sprite Tint", Color) = (1,1,1,1)

        [PerRendererData]
        _AlphaTex ("External Alpha", 2D) = "white" {}

        [PerRendererData]
        _EnableExternalAlpha ("Enable External Alpha", Float) = 0

        [Header(Underwater Detection)]
        [Toggle]
        _UseWaterSurface ("Use Water Surface", Float) = 0

        _WaterSurfaceY ("Water Surface World Y", Float) = 0

        _SurfaceTransition
        (
            "Surface Transition",
            Range(0.001, 0.3)
        ) = 0.035

        _UnderwaterAmount
        (
            "Underwater Amount",
            Range(0, 1)
        ) = 1

        _ManualDepth
        (
            "Manual Depth",
            Range(0, 1)
        ) = 0.4

        _DepthRange
        (
            "Automatic Depth Range",
            Range(0.1, 20)
        ) = 5

        [Header(Underwater Color)]
        _ShallowTint
        (
            "Shallow Tint",
            Color
        ) = (0.55, 0.86, 0.84, 1)

        _DeepTint
        (
            "Deep Tint",
            Color
        ) = (0.14, 0.40, 0.50, 1)

        _TintStrength
        (
            "Tint Strength",
            Range(0, 1)
        ) = 0.38

        _OriginalColorPreservation
        (
            "Preserve Original Color",
            Range(0, 1)
        ) = 0.42

        _DepthDarkness
        (
            "Depth Darkness",
            Range(0, 1)
        ) = 0.38

        _Saturation
        (
            "Underwater Saturation",
            Range(0, 2)
        ) = 0.82

        _Brightness
        (
            "Brightness",
            Range(0.5, 1.5)
        ) = 0.95

        _Contrast
        (
            "Contrast",
            Range(0.5, 2)
        ) = 1.13

        [Header(Rock Surface Detail)]
        _CreviceDarkness
        (
            "Crevice Darkness",
            Range(0, 0.5)
        ) = 0.12

        _SurfaceVariation
        (
            "Surface Variation",
            Range(0, 0.3)
        ) = 0.05

        _SurfaceVariationScale
        (
            "Surface Variation Scale",
            Range(0.1, 30)
        ) = 5

        [Header(Sediment)]
        _SedimentColor
        (
            "Sediment Color",
            Color
        ) = (0.35, 0.52, 0.42, 1)

        _SedimentScale
        (
            "Sediment Scale",
            Range(0.1, 30)
        ) = 4

        _SedimentAmount
        (
            "Sediment Amount",
            Range(0, 1)
        ) = 0.12

        _SedimentContrast
        (
            "Sediment Contrast",
            Range(0.1, 10)
        ) = 3

        [Header(Caustics)]
        _CausticsColor
        (
            "Caustics Color",
            Color
        ) = (0.62, 1, 0.88, 1)

        _CausticsScale
        (
            "Caustics Scale",
            Range(0.1, 30)
        ) = 4.5

        _CausticsSpeed
        (
            "Caustics Speed",
            Range(0, 5)
        ) = 0.48

        _CausticsIntensity
        (
            "Caustics Intensity",
            Range(0, 2)
        ) = 0.18

        _CausticsSharpness
        (
            "Caustics Sharpness",
            Range(1, 12)
        ) = 5

        _CausticsDepthFade
        (
            "Caustics Depth Fade",
            Range(0, 1)
        ) = 0.72

        [Header(Wet Waterline)]
        _WetTint
        (
            "Wet Band Tint",
            Color
        ) = (0.12, 0.30, 0.34, 1)

        _WetBandWidth
        (
            "Wet Band Width",
            Range(0.001, 0.5)
        ) = 0.09

        _WetBandStrength
        (
            "Wet Band Strength",
            Range(0, 1)
        ) = 0.45

        _SurfaceLineColor
        (
            "Surface Line Color",
            Color
        ) = (0.72, 1, 0.92, 1)

        _SurfaceLineWidth
        (
            "Surface Line Width",
            Range(0.001, 0.2)
        ) = 0.025

        _SurfaceLineIntensity
        (
            "Surface Line Intensity",
            Range(0, 2)
        ) = 0.25
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off

        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM

            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _AlphaTex;

            float4 _MainTex_ST;

            fixed4 _Color;

            float _EnableExternalAlpha;

            float _UseWaterSurface;
            float _WaterSurfaceY;
            float _SurfaceTransition;
            float _UnderwaterAmount;
            float _ManualDepth;
            float _DepthRange;

            fixed4 _ShallowTint;
            fixed4 _DeepTint;

            float _TintStrength;
            float _OriginalColorPreservation;
            float _DepthDarkness;
            float _Saturation;
            float _Brightness;
            float _Contrast;

            float _CreviceDarkness;
            float _SurfaceVariation;
            float _SurfaceVariationScale;

            fixed4 _SedimentColor;
            float _SedimentScale;
            float _SedimentAmount;
            float _SedimentContrast;

            fixed4 _CausticsColor;
            float _CausticsScale;
            float _CausticsSpeed;
            float _CausticsIntensity;
            float _CausticsSharpness;
            float _CausticsDepthFade;

            fixed4 _WetTint;
            float _WetBandWidth;
            float _WetBandStrength;

            fixed4 _SurfaceLineColor;
            float _SurfaceLineWidth;
            float _SurfaceLineIntensity;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 worldPosition : TEXCOORD1;
                fixed4 color : COLOR;
            };

            fixed4 SampleSpriteTexture(float2 uv)
            {
                fixed4 spriteColor = tex2D(
                    _MainTex,
                    uv
                );

                #if ETC1_EXTERNAL_ALPHA

                    fixed4 externalAlpha = tex2D(
                        _AlphaTex,
                        uv
                    );

                    spriteColor.a = lerp(
                        spriteColor.a,
                        externalAlpha.r,
                        _EnableExternalAlpha
                    );

                #endif

                return spriteColor;
            }

            float Hash21(float2 position)
            {
                position = frac(
                    position *
                    float2(
                        123.34,
                        456.21
                    )
                );

                position += dot(
                    position,
                    position + 45.32
                );

                return frac(
                    position.x *
                    position.y
                );
            }

            float ValueNoise(float2 position)
            {
                float2 cellID =
                    floor(position);

                float2 localPosition =
                    frac(position);

                localPosition =
                    localPosition *
                    localPosition *
                    (
                        3.0 -
                        2.0 *
                        localPosition
                    );

                float valueA =
                    Hash21(cellID);

                float valueB =
                    Hash21(
                        cellID +
                        float2(1.0, 0.0)
                    );

                float valueC =
                    Hash21(
                        cellID +
                        float2(0.0, 1.0)
                    );

                float valueD =
                    Hash21(
                        cellID +
                        float2(1.0, 1.0)
                    );

                float lowerValue =
                    lerp(
                        valueA,
                        valueB,
                        localPosition.x
                    );

                float upperValue =
                    lerp(
                        valueC,
                        valueD,
                        localPosition.x
                    );

                return lerp(
                    lowerValue,
                    upperValue,
                    localPosition.y
                );
            }

            float FBM(float2 position)
            {
                float result = 0.0;
                float amplitude = 0.5;

                result +=
                    ValueNoise(position) *
                    amplitude;

                position =
                    position * 2.03 +
                    float2(17.1, 9.2);

                amplitude *= 0.5;

                result +=
                    ValueNoise(position) *
                    amplitude;

                position =
                    position * 2.01 +
                    float2(8.3, 19.7);

                amplitude *= 0.5;

                result +=
                    ValueNoise(position) *
                    amplitude;

                position =
                    position * 2.04 +
                    float2(13.5, 4.8);

                amplitude *= 0.5;

                result +=
                    ValueNoise(position) *
                    amplitude;

                return result;
            }

            float3 ApplySaturation(
                float3 inputColor,
                float saturationValue
            )
            {
                float luminance = dot(
                    inputColor,
                    float3(
                        0.2126,
                        0.7152,
                        0.0722
                    )
                );

                return lerp(
                    float3(
                        luminance,
                        luminance,
                        luminance
                    ),
                    inputColor,
                    saturationValue
                );
            }

            float3 ApplyContrast(
                float3 inputColor,
                float contrastValue
            )
            {
                return
                    (
                        inputColor -
                        0.5
                    ) *
                    contrastValue +
                    0.5;
            }

            float CalculateUnderwaterMask(
                float worldY
            )
            {
                float signedDepth =
                    _WaterSurfaceY -
                    worldY;

                float transitionWidth = max(
                    _SurfaceTransition,
                    0.0001
                );

                float surfaceMask =
                    smoothstep(
                        -transitionWidth,
                        transitionWidth,
                        signedDepth
                    );

                float underwaterMask =
                    lerp(
                        1.0,
                        surfaceMask,
                        saturate(
                            _UseWaterSurface
                        )
                    );

                return
                    saturate(
                        underwaterMask
                    ) *
                    saturate(
                        _UnderwaterAmount
                    );
            }

            float CalculateDepth01(float worldY)
            {
                float signedDepth = max(
                    _WaterSurfaceY -
                    worldY,
                    0.0
                );

                float automaticDepth =
                    saturate(
                        signedDepth /
                        max(
                            _DepthRange,
                            0.0001
                        )
                    );

                return lerp(
                    saturate(
                        _ManualDepth
                    ),
                    automaticDepth,
                    saturate(
                        _UseWaterSurface
                    )
                );
            }

            float CalculateCaustics(
                float2 worldPosition,
                float timeValue
            )
            {
                float2 position =
                    worldPosition *
                    _CausticsScale;

                position += float2(
                    timeValue *
                    _CausticsSpeed,

                    -timeValue *
                    _CausticsSpeed *
                    0.72
                );

                float waveA = sin(
                    position.x +
                    sin(
                        position.y * 1.31 +
                        timeValue * 0.65
                    )
                );

                float waveB = sin(
                    position.y * 1.73 +
                    sin(
                        position.x * 0.84 -
                        timeValue * 0.87
                    )
                );

                float waveC = sin(
                    (
                        position.x +
                        position.y
                    ) *
                    0.71 -
                    timeValue *
                    0.53 +
                    sin(
                        position.x *
                        0.31
                    )
                );

                float waveD = sin(
                    (
                        position.x -
                        position.y
                    ) *
                    1.17 +
                    timeValue *
                    0.41
                );

                float lineDistance = abs(
                    waveA +
                    waveB +
                    waveC * 0.7 +
                    waveD * 0.35
                );

                lineDistance /= 3.05;

                float causticValue =
                    1.0 -
                    saturate(
                        lineDistance
                    );

                return pow(
                    causticValue,
                    _CausticsSharpness
                );
            }

            v2f vert(appdata v)
            {
                v2f o;

                /*
                 * Không dịch vertex.
                 * Đá luôn giữ nguyên hình dạng.
                 */
                o.vertex =
                    UnityObjectToClipPos(
                        v.vertex
                    );

                o.uv =
                    TRANSFORM_TEX(
                        v.uv,
                        _MainTex
                    );

                o.worldPosition =
                    mul(
                        unity_ObjectToWorld,
                        v.vertex
                    ).xy;

                o.color =
                    v.color;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float timeValue =
                    _Time.y;

                fixed4 spriteColor =
                    SampleSpriteTexture(
                        i.uv
                    );

                spriteColor *=
                    i.color *
                    _Color;

                clip(
                    spriteColor.a -
                    0.001
                );

                float underwaterMask =
                    CalculateUnderwaterMask(
                        i.worldPosition.y
                    );

                float depth01 =
                    CalculateDepth01(
                        i.worldPosition.y
                    );

                float3 originalColor =
                    spriteColor.rgb;

                /*
                 * Màu nước theo độ sâu.
                 */
                float3 depthTint =
                    lerp(
                        _ShallowTint.rgb,
                        _DeepTint.rgb,
                        depth01
                    );

                float3 underwaterColor =
                    lerp(
                        originalColor,
                        originalColor *
                        depthTint,
                        _TintStrength
                    );

                /*
                 * Giữ lại màu và texture gốc của đá.
                 */
                underwaterColor =
                    lerp(
                        underwaterColor,
                        originalColor,
                        _OriginalColorPreservation
                    );

                underwaterColor =
                    ApplySaturation(
                        underwaterColor,
                        _Saturation
                    );

                underwaterColor =
                    ApplyContrast(
                        underwaterColor,
                        _Contrast
                    );

                underwaterColor *=
                    _Brightness;

                /*
                 * Tối dần theo độ sâu.
                 */
                underwaterColor *=
                    1.0 -
                    depth01 *
                    _DepthDarkness;

                /*
                 * Làm tối nhẹ những vùng vốn đã tối,
                 * giúp giữ các khe và vết nứt của đá.
                 */
                float originalLuminance =
                    dot(
                        originalColor,
                        float3(
                            0.2126,
                            0.7152,
                            0.0722
                        )
                    );

                float creviceMask =
                    saturate(
                        1.0 -
                        originalLuminance
                    );

                creviceMask *=
                    creviceMask;

                underwaterColor *=
                    1.0 -
                    creviceMask *
                    _CreviceDarkness;

                /*
                 * Biến thiên màu rất nhẹ để đá
                 * không trông như được phủ màu phẳng.
                 */
                float surfaceNoise =
                    FBM(
                        i.worldPosition *
                        _SurfaceVariationScale
                    );

                underwaterColor *=
                    lerp(
                        1.0 -
                        _SurfaceVariation,
                        1.0 +
                        _SurfaceVariation,
                        surfaceNoise
                    );

                /*
                 * Cặn, rêu hoặc bụi bám bất quy tắc.
                 * Không chuyển động vì nó bám trên đá.
                 */
                float sedimentNoise =
                    FBM(
                        i.worldPosition *
                        _SedimentScale +
                        float2(
                            13.7,
                            4.9
                        )
                    );

                float sedimentMask =
                    saturate(
                        (
                            sedimentNoise -
                            0.45
                        ) *
                        _SedimentContrast
                    );

                sedimentMask *=
                    _SedimentAmount *
                    underwaterMask;

                underwaterColor =
                    lerp(
                        underwaterColor,
                        underwaterColor *
                        _SedimentColor.rgb,
                        sedimentMask
                    );

                /*
                 * Caustics nhẹ trên mặt đá.
                 */
                float caustics =
                    CalculateCaustics(
                        i.worldPosition,
                        timeValue
                    );

                float causticsDepthFade =
                    lerp(
                        1.0,
                        1.0 -
                        depth01,
                        _CausticsDepthFade
                    );

                caustics *=
                    _CausticsIntensity *
                    causticsDepthFade;

                underwaterColor +=
                    _CausticsColor.rgb *
                    caustics *
                    0.3;

                float3 finalColor =
                    lerp(
                        originalColor,
                        underwaterColor,
                        underwaterMask
                    );

                /*
                 * Phần đá sát đường mặt nước
                 * được làm tối để tạo cảm giác ẩm ướt.
                 */
                float signedDepth =
                    _WaterSurfaceY -
                    i.worldPosition.y;

                float wetBand =
                    1.0 -
                    smoothstep(
                        0.0,
                        max(
                            _WetBandWidth,
                            0.0001
                        ),
                        abs(
                            signedDepth
                        )
                    );

                wetBand *=
                    saturate(
                        _UseWaterSurface
                    );

                finalColor =
                    lerp(
                        finalColor,
                        finalColor *
                        _WetTint.rgb,
                        wetBand *
                        _WetBandStrength
                    );

                /*
                 * Đường phản sáng mỏng ở nơi
                 * mặt nước cắt qua sprite đá.
                 */
                float surfaceLine =
                    1.0 -
                    smoothstep(
                        0.0,
                        max(
                            _SurfaceLineWidth,
                            0.0001
                        ),
                        abs(
                            signedDepth
                        )
                    );

                surfaceLine *=
                    saturate(
                        _UseWaterSurface
                    );

                finalColor +=
                    _SurfaceLineColor.rgb *
                    surfaceLine *
                    _SurfaceLineIntensity *
                    spriteColor.a;

                return fixed4(
                    saturate(
                        finalColor
                    ),
                    spriteColor.a
                );
            }

            ENDCG
        }
    }

    FallBack "Sprites/Default"
}