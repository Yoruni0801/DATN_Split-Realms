Shader "Bao/DreadwormFrozenCocoonShader"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        [Header(Deep Glacial Colors)]
        [HDR] _GlacialCoreColor ("Glacial Core Color", Color) = (0.12, 0.45, 0.85, 1)
        [HDR] _GlacialDeepColor ("Glacial Deep Color", Color) = (0.02, 0.08, 0.25, 1)
        [HDR] _FresnelColor ("Fresnel Rim Color", Color) = (0.85, 1.85, 2.45, 1)
        [HDR] _CrackGlowColor ("Crack HDR Glow", Color) = (1.45, 2.25, 2.85, 1)

        [Header(Dome Controls)]
        _HealthRatio ("Cocoon Health Ratio (1 to 0)", Range(0, 1)) = 1.0
        _RefractionStrength ("Refraction Distortion", Range(0, 0.2)) = 0.045
        _CrackDensity ("Crack Density", Range(1, 25)) = 7.5
        _PulseSpeed ("Caustic Pulse Speed", Range(0, 10)) = 3.2
        _Aspect ("Dome Aspect Ratio (X/Y)", Range(0.5, 3.0)) = 1.65
        _HitFlash ("Hit Impact Flash", Range(0, 1)) = 0.0
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
            fixed4 _Color;
            fixed4 _GlacialCoreColor;
            fixed4 _GlacialDeepColor;
            fixed4 _FresnelColor;
            fixed4 _CrackGlowColor;
            float _HealthRatio;
            float _RefractionStrength;
            float _CrackDensity;
            float _PulseSpeed;
            float _Aspect;
            float _HitFlash;

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            // High performance 2D Hash
            float2 hash22(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453123);
            }

            // Voronoi Cell Distance for sharp crystalline cracks
            float2 voronoi(float2 uv)
            {
                float2 g = floor(uv);
                float2 f = frac(uv);
                float f1 = 8.0;
                float f2 = 8.0;

                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 lattice = float2(x, y);
                        float2 offset = hash22(g + lattice);
                        float2 r = lattice + offset - f;
                        float d = dot(r, r);

                        if (d < f1)
                        {
                            f2 = f1;
                            f1 = d;
                        }
                        else if (d < f2)
                        {
                            f2 = d;
                        }
                    }
                }
                return float2(sqrt(f1), sqrt(f2));
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 p = (uv - 0.5) * 2.0;
                p.y /= max(0.1, _Aspect);

                float distSq = dot(p, p);
                if (distSq >= 1.0)
                    return fixed4(0, 0, 0, 0);

                float dist = sqrt(distSq);

                // Reconstruct 3D Ellipsoid Normals in 2D
                float z = sqrt(max(0.0, 1.0 - distSq));
                float3 normal = normalize(float3(p.x, p.y, z * 1.35));

                // Time variables
                float time = _Time.y * _PulseSpeed;

                // 2D Glacial Caustic Shimmer Wave
                float caustic = sin(uv.x * 16.0 + time + cos(uv.y * 12.0 - time * 0.8)) *
                                cos(uv.y * 18.0 - time * 1.2);
                caustic = pow(caustic * 0.5 + 0.5, 2.2);

                // Subsurface Optical Depth Gradient (Thấu quang lòng sâu)
                float opticalDepth = pow(1.0 - dist, 1.4);
                fixed3 iceBase = lerp(_GlacialDeepColor.rgb, _GlacialCoreColor.rgb, opticalDepth);
                iceBase += caustic * fixed3(0.25, 0.65, 0.95) * 0.38;

                // Crystalline Facet Lattice
                float2 vDist = voronoi(uv * _CrackDensity + float2(time * 0.04, 0.0));
                float facet = pow(saturate(vDist.y - vDist.x), 2.5);
                iceBase += facet * fixed3(0.12, 0.38, 0.65) * 0.28;

                // Sharp Fresnel Rim & Cryogenic Dispersion
                float viewDot = normal.z;
                float fresnel = pow(1.0 - viewDot, 2.6);
                fixed3 rimColor = _FresnelColor.rgb * fresnel * 1.35;

                // Dynamic Voronoi Crack Network driven by Shell Damage (1.0 -> 0.0)
                float damage = saturate(1.0 - _HealthRatio);
                float crackWidth = 0.035 + damage * 0.065;
                float crackField = abs(vDist.y - vDist.x - 0.02);
                float crackMask = smoothstep(crackWidth, 0.005, crackField) * damage;

                // Crack Glowing Energy (Thermal Leakage)
                fixed3 crackGlow = _CrackGlowColor.rgb * crackMask * (1.2 + damage * 1.8);

                // Final Color Synthesis
                fixed3 finalRGB = iceBase + rimColor + crackGlow;

                // Hit Impact Flash
                float flash = saturate(_HitFlash);
                finalRGB = lerp(finalRGB, fixed3(1.6, 2.2, 2.8), flash * 0.75);

                // Alpha Calculation: Thấu quang tinh khiết nhìn xuyên thân boss
                float alpha = smoothstep(1.0, 0.88, dist);
                float internalAlpha = lerp(0.32, 0.78, fresnel + damage * 0.25);
                float finalAlpha = alpha * internalAlpha * input.color.a;

                // Add crack opacity
                finalAlpha = saturate(finalAlpha + crackMask * 0.45);

                return fixed4(finalRGB * finalAlpha, finalAlpha);
            }
            ENDCG
        }
    }
}
