Shader "Bao/DreadwormVFX"
{
    Properties
    {
        _MainTex ("Effect Texture", 2D) = "white" {}
        [HDR] _TintColor ("Tint", Color) = (1, 1, 1, 1)
        [HDR] _RimColor ("Rim Glint Color", Color) = (1.45, 1.95, 2.45, 1)
        _PulseStrength ("Pulse Strength", Range(0, 2)) = 0.2
        _PulseSpeed ("Pulse Speed", Range(0, 20)) = 4.0
        _RimStrength ("Rim Strength", Range(0, 5)) = 0.0
        _Dissolve ("Dissolve Amount", Range(0, 1)) = 0.0
        _PrismaticSpread ("Prismatic Dispersion", Range(0, 2)) = 0.0
        _GradientStrength ("Vertical Gradient Strength", Range(0, 1)) = 0.0
        _RefractionStrength ("Internal Refraction Shimmer", Range(0, 1)) = 0.0
        [HideInInspector] _SrcBlend ("Source Blend", Float) = 5
        [HideInInspector] _DstBlend ("Destination Blend", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+80"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend [_SrcBlend] [_DstBlend]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
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
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _TintColor;
            fixed4 _RimColor;
            float _PulseStrength;
            float _PulseSpeed;
            float _RimStrength;
            float _Dissolve;
            float _PrismaticSpread;
            float _GradientStrength;
            float _RefractionStrength;

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color;
                return output;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 uv = input.texcoord;
                fixed4 sampled = tex2D(_MainTex, uv);
                if (sampled.a <= 0.001)
                    return fixed4(0, 0, 0, 0);

                // Dissolve calculation
                if (_Dissolve > 0.001)
                {
                    float noise = hash21(floor(uv * 96.0));
                    float dFactor = (_Dissolve * 1.25) - (1.0 - uv.y * 0.45 + noise * 0.25);
                    if (dFactor > 0.0)
                        return fixed4(0, 0, 0, 0);

                    // Edge burn glow
                    float edgeGlow = smoothstep(-0.08, 0.0, dFactor);
                    sampled.rgb += edgeGlow * _RimColor.rgb * 2.5;
                }

                // Inner rim detection if enabled
                float rimGlow = 0.0;
                if (_RimStrength > 0.01)
                {
                    float2 texel = _MainTex_TexelSize.xy * 1.5;
                    float aR = tex2D(_MainTex, uv + float2(texel.x, 0)).a;
                    float aL = tex2D(_MainTex, uv - float2(texel.x, 0)).a;
                    float aU = tex2D(_MainTex, uv + float2(0, texel.y)).a;
                    float aD = tex2D(_MainTex, uv - float2(0, texel.y)).a;
                    float minA = min(min(aR, aL), min(aU, aD));
                    rimGlow = saturate((sampled.a - minA) * 3.5) * _RimStrength;
                    rimGlow *= (0.7 + uv.y * 0.6); // Vertical Fresnel bias: đỉnh sáng hơn đáy
                }

                // Pulse Beat: sin wave chạy từ đáy lên đỉnh (uv.y) để tạo hiệu ứng thở của pha lê
                float pulse = 1.0 + sin(_Time.y * _PulseSpeed + uv.y * 12.0) * _PulseStrength;
                fixed4 color = sampled * input.color * _TintColor;
                color.rgb *= pulse;

                // Vertical Gradient: Base Dark (Navy) → Apex Diamond (Cyan-White)
                if (_GradientStrength > 0.001)
                {
                    fixed3 baseCol = fixed3(0.04, 0.10, 0.22);  // GlacialDeepNavy
                    fixed3 apexCol = fixed3(0.85, 1.35, 1.55);  // GlacialApexDiamond
                    fixed3 grad = lerp(baseCol, apexCol, pow(saturate(uv.y), 0.65));
                    color.rgb = lerp(color.rgb, grad * color.rgb * 1.4, _GradientStrength);
                }

                // Internal Refraction Shimmer: UV distortion lấy mẫu từ vị trí lân cận
                if (_RefractionStrength > 0.001)
                {
                    float2 distortUV = uv;
                    distortUV.x += sin(uv.y * 18.0 + _Time.y * 3.2) * 0.018 * _RefractionStrength;
                    distortUV.y += cos(uv.x * 12.0 + _Time.y * 2.1) * 0.012 * _RefractionStrength;
                    fixed4 refractSample = tex2D(_MainTex, distortUV);
                    color.rgb = lerp(color.rgb, refractSample.rgb * color.rgb * 1.25, _RefractionStrength * 0.55);
                }

                // Glacial Crystal Sheen & Diamond dispersion (eliminates magenta/pink artifacts)
                if (_PrismaticSpread > 0.01)
                {
                    float sweep = uv.x * 3.5 + uv.y * 1.5 - _Time.y * 2.5;
                    float sheen = pow(max(0.0, sin(sweep) * 0.5 + 0.5), 8.0) * _PrismaticSpread;
                    // Dual-chromatic Arctic Jade to Diamond Cyan gradient (Zero Red channel noise)
                    fixed3 glacialSheen = lerp(
                        fixed3(0.20, 0.95, 0.85),
                        fixed3(0.85, 1.25, 1.45),
                        sin(sweep * 0.5) * 0.5 + 0.5
                    );
                    color.rgb += glacialSheen * sheen * _RimColor.rgb * 0.65;
                }

                color.rgb += _RimColor.rgb * rimGlow;
                return color;
            }
            ENDCG
        }
    }
    Fallback "Sprites/Default"
}
