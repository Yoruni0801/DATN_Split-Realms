Shader "Custom/ROTA/GhostAfterimage"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite/Mesh Texture", 2D) = "white" {}
        [HDR] _Color ("Ghost Tint (HDR)", Color) = (1, 1, 1, 1)
        _AlphaMultiplier ("Alpha Multiplier", Range(0, 1)) = 1.0
        _RimPower ("Rim / Edge Boost", Range(0.1, 5.0)) = 1.5
    }

    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent+50" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent" 
            "PreviewType"="Plane"
            "RenderPipeline"="UniversalPipeline"
        }

        Blend SrcAlpha One
        Cull Off
        Lighting Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
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
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _AlphaMultiplier;
            float _RimPower;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = TRANSFORM_TEX(IN.texcoord, _MainTex);
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, IN.texcoord);
                
                // Color with HDR tint and alpha fade
                float alpha = tex.a * IN.color.a * _AlphaMultiplier;
                if (alpha <= 0.005) discard;

                float3 emission = tex.rgb * IN.color.rgb;
                return fixed4(emission * alpha, alpha);
            }
            ENDCG
        }
    }
    FallBack "Mobile/Particles/Additive"
}
