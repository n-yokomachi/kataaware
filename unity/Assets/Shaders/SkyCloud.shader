// 空に浮かべる雲の層。**霧を掛けず、代わりに地平へ向かって薄れさせる。**
//
// 場面 8 の雲は高いところに広く敷くので、浅い角度で見ると数百 m 先になる。
// URP の Unlit は必ず霧を掛けるので、そのまま使うと雲が地平の霧に丸ごと呑まれて、
// 天頂に少し残るだけになる。空は霧の向こう側にあるものなので、ここでは距離を見ない。
//
// そのかわり板の縁が問題になる。板は有限なので、浅い角度で見れば必ず縁が来て、
// 空を横切る直線として見える。見る向きの仰角で薄れさせて、縁に届く前に消し切る。
// _HazeLow は板の縁の仰角（高さ / 半幅）より上に取ること。
// 雲が地平の靄に沈んで見えなくなるのは、実際にもそう見えるので都合がよい。
//
// 絵をずらすのは CloudDrift。_BaseMap の offset を動かすので、名前を URP に揃えてある
Shader "HalfAware/SkyCloud"
{
    Properties
    {
        _BaseMap ("雲の絵", 2D) = "white" {}
        _BaseColor ("色と濃さ", Color) = (1, 1, 1, 1)
        _HazeLow ("ここより低い仰角では消える。sin", Range(0, 1)) = 0.09
        _HazeHigh ("ここより高い仰角で濃さのまま。sin", Range(0, 1)) = 0.26
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-50" }

        Pass
        {
            Name "SkyCloud"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _HazeLow;
                half _HazeHigh;
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
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                float3 look = i.positionWS - _WorldSpaceCameraPos;
                // 見る向きの仰角の sin。板は目より上にあるので、これは 0〜1
                half up = saturate(look.y / max(length(look), 1e-4));
                c.a *= saturate((up - _HazeLow) / max(_HazeHigh - _HazeLow, 1e-4));
                return c;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
