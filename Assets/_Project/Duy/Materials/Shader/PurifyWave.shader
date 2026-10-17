Shader "Custom/PurifyWave"
{
    Properties
    {
        _Center ("Tâm lan sáng UV", Vector) = (0.5, 0.5, 0, 0)
        _Progress ("Tiến trình lan", Range(0, 1)) = 0

        _WaveWidth ("Độ dày vòng sáng", Range(0.01, 0.5)) = 0.12
        _LightColor ("Màu ánh sáng", Color) = (1.0, 0.85, 0.25, 1.0)
        _CoreColor ("Màu lõi sáng", Color) = (1.0, 1.0, 0.8, 1.0)

        _Strength ("Độ mạnh ánh sáng", Range(0, 8)) = 3.0
        _FillStrength ("Độ sáng vùng đã thanh tẩy", Range(0, 2)) = 0.45
        _MaxRadius ("Bán kính lan tối đa", Range(0.5, 3.0)) = 1.6
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent+100"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
        }

        Cull Off
        ZWrite Off
        Lighting Off
        Blend SrcAlpha One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            float4 _Center;
            float _Progress;

            float _WaveWidth;
            fixed4 _LightColor;
            fixed4 _CoreColor;

            float _Strength;
            float _FillStrength;
            float _MaxRadius;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float dist = distance(uv, _Center.xy);

                float progress = saturate(_Progress);
                float radius = progress * _MaxRadius;

                // Vòng sáng chính đang lan ra
                float wave = 1.0 - smoothstep(0.0, _WaveWidth, abs(dist - radius));

                // Vùng phía sau vòng sáng được phủ ánh sáng nhẹ
                float filledArea = 1.0 - smoothstep(radius - 0.25, radius, dist);
                filledArea = saturate(filledArea);

                // Cho hiệu ứng tự mờ dần ở cuối
                float fadeIn = smoothstep(0.0, 0.08, progress);
                float fadeOut = 1.0 - smoothstep(0.85, 1.0, progress);
                float life = fadeIn * fadeOut;

                float intensity = wave * _Strength + filledArea * _FillStrength;
                intensity *= life;

                fixed3 color = lerp(_LightColor.rgb, _CoreColor.rgb, wave);

                fixed4 finalColor;
                finalColor.rgb = color * intensity;
                finalColor.a = saturate(intensity);

                return finalColor;
            }
            ENDCG
        }
    }
}