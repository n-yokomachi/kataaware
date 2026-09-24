// 机のモニターの黒い画面に映る、主人公の映り込み（TerminalReflection）。
//
// 映り込みのカメラが撮った絵を、画面の面に貼った板へ、明るい所だけを重ねる（加算）。暗い所は画面の黒に溶ける。
// 鏡なので左右を返して貼る。色はほぼ抜く（唇の赤みを残さない）。
//
// **性別を見せない。** 目の映る所（_Eye.xy）のまわりを沈め、口元と顎だけを少し明るくし、
// 胸元の高さ（_Chest.x）から下を消す（_Chest.y で消し終える）。顔の大きさは _Eye.zw（顔の幅の半分と、目から顎までの下がり。uv）。
// 灯りも影も受けない。霧も掛けない（画面の上の映り込みは、画面と同じ近さにある）
Shader "HalfAware/ScreenReflection"
{
    Properties
    {
        _BaseMap ("映り込みのカメラの絵", 2D) = "black" {}
        _BaseColor ("色（α で濃さ）", Color) = (1, 1, 1, 1)
        _Strength ("濃さ", Range(0, 1)) = 0.14
        _Saturation ("色の残し方", Range(0, 1)) = 0.08
        _Eye ("目の映る所（uv）と顔の幅の半分・目から顎まで（uv）", Vector) = (0.5, 0.6, 0.05, 0.12)
        _Chest ("胸元を消し始める高さと消し終える高さ（uv の v）", Vector) = (0.3, 0.2, 0, 0)
        _EyeShade ("目のあたりを沈める強さ", Range(0, 1)) = 0.9
        _Lift ("口元と顎を明るくする強さ", Range(0, 4)) = 1.2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ScreenReflection"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
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
                half _Strength;
                half _Saturation;
                float4 _Eye;
                float4 _Chest;
                half _EyeShade;
                half _Lift;
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
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                // 鏡なので左右を返す
                half3 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, float2(1.0 - i.uv.x, i.uv.y)).rgb;
                half l = dot(c, half3(0.299h, 0.587h, 0.114h));
                c = lerp(l.xxx, c, _Saturation);

                // 顔の大きさで測った、目の映る所からの離れ（右が +x、上が +y。顎の先で y = -1）
                float2 d = (i.uv - _Eye.xy) / max(_Eye.zw, 1e-4);
                // 目のあたり（目の少し上から鼻の上まで）を沈める
                half eye = exp(-(d.x * d.x * 0.45 + (d.y - 0.05) * (d.y - 0.05) * 7.0));
                half shade = 1.0h - _EyeShade * eye;
                // 口元と顎を少し明るくする（映り込みの中で、顔の下半分だけがうっすら照らされる）
                half low = exp(-(d.x * d.x * 0.8 + (d.y + 0.7) * (d.y + 0.7) * 5.0));
                half lift = 1.0h + _Lift * low;
                // 胸元の高さから下を消す
                half chest = smoothstep(_Chest.y, _Chest.x, i.uv.y);

                half3 o = c * shade * lift * chest * _Strength * _BaseColor.rgb * _BaseColor.a;
                return half4(o, 1.0h);
            }
            ENDHLSL
        }
    }
}
