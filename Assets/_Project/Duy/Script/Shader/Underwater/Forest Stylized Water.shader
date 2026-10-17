Shader "Custom/2D/Forest Realistic Stylized Water"
{
    Properties
    {
        [PerRendererData] _MainTex ("Water Mask / Sprite", 2D) = "white" {}
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0

        [Header(Water Colors)]
        _TopColor ("Shallow Color", Color) = (0.40, 0.95, 0.88, 1)
        _MiddleColor ("Middle Color", Color) = (0.08, 0.62, 0.68, 1)
        _BottomColor ("Deep Color", Color) = (0.015, 0.22, 0.32, 1)

        _Alpha ("Overall Alpha", Range(0, 1)) = 0.85
        _DepthDarkness ("Depth Darkness", Range(0, 1)) = 0.38
        _ColorSaturation ("Color Saturation", Range(0, 2)) = 1.1

        [Header(Surface Wave)]
        _SurfaceWaveSpeed ("Wave Speed", Range(0, 8)) = 1.2
        _SurfaceWaveStrength ("Wave Strength", Range(0, 0.15)) = 0.018
        _SurfaceWaveScale ("Wave Scale", Range(1, 80)) = 18
        _SurfaceWaveZone ("Top Wave Area", Range(0.05, 1)) = 0.3

        [Header(Flow Distortion)]
        _FlowScale ("Large Flow Scale", Range(1, 80)) = 12
        _FlowSpeed ("Large Flow Speed", Range(0, 5)) = 0.35
        _FlowStrength ("Large Flow Strength", Range(0, 0.15)) = 0.025

        _DetailScale ("Detail Scale", Range(1, 150)) = 42
        _DetailSpeed ("Detail Speed", Range(0, 8)) = 0.9
        _DetailStrength ("Detail Strength", Range(0, 0.1)) = 0.01

        [Header(Caustics)]
        _CausticsColor ("Caustics Color", Color) = (0.62, 1, 0.88, 1)
        _CausticsScale ("Caustics Scale", Range(1, 100)) = 24
        _CausticsSpeed ("Caustics Speed", Range(0, 5)) = 0.55
        _CausticsIntensity ("Caustics Intensity", Range(0, 4)) = 1.2
        _CausticsSharpness ("Caustics Sharpness", Range(1, 12)) = 5
        _CausticsDepthFade ("Caustics Depth Fade", Range(0, 1)) = 0.55

        [Header(Surface Highlight)]
        _HighlightColor ("Highlight Color", Color) = (0.85, 1, 0.95, 1)
        _HighlightIntensity ("Highlight Intensity", Range(0, 5)) = 1.2
        _HighlightSharpness ("Highlight Sharpness", Range(1, 64)) = 18
        _HighlightScale ("Highlight Scale", Range(1, 100)) = 28

        [Header(Sparkles)]
        _SparkleColor ("Sparkle Color", Color) = (1, 1, 0.9, 1)
        _SparkleDensity ("Sparkle Density", Range(4, 100)) = 38
        _SparkleIntensity ("Sparkle Intensity", Range(0, 5)) = 0.8
        _SparkleSpeed ("Sparkle Speed", Range(0, 10)) = 2.5
        _SparkleThreshold ("Sparkle Amount", Range(0.8, 0.995)) = 0.95

        [Header(Foam Edge)]
        _FoamColor ("Foam Color", Color) = (0.78, 1, 0.9, 1)
        _FoamWidth ("Foam Width In Pixels", Range(0.5, 15)) = 3
        _FoamIntensity ("Foam Intensity", Range(0, 4)) = 1.1
        _FoamNoiseScale ("Foam Noise Scale", Range(1, 100)) = 24
        _FoamSpeed ("Foam Speed", Range(0, 5)) = 0.7
        _FoamCutoff ("Foam Breakup", Range(0, 1)) = 0.3

        [Header(Bubbles Side View)]
        _BubbleColor ("Bubble Color", Color) = (0.75, 1, 0.94, 1)
        _BubbleDensity ("Bubbles Per World Unit", Range(0.5, 12)) = 3
        _BubbleSize ("Bubble World Size", Range(0.005, 0.15)) = 0.04
        _BubbleSpeed ("Bubble Rise Speed", Range(0, 3)) = 0.28
        _BubbleIntensity ("Bubble Intensity", Range(0, 4)) = 0.9
        _BubbleAmount ("Bubble Amount", Range(0, 1)) = 0.3
        _BubbleWobble ("Horizontal Wobble", Range(0, 0.25)) = 0.025
        _BubbleWobbleSpeed ("Wobble Speed", Range(0, 8)) = 1.8
        _BubbleVerticalStretch ("Vertical Stretch", Range(0.7, 1.8)) = 1.1
        _BubbleSurfaceFade ("Surface Fade Area", Range(0.01, 0.5)) = 0.12

        [Header(Depth Particles)]
        _ParticleColor ("Particle Color", Color) = (0.55, 0.95, 0.85, 1)
        _ParticleDensity ("Particle Density", Range(4, 100)) = 28
        _ParticleSpeed ("Particle Speed", Range(0, 3)) = 0.18
        _ParticleIntensity ("Particle Intensity", Range(0, 2)) = 0.25
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
            float _EnableExternalAlpha;

            fixed4 _TopColor;
            fixed4 _MiddleColor;
            fixed4 _BottomColor;

            float _Alpha;
            float _DepthDarkness;
            float _ColorSaturation;

            float _SurfaceWaveSpeed;
            float _SurfaceWaveStrength;
            float _SurfaceWaveScale;
            float _SurfaceWaveZone;

            float _FlowScale;
            float _FlowSpeed;
            float _FlowStrength;

            float _DetailScale;
            float _DetailSpeed;
            float _DetailStrength;

            fixed4 _CausticsColor;
            float _CausticsScale;
            float _CausticsSpeed;
            float _CausticsIntensity;
            float _CausticsSharpness;
            float _CausticsDepthFade;

            fixed4 _HighlightColor;
            float _HighlightIntensity;
            float _HighlightSharpness;
            float _HighlightScale;

            fixed4 _SparkleColor;
            float _SparkleDensity;
            float _SparkleIntensity;
            float _SparkleSpeed;
            float _SparkleThreshold;

            fixed4 _FoamColor;
            float _FoamWidth;
            float _FoamIntensity;
            float _FoamNoiseScale;
            float _FoamSpeed;
            float _FoamCutoff;

            fixed4 _BubbleColor;
            float _BubbleDensity;
            float _BubbleSize;
            float _BubbleSpeed;
            float _BubbleIntensity;
            float _BubbleAmount;
            float _BubbleWobble;
            float _BubbleWobbleSpeed;
            float _BubbleVerticalStretch;
            float _BubbleSurfaceFade;

            fixed4 _ParticleColor;
            float _ParticleDensity;
            float _ParticleSpeed;
            float _ParticleIntensity;

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
                float2 rawUV : TEXCOORD1;
                float2 worldPos : TEXCOORD2;
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

            float Hash21(float2 position)
            {
                position = frac(
                    position * float2(123.34, 456.21)
                );

                position += dot(
                    position,
                    position + 45.32
                );

                return frac(position.x * position.y);
            }

            float HashSeed(float2 position, float seed)
            {
                return Hash21(
                    position +
                    float2(
                        seed * 17.17,
                        seed * 43.31
                    )
                );
            }

            float ValueNoise(float2 position)
            {
                float2 cellID = floor(position);
                float2 localPosition = frac(position);

                localPosition =
                    localPosition *
                    localPosition *
                    (3.0 - 2.0 * localPosition);

                float valueA = Hash21(cellID);
                float valueB = Hash21(cellID + float2(1.0, 0.0));
                float valueC = Hash21(cellID + float2(0.0, 1.0));
                float valueD = Hash21(cellID + float2(1.0, 1.0));

                float lowerValue =
                    lerp(valueA, valueB, localPosition.x);

                float upperValue =
                    lerp(valueC, valueD, localPosition.x);

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

                result += ValueNoise(position) * amplitude;

                position =
                    position * 2.03 +
                    float2(17.1, 9.2);

                amplitude *= 0.5;
                result += ValueNoise(position) * amplitude;

                position =
                    position * 2.01 +
                    float2(8.3, 19.7);

                amplitude *= 0.5;
                result += ValueNoise(position) * amplitude;

                position =
                    position * 2.04 +
                    float2(13.5, 4.8);

                amplitude *= 0.5;
                result += ValueNoise(position) * amplitude;

                return result;
            }

            float2 CalculateLargeFlow(float2 uv, float timeValue)
            {
                float2 flowUV1 =
                    uv * _FlowScale +
                    float2(
                        timeValue * _FlowSpeed,
                        -timeValue * _FlowSpeed * 0.63
                    );

                float2 flowUV2 =
                    uv * (_FlowScale * 1.73) +
                    float2(
                        -timeValue * _FlowSpeed * 0.48,
                        timeValue * _FlowSpeed * 0.81
                    );

                float baseNoise1 = FBM(flowUV1);
                float noiseX1 = FBM(flowUV1 + float2(0.035, 0.0));
                float noiseY1 = FBM(flowUV1 + float2(0.0, 0.035));

                float baseNoise2 = FBM(flowUV2);
                float noiseX2 = FBM(flowUV2 + float2(0.022, 0.0));
                float noiseY2 = FBM(flowUV2 + float2(0.0, 0.022));

                float2 direction1 = float2(
                    noiseX1 - baseNoise1,
                    noiseY1 - baseNoise1
                );

                float2 direction2 = float2(
                    noiseX2 - baseNoise2,
                    noiseY2 - baseNoise2
                );

                return
                    (direction1 + direction2 * 0.55) *
                    _FlowStrength;
            }

            float2 CalculateDetailFlow(float2 uv, float timeValue)
            {
                float2 detailUV =
                    uv * _DetailScale +
                    float2(
                        -timeValue * _DetailSpeed,
                        timeValue * _DetailSpeed * 0.37
                    );

                float detailX =
                    sin(
                        detailUV.x +
                        sin(detailUV.y * 0.7)
                    );

                detailX +=
                    sin(
                        detailUV.x * 0.47 -
                        detailUV.y * 0.83
                    );

                float detailY =
                    cos(
                        detailUV.y +
                        sin(detailUV.x * 0.63)
                    );

                detailY +=
                    cos(
                        detailUV.y * 0.51 +
                        detailUV.x * 0.91
                    );

                return
                    float2(detailX, detailY) *
                    0.5 *
                    _DetailStrength;
            }

            float CalculateCaustics(float2 uv, float timeValue)
            {
                float2 position =
                    uv * _CausticsScale;

                position += float2(
                    timeValue * _CausticsSpeed,
                    -timeValue * _CausticsSpeed * 0.72
                );

                float waveA =
                    sin(
                        position.x +
                        sin(
                            position.y * 1.31 +
                            timeValue * 0.65
                        )
                    );

                float waveB =
                    sin(
                        position.y * 1.73 +
                        sin(
                            position.x * 0.84 -
                            timeValue * 0.87
                        )
                    );

                float waveC =
                    sin(
                        (position.x + position.y) * 0.71 -
                        timeValue * 0.53 +
                        sin(position.x * 0.31)
                    );

                float waveD =
                    sin(
                        (position.x - position.y) * 1.17 +
                        timeValue * 0.41
                    );

                float lineDistance =
                    abs(
                        waveA +
                        waveB +
                        waveC * 0.7 +
                        waveD * 0.35
                    );

                lineDistance /= 3.05;

                float causticValue =
                    1.0 - saturate(lineDistance);

                return pow(
                    causticValue,
                    _CausticsSharpness
                );
            }

            float SampleMaskAlpha(float2 uv)
            {
                return SampleSpriteTexture(uv).a;
            }

            float CalculateEdgeFoam(
                float2 uv,
                float centerAlpha
            )
            {
                float2 sampleOffset =
                    _MainTex_TexelSize.xy *
                    _FoamWidth;

                float alphaLeft =
                    SampleMaskAlpha(
                        uv + float2(-sampleOffset.x, 0.0)
                    );

                float alphaRight =
                    SampleMaskAlpha(
                        uv + float2(sampleOffset.x, 0.0)
                    );

                float alphaUp =
                    SampleMaskAlpha(
                        uv + float2(0.0, sampleOffset.y)
                    );

                float alphaDown =
                    SampleMaskAlpha(
                        uv + float2(0.0, -sampleOffset.y)
                    );

                float alphaUpperLeft =
                    SampleMaskAlpha(
                        uv +
                        float2(
                            -sampleOffset.x,
                            sampleOffset.y
                        )
                    );

                float alphaUpperRight =
                    SampleMaskAlpha(
                        uv +
                        float2(
                            sampleOffset.x,
                            sampleOffset.y
                        )
                    );

                float alphaLowerLeft =
                    SampleMaskAlpha(
                        uv +
                        float2(
                            -sampleOffset.x,
                            -sampleOffset.y
                        )
                    );

                float alphaLowerRight =
                    SampleMaskAlpha(
                        uv +
                        float2(
                            sampleOffset.x,
                            -sampleOffset.y
                        )
                    );

                float minimumAlpha =
                    min(alphaLeft, alphaRight);

                minimumAlpha =
                    min(minimumAlpha, alphaUp);

                minimumAlpha =
                    min(minimumAlpha, alphaDown);

                minimumAlpha =
                    min(minimumAlpha, alphaUpperLeft);

                minimumAlpha =
                    min(minimumAlpha, alphaUpperRight);

                minimumAlpha =
                    min(minimumAlpha, alphaLowerLeft);

                minimumAlpha =
                    min(minimumAlpha, alphaLowerRight);

                return saturate(
                    (centerAlpha - minimumAlpha) * 4.0
                );
            }

            float CalculateSideViewBubbleLayer(
                float2 worldPosition,
                float timeValue,
                float density,
                float bubbleSize,
                float seed
            )
            {
                /*
                 * Dịch không gian xuống dưới.
                 * Hoa văn hiển thị sẽ chuyển động lên trên.
                 */
                float2 movingPosition = worldPosition;

                movingPosition.y -=
                    timeValue * _BubbleSpeed;

                float2 gridPosition =
                    movingPosition * density;

                float2 cellID =
                    floor(gridPosition);

                float2 cellUV =
                    frac(gridPosition);

                float randomPresence =
                    HashSeed(cellID, seed);

                float randomX =
                    HashSeed(cellID, seed + 1.27);

                float randomY =
                    HashSeed(cellID, seed + 4.91);

                float randomSize =
                    HashSeed(cellID, seed + 8.63);

                float randomPhase =
                    HashSeed(cellID, seed + 12.37);

                float bubbleExists =
                    step(
                        1.0 - _BubbleAmount,
                        randomPresence
                    );

                float horizontalMovement =
                    sin(
                        timeValue * _BubbleWobbleSpeed +
                        randomPhase * 6.2831853
                    );

                horizontalMovement *=
                    _BubbleWobble * density;

                float2 bubbleCenter;

                bubbleCenter.x =
                    lerp(
                        0.18,
                        0.82,
                        randomX
                    ) +
                    horizontalMovement;

                bubbleCenter.y =
                    lerp(
                        0.15,
                        0.85,
                        randomY
                    );

                float bubbleRadius =
                    bubbleSize *
                    density *
                    lerp(
                        0.55,
                        1.45,
                        randomSize
                    );

                float2 bubbleDelta =
                    cellUV - bubbleCenter;

                /*
                 * Kéo nhẹ theo chiều dọc để phù hợp góc nhìn ngang.
                 */
                bubbleDelta.y /=
                    max(
                        _BubbleVerticalStretch,
                        0.001
                    );

                float bubbleDistance =
                    length(bubbleDelta);

                float edgeSoftness =
                    max(
                        fwidth(bubbleDistance) * 1.5,
                        0.002
                    );

                float outerShape =
                    1.0 -
                    smoothstep(
                        bubbleRadius,
                        bubbleRadius + edgeSoftness,
                        bubbleDistance
                    );

                float innerRadius =
                    bubbleRadius * 0.72;

                float innerShape =
                    1.0 -
                    smoothstep(
                        innerRadius,
                        innerRadius + edgeSoftness,
                        bubbleDistance
                    );

                /*
                 * Viền bong bóng trong suốt.
                 */
                float bubbleRim =
                    saturate(
                        outerShape - innerShape
                    );

                /*
                 * Điểm phản sáng trên bên trái.
                 */
                float2 highlightCenter =
                    float2(
                        -bubbleRadius * 0.3,
                        bubbleRadius * 0.32
                    );

                float highlightDistance =
                    length(
                        bubbleDelta -
                        highlightCenter
                    );

                float primaryHighlight =
                    1.0 -
                    smoothstep(
                        bubbleRadius * 0.08,
                        bubbleRadius * 0.25,
                        highlightDistance
                    );

                /*
                 * Vệt sáng cong ở cạnh trái.
                 */
                float leftHighlight =
                    1.0 -
                    smoothstep(
                        bubbleRadius * 0.12,
                        bubbleRadius * 0.32,
                        abs(
                            bubbleDelta.x +
                            bubbleRadius * 0.42
                        )
                    );

                leftHighlight *=
                    1.0 -
                    smoothstep(
                        bubbleRadius * 0.05,
                        bubbleRadius * 0.75,
                        abs(bubbleDelta.y)
                    );

                leftHighlight *= outerShape;

                float softInterior =
                    outerShape *
                    saturate(
                        1.0 -
                        bubbleDistance /
                        max(bubbleRadius, 0.0001)
                    );

                float bubbleResult =
                    bubbleRim * 0.9 +
                    primaryHighlight * 0.85 +
                    leftHighlight * 0.32 +
                    softInterior * 0.05;

                return
                    bubbleResult *
                    bubbleExists;
            }

            float CalculateSparkles(
                float2 uv,
                float timeValue
            )
            {
                float2 movingUV =
                    uv +
                    float2(
                        timeValue * 0.018,
                        -timeValue * 0.011
                    );

                float2 sparkleGrid =
                    movingUV * _SparkleDensity;

                float2 sparkleCellID =
                    floor(sparkleGrid);

                float2 sparkleCellUV =
                    frac(sparkleGrid) - 0.5;

                float randomValue =
                    Hash21(sparkleCellID);

                float randomPhase =
                    HashSeed(
                        sparkleCellID,
                        6.73
                    );

                float sparkleExists =
                    step(
                        _SparkleThreshold,
                        randomValue
                    );

                float pulseValue =
                    sin(
                        timeValue * _SparkleSpeed +
                        randomPhase * 6.2831853
                    );

                pulseValue =
                    pow(
                        saturate(pulseValue),
                        10.0
                    );

                float sparkleCore =
                    1.0 -
                    smoothstep(
                        0.0,
                        0.13,
                        length(sparkleCellUV)
                    );

                float horizontalLine =
                    1.0 -
                    smoothstep(
                        0.0,
                        0.045,
                        abs(sparkleCellUV.y)
                    );

                horizontalLine *=
                    saturate(
                        1.0 -
                        abs(sparkleCellUV.x) * 4.0
                    );

                float verticalLine =
                    1.0 -
                    smoothstep(
                        0.0,
                        0.045,
                        abs(sparkleCellUV.x)
                    );

                verticalLine *=
                    saturate(
                        1.0 -
                        abs(sparkleCellUV.y) * 4.0
                    );

                float sparkleLines =
                    max(
                        horizontalLine,
                        verticalLine
                    );

                float sparkleShape =
                    max(
                        sparkleCore,
                        sparkleLines * 0.55
                    );

                return
                    sparkleExists *
                    pulseValue *
                    sparkleShape;
            }

            float CalculateDepthParticles(
                float2 worldPosition,
                float timeValue
            )
            {
                float2 movingPosition =
                    worldPosition +
                    float2(
                        timeValue * _ParticleSpeed * 0.2,
                        timeValue * _ParticleSpeed
                    );

                float2 particleGrid =
                    movingPosition * _ParticleDensity;

                float2 particleCellID =
                    floor(particleGrid);

                float2 particleCellUV =
                    frac(particleGrid);

                float randomPresence =
                    Hash21(particleCellID);

                float randomX =
                    HashSeed(
                        particleCellID,
                        3.13
                    );

                float randomY =
                    HashSeed(
                        particleCellID,
                        7.71
                    );

                float randomSize =
                    HashSeed(
                        particleCellID,
                        11.42
                    );

                float2 particleCenter =
                    float2(randomX, randomY);

                float particleDistance =
                    length(
                        particleCellUV -
                        particleCenter
                    );

                float particleSize =
                    lerp(
                        0.025,
                        0.09,
                        randomSize
                    );

                float particleShape =
                    1.0 -
                    smoothstep(
                        particleSize,
                        particleSize + 0.025,
                        particleDistance
                    );

                float particleExists =
                    step(
                        0.76,
                        randomPresence
                    );

                return
                    particleShape *
                    particleExists;
            }

            float3 ApplySaturation(
                float3 inputColor,
                float saturationValue
            )
            {
                float luminance =
                    dot(
                        inputColor,
                        float3(
                            0.2126,
                            0.7152,
                            0.0722
                        )
                    );

                float3 grayscale =
                    float3(
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

            v2f vert(appdata v)
            {
                v2f o;

                float timeValue = _Time.y;

                float topWaveMask =
                    smoothstep(
                        1.0 - _SurfaceWaveZone,
                        1.0,
                        v.uv.y
                    );

                float mainWave =
                    sin(
                        v.uv.x *
                        _SurfaceWaveScale +
                        timeValue *
                        _SurfaceWaveSpeed
                    );

                float secondaryWave =
                    sin(
                        v.uv.x *
                        _SurfaceWaveScale *
                        1.83 -
                        timeValue *
                        _SurfaceWaveSpeed *
                        1.37 +
                        1.4
                    );

                float tertiaryWave =
                    sin(
                        v.uv.x *
                        _SurfaceWaveScale *
                        0.47 +
                        timeValue *
                        _SurfaceWaveSpeed *
                        0.61
                    );

                float combinedWave =
                    mainWave +
                    secondaryWave * 0.38 +
                    tertiaryWave * 0.22;

                v.vertex.y +=
                    combinedWave *
                    _SurfaceWaveStrength *
                    topWaveMask;

                v.vertex.x +=
                    secondaryWave *
                    _SurfaceWaveStrength *
                    0.12 *
                    topWaveMask;

                o.vertex =
                    UnityObjectToClipPos(v.vertex);

                o.uv =
                    TRANSFORM_TEX(
                        v.uv,
                        _MainTex
                    );

                o.rawUV = v.uv;

                o.worldPos =
                    mul(
                        unity_ObjectToWorld,
                        v.vertex
                    ).xy;

                o.color = v.color;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float timeValue = _Time.y;

                fixed4 spriteMask =
                    SampleSpriteTexture(i.uv);

                clip(spriteMask.a - 0.001);

                float vertical =
                    saturate(i.rawUV.y);

                float depth =
                    1.0 - vertical;

                float2 largeFlow =
                    CalculateLargeFlow(
                        i.rawUV,
                        timeValue
                    );

                float2 detailFlow =
                    CalculateDetailFlow(
                        i.rawUV,
                        timeValue
                    );

                float2 totalFlow =
                    largeFlow + detailFlow;

                float2 animatedUV =
                    i.rawUV + totalFlow;

                /*
                 * Gradient màu nước.
                 */
                float3 deepColor =
                    lerp(
                        _BottomColor.rgb,
                        _MiddleColor.rgb,
                        smoothstep(
                            0.0,
                            0.58,
                            vertical
                        )
                    );

                float3 shallowColor =
                    lerp(
                        _MiddleColor.rgb,
                        _TopColor.rgb,
                        smoothstep(
                            0.48,
                            1.0,
                            vertical
                        )
                    );

                float3 waterColor =
                    lerp(
                        deepColor,
                        shallowColor,
                        smoothstep(
                            0.35,
                            0.72,
                            vertical
                        )
                    );

                float deepNoise =
                    FBM(
                        animatedUV * 5.0 +
                        float2(
                            timeValue * 0.05,
                            -timeValue * 0.035
                        )
                    );

                float depthDarkening =
                    depth *
                    _DepthDarkness *
                    lerp(
                        0.72,
                        1.15,
                        deepNoise
                    );

                waterColor *=
                    1.0 -
                    saturate(depthDarkening);

                waterColor =
                    ApplySaturation(
                        waterColor,
                        _ColorSaturation
                    );

                /*
                 * Caustics dưới nước.
                 */
                float causticsA =
                    CalculateCaustics(
                        animatedUV,
                        timeValue
                    );

                float causticsB =
                    CalculateCaustics(
                        animatedUV * 1.37 +
                        float2(4.7, 8.2),
                        -timeValue * 0.73
                    );

                float causticValue =
                    max(
                        causticsA,
                        causticsB * 0.68
                    );

                float causticDepthFade =
                    lerp(
                        1.0,
                        1.0 - depth,
                        _CausticsDepthFade
                    );

                causticValue *=
                    _CausticsIntensity *
                    causticDepthFade;

                waterColor +=
                    _CausticsColor.rgb *
                    causticValue *
                    0.32;

                /*
                 * Phản sáng bề mặt.
                 */
                float3 fakeNormal =
                    normalize(
                        float3(
                            -totalFlow.x * 12.0,
                            -totalFlow.y * 12.0,
                            1.0
                        )
                    );

                float3 fakeLightDirection =
                    normalize(
                        float3(
                            -0.35,
                            0.58,
                            0.75
                        )
                    );

                float normalHighlight =
                    pow(
                        saturate(
                            dot(
                                fakeNormal,
                                fakeLightDirection
                            )
                        ),
                        _HighlightSharpness
                    );

                float highlightWaveA =
                    sin(
                        animatedUV.x *
                        _HighlightScale +
                        animatedUV.y *
                        _HighlightScale *
                        0.43 +
                        timeValue * 1.35
                    );

                float highlightWaveB =
                    sin(
                        animatedUV.x *
                        _HighlightScale *
                        1.71 -
                        animatedUV.y *
                        _HighlightScale *
                        0.68 -
                        timeValue * 1.03
                    );

                float crestBase =
                    saturate(
                        highlightWaveA * 0.35 +
                        highlightWaveB * 0.25 +
                        0.5
                    );

                float crestPower =
                    max(
                        2.0,
                        _HighlightSharpness * 0.23
                    );

                float crestHighlight =
                    pow(
                        crestBase,
                        crestPower
                    );

                float combinedHighlight =
                    normalHighlight * 0.6 +
                    crestHighlight * 0.75;

                combinedHighlight *=
                    _HighlightIntensity *
                    smoothstep(
                        0.08,
                        0.9,
                        vertical
                    );

                waterColor +=
                    _HighlightColor.rgb *
                    combinedHighlight *
                    0.24;

                /*
                 * Bong bóng nhìn ngang bằng world position.
                 */
                float largeBubbles =
                    CalculateSideViewBubbleLayer(
                        i.worldPos,
                        timeValue,
                        _BubbleDensity,
                        _BubbleSize,
                        2.17
                    );

                float smallBubbles =
                    CalculateSideViewBubbleLayer(
                        i.worldPos +
                        float2(0.173, 0.347),
                        timeValue * 1.13,
                        _BubbleDensity * 1.65,
                        _BubbleSize * 0.52,
                        9.43
                    );

                float tinyBubbles =
                    CalculateSideViewBubbleLayer(
                        i.worldPos +
                        float2(0.521, 0.119),
                        timeValue * 1.34,
                        _BubbleDensity * 2.3,
                        _BubbleSize * 0.3,
                        15.71
                    );

                float bubbleValue =
                    largeBubbles +
                    smallBubbles * 0.62 +
                    tinyBubbles * 0.32;

                float lowerBubbleFade =
                    smoothstep(
                        0.02,
                        0.18,
                        vertical
                    );

                float upperBubbleFade =
                    1.0 -
                    smoothstep(
                        1.0 - _BubbleSurfaceFade,
                        1.0,
                        vertical
                    );

                bubbleValue *=
                    lowerBubbleFade *
                    upperBubbleFade *
                    _BubbleIntensity;

                waterColor +=
                    _BubbleColor.rgb *
                    bubbleValue *
                    0.62;

                /*
                 * Hạt nhỏ dưới nước.
                 */
                float depthParticles =
                    CalculateDepthParticles(
                        i.worldPos,
                        timeValue
                    );

                depthParticles *=
                    depth *
                    _ParticleIntensity;

                waterColor +=
                    _ParticleColor.rgb *
                    depthParticles;

                /*
                 * Bọt theo viền alpha sprite.
                 */
                float edgeFoam =
                    CalculateEdgeFoam(
                        i.uv,
                        spriteMask.a
                    );

                float foamNoise =
                    FBM(
                        animatedUV *
                        _FoamNoiseScale +
                        float2(
                            timeValue * _FoamSpeed,
                            -timeValue *
                            _FoamSpeed *
                            0.57
                        )
                    );

                float foamPattern =
                    smoothstep(
                        _FoamCutoff,
                        1.0,
                        foamNoise
                    );

                float foamValue =
                    edgeFoam *
                    lerp(
                        0.42,
                        1.0,
                        foamPattern
                    ) *
                    _FoamIntensity;

                waterColor =
                    lerp(
                        waterColor,
                        _FoamColor.rgb,
                        saturate(
                            foamValue * 0.72
                        )
                    );

                /*
                 * Lấp lánh gần mặt nước.
                 */
                float sparkleValue =
                    CalculateSparkles(
                        animatedUV,
                        timeValue
                    );

                sparkleValue *=
                    _SparkleIntensity *
                    smoothstep(
                        0.55,
                        1.0,
                        vertical
                    );

                waterColor +=
                    _SparkleColor.rgb *
                    sparkleValue;

                /*
                 * Biến thiên màu nhẹ.
                 */
                float colorVariation =
                    FBM(
                        animatedUV * 7.0 +
                        float2(
                            -timeValue * 0.08,
                            timeValue * 0.04
                        )
                    );

                waterColor *=
                    lerp(
                        0.94,
                        1.06,
                        colorVariation
                    );

                waterColor *= i.color.rgb;

                float finalAlpha =
                    spriteMask.a *
                    _Alpha *
                    i.color.a;

                return fixed4(
                    waterColor,
                    finalAlpha
                );
            }

            ENDCG
        }
    }

    FallBack "Sprites/Default"
}