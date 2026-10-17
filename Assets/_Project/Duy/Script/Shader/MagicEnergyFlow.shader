Shader "Custom/MagicEnergyFlow"
{
    Properties
    {
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        
        _Speed ("Tốc độ (Speed)", Float) = 0.3
        _NoiseScale ("Độ nhiễu (Noise Scale)", Float) = 1.0
        _Intensity ("Cường độ sáng (Intensity)", Float) = 1.0
        
        [HDR] _CoreColor ("Màu lõi (Trắng/Cyan)", Color) = (0.8, 0.95, 1.0, 1.0)
        [HDR] _MidColor ("Màu giữa (Xanh dương)", Color) = (0.2, 0.5, 1.0, 1.0)
        [HDR] _DarkColor ("Màu viền (Xanh đậm)", Color) = (0.05, 0.1, 0.6, 1.0)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex; 
            float4 _MainTex_ST;
            
            float _Speed;
            float _NoiseScale;
            float _Intensity;
            float4 _CoreColor;
            float4 _MidColor;
            float4 _DarkColor;

            float hash(float2 p) {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            float noise(float2 p) {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash(i + float2(0.0, 0.0)), hash(i + float2(1.0, 0.0)), u.x),
                            lerp(hash(i + float2(0.0, 1.0)), hash(i + float2(1.0, 1.0)), u.x), u.y);
            }

            float fbm(float2 p) {
                float v = 0.0;
                float a = 0.5;
                float c = cos(0.5);
                float s = sin(0.5);
                
                for (int i = 0; i < 5; ++i) {
                    v += a * noise(p);
                    float2 p2 = float2(p.x * c - p.y * s, p.x * s + p.y * c);
                    p = p2 * 2.0 + float2(100.0, 100.0);
                    a *= 0.5;
                }
                return v;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 centeredUv = i.uv * 2.0 - 1.0;
                
                float2 flowUV = centeredUv * _NoiseScale + float2(_Time.y * -_Speed, 0.0);
                
                float n1 = fbm(flowUV);
                flowUV.y += (n1 - 0.5) * 0.8;
                
                float n2 = fbm(flowUV * 1.5 - float2(_Time.y * (_Speed * 0.5), 0.0));

                float maskY = 1.0 - smoothstep(0.0, 0.8, abs(centeredUv.y)); 
                float maskX = 1.0 - smoothstep(0.4, 1.0, abs(centeredUv.x)); 
                float mask = maskY * maskX;

                float intensity = n2 * mask;
                intensity = pow(intensity, 1.8) * _Intensity;

                float3 col = lerp(float3(0,0,0), _DarkColor.rgb, smoothstep(0.0, 0.2, intensity));
                col = lerp(col, _MidColor.rgb, smoothstep(0.2, 0.5, intensity));
                col = lerp(col, _CoreColor.rgb, smoothstep(0.5, 1.0, intensity));

                float alpha = saturate(intensity * 1.5); 

                fixed4 texColor = tex2D(_MainTex, i.uv);
                
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
}