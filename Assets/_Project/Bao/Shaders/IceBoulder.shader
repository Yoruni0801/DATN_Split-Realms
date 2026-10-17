Shader "Bao/IceBoulder"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Main Sprite Tint", Color) = (1,1,1,1)

        [Header(1. OUTLINE AND RIM CONTRAST)]
        [Toggle] _EnableOutline ("Enable Dark Outer Outline", Float) = 1
        _OutlineColor ("Dark Outline Color", Color) = (0.04, 0.09, 0.22, 1.0)
        _OutlineThickness ("Dark Border Width", Range(0.001, 0.05)) = 0.012

        [Toggle] _EnableInnerRim ("Enable Inner Rim Highlight", Float) = 1
        _InnerOutlineColor ("Inner Rim Color", Color) = (0.65, 0.95, 1.0, 1.0)
        _InnerOutlineThickness ("Inner Rim Width", Range(0.001, 0.03)) = 0.006

        [Header(2. ICE 3D SPHERE SHADING)]
        [Toggle] _EnableSphereShading ("Enable 3D Sphere Shading", Float) = 1
        _IceCoreBright ("Highlight Color (Top/Light)", Color) = (0.94, 0.98, 1.0, 1.0)
        _IceCoreBody ("Midtone Color (Body)", Color) = (0.22, 0.48, 0.78, 1.0)
        _IceCoreDeep ("Shadow Color (Bottom/Dark)", Color) = (0.07, 0.14, 0.30, 1.0)
        _LightDirection ("Light Direction (XY)", Vector) = (-0.45, 0.75, 0, 0)
        _TextureMix ("Texture Detail Blend Factor", Range(0.0, 1.0)) = 0.55
        _ShadingContrast ("Sphere Shading Contrast", Range(0.5, 3.0)) = 1.3
        _CoreIntensity ("Overall Core Brightness", Range(0.0, 5.0)) = 1.1

        [Header(3. SPECULAR HIGHLIGHT)]
        [Toggle] _EnableSpecular ("Enable Sun Specular Point", Float) = 1
        _SpecularColor ("Specular Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _SpecularPower ("Specular Sharpness", Range(2.0, 64.0)) = 24.0
        _SpecularIntensity ("Specular Intensity", Range(0.0, 3.0)) = 0.85

        [Header(4. FROST VAPOR AURA)]
        [Toggle] _EnableAura ("Enable Outer Frost Aura", Float) = 1
        [HDR] _AuraColor ("Aura Frost Glow", Color) = (0.35, 0.75, 1.0, 0.9)
        _AuraSpread ("Aura Glow Size", Range(0.001, 0.04)) = 0.012
        _AuraPulseSpeed ("Aura Breathe Speed", Float) = 2.5
        _AuraPulseAmount ("Aura Breathe Intensity", Range(0.0, 0.8)) = 0.2

        [Header(5. SUBSURFACE SCATTERING)]
        _SSSIntensity ("Subsurface Softness", Range(0.0, 1.5)) = 0.55
        _SSSTint ("Subsurface Glow Tint", Color) = (0.30, 0.65, 0.95, 1.0)

        [Header(6. GLOWING CRACKS)]
        [Toggle] _EnableCracks ("Enable Glowing Cracks", Float) = 1
        [HDR] _CrackGlow ("Crack Glow (HDR Color)", Color) = (0.15, 0.9, 1.0, 2.2)
        _CrackThreshold ("Crack Luminance Threshold", Range(0.1, 0.9)) = 0.52
        _CrackIntensity ("Crack Glow Intensity", Range(0.0, 5.0)) = 1.8

        [Header(7. DYNAMIC SCRIPT BOOST)]
        _GrowthEmissionBoost ("Growth Boost (Controlled by C#)", Range(0.0, 3.0)) = 0.0

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
                float2 texcoord : TEXCOORD0;
                float2 localPos : TEXCOORD1;
            };

            fixed4 _Color;

            float _EnableOutline;
            float4 _OutlineColor;
            float _OutlineThickness;

            float _EnableInnerRim;
            float4 _InnerOutlineColor;
            float _InnerOutlineThickness;

            float _EnableSphereShading;
            float4 _IceCoreBright;
            float4 _IceCoreBody;
            float4 _IceCoreDeep;
            float4 _LightDirection;
            float _TextureMix;
            float _ShadingContrast;
            float _CoreIntensity;

            float _EnableSpecular;
            float4 _SpecularColor;
            float _SpecularPower;
            float _SpecularIntensity;

            float _EnableAura;
            float4 _AuraColor;
            float _AuraSpread;
            float _AuraPulseSpeed;
            float _AuraPulseAmount;

            float _SSSIntensity;
            float4 _SSSTint;

            float _EnableCracks;
            float4 _CrackGlow;
            float _CrackThreshold;
            float _CrackIntensity;

            float _GrowthEmissionBoost;

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.localPos = IN.vertex.xy;
                OUT.color = IN.color * _Color;

                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif

                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, IN.texcoord);

                // 1. SAMPLING ĐƯỜNG BIÊN ĐỂ LÀM VIỀN TƯƠNG PHẢN (8 Hướng)
                float2 oStep = _OutlineThickness * _MainTex_TexelSize.xy * 250.0;
                float a_t  = tex2D(_MainTex, IN.texcoord + float2(0, oStep.y)).a;
                float a_b  = tex2D(_MainTex, IN.texcoord - float2(0, oStep.y)).a;
                float a_r  = tex2D(_MainTex, IN.texcoord + float2(oStep.x, 0)).a;
                float a_l  = tex2D(_MainTex, IN.texcoord - float2(oStep.x, 0)).a;
                float a_tr = tex2D(_MainTex, IN.texcoord + float2(oStep.x * 0.707, oStep.y * 0.707)).a;
                float a_tl = tex2D(_MainTex, IN.texcoord + float2(-oStep.x * 0.707, oStep.y * 0.707)).a;
                float a_br = tex2D(_MainTex, IN.texcoord + float2(oStep.x * 0.707, -oStep.y * 0.707)).a;
                float a_bl = tex2D(_MainTex, IN.texcoord + float2(-oStep.x * 0.707, -oStep.y * 0.707)).a;

                float minAlpha = min(min(min(a_t, a_b), min(a_r, a_l)), min(min(a_tr, a_tl), min(a_br, a_bl)));
                float maxAlpha = max(max(max(a_t, a_b), max(a_r, a_l)), max(max(a_tr, a_tl), max(a_br, a_bl)));

                // Viền tối bên trong mép Sprite
                float darkBorderMask = saturate((1.0 - minAlpha) * 3.5) * _EnableOutline;

                // Viền sáng cạnh trong
                float2 rStep = (_OutlineThickness + _InnerOutlineThickness) * _MainTex_TexelSize.xy * 250.0;
                float ra_t  = tex2D(_MainTex, IN.texcoord + float2(0, rStep.y)).a;
                float ra_b  = tex2D(_MainTex, IN.texcoord - float2(0, rStep.y)).a;
                float ra_r  = tex2D(_MainTex, IN.texcoord + float2(rStep.x, 0)).a;
                float ra_l  = tex2D(_MainTex, IN.texcoord - float2(rStep.x, 0)).a;
                float ra_tr = tex2D(_MainTex, IN.texcoord + float2(rStep.x * 0.707, rStep.y * 0.707)).a;
                float ra_tl = tex2D(_MainTex, IN.texcoord + float2(-rStep.x * 0.707, rStep.y * 0.707)).a;
                float ra_br = tex2D(_MainTex, IN.texcoord + float2(rStep.x * 0.707, -rStep.y * 0.707)).a;
                float ra_bl = tex2D(_MainTex, IN.texcoord + float2(-rStep.x * 0.707, -rStep.y * 0.707)).a;
                float minRimAlpha = min(min(min(ra_t, ra_b), min(ra_r, ra_l)), min(min(ra_tr, ra_tl), min(ra_br, ra_bl)));

                float innerRimMask = saturate((1.0 - minRimAlpha) * 3.5) * (1.0 - darkBorderMask) * _EnableInnerRim;

                // 2. KHỐI CẦU BĂNG 3D
                float2 normal2D = clamp(IN.localPos * 2.2, -1.0, 1.0);
                float r2 = dot(normal2D, normal2D);
                float z = sqrt(max(0.0, 1.0 - r2));
                float3 sphereNormal = normalize(float3(normal2D.x, normal2D.y, z * 0.85));

                float3 lightDir = normalize(float3(_LightDirection.x, _LightDirection.y, 0.55));
                float NdotL = dot(sphereNormal, lightDir);
                float lightVal = saturate(NdotL * 0.5 + 0.5);
                lightVal = pow(lightVal, _ShadingContrast);

                float3 iceGradient = lerp(_IceCoreDeep.rgb, _IceCoreBody.rgb, smoothstep(0.05, 0.55, lightVal));
                iceGradient = lerp(iceGradient, _IceCoreBright.rgb, smoothstep(0.50, 0.95, lightVal));
                iceGradient = lerp(float3(1, 1, 1), iceGradient, _EnableSphereShading);

                // Specular
                float3 viewDir = float3(0, 0, 1);
                float3 halfDir = normalize(lightDir + viewDir);
                float NdotH = max(0.0, dot(sphereNormal, halfDir));
                float spec = pow(NdotH, _SpecularPower) * _SpecularIntensity * _EnableSpecular;
                float3 specularGlow = _SpecularColor.rgb * spec;

                // Tán xạ dưới bề mặt tuyết (Subsurface Scattering)
                float sss = pow(saturate(length(normal2D)), 2.0) * _SSSIntensity * (1.0 - lightVal * 0.4);
                float3 sssGlow = _SSSTint.rgb * sss;

                // 3. TEXTURE VÀ PHÁT QUANG VẾT NỨT
                float luminance = dot(tex.rgb, float3(0.299, 0.587, 0.114));
                float3 surfaceShading = (iceGradient + sssGlow) * lerp(1.0, tex.rgb * 1.15, _TextureMix) + specularGlow;

                float isCrack = saturate((_CrackThreshold - luminance) * 5.0);
                float3 crackEmission = _CrackGlow.rgb * isCrack * (_CrackIntensity + _GrowthEmissionBoost) * _EnableCracks;

                // Tổng hợp thân khối
                float3 baseBody = surfaceShading * _CoreIntensity + crackEmission;

                // Áp dụng Inner Rim và Dark Border
                baseBody = lerp(baseBody, _InnerOutlineColor.rgb, innerRimMask * 0.85);
                baseBody = lerp(baseBody, _OutlineColor.rgb, darkBorderMask * _OutlineColor.a);

                // 4. HÀO QUANG VÀ VIỀN NGOÀI
                float auraPulse = sin(_Time.y * _AuraPulseSpeed) * _AuraPulseAmount + 1.0;
                float2 auraOffset = _AuraSpread * auraPulse * _MainTex_TexelSize.xy * 180.0;
                float aSum = (tex2D(_MainTex, IN.texcoord + float2(0, auraOffset.y)).a +
                              tex2D(_MainTex, IN.texcoord - float2(0, auraOffset.y)).a +
                              tex2D(_MainTex, IN.texcoord + float2(auraOffset.x, 0)).a +
                              tex2D(_MainTex, IN.texcoord - float2(auraOffset.x, 0)).a) * 0.25;
                float auraMask = saturate((aSum - tex.a) * 2.5) * _EnableAura;
                float3 auraGlow = _AuraColor.rgb * auraMask * _AuraColor.a;

                float outerOutlineMask = saturate((maxAlpha - tex.a) * 3.5) * _EnableOutline;
                float3 outerBorder = _OutlineColor.rgb * outerOutlineMask * _OutlineColor.a;

                float3 finalColor = (baseBody * tex.a + outerBorder + auraGlow) * IN.color.rgb;
                float finalAlpha = max(max(tex.a * IN.color.a, outerOutlineMask * _OutlineColor.a), auraMask * saturate(_AuraColor.a * 0.7));

                return fixed4(finalColor, finalAlpha);
            }
            ENDCG
        }
    }
    FallBack "Sprites/Default"
}
