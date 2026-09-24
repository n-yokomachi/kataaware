// 机のモニターの黒い画面に映る、主人公の映り込み（TerminalReflection）。
//
// 映り込みのカメラが撮った絵を、画面の面に貼った板へ、明るい所だけを重ねる（加算）。暗い所は画面の黒に溶ける。
// 鏡に見えるよう左右を返して貼る。色はほぼ抜く（唇の赤みを残さない）。
// 撮った絵は粗いので少しぼかし、明るい所は寝かせる（腕の照り返しが点の列にならず、灯りの照り返しで白く飛ばない）。
// 画面の上の縁（鼻から上）と下の縁（胸から下）のきわは、少しずつ暗く沈める。顎と首のまわり（肩）も沈める。
// 灯りも影も受けない。霧も掛けない（画面の上の映り込みは、画面と同じ近さにある）
Shader "HalfAware/ScreenReflection"
{
    Properties
    {
        _BaseMap ("映り込みのカメラの絵", 2D) = "black" {}
        _BaseColor ("色（α で濃さ）", Color) = (1, 1, 1, 1)
        _Strength ("濃さ", Range(0, 1)) = 0.14
        _Saturation ("色の残し方", Range(0, 1)) = 0.08
        _Edge ("上の縁と下の縁から沈める幅（uv）", Vector) = (0.12, 0.10, 0, 0)
        _Focus ("顎と首を真ん中に、まわりを沈める（真ん中の uv と半径）", Vector) = (0.5, 0.65, 0.7, 0.8)
        _FocusFloor ("まわりの沈め方（いちばん外の明るさ）", Range(0, 1)) = 0.65
        _Compress ("明るい所を寝かせる強さ", Range(0, 8)) = 2.5
        _Texel ("映り込みの絵の 1 px（uv）", Vector) = (0.006, 0.01, 0, 0)
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
                float4 _Edge;
                float4 _Focus;
                half _FocusFloor;
                half _Compress;
                float4 _Texel;
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
                // 鏡なので左右を返す。映り込みの絵は粗いので、2 px の幅でぼかしてから重ねる（撮る絵は貼る大きさの 2 倍ほど）
                // （腕の肌の照り返しが、ところどころの明るい点になって並ばないように）
                float2 uv = float2(1.0 - i.uv.x, i.uv.y);
                float2 e = _Texel.xy * 2.0;
                half3 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb * 0.25h;
                c += SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv + float2(e.x, 0)).rgb * 0.125h;
                c += SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv - float2(e.x, 0)).rgb * 0.125h;
                c += SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv + float2(0, e.y)).rgb * 0.125h;
                c += SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv - float2(0, e.y)).rgb * 0.125h;
                c += SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv + e).rgb * 0.0625h;
                c += SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv - e).rgb * 0.0625h;
                c += SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv + float2(e.x, -e.y)).rgb * 0.0625h;
                c += SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv + float2(-e.x, e.y)).rgb * 0.0625h;
                // 明るい所を寝かせる（部屋の灯りの照り返しで、画面が白く飛ばないように）
                c = c / (1.0h + c * _Compress);
                half l = dot(c, half3(0.299h, 0.587h, 0.114h));
                c = lerp(l.xxx, c, _Saturation);

                // 上の縁（鼻から上の見切れ）と下の縁（胸から下の見切れ）のきわを沈める
                half edge = smoothstep(0.0, _Edge.y, i.uv.y) * (1.0h - smoothstep(1.0 - _Edge.x, 1.0, i.uv.y));

                // 顎と首を主役にし、まわり（左右の画面で手前に大きく入る肩）を沈める
                half focus = saturate(1.0h - length((i.uv - _Focus.xy) / max(_Focus.zw, 1e-3)));
                edge *= lerp(_FocusFloor, 1.0h, smoothstep(0.0h, 1.0h, focus));

                half3 o = c * edge * _Strength * _BaseColor.rgb * _BaseColor.a;
                return half4(o, 1.0h);
            }
            ENDHLSL
        }
    }
}
