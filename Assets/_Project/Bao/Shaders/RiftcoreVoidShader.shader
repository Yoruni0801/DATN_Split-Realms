Shader "Bao/RiftcoreVoidShader"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _VoidColor ("Rift Energy Color", Color) = (0.73,0.75,0.29,1)
        _CollapseLevel ("Collapse Amount", Range(0, 10)) = 1.0
        _ChargeProgress ("Charge Progress", Range(0, 1)) = 0.0
        _ExplosionFlash ("Explosion Flash", Range(0, 1)) = 0.0
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
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _VoidColor;
            float _CollapseLevel;
            float _ChargeProgress;
            float _ExplosionFlash;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float collapseCharge = saturate((_CollapseLevel - 1.0) / 7.0);
                float charge = max(collapseCharge, saturate(_ChargeProgress));
                float critical = smoothstep(0.76, 1.0, charge);
                fixed4 baseColor = tex2D(_MainTex, IN.texcoord) * IN.color;
                if (baseColor.a < 0.01) discard;

                fixed4 c = baseColor;
                float shellMask = saturate(((c.r + c.g) * 0.5 - c.b) * 1.85) * saturate(IN.texcoord.y - 0.16);
                float2 coreCenter = float2(0.5, 0.42);
                float2 delta = IN.texcoord - coreCenter;
                float dist = length(delta);
                float angle = atan2(delta.y, delta.x);
                float swirlAngle = angle + (1.0 / (dist + 0.075)) * (0.12 + charge * 0.09) + _Time.y * (2.4 + charge * 6.0 + critical * 7.0);
                float spiral = sin(swirlAngle * 3.0 + sin(_Time.y * 2.1) * 0.35) * 0.5 + 0.5;
                float voidCore = pow(spiral, 2.35) * smoothstep(0.235, 0.025, dist);

                float crackPattern = abs(sin(IN.texcoord.x * 28.0 + IN.texcoord.y * 14.0 + _Time.y * (1.0 + charge * 8.0)) + cos(IN.texcoord.y * 24.0 - IN.texcoord.x * 20.0 - _Time.y * (0.72 + charge * 5.7)));
                float cracks = pow(1.0 - saturate(crackPattern * 0.5), 11.0) * shellMask;
                float nearCoreShell = smoothstep(0.52, 0.12, dist) * shellMask;
                fixed3 whiteGold = fixed3(1.35, 1.28, 0.98);
                fixed3 energyColor = _VoidColor.rgb * (1.25 + charge * 2.8);
                c.rgb += lerp(energyColor, whiteGold * 1.4, critical) * cracks * (0.6 + charge * 2.7);
                c.rgb += whiteGold * nearCoreShell * critical * (0.18 + cracks * 0.75);

                fixed3 coreGlow = lerp(_VoidColor.rgb * 1.5, whiteGold * 1.35, critical);
                c.rgb += coreGlow * voidCore * (1.25 + charge * 3.2) * (0.82 + sin(_Time.y * (3.0 + charge * 10.0)) * (0.08 + charge * 0.08));
                c.rgb += coreGlow * smoothstep(0.29, 0.0, dist) * (0.34 + charge * 1.1);

                float ringRadius = lerp(0.245, 0.135, charge);
                float compressionRing = 1.0 - smoothstep(0.0, 0.018 + charge * 0.008, abs(dist - ringRadius));
                c.rgb += lerp(_VoidColor.rgb * 1.5, whiteGold * 1.8, critical) * compressionRing * smoothstep(0.02, 0.7, charge) * (0.55 + charge * 1.7);

                float2 glowOffset = float2(0.008, 0.008) * (1.0 + charge * 1.8);
                float bloomOutline = saturate(tex2D(_MainTex, IN.texcoord + float2(-glowOffset.x, 0)).a + tex2D(_MainTex, IN.texcoord + float2(glowOffset.x, 0)).a + tex2D(_MainTex, IN.texcoord + float2(0, glowOffset.y)).a + tex2D(_MainTex, IN.texcoord + float2(0, -glowOffset.y)).a) - baseColor.a;
                c.rgb += lerp(_VoidColor.rgb * 1.4, whiteGold * 1.35, critical) * bloomOutline * (0.32 + charge * 1.85);
                c.a = max(c.a, bloomOutline * saturate(_VoidColor.a * (0.42 + charge * 0.58)));

                c.rgb = lerp(c.rgb, whiteGold * 1.65, smoothstep(0.91, 1.0, charge) * smoothstep(0.30, 0.0, dist) * 0.72);
                c.rgb = lerp(c.rgb, lerp(whiteGold * 1.65, _VoidColor.rgb * 2.2, shellMask * 0.28), _ExplosionFlash);
                c.a = max(c.a, _ExplosionFlash);
                c.rgb *= c.a;
                return c;
            }
        ENDCG
        }
    }
}
