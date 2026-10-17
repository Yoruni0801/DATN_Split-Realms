Shader "Custom/SpriteOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        [Header(Outline Settings)]
        _OutlineColor ("Outline Color", Color) = (1, 1, 0, 1) // Default yellow
        _OutlineWidth ("Outline Width", Range(0, 5)) = 1
        _OutlineSmoothness ("Độ mịn của viền", Range(0,1)) = 0.5
        _PulseAmplitude ("Pulse Amplitude", Range(0,1)) = 0.6
        _PulseOffset ("Pulse Offset", Range(0,6.28)) = 0
        _EnablePulse ("Enable Pulse", Float) = 1
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 3

        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
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
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            fixed4 _OutlineColor;
            float _OutlineWidth;
float _OutlineSmoothness;
            float _PulseSpeed;
    float _PulseAmplitude;
    float _PulseOffset;
    float _EnablePulse;
            float4 _MainTex_TexelSize;

            struct VertexInput
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VertexOutput
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            VertexOutput vert(VertexInput v)
            {
                VertexOutput o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color * _RendererColor;
                #ifdef PIXELSNAP_ON
                o.vertex = UnityPixelSnap (o.vertex);
                #endif
                return o;
            }

            fixed4 frag(VertexOutput i) : SV_Target
            {
                fixed4 c = SampleSpriteTexture(i.uv) * i.color;

                // Nếu pixel hiện tại gần trong suốt (alpha < 0.1)
                if (c.a < 0.1)
                {
                    // Lấy alpha của các pixel xung quanh (8 hướng) và thêm các mẫu offset để mượt hơn
                    float2 texel = _MainTex_TexelSize.xy * _OutlineWidth;
        float maxAlpha = 0.0;
        // Mẫu lấy mẫu đa hướng để giảm răng cưa
        float2 offsets[8] = {
            float2(texel.x, 0), float2(-texel.x, 0),
            float2(0, texel.y), float2(0, -texel.y),
            float2(texel.x, texel.y), float2(-texel.x, texel.y),
            float2(texel.x, -texel.y), float2(-texel.x, -texel.y)
        };
        for (int k = 0; k < 8; k++) {
            maxAlpha = max(maxAlpha, SampleSpriteTexture(i.uv + offsets[k]).a);
        }
        // Nửa texel offsets
        float2 half = texel * 0.5;
        float2 halfOffsets[4] = {
            float2(half.x, 0), float2(-half.x, 0),
            float2(0, half.y), float2(0, -half.y)
        };
        for (int k = 0; k < 4; k++) {
            maxAlpha = max(maxAlpha, SampleSpriteTexture(i.uv + halfOffsets[k]).a);
        }
        // Quarter texel offsets
        float2 quarter = texel * 0.25;
        float2 quarterOffsets[4] = {
            float2(quarter.x, quarter.y), float2(-quarter.x, quarter.y),
            float2(quarter.x, -quarter.y), float2(-quarter.x, -quarter.y)
        };
        for (int k = 0; k < 4; k++) {
            maxAlpha = max(maxAlpha, SampleSpriteTexture(i.uv + quarterOffsets[k]).a);
        }
        // Three‑quarter texel offsets để tăng độ mượt
        float2 threeQuarter = texel * 0.75;
        float2 threeQuarterOffsets[4] = {
            float2(threeQuarter.x, threeQuarter.y), float2(-threeQuarter.x, threeQuarter.y),
            float2(threeQuarter.x, -threeQuarter.y), float2(-threeQuarter.x, -threeQuarter.y)
        };
        for (int k = 0; k < 4; k++) {
            maxAlpha = max(maxAlpha, SampleSpriteTexture(i.uv + threeQuarterOffsets[k]).a);
        }
        // Độ mịn viền có thể kiểm soát
        float smoothLow = _OutlineSmoothness * 0.2;
        float smoothHigh = _OutlineSmoothness;
        float outlineAlpha = smoothstep(smoothLow, smoothHigh, maxAlpha - c.a);

                    if (outlineAlpha > 0.0)
                    {
                        fixed4 outlineCol = _OutlineColor;
                        // Hiệu ứng nhấp nháy nếu bật
                        if (_EnablePulse > 0.5)
                        {
                            float pulse = (sin(_Time.y * _PulseSpeed + _PulseOffset) * 0.5) + 0.5;
                            float minAlpha = _PulseAmplitude;
                            outlineCol.a = lerp(minAlpha, 1.0, pulse) * outlineAlpha;
                        }
                        else
                        {
                            outlineCol.a = outlineAlpha;
                        }
                        // Trộn màu viền lên pixel gốc
                        c = lerp(c, outlineCol, outlineCol.a);
                    }
                }
                return c;
            }
            ENDCG
        }
    }
}
