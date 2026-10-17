Shader "Custom/2D/Acid Water"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HDR] _MainColor ("Main Acid Color", Color) = (0.35, 1.0, 0.02, 1)
        [HDR] _DeepColor ("Deep Acid Color", Color) = (0.0, 0.25, 0.03, 1)
        [HDR] _EdgeColor ("Edge Glow Color", Color) = (0.75, 1.0, 0.02, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.82
        _RippleSpeed ("Ripple Speed", Range(0, 5)) = 1.25
        _RippleScale ("Ripple Scale", Range(1, 80)) = 24
        _DistortionStrength ("Distortion Strength", Range(0, 0.12)) = 0.035
        _EdgeWidth ("Edge Width", Range(0.001, 0.2)) = 0.055
        _BubbleStrength ("Internal Bubble Strength", Range(0, 2)) = 0.75
        _SurfaceFoam ("Surface Foam", Range(0, 2)) = 0.85
        _MaskPower ("Mask Power", Range(0.2, 4)) = 1
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
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainColor;
            float4 _DeepColor;
            float4 _EdgeColor;
            float _Alpha;
            float _RippleSpeed;
            float _RippleScale;
            float _DistortionStrength;
            float _EdgeWidth;
            float _BubbleStrength;
            float _SurfaceFoam;
            float _MaskPower;

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
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float time = _Time.y * _RippleSpeed;
                float waveA = noise(i.uv * _RippleScale + float2(time, time * 0.37));
                float waveB = noise(i.uv * (_RippleScale * 0.55) + float2(-time * 0.28, time * 0.64));
                float ripple = saturate((waveA * 0.62 + waveB * 0.48) * 1.15);
                float2 distortion = float2(waveA - 0.5, waveB - 0.5) * _DistortionStrength;

                fixed4 sprite = tex2D(_MainTex, i.uv + distortion) * i.color;
                float mask = pow(saturate(sprite.a), _MaskPower);
                float edgeMask = smoothstep(0.01, _EdgeWidth, mask) * (1 - smoothstep(_EdgeWidth, _EdgeWidth * 2.4, mask));
                float surface = smoothstep(0.36, 0.92, i.uv.y + ripple * 0.12);
                float bubbleCells = noise(i.uv * (_RippleScale * 1.7) + float2(time * 0.18, -time * 0.9));
                float internalBubbles = smoothstep(0.76, 0.98, bubbleCells) * _BubbleStrength * mask;
                float foam = smoothstep(0.54, 0.92, ripple) * _SurfaceFoam * mask;

                float depth = saturate(i.uv.y * 0.85 + ripple * 0.18);
                float3 color = lerp(_DeepColor.rgb, _MainColor.rgb, depth);
                color += _EdgeColor.rgb * (edgeMask * 1.35 + foam * 0.26 + internalBubbles * 0.32 + surface * 0.08);

                float alpha = saturate(mask * _Alpha + edgeMask * 0.22 + foam * 0.08);
                return fixed4(color, alpha * sprite.a);
            }
            ENDCG
        }
    }
}
