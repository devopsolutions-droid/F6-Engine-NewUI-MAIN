Shader "Custom/Outline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 0, 0, 1)
        _OutlineWidth ("Outline Width", Float) = 0.015
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+1" }

        Pass
        {
            Name "Outline"

            Cull Front
            ZWrite On
            ZTest LEqual

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

                float3 expandedPos = v.vertex.xyz + normalize(v.normal) * _OutlineWidth;
                o.pos = UnityObjectToClipPos(float4(expandedPos, 1.0));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return _OutlineColor;
            }
            ENDCG
        }
    }
}
