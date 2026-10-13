Shader "Shattered Sail/UI/Battle Ship Cutout"
{
    Properties
    {
        [PerRendererData] _MainTex ("Ship Atlas", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _KeyGamma ("Atlas Background In sRGB", Vector) = (0.001960784,0.049019608,0.125490196,0)
        _KeyTolerance ("sRGB Channel Tolerance", Vector) = (0.005882353,0.005882353,0.007843137,1.75)
        _UseColorKey ("Use Legacy Navy Color Key", Float) = 1
        _AtlasGrid ("Cell Count XY / Separator Margin In Texels ZW", Vector) = (0,0,0,0)
        _SmoothCutout ("Alpha-aware Bilinear Filtering", Float) = 0
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
            float4 _Color, _ClipRect, _KeyGamma, _KeyTolerance;
            float4 _TextureSampleAdd;
            float4 _MainTex_TexelSize, _AtlasGrid;
            float _SmoothCutout, _UseColorKey;

            v2f vert(appdata input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.localPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color * _Color;
                output.uv = input.uv;
                return output;
            }

            // Compare the authored bytes, not scene-linear luminance. The IEC
            // low segment matters for an atlas whose red background is 0/255.
            float3 ToGammaExact(float3 linearColor)
            {
                float3 upper = 1.055 * pow(max(linearColor, 0.0), 1.0 / 2.4) - 0.055;
                return lerp(linearColor * 12.92, upper, step(0.0031308, linearColor));
            }
            float3 ToLinearExact(float3 gammaColor)
            {
                float3 upper = pow(max((gammaColor + 0.055) / 1.055, 0.0), 2.4);
                return lerp(gammaColor / 12.92, upper, step(0.04045, gammaColor));
            }

            float CellCoverage(float2 uv)
            {
                if (_AtlasGrid.x > 0.0 && _AtlasGrid.y > 0.0)
                {
                    float2 cellSize = _MainTex_TexelSize.zw / _AtlasGrid.xy;
                    float2 inCell = frac(uv * _AtlasGrid.xy) * cellSize;
                    float2 edge = min(inCell, cellSize - inCell);
                    float2 safe = step(_AtlasGrid.zw, edge);
                    return safe.x * safe.y;
                }
                return 1.0;
            }

            // Key each source texel BEFORE filtering. Filtering opaque navy
            // into a sail/rope first makes the mixed color miss the narrow key
            // and leaves an opaque dark fringe when the image is turned or
            // downscaled. Work in premultiplied color until all four keyed
            // samples are combined; retain the UI's straight-alpha blend mode.
            float4 KeyedTexel(float2 uv)
            {
                float4 sampled = tex2Dlod(_MainTex, float4(uv, 0.0, 0.0)) + _TextureSampleAdd;
                float coverage = 1.0;
                float3 foreground = sampled.rgb;
                if (_UseColorKey > 0.5)
                {
                    float3 encoded = sampled.rgb;
                    float3 keyInWorkingSpace = _KeyGamma.rgb;
                    #ifndef UNITY_COLORSPACE_GAMMA
                    encoded = ToGammaExact(sampled.rgb);
                    keyInWorkingSpace = ToLinearExact(_KeyGamma.rgb);
                    #endif

                    // This narrow three-channel legacy key leaves black sails,
                    // purple cloth, shadowed timber and green wakes opaque.
                    float3 channels = abs(encoded - _KeyGamma.rgb) / max(_KeyTolerance.rgb, 0.00001);
                    float distance = max(channels.r, max(channels.g, channels.b));
                    coverage = smoothstep(1.0, _KeyTolerance.a, distance);

                    // Undo only the old matte contribution at silhouette edges.
                    foreground = max(0.0, (sampled.rgb - keyInWorkingSpace * (1.0 - coverage)) / max(coverage, 0.01));
                }
                // Alpha-authored ships retain every RGB color, including navy.
                float alpha = sampled.a * coverage * CellCoverage(uv);
                return float4(foreground * alpha, alpha);
            }

            float4 frag(v2f input) : SV_Target
            {
                float4 filtered;
                if (_UseColorKey < 0.5)
                {
                    // Authored RGB and alpha use the same native sampling as
                    // the starter ship and a standard UI RawImage.
                    float4 sampled = tex2D(_MainTex, input.uv) + _TextureSampleAdd;
                    float alpha = sampled.a * CellCoverage(input.uv);
                    filtered = float4(sampled.rgb * alpha, alpha);
                }
                else if (_SmoothCutout > 0.5)
                {
                    float2 texel = input.uv * _MainTex_TexelSize.zw - 0.5;
                    float2 origin = (floor(texel) + 0.5) * _MainTex_TexelSize.xy;
                    float2 weight = frac(texel);
                    float4 lower = lerp(KeyedTexel(origin),
                        KeyedTexel(origin + float2(_MainTex_TexelSize.x, 0.0)), weight.x);
                    float4 upper = lerp(KeyedTexel(origin + float2(0.0, _MainTex_TexelSize.y)),
                        KeyedTexel(origin + _MainTex_TexelSize.xy), weight.x);
                    filtered = lerp(lower, upper, weight.y);
                }
                else filtered = KeyedTexel(input.uv);
                float4 result = float4(filtered.rgb / max(filtered.a, 0.00001), filtered.a) * input.color;
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
