// 小麦。頂点を横へずらして微風になびかせる。
// 株は区切り 1 つにつき 300 あまりを 1 枚の mesh に焼いてあるので、Transform では動かせない。
// 式と数は WheatWind と揃えてある。片方だけ直すと見直しの見立てが狂う
Shader "HalfAware/Wheat"
{
    Properties
    {
        _BaseColor ("根元の色", Color) = (0.60, 0.46, 0.17, 1)
        _TipColor ("穂先の色", Color) = (0.86, 0.72, 0.34, 1)
        // 箱で作った株なので、面の向きが四方向しか無い。素の Lambert だと
        // 日射しに背を向けた面が真っ黒に落ちて、畑が市松模様に見える
        _Wrap ("光の回り込み", Range(0, 1)) = 0.6
        // 穂が朝日を透かす。低い日射しは株の天面にはほとんど当たらないので、
        // これが無いと畑の上面だけが暗く沈んで、箱を並べただけに見える
        _Glow ("穂が透かす光", Range(0, 1)) = 0.32
        _SwayAmp ("穂先の振れ幅。m", Range(0, 0.5)) = 0.1
        _SwayFlutter ("細かい震えの割合", Range(0, 1)) = 0.32
        _SwayAcross ("x 方向の波数。rad/m", Float) = 0.44
        _SwayAlong ("z 方向の波数。rad/m", Float) = 0.62831853
        _SwayRate ("波の進む速さ。rad/s", Float) = 2.2
        _SwayFlutterRate ("細かい震えの速さ。rad/s", Float) = 5.2
        _SwayHigh ("重みが 1 に届く高さ。m", Float) = 1.5
        _SwaySide ("z 方向の振れの割合", Range(0, 1)) = 0.4
        // 時刻のずらし。秒。ふだんは 0。
        // 再生せずに別の瞬間の絵を撮るときだけ動かす。位相ではなく秒で持つのは、
        // 波と震えで進む速さが違い、ひとつの位相では両方を同じ瞬間へ運べないため
        _SwayShift ("時刻のずらし。秒", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _TipColor;
            half _Wrap;
            half _Glow;
            float _SwayAmp;
            float _SwayFlutter;
            float _SwayAcross;
            float _SwayAlong;
            float _SwayRate;
            float _SwayFlutterRate;
            float _SwayHigh;
            float _SwaySide;
            float _SwayShift;
        CBUFFER_END

        // 根からの高さの重み。根は動かさない。
        // 二乗するのは、真っ直ぐ倒れるのではなく穂先ほど大きく撓ませるため
        float SwayWeight(float y)
        {
            float w = saturate(y / max(_SwayHigh, 1e-4));
            return w * w;
        }

        // 位相は区切りの中の座標から取る。世界の座標から取ると、区切りが手前へ流れる
        // ぶんだけ位相が動いて、なびくのではなく細かく震えて見える
        float3 Sway(float3 p)
        {
            float phase = p.x * _SwayAcross + p.z * _SwayAlong;
            float t = _Time.y + _SwayShift;
            float slow = phase + t * _SwayRate;
            float quick = phase * 2.0 + t * _SwayFlutterRate;
            float w = SwayWeight(p.y);
            p.x += (sin(slow) + sin(quick) * _SwayFlutter) * _SwayAmp * w;
            p.z += cos(slow) * _SwayAmp * _SwaySide * w;
            return p;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float high : TEXCOORD1;
                float fogCoord : TEXCOORD2;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 p = Sway(v.positionOS.xyz);
                float3 world = TransformObjectToWorld(p);
                o.positionCS = TransformWorldToHClip(world);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                // 色の混ぜ具合は撓みの重みと分ける。二乗すると穂先の色が先だけに寄る
                o.high = saturate(v.positionOS.y / max(_SwayHigh, 1e-4));
                o.fogCoord = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half3 n = normalize(i.normalWS);
                Light sun = GetMainLight();
                half ndl = dot(n, sun.direction);
                half wrapped = saturate((ndl + _Wrap) / (1.0 + _Wrap));
                half3 albedo = lerp(_BaseColor.rgb, _TipColor.rgb, i.high);
                // 透かしは日射しの当たっていない側だけに足す。全面に足すと、
                // 当たっている面が振り切れて白く飛ぶ
                half3 col = albedo * (SampleSH(n) + sun.color * (wrapped + _Glow * i.high * (1.0 - wrapped)));
                col = MixFog(col, i.fogCoord);
                return half4(col, 1.0);
            }
            ENDHLSL
        }

        // 深度だけの描き込み。なびいた先を書かないと、深度を使う絵で麦だけ立ったままになる
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            float4 DepthVert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(Sway(positionOS.xyz));
            }

            half DepthFrag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
