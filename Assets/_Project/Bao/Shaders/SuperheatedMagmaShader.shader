Shader "Bao/SuperheatedMagmaShader"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _MagmaColor ("Magma Base Color", Color) = (1.0, 0.18, 0.0, 1.0)
        _WhiteHotColor ("White-Hot Heat Color", Color) = (1.0, 0.95, 0.5, 1.0)
        _FlowSpeed ("Drip Flow Speed", Float) = 3.0
        _HeatStrength ("Lava Heat Strength", Range(0, 5)) = 0.0
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
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
            };

            fixed4 _Color;
            sampler2D _MainTex;
            fixed4 _MagmaColor;
            fixed4 _WhiteHotColor;
            float _FlowSpeed;
            float _HeatStrength;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            // Pseudo-random noise for fluid liquid scroll
            float rand(float2 co)
            {
                return frac(sin(dot(co.xy ,float2(12.9898,78.233))) * 43758.5453);
            }

            float fluidNoise(float2 uv)
            {
                float2 ip = floor(uv);
                float2 fp = frac(uv);
                
                float a = rand(ip);
                float b = rand(ip + float2(1.0, 0.0));
                float c = rand(ip + float2(0.0, 1.0));
                float d = rand(ip + float2(1.0, 1.0));

                float2 u = fp * fp * (3.0 - 2.0 * fp);
                return lerp(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                fixed4 mainColor = tex2D(_MainTex, uv) * IN.color;

                if (mainColor.a <= 0.01)
                    return fixed4(0,0,0,0);

                float time = _Time.y * _FlowSpeed;
                
                // scrolling fluid coordinates (flowing downwards vertically to look like molten lava drops sliding down the body)
                float2 flowUV = uv * 6.0;
                flowUV.y += time; // Flow vertically downwards

                float n = fluidNoise(flowUV);
                float n2 = fluidNoise(flowUV * 2.1 - float2(time * 0.2, time * 0.7));
                float combinedFluid = saturate(n * 0.65 + n2 * 0.35);

                // Create a beautiful superheated color transition: at low heat, warm amber orange; at high heat, blistering white-hot neon yellow.
                fixed4 moltenState = lerp(_MagmaColor, _WhiteHotColor, combinedFluid * 0.7);
                fixed4 magmaGlow = moltenState * combinedFluid * _HeatStrength * mainColor.a;

                // Subtle underlying magma radiant heat
                fixed4 radiantHeat = _MagmaColor * 0.15 * _HeatStrength * mainColor.a;

                fixed4 finalColor = mainColor + magmaGlow + radiantHeat;
                finalColor.rgb *= finalColor.a;

                return finalColor;
            }
        ENDCG
        }
    }
}
