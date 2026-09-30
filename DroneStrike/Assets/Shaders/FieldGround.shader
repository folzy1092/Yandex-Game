Shader "DroneStrike/Field Ground"
{
    Properties
    {
        _MainTex ("Ground detail", 2D) = "white" {}
        _Color ("Grass", Color) = (0.32, 0.42, 0.24, 1)
        _SoilColor ("Dry earth", Color) = (0.39, 0.31, 0.21, 1)
        _RockColor ("Rock", Color) = (0.42, 0.43, 0.39, 1)
        _DetailScale ("Detail per metre", Float) = 0.22
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color, _SoilColor, _RockColor;
        float _DetailScale;
        struct Input { float3 worldPos; fixed4 color : COLOR; };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.worldPos.xz * _DetailScale;
            // Two differently oriented scales break up repeating tiles. Large
            // patches and slope masks are baked into vertex colours, not
            // evaluated as expensive noise for every screen pixel.
            fixed detail = tex2D(_MainTex, uv).r;
            fixed broad = tex2D(_MainTex, float2(uv.x + uv.y, uv.y - uv.x) * 0.073).r;
            fixed3 baseColor = lerp(_Color.rgb, _SoilColor.rgb, IN.color.r);
            baseColor = lerp(baseColor, _RockColor.rgb, IN.color.g);
            o.Albedo = baseColor * (0.72 + detail * 0.45 + broad * 0.24)
                * lerp(0.85, 1.12, IN.color.b);
            o.Metallic = 0;
            o.Smoothness = 0.04;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
