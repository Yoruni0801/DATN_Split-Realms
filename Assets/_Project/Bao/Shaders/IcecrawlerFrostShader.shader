Shader "Bao/IcecrawlerFrostShader"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FrostColor ("Frost Crystal Glow", Color) = (0.7,0.9,1,1)
        _SpikeLevel ("Spike Amount", Range(0, 10)) = 0.0
        _ChargeIntensity ("Charge Glow", Range(0, 5)) = 0.0
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
        Blend One OneMinusSrcAlpha

        Pass
        {
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
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
            fixed4 _FrostColor;
            float _SpikeLevel;
            float _ChargeIntensity;

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 uv = input.texcoord;
                fixed4 color = tex2D(_MainTex, uv) * input.color;
                if (color.a < 0.01) discard;

                float spikeWave = sin(uv.x * 30.0) * cos(uv.y * 30.0 + _Time.y * 3.0) * 0.5 + 0.5;
                float frostMask = smoothstep(0.4, 0.7, spikeWave);
                fixed3 frost = _FrostColor.rgb * frostMask * saturate(_SpikeLevel * 0.4);

                float outlineNoise = sin(uv.y * 50.0 + _Time.z * 2.0) * 0.5 + 0.5;
                float jaggedOutline = smoothstep(0.02, 0.0, abs(color.a - 0.5) - outlineNoise * 0.01);
                frost += _FrostColor.rgb * jaggedOutline * _SpikeLevel * 0.6;

                float pulse = 0.5 + 0.5 * sin(_Time.y * 18.0);
                float chargeBand = pow(saturate(sin((uv.x + uv.y) * 42.0 - _Time.y * 14.0) * 0.5 + 0.5), 7.0);
                float chargeMask = saturate(frostMask * 0.45 + chargeBand * 0.75 + pulse * 0.12);
                frost += _FrostColor.rgb * chargeMask * _ChargeIntensity;

                color.rgb += frost;
                color.rgb *= color.a;
                return color;
            }
        ENDCG
        }
    }
}