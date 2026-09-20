// 小麦。交差させた札に絵を貼り、α で株の形を抜く。頂点を横へずらして微風になびかせる。
// 株は区切り 1 つにつき 600 あまりを 1 枚の mesh に焼いてあるので、Transform では動かせない。
// 式と数は WheatWind と揃えてある。片方だけ直すと見直しの見立てが狂う
Shader "HalfAware/Wheat"
{
    Properties
    {
        // 株の明暗と形。色は持たない。**α が株の輪郭そのもの。**
        // 絵が無いときのために既定を灰にしてある。白だと 2 倍して倍の明るさになる
        _BaseMap ("株の絵", 2D) = "grey" {}
        // α をここで切る。混ぜずに抜くのは、この密度で並べると混ぜたものは
        // 前後の順が狂ううえ、塗る画素の数がそのまま増えるため。
        // 低すぎると抜いた縁に半端な画素が残って穂が太り、高すぎると芒が消える。
        // 畑の地（_Rooted 0）は α を持たない絵なので 0 にして素通しにする
        _Cutoff ("α を抜く閾値", Range(0, 1)) = 0.34
        _BaseColor ("根元の色", Color) = (0.60, 0.46, 0.17, 1)
        _TipColor ("穂先の色", Color) = (0.86, 0.72, 0.34, 1)
        // 札で作った株なので、1 群につき面の向きが二つしか無い。素の Lambert だと
        // 日射しに背を向けた札が真っ黒に落ちて、畑が市松模様に見える
        _Wrap ("光の回り込み", Range(0, 1)) = 0.6
        // 穂が朝日を透かす。麦の穂は薄く、裏から照らされても光る。
        // 札は裏も表も同じ絵なので、逆光になる面が必ず半分ある。
        // これが無いと、通り過ぎるあいだ畑の半分が沈んで見える
        _Glow ("穂が透かす光", Range(0, 1)) = 0.32
        // 札の法線は上へ倒してある（Bank.CardLift）ので、どの札も上を向く成分を持つ。
        // 低い日射しは立った穂の横面に当たり、穂群の天は畑で一番明るい。
        // これが無いと、上を向く成分のぶんが空の青い環境光ばかりを拾って畑が灰に転ぶ
        _Canopy ("上を向いた面が受ける光", Range(0, 1)) = 0.35
        // 1 なら株、0 なら畑の地。地は uv1 を持たないので、絵の引き方と揺れを切り替える
        _Rooted ("株として塗るか", Float) = 1
        // 地のときの穂先寄り。色の混ぜ具合をここで固定する
        _GroundHigh ("地のときの穂先寄り", Range(0, 1)) = 0.75
        _SwayAmp ("穂先の振れ幅。m", Range(0, 0.5)) = 0.1
        _SwayFlutter ("細かい震えの割合", Range(0, 1)) = 0.32
        _SwayAcross ("x 方向の波数。rad/m", Float) = 0.44
        _SwayAlong ("z 方向の波数。rad/m", Float) = 0.62831853
        _SwayRate ("波の進む速さ。rad/s", Float) = 2.2
        _SwayFlutterRate ("細かい震えの速さ。rad/s", Float) = 5.2
        _SwayHigh ("重みが 1 に届く高さ。m", Float) = 1.5
        _SwaySide ("z 方向の振れの割合", Range(0, 1)) = 0.4
        // 風のむら。長い波で振れ幅そのものを撫でて、塊でなびかせる
        _GustDeep ("風のむらの深さ", Range(0, 1)) = 0.35
        _GustAcross ("むらの x 方向の波数。rad/m", Float) = 0.11
        _GustAlong ("むらの z 方向の波数。rad/m", Float) = 0.31415927
        _GustRate ("むらの進む速さ。rad/s", Float) = 0.85
        // むらが明るさを動かす深さ。麦が倒れると日の当たる面の向きが変わり、
        // 畑の上を明暗の帯が渡る。頂点を動かすだけでは遠い畑に風が出ない
        _ShadeDeep ("むらが明るさを動かす深さ", Range(0, 0.5)) = 0.12
        // 時刻のずらし。秒。ふだんは 0。
        // 再生せずに別の瞬間の絵を撮るときだけ動かす。位相ではなく秒で持つのは、
        // 波と震えで進む速さが違い、ひとつの位相では両方を同じ瞬間へ運べないため
        _SwayShift ("時刻のずらし。秒", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _TipColor;
            half _Cutoff;
            half _Wrap;
            half _Glow;
            half _Canopy;
            float _Rooted;
            half _GroundHigh;
            float _SwayAmp;
            float _SwayFlutter;
            float _SwayAcross;
            float _SwayAlong;
            float _SwayRate;
            float _SwayFlutterRate;
            float _SwayHigh;
            float _SwaySide;
            float _SwayShift;
            float _GustDeep;
            float _GustAcross;
            float _GustAlong;
            float _GustRate;
            half _ShadeDeep;
        CBUFFER_END

        // 根からの高さの重み。根は動かさない。
        // 二乗するのは、真っ直ぐ倒れるのではなく穂先ほど大きく撓ませるため
        float SwayWeight(float up)
        {
            float w = saturate(up / max(_SwayHigh, 1e-4));
            return w * w;
        }

        // 位相は区切りの中の座標から取る。世界の座標から取ると、区切りが手前へ流れる
        // ぶんだけ位相が動いて、なびくのではなく細かく震えて見える。
        //
        // 高さは頂点の y ではなく uv1 に持たせた「根からの高さ」で測る。
        // 畑は起伏を持つので、丘の上の株は y が丸ごと持ち上がる。y で測ると
        // 株ぜんたいが穂先の重みになり、撓むかわりに横へ滑る
        // 風のむら。-1〜1。長い波で、畑を塊ごとに撫でていく
        float Gusting(float3 p)
        {
            float t = _Time.y + _SwayShift;
            return sin(p.x * _GustAcross + p.z * _GustAlong + t * _GustRate);
        }

        float3 Sway(float3 p, float up)
        {
            float phase = p.x * _SwayAcross + p.z * _SwayAlong;
            float t = _Time.y + _SwayShift;
            float slow = phase + t * _SwayRate;
            float quick = phase * 2.0 + t * _SwayFlutterRate;
            float w = SwayWeight(up);
            // むらは振れ幅そのものに掛ける。別の揺れとして足すと、
            // 塊でなびくのではなく二つの波が重なって細かく震えて見える
            float amp = _SwayAmp * (1.0 + _GustDeep * Gusting(p));
            p.x += (sin(slow) + sin(quick) * _SwayFlutter) * amp * w;
            p.z += cos(slow) * amp * _SwaySide * w;
            return p;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            // 札は 1 枚の面しか持たない。裏を落とすと、交差させた札が向きによって
            // 半分消えて、畑に穴が空いたように見える
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                // uv1。x が根からの高さ（m）、y が株の背に対する割合
                float2 root : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float high : TEXCOORD1;
                float fogCoord : TEXCOORD2;
                float2 uv : TEXCOORD3;
                float gust : TEXCOORD4;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                // 地は uv1 を持たない。揺れも色も絵も、株と地で引き方が違う
                float up = v.root.x * _Rooted;
                float3 p = Sway(v.positionOS.xyz, up);
                float3 world = TransformObjectToWorld(p);
                o.positionCS = TransformWorldToHClip(world);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                // 色の混ぜ具合は撓みの重みと分ける。二乗すると穂先の色が先だけに寄る。
                // 割合は株ごとに正規化してあるので、背の低い株でも穂先は穂先の色になる
                o.high = lerp(_GroundHigh, v.root.y, _Rooted);
                // uv は mesh が持つ。札は縦が必ず 0→1（Bank.Card）で、絵の下端が根、
                // 上端が穂先に来る。地は升目に振った uv をそのまま使う
                o.uv = v.uv;
                o.gust = Gusting(v.positionOS.xyz);
                o.fogCoord = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                // **株の形はここで決まる。** 箱の輪郭ではなく絵の α が穂先の凸凹を持つ
                clip(tex.a - _Cutoff);
                half3 n = normalize(i.normalWS);
                Light sun = GetMainLight();
                half ndl = dot(n, sun.direction);
                half wrapped = saturate((ndl + _Wrap) / (1.0 + _Wrap));
                // 絵の rgb は明暗だけ。平均が 0.5 になるよう描いてあるので、2 倍して色に掛ける
                half3 detail = tex.rgb * 2.0;
                half3 albedo = lerp(_BaseColor.rgb, _TipColor.rgb, i.high) * detail;
                // 透かしは日射しの当たっていない側だけに足す。全面に足すと、
                // 当たっている面が振り切れて白く飛ぶ
                // 上を向いた面は穂群の天。立った穂が低い日射しを受ける
                half canopy = saturate(n.y) * _Canopy;
                half3 col = albedo * (SampleSH(n)
                    + sun.color * (wrapped + canopy + _Glow * i.high * (1.0 - wrapped)));
                // 風が渡ったところは穂の向きが変わって明暗が動く。
                // 揺れない地（_Rooted 0）にもこれは掛ける。遠い畑に風を出しているのはこちら
                col *= 1.0 + _ShadeDeep * i.gust;
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
            Cull Off

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct DepthIn
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 root : TEXCOORD1;
            };

            struct DepthOut
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            DepthOut DepthVert(DepthIn v)
            {
                DepthOut o;
                o.positionCS = TransformObjectToHClip(Sway(v.positionOS.xyz, v.root.x * _Rooted));
                o.uv = v.uv;
                return o;
            }

            // 深度にも同じ閾値で抜く。抜かないと、札の透けたところが
            // 深度だけ書き込んで、その後ろの株が消える
            half DepthFrag(DepthOut i) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
