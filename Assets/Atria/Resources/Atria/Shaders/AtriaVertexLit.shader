// Cheap, light-free surface shader for Atria's procedural building.
// Colour comes from mesh vertex colours; shading is a fixed directional term computed per
// vertex, so no realtime lights, shadows or lightmaps are needed. Works in the Built-in and
// Universal render pipelines (single untagged pass).
Shader "Atria/VertexLit"
{
    Properties
    {
        _LightDir ("Light direction (world)", Vector) = (0.35, 0.8, 0.5, 0)
        _Ambient ("Ambient", Range(0, 1)) = 0.68
        _Diffuse ("Directional strength", Range(0, 1)) = 0.32
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 100
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _LightDir;
            half _Ambient;
            half _Diffuse;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                fixed4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 n = UnityObjectToWorldNormal(v.normal);
                // Signed term (not clamped) so every wall orientation gets a distinct shade.
                half light = _Ambient + _Diffuse * dot(n, normalize(_LightDir.xyz));
                o.color = fixed4(v.color.rgb * saturate(light), 1);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return i.color;
            }
            ENDCG
        }
    }
    Fallback Off
}
