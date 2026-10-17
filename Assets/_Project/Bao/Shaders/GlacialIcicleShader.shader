Shader "Bao/GlacialIcicleShader"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        [HDR] _CoreGlowColor ("Core Glow Color", Color) = (0.6, 2.2, 2.8, 1)
        [HDR] _GlacierDeepColor ("Glacier Deep Color", Color) = (0.05, 0.35, 0.85, 1)
        [HDR] _FrostApexColor ("Frost Apex Color", Color) = (1.8, 2.4, 2.8, 1)
        [HDR] _FresnelColor ("Fresnel Edge Color", Color) = (0.3, 1.5, 2.5, 1)
        _FresnelPower ("Fresnel Sharpness", Range(0.5, 8.0)) = 2.8
        _FrostCausticSpeed ("Frost Shimmer Speed", Range(0, 10)) = 4.5
        _StateIntensity ("Glow Intensity", Range(0, 3)) = 1.2
        _DissolveProgress ("Dissolve Progress", Range(0, 1)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+90"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float2 worldPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _CoreGlowColor;
            fixed4 _GlacierDeepColor;
            fixed4 _FrostApexColor;
            fixed4 _FresnelColor;
            float _FresnelPower;
            float _FrostCausticSpeed;
            float _StateIntensity;
            float _DissolveProgress;

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                output.worldPos = mul(unity_ObjectToWorld, input.vertex).xy;
                return output;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 uv = input.texcoord;
                fixed4 sprite = tex2D(_MainTex, uv) * input.color;
                if (sprite.a <= 0.005)
                    return fixed4(0, 0, 0, 0);

                // Dissolve check if enabled
                if (_DissolveProgress > 0.0)
                {
                    float dissolveNoise = noise2D(uv * 12.0 + input.worldPos * 2.0);
                    if (dissolveNoise < _DissolveProgress)
                        return fixed4(0, 0, 0, 0);
                }

                // Inner rim / fresnel detection
                float2 texel = _MainTex_TexelSize.xy * 2.2;
                float neighbourAlpha = min(
                    min(tex2D(_MainTex, uv + float2(texel.x, 0)).a, tex2D(_MainTex, uv - float2(texel.x, 0)).a),
                    min(tex2D(_MainTex, uv + float2(0, texel.y)).a, tex2D(_MainTex, uv - float2(0, texel.y)).a));
                float innerEdge = saturate((sprite.a - neighbourAlpha) * 4.5);
                float fresnel = pow(innerEdge, _FresnelPower);

                // Dynamic Ice Caustic Shimmer & Frost Vein calculation
                float time = _Time.y * _FrostCausticSpeed;
                float shimmer1 = sin(uv.y * 18.0 - time * 2.5 + uv.x * 6.0) * 0.5 + 0.5;
                float shimmer2 = cos(uv.x * 22.0 + time * 1.8 - uv.y * 10.0) * 0.5 + 0.5;
                float frostCaustics = shimmer1 * shimmer2;

                // Core tip highlight (icicles are sharpest and brightest at the tip)
                float tipGlow = saturate(1.0 - uv.y * 1.2) * 0.75;
                float coreLuminance = saturate(sprite.r * 0.4 + sprite.g * 0.5 + sprite.b * 0.6);

                // Gradient Blending
                fixed3 iceBase = lerp(_GlacierDeepColor.rgb, _CoreGlowColor.rgb, coreLuminance * 0.85);
                iceBase += _FrostApexColor.rgb * (frostCaustics * 0.45 + tipGlow * 0.6);
                iceBase += _FresnelColor.rgb * fresnel * 1.8;

                iceBase *= _StateIntensity;

                float finalAlpha = sprite.a;
                return fixed4(iceBase * finalAlpha, finalAlpha);
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}
