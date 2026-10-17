Shader "Bao/IceCrabFrostShader"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _DeepTeal ("Deep Teal", Color) = (0.03,0.34,0.42,1)
        _CrabCyan ("Crab Cyan", Color) = (0.08,0.74,0.82,1)
        _FrostColor ("Frost Cyan", Color) = (0.47,0.96,1,1)
        _PearlWhite ("Pearl White", Color) = (0.96,1,1,1)

        _ShellCream ("Shell Cream", Color) = (1,0.88,0.48,1)
        _ShellGold ("Shell Gold", Color) = (0.95,0.66,0.25,1)
        _CoralCore ("Coral Core", Color) = (0.91,0.33,0.20,1)

        _Charge ("Charge", Range(0,1)) = 0
        _Blow ("Bubble Blow", Range(0,1)) = 0
        _MuzzleFlash ("Muzzle Flash", Range(0,1)) = 0
        _ShellPulse ("Shell Pulse", Range(0,1)) = 0
        _PearlGleam ("Pearl Gleam", Range(0,1)) = 0.25
        _FacingDirection ("Facing Direction", Float) = 1

        // Backwards-compatible property name from the old controller.
        _FrostIntensity ("Legacy Frost Intensity", Range(0,5)) = 0
        _GleamSpeed ("Gleam Speed", Float) = 2.0
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
            #pragma target 2.0
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            fixed4 _Color;
            fixed4 _DeepTeal;
            fixed4 _CrabCyan;
            fixed4 _FrostColor;
            fixed4 _PearlWhite;

            fixed4 _ShellCream;
            fixed4 _ShellGold;
            fixed4 _CoralCore;

            float _Charge;
            float _Blow;
            float _MuzzleFlash;
            float _ShellPulse;
            float _PearlGleam;
            float _FacingDirection;

            float _FrostIntensity;
            float _GleamSpeed;

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));

                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(a, b, u.x) +
                       (c - a) * u.y * (1.0 - u.x) +
                       (d - b) * u.x * u.y;
            }

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, input.uv) * input.color;
                if (tex.a < 0.01)
                    discard;

                // -------------------------------------------------------------
                // Sprite silhouette edge.
                // -------------------------------------------------------------
                float2 px = _MainTex_TexelSize.xy;
                float aC = tex.a;
                float aL = tex2D(_MainTex, input.uv + float2(-px.x, 0)).a;
                float aR = tex2D(_MainTex, input.uv + float2(px.x, 0)).a;
                float aU = tex2D(_MainTex, input.uv + float2(0, px.y)).a;
                float aD = tex2D(_MainTex, input.uv + float2(0, -px.y)).a;

                float edge = saturate(
                    (abs(aC - aL) + abs(aC - aR) + abs(aC - aU) + abs(aC - aD)) * 1.9);

                // -------------------------------------------------------------
                // Separate the warm shell from the teal body using the original
                // sprite color itself. This preserves the creature's identity.
                // -------------------------------------------------------------
                float warmMask = saturate(
                    (tex.r - tex.b) * 2.20 +
                    (tex.g - tex.b) * 0.45 +
                    0.05);

                warmMask = smoothstep(0.08, 0.72, warmMask);
                float coolMask = 1.0 - warmMask;

                // Orange/coral center detector.
                float coralMask = saturate(
                    (tex.r - tex.g) * 2.7 +
                    (tex.g - tex.b) * 0.7);

                coralMask *= warmMask;
                coralMask = smoothstep(0.06, 0.78, coralMask);

                // -------------------------------------------------------------
                // Warm shell: preserve original art, then add pearl depth.
                // -------------------------------------------------------------
                float shellValue = saturate(dot(tex.rgb, float3(0.36, 0.52, 0.12)));
                fixed3 shellTarget = lerp(_ShellGold.rgb, _ShellCream.rgb, shellValue);

                // Keep most of the original painted shell color.
                fixed3 warmBase = lerp(tex.rgb, shellTarget, 0.14 * warmMask);

                float pearlNoise = noise(input.uv * 7.5 + float2(_Time.y * 0.10, -_Time.y * 0.08));
                float pearlBand = sin(
                    (input.uv.x * 0.85 + input.uv.y) * 13.0 -
                    _Time.y * _GleamSpeed);

                pearlBand = pow(saturate(pearlBand * 0.5 + 0.5), 9.0);
                float pearlSheen =
                    pearlBand *
                    (0.20 + pearlNoise * 0.24) *
                    _PearlGleam *
                    warmMask;

                // Shell pressure pulse stays warm instead of turning the shell blue.
                float shellPulse =
                    _ShellPulse *
                    warmMask *
                    (0.18 + pearlNoise * 0.18);

                fixed3 shellEnergy =
                    _PearlWhite.rgb * pearlSheen +
                    _ShellCream.rgb * shellPulse * 0.42 +
                    _CoralCore.rgb * coralMask * _ShellPulse * 0.24;

                // -------------------------------------------------------------
                // Teal body: deepen the body and add restrained cold rim.
                // -------------------------------------------------------------
                float coolValue = saturate((tex.g + tex.b) * 0.55);
                fixed3 coolTarget = lerp(_DeepTeal.rgb, _CrabCyan.rgb, coolValue);
                fixed3 coolBase = lerp(tex.rgb, coolTarget, coolMask * 0.20);

                float coldNoise = noise(
                    input.uv * 10.0 +
                    float2(_Time.y * 0.22, -_Time.y * 0.16));

                float frostEdge =
                    edge *
                    coolMask *
                    (0.18 + _Charge * 0.42 + _Blow * 0.34);

                // A tiny amount may catch the shell tips so the creature still feels icy.
                frostEdge += edge * warmMask * (0.035 + _Charge * 0.06);

                // -------------------------------------------------------------
                // Charge flow: warm shell pressure flows toward the front,
                // then the cool body/mouth becomes brighter.
                // -------------------------------------------------------------
                float frontCoord =
                    _FacingDirection >= 0.0
                    ? input.uv.x
                    : (1.0 - input.uv.x);

                float flowCenter = frac(_Time.y * 1.7);
                float flowDistance = abs(frontCoord - flowCenter);
                flowDistance = min(flowDistance, 1.0 - flowDistance);

                float flowBand = 1.0 - saturate(flowDistance / 0.13);
                flowBand = pow(flowBand, 3.0);

                float chargeFlow =
                    flowBand *
                    _Charge *
                    (warmMask * 0.22 + coolMask * 0.50);

                // While the original bubble ParticleSystem is firing, add a
                // subtle cold shimmer to the teal body, not to the bubble itself.
                float blowShimmer =
                    (0.5 + 0.5 * sin(
                        input.uv.y * 20.0 +
                        _Time.y * 9.0)) *
                    _Blow *
                    coolMask *
                    0.12;

                float flash =
                    _MuzzleFlash *
                    (coolMask * 0.42 + edge * 0.40);

                // Legacy property contributes only lightly for compatibility.
                float legacyFrost =
                    saturate(_FrostIntensity * 0.10) *
                    coolMask *
                    (coldNoise * 0.20 + edge * 0.24);

                fixed3 baseColor = lerp(coolBase, warmBase, warmMask);

                fixed3 effects =
                    shellEnergy +
                    _FrostColor.rgb * frostEdge +
                    _CrabCyan.rgb * blowShimmer +
                    _FrostColor.rgb * chargeFlow +
                    _PearlWhite.rgb * flash +
                    _FrostColor.rgb * legacyFrost;

                fixed3 finalColor = baseColor + effects;

                // Premultiply for Blend One OneMinusSrcAlpha.
                finalColor *= tex.a;
                return fixed4(finalColor, tex.a);
            }
        ENDCG
        }
    }

    Fallback "Sprites/Default"
}
