// 黑暗草原天空盒：深青黑渐变 + 月亮 + 星河 + 远山剪影 + 地平线辉光 + 云层
Shader "Custom/DarkGrasslandSkybox"
{
    Properties
    {
        _TopColor      ("Top Color", Color) = (0.01, 0.02, 0.04, 1)
        _BottomColor   ("Bottom Color", Color) = (0.05, 0.10, 0.10, 1)
        _HorizonColor  ("Horizon Glow Color", Color) = (0.10, 0.16, 0.14, 1)
        _Exponent      ("Gradient Exponent", Range(0.5, 4)) = 1.4
        _HorizonGlow   ("Horizon Glow Width", Range(0.01, 0.5)) = 0.15

        _StarColor     ("Star Color", Color) = (0.7, 0.9, 1, 1)
        _StarDensity   ("Star Density", Range(0, 1)) = 0.6
        _StarBrightness("Star Brightness", Range(0, 3)) = 0.9

        [Header(Moon)]
        _MoonDir       ("Moon Direction", Vector) = (0.5, 0.6, 0.3, 0)
        _MoonColor     ("Moon Color", Color) = (1.0, 0.96, 0.85, 1)
        _MoonSize      ("Moon Size", Range(0.01, 0.1)) = 0.035
        _MoonHalo      ("Moon Halo", Range(0, 1)) = 0.35

        [Header(Milky Way)]
        _GalaxyColor   ("Galaxy Color", Color) = (0.5, 0.6, 0.85, 1)
        _GalaxyTilt    ("Galaxy Tilt (deg)", Range(-80, 80)) = -25
        _GalaxyWidth   ("Galaxy Width", Range(0.05, 0.5)) = 0.18
        _GalaxyBright  ("Galaxy Brightness", Range(0, 1)) = 0.25

        [Header(Mountains)]
        _MountainColor ("Mountain Color", Color) = (0.02, 0.04, 0.035, 1)
        _MountainHeight("Mountain Height", Range(0, 0.3)) = 0.08
        _MountainFreq  ("Mountain Frequency", Range(1, 30)) = 8

        [Header(Clouds)]
        _CloudColor    ("Cloud Color", Color) = (0.12, 0.16, 0.18, 1)
        _CloudDensity  ("Cloud Density", Range(0, 1)) = 0.4
        _CloudSpeed    ("Cloud Speed", Range(0, 2)) = 0.08
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

            float4 _TopColor, _BottomColor, _HorizonColor;
            float _Exponent, _HorizonGlow;
            float4 _StarColor;
            float _StarDensity, _StarBrightness;

            float4 _MoonDir, _MoonColor;
            float _MoonSize, _MoonHalo;

            float4 _GalaxyColor;
            float _GalaxyTilt, _GalaxyWidth, _GalaxyBright;

            float4 _MountainColor;
            float _MountainHeight, _MountainFreq;

            float4 _CloudColor;
            float _CloudDensity, _CloudSpeed;

            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

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
                for (int i = 0; i < 4; i++)
                {
                    v += a * vnoise(p);
                    p *= 2.0;
                    a *= 0.5;
                }
                return v;
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
                float3 dir = normalize(i.worldDir);
                float h = dir.y * 0.5 + 0.5;

                // 基础渐变 + 地平线辉光
                float t = pow(h, _Exponent);
                float3 col = lerp(_BottomColor.rgb, _TopColor.rgb, t);
                float horizon = exp(-pow((h - 0.0) / _HorizonGlow, 2));
                col += _HorizonColor.rgb * horizon * 0.4;

                // 星河（只在上半部）
                if (dir.y > 0.02)
                {
                    float tiltRad = radians(_GalaxyTilt);
                    float2 horizonPlane = dir.xz;
                    float angle = atan2(horizonPlane.y, horizonPlane.x) + tiltRad;
                    float distToBand = abs(frac(angle / 6.2831 + 0.5) - 0.5) * 2.0;
                    distToBand = abs(dir.y) * 0.5 + distToBand * 0.5;
                    float band = exp(-pow(distToBand / _GalaxyWidth, 2));
                    float galaxyNoise = fbm(dir.xz * 30.0) * fbm(dir.xy * 50.0 + 10.0);
                    col += _GalaxyColor.rgb * band * _GalaxyBright * (0.5 + galaxyNoise * 0.8) * step(0, dir.y);
                }

                // 星星（只在上半部）
                if (dir.y > 0)
                {
                    float3 starPos = floor(dir * 220.0);
                    float r = hash(starPos);
                    float starMask = step(0.985, r);
                    float twinkle = 0.7 + 0.3 * sin(_Time.y * 2.5 + r * 60.0);
                    col += _StarColor.rgb * _StarBrightness * twinkle * starMask * _StarDensity;
                }

                // 月亮
                float3 moonDir = normalize(_MoonDir.xyz);
                float moonDot = dot(dir, moonDir);
                if (moonDot > 0.0)
                {
                    float moonDist = acos(moonDot);
                    float moonDisk = smoothstep(_MoonSize, _MoonSize * 0.7, moonDist);
                    float moonHalo = exp(-moonDist * 40.0) * _MoonHalo;
                    float moonCraters = fbm(dir.xz * 400.0 + 50.0) * 0.15;
                    col += _MoonColor.rgb * (moonDisk * (1.0 - moonCraters) + moonHalo) * step(0, dir.y);
                }

                // 远山剪影（地平线附近）
                if (dir.y < _MountainHeight * 2.0 && dir.y > -0.05)
                {
                    float angle = atan2(dir.z, dir.x);
                    float mNoise = fbm(float2(angle * _MountainFreq, 0.0)) * 0.5 + 0.5;
                    float mLine = _MountainHeight * (0.6 + mNoise * 0.8);
                    float mountain = smoothstep(mLine, mLine - 0.008, dir.y);
                    col = lerp(col, _MountainColor.rgb, mountain);
                }

                // 云层（飘动，只在天空中上部）
                if (dir.y > 0.05)
                {
                    float2 cloudUV = dir.xz / (dir.y + 0.1) * 3.0;
                    cloudUV.x += _Time.x * _CloudSpeed;
                    float cloud = fbm(cloudUV);
                    cloud = smoothstep(0.4 - _CloudDensity * 0.3, 0.7, cloud) * _CloudDensity;
                    col = lerp(col, _CloudColor.rgb, cloud * 0.35 * smoothstep(0.05, 0.3, dir.y));
                }

                return float4(col, 1.0);
            }
            ENDCG
        }
    }
}
