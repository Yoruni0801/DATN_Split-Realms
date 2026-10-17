Shader "Custom/ROTA/CosmicVoidNebula"
{
    Properties
    {
        [Header(Base Colors)]
        [HDR] _DeepVoidColor ("Deep Void Color (Nền tối sâu)", Color) = (0.02, 0.01, 0.06, 0.95)
        [HDR] _NebulaColorA ("Nebula Color A (Tím vũ trụ)", Color) = (0.25, 0.05, 0.45, 1.0)
        [HDR] _NebulaColorB ("Nebula Color B (Hồng năng lượng)", Color) = (0.55, 0.10, 0.40, 1.0)
        [HDR] _CyanWhispColor ("Whisp Color (Xanh huyền bí)", Color) = (0.05, 0.35, 0.45, 1.0)
        
        [Header(Stars)]
        [HDR] _StarColor ("Star Color (Màu sao sáng)", Color) = (0.9, 0.85, 1.0, 1.0)
        _StarDensity ("Star Density (Mật độ sao)", Range(5, 80)) = 35.0
        _StarTwinkleSpeed ("Star Twinkle Speed (Tốc độ nhấp nháy)", Float) = 2.5
        _StarBrightness ("Star Brightness (Độ sáng sao)", Float) = 1.2
        
        [Header(Nebula Dynamics)]
        _FlowSpeed ("Flow Speed (Tốc độ mây xoáy)", Float) = 0.03
        _NebulaScale ("Nebula Scale (Tỉ lệ mây)", Float) = 1.8
        _VortexStrength ("Vortex Swirl (Độ xoáy tâm)", Range(0, 2)) = 0.35
        _VignettePower ("Edge Fade / Vignette (Mờ viền)", Range(0.5, 4.0)) = 1.6
        _AlphaMultiplier ("Overall Opacity (Độ trong suốt)", Range(0, 1)) = 0.9
    }

    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent-100" 
            "RenderType"="Transparent" 
            "IgnoreProjector"="True" 
            "RenderPipeline"="UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "CosmicVoid"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float2 worldPos : TEXCOORD1;
            };

            float4 _DeepVoidColor;
            float4 _NebulaColorA;
            float4 _NebulaColorB;
            float4 _CyanWhispColor;
            float4 _StarColor;

            float _StarDensity;
            float _StarTwinkleSpeed;
            float _StarBrightness;

            float _FlowSpeed;
            float _NebulaScale;
            float _VortexStrength;
            float _VignettePower;
            float _AlphaMultiplier;

            // 2D Hash & Noise Functions
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // Fractal Brownian Motion
            float fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;
                for (int i = 0; i < 4; i++)
                {
                    value += amplitude * noise(p * frequency);
                    frequency *= 2.05;
                    amplitude *= 0.5;
                }
                return value;
            }

            // Procedural Twinkling Stars
            float renderStars(float2 uv, float t)
            {
                float2 gridId = floor(uv * _StarDensity);
                float2 gridUv = frac(uv * _StarDensity) - 0.5;

                float n = hash21(gridId);
                if (n < 0.82) return 0.0; // Filter out most cells

                // Random offset within cell
                float2 offset = float2(hash21(gridId + 1.2), hash21(gridId + 3.7)) - 0.5;
                float d = length(gridUv - offset * 0.6);

                // Star core and flare
                float starSize = lerp(0.03, 0.08, hash21(gridId + 5.1));
                float star = smoothstep(starSize, 0.0, d);

                // Cross flare for larger stars
                if (n > 0.95)
                {
                    float flareX = smoothstep(0.015, 0.0, abs(gridUv.y - offset.y * 0.6)) * smoothstep(0.2, 0.0, abs(gridUv.x - offset.x * 0.6));
                    float flareY = smoothstep(0.015, 0.0, abs(gridUv.x - offset.x * 0.6)) * smoothstep(0.2, 0.0, abs(gridUv.y - offset.y * 0.6));
                    star += (flareX + flareY) * 0.5;
                }

                // Twinkle animation
                float twinkle = sin(t * _StarTwinkleSpeed + n * 6.2831) * 0.5 + 0.5;
                return star * twinkle * _StarBrightness;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xy;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 centeredUv = i.uv - 0.5;
                float distCenter = length(centeredUv);

                // Subtle vortex distortion around center
                float angle = atan2(centeredUv.y, centeredUv.x);
                float swirl = sin(distCenter * 4.0 - _Time.y * _FlowSpeed * 2.0) * _VortexStrength;
                angle += swirl;
                float2 swirledUv = float2(cos(angle), sin(angle)) * distCenter;

                // Time coords for flowing gas layers
                float t = _Time.y * _FlowSpeed;
                float2 flowUv1 = swirledUv * _NebulaScale + float2(t * 0.7, t * 0.4);
                float2 flowUv2 = swirledUv * (_NebulaScale * 1.5) - float2(t * 0.5, -t * 0.8);

                // Multi-octave clouds
                float cloud1 = fbm(flowUv1);
                float cloud2 = fbm(flowUv2 + cloud1 * 0.8);
                float cloud3 = fbm(swirledUv * (_NebulaScale * 2.2) + cloud2 * 0.5);

                // Blend cosmic colors
                float3 finalColor = _DeepVoidColor.rgb;
                finalColor = lerp(finalColor, _NebulaColorA.rgb, smoothstep(0.2, 0.7, cloud1) * _NebulaColorA.a);
                finalColor = lerp(finalColor, _NebulaColorB.rgb, smoothstep(0.35, 0.85, cloud2) * _NebulaColorB.a);
                finalColor += _CyanWhispColor.rgb * smoothstep(0.55, 0.95, cloud3) * _CyanWhispColor.a * 0.7;

                // Add stars layer
                float stars = renderStars(i.uv + float2(t * 0.05, 0), _Time.y);
                finalColor += _StarColor.rgb * stars;

                // Vignette edge mask (darken edges to blend naturally with surrounding cave)
                float vignette = 1.0 - smoothstep(0.15, 0.5, distCenter);
                vignette = pow(vignette, _VignettePower);

                float alpha = (_DeepVoidColor.a + (cloud1 + cloud2) * 0.3 + stars) * vignette * _AlphaMultiplier * i.color.a;

                return fixed4(finalColor, saturate(alpha));
            }
            ENDCG
        }
    }
    FallBack "Sprites/Default"
}
