Shader "Hidden/Timeline/PlayerReveal"
{
    Properties
    {
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}

        _InsideColor
        (
            "Inside Color",
            Color
        ) = (1,1,1,1)


        _InsideAlpha
        (
            "Inside Alpha",
            Range(0,1)
        ) = 0


        _Radius
        (
            "Radius",
            Float
        ) = 0.08


        _Center
        (
            "Center",
            Vector
        ) = (0.5,0.5,0,0)


        _EdgeSoftness
        (
            "Edge Softness",
            Range(0.001,0.2)
        ) = 0.025


        _EdgeIntensity
        (
            "Edge Intensity",
            Range(0,2)
        ) = 0.35
    }


    SubShader
    {
        Tags
        {
            "Queue"="Overlay"
            "RenderType"="Transparent"
        }


        Cull Off

        ZWrite Off

        ZTest Always

        Blend SrcAlpha OneMinusSrcAlpha


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
                float4 vertex : SV_POSITION;

                float2 uv : TEXCOORD0;
            };


            fixed4 _InsideColor;

            float _InsideAlpha;

            float _Radius;

            float4 _Center;

            float _EdgeSoftness;

            float _EdgeIntensity;


            // =================================================
            // VERTEX
            // =================================================

            v2f vert(appdata v)
            {
                v2f o;


                o.vertex =
                    UnityObjectToClipPos(
                        v.vertex
                    );


                o.uv =
                    v.uv;


                return o;
            }


            // =================================================
            // FRAGMENT
            // =================================================

            fixed4 frag(v2f i) : SV_Target
            {
                // ------------------------------------------------
                // Aspect ratio
                // ------------------------------------------------

                float aspect =
                    _ScreenParams.x /
                    _ScreenParams.y;


                // ------------------------------------------------
                // Distance
                // ------------------------------------------------

                float2 delta =
                    i.uv -
                    _Center.xy;


                delta.x *=
                    aspect;


                float distanceFromCenter =
                    length(
                        delta
                    );


                // ------------------------------------------------
                // Circle
                // ------------------------------------------------

                float innerRadius =
                    max(
                        0.0001,

                        _Radius -
                        _EdgeSoftness
                    );


                float circle =
                    1.0 -
                    smoothstep(
                        innerRadius,

                        _Radius,

                        distanceFromCenter
                    );


                // =================================================
                // OUTSIDE
                // =================================================

                /*
                 * Ngoài circle:
                 *
                 * BLACK
                 */

                float blackAlpha =
                    1.0 -
                    circle;


                // =================================================
                // INSIDE
                // =================================================

                /*
                 * Trong circle:
                 *
                 * Alpha = 0
                 *
                 * => nhìn thấy map nguyên bản.
                 */

                float insideAlpha =
                    circle *
                    _InsideAlpha;


                // =================================================
                // EDGE
                // =================================================

                float edge =
                    smoothstep(
                        _Radius,

                        _Radius -
                        _EdgeSoftness,

                        distanceFromCenter
                    );


                float edgeRing =
                    saturate(
                        circle -
                        edge
                    );


                float edgeAlpha =
                    edgeRing *
                    _EdgeIntensity;


                // =================================================
                // FINAL
                // =================================================

                fixed3 finalColor =
                    _InsideColor.rgb;


                float finalAlpha =
                    blackAlpha +
                    insideAlpha;


                finalAlpha =
                    saturate(
                        finalAlpha +
                        edgeAlpha
                    );


                return fixed4(
                    finalColor,

                    finalAlpha
                );
            }

            ENDCG
        }
    }
}