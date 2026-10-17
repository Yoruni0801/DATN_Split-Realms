Shader "Bao/StealthHologramShader"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Dissolve ("Dissolve Amount", Range(0, 1)) = 0.0
        [HDR] _EdgeColor ("Neon Edge Color", Color) = (0.0, 1.0, 0.5, 1.0)
        _EdgeWidth ("Edge Width", Range(0, 0.5)) = 0.08
        _GridScale ("Digital Grid Scale", Float) = 40.0
        _GlitchSpeed ("Glitch Speed", Float) = 15.0
        _GlitchIntensity ("Glitch Intensity", Range(0, 1)) = 0.15
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
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
            #pragma multi_compile _ PIXELSNAP_ON
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
                float4 screenPos : TEXCOORD1;
            };

            fixed4 _Color;
            float _Dissolve;
            float4 _EdgeColor;
            float _EdgeWidth;
            float _GridScale;
            float _GlitchSpeed;
            float _GlitchIntensity;

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                
                // Add minor horizontal glitch/offset based on time if dissolving
                float glitch = sin(_Time.y * _GlitchSpeed) * cos(_Time.y * _GlitchSpeed * 1.5) * _GlitchIntensity * saturate(_Dissolve * (1.0 - _Dissolve));
                float4 shiftedPos = IN.vertex;
                shiftedPos.x += glitch * 0.15;

                OUT.vertex = UnityObjectToClipPos(shiftedPos);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                OUT.screenPos = ComputeScreenPos(OUT.vertex);
                
                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap (OUT.vertex);
                #endif
                return OUT;
            }

            sampler2D _MainTex;

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 c = tex2D (_MainTex, IN.texcoord);
                if (c.a == 0.0) discard;
                c *= IN.color;

                // Screen-space digital blocky/grid noise for stealth hologram look
                float2 screenUV = IN.screenPos.xy / max(IN.screenPos.w, 0.0001);
                float2 gridUV = floor(screenUV * _GridScale) / _GridScale;
                float noiseVal = hash(gridUV + float2(sin(_Time.x), cos(_Time.x)));

                // Calculate dissolve mask
                // Combining sprite alpha and the noise
                float dissolveMask = saturate(c.a * (1.0 - _Dissolve) + (noiseVal - 0.5) * 0.3);
                
                // Dissolve step
                if (dissolveMask < 0.15 * _Dissolve)
                {
                    discard;
                }

                // Digital neon outline border
                float borderMask = step(dissolveMask, 0.15 * _Dissolve + _EdgeWidth);
                float3 borderGlow = _EdgeColor.rgb * borderMask * 3.0;

                // Cyber horizontal scanlines
                float scanline = sin(screenUV.y * 300.0 + _Time.y * 10.0) * 0.15 + 0.85;

                // Apply colors and scanlines
                c.rgb = lerp(c.rgb * scanline, borderGlow, borderMask);
                c.rgb *= c.a; // Premultiply alpha

                // Fade out transparency as dissolve progresses
                c *= (1.0 - _Dissolve * 0.7);

                return c;
            }
            ENDCG
        }
    }
}
