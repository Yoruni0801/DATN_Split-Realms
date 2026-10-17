Shader "Hidden/SpriteTangentNormal"
{
	Properties
	{
		_NormalLightingSteps("Normal Lighting Steps", Float) = 2
	}

	SubShader
	{
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			float _NormalLightingSteps;

			struct appdata
			{
				float4 vertex : POSITION;
                float3 normal : NORMAL;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
                float3 normal : TEXCOORD0;
			};

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);

				// Convert to view space first, then reinterpret the result in the
				// sprite plane basis so a camera-facing surface becomes a neutral
				// tangent-space normal (0.5, 0.5, 1.0).
				float3 viewNormal = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, v.normal));
				o.normal = normalize(float3(viewNormal.x, viewNormal.y, -viewNormal.z));
				return o;
			}
            
			fixed4 frag (v2f i) : SV_Target
			{
				float3 encodedNormal = i.normal * 0.5 + 0.5;
				float steps = max(1.0, _NormalLightingSteps);
				if (steps > 1.0)
				{
					encodedNormal = round(encodedNormal * (steps - 1.0)) / (steps - 1.0);
					encodedNormal = normalize(encodedNormal * 2.0 - 1.0) * 0.5 + 0.5;
				}

				return half4(encodedNormal, 1.0f);
			}
			ENDCG
		}
	}
}
