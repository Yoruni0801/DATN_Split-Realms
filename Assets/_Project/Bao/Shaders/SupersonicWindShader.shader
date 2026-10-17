Shader "Bao/SupersonicWindShader"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _WindColor ("Wind Aura Color", Color) = (0.6, 0.85, 1.0, 1.0)
        _WindSpeed ("Wind Speed", Float) = 5.0
        _Distortion ("Distortion Strength", Float) = 0.08
        _OutlineGlow ("Outline Glow", Float) = 3.0
        _WindPower ("Wind Power Strength", Range(0, 1)) = 0.0
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
            float4 _MainTex_TexelSize;
            fixed4 _WindColor;
            float _WindSpeed;
            float _Distortion;
            float _OutlineGlow;
            float _WindPower;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            // Simple pseudo-noise function
            float noise(float2 uv)
            {
                return frac(sin(dot(uv, float2(127.1, 311.7))) * 43758.5453123);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float time = _Time.y * _WindSpeed;

                // Create scrolling supersonic wind distortion
                float2 windUV = uv;
                windUV.x += sin(uv.y * 10.0 + time) * _Distortion * _WindPower;
                windUV.y += cos(uv.x * 8.0 + time * 1.2) * _Distortion * _WindPower;

                fixed4 mainColor = tex2D(_MainTex, windUV) * IN.color;

                // Compute Fresnel outline based on neighbor alpha sampling
                float alpha = mainColor.a;
                float totalAlpha = 0.0;
                
                // Sample 4 cardinal directions to identify edges
                float2 offsets[4] = {
                    float2(1, 0), float2(-1, 0), float2(0, 1), float2(0, -1)
                };
                
                for(int i = 0; i < 4; i++)
                {
                    float2 sampleUV = windUV + offsets[i] * _MainTex_TexelSize.xy * 2.0;
                    totalAlpha += tex2D(_MainTex, sampleUV).a;
                }

                // If on edge and wind shield is active, inject high-intensity sonic aura
                float edge = saturate((totalAlpha / 4.0) - alpha);
                fixed4 windGlow = _WindColor * _OutlineGlow * edge * _WindPower;

                // Apply a dynamic wind ripple layer over the character sprite
                float windRipple = sin(uv.x * 20.0 - time * 2.0) * cos(uv.y * 15.0 + time) * 0.5 + 0.5;
                fixed4 overlay = _WindColor * windRipple * 0.4 * alpha * _WindPower;

                // Final premultiplied alpha color output
                fixed4 finalColor = mainColor + windGlow + overlay;
                finalColor.rgb *= finalColor.a;

                return finalColor;
            }
        ENDCG
        }
    }
}
