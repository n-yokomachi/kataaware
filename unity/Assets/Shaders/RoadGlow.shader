// 路面に落ちる灯り。街灯の照り返し・前照灯の照らし・ネオンの映り込みがこれで乗る。
//
// **WebGL なので本物の灯りは置けない。** 街灯は片側 5 本ずつあり、灯りにすると
// 一度に 10 個が道を照らすことになる。ネオンはさらに多い。BuildAlley と同じく
// 光る面（Unlit）で済ませる構えを取るが、面が自分で光るだけでは道は暗いままで、
// 「街灯の橙が一定の間隔で流れる」にならない。灯りが落ちた跡の方を板で描いて、
// 路面へ加算で重ねる。
//
// URP の Unlit は使えない。あちらは必ず霧を色として混ぜる（MixFog）ので、
// 加算で重ねる板では霧の色がそのまま板ぜんたいに足され、絵の無いところまで
// 明るい四角として浮く。夜の霧の色でも sRGB で 50 ほどあり、道の上に板の形が出た。
// ここでは霧を色として混ぜず、遠いほど灯りが減る方で掛ける。
//
// 距離は画素ごとに世界座標から測る。板は 40 m を越える長さで敷くので、
// 頂点で測って補間すると二乗の霧が直線に潰れ、手前が暗く遠くが明るくなる
Shader "HalfAware/RoadGlow"
{
    Properties
    {
        _BaseMap ("灯りの形", 2D) = "black" {}
        [HDR] _BaseColor ("色と強さ", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass
        {
            Name "RoadGlow"
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half3 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb * _BaseColor.a;
                // 霧の向こうの灯りは減るだけで、霧の色を帯びはしない。
                // unity_FogParams は x が二乗の掛かり、y が一乗の掛かり（どちらも 2 の冪で効く）
                float d = length(i.positionWS - _WorldSpaceCameraPos);
                #if defined(FOG_EXP2)
                    half through = exp2(-(unity_FogParams.x * d) * (unity_FogParams.x * d));
                #elif defined(FOG_EXP)
                    half through = exp2(-unity_FogParams.y * d);
                #else
                    half through = 1.0h;
                #endif
                return half4(c * through, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
