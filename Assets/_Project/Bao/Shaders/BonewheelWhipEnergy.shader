Shader "Bao/BonewheelWhipEnergy"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _CoreColor ("Core Color", Color) = (0.82, 0.62, 0.24, 1)
        _EdgeColor ("Edge Color", Color) = (0.48, 0.43, 0.27, 1)
        _PulseCenter ("Pulse Center", Range(0, 1)) = 0
        _PulseWidth ("Pulse Width", Range(0.01, 0.5)) = 0.075
        _Intensity ("Intensity", Range(0, 3)) = 0
        _PulseDirection ("Pulse Direction", Float) = 1
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
            fixed4 _CoreColor;
            fixed4 _EdgeColor;
            float _PulseCenter;
            float _PulseWidth;
            float _Intensity;
            float _PulseDirection;

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 source = tex2D(_MainTex, input.texcoord) * input.color;
                if (source.a < 0.01) discard;

                float pulseUv = _PulseDirection > 0.0 ? input.texcoord.x : 1.0 - input.texcoord.x;
                float pulse = 1.0 - smoothstep(0.0, _PulseWidth, abs(pulseUv - _PulseCenter));
                float edgeMask = smoothstep(0.0, 0.16, pulseUv) * (1.0 - smoothstep(0.82, 1.0, pulseUv));
                float shimmer = pow(saturate(sin(input.texcoord.y * 18.0 + _Time.y * 10.0) * 0.5 + 0.5), 6.0);
                float glow = saturate(_Intensity * (pulse * 0.88 + shimmer * pulse * 0.12 + 0.035 * edgeMask));
                fixed3 energy = lerp(_EdgeColor.rgb, _CoreColor.rgb, saturate(pulse * 1.2));
                return fixed4(energy * source.a * glow, source.a * glow);
            }
            ENDCG
        }
    }
}
