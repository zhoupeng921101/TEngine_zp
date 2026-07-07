Shader "UI/GlowCell"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _GlowSharpness ("Glow Sharpness", Range(0.5, 4)) = 1.2
        _GlowIntensity ("Glow Intensity", Range(1, 4)) = 1.1
        _CoreFrac ("Core Fraction", Range(0.2, 1)) = 0.62
        _Vertical ("Vertical", Float) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
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
        Blend SrcAlpha One
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

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
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _GlowSharpness;
            float _GlowIntensity;
            float _CoreFrac;
            float _Vertical;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // 纯 UV 计算沿「带边缘」的描边辉光，不采样 _MainTex（不依赖 sprite 形状）。
                // quad 是一条横贯整行/整列的长条，比真实行/列更宽（_CoreFrac = 真实行厚度 / quad 厚度）；
                // 沿带厚度方向最亮的两条线落在真实行的上下两条长边上，向两侧软晕开。
                // a = 沿「带厚度」方向的归一化坐标（横条取 y、竖条取 x）
                float a = (_Vertical > 0.5) ? IN.texcoord.x : IN.texcoord.y;
                float c0 = (1.0 - _CoreFrac) * 0.5;   // 真实行的近端边缘位置
                float c1 = 1.0 - c0;                  // 真实行的远端边缘位置
                float dEdge = min(abs(a - c0), abs(a - c1));   // 到最近一条核心边缘的距离
                float glow = pow(saturate(1.0 - dEdge / max(c0, 1e-4)), _GlowSharpness); // 边缘最亮，向两侧晕开

                fixed4 color = IN.color;
                color.rgb *= _GlowIntensity;   // 加色辉光提亮：rgb 越高叠加越亮
                color.a *= glow;               // 边缘衰减控制描边形状

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
