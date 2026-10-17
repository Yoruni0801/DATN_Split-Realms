Shader "Custom/2D/Boss Summon Portal Motes V2"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _MoteColor ("Mote Color", Color) = (2.8, 1.05, 0.08, 1)
        [HDR] _TrailColor ("Trail Color", Color) = (1.4, 0.035, 0.55, 1)

        _Alpha ("Alpha", Range(0, 1)) = 0.72
        _Speed ("Flow Speed", Range(-3, 3)) = 0.75
        _InnerRadius ("Inner Radius", Range(0.03, 0.3)) = 0.1
        _OuterRadius ("Outer Radius", Range(0.25, 0.49)) = 0.45
        _MoteSize ("Mote Size", Range(0.002, 0.04)) = 0.009
        _TrailLength ("Trail Length", Range(0.005, 0.12)) = 0.038
        _Intensity ("Intensity", Range(0, 4)) = 1.25
        _ArcWidth ("Outer Arc Width", Range(0.002, 0.04)) = 0.009
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+1"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha One

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            #define TAU 6.28318530718

            sampler2D _MainTex;
            float4 _MoteColor;
            float4 _TrailColor;
            float _Alpha;
            float _Speed;
            float _InnerRadius;
            float _OuterRadius;
            float _MoteSize;
            float _TrailLength;
            float _Intensity;
            float _ArcWidth;

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
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float Hash11(float value)
            {
                return frac(sin(value * 127.1) * 43758.5453123);
            }

            float2 SpiralPosition(float seed, float phase, float time)
            {
                float radius = lerp(_OuterRadius, _InnerRadius, phase);
                float turns = lerp(1.25, 2.15, Hash11(seed + 3.7));
                float direction = Hash11(seed + 8.1) > 0.22 ? 1.0 : -1.0;
                float angle = seed * TAU
                    + phase * turns * TAU * direction
                    + time * lerp(0.12, 0.34, Hash11(seed + 1.9));
                return float2(cos(angle), sin(angle)) * radius;
            }

            float SegmentDistance(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a;
                float2 ba = b - a;
                float projection = saturate(dot(pa, ba) / max(dot(ba, ba), 0.000001));
                return length(pa - ba * projection);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 sprite = tex2D(_MainTex, i.uv);
                float2 p = i.uv - 0.5;
                float time = _Time.y * _Speed;
                float3 color = 0.0;
                float alpha = 0.0;

                [unroll]
                for (int moteIndex = 0; moteIndex < 12; moteIndex++)
                {
                    float index = (float)moteIndex + 1.0;
                    float seed = Hash11(index * 19.37);
                    float travelSpeed = lerp(0.055, 0.105, Hash11(index * 7.13));
                    float phase = frac(seed + time * travelSpeed);
                    float previousPhase = max(phase - _TrailLength, 0.0);

                    float2 motePosition = SpiralPosition(seed, phase, time);
                    float2 trailPosition = SpiralPosition(seed, previousPhase, time);
                    float lifeFade = smoothstep(0.03, 0.16, phase)
                        * (1.0 - smoothstep(0.78, 1.0, phase));
                    float sizeVariation = lerp(0.65, 1.45, Hash11(index * 5.71));
                    float moteSize = _MoteSize * sizeVariation;

                    float moteDistance = length(p - motePosition);
                    float moteCore = 1.0 - smoothstep(
                        moteSize * 0.25,
                        moteSize,
                        moteDistance);
                    float moteGlow = 1.0 - smoothstep(
                        moteSize,
                        moteSize * 4.5,
                        moteDistance);

                    float trailDistance = SegmentDistance(p, trailPosition, motePosition);
                    float trail = 1.0 - smoothstep(
                        moteSize * 0.35,
                        moteSize * 1.8,
                        trailDistance);
                    trail *= 1.0 - smoothstep(
                        moteSize * 1.4,
                        moteSize * 7.0,
                        length(p - motePosition));

                    color += _TrailColor.rgb * trail * lifeFade * 0.45;
                    color += _MoteColor.rgb
                        * (moteCore * 1.6 + moteGlow * 0.38)
                        * lifeFade;
                    alpha = max(alpha, (moteCore + moteGlow * 0.45 + trail * 0.35) * lifeFade);
                }

                float radiusFromCenter = length(p);
                float angle = atan2(p.y, p.x);
                float arcDistance = abs(radiusFromCenter - _OuterRadius * 0.94);
                float arcRing = 1.0 - smoothstep(
                    _ArcWidth,
                    _ArcWidth * 3.5,
                    arcDistance);
                float arcBreakup = pow(saturate(
                    0.5 + 0.5 * sin(
                        angle * 13.0
                        - time * 2.4
                        + sin(angle * 5.0 + time) * 2.0)), 12.0);
                float arc = arcRing * arcBreakup;

                color += _MoteColor.rgb * arc * 0.85;
                alpha = max(alpha, arc * 0.75);

                color *= _Intensity * i.color.rgb;
                alpha *= sprite.a * _Alpha * i.color.a;
                return fixed4(color, saturate(alpha));
            }
            ENDCG
        }
    }
}
