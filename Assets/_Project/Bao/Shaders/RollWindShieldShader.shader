Shader "Bao/RollWindShieldShader"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,0.95,0.82,1)
        _CoreColor ("Core Color", Color) = (0.84,0.68,0.47,1)
        _OuterColor ("Outer Color", Color) = (1,0.95,0.82,1)
        _Alpha ("Alpha", Range(0,1)) = 0.8
        _Radius ("Radius", Range(0,1)) = 0.72
        _Thickness ("Thickness", Range(0.01,0.4)) = 0.15
        _ArcCount ("Arc Count", Range(1,8)) = 4
        _ArcSharpness ("Arc Sharpness", Range(0.5,6)) = 2.4
        _NoiseScale ("Noise Scale", Range(1,16)) = 6
        _SpinSpeed ("Spin Speed", Range(0,20)) = 8
        _RollDirection ("Roll Direction", Float) = 1
        _Stretch ("Stretch", Range(0,2)) = 0.8
        _Charge ("Charge", Range(0,1)) = 0
        _Speed01 ("Speed 01", Range(0,1)) = 0
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

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

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
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            fixed4 _Tint;
            fixed4 _CoreColor;
            fixed4 _OuterColor;
            float _Alpha;
            float _Radius;
            float _Thickness;
            float _ArcCount;
            float _ArcSharpness;
            float _NoiseScale;
            float _SpinSpeed;
            float _RollDirection;
            float _Stretch;
            float _Charge;
            float _Speed01;

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
                float b = hash21(i + float2(1,0));
                float c = hash21(i + float2(0,1));
                float d = hash21(i + float2(1,1));

                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(a, b, u.x) +
                       (c - a) * u.y * (1.0 - u.x) +
                       (d - b) * u.x * u.y;
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.uv = IN.texcoord;
                OUT.color = IN.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.uv * 2.0 - 1.0;
                uv.x *= (1.0 + _Stretch * (_Speed01 * 0.75 + _Charge * 0.2));

                float r = length(uv);
                float angle = atan2(uv.y, uv.x);

                float ring = 1.0 - saturate(abs(r - _Radius) / _Thickness);
                ring = pow(saturate(ring), 1.6);

                float spin = angle * _ArcCount + _Time.y * _SpinSpeed * _RollDirection;
                float arcPattern = sin(spin);
                arcPattern = saturate((arcPattern * 0.5 + 0.5));
                arcPattern = pow(arcPattern, _ArcSharpness);

                float n = noise(uv * _NoiseScale + _Time.y * float2(1.3 * _RollDirection, -0.8));
                float breakup = saturate((n - 0.22) * 1.35);

                float leadingBoost = saturate((uv.x * sign(_RollDirection)) * 0.75 + 0.5);
                float alpha = ring * arcPattern * breakup * _Alpha;
                alpha *= lerp(0.8, 1.25, leadingBoost);
                alpha *= lerp(0.75, 1.1, _Speed01);

                float core = saturate(1.0 - abs(r - (_Radius - _Thickness * 0.35)) / (_Thickness * 0.55));
                fixed3 col = lerp(_CoreColor.rgb, _OuterColor.rgb, core);
                col = lerp(col, _Tint.rgb, arcPattern * 0.65 + leadingBoost * 0.35);

                return fixed4(col, alpha);
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
