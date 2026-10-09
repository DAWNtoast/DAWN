Shader "Shattered Sail/UI/Continuous Voyage Coast"
{
    Properties
    {
        [PerRendererData] _MainTex ("Coast Field", 2D) = "white" {}
        _LandArt ("Authored Land Detail", 2D) = "white" {}
        _WaterArt ("Authored Water", 2D) = "white" {}
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
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="False" }
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
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex, _LandArt, _WaterArt;
            float4 _Color, _ClipRect, _Grid, _Seed;
            v2f vert(appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world=v.vertex; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color*_Color; return o;
            }
            float hash(float2 p) { p=frac(p*float2(123.34,456.21)); p+=dot(p,p+45.32); return frac(p.x*p.y); }
            float noise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3.-2.*f);
                return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1.),f.x),f.y);
            }
            float fbm(float2 p) { return noise(p)*.57+noise(p*2.07+19.1)*.28+noise(p*4.13+7.7)*.15; }
            float2 mirror(float2 p) { return 1.-abs(frac(p*.5)*2.-1.); }
            fixed4 frag(v2f i):SV_Target
            {
                float2 uv=i.uv;
                float2 fieldUV=uv*(1.-_Grid.zw)+.5*_Grid.zw;
                float4 field=tex2D(_MainTex,fieldUV);
                float2 p=uv*_Grid.xy, q=p+_Seed.xy;
                float micro=fbm(q*11.7);
                float d=(field.r-.5)*4. + (micro-.5)*.050;
                float aa=max(fwidth(d)*.65,.010);
                float land=smoothstep(-aa,aa,d);
                float shelfWidth=.20+.28*fbm(q*1.5);
                float shallows=(1.-smoothstep(.03,shelfWidth,max(0.,-d))) * (1.-land);
                float waterGrain=dot(tex2D(_WaterArt,uv).rgb,float3(.25,.5,.25));
                float3 aqua=lerp(float3(.025,.24,.34),float3(.10,.49,.47),smoothstep(-shelfWidth,0.,d));
                aqua*=.80+waterGrain*.65;
                // Continuous authored ground material: no miniature island
                // cutouts, transparent bays or baked ocean inside the land.
                float2 atlas=mirror(q*.115+float2(fbm(q*.45),fbm(q*.45+13.))*.035);
                float3 painted=tex2D(_LandArt,atlas).rgb;
                float grain=fbm(q*23.);
                float sandNoise=fbm(q*9.1);
                float beachType=smoothstep(.37,.64,fbm(q*.93));
                beachType=beachType*beachType*beachType;
                float beachWidth=.006+beachType*(.03+.07*noise(q*2.1));
                float3 sand=lerp(float3(.45,.36,.23),float3(.72,.63,.44),sandNoise);
                float strata=abs(noise(q*4.7)*2.-1.);
                float3 cliff=lerp(float3(.22,.25,.21),float3(.49,.49,.37),grain)*(.84+strata*.25);
                float3 coastColor=lerp(cliff,sand,beachType);
                float2 gradient=float2(tex2D(_MainTex,fieldUV+float2(_Grid.z,0)).r-tex2D(_MainTex,fieldUV-float2(_Grid.z,0)).r,
                    tex2D(_MainTex,fieldUV+float2(0,_Grid.w)).r-tex2D(_MainTex,fieldUV-float2(0,_Grid.w)).r);
                float lighting=clamp(1.+dot(gradient,float2(-5.5,7.5)),.53,1.28);
                float slope=smoothstep(beachWidth,beachWidth+.045,d+(sandNoise-.5)*.045);
                float3 landColor=lerp(coastColor,painted*(.90+grain*.22),slope)*lighting;
                // Surf is sparse foam breaking on points and rock, not an
                // unbroken luminous outline around every island.
                float breaker=smoothstep(.34,.70,fbm(q*5.1));
                float surf=(1.-smoothstep(.008,.039,abs(d+.030)))*breaker;
                float outerSurf=(1.-smoothstep(.005,.022,abs(d+.14+(noise(q*6.)-.5)*.065)))
                    *smoothstep(.52,.72,micro)*.24;
                aqua=lerp(aqua,float3(.67,.82,.75),saturate(surf*.9+outerSurf));
                landColor*=1.-(1.-smoothstep(0.,.03,d))*.22;
                float3 finalColor=lerp(aqua,landColor,land);
                float alpha=max(land,max(shallows*.42,surf*.66));
                // Existing sandbank/reef logical cells tint the continuous ocean
                // beneath their feathered painted details without square fills.
                float banks=smoothstep(.12,.90,field.g)*(.10+.13*noise(q*2.2))*(1.-alpha);
                finalColor=lerp(finalColor,float3(.12,.39,.38)*(.8+waterGrain*.5),banks);
                alpha=max(alpha,banks);
                if (_Seed.z>1.5) finalColor*=float3(.88,.94,1.02);
                else if (_Seed.z>.5) finalColor*=float3(1.03,1.02,.94);
                fixed4 result=fixed4(finalColor,alpha)*i.color;
                #ifdef UNITY_UI_CLIP_RECT
                result.a*=UnityGet2DClipping(i.world.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a-.001);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
