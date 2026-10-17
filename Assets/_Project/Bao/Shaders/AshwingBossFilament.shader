Shader "Bao/AshwingBossFilament"
{
    Properties { _Color("Tint",Color)=(1,1,1,1) _Softness("Softness",Range(.1,4))=1.6 }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" }
        Cull Off ZWrite Off Lighting Off
        Blend SrcAlpha One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            float4 _Color; float _Softness;
            v2f vert(appdata v) { v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.color=v.color*_Color;o.uv=v.uv;return o; }
            float4 frag(v2f i):SV_Target
            {
                float core=pow(saturate(1.-abs(i.uv.y*2.-1.)),_Softness);
                float taper=saturate(sin(saturate(i.uv.x)*3.141593)*8.);
                return float4(i.color.rgb,i.color.a*core*taper);
            }
            ENDCG
        }
    }
}
