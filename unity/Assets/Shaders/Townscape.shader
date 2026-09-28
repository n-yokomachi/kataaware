// 自室の窓の外の街並み（BuildRoomView）。灯りを受けず、絵 × 頂点色 × 時刻の沈めで塗り、遠いほど霞に沈める。
//
// **灯りは絵の α で見分ける。** α 0 は抜く所、0.5 はふつうの面、1 は自分で光る所（灯った窓・街灯・電話ボックス）。
// ふつうの面は頂点色（面の向きの陰りと街灯の照り返し）と _Tint（夕暮れ・夜の沈め）を掛けるが、
// 光る所は _GlowTint だけを掛ける。夜に街並みを沈めても、灯った窓は沈まない。
//
// **霧は使わない。** 自室は霧を切ってあり、部屋の中の色を変えずに外だけ霞ませたい。距離は目から測り、
// _HazeNear から _HazeFar へ濃くなって _HazeMax で止まる。霞の色は、日の沈んだ側（_WarmDir）を向くほど _HazeWarm へ寄る。
// 空の絵（BuildRoomView の空）も同じ式で地平の色を塗ってあるので、遠い棟が空の地平へ溶ける。
//
// **足し算にも使う。** 街灯の暈と路面の明かりの溜まりは、同じシェーダーを _Additive 1（Blend One One、ZWrite Off）で描く。
// そのときは絵の α を明るさにし、頂点色を灯りの色にする。霞は明るさを削る向きに効かせる
Shader "HalfAware/Townscape"
{
    Properties
    {
        _BaseMap ("絵", 2D) = "white" {}
        _Tint ("ふつうの面の沈め", Color) = (1, 1, 1, 1)
        _GlowTint ("光る所の掛け", Color) = (1, 1, 1, 1)
        _Haze ("霞の色", Color) = (0.4, 0.33, 0.5, 1)
        _HazeWarm ("日の沈んだ側の霞の色", Color) = (0.7, 0.45, 0.45, 1)
        _WarmDir ("日の沈んだ側の向き（xz）", Vector) = (-0.94, 0, 0.34, 0)
        _WarmWidth ("日の沈んだ側へ寄る絞り", Float) = 2
        _HazeNear ("霞の始まり（m）", Float) = 20
        _HazeFar ("霞の終わり（m）", Float) = 420
        _HazeMax ("霞の濃さの限り", Range(0, 1)) = 0.85
        _HazeCurve ("霞の立ち上がり。1 で距離どおり、小さいほど近くから", Range(0.3, 2)) = 0.8
        _GlowHaze ("光る所が霞に負ける割合", Range(0, 1)) = 0.45
        _Cutoff ("抜く境", Range(-1, 1)) = 0.25
        _Additive ("足し算で描く", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("", Float) = 0
        _ZWrite ("", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _Tint;
            half4 _GlowTint;
            half4 _Haze;
            half4 _HazeWarm;
            float4 _WarmDir;
            float _WarmWidth;
            float _HazeNear;
            float _HazeFar;
            half _HazeMax;
            half _HazeCurve;
            half _GlowHaze;
            half _Cutoff;
            half _Additive;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "Townscape"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.color = v.color;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                clip(t.a - _Cutoff);

                float3 look = i.positionWS - _WorldSpaceCameraPos;
                float far = length(look);
                half k = pow(saturate((far - _HazeNear) / max(_HazeFar - _HazeNear, 1.0)), _HazeCurve) * _HazeMax;
                float2 flat = look.xz / max(length(look.xz), 1e-4);
                half warm = pow(saturate(dot(flat, normalize(_WarmDir.xz))), _WarmWidth);
                half3 haze = lerp(_Haze.rgb, _HazeWarm.rgb, warm);

                // 光る所の割合。α 0.5 がふつうの面、1 が光る所
                half glow = saturate((t.a - 0.75h) * 4.0h);
                half3 lit = t.rgb * i.color.rgb * _Tint.rgb;
                half3 self = t.rgb * _GlowTint.rgb;
                half3 c = lerp(lit, self, glow);
                c = lerp(c, haze, k * lerp(1.0h, _GlowHaze, glow));

                // 足し算の暈。α を明るさに、頂点色を灯りの色にする。霞は明るさを削る
                half3 add = t.rgb * t.a * i.color.rgb * _GlowTint.rgb * (1.0h - k * _GlowHaze);
                c = lerp(c, add, _Additive);
                return half4(c, 1.0h);
            }
            ENDHLSL
        }

        // 深さだけを書く。抜いた所（木の葉の隙間・柵の間）まで塞がないように、同じ境で切る。
        // 足し算の暈のマテリアルでは組み立てがこの pass を切る
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

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
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half Frag(Varyings i) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a;
                clip(a - _Cutoff);
                return i.positionCS.z;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
