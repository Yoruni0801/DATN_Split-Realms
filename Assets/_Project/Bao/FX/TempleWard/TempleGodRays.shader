Shader "Custom/2D/Temple God Rays"
{
    Properties
    {
        _RayColor ("Ray Color", Color) = (1.0, 0.82, 0.45, 1)
        _TintColor ("Secondary Tint", Color) = (0.55, 1.0, 0.65, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.34
        _Radius ("Dome Radius", Range(0.3, 0.5)) = 0.486
        _SDFTex ("Temple SDF", 2D) = "gray" {}
        _SDFRange ("SDF Encode Range", Float) = 0.16
        _Standoff ("Surface Standoff", Range(0.0, 0.08)) = 0.036
        _Tilt ("Beam Tilt", Range(-0.6, 0.6)) = 0.30
        _Speed ("Shimmer Speed", Range(0, 2)) = 0.35
        _TopDownCut ("Top Down Emergence Cut", Range(0, 1)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
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
            #include "UnityCG.cginc"

            float4 _RayColor;
            float4 _TintColor;
            float _Opacity;
            float _Radius;
            float _Tilt;
            float _Speed;
            sampler2D _SDFTex;
            float _SDFRange;
            float _Standoff;
            float _TopDownCut;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                return frac(p * (p + p));
            }

            float noise11(float x)
            {
                float i = floor(x);
                float f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(hash11(i), hash11(i + 1.0), f);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float time = _Time.y * _Speed;
                float2 p = i.uv - 0.5;

                // ward-interior mask straight from the temple SDF
                float sd = (tex2D(_SDFTex, i.uv).r - 0.5) * 2.0 * _SDFRange;
                float d = sd - _Standoff;
                float mask = 1.0 - smoothstep(-0.03, 0.0, d);

                // beam-perpendicular coordinate (tilted parallel shafts)
                float2 beamDir = normalize(float2(_Tilt, -1.0));
                float coord = dot(p, float2(-beamDir.y, beamDir.x));

                // layered shaft bands — sharp distinct beams, each drifting at its own pace
                float b1 = pow(saturate(sin(coord * 30.0 + time * 0.9) * 0.5 + 0.5), 9.0);
                float b2 = pow(saturate(sin(coord * 17.0 - time * 0.55 + 1.7) * 0.5 + 0.5), 12.0);
                float b3 = pow(saturate(sin(coord * 47.0 + time * 0.35 + 3.9) * 0.5 + 0.5), 15.0);
                float bands = b1 * 0.50 + b2 * 0.55 + b3 * 0.30;

                // slow cloud-cover breathing per shaft region
                float cover = noise11(coord * 9.0 + time * 0.30);
                bands *= 0.30 + cover * 0.95;

                // shafts pour from the canopy: strong up top, dissolving low
                float fadeV = smoothstep(-0.42, 0.22, p.y);
                // thin out toward the rim sides so beams stay inside the dome read
                float fadeSide = 1.0 - smoothstep(0.30, 0.48, abs(p.x));

                // Top-down emergence cutoff
                float cutoffLine = 1.0 - saturate(_TopDownCut);
                float emergenceMask = smoothstep(-0.03, 0.04, i.uv.y - cutoffLine);

                float a = bands * fadeV * fadeSide * mask * _Opacity * emergenceMask;

                float3 col = lerp(_RayColor.rgb, _TintColor.rgb, cover * 0.30);
                return fixed4(col, a);
            }
            ENDCG
        }
    }
}
