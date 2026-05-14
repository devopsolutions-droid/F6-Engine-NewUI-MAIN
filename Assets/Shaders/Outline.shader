Shader "Custom/Outline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 0.08, 0.08, 1)
        _OutlineWidth ("Outline Width (px)", Float) = 2.5
    }

    SubShader
    {
        // Render after all opaque geometry so the outline sits on top cleanly
        Tags { "RenderType"="Opaque" "Queue"="Geometry+100" }

        Pass
        {
            Name "Outline"

            // Inverted-hull: only back faces are drawn, expanded outward along normals.
            // This means the outline is ALWAYS exterior — it physically cannot appear
            // on internal edges because front faces occlude the expanded back faces.
            Cull Front
            ZWrite Off          // Don't write depth — avoids z-fighting with the mesh
            ZTest LEqual
            ColorMask RGB

            // Additive-friendly blend: outline draws on top without darkening the mesh
            Blend SrcAlpha OneMinusSrcAlpha

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
                // UV1 can carry baked smooth normals (optional — falls back to mesh normals)
                float3 texcoord1 : TEXCOORD1;
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

                // Use baked smooth normals from UV1 if they exist (non-zero),
                // otherwise fall back to the mesh normal. Smooth normals prevent
                // cracks at hard edges (e.g. the jagged rear of a cover mesh).
                float3 useNormal = (dot(v.texcoord1, v.texcoord1) > 0.001)
                                   ? normalize(v.texcoord1)
                                   : normalize(v.normal);

                float4 clipPos = UnityObjectToClipPos(v.vertex);

                // Transform normal to view space with the correct inverse-transpose
                float3 viewNormal = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, useNormal));

                // Project normal into NDC (screen) space, accounting for FOV and aspect.
                // Normalising here keeps the direction but removes magnitude variation.
                float2 screenNormal = normalize(float2(
                    viewNormal.x * UNITY_MATRIX_P[0][0],
                    viewNormal.y * UNITY_MATRIX_P[1][1]
                ));

                // NDC-space offset: divide by clipPos.w to convert from clip → NDC,
                // then scale by _OutlineWidth in pixels (1 unit ≈ 1 px at 1080p).
                // This makes the outline CONSTANT WIDTH regardless of distance — the
                // key property visible in the reference image.
                float2 ndcOffset = screenNormal * (_OutlineWidth / _ScreenParams.xy);
                clipPos.xy += ndcOffset * clipPos.w;   // back to clip space

                o.pos = clipPos;
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

    // Fallback so the shader still compiles on older hardware
    FallBack Off
}
