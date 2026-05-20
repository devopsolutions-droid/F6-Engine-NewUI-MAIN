Shader "Custom/XRay"
{
    Properties
    {
        _XRayColor      ("X-Ray Color",        Color)          = (0, 0.9, 1, 1)
        _Alpha          ("Body Alpha",          Range(0, 1))    = 0.12
        _RimAlpha       ("Rim Alpha",           Range(0, 1))    = 0.55
        _GlowIntensity  ("Glow Intensity",      Range(0, 3))    = 0.6
        _ScanlineSpeed  ("Scanline Speed",      Float)          = 0.55
        _ScanlineWidth  ("Scanline Band Width", Range(0.01,0.4))= 0.05
        _ScanlineBright ("Scanline Brightness", Range(0, 3))    = 1.6
        _PulseSpeed     ("Pulse Speed",         Float)          = 1.1
        _PulseAmount    ("Pulse Amount",        Range(0, 0.4))  = 0.07
        _ObjectMinY     ("Object World Min Y",  Float)          = 0.0
        _ObjectMaxY     ("Object World Max Y",  Float)          = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent+50"
            "RenderPipeline" = "UniversalPipeline"
        }

        // ── Pass 1: Back faces ────────────────────────────────────────────────
        // Render the inside of the mesh first so inner surfaces are visible
        // through the front faces. ZTest Always = ignores depth buffer entirely.
        Pass
        {
            Name    "XRayBack"
            Cull    Front
            ZTest   Always
            ZWrite  Off
            Blend   SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _XRayColor;
            float  _Alpha;
            float  _RimAlpha;
            float  _GlowIntensity;
            float  _ScanlineSpeed;
            float  _ScanlineWidth;
            float  _ScanlineBright;
            float  _PulseSpeed;
            float  _PulseAmount;
            float  _ObjectMinY;
            float  _ObjectMaxY;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float  fresnel  : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos      = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 worldNormal = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
                float3 viewDir     = normalize(_WorldSpaceCameraPos - o.worldPos);
                // Invert normal for back face fresnel
                o.fresnel = 1.0 - saturate(dot(-worldNormal, viewDir));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float pulse    = 1.0 + _PulseAmount * sin(_Time.y * _PulseSpeed * 6.2832);
                float normY    = (_ObjectMaxY - i.worldPos.y) / max(_ObjectMaxY - _ObjectMinY, 0.001);
                float scanPos  = frac(_Time.y * _ScanlineSpeed);
                float scanline = smoothstep(_ScanlineWidth, 0.0, abs(normY - scanPos));

                float3 emissive  = _XRayColor.rgb * _GlowIntensity * pulse * 0.5;
                float3 finalRGB  = _XRayColor.rgb + emissive + scanline * _ScanlineBright * 0.5 * _XRayColor.rgb;
                // Back faces are dimmer — half the alpha of front faces
                float  finalA    = saturate(_Alpha * 0.5 + scanline * 0.15);

                return fixed4(finalRGB, finalA);
            }
            ENDCG
        }

        // ── Pass 2: Front faces ───────────────────────────────────────────────
        // Render the outside of the mesh. ZTest Always means this draws even
        // when another mesh is in front — you see every part through every other part.
        Pass
        {
            Name    "XRayFront"
            Cull    Back
            ZTest   Always
            ZWrite  Off
            Blend   SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _XRayColor;
            float  _Alpha;
            float  _RimAlpha;
            float  _GlowIntensity;
            float  _ScanlineSpeed;
            float  _ScanlineWidth;
            float  _ScanlineBright;
            float  _PulseSpeed;
            float  _PulseAmount;
            float  _ObjectMinY;
            float  _ObjectMaxY;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float  fresnel  : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos      = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 worldNormal = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
                float3 viewDir     = normalize(_WorldSpaceCameraPos - o.worldPos);
                // Fresnel: silhouette edges are bright, face centres are near-invisible
                o.fresnel = 1.0 - saturate(dot(worldNormal, viewDir));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // Fresnel rim — edges glow strongly, centres are very transparent
                float fresnelPow = pow(i.fresnel, 2.5);
                float rimAlpha   = fresnelPow * _RimAlpha;

                float pulse    = 1.0 + _PulseAmount * sin(_Time.y * _PulseSpeed * 6.2832);
                float normY    = (_ObjectMaxY - i.worldPos.y) / max(_ObjectMaxY - _ObjectMinY, 0.001);
                float scanPos  = frac(_Time.y * _ScanlineSpeed);
                float scanline = smoothstep(_ScanlineWidth, 0.0, abs(normY - scanPos));

                float3 emissive = _XRayColor.rgb * _GlowIntensity * pulse;
                float3 finalRGB = _XRayColor.rgb + emissive + scanline * _ScanlineBright * _XRayColor.rgb;
                // Centre faces use _Alpha (very low), rim uses rimAlpha (bright)
                float  finalA   = saturate(_Alpha + rimAlpha + scanline * 0.4);

                return fixed4(finalRGB, finalA);
            }
            ENDCG
        }
    }

    FallBack Off
}
