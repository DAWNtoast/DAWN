Shader "Shattered Sail/UI/Crew Portrait Chroma Key"
{
    Properties
    {
        [PerRendererData] _MainTex ("Artwork", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _KeyColorSrgb ("Blue Key (Encoded sRGB)", Vector) = (0,0.5019608,1,1)
        _KeyTolerance ("Background Tolerance", Range(0,0.25)) = 0.035
        _ForegroundTolerance ("Foreground Blue Tolerance", Range(0,0.2)) = 0.05
        _Despill ("Edge Despill", Range(0,1)) = 1
        _UvRect ("Artwork UV Rectangle", Vector) = (0,0,1,1)
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
            sampler2D _MainTex;
            float4 _Color, _ClipRect, _TextureSampleAdd, _MainTex_TexelSize;
            float4 _KeyColorSrgb, _UvRect;
            float _KeyTolerance, _ForegroundTolerance, _Despill;

            v2f vert(appdata input)
            {
                v2f output = (v2f)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.localPosition = input.vertex;
                output.color = input.color * _Color;
                output.uv = input.uv;
                return output;
            }

            float4 frag(v2f input) : SV_Target
            {
                float2 first = _UvRect.xy;
                float2 last = first + _UvRect.zw;
                float2 inset = abs(_MainTex_TexelSize.xy) * 0.5;
                float2 uv = clamp(input.uv, min(first, last) + inset, max(first, last) - inset);
                float4 source = tex2D(_MainTex, uv) + _TextureSampleAdd;
                float3 encoded = source.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                encoded = LinearToGammaSpace(encoded);
                #endif

                // Estimate the blue screen's contribution in encoded sRGB.
                // Neutral silver/white and warm skin/metal have no blue excess,
                // so they retain full coverage, regardless of brightness.
                float blueExcess = max(0.0, encoded.b - max(encoded.r, encoded.g));
                float keyExcess = max(0.001, _KeyColorSrgb.b - max(_KeyColorSrgb.r, _KeyColorSrgb.g));
                // A small dead zone also protects cool highlights in silver
                // hair and metal, instead of making slight blue tints translucent.
                float screenExcess = max(0.0, blueExcess - _ForegroundTolerance);
                float coverage = saturate(1.0 - screenExcess / max(0.001, keyExcess - _ForegroundTolerance));
                coverage = saturate((coverage - _KeyTolerance) / max(0.001, 1.0 - _KeyTolerance));

                // Subtract the estimated screen contribution before recovering
                // straight foreground RGB. This removes blue edge contamination
                // from JPEG antialiasing and bilinear texture sampling.
                float3 foreground = saturate((encoded - _KeyColorSrgb.rgb * (1.0 - coverage)) /
                    max(coverage, 0.0001));
                // Neutralize residual blue only on keyed, partially covered
                // edges. Opaque silver highlights and warm foreground colors
                // stay unchanged; this never erodes alpha or the silhouette.
                float edgeSpill = smoothstep(0.001, 0.08, 1.0 - coverage);
                float neutralBlue = min(foreground.b, (foreground.r + foreground.g) * 0.5);
                foreground.b = lerp(foreground.b, neutralBlue, edgeSpill);
                encoded = lerp(encoded, foreground, saturate(_Despill));
                #ifndef UNITY_COLORSPACE_GAMMA
                encoded = GammaToLinearSpace(encoded);
                #endif
                float4 result = float4(encoded, source.a * coverage) * input.color;
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
