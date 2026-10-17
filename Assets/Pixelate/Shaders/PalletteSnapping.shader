Shader "Pixelate/AutoPalletteSnapping"
{
    Properties {
		_MainTex("Albedo", 2D) = "white" {}
		_Pallette("Palette", 2D) = "white"{}  // Note: Palette should always be set to clamp and no filter (point)

		[Toggle(useIgnoreMap)]
		_UseIgnoreMap("Use IgnoreMap", Float) = 0
		_IgnoreMap("Ignore Map. Alpha channel is used to determine ignore amount.", 2D) = "white" {}
		

	}
	
	SubShader {
		
		Pass{

			Tags{
			}

			Blend SrcAlpha OneMinusSrcAlpha
			ZTest Always Cull Off ZWrite Off
			
				
			CGPROGRAM
			#pragma target 4.0 // need 4.0 to get real for-loops.

			#include "UnityCG.cginc"


			#pragma vertex vert
			#pragma fragment frag
				
			#pragma shader_feature_local useIgnoreMap

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				float4 col : COLOR;
			};
					
			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
				float4 col : COLOR0;
			};
				

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				o.col = v.col;
				return o;
			}
					
			sampler2D _MainTex;
			sampler2D _Pallette;
			float4 _Pallette_TexelSize; // 1/width, 1/height, width, height for _Pallette
#ifdef useIgnoreMap
			sampler2D _IgnoreMap;
#endif


			float4 frag(v2f i) : SV_Target
			{
				float4 texCol = tex2D(_MainTex, i.uv);
				if (texCol.a < 0.5) {
					return float4(0, 0, 0, 0);
				}

				float diff;
				float3 palletteSampleI;
				float minDiff = 99999;
				int minDiffIndex = 0;

				float distR, distG, distB;

				[loop]
				for (int index = 0; index < _Pallette_TexelSize.z; index++) {
					// doing a basic non-human eye hue sensitivity weighted approach here, should be good enough since palette elements are gonna be pretty distinct anyways.
					palletteSampleI = tex2D(_Pallette, float2(index * _Pallette_TexelSize.r + 0.00001f, 0)); // Make sure palette texture is set to Clamp, otherwise this fails from the 0.00001f. We're adding it to avoid floating point errors giving us the wrong pixel.
					distR = texCol.r - palletteSampleI.r;
					distG = texCol.g - palletteSampleI.g;
					distB = texCol.b - palletteSampleI.b;
					diff = sqrt(distR*distR + distG*distG + distB*distB);
					if (diff < minDiff) {
						minDiff = diff;
						minDiffIndex = index;
					}
				}

				float3 sampledPallette = tex2D(_Pallette, float2(minDiffIndex * _Pallette_TexelSize.r + 0.00001f, 0));

#ifdef useIgnoreMap
				half ignoreVal = tex2D(_IgnoreMap, i.uv).a;
				return float4(sampledPallette.rgb * ignoreVal + texCol.rgb * (1 - ignoreVal), 1.0);
#else
				return float4(sampledPallette.rgb, 1);
#endif
			}
				
			ENDCG
				
		}
				
			
	}


}
