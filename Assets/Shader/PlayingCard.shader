// 3D 扑克牌专用 Shader（黑暗草原·月光森林风格）
// 挂到 Card 预制体上的 CardArt / CardFront / CardBack / Card 四个材质。
// 不依赖额外贴图：保留 _MainTex（脚本运行时换动物图），质感全程序生成。
// 特性：月光边缘光（Fresnel）、旧卡纸冷调、暗角、细微颗粒、受击闪红 _HitFlash、选中扫光 _SelectGlow。
Shader "Custom/PlayingCard"
{
    Properties
    {
        _MainTex ("卡牌贴图", 2D) = "white" {}
        _Color ("整体染色（保持白色）", Color) = (1,1,1,1)

        [Header(Paper Look)]
        _PaperTint    ("纸色冷调（乘色）", Color) = (0.96, 0.97, 1.0, 1)
        _Vignette     ("边缘暗角强度", Range(0, 1)) = 0.28
        _GrainScale   ("颗粒密度", Range(40, 600)) = 220
        _GrainStrength("颗粒强度", Range(0, 0.3)) = 0.05

        [Header(Moonlight Rim)]
        _RimColor     ("边缘光颜色（月夜青蓝）", Color) = (0.36, 0.55, 0.62, 1)
        _RimStrength  ("边缘光强度", Range(0, 2)) = 0.55
        _RimPower     ("边缘光锐利度（越小越宽）", Range(0.5, 8)) = 3.2

        [Header(Hit Feedback)]
        _HitFlash     ("受击红闪（脚本驱动，勿手调）", Range(0, 1)) = 0
        _HitColor     ("受击颜色", Color) = (0.85, 0.10, 0.07, 1)

        [Header(Skill Flash)]
        _SkillFlash   ("技能闪光（脚本驱动，勿手调）", Range(0, 1)) = 0
        _SkillColor   ("技能闪光颜色（青绿）", Color) = (0.15, 0.85, 0.45, 1)

        [Header(Select Scan)]
        _SelectGlow   ("选中状态（脚本驱动，0/1）", Range(0, 1)) = 0
        _ScanColor    ("扫光颜色（冷月白）", Color) = (0.72, 0.88, 1.0, 1)
        _ScanWidth    ("扫光带宽度", Range(0.02, 0.5)) = 0.16
    }

    SubShader
    {
        Tags
        {
            "Queue"="Geometry"
            "RenderType"="Opaque"
        }

        Cull Off        // 牌翻面后背面物体也要正常渲染
        Lighting Off
        ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex    : SV_POSITION;
                float2 uv        : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos  : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            fixed4 _PaperTint;
            float _Vignette;
            float _GrainScale;
            float _GrainStrength;

            fixed4 _RimColor;
            float _RimStrength;
            float _RimPower;

            float _HitFlash;
            fixed4 _HitColor;

            float _SkillFlash;
            fixed4 _SkillColor;

            float _SelectGlow;
            fixed4 _ScanColor;
            float _ScanWidth;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            // 轻量程序噪声（纸面颗粒）
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                half3 col = tex.rgb * _Color.rgb;

                // --- 旧卡纸：整体冷调（极轻，不盖掉动物图颜色） ---
                col *= _PaperTint.rgb;

                // --- 颗粒：明暗 ±_GrainStrength 的细噪点 ---
                float grain = hash21(i.uv * _GrainScale);
                col *= 1.0 + (grain - 0.5) * 2.0 * _GrainStrength;

                // --- 暗角：离 UV 中心越远越暗 ---
                float2 d = i.uv - 0.5;
                float vig = 1.0 - saturate(dot(d, d) * 2.2);
                col *= lerp(1.0 - _Vignette, 1.0, vig);

                // --- 月光边缘光：视角越擦过牌面，边缘光越强（俯视桌面时只在斜边缘隐约可见） ---
                float3 N = normalize(i.worldNormal);
                float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);
                float fres = pow(1.0 - saturate(abs(dot(N, V))), _RimPower);
                col += _RimColor.rgb * fres * _RimStrength;

                // --- 受击红闪：整牌泛红 ---
                col = lerp(col, _HitColor.rgb, _HitFlash * 0.62);

                // --- 技能闪光：整牌泛青绿（比受击更亮，让玩家明确知道技能触发了）---
                col = lerp(col, _SkillColor.rgb, _SkillFlash * 0.55);

                // --- 选中扫光：一道冷光沿 X 方向循环扫过（只有 _SelectGlow=1 时出现） ---
                // 带斜向感，扫到的位置最亮，两侧软衰减
                float scanPos = frac(_Time.y * 0.55);
                float coord = i.uv.x + i.uv.y * 0.35;
                float band = 1.0 - smoothstep(0.0, _ScanWidth, abs(frac(coord - scanPos + 0.5) - 0.5) * 2.0);
                col += _ScanColor.rgb * band * _SelectGlow * 0.6;
                // 选中时再叠一层极淡的常驻冷光，提示"这张牌是亮的"
                col += _ScanColor.rgb * _SelectGlow * 0.05;

                return fixed4(col, tex.a * _Color.a);
            }
            ENDCG
        }
    }
    Fallback "Unlit/Texture"
}
