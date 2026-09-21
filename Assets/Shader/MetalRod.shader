// 卷轴金属杆 UI Shader（黑暗草原风格）
// 用法：新建材质用这个 shader，拖到 TopRod / BottomRod 的 Image.Material 槽，Image.Color 设为纯白。
// 不依赖贴图：圆柱面渐变高光带 + 两端圆头端帽（颜色模拟凸起）+ 表面颗粒 + 顶底细金高光线 全部程序生成。
// 兼容 RectMask2D 裁切、Mask、CanvasGroup 透明度。
Shader "UI/MetalRod"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint（Image 自带颜色，保持白色即可）", Color) = (1,1,1,1)

        [Header(Rod Metal)]
        _RodColor       ("杆身主色（发乌旧铜）", Color) = (0.30, 0.23, 0.15, 1)
        _ShadowColor    ("杆身暗部（近黑褐）", Color) = (0.06, 0.05, 0.04, 1)
        _RustColor      ("锈迹颜色", Color) = (0.30, 0.14, 0.07, 1)
        _RustStrength   ("锈迹强度", Range(0, 1)) = 0.45
        _HighlightColor ("高光带颜色（灰哑）", Color) = (0.46, 0.42, 0.34, 1)
        _HighlightY     ("高光带位置（0=下 1=上）", Range(0, 1)) = 0.62
        _HighlightWidth ("高光带宽度", Range(0.02, 0.5)) = 0.14
        _HighlightStrength ("高光带强度（压低=旧金属）", Range(0, 1)) = 0.30

        [Header(Surface Grain)]
        _GrainScale     ("颗粒密度", Range(20, 600)) = 200
        _GrainStrength  ("颗粒强度（麻点旧金属）", Range(0, 0.5)) = 0.20

        [Header(End Caps)]
        _CapWidth       ("端帽占比", Range(0.02, 0.25)) = 0.08
        _CapColor       ("端帽主色（暗铜）", Color) = (0.21, 0.16, 0.10, 1)
        _CapDarkColor   ("端帽暗部（最深处）", Color) = (0.04, 0.03, 0.025, 1)
        _CapHighlight   ("端帽高光（闷亮）", Color) = (0.36, 0.31, 0.23, 1)
        _CapCurve       ("端帽圆头曲度（越大越凸）", Range(0.5, 4)) = 1.6

        [Header(Edge Lines)]
        _EdgeLineColor  ("顶底反光线颜色（旧铜）", Color) = (0.40, 0.33, 0.22, 1)
        _EdgeLineWidth  ("顶底反光线宽度", Range(0.002, 0.05)) = 0.012
        _EdgeLineAlpha  ("顶底反光线强度（压暗）", Range(0, 1)) = 0.28

        // UI 标准 Stencil 配置
        [PerRendererData] _StencilComp ("Stencil Comparison", Float) = 8
        [PerRendererData] _Stencil ("Stencil ID", Float) = 0
        [PerRendererData] _StencilOp ("Stencil Operation", Float) = 0
        [PerRendererData] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [PerRendererData] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [PerRendererData] _ColorMask ("Color Mask", Float) = 15
        [PerRendererData] _ClipRect ("Clip Rect", Vector) = (-32767,-32767,32767,32767)
        [PerRendererData] _ClipSoftness ("Clip Softness", Vector) = (0,0,0,0)
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

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        ColorMask [_ColorMask]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 uv       : TEXCOORD0;
                float4 worldPos : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float4 _ClipRect;

            fixed4 _RodColor;
            fixed4 _ShadowColor;
            fixed4 _RustColor;
            float _RustStrength;
            fixed4 _HighlightColor;
            float _HighlightY;
            float _HighlightWidth;
            float _HighlightStrength;
            float _GrainScale;
            float _GrainStrength;
            float _CapWidth;
            fixed4 _CapColor;
            fixed4 _CapDarkColor;
            fixed4 _CapHighlight;
            float _CapCurve;
            fixed4 _EdgeLineColor;
            float _EdgeLineWidth;
            float _EdgeLineAlpha;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPos = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            // 程序噪声
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.uv;

                // === 1. 圆柱面渐变（垂直方向） ===
                // sin(uv.y * PI)：中间（0.5）= 1 最亮，上下 = 0 最暗 → 模拟圆柱弧面反光
                float cyl = sin(uv.y * 3.14159265);
                half3 col = lerp(_ShadowColor.rgb, _RodColor.rgb, cyl);

                // === 2. 高光带：在某个 y 位置叠一条亮带（高斯衰减，旧金属压得很弱） ===
                float dy = uv.y - _HighlightY;
                float highlight = exp(-dy * dy / (_HighlightWidth * _HighlightWidth));
                col = lerp(col, _HighlightColor.rgb, highlight * _HighlightStrength);

                // === 3. 表面颗粒（麻点） ===
                float g1 = valueNoise(uv * _GrainScale);
                float g2 = valueNoise(uv * _GrainScale * 2.3 + 17.5);
                float grain = g1 * 0.6 + g2 * 0.4;
                float grainMul = lerp(1.0 - _GrainStrength, 1.0 + _GrainStrength, grain);
                col *= grainMul;

                // === 3.5 锈迹：低频大块噪声 + 高频斑点，超阈值处混入锈红 ===
                float rustBig = valueNoise(uv * 7.0 + 31.7);          // 大块锈斑分布
                float rustSpot = valueNoise(uv * _GrainScale * 0.6 + 5.2); // 麻点锈
                float rustMask = smoothstep(0.52, 0.78, rustBig * 0.7 + rustSpot * 0.3);
                col = lerp(col, _RustColor.rgb, rustMask * _RustStrength);

                // === 4. 两端圆头端帽 ===
                // 端帽区域：uv.x < _CapWidth（左端）或 uv.x > 1 - _CapWidth（右端）
                // 端帽内沿 capU = 0（最外端）→ 1（与杆身衔接处）
                // 圆头凸起：sin(capU * PI) → 中间最高（颜色更亮/凸），两端最低（最暗）
                float capU = 0.0;
                float isCap = 0.0;
                if (uv.x < _CapWidth)
                {
                    capU = uv.x / _CapWidth;          // 左端：0=外 1=内
                    isCap = 1.0;
                }
                else if (uv.x > 1.0 - _CapWidth)
                {
                    capU = (1.0 - uv.x) / _CapWidth;   // 右端：0=外 1=内
                    isCap = 1.0;
                }

                if (isCap > 0.5)
                {
                    // 圆头凸起：曲度 _CapCurve 控制凸度
                    float capRise = pow(sin(capU * 3.14159265), 1.0 / _CapCurve);
                    // 端帽纵向也做圆柱渐变（端帽本身也是圆柱）
                    float capCyl = sin(uv.y * 3.14159265);
                    // 端帽色：从暗部 → 主色 → 高光，按凸起程度
                    half3 capCol = lerp(_CapDarkColor.rgb, _CapColor.rgb, capCyl);
                    capCol = lerp(capCol, _CapHighlight.rgb, capRise * 0.7);
                    // 外端（capU→0）整体压暗，模拟端帽比杆身更深的视觉
                    capCol *= lerp(0.55, 1.0, capU);
                    col = capCol;

                    // 端帽颗粒（沿用同一 grain）+ 同样上锈
                    col *= grainMul;
                    col = lerp(col, _RustColor.rgb, rustMask * _RustStrength);
                }

                // === 5. 顶/底细金高光线（金属边缘反光） ===
                float edgeLine = 0.0;
                edgeLine = max(edgeLine, 1.0 - smoothstep(0.0, _EdgeLineWidth, uv.y));          // 底
                edgeLine = max(edgeLine, 1.0 - smoothstep(0.0, _EdgeLineWidth, 1.0 - uv.y));   // 顶
                col = lerp(col, _EdgeLineColor.rgb, edgeLine * _EdgeLineAlpha);

                // === 6. 端帽与杆身衔接处一条细暗线（增强立体感） ===
                float seamL = 1.0 - smoothstep(0.0, 0.008, abs(uv.x - _CapWidth));
                float seamR = 1.0 - smoothstep(0.0, 0.008, abs(uv.x - (1.0 - _CapWidth)));
                float seam = max(seamL, seamR);
                col = lerp(col, _CapDarkColor.rgb, seam * 0.5);

                // === 7. 标准 UI 输出 ===
                half4 sprite = tex2D(_MainTex, uv) * IN.color;
                half4 OUT;
                OUT.rgb = col * sprite.rgb;
                OUT.a = sprite.a;

                OUT.a *= UnityGet2DClipping(IN.worldPos.xy, _ClipRect);
                clip(OUT.a - 0.001);
                return OUT;
            }
            ENDCG
        }
    }
}
