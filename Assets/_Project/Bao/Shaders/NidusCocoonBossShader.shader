Shader "Bao/NidusCocoonBossShader"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Cocoon Core Glow)]
        [HDR] _CoreGlowColor ("Core Glow Color", Color) = (0.15, 0.6, 0.05, 1.0)
        _CorePulseSpeed ("Core Pulse Speed", Float) = 2.0
        _CorePulseIntensity ("Core Pulse Intensity", Range(0, 2)) = 0.25

        [Header(Crack Veins)]
        [HDR] _VeinColor ("Vein Glow Color", Color) = (0.2, 0.8, 0.05, 1.0)
        _VeinScale ("Vein Pattern Scale", Float) = 14.0
        _VeinSpeed ("Vein Flow Speed", Float) = 1.2
        _VeinIntensity ("Vein Glow Intensity", Range(0, 3)) = 0.6

        [Header(Outer Aura)]
        [HDR] _AuraColor ("Outer Aura Color", Color) = (0.08, 0.7, 0.03, 1.0)
        _AuraWidth ("Aura Width", Range(0, 0.02)) = 0.005
        _AuraPulseSpeed ("Aura Pulse Speed", Float) = 3.5
        _AuraIntensity ("Aura Intensity", Range(0, 2)) = 0.55

        [Header(Root Tentacles)]
        [HDR] _RootGlowColor ("Root Glow Color", Color) = (0.3, 0.15, 0.0, 1.0)
        _RootGlowIntensity ("Root Glow Intensity", Range(0, 2)) = 0.3

        [Header(Phase and State)]
        _PhaseLevel ("Phase Level (1=P1, 2=P2)", Range(1, 2)) = 1.0
        _HitFlash ("Hit Flash", Range(0, 1)) = 0.0
        _DeathDissolve ("Death Dissolve", Range(0, 1)) = 0.0
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
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _CoreGlowColor;
            float _CorePulseSpeed;
            float _CorePulseIntensity;
            float4 _VeinColor;
            float _VeinScale;
            float _VeinSpeed;
            float _VeinIntensity;
            float4 _AuraColor;
            float _AuraWidth;
            float _AuraPulseSpeed;
            float _AuraIntensity;
            float4 _RootGlowColor;
            float _RootGlowIntensity;
            float _PhaseLevel;
            float _HitFlash;
            float _DeathDissolve;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            float hash2D(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            float smoothNoise(float2 uv)
            {
                float2 ip = floor(uv);
                float2 fp = frac(uv);
                float a = hash2D(ip);
                float b = hash2D(ip + float2(1.0, 0.0));
                float c = hash2D(ip + float2(0.0, 1.0));
                float d = hash2D(ip + float2(1.0, 1.0));
                float2 u = fp * fp * (3.0 - 2.0 * fp);
                return lerp(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
            }

            // Crack / vein pattern using Voronoi-style cell borders
            float crackVeins(float2 uv)
            {
                float2 ip = floor(uv);
                float2 fp = frac(uv);
                float minDist = 1.0;
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 n = float2(x, y);
                        float2 pt = n + float2(
                            hash2D(ip + n),
                            hash2D(ip + n * 1.7)) * 0.6 + 0.2;
                        minDist = min(minDist, distance(fp, pt));
                    }
                }
                // Narrow, sharp crack lines (not broad fills)
                return saturate(1.0 - minDist * 3.5);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;

                // ── CRITICAL: respect vertex alpha for burrow fade-out ──
                // If the SpriteRenderer's color alpha is nearly zero, discard everything
                // This prevents the aura from "leaking through" when the boss is invisible
                float vertexAlpha = IN.color.a;

                fixed4 mainColor = tex2D(_MainTex, uv) * IN.color;

                if (mainColor.a <= 0.01)
                {
                    // Only render aura if the boss is actually visible (vertexAlpha > 0.05)
                    if (vertexAlpha < 0.05)
                        return fixed4(0, 0, 0, 0);

                    // ── Outer Aura on transparent border pixels ──
                    float neighborAlpha = 0.0;
                    float2 offsets[8] = {
                        float2(-1, 0), float2(1, 0), float2(0,-1), float2(0, 1),
                        float2(-0.7,-0.7), float2(0.7,-0.7), float2(-0.7,0.7), float2(0.7,0.7)
                    };
                    for (int i = 0; i < 8; i++)
                        neighborAlpha += tex2D(_MainTex, uv + offsets[i] * _AuraWidth).a;

                    if (neighborAlpha > 0.0)
                    {
                        float auraPulse = sin(_Time.y * _AuraPulseSpeed) * 0.35 + 0.65;
                        float phaseBoost = (_PhaseLevel - 1.0) * 0.6;
                        float finalAuraIntensity = _AuraIntensity * (1.0 + phaseBoost) * auraPulse;

                        fixed4 auraOut = _AuraColor;
                        auraOut.rgb *= finalAuraIntensity;
                        auraOut.a = saturate(neighborAlpha / 8.0) * 0.75 * vertexAlpha;
                        auraOut.rgb *= auraOut.a;
                        return auraOut;
                    }
                    return fixed4(0, 0, 0, 0);
                }

                fixed4 c = mainColor;
                float phase2Factor = saturate(_PhaseLevel - 1.0);

                // ── 1. Classify regions: cocoon body (top green) vs roots (bottom dark) ──
                // Roots = dark area with low green relative to red+blue, found at bottom of sprite
                float isDarkRoot = saturate((1.0 - c.g * 1.5 + (c.r + c.b) * 0.5) * 2.0)
                                 * saturate((0.45 - uv.y) * 5.0); // only affect lower 45% of sprite
                float isCocoonBody = 1.0 - isDarkRoot;

                // ── 2. Core Glow — subtle heartbeat breathing ──
                float t = _Time.y * _CorePulseSpeed;
                float heartbeat = sin(t) * 0.4 + sin(t * 2.3 + 0.5) * 0.15 + 0.75;
                heartbeat = saturate(heartbeat);

                float phaseGlowMult = 1.0 + phase2Factor * 1.2; // phase 2 is moderately brighter
                float coreGlow = heartbeat * _CorePulseIntensity * isCocoonBody * phaseGlowMult;
                c.rgb += _CoreGlowColor.rgb * coreGlow * c.a;

                // ── 3. Crack Vein Glow — the zigzag seams, subtle flow ──
                // Scrolling vertically to simulate energy rising through the cocoon
                float2 veinUV = float2(uv.x * _VeinScale * 0.7, uv.y * _VeinScale);
                veinUV.y -= _Time.y * _VeinSpeed * 0.4;
                float vein = crackVeins(veinUV) * isCocoonBody;

                // Only light veins where they align with naturally darker seam areas
                // The cocoon's zigzag cracks are typically darker pixels in the sprite
                float seamMask = saturate((1.0 - c.g * 0.8 - c.r * 0.3) * 2.0) * isCocoonBody;
                vein = lerp(vein * 0.3, vein, seamMask * 0.6 + 0.4);

                float veinPulse = sin(_Time.y * _CorePulseSpeed * 1.3 + 1.0) * 0.25 + 0.75;
                float veinPhaseBoost = 1.0 + phase2Factor * 1.0;
                c.rgb += _VeinColor.rgb * vein * _VeinIntensity * veinPulse * veinPhaseBoost * c.a;

                // ── 4. Root Tentacle Glow — subtle toxic seep from roots ──
                float rootFlow = smoothNoise(float2(uv.x * 3.0, uv.y * 6.0 + _Time.y * 0.4));
                float rootPulse = sin(_Time.y * 1.8 + uv.x * 4.0) * 0.2 + 0.8;
                c.rgb += _RootGlowColor.rgb * rootFlow * isDarkRoot * _RootGlowIntensity
                       * rootPulse * (1.0 + phase2Factor * 0.5) * c.a;

                // ── 5. Phase 2 crackle — subtle, not overwhelming ──
                if (phase2Factor > 0.01)
                {
                    float2 crackleUV = uv * 18.0 + float2(_Time.y * 2.5, -_Time.y * 1.8);
                    float crackle = pow(smoothNoise(crackleUV), 5.0); // High power = rare flashes only
                    c.rgb += _VeinColor.rgb * crackle * phase2Factor * 0.8 * c.a;
                }

                // ── 6. Death Dissolve ──
                if (_DeathDissolve > 0.01)
                {
                    float dNoise = smoothNoise(uv * 8.0 + _Time.y * 0.5);
                    float dissolveLine = (1.0 - uv.y) + dNoise * 0.12;
                    float dMask = dissolveLine - _DeathDissolve * 1.1;
                    if (dMask < 0.0)
                        discard;
                    else if (dMask < 0.04)
                    {
                        float edgeFactor = 1.0 - dMask / 0.04;
                        c.rgb += _VeinColor.rgb * edgeFactor * 2.0 * c.a;
                    }
                }

                // ── 7. Hit Flash ──
                if (_HitFlash > 0.01)
                {
                    fixed3 flashColor = fixed3(1.5, 1.5, 1.5);
                    c.rgb = lerp(c.rgb, flashColor * c.a, _HitFlash);
                }

                c.rgb *= c.a;
                return c;
            }
        ENDCG
        }
    }
}
