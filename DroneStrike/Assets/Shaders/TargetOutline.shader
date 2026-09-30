// Thin neon silhouette for mission targets, and a flat unlit colour for the
// small marker props (weak-rear strip, jammer beacon, fuel danger ring).
//
// Outline mode (_Cull = 1, front faces culled): every part of a target gets a
// duplicate renderer with this material. The vertex is pushed outward by a
// constant number of *pixels*, not metres, so the line stays readable from
// 20 m and from 150 m alike — a world-space shell is a fat halo up close and
// sub-pixel at altitude.
//
// Two details keep it a silhouette rather than a wireframe of every part:
// - _Radial = 1 extrudes along the vertex's direction from the mesh centre
//   instead of its normal. A cube's corners carry three different normals,
//   so normal extrusion tears the shell open at every edge; the radial
//   direction is shared by all copies of a corner, so the shell stays closed.
//   Primitive cubes and cylinders are centred on their own origin, which is
//   exactly the case where this is correct. Imported models use normals.
// - _DepthPush moves the shell a little away from the camera. Where one part
//   of a truck overlaps another, the neighbour is closer than that and hides
//   the internal edge; against the ground or sky, metres away, it still shows.
//
// Marker mode (_Cull = 2, _Width = 0, _DepthPush = 0): an ordinary unlit
// solid, used where a Standard material would be dimmed by shadow and fog.
Shader "DroneStrike/TargetOutline"
{
    Properties
    {
        _Color ("Colour", Color) = (0.35, 0.9, 1, 1)
        _Width ("Width (pixels)", Float) = 2.5
        _Radial ("Radial extrusion", Float) = 1
        _DepthPush ("Depth push (metres)", Float) = 0.7
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+10" "IgnoreProjector" = "True" }

        Pass
        {
            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Width;
            float _Radial;
            float _DepthPush;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;

                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 radial = mul((float3x3)unity_ObjectToWorld, v.vertex.xyz);
                float3 normal = UnityObjectToWorldNormal(v.normal);
                float3 direction = _Radial > 0.5 ? radial : normal;
                direction = direction / max(length(direction), 1e-5);

                float3 viewPos = mul(UNITY_MATRIX_V, float4(worldPos, 1.0)).xyz;
                viewPos += normalize(viewPos) * _DepthPush;
                float4 clip = mul(UNITY_MATRIX_P, float4(viewPos, 1.0));

                // Screen direction of the extrusion: project a short step along
                // it and measure where that lands relative to the vertex.
                float3 viewDir = mul((float3x3)UNITY_MATRIX_V, direction);
                float4 stepClip = mul(UNITY_MATRIX_P, float4(viewPos + viewDir * 0.05, 1.0));
                float2 screenStep = stepClip.xy / stepClip.w - clip.xy / clip.w;
                float stepLength = length(screenStep);
                if (stepLength > 1e-6)
                    clip.xy += (screenStep / stepLength) * _Width * 2.0 / _ScreenParams.xy * clip.w;

                o.pos = clip;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }

    Fallback Off
}
