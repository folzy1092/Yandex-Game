// Full-screen "you are in the drone" lens: the look of a small wide-angle
// FPV camera rather than a clean game camera.
//  - barrel distortion (the fisheye bulge of a 2.1 mm lens), with a slight
//    zoom so the corners do not sample outside the frame;
//  - chromatic aberration growing toward the edges;
//  - vignette;
//  - analog grain and a touch of contrast.
// Driven per frame by FpvCameraFx, which also raises aberration and grain
// under jamming.
Shader "DroneStrike/FpvLens"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Barrel ("Barrel distortion", Float) = 0.16
        _Zoom ("Zoom", Float) = 0.93
        _Chroma ("Chromatic aberration", Float) = 0.004
        _Vignette ("Vignette", Float) = 0.55
        _Grain ("Grain", Float) = 0.05
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Barrel;
            float _Zoom;
            float _Chroma;
            float _Vignette;
            float _Grain;

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            fixed4 frag (v2f_img i) : SV_Target
            {
                // Size of the source, not _ScreenParams: inside a Blit the
                // screen parameters are not the image's, and came through as
                // zeros in the editor capture (all-black frame).
                float aspect = _MainTex_TexelSize.z / max(_MainTex_TexelSize.w, 1.0);
                float2 d = i.uv - 0.5;
                d.x *= aspect;
                float r2 = dot(d, d);

                float2 bent = d * (1.0 + _Barrel * r2) * _Zoom;
                float2 edge = bent * _Chroma * r2 * 4.0;
                bent.x /= aspect;
                edge.x /= aspect;

                float2 uv = 0.5 + bent;
                fixed4 colour;
                colour.r = tex2D(_MainTex, uv + edge).r;
                colour.g = tex2D(_MainTex, uv).g;
                colour.b = tex2D(_MainTex, uv - edge).b;
                colour.a = 1.0;

                // Outside the sensor: black, like the rounded corners of a real feed.
                float2 inside = step(0.0, uv) * step(uv, 1.0);
                colour.rgb *= inside.x * inside.y;

                float vignette = 1.0 - _Vignette * smoothstep(0.25, 0.95, sqrt(r2));
                colour.rgb *= vignette;

                colour.rgb = (colour.rgb - 0.5) * 1.06 + 0.5;
                float grain = hash(i.uv * _MainTex_TexelSize.zw + frac(_Time.y * 37.0) * 91.0) - 0.5;
                colour.rgb += grain * _Grain;
                return saturate(colour);
            }
            ENDCG
        }
    }

    Fallback Off
}
