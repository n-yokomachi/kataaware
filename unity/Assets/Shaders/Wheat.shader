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
        // ---- 遠さで均す ----------------------------------------------------
        //
        // **畑が汚く見えていた原因はここに集まっている。** 描画解像度は 427 × 240 しか
        // 無いので、20 m より先では穂 1 本が 1 画素を割る。そこへ
        //   ・α を 0.34 で断ち切った輪郭（隙間は地が透ける）
        //   ・平均 0.5 の絵を 2 倍した明暗（0.2 倍から 2.0 倍まで振れる）
        // の二つが重なると、隣り合う画素が「白く飛んだ穂」と「暗い地」を行き来して、
        // 畑ではなく胡麻塩の砂嵐になる。走れば画素ごとに白黒が入れ替わるので、
        // 止めた絵よりさらに汚い。
        //
        // **穂の数を減らしても直らない。** 減らせば隙間が増えるだけで、振れ幅は同じ。
        // 遠いところほど「一本ずつ」を諦めて、畑という一つの面へ均すのが要る。
        // 実際の麦畑も、遠くは穂が見分けられない黄金色の面に見える。
        //
        // **ここから下の 7 つは組み立てが書き込まない。** BuildDrive.Crop が並べているのは
        // 色と揺れの数だけで、この帯は既定値のまま使う。手で触ると組み直しても戻らないので、
        // 詰め直すときは既定値そのものを直すこと。
        //
        // 0.62 / 0.45 / 0.34 を撮って隣り合う画素の差を測った。手前の畑で
        // 0.0572 / 0.0521 / 0.0489。下げるほど静かになるが、下げ切ると穂の一本ずつが
        // 消えて畑が平らな布になる。0.38 は、株の見分けが残る一番低いところ
        _DetailDeep ("絵の明暗の効き", Range(0, 2)) = 0.38
        _DetailFade ("遠くで明暗を均す割合", Range(0, 1)) = 0.86
        // 遠くで株の隙間を埋める量。α へ足してから閾値で切る。
        // **閾値（_Cutoff）より小さく取ること。** 越えると札が丸ごと通って、
        // 遠景に黄金色の長方形が並ぶ
        _FarFill ("遠くで株の隙間を埋める量", Range(0, 1)) = 0.22
        _FarFrom ("均しはじめる距離。m", Float) = 6
        _FarTo ("均し切る距離。m", Float) = 45
        // 遠くで地と株の明るさを寄せる。**畑の粒立ちはここがいちばん効く。**
        //
        // 畑の地は株の陰になる面として、株の半分ほどの明るさで塗ってある
        // （BuildDrive.FieldMat）。手前ではそれが株の隙間の日陰になって正しいが、
        // 遠くでは株ひと群が 1 画素を割るので、隣り合う画素が「株」と「地」を
        // 行き来して、畑ぜんたいが胡麻塩に見える。実際に測ると赤の明るさで
        // 株 1.43 に対して地 0.67 と倍以上あった。
        //
        // 遠さに連れて地を持ち上げ、株をわずかに落として、一つの面へ寄せる。
        // 株と地は同じシェーダーで塗るので、どちらかは _Rooted で見分ける
        _FarGroundGain ("遠くの地の持ち上げ", Range(1, 3)) = 2.15
        _FarCropGain ("遠くの株の落とし", Range(0.5, 1)) = 0.92
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
        _SwayAmp ("穂先の振れ幅。m", Range(0, 0.5)) = 0.17
        _SwayFlutter ("細かい震えの割合", Range(0, 1)) = 0.32
        _SwayAcross ("x 方向の波数。rad/m", Float) = 0.44
        _SwayAlong ("z 方向の波数。rad/m", Float) = 0.62831853
        _SwayRate ("波の進む速さ。rad/s", Float) = 2.2
        _SwayFlutterRate ("細かい震えの速さ。rad/s", Float) = 5.2
        _SwayHigh ("重みが 1 に届く高さ。m", Float) = 1.5
        _SwaySide ("z 方向の振れの割合", Range(0, 1)) = 0.4
        // 風のむら。長い波で振れ幅そのものを撫でて、塊でなびかせる
        _GustDeep ("風のむらの深さ", Range(0, 1)) = 0.45
        _GustAcross ("むらの x 方向の波数。rad/m", Float) = 0.11
        _GustAlong ("むらの z 方向の波数。rad/m", Float) = 0.31415927
        _GustRate ("むらの進む速さ。rad/s", Float) = 0.85
        // むらの内訳。斜めの短い波と、道と平行な長い波。**足して 1 にすること。**
        // 越えると穂先が WheatWind.Reach より遠くへ振れ、見直しの見立てが狂う
        _GustSlant ("斜めの波の取り分", Range(0, 1)) = 0.60
        _GustRoll ("道と平行な長い波の取り分", Range(0, 1)) = 0.40
        _RollAcross ("長い波の波数。_GustAcross に対する割合", Float) = 0.80
        _RollRate ("長い波の速さ。_GustRate に対する割合", Float) = -0.45
        // むらが明るさを動かす深さ。麦が倒れると日の当たる面の向きが変わり、
        // 畑の上を明暗の帯が渡る。頂点を動かすだけでは遠い畑に風が出ない
        _ShadeDeep ("むらが明るさを動かす深さ", Range(0, 0.5)) = 0.22
        // 朝靄。**距離の霧とは別に要る。** RenderSettings の霧は距離だけを見るので、
        // どれだけ濃くしても空気が一様に霞むだけになる。原作の「朝靄の中で」は
        // そうではなく、靄が地面に溜まって、穂先だけがそこから抜けて立っている。
        // 畑が窪みで白く沈み、丘の背が靄の上に出る。この層がそれを出す
        _MistColor ("靄の色", Color) = (0.82, 0.80, 0.75, 1)
        _MistDeep ("靄の溜まりの濃さ。1/m", Float) = 0.0105
        _MistTop ("靄が消える高さ。m", Float) = 3.4
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
            half _DetailDeep;
            half _DetailFade;
            half _FarFill;
            float _FarFrom;
            float _FarTo;
            half _FarGroundGain;
            half _FarCropGain;
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
            float _GustSlant;
            float _GustRoll;
            float _RollAcross;
            float _RollRate;
            half _ShadeDeep;
            half4 _MistColor;
            float _MistDeep;
            float _MistTop;
        CBUFFER_END

        // 地面に溜まる朝靄。0〜1。世界の高さと目からの距離で決まる。
        //
        // 高さの落とし方を二乗ではなく両端を寝かせた形にしてあるのは、層の上端に
        // 水平の切れ目を出さないため。畑は丘を持つので、切れ目が出ると
        // 靄の面が丘を横切る一本の線になって、雲海に浮かぶ島に見える
        float Misting(float3 world, float eye)
        {
            float lay = saturate(1.0 - world.y / max(_MistTop, 1e-3));
            lay = lay * lay * (3.0 - 2.0 * lay);
            // **距離は二乗で効かせる。** 一乗だと 20 m 先の株にもう 2 割の白が乗り、
            // 手前の畑から黄金色が抜けて生成りの布になる。原作は靄と黄金色を
            // 同時に書いているので、どちらも捨てられない。二乗にすると手前は
            // ほとんど素通しのまま、40 m あたりから急に白が溜まりはじめる
            float t = _MistDeep * eye;
            float far = 1.0 - exp(-t * t);
            return saturate(lay * far);
        }

        // 目からの遠さ。0〜1。穂 1 本が 1 画素を割るところから均しはじめる。
        // 端を寝かせるのは、均しの始まる距離に輪の形の継ぎ目を出さないため
        float Farness(float eye)
        {
            float f = saturate((eye - _FarFrom) / max(_FarTo - _FarFrom, 1e-3));
            return f * f * (3.0 - 2.0 * f);
        }

        // 遠くで α へ足す量。**根元だけ埋めて穂先は埋めない。**
        // 一様に足すと札の上端まで通って、遠景の稜線が長方形の縁になる。
        // 根元（v 0）から v 0.8 のあたりまでで 0 へ落とすと、
        // 畑は詰まったまま、空との境だけが穂先の凸凹で毛羽立つ
        float Filling(float far, float v)
        {
            return _FarFill * far * saturate(1.0 - v * 1.25);
        }

        // 根からの高さの重み。根は動かさない。
        // 二乗するのは、真っ直ぐ倒れるのではなく穂先ほど大きく撓ませるため
        float SwayWeight(float up)
        {
            float w = saturate(up / max(_SwayHigh, 1e-4));
            return w * w;
        }

        // 風のむら。-1〜1。長い波で、畑を塊ごとに撫でていく。
        //
        // **二つの波を足す。** 一つめは斜めの短い波（およそ 19 m）。z 方向の波長は
        // 区切りの長さ 20 m を割り切らないと継ぎ目で途切れるので、これ以上は伸ばせない。
        // 二つめは z を持たない長い波（およそ 71 m）で、道と平行な帯が畑を横切って
        // 外へ渡っていく。**風が渡って見えるのはこちら。** z を持たないので継ぎ目に出ない。
        // 取り分は足して 1。越えると穂先が WheatWind.Reach より遠くへ振れ、
        // 見直しが轍への倒れ込みを見逃す。数は WheatWind と揃えてある
        float Gusting(float3 p)
        {
            float t = _Time.y + _SwayShift;
            return _GustSlant * sin(p.x * _GustAcross + p.z * _GustAlong + t * _GustRate)
                + _GustRoll * sin(p.x * _GustAcross * _RollAcross + t * _GustRate * _RollRate);
        }

        // 位相は区切りの中の座標から取る。世界の座標から取ると、区切りが手前へ流れる
        // ぶんだけ位相が動いて、なびくのではなく細かく震えて見える。
        //
        // 高さは頂点の y ではなく uv1 に持たせた「根からの高さ」で測る。
        // 畑は起伏を持つので、丘の上の株は y が丸ごと持ち上がる。y で測ると
        // 株ぜんたいが穂先の重みになり、撓むかわりに横へ滑る
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
                float mist : TEXCOORD5;
                float far : TEXCOORD6;
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
                // 靄は頂点で引く。札の下辺と上辺で値が違うので、そのまま補間すれば
                // 1 枚の札の中で根元が白く沈んで穂先が抜けて立つ
                float eye = distance(world, _WorldSpaceCameraPos);
                o.mist = Misting(world, eye);
                o.far = Farness(eye);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                // **株の形はここで決まる。** 箱の輪郭ではなく絵の α が穂先の凸凹を持つ。
                // 遠いところは根元の隙間を埋めて、株の集まりではなく一つの面にする
                clip(tex.a + Filling(i.far, i.uv.y) - _Cutoff);
                half3 n = normalize(i.normalWS);
                Light sun = GetMainLight();
                half ndl = dot(n, sun.direction);
                half wrapped = saturate((ndl + _Wrap) / (1.0 + _Wrap));
                // 絵の rgb は明暗だけ。平均が 0.5 になるよう描いてあるので、2 倍して色に掛ける。
                //
                // **そのまま掛けると 0.2 倍から 2.0 倍まで振れる。** 穂 1 本が 1 画素を
                // 割る距離では、隣り合う画素が白飛びと暗がりを行き来して砂嵐になる。
                // 手前は _DetailDeep で振れ幅を詰め、遠くは _DetailFade で 1 倍へ均す。
                // 遠くの畑は穂の一本ずつではなく、黄金色の面として見えているのが正しい
                half k = _DetailDeep * (1.0 - _DetailFade * i.far);
                half3 detail = 1.0 + (tex.rgb * 2.0 - 1.0) * k;
                half3 albedo = lerp(_BaseColor.rgb, _TipColor.rgb, i.high) * detail;
                // 透かしは日射しの当たっていない側だけに足す。全面に足すと、
                // 当たっている面が振り切れて白く飛ぶ
                // 上を向いた面は穂群の天。立った穂が低い日射しを受ける
                half canopy = saturate(n.y) * _Canopy;
                half3 col = albedo * (SampleSH(n)
                    + sun.color * (wrapped + canopy + _Glow * i.high * (1.0 - wrapped)));
                // 遠くで地と株の明るさを寄せる。**風の帯より先に掛ける。**
                // 後に回すと、寄せる掛け算が風の明暗まで均してしまう
                col *= lerp(1.0, lerp(_FarGroundGain, _FarCropGain, _Rooted), i.far);
                // 風が渡ったところは穂の向きが変わって明暗が動く。
                // 揺れない地（_Rooted 0）にもこれは掛ける。遠い畑に風を出しているのはこちら
                col *= 1.0 + _ShadeDeep * i.gust;
                col = MixFog(col, i.fogCoord);
                // 距離の霧の上へ、地面に溜まったぶんを重ねる。
                // 色は霧と同じにしてあるので（DriveSky.haze）、二つの掛かり方が
                // 一つの空気として見える。離すと畑の途中に色の変わる線が出る
                col = lerp(col, _MistColor.rgb, i.mist);
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
                float far : TEXCOORD1;
            };

            DepthOut DepthVert(DepthIn v)
            {
                DepthOut o;
                float3 p = Sway(v.positionOS.xyz, v.root.x * _Rooted);
                float3 world = TransformObjectToWorld(p);
                o.positionCS = TransformWorldToHClip(world);
                o.uv = v.uv;
                // **遠さの均しはこちらにも要る。** 色の面だけ埋めて深度を埋めないと、
                // 埋めたところが深度を持たず、深度を使う絵で麦だけ抜け落ちる
                o.far = Farness(distance(world, _WorldSpaceCameraPos));
                return o;
            }

            // 深度にも同じ閾値で抜く。抜かないと、札の透けたところが
            // 深度だけ書き込んで、その後ろの株が消える
            half DepthFrag(DepthOut i) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a;
                clip(a + Filling(i.far, i.uv.y) - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
