Shader "Custom/DoubleSidedPaper"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Paper Texture", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0, 1)) = 0.1
        _Metallic ("Metallic", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        LOD 200

        // 关闭正面和背面剔除
        Cull Off

        CGPROGRAM

        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
            float facing : VFACE;
        };

        fixed4 _Color;
        half _Glossiness;
        half _Metallic;

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 color = tex2D(_MainTex, IN.uv_MainTex) * _Color;

            o.Albedo = color.rgb;
            o.Alpha = color.a;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;

            // 背面翻转法线，避免背面光照异常
            o.Normal *= IN.facing >= 0 ? 1 : -1;
        }

        ENDCG
    }

    FallBack "Standard"
}