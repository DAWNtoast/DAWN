Shader "Shattered Sail/UI/Tavern Portrait Paper Blend"
{
    Properties
    {
        [PerRendererData] _MainTex ("Original Portrait Atlas", 2D) = "white" {}
        _PaperMask ("Connected Paper Coverage (Linear)", 2D) = "white" {}
        _MaskUv ("Atlas UV To Cell UV", Vector) = (1,1,0,0)
        _BottomFeather ("Bottom Feather Fraction", Range(0,0.05)) = 0.035
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
        Tags
        {
            "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"
            "PreviewType"="Plane" "CanUseSpriteAtlas"="False"
        }
        Stencil
        {
            Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp]
            ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask]
        }
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

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 localPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            sampler2D _MainTex, _PaperMask;
            float4 _Color, _ClipRect, _TextureSampleAdd, _MaskUv;
            float4 _MainTex_TexelSize, _PaperMask_TexelSize;
            float _BottomFeather;

            v2f vert(appdata input)
            {
                v2f output = (v2f)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.localPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color * _Color;
                output.uv = input.uv;
                return output;
            }

            float2 ClampPortraitUv(float2 atlasUv)
            {
                float2 first = -_MaskUv.zw / _MaskUv.xy;
                float2 last = (1.0 - _MaskUv.zw) / _MaskUv.xy;
                float2 inset = abs(_MainTex_TexelSize.xy) * 0.5;
                return clamp(atlasUv, min(first, last) + inset, max(first, last) - inset);
            }

            float4 CoveredPortraitTap(float2 cellUv, float coverage)
            {
                float2 atlasUv = ClampPortraitUv((cellUv - _MaskUv.zw) / _MaskUv.xy);
                float4 color = tex2Dlod(_MainTex, float4(atlasUv, 0.0, 0.0)) + _TextureSampleAdd;
                color.a *= coverage;
                color.rgb *= color.a;
                return color;
            }

            float4 SampleCoveredPortrait(float2 atlasUv, float2 cellUv)
            {
                // Take implicit-derivative sampling outside coverage-dependent
                // control flow. D3D compilers cannot derive every lane after an
                // early return, which produced an uninitialized-value warning.
                float4 original = tex2D(_MainTex, ClampPortraitUv(atlasUv)) + _TextureSampleAdd;
                float2 maskSize = _PaperMask_TexelSize.zw;
                float2 position = cellUv * maskSize - 0.5;
                float2 fraction = frac(position);
                float2 firstPixel = floor(position);
                float2 firstUv = (clamp(firstPixel, 0.0, maskSize - 1.0) + 0.5) * _PaperMask_TexelSize.xy;
                float2 lastUv = (clamp(firstPixel + 1.0, 0.0, maskSize - 1.0) + 0.5) * _PaperMask_TexelSize.xy;
                float4 coverage = float4(
                    tex2Dlod(_PaperMask, float4(firstUv, 0.0, 0.0)).r,
                    tex2Dlod(_PaperMask, float4(lastUv.x, firstUv.y, 0.0, 0.0)).r,
                    tex2Dlod(_PaperMask, float4(firstUv.x, lastUv.y, 0.0, 0.0)).r,
                    tex2Dlod(_PaperMask, float4(lastUv, 0.0, 0.0)).r);

                // Keep the original atlas sampling and its finer detail inside
                // the portrait, even when the coverage texture is smaller.
                if (min(min(coverage.x, coverage.y), min(coverage.z, coverage.w)) >= 0.99999)
                    return original;

                // Filter color * coverage together. Filtering the paper-backed
                // RGB first would carry removed paper into every soft edge.
                float4 lower = lerp(CoveredPortraitTap(firstUv, coverage.x),
                    CoveredPortraitTap(float2(lastUv.x, firstUv.y), coverage.y), fraction.x);
                float4 upper = lerp(CoveredPortraitTap(float2(firstUv.x, lastUv.y), coverage.z),
                    CoveredPortraitTap(lastUv, coverage.w), fraction.x);
                float4 color = lerp(lower, upper, fraction.y);
                // The existing UI blend state takes straight alpha. Recover
                // foreground RGB only after the covered taps have been mixed.
                color.rgb = color.a > 0.00001 ? color.rgb / color.a : 0.0;
                return color;
            }

            float4 frag(v2f input) : SV_Target
            {
                float2 cellUv = input.uv * _MaskUv.xy + _MaskUv.zw;
                float bottom = _BottomFeather > 0.0 ? smoothstep(0.0, _BottomFeather, cellUv.y) : 1.0;
                float4 result = SampleCoveredPortrait(input.uv, cellUv) * input.color;
                result.a *= bottom;
                #ifdef UNITY_UI_CLIP_RECT
                result.a *= UnityGet2DClipping(input.localPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - 0.001);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
