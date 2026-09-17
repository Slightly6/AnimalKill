// 黑暗草原天空盒：上深下浅的青黑渐变，带一点星星
Shader "Custom/DarkGrasslandSkybox"
{
    Properties
    {
        _TopColor ("Top Color", Color) = (0.02, 0.05, 0.06, 1)      // 顶部近黑
        _BottomColor ("Bottom Color", Color) = (0.08, 0.16, 0.14, 1) // 底部墨绿
        _Exponent ("Gradient Exponent", Range(0.5, 4)) = 1.5
        _StarColor ("Star Color", Color) = (0.7, 0.9, 1, 1)
        _StarDensity ("Star Density", Range(0, 1)) = 0.5
        _StarBrightness ("Star Brightness", Range(0, 2)) = 0.8
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldDir : TEXCOORD0;
            };

            float4 _TopColor;
            float4 _BottomColor;
            float _Exponent;
            float4 _StarColor;
            float _StarDensity;
            float _StarBrightness;

            // 简单哈希伪随机
            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldDir = normalize(v.vertex.xyz);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 按世界方向的 Y 分量做渐变（天空顶部 Y=1，地平线 Y=0）
                float t = saturate(i.worldDir.y * 0.5 + 0.5);
                t = pow(t, _Exponent);
                float3 col = lerp(_BottomColor.rgb, _TopColor.rgb, t);

                // 星星：只在上半部分
                if (i.worldDir.y > 0)
                {
                    float3 starPos = floor(i.worldDir * 200.0);
                    float r = hash(starPos);
                    if (r > (1.0 - _StarDensity * 0.15))
                    {
                        // 星星闪烁
                        float twinkle = 0.6 + 0.4 * sin(_Time.y * 3.0 + r * 50.0);
                        col += _StarColor.rgb * _StarBrightness * twinkle * step(0.99, r);
                    }
                }

                return float4(col, 1.0);
            }
            ENDCG
        }
    }
}
