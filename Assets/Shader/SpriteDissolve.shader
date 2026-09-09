Shader "Custom/SpriteDissolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite 贴图", 2D) = "white" {}
        _Color ("颜色", Color) = (1,1,1,1)
        _DissolveTex ("溶解噪声图", 2D) = "white" {}
        _DissolveAmount ("溶解进度", Range(0,1)) = 0
        _BurnColor ("溶解边缘颜色", Color) = (1,1,0,1)
        _BurnSize ("溶解边缘宽度", Range(0,1)) = 0
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _DissolveTex;
            fixed4 _Color;
            fixed4 _BurnColor;
            float _DissolveAmount;
            float _BurnSize;

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.texcoord = v.texcoord;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 采样 sprite 自己的贴图
                fixed4 c = tex2D(_MainTex, i.texcoord) * i.color;

                // 采样噪声图，得到 0~1
                fixed noise = tex2D(_DissolveTex, i.texcoord).r;

                // 核心：噪声值小于溶解进度，就丢弃这块像素（溶解）
                clip(noise - _DissolveAmount);

                // 边缘发光：靠近溶解线的像素，混入燃烧色
                if (noise - _DissolveAmount < _BurnSize)
                {
                    c.rgb = _BurnColor.rgb;
                }

                // 预乘 alpha（Sprite 的标准混合方式）
                c.rgb *= c.a;
                return c;
            }
        ENDCG
        }
    }
}
