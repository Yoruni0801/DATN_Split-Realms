Shader "Bao/PoisonMossShader"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HDR] _GlowColor ("Glow Color", Color) = (1.0, 0.9, 0.5, 1.0)
        _PulseSpeed ("Pulse Speed", Float) = 2.0
        _ShimmerScale ("Shimmer Scale", Float) = 15.0
        _ShimmerSpeed ("Shimmer Speed", Float) = 1.0
        _TriggerGlow ("Trigger Glow Boost", Range(0, 10)) = 0.0
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
            };

            fixed4 _Color;
            sampler2D _MainTex;
            float4 _GlowColor;
            float _PulseSpeed;
            float _ShimmerScale;
            float _ShimmerSpeed;
            float _TriggerGlow;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap (OUT.vertex);
                #endif
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 c = tex2D (_MainTex, IN.texcoord);
                if (c.a <= 0.01) return fixed4(0,0,0,0);
                c *= IN.color;

                // Magical Shimmering effect (like pollen/energy moving over the thorns/flowers)
                float shimmerInput = (IN.texcoord.x + IN.texcoord.y) * _ShimmerScale - _Time.y * _ShimmerSpeed;
                float shimmer = sin(shimmerInput) * 0.5 + 0.5;
                shimmer = pow(shimmer, 2.0); // Soften shimmer

                // Breathing pulse
                float t = _Time.y * _PulseSpeed;
                float pulse = sin(t) * 0.2 + 0.8; // Gentle pulse between 0.6 and 1.0

                // Base magical glow
                float baseGlow = (shimmer * 0.4 + 0.6) * pulse;

                // Burst of energy when trap snaps
                float triggerGlow = _TriggerGlow * (pulse * 0.5 + 0.5);

                float totalIntensity = baseGlow * 0.6 + triggerGlow;

                // Golden/Yellow emissive light
                fixed3 emission = _GlowColor.rgb * totalIntensity * c.a;
                
                fixed4 finalColor;
                finalColor.rgb = c.rgb + emission;
                finalColor.a = c.a;

                // Premultiply alpha for transparent blend
                finalColor.rgb *= finalColor.a;

                return finalColor;
            }
            ENDCG
        }
    }
}
