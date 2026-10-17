Shader "Custom/2D/Underwater Decoration"
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

        [Header(Underwater Color)]
        _ShallowTint ("Shallow Tint", Color) = (0.55, 0.92, 0.90, 1)
        _DeepTint ("Deep Tint", Color) = (0.18, 0.48, 0.58, 1)

        _TintStrength ("Tint Strength", Range(0, 1)) = 0.42
        _DepthRange ("Depth Range", Range(0.1, 20)) = 4
        _DepthDarkness ("Depth Darkness", Range(0, 1)) = 0.32
        _UnderwaterSaturation ("Underwater Saturation", Range(0, 2)) = 0.82
        _SurfaceTransition ("Surface Transition", Range(0.001, 0.25)) = 0.035

        [Header(Underwater Distortion)]
        _DistortionScale ("Distortion Scale", Range(0.1, 50)) = 8
        _DistortionSpeed ("Distortion Speed", Range(0, 10)) = 1.1
        _DistortionStrength ("Distortion Strength", Range(0, 0.03)) = 0.003

        [Header(Caustics)]
        _CausticsColor ("Caustics Color", Color) = (0.65, 1, 0.88, 1)
        _CausticsScale ("Caustics Scale", Range(0.1, 50)) = 5
        _CausticsSpeed ("Caustics Speed", Range(0, 5)) = 0.55
        _CausticsIntensity ("Caustics Intensity", Range(0, 3)) = 0.28
        _CausticsSharpness ("Caustics Sharpness", Range(1, 12)) = 5
        _CausticsDepthFade ("Caustics Depth Fade", Range(0, 1)) = 0.7

        [Header(Water Surface Line)]
        _SurfaceLineColor ("Surface Line Color", Color) = (0.75, 1, 0.94, 1)
        _SurfaceLineWidth ("Surface Line Width", Range(0.001, 0.3)) = 0.045
        _SurfaceLineIntensity ("Surface Line Intensity", Range(0, 3)) = 0.45

        /*
         * Các giá trị bên dưới được script truyền vào.
         */
        [HideInInspector]
        _WaterSurfaceY ("Water Surface Y", Float) = 0

        [HideInInspector]
        _WaterLeftX ("Water Left X", Float) = 0

        [HideInInspector]
        _WaterWidth ("Water Width", Float) = 1

        [HideInInspector]
        _WaterWaveSpeed ("Water Wave Speed", Float) = 1.2

        [HideInInspector]
        _WaterWaveScale ("Water Wave Scale", Float) = 18

        [HideInInspector]
        _WaterWaveStrengthWorld ("Water Wave Strength World", Float) = 0.018
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

            fixed4 _ShallowTint;
            fixed4 _DeepTint;

            float _TintStrength;
            float _DepthRange;
            float _DepthDarkness;
            float _UnderwaterSaturation;
            float _SurfaceTransition;

            float _DistortionScale;
            float _DistortionSpeed;
            float _DistortionStrength;

            fixed4 _CausticsColor;
            float _CausticsScale;
            float _CausticsSpeed;
            float _CausticsIntensity;
            float _CausticsSharpness;
            float _CausticsDepthFade;

            fixed4 _SurfaceLineColor;
            float _SurfaceLineWidth;
            float _SurfaceLineIntensity;

            float _WaterSurfaceY;
            float _WaterLeftX;
            float _WaterWidth;

            float _WaterWaveSpeed;
            float _WaterWaveScale;
            float _WaterWaveStrengthWorld;

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
                fixed4 spriteColor = tex2D(_MainTex, uv);

                #if ETC1_EXTERNAL_ALPHA

                    fixed4 externalAlpha = tex2D(_AlphaTex, uv);

                    spriteColor.a = lerp(
                        spriteColor.a,
                        externalAlpha.r,
                        _EnableExternalAlpha
                    );

                #endif

                return spriteColor;
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

                float3 grayscale = float3(
                    luminance,
                    luminance,
                    luminance
                );

                return lerp(
                    grayscale,
                    inputColor,
                    saturationValue
                );
            }

            /*
             * Tính chiều cao mặt nước tại vị trí X.
             * Công thức này được làm giống phần sóng của shader nước.
             */
            float CalculateWaterSurfaceY(
                float worldX,
                float timeValue
            )
            {
                float safeWidth = max(
                    abs(_WaterWidth),
                    0.0001
                );

                float waterUVX = saturate(
                    (worldX - _WaterLeftX) /
                    safeWidth
                );

                float mainWave = sin(
                    waterUVX *
                    _WaterWaveScale +
                    timeValue *
                    _WaterWaveSpeed
                );

                float secondaryWave = sin(
                    waterUVX *
                    _WaterWaveScale *
                    1.83 -
                    timeValue *
                    _WaterWaveSpeed *
                    1.37 +
                    1.4
                );

                float tertiaryWave = sin(
                    waterUVX *
                    _WaterWaveScale *
                    0.47 +
                    timeValue *
                    _WaterWaveSpeed *
                    0.61
                );

                float combinedWave =
                    mainWave +
                    secondaryWave * 0.38 +
                    tertiaryWave * 0.22;

                return
                    _WaterSurfaceY +
                    combinedWave *
                    _WaterWaveStrengthWorld;
            }

            float2 CalculateDistortion(
                float2 worldPosition,
                float timeValue
            )
            {
                float waveX = sin(
                    worldPosition.y *
                    _DistortionScale +
                    timeValue *
                    _DistortionSpeed
                );

                waveX += sin(
                    worldPosition.y *
                    _DistortionScale *
                    1.71 -
                    timeValue *
                    _DistortionSpeed *
                    0.63
                ) * 0.35;

                float waveY = cos(
                    worldPosition.x *
                    _DistortionScale *
                    0.73 -
                    timeValue *
                    _DistortionSpeed *
                    0.48
                );

                return float2(
                    waveX,
                    waveY * 0.3
                ) * _DistortionStrength;
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
                    timeValue * 0.53 +
                    sin(position.x * 0.31)
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

                float surfaceY =
                    CalculateWaterSurfaceY(
                        i.worldPosition.x,
                        timeValue
                    );

                /*
                 * Dương khi pixel nằm dưới mặt nước.
                 */
                float signedDepth =
                    surfaceY -
                    i.worldPosition.y;

                float edgeWidth = max(
                    _SurfaceTransition,
                    fwidth(signedDepth) * 1.5
                );

                float underwaterMask = smoothstep(
                    -edgeWidth,
                    edgeWidth,
                    signedDepth
                );

                float depth = max(
                    signedDepth,
                    0.0
                );

                float depth01 = saturate(
                    depth /
                    max(_DepthRange, 0.0001)
                );

                float2 distortion =
                    CalculateDistortion(
                        i.worldPosition,
                        timeValue
                    );

                distortion *= underwaterMask;

                fixed4 spriteColor =
                    SampleSpriteTexture(
                        i.uv + distortion
                    );

                spriteColor *=
                    i.color *
                    _Color;

                clip(spriteColor.a - 0.001);

                float3 depthTint = lerp(
                    _ShallowTint.rgb,
                    _DeepTint.rgb,
                    depth01
                );

                float3 tintedColor =
                    spriteColor.rgb *
                    lerp(
                        float3(1.0, 1.0, 1.0),
                        depthTint,
                        _TintStrength
                    );

                tintedColor = ApplySaturation(
                    tintedColor,
                    _UnderwaterSaturation
                );

                /*
                 * Vật càng sâu càng tối.
                 */
                tintedColor *=
                    1.0 -
                    depth01 *
                    _DepthDarkness;

                /*
                 * Ánh sáng caustics chỉ xuất hiện dưới nước.
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
                    0.32;

                float3 finalColor = lerp(
                    spriteColor.rgb,
                    tintedColor,
                    underwaterMask
                );

                /*
                 * Đường sáng nơi vật cắt qua mặt nước.
                 */
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

                finalColor +=
                    _SurfaceLineColor.rgb *
                    surfaceLine *
                    _SurfaceLineIntensity *
                    spriteColor.a;

                return fixed4(
                    finalColor,
                    spriteColor.a
                );
            }

            ENDCG
        }
    }

    FallBack "Sprites/Default"
}