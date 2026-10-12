// 程序化草地地面：用 FBM 噪声生成深浅不一的草地颜色，配合法线贴图增加细节
Shader "Custom/GrassGround"
{
    Properties
    {
        _GrassDark   ("Grass Dark", Color) = (0.06, 0.14, 0.06, 1)
        _GrassLight  ("Grass Light", Color) = (0.12, 0.26, 0.10, 1)
        _GrassDry    ("Dry Patch Color", Color) = (0.18, 0.16, 0.08, 1)
        _NoiseScale  ("Noise Scale", Range(1, 50)) = 12
        _NoiseAmp    ("Noise Amplitude", Range(0, 1)) = 0.5
        _DryAmount   ("Dry Patches", Range(0, 1)) = 0.15

        [Header(Normal Map)]
        _BumpMap     ("Normal Map", 2D) = "bump" {}
        _BumpScale   ("Normal Strength", Range(0, 10)) = 3.0
        _NormalTiling("Normal Tiling", Range(1, 50)) = 8

        [Header(Lighting)]
        _Glossiness  ("Smoothness", Range(0, 1)) = 0.3
        _Metallic    ("Metallic", Range(0, 1)) = 0.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _BumpMap;
        float4 _BumpMap_ST;

        struct Input
        {
            float2 uv_BumpMap;
            float3 worldPos;
        };

        fixed4 _GrassDark, _GrassLight, _GrassDry;
        float _NoiseScale, _NoiseAmp, _DryAmount;
        float _BumpScale, _NormalTiling;
        half _Glossiness, _Metallic;

        float hash21(float2 p)
        {
            float3 p3 = frac(float3(p.xyx) * 0.1031);
            p3 += dot(p3, p3.yzx + 33.33);
            return frac((p3.x + p3.y) * p3.z);
        }

        float vnoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            float a = hash21(i);
            float b = hash21(i + float2(1, 0));
            float c = hash21(i + float2(0, 1));
            float d = hash21(i + float2(1, 1));
            float2 u = f * f * (3 - 2 * f);
            return lerp(a, b, u.x) + (c - a) * u.y * (1 - u.x) + (d - b) * u.x * u.y;
        }

        float fbm(float2 p)
        {
            float v = 0;
            float a = 0.5;
            for (int i = 0; i < 5; i++)
            {
                v += a * vnoise(p);
                p *= 2.0;
                a *= 0.5;
            }
            return v;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.worldPos.xz * _NoiseScale * 0.1;

            // 低频地形色（深浅草地区域）
            float baseNoise = fbm(uv * 0.5);
            // 高频草叶细节
            float detailNoise = fbm(uv * 4.0);
            // 干草斑块
            float dryNoise = fbm(uv * 0.3 + 100.0);
            float dryMask = smoothstep(0.7 - _DryAmount, 0.7, dryNoise);

            float3 grass = lerp(_GrassDark.rgb, _GrassLight.rgb, baseNoise);
            grass = lerp(grass, grass * (0.7 + detailNoise * 0.6), _NoiseAmp);
            grass = lerp(grass, _GrassDry.rgb, dryMask * 0.6);

            o.Albedo = grass;

            float2 nUV = IN.worldPos.xz * _NormalTiling * 0.1;
            float3 normal = UnpackNormal(tex2D(_BumpMap, nUV));
            normal.xy *= _BumpScale;
            o.Normal = normalize(normal);

            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
