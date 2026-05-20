Shader "Custom/Outline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 0.08, 0.08, 1)
        _OutlineWidth ("Outline Width (px)", Float) = 3.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+1" }

        // Pass 1: Write depth for the mesh body (invisible) so the outline
        // has correct depth to test against. ColorMask 0 = writes no colour.
        Pass
        {
            Name "DepthPrime"
            Cull Back
            ZWrite On
            ZTest LEqual
            ColorMask 0
        }

        // Pass 2: Render the outline by expanding back-face silhouette
        Pass
        {
            Name "Outline"
            Cull Front
            ZWrite Off
            ZTest LEqual
            ColorMask RGB

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            fixed4 _OutlineColor;
            float  _OutlineWidth;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float4 clipPos = UnityObjectToClipPos(v.vertex);
                float3 viewNormal = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, v.normal));

                float2 screenNormal = normalize(float2(
                    viewNormal.x * UNITY_MATRIX_P[0][0],
                    viewNormal.y * UNITY_MATRIX_P[1][1]
                ));

                float2 ndcOffset = screenNormal * (_OutlineWidth / _ScreenParams.xy);
                clipPos.xy += ndcOffset * clipPos.w;

                o.pos = clipPos;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return fixed4(_OutlineColor.rgb, 1.0);
            }
            ENDCG
        }
    }

    FallBack Off
}
