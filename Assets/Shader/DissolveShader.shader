Shader "Custom/Dissolve"
  {
      Properties
      {
          _Color ("颜色", Color) = (1,1,1,1)
          _MainTex ("主贴图", 2D) = "white" {}
          _DissolveTex ("溶解噪声图", 2D) = "white" {}
          _DissolveAmount ("溶解进度", Range(0,1)) = 0
          _BurnColor ("溶解边缘颜色", Color) = (1,1,0,1)
          _BurnSize ("溶解边缘宽度", Range(0,1)) = 0
      }
      SubShader
      {
          Tags { "RenderType"="Opaque" }
          Cull Off

          CGPROGRAM
          #pragma surface surf Standard fullforwardshadows

          sampler2D _MainTex;
          sampler2D _DissolveTex;
          fixed4 _Color;
          fixed4 _BurnColor;
          float _DissolveAmount;
          float _BurnSize;

          struct Input
          {
              float2 uv_MainTex;
              float2 uv_DissolveTex;
          };

          void surf (Input IN, inout SurfaceOutputStandard o)
          {
              // 主贴图
              fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;

              // 采样噪声图，得到 0~1 的值
              fixed noise = tex2D(_DissolveTex, IN.uv_DissolveTex).r;

              // 核心：噪声值小于溶解进度，就丢弃这块像素（溶解）
              clip(noise - _DissolveAmount);

              // 边缘发光：靠近溶解线的像素，混入燃烧色
              if (noise - _DissolveAmount < _BurnSize)
              {
                  c.rgb = _BurnColor.rgb;
              }

              o.Albedo = c.rgb;
              o.Alpha = c.a;
              o.Metallic = 0;
              o.Smoothness = 0.2;
          }
          ENDCG
      }
      FallBack "Diffuse"
  }