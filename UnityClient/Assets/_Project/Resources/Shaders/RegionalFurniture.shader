Shader "Shattered Sail/UI/Regional Furniture"
{
    Properties
    {
        [PerRendererData] _MainTex ("Furniture atlas", 2D) = "white" {}
        _Coverage ("Export matte coverage", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float4 local : TEXCOORD1; };
            sampler2D _MainTex, _Coverage;
            float4 _Color, _ClipRect, _MainTex_TexelSize;
            float4 covered(float2 uv)
            {
                float4 c = tex2Dlod(_MainTex, float4(uv, 0, 0));
                c.a *= tex2Dlod(_Coverage, float4(uv, 0, 0)).r;
                c.rgb *= c.a;
                return c;
            }
            v2f vert(appdata i)
            {
                v2f o; o.local = i.vertex; o.vertex = UnityObjectToClipPos(i.vertex);
                o.color = i.color * _Color; o.uv = i.uv; return o;
            }
            float4 frag(v2f i) : SV_Target
            {
                // Interpolate covered colors, not the white pixels outside the prop.
                // Mirrored atlas UVs use the same source-pixel coverage.
                float2 pixel = i.uv * _MainTex_TexelSize.zw - 0.5;
                float2 f = frac(pixel);
                float2 uv = (floor(pixel) + 0.5) * _MainTex_TexelSize.xy;
                float4 a = lerp(covered(uv), covered(uv + float2(_MainTex_TexelSize.x, 0)), f.x);
                float4 b = lerp(covered(uv + float2(0, _MainTex_TexelSize.y)), covered(uv + _MainTex_TexelSize.xy), f.x);
                float4 c = lerp(a, b, f.y);
                c.rgb /= max(c.a, 0.0001);
                c *= i.color;
                #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(i.local.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(c.a - 0.001);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
