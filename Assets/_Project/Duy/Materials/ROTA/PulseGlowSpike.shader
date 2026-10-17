Shader "Custom/ROTA/PulseGlowSpike"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        [Header(Glow Pulse Settings)]
        [HDR] _GlowColor ("Glow Color (Màu phát sáng HDR)", Color) = (1.8, 0.2, 0.35, 1.0)
        _PulseSpeed ("Pulse Speed (Tốc độ nhấp nháy)", Float) = 3.2
        _PulseMin ("Min Glow (Độ sáng tối thiểu)", Range(0, 1)) = 0.2
        _PulseMax ("Max Glow (Độ sáng tối đa)", Range(0.5, 3.0)) = 1.6
        _RimStrength ("Edge / Rim Glow (Độ sáng viền)", Range(0, 5)) = 2.0
        
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
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
            "RenderPipeline"="UniversalPipeline"
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
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
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
            fixed4 _RendererColor;
            float4 _GlowColor;
            float _PulseSpeed;
            float _PulseMin;
            float _PulseMax;
            float _RimStrength;
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color * _RendererColor;
                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap (OUT.vertex);
                #endif
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, IN.texcoord);
                if (tex.a <= 0.01) discard;

                // Sample neighbors for edge detection (Rim Glow)
                float2 offset = _MainTex_TexelSize.xy * 1.5;
                float alphaUp    = tex2D(_MainTex, IN.texcoord + float2(0, offset.y)).a;
                float alphaDown  = tex2D(_MainTex, IN.texcoord - float2(0, offset.y)).a;
                float alphaLeft  = tex2D(_MainTex, IN.texcoord - float2(offset.x, 0)).a;
                float alphaRight = tex2D(_MainTex, IN.texcoord + float2(offset.x, 0)).a;
                float edge = 1.0 - saturate(alphaUp * alphaDown * alphaLeft * alphaRight);

                // Pulse factor
                float pulse = lerp(_PulseMin, _PulseMax, sin(_Time.y * _PulseSpeed) * 0.5 + 0.5);

                // Base sprite color blended with vertex color
                float3 baseColor = tex.rgb * IN.color.rgb;

                // Add glowing rim on edges and subtle body glow
                float3 emission = _GlowColor.rgb * (edge * _RimStrength + 0.25) * pulse;

                float3 finalColor = baseColor + emission * tex.a;
                float finalAlpha = tex.a * IN.color.a;

                return fixed4(finalColor, finalAlpha);
            }
            ENDCG
        }
    }
    FallBack "Sprites/Default"
}
