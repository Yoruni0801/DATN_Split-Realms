Shader "SplitRealms/VFX/AetherMotes"
{
    Properties
    {
        [HDR] _Tint ("Tint", Color) = (1, 0.87, 0.54, 1)
        _Glow ("Light intensity", Range(0, 5)) = 1
        _Core ("Soft luminous core", Range(0, 1)) = 0.65
        _Pulse ("Breathing depth", Range(0, 1)) = 0.35
        _PulseSpeed ("Breathing speed", Range(0, 3)) = 0.8
        [Toggle] _Mist ("Mist instead of mote", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "AetherUnlit"
            Tags { "LightMode"="Universal2D" }
            Blend SrcAlpha One
            ZWrite Off
            // This is a 2D layer-sorted ambient effect, including depth-writing sprites.
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Glow, _Core, _Pulse, _PulseSpeed, _Mist;
            CBUFFER_END

            // Standard particle streams keep the layout compatible with URP 2D.
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.color = input.color;
                o.uv = input.uv;
                return o;
            }
            float SoftLobe(float2 p, float2 scale, float sharpness)
            {
                float2 q = p * scale;
                return exp2(-dot(q, q) * sharpness);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                // Randomized particle hues provide distinct breathing phases without
                // a custom vertex stream or a per-frame CPU controller.
                float phase = input.color.g * 73.0 + input.color.b * 31.0;
                float t = _Time.y;
                // A zero-alpha border prevents visible rectangular billboard edges.
                float edge = 1.0 - smoothstep(0.60, 0.98, max(abs(p.x), abs(p.y)));
                float breathing = 0.5 + 0.5 * sin(t * _PulseSpeed + phase);
                float flicker = lerp(1.0, 0.30 + 0.70 * breathing * breathing, _Pulse);
                float mask;
                if (_Mist > 0.5)
                {
                    float2 drift = float2(sin(t * 0.09 + phase), cos(t * 0.07 + phase)) * 0.09;
                    mask = SoftLobe(p + drift, float2(0.8, 2.3), 5.0) * 0.30;
                    mask += SoftLobe(p - float2(0.28, 0.09), float2(1.5, 3.3), 5.0) * 0.25;
                    mask += SoftLobe(p + float2(0.25, 0.10), float2(1.8, 2.5), 6.0) * 0.22;
                }
                else
                {
                    // Overlapping, off-centre lobes look like pollen, not solid discs.
                    float2 offset = float2(sin(phase), cos(phase * 1.7)) * 0.075;
                    float halo = SoftLobe(p, float2(1.0, 1.14), 6.0) * 0.28;
                    float core = SoftLobe(p - offset, float2(1.6, 1.05), 24.0);
                    float fleck = SoftLobe(p + float2(0.10, -0.08), float2(1.2, 1.6), 21.0) * 0.28;
                    mask = halo + _Core * (core + fleck);
                }
                half alpha = saturate(mask * edge * flicker * input.color.a * _Tint.a);
                return half4(input.color.rgb * _Tint.rgb * _Glow, alpha);
            }
            ENDHLSL
        }
    }
}
