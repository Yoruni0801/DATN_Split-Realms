Shader "Custom/GemAura"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(OUTER GLOW)]
        _AuraColor ("Aura Color 1", Color) = (0.2, 0.6, 1.0, 1.0)
        _AuraColor2 ("Aura Color 2", Color) = (0.0, 0.3, 0.8, 1.0)
        _GlowWidth ("Glow Width", Range(0.01, 0.25)) = 0.08
        _GlowSoftness ("Glow Softness", Range(0.01, 0.3)) = 0.12
        _GlowIntensity ("Glow Brightness", Range(0, 4)) = 1.5

        [Header(ANIMATION)]
        _PulseSpeed ("Pulse Speed", Range(0, 8)) = 2.0
        _PulseAmount ("Pulse Amount", Range(0, 0.5)) = 0.15
        _RotationSpeed ("Rotation Speed", Range(0, 5)) = 1.0
        _NoiseScale ("Noise Scale", Range(1, 12)) = 5.0
        _NoiseAmount ("Noise Distortion", Range(0, 0.5)) = 0.15

        [Header(SHIMMER)]
        _Shimmer ("Shimmer Strength", Range(0, 2)) = 0.6
        _ShimmerSpeed ("Shimmer Speed", Range(0, 8)) = 3.0

        [Header(SURFACE OVERLAY)]
        _OverlayBlend ("Color Overlay", Range(0, 0.6)) = 0.15
        _FlowSpeed ("Flow Speed", Range(0, 3)) = 1.0

        // Unity UI stencil support
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "GemAura"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 uv            : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            float4 _ClipRect;

            fixed4 _Color;
            fixed4 _AuraColor;
            fixed4 _AuraColor2;
            float _GlowWidth;
            float _GlowSoftness;
            float _GlowIntensity;
            float _PulseSpeed;
            float _PulseAmount;
            float _RotationSpeed;
            float _NoiseScale;
            float _NoiseAmount;
            float _Shimmer;
            float _ShimmerSpeed;
            float _OverlayBlend;
            float _FlowSpeed;

            // =============================================
            // Noise utilities
            // =============================================
            float hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f); // smoothstep

                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                float2 shift = float2(100.0, 100.0);
                for (int i = 0; i < 4; i++)
                {
                    v += a * noise(p);
                    p = p * 2.0 + shift;
                    a *= 0.5;
                }
                return v;
            }

            // =============================================
            // Sample max alpha in neighborhood (dilated alpha)
            // Used to detect how close a transparent pixel is
            // to the sprite edge -> creates outer glow region
            // =============================================
            float sampleDilatedAlpha(float2 uv, float radius)
            {
                float maxAlpha = 0.0;
                float2 texel = _MainTex_TexelSize.xy;

                // Sample 16 points in a circle pattern at the given radius
                // This creates a smooth, circular glow around the sprite
                const int SAMPLES = 16;
                [unroll]
                for (int j = 0; j < SAMPLES; j++)
                {
                    float ang = (float)j / (float)SAMPLES * 6.2832;
                    float2 offset = float2(cos(ang), sin(ang)) * radius * texel;
                    float a = tex2Dlod(_MainTex, float4(uv + offset, 0, 0)).a;
                    maxAlpha = max(maxAlpha, a);
                }
                return maxAlpha;
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y;

                // ========================================
                // 1. Sample sprite
                // ========================================
                fixed4 sprite = tex2D(_MainTex, i.uv) * i.color;
                float spriteA = sprite.a;

                // ========================================
                // 2. Centered UV & polar coords
                // ========================================
                float2 cUV = i.uv - 0.5;
                float dist = length(cUV);
                float angle = atan2(cUV.y, cUV.x);

                // ========================================
                // 3. Animation: pulse
                // ========================================
                float pulse = 1.0 + _PulseAmount * sin(t * _PulseSpeed);

                // ========================================
                // 4. OUTER GLOW (for transparent pixels)
                //    Detects proximity to sprite edge and
                //    draws animated glow in the empty area
                // ========================================
                float glowAlpha = 0.0;
                fixed3 glowColor = fixed3(0, 0, 0);

                if (spriteA < 0.1)
                {
                    // How far to search for sprite edge (in texels)
                    float searchRadius = _GlowWidth * 256.0;

                    // Multi-step dilated alpha for smooth falloff
                    float nearAlpha = sampleDilatedAlpha(i.uv, searchRadius * 0.3);
                    float midAlpha  = sampleDilatedAlpha(i.uv, searchRadius * 0.6);
                    float farAlpha  = sampleDilatedAlpha(i.uv, searchRadius);

                    // Distance factor: closer to edge = brighter
                    float proximity = nearAlpha * 0.6 + midAlpha * 0.3 + farAlpha * 0.1;

                    // Animated noise distortion on the glow shape
                    float2 noiseUV = float2(
                        angle / 6.2832 * _NoiseScale + t * _RotationSpeed,
                        dist * _NoiseScale * 2.0
                    );
                    float n = fbm(noiseUV);

                    // Shimmer sparkles in the glow
                    float sparkle = pow(noise(float2(angle * 10.0 + t * _ShimmerSpeed, dist * 30.0)), 5.0);

                    // Blend 2 aura colors with noise
                    glowColor = lerp(_AuraColor.rgb, _AuraColor2.rgb, n);

                    // Compute glow intensity
                    float glowBase = proximity * _GlowIntensity * pulse;
                    // Soften the edges
                    glowBase *= smoothstep(0.0, _GlowSoftness, proximity);
                    // Add noise variation for organic feel
                    glowBase *= lerp(1.0 - _NoiseAmount, 1.0, n);
                    // Add shimmer
                    glowBase += sparkle * _Shimmer * proximity * pulse;

                    glowAlpha = saturate(glowBase);
                }

                // ========================================
                // 5. SURFACE EFFECTS (for opaque pixels)
                //    Subtle animated overlay on the gem
                // ========================================
                fixed3 surfaceResult = sprite.rgb;

                if (spriteA > 0.1)
                {
                    // Flowing energy overlay
                    float2 flowUV = float2(
                        angle / 6.2832 * 3.0 + t * _FlowSpeed,
                        dist * 5.0 - t * _FlowSpeed * 0.2
                    );
                    float flow = fbm(flowUV);
                    fixed3 auraBlend = lerp(_AuraColor.rgb, _AuraColor2.rgb, flow);

                    // Subtle color overlay
                    surfaceResult = lerp(surfaceResult, surfaceResult * auraBlend * 2.2, _OverlayBlend * flow);

                    // Surface shimmer
                    float surfSparkle = pow(noise(float2(angle * 8.0 + t * _ShimmerSpeed, dist * 20.0)), 6.0);
                    surfaceResult += auraBlend * surfSparkle * _Shimmer * 0.3;

                    // Gentle pulse brightness
                    surfaceResult *= lerp(1.0, pulse, 0.1);
                }

                // ========================================
                // 6. Composite: sprite on top, glow behind
                // ========================================
                fixed3 finalRGB;
                float finalA;

                if (spriteA > 0.1)
                {
                    // Opaque sprite pixels: show enhanced sprite
                    finalRGB = surfaceResult;
                    finalA = spriteA;
                }
                else
                {
                    // Transparent area: show outer glow
                    finalRGB = glowColor;
                    finalA = glowAlpha;
                }

                fixed4 finalColor = fixed4(finalRGB, finalA);

                #ifdef UNITY_UI_CLIP_RECT
                finalColor.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(finalColor.a - 0.001);
                #endif

                return finalColor;
            }
            ENDCG
        }
    }
}
