// 卷轴羊皮纸 UI Shader（黑暗草原风格·升级版）
// 用法：新建材质用这个 shader，拖到 MapPaper 的 Image.Material 槽，Image 的 Color 设为纯白。
// 不依赖贴图：纸张颗粒/纤维/折痕水渍/不规则破损边/焦边/杆影/金线/月光晕全部程序生成。
// 兼容 RectMask2D 裁切、Mask、CanvasGroup 透明度。
Shader "UI/ScrollParchment"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint（Image 自带颜色，保持白色即可）", Color) = (1,1,1,1)

        [Header(Paper)]
        _PaperColor    ("纸色（破旧牛皮棕）", Color) = (0.32, 0.235, 0.15, 1)
        _GrainScale    ("颗粒密度", Range(10, 400)) = 110
        _GrainStrength ("颗粒强度", Range(0, 1)) = 0.24

        [Header(Fiber)]
        _FiberStrength  ("纤维强度", Range(0, 1)) = 0.38
        _FiberStretch   ("纤维拉伸（越大越细越长）", Range(1, 20)) = 6.0
        _FiberAngle     ("纤维方向（0=竖 90=横）", Range(0, 90)) = 12

        [Header(Stains)]
        _StainScale     ("水渍频率", Range(2, 30)) = 8
        _StainStrength  ("水渍强度", Range(0, 1)) = 0.38
        _StainColor     ("水渍颜色（深褐）", Color) = (0.13, 0.085, 0.05, 1)
        _StainThreshold ("水渍阈值（越大越少斑）", Range(0.3, 0.95)) = 0.60

        [Header(Torn Edges)]
        _TearAmplitude  ("破损幅度", Range(0, 0.1)) = 0.03
        _TearFrequency  ("破损频率", Range(5, 50)) = 22

        [Header(Burnt Edge)]
        _EdgeColor    ("边缘焦黑颜色", Color) = (0.025, 0.018, 0.012, 1)
        _EdgeWidth    ("焦边宽度", Range(0.01, 0.5)) = 0.24
        _EdgeStrength ("焦边强度", Range(0, 1)) = 0.96

        [Header(Rod Shadow)]
        _RodShadow ("杆投影强度", Range(0, 1)) = 0.65
        _RodWidth  ("杆影宽度", Range(0.01, 0.3)) = 0.1
        _RodSoftness ("杆影柔和度（越大越散）", Range(0.5, 6)) = 2.0

        [Header(Gold Lines)]
        _InkColor  ("描线颜色（褪色旧铜）", Color) = (0.52, 0.40, 0.22, 1)
        _InkInset  ("描线距边", Range(0, 0.1)) = 0.04
        _InkWidth  ("描线粗细", Range(0.001, 0.02)) = 0.006
        _InkAlpha  ("描线透明度（暗淡）", Range(0, 1)) = 0.55

        [Header(Moonlight Rim)]
        _RimGlowColor ("冷晕颜色（月夜青蓝）", Color) = (0.10, 0.20, 0.27, 1)
        _RimGlow      ("冷晕强度（只压边缘）", Range(0, 1)) = 0.26

        // UI 标准 Stencil 配置（支持 Mask 组件；RectMask2D 走 _ClipRect，两边都兼容）
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

            fixed4 _PaperColor;
            float _GrainScale;
            float _GrainStrength;
            float _FiberStrength;
            float _FiberStretch;
            float _FiberAngle;
            float _StainScale;
            float _StainStrength;
            fixed4 _StainColor;
            float _StainThreshold;
            float _TearAmplitude;
            float _TearFrequency;
            fixed4 _EdgeColor;
            float _EdgeWidth;
            float _EdgeStrength;
            float _RodShadow;
            float _RodWidth;
            float _RodSoftness;
            fixed4 _InkColor;
            float _InkInset;
            float _InkWidth;
            float _InkAlpha;
            fixed4 _RimGlowColor;
            float _RimGlow;

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

            // ===== 程序噪声 =====
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

            // 旋转矩阵：让纤维方向可调
            float2 rotate(float2 p, float deg)
            {
                float r = radians(deg);
                float s, c; sincos(r, s, c);
                return mul(float2x2(c, -s, s, c), p);
            }

            // 各向异性纤维噪声：拉伸某个方向，模拟纸纤维走向
            float fiberNoise(float2 uv)
            {
                float2 p = rotate(uv, _FiberAngle);
                p.x *= _FiberStretch;   // 拉伸 x 方向 → 长条状纤维
                return valueNoise(p * _GrainScale * 0.5);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.uv;

                // --- 颗粒：一层大颗粒 + 一层细颗粒 ---
                float n1 = valueNoise(uv * _GrainScale);
                float n2 = valueNoise(uv * _GrainScale * 2.7 + 13.7);
                float grain = n1 * 0.65 + n2 * 0.35;
                float grainMul = lerp(1.0 - _GrainStrength, 1.0 + _GrainStrength, grain);

                // --- 纤维方向：在颗粒基础上叠一层拉伸噪声，让纸看起来有走向 ---
                float fiber = fiberNoise(uv);
                float fiberMul = lerp(1.0 - _FiberStrength * 0.5, 1.0 + _FiberStrength * 0.5, fiber);
                grainMul *= fiberMul;

                half3 col = _PaperColor.rgb * grainMul;

                // --- 折痕水渍：低频 noise，超阈值的位置加深偏黄 ---
                float stainN = valueNoise(uv * _StainScale + 71.3);
                float stainMask = smoothstep(_StainThreshold, _StainThreshold + 0.08, stainN);
                col = lerp(col, _StainColor.rgb, stainMask * _StainStrength);

                // --- 不规则破损边：用 1D noise 扰动 edgeDist，让边缘不是直线 ---
                float tearN = valueNoise(float2(uv.y * _TearFrequency, uv.x * _TearFrequency * 0.5 + 9.1));
                float tearOffset = (tearN - 0.5) * _TearAmplitude;
                float edgeDist = min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y));
                edgeDist += tearOffset;

                // --- 焦边：离四条边越近越黑 ---
                float edge = 1.0 - smoothstep(0.0, _EdgeWidth, edgeDist);

                // --- 四角额外压暗（椭圆形衰减，避免死板） ---
                float2 centered = uv - 0.5;
                float radial = length(centered * float2(0.75, 1.0));
                float corner = 1.0 - smoothstep(0.15, 0.72, radial);

                float darkening = max(edge, corner * 0.45);
                col = lerp(col, _EdgeColor.rgb, darkening * _EdgeStrength);

                // --- 杆投影：靠上下边柔和高斯衰减（杆卷在杆上的 AO） ---
                // 改用高斯衰减，比 smoothstep 阶跃更柔和、过渡更长
                float dyTop = uv.y;                 // 距下边距离
                float dyBot = 1.0 - uv.y;           // 距上边距离
                float dyEdge = min(dyTop, dyBot);
                // exp 衰减：杆影宽度 _RodWidth，柔和度 _RodSoftness 越大过渡越长
                float rodAO = exp(-dyEdge * dyEdge * _RodSoftness / (_RodWidth * _RodWidth + 0.0001));
                rodAO = saturate(rodAO);
                col *= lerp(1.0, 1.0 - _RodShadow, rodAO);

                // --- 金线描边：距边 _InkInset 的矩形细线 ---
                float dl = min(abs(uv.x - _InkInset), abs(uv.x - (1.0 - _InkInset)));
                float db = min(abs(uv.y - _InkInset), abs(uv.y - (1.0 - _InkInset)));
                float inkLine = 1.0 - smoothstep(0.0, _InkWidth, min(dl, db));
                col = lerp(col, _InkColor.rgb, inkLine * _InkAlpha);

                // --- 月光：最外缘冷晕 + 整张纸极淡的夜色冷调（不盖掉牛皮纸暖棕） ---
                col += _RimGlowColor.rgb * edge * _RimGlow;
                col = lerp(col, col * half3(0.82, 0.90, 1.02), _RimGlow * 0.18);

                // --- 标准 UI 输出：乘 Image 颜色（顶点色），走 RectMask2D 裁切 ---
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
