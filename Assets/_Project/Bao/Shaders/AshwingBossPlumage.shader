Shader "Bao/AshwingBossPlumage"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable alpha", Float) = 0
        _Heat ("Cinder heat", Range(0,1)) = .12
        _Enrage ("Burning plumage", Range(0,1)) = 0
        _HitFlash ("Hit flash", Range(0,1)) = 0
        _Dissolve ("Ash dissolve", Range(0,1)) = 0
        [HDR] _EmberColor ("Ember", Color) = (1.6,.32,.025,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" "PreviewType"="Plane" }
        Cull Off Lighting Off ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"
            float4 _MainTex_TexelSize;
            float _Heat, _Enrage, _HitFlash, _Dissolve;
            float4 _EmberColor;
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3.-2.*f);
                return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
            }
            float4 frag(v2f IN) : SV_Target
            {
                float4 source=SampleSpriteTexture(IN.texcoord);
                float4 c=source*IN.color;
                float luminance=dot(c.rgb,float3(.22,.7,.08));
                float red=saturate((source.r-max(source.g,source.b))*3.8);
                float soot=(1.-smoothstep(.15,.75,luminance))*.28;
                c.rgb*=1.-soot;
                c.rgb=lerp(c.rgb,c.rgb*float3(.72,.41,.37),red*.28);
                float n=noise(IN.texcoord*72.+float2(_Time.y*.7,-_Time.y*1.9));
                float veins=pow(saturate(1.-abs(n-.5)*7.),3.);
                float pulse=.88+.12*sin(_Time.y*3.+IN.texcoord.y*20.);
                float2 px=_MainTex_TexelSize.xy*1.5;
                float edge=1.-min(min(SampleSpriteTexture(IN.texcoord+float2(px.x,0)).a,SampleSpriteTexture(IN.texcoord-float2(px.x,0)).a),min(SampleSpriteTexture(IN.texcoord+float2(0,px.y)).a,SampleSpriteTexture(IN.texcoord-float2(0,px.y)).a));
                c.rgb+=_EmberColor.rgb*red*(.08+_Heat*.55+_Enrage*.22)*(.2+veins*.8)*pulse;
                c.rgb+=float3(1.5,.8,.25)*edge*red*(.1+_Heat*.6+_Enrage*.22);
                float dissolveNoise=noise(IN.texcoord*34.);
                float threshold=_Dissolve*1.12-.08;
                float alive=smoothstep(threshold,threshold+.055,dissolveNoise);
                float burn=(1.-smoothstep(threshold+.05,threshold+.13,dissolveNoise))*step(.01,_Dissolve);
                c.rgb+=burn*float3(2.,.5,.04);
                c.rgb=lerp(c.rgb,float3(1.6,1.32,.85),_HitFlash*.85);
                c.a*=alive;
                c.rgb*=c.a;
                return c;
            }
            ENDCG
        }
    }
}
