Shader "Custom/ROTA/SoftGlowParticle"
{
    Properties
    {
        _MainTex ("Particle Texture (Optional)", 2D) = "white" {}
        [HDR] _TintColor ("Tint & Intensity (Màu & Độ sáng)", Color) = (1.5, 0.8, 2.0, 1.0)
        _Softness ("Edge Softness (Độ mờ tròn)", Range(0.1, 2.0)) = 1.0
        _CoreBoost ("Core Glow (Độ sáng tâm)", Range(1.0, 5.0)) = 2.5
    }

    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent" 
            "PreviewType"="Plane"
            "RenderPipeline"="UniversalPipeline"
        }

        Blend SrcAlpha One
        ColorMask RGB
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
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _TintColor;
            float _Softness;
            float _CoreBoost;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Procedural smooth radial mask from center (0.5, 0.5)
                float2 centered = i.texcoord - 0.5;
                float dist = length(centered) * 2.0; // 0 at center, 1 at edge
                
                // Inverse quadratic falloff for realistic magical glow
                float radial = saturate(1.0 - dist);
                radial = pow(radial, _Softness);

                // Sample texture if provided
                fixed4 tex = tex2D(_MainTex, i.texcoord);
                float alpha = radial * tex.a * i.color.a;

                // Color calculation with HDR intensity boost
                float3 col = i.color.rgb * _TintColor.rgb * (1.0 + (1.0 - dist) * _CoreBoost);

                return fixed4(col * alpha, alpha);
            }
            ENDCG
        }
    }
    FallBack "Mobile/Particles/Additive"
}
