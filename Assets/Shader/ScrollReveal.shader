// 卷轴展开 Shader：从下往上揭示地图，边缘带弯曲
Shader "Custom/ScrollReveal"
{
    Properties
    {
        _MainTex ("地图贴图", 2D) = "white" {}
        _Reveal ("展开进度 (0~1)", Range(0,1)) = 0
        _Curvature ("边缘弯曲强度", Range(0, 0.2)) = 0.05
        _EdgeSoftness ("边缘羽化", Range(0, 0.1)) = 0.02
        _Tint ("整体色调", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        Pass
        {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off

            // 写入 Stencil：只在像素实际显示（alpha>0.5）时写 1
            Stencil
            {
                Ref 1
                Comp Always
                Pass Replace
                ZFail Keep
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Reveal;
            float _Curvature;
            float _EdgeSoftness;
            float4 _Tint;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * _Tint;

                // 弯曲：边缘（x 接近 0 或 1）展开进度略低，中间展开进度略高
                // 用 sin 曲线模拟卷轴展开时中间先出来、两边稍慢的弯曲感
                float edgeFactor = sin(i.uv.x * 3.14159);  // 0~1，中间=1，两边=0
                float curvedReveal = _Reveal - (1.0 - edgeFactor) * _Curvature;

                // UV.y = 0 是底部，1 是顶部
                // 展开时：底部先显示，所以阈值 = 1 - reveal
                float threshold = 1.0 - curvedReveal;

                // 边缘羽化：让裁切边缘有渐变，不生硬
                float diff = i.uv.y - threshold;
                float alpha = smoothstep(-_EdgeSoftness, _EdgeSoftness, diff);
                col.a *= alpha;

                return col;
            }
            ENDCG
        }
    }
}
