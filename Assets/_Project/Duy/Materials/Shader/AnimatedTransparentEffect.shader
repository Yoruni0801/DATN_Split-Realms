Shader "Custom/2D/Effect_SubtleMotion_NoDistort"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        _Color ("Base Tint", Color) = (1,1,1,1)
        _HighlightColor ("Highlight Color", Color) = (1,0.85,0.4,1)

        _Alpha ("Base Alpha", Range(0,1)) = 1
        _Brightness ("Base Brightness", Range(0,3)) = 1

        _PulseSpeed ("Pulse Speed", Range(0,10)) = 2
        _PulseAmount ("Pulse Amount", Range(0,1)) = 0.08

        _FlowSpeed ("Flow Speed", Range(0,10)) = 1.5
        _FlowAmount ("Flow Amount", Range(0,2)) = 0.2
        _FlowScale ("Flow Scale", Range(1,50)) = 12

        _ShimmerSpeed ("Shimmer Speed", Range(0,10)) = 2
        _ShimmerAmount ("Shimmer Amount", Range(0,2)) = 0.15
        _ShimmerScale ("Shimmer Scale", Range(1,50)) = 18

        _AlphaFlickerSpeed ("Alpha Flicker Speed", Range(0,20)) = 6
        _AlphaFlickerAmount ("Alpha Flicker Amount", Range(0,1)) = 0.04

        _AlphaCutoff ("Alpha Cutoff", Range(0,1)) = 0.01
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True"
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
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;

            fixed4 _Color;
            fixed4 _HighlightColor;

            float _Alpha;
            float _Brightness;

            float _PulseSpeed;
            float _PulseAmount;

            float _FlowSpeed;
            float _FlowAmount;
            float _FlowScale;

            float _ShimmerSpeed;
            float _ShimmerAmount;
            float _ShimmerScale;

            float _AlphaFlickerSpeed;
            float _AlphaFlickerAmount;

            float _AlphaCutoff;

            struct appdata
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 uv       : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                float4 color    : COLOR;
                float2 uv       : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y;

                // Sample ảnh gốc - KHÔNG bóp méo UV
                fixed4 tex = tex2D(_MainTex, i.uv);

                // Khóa trong vùng alpha gốc
                float baseAlpha = tex.a;
                if (baseAlpha <= _AlphaCutoff)
                    discard;

                // Nhấp nháy tổng thể rất nhẹ
                float pulse = 1.0 + sin(t * _PulseSpeed) * _PulseAmount;

                // Dòng sáng mềm chạy bên trong ảnh
                float flow1 = sin(i.uv.y * _FlowScale + t * _FlowSpeed);
                float flow2 = cos(i.uv.x * (_FlowScale * 0.85) - t * (_FlowSpeed * 1.15));
                float flow = (flow1 + flow2) * 0.5;
                flow = flow * 0.5 + 0.5; // đưa về 0..1

                // Shimmer mịn hơn để tạo cảm giác chuyển động nhẹ
                float shimmer = sin((i.uv.x + i.uv.y) * _ShimmerScale - t * _ShimmerSpeed);
                shimmer = shimmer * 0.5 + 0.5;

                // Độ sáng phụ thêm, nhưng chỉ trong ảnh
                float highlightMask = flow * _FlowAmount + shimmer * _ShimmerAmount;

                // Alpha rung nhẹ, không làm đổi silhouette
                float alphaFlicker = 1.0 + sin(t * _AlphaFlickerSpeed + i.uv.y * 10.0) * _AlphaFlickerAmount;

                float finalAlpha = saturate(baseAlpha * _Alpha * alphaFlicker);

                fixed3 baseCol = tex.rgb * _Color.rgb * _Brightness;
                fixed3 highlight = _HighlightColor.rgb * highlightMask;

                fixed3 finalCol = baseCol * pulse + highlight;

                // khóa màu theo alpha gốc để không bị tràn ra ngoài
                finalCol *= baseAlpha;

                return fixed4(finalCol, finalAlpha);
            }
            ENDCG
        }
    }
}