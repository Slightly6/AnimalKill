// 虚影专用：Custom/PlayingCard 的半透明版本。
// 只给桌面"出牌虚影"用，真牌永远用 Custom/PlayingCard（不透明）。
// 与原 shader 的区别：Transparent 队列 + 透明度混合 + _GhostAlpha 控制整体透明。
Shader "Custom/PlayingCardGhost"
{
    Properties
    {
        _MainTex ("卡牌贴图", 2D) = "white" {}
        _Color ("整体染色（保持白色）", Color) = (1,1,1,1)
        _GhostAlpha ("虚影透明度", Range(0,1)) = 0.45

        _PaperTint    ("纸色冷调（乘色）", Color) = (0.96, 0.97, 1.0, 1)
        _Vignette     ("边缘暗角强度", Range(0, 1)) = 0.28
        _GrainScale   ("颗粒密度", Range(40, 600)) = 220
        _GrainStrength("颗粒强度", Range(0, 0.3)) = 0.05

        _RimColor     ("边缘光颜色", Color) = (0.36, 0.55, 0.62, 1)
        _RimStrength  ("边缘光强度", Range(0, 2)) = 0.55
        _RimPower     ("边缘光锐利度", Range(0.5, 8)) = 3.2
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

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
            half _GhostAlpha;

            fixed4 _PaperTint;
            float _Vignette;
            float _GrainScale;
            float _GrainStrength;

            fixed4 _RimColor;
            float _RimStrength;
            float _RimPower;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

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
                col *= _PaperTint.rgb;

                float grain = hash21(i.uv * _GrainScale);
                col *= 1.0 + (grain - 0.5) * 2.0 * _GrainStrength;

                float2 d = i.uv - 0.5;
                float vig = 1.0 - saturate(dot(d, d) * 2.2);
                col *= lerp(1.0 - _Vignette, 1.0, vig);

                float3 N = normalize(i.worldNormal);
                float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);
                float fres = pow(1.0 - saturate(abs(dot(N, V))), _RimPower);
                col += _RimColor.rgb * fres * _RimStrength;

                return fixed4(col, tex.a * _Color.a * _GhostAlpha);
            }
            ENDCG
        }

        // 阴影投射 Pass：让方向光把虚影的牌形轮廓投到桌面（真实阴影）
        // 虚影是半透明体，这里按几何体轮廓投实心牌影（一张立牌本就该是矩形影子）
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Cull Off

            CGPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"

            struct shadowV2f
            {
                V2F_SHADOW_CASTER;
            };

            shadowV2f shadowVert(appdata_base v)
            {
                shadowV2f o;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }

            float4 shadowFrag(shadowV2f i) : SV_Target
            {
                return 0;
            }
            ENDCG
        }
    }
    Fallback "Unlit/Transparent"
}
