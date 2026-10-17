Shader "Custom/2D/Underwater Creature"
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
        _SurfaceTransition ("Surface Transition", Range(0.001, 0.3)) = 0.04
        _UnderwaterAmount ("Underwater Amount", Range(0, 1)) = 1

        _ManualDepth ("Manual Depth", Range(0, 1)) = 0.35
        _DepthRange ("Automatic Depth Range", Range(0.1, 20)) = 4

        [Header(Creature Color)]
        _ShallowTint ("Shallow Tint", Color) = (0.40, 0.95, 0.88, 1)
        _DeepTint ("Deep Tint", Color) = (0.08, 0.42, 0.52, 1)

        _TintStrength ("Tint Strength", Range(0, 1)) = 0.32

        _OriginalColorPreservation
        (
            "Preserve Original Color",
            Range(0, 1)
        ) = 0.5

        _DepthDarkness ("Depth Darkness", Range(0, 1)) = 0.22
        _Saturation ("Underwater Saturation", Range(0, 2)) = 0.9
        _Brightness ("Brightness", Range(0.5, 1.5)) = 1
        _Contrast ("Contrast", Range(0.5, 2)) = 1.08

        [Header(Body Movement)]
        _BodySwayStrength
        (
            "Body Sway Strength",
            Range(0, 0.08)
        ) = 0.008

        _BodySwaySpeed ("Body Sway Speed", Range(0, 8)) = 1.5
        _BodySwayFrequency ("Body Sway Frequency", Range(0.1, 20)) = 4

        [Header(Animated Water Lighting)]
        _WaterLightColor
        (
            "Water Light Color",
            Color
        ) = (0.62, 1, 0.91, 1)

        _WaterLightScale ("Water Light Scale", Range(0.1, 30)) = 4
        _WaterLightSpeed ("Water Light Speed", Range(0, 8)) = 1
        _WaterLightIntensity ("Water Light Intensity", Range(0, 1)) = 0.08

        [Header(Caustics)]
        _CausticsColor
        (
            "Caustics Color",
            Color
        ) = (0.68, 1, 0.9, 1)

        _CausticsScale ("Caustics Scale", Range(0.1, 30)) = 4.5
        _CausticsSpeed ("Caustics Speed", Range(0, 5)) = 0.55

        _CausticsIntensity
        (
            "Caustics Intensity",
            Range(0, 2)
        ) = 0.12

        _CausticsSharpness
        (
            "Caustics Sharpness",
            Range(1, 12)
        ) = 5

        _CausticsDepthFade
        (
            "Caustics Depth Fade",
            Range(0, 1)
        ) = 0.65

        [Header(Water Surface Line)]
        _SurfaceLineColor
        (
            "Surface Line Color",
            Color
        ) = (0.78, 1, 0.95, 1)

        _SurfaceLineWidth
        (
            "Surface Line Width",
            Range(0.001, 0.25)
        ) = 0.035

        _SurfaceLineIntensity
        (
            "Surface Line Intensity",
            Range(0, 3)
        ) = 0.45
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
            float4 _MainTex_TexelSize;

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

            float _BodySwayStrength;
            float _BodySwaySpeed;
            float _BodySwayFrequency;

            fixed4 _WaterLightColor;
            float _WaterLightScale;
            float _WaterLightSpeed;
            float _WaterLightIntensity;

            fixed4 _CausticsColor;
            float _CausticsScale;
            float _CausticsSpeed;
            float _CausticsIntensity;
            float _CausticsSharpness;
            float _CausticsDepthFade;


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

            float CalculateSurfaceMask(float worldY)
            {
                float signedDepth =
                    _WaterSurfaceY -
                    worldY;

                float transitionWidth = max(
                    _SurfaceTransition,
                    0.0001
                );

                return smoothstep(
                    -transitionWidth,
                    transitionWidth,
                    signedDepth
                );
            }

            float CalculateUnderwaterMask(float worldY)
            {
                float surfaceMask =
                    CalculateSurfaceMask(worldY);

                float mask = lerp(
                    1.0,
                    surfaceMask,
                    saturate(_UseWaterSurface)
                );

                return
                    saturate(mask) *
                    saturate(_UnderwaterAmount);
            }

            float CalculateDepth01(float worldY)
            {
                float signedDepth = max(
                    _WaterSurfaceY -
                    worldY,
                    0.0
                );

                float automaticDepth = saturate(
                    signedDepth /
                    max(_DepthRange, 0.0001)
                );

                return lerp(
                    saturate(_ManualDepth),
                    automaticDepth,
                    saturate(_UseWaterSurface)
                );
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
                    (inputColor - 0.5) *
                    contrastValue +
                    0.5;
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
                    timeValue * _CausticsSpeed,
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
                    (position.x + position.y) *
                    0.71 -
                    timeValue * 0.53
                );

                float waveD = sin(
                    (position.x - position.y) *
                    1.17 +
                    timeValue * 0.41
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
                    saturate(lineDistance);

                return pow(
                    causticValue,
                    _CausticsSharpness
                );
            }


            v2f vert(appdata v)
            {
                v2f o;

                float2 originalWorldPosition = mul(
                    unity_ObjectToWorld,
                    v.vertex
                ).xy;

                float underwaterMask =
                    CalculateUnderwaterMask(
                        originalWorldPosition.y
                    );

                float sway = sin(
                    v.vertex.y *
                    _BodySwayFrequency +
                    _Time.y *
                    _BodySwaySpeed +
                    originalWorldPosition.x *
                    0.35
                );

                /*
                 * Lắc phần thân theo trục ngang.
                 * Không méo UV nên an toàn hơn với sprite animation.
                 */
                v.vertex.x +=
                    sway *
                    _BodySwayStrength *
                    underwaterMask;

                o.vertex = UnityObjectToClipPos(
                    v.vertex
                );

                o.uv = TRANSFORM_TEX(
                    v.uv,
                    _MainTex
                );

                o.worldPosition = mul(
                    unity_ObjectToWorld,
                    v.vertex
                ).xy;

                o.color = v.color;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float timeValue = _Time.y;

                fixed4 spriteColor =
                    SampleSpriteTexture(i.uv);

                spriteColor *=
                    i.color *
                    _Color;

                clip(spriteColor.a - 0.001);

                float underwaterMask =
                    CalculateUnderwaterMask(
                        i.worldPosition.y
                    );

                float depth01 =
                    CalculateDepth01(
                        i.worldPosition.y
                    );

                float3 depthTint = lerp(
                    _ShallowTint.rgb,
                    _DeepTint.rgb,
                    depth01
                );

                /*
                 * Giữ màu gốc của sinh vật,
                 * chỉ phủ một lớp màu nước lên trên.
                 */
                float3 tintedColor = lerp(
                    spriteColor.rgb,
                    spriteColor.rgb *
                    depthTint,
                    _TintStrength
                );

                tintedColor = lerp(
                    tintedColor,
                    spriteColor.rgb,
                    _OriginalColorPreservation
                );

                tintedColor =
                    ApplySaturation(
                        tintedColor,
                        _Saturation
                    );

                tintedColor =
                    ApplyContrast(
                        tintedColor,
                        _Contrast
                    );

                tintedColor *= _Brightness;

                /*
                 * Tối dần theo độ sâu.
                 */
                tintedColor *=
                    1.0 -
                    depth01 *
                    _DepthDarkness;

                /*
                 * Ánh nước chuyển động nhẹ trên cơ thể.
                 * Không làm méo texture mặt hoặc mắt.
                 */
                float waterLightWave = sin(
                    i.worldPosition.y *
                    _WaterLightScale +
                    timeValue *
                    _WaterLightSpeed +
                    sin(
                        i.worldPosition.x *
                        _WaterLightScale *
                        0.63 -
                        timeValue * 0.47
                    )
                );

                waterLightWave =
                    waterLightWave *
                    0.5 +
                    0.5;

                tintedColor +=
                    _WaterLightColor.rgb *
                    waterLightWave *
                    _WaterLightIntensity;

                /*
                 * Caustics trên cơ thể sinh vật.
                 */
                float caustics =
                    CalculateCaustics(
                        i.worldPosition,
                        timeValue
                    );

                float causticsDepthFade = lerp(
                    1.0,
                    1.0 - depth01,
                    _CausticsDepthFade
                );

                caustics *=
                    _CausticsIntensity *
                    causticsDepthFade;

                tintedColor +=
                    _CausticsColor.rgb *
                    caustics *
                    0.3;


                float3 finalColor = lerp(
                    spriteColor.rgb,
                    tintedColor,
                    underwaterMask
                );

                /*
                 * Đường sáng nếu sinh vật cắt qua mặt nước.
                 */
                float signedDepth =
                    _WaterSurfaceY -
                    i.worldPosition.y;

                float surfaceLine =
                    1.0 -
                    smoothstep(
                        0.0,
                        max(
                            _SurfaceLineWidth,
                            0.0001
                        ),
                        abs(signedDepth)
                    );

                surfaceLine *=
                    saturate(_UseWaterSurface);

                finalColor +=
                    _SurfaceLineColor.rgb *
                    surfaceLine *
                    _SurfaceLineIntensity *
                    spriteColor.a;

                return fixed4(
                    saturate(finalColor),
                    spriteColor.a
                );
            }

            ENDCG
        }
    }

    FallBack "Sprites/Default"
}