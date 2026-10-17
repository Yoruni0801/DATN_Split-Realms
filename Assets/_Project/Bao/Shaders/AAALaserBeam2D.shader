Shader "Custom/AAALaserBeam2D"
{
    Properties
    {
        [Header(Textures and Noise)]
        [NoScaleOffset] _MainTex ("Beam Core Texture (Seamless)", 2D) = "white" {}
        [NoScaleOffset] _NoiseTex ("Distortion Noise Texture", 2D) = "white" {}

        [Header(Color and Energy Signature)]
        [HDR] _NeonColor ("Neon Body Tint", Color) = (1.05, 0.20, 0.62, 1.0)
        [HDR] _CoreColor ("Mid Energy Accent", Color) = (0.88, 0.12, 0.48, 1.0)
        [HDR] _WhiteFilamentColor ("Hyper-Core Filament", Color) = (1.6, 1.45, 1.6, 1.0)
        _AlphaBoost ("Alpha Multiplier", Range(0.5, 3.0)) = 1.35

        [Header(Energy Flow Dynamics)]
        _FlowSpeed ("Beam Flow Speed", Range(-20.0, 20.0)) = 6.0
        _BeamTilingX ("Beam Length Tiling", Range(0.1, 30.0)) = 1.0
        _NoiseDistortionStrength ("Noise Distortion Intensity", Range(0.0, 0.3)) = 0.05
        _NoiseTiling ("Noise Frequency", Range(0.5, 15.0)) = 3.0
        _NoiseSpeed ("Turbulence Flow Speed", Range(0.0, 15.0)) = 3.5

        [Header(Core and Edge Falloff)]
        _EdgeFalloffPower ("Edge Feather Falloff", Range(0.5, 4.0)) = 1.2
        _CoreFilamentNarrowness ("Filament Sharpness", Range(1.0, 20.0)) = 4.0
        _CoreFilamentStrength ("Filament Brightness", Range(0.0, 5.0)) = 2.8
        _PulseSpeed ("Energy Pulse Speed", Range(0.0, 15.0)) = 3.5
        _PulseAmount ("Energy Pulse Amount", Range(0.0, 0.3)) = 0.08
        _CapFadeRatio ("Terminal Cap Fade Ratio", Range(0.0, 0.05)) = 0.0
        _EnableCapFade ("Enable Terminal Cap Fade", Float) = 0.0

        [Header(Corona Aura Mode)]
        _IsCoronaPass ("Is Corona Pass (Soft Outer Glow)", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+10"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            sampler2D _NoiseTex;

            fixed4 _NeonColor;
            fixed4 _CoreColor;
            fixed4 _WhiteFilamentColor;
            half _AlphaBoost;

            half _FlowSpeed;
            half _BeamTilingX;
            half _NoiseDistortionStrength;
            half _NoiseTiling;
            half _NoiseSpeed;

            half _EdgeFalloffPower;
            half _CoreFilamentNarrowness;
            half _CoreFilamentStrength;
            half _PulseSpeed;
            half _PulseAmount;
            half _CapFadeRatio;
            half _EnableCapFade;
            half _IsCoronaPass;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float timeVal = _Time.y;

                // 1. Normalized distance from laser centerline [0..1]
                // Dead center = 0.0, quad outer edges = 1.0
                float distFromCenter = saturate(abs(uv.y - 0.5) * 2.0);

                // 2. Analytical Cosine Feathering: Smooth and solid body
                float edgeFeather = saturate(cos(distFromCenter * 1.57079632679));
                edgeFeather = pow(edgeFeather, _EdgeFalloffPower);

                // Optional smooth terminal cap fade at extreme beam ends (when enabled)
                float capFade = 1.0;
                if (_EnableCapFade > 0.5)
                {
                    float capEdge = max(0.001, _CapFadeRatio);
                    capFade = smoothstep(0.0, capEdge, uv.x) * smoothstep(1.0, 1.0 - capEdge, uv.x);
                }

                // 3. Dedicated Corona Aura Mode (Layer 1): Atmospheric wide glowing envelope
                if (_IsCoronaPass > 0.5)
                {
                    float coronaFalloff = pow(edgeFeather, 1.1);
                    half4 coronaCol = _NeonColor * coronaFalloff * 0.85 * _AlphaBoost;
                    coronaCol.a = saturate(coronaFalloff * IN.color.a * 0.85 * _AlphaBoost);
                    return coronaCol * capFade;
                }

                // 4. Plasma Turbulence Noise Distortion:
                // Plasma mask is 0 at center (preserving laser straightness) and 0 at outer boundary
                float plasmaMask = distFromCenter * (1.0 - distFromCenter) * 4.0;
                float noiseU = uv.x * _NoiseTiling;
                float2 noiseUV1 = float2(noiseU - timeVal * _NoiseSpeed * 0.7, uv.y * 1.5);
                float2 noiseUV2 = float2(noiseU * 1.63 + timeVal * _NoiseSpeed * 0.9, uv.y * 1.8 + 0.37);

                float n1 = tex2D(_NoiseTex, noiseUV1).r;
                float n2 = tex2D(_NoiseTex, noiseUV2).r;
                float distortion = (n1 - n2) * _NoiseDistortionStrength * plasmaMask;

                // 5. Sample Primary Energy Texture with dynamic flow
                float2 beamUV = float2(uv.x * _BeamTilingX - timeVal * _FlowSpeed, saturate(uv.y + distortion));
                fixed4 beamSample = tex2D(_MainTex, beamUV);

                // 6. Dynamic Energy Pulse
                float pulse = 1.0 + sin(timeVal * _PulseSpeed) * _PulseAmount;

                // 7. Hyper-Core White Filament:
                // Broad & vibrant laser core locked strictly along beam centerline
                float coreDist = distFromCenter * _CoreFilamentNarrowness * pulse;
                float filamentIntensity = exp(-coreDist * coreDist * 2.0) * _CoreFilamentStrength;

                // 8. Color Layering & Composition
                half3 plasmaRGB = lerp(_NeonColor.rgb, _CoreColor.rgb, edgeFeather) * (beamSample.rgb * 1.35 + 0.5);
                half3 finalRGB = plasmaRGB * edgeFeather + _WhiteFilamentColor.rgb * filamentIntensity;

                // Modulate by edge envelope and alpha boost
                finalRGB *= _AlphaBoost * (0.95 + 0.15 * pulse);
                half finalAlpha = saturate((edgeFeather * 0.85 + filamentIntensity * 0.95) * edgeFeather * IN.color.a * _AlphaBoost);

                return half4(finalRGB * capFade, finalAlpha * capFade);
            }
            ENDCG
        }
    }
    FallBack "Sprites/Default"
}
