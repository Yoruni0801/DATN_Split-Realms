Shader "Bao/CrystalPhantomShader"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HDR] _GlowColor ("Glow Color", Color) = (0.2, 0.8, 1.0, 1.0)
        _GlowIntensity ("Glow Intensity", Range(0, 10)) = 2.0
        _ShimmerSpeed ("Shimmer Speed", Float) = 4.0
        _CrystalScale ("Crystal Scale", Float) = 15.0
        _ShimmerPower ("Shimmer Power", Range(0, 2)) = 1.0
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
                float3 worldPos : TEXCOORD1;
            };

            fixed4 _Color;
            float4 _GlowColor;
            float _GlowIntensity;
            float _ShimmerSpeed;
            float _CrystalScale;
            float _ShimmerPower;

            // Simple pseudo-noise function
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f*f*(3.0-2.0*f);
                return lerp(lerp(hash(i + float2(0.0,0.0)), hash(i + float2(1.0,0.0)), u.x),
                            lerp(hash(i + float2(0.0,1.0)), hash(i + float2(1.0,1.0)), u.x), u.y);
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                OUT.worldPos = mul(unity_ObjectToWorld, IN.vertex).xyz;
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

                // Crystal refraction / shimmering color noise
                float2 shimmerUV = IN.worldPos.xy * _CrystalScale + float2(_Time.y * _ShimmerSpeed, -_Time.y * _ShimmerSpeed * 0.7);
                float shimmerNoise = noise(shimmerUV);

                // Build sparkling spectrum colors (rainbow/opal crystal effect)
                float3 rainbow = 0.5 + 0.5 * cos(_Time.z * 0.5 + float3(0.0, 2.0, 4.0) + shimmerNoise * 3.1415);
                
                // Fresnel outline/edge glow estimation for 2D sprites
                // We use standard derivatives to find the edges of the sprite alpha
                float edge = 0.0;
                float2 width = float2(0.015, 0.015);
                float aUp = tex2D(_MainTex, IN.texcoord + float2(0, width.y)).a;
                float aDown = tex2D(_MainTex, IN.texcoord - float2(0, width.y)).a;
                float aLeft = tex2D(_MainTex, IN.texcoord - float2(width.x, 0)).a;
                float aRight = tex2D(_MainTex, IN.texcoord + float2(width.x, 0)).a;
                
                // Edge detection
                float edgeVal = (1.0 - aUp) + (1.0 - aDown) + (1.0 - aLeft) + (1.0 - aRight);
                edge = saturate(edgeVal * 1.5) * c.a;

                // Final crystalline blending
                float3 shimmerEff = rainbow * shimmerNoise * _ShimmerPower;
                float3 glowEff = _GlowColor.rgb * _GlowIntensity * (1.0 + edge * 2.0);

                c.rgb = c.rgb * (1.0 + shimmerEff) + (glowEff * edge * c.a);
                c.rgb *= c.a; // Premultiply alpha

                return c;
            }
            ENDCG
        }
    }
}
