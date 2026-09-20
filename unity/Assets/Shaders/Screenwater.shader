// 風防に乗った水。**車の外に降る雨ではなく、ガラスの面に付いた水の方を描く。**
//
// 場面 8 は運転席からの一人称で、走行中の画面はほとんど風防で埋まっている。
// 外に粒を降らせても、目の前を 28 m/s で過ぎる粒は 1 フレームで画面を横切るので
// 427 × 240 では線にすらならず、粒を増やすほど WebGL が重くなるだけだった。
// 雨が降っていると分かるのはガラスの側で、溜まった水と、それを拭う羽根の二つで足りる。
//
// 板は一枚。砂目も筋も薄膜も、画素ごとに乱数から起こす。絵も持たないし、
// 粒ひとつに mesh を立てることもしない。
//
// ---- 何を主役にするか -------------------------------------------------------
//
// **丸い粒を主役にしない。** はじめは 2〜3 cm の丸い粒をまばらに散らしていたが、
// 白くて丸くて大きさの揃ったものが点々と貼り付いているだけになり、雨ではなく
// 雪にしか読めなかった。実際にガラスに付く水滴は 2〜5 mm しかない。
//
// 1 画素はこの解像度で 4.7 mm ぶんあるので、2〜5 mm の粒は必ず 1 画素に満たない。
// 丸を描こうとすると画素の網に掛からず、ちらついて消える。なので粒は「点」ではなく
// **砂目**（<see cref="Sand"/>。滑らかな値雑音を閾値で抜いたもの）として置く。
//
// **止まった絵ではなく、動いているものとして組む。** 粒を細く暗くしたところ、
// 今度は雨が降っていることが伝わらなくなった。足りなかったのは量ではなく動きで、
// 走行風に引かれて上へ外へ走り続ける水が無いと、ただの汚れたガラスに見える。
// 読める太さを持てるのは筋（<see cref="Rill"/>）と、ちぎれて飛ぶ水
// （<see cref="Dart"/>）の二つで、こちらが主役になる。前者は長く途切れずに、
// 後者は細く速く（2.4 m/s。間口を 0.26 秒で抜ける）走る。
//
// ---- 明るさ -----------------------------------------------------------------
//
// **水そのものは光らない。** 夜のガラスの水は、後ろの灯りを歪めて見せるもので、
// 自分で白く光るものではない。地の色は夜の空（線形 0.115）に近い暗い灰に取ってある。
//
// そのうえで **後ろの灯りを水がにじませるところ**を描く。板は 2 回引く。
//   1 枚目（<c>ScreenwaterLit</c>）は <c>Blend DstColor One</c> で、
//     **後ろにあるものの明るさに掛け算する。** 暗い空の上では何も起きず、
//     街灯や対向車の前照灯の上でだけ水が明るく濁る。夜の水が存在を示すのは
//     この形で、自分から光る粒を置くのとは見え方がまるで違う。
//   2 枚目（<c>Screenwater</c>）は薄い水の色を普通に重ねる。空を背にすれば
//     ほとんど出ず、水の在り処だけが淡く残る。
//
// **<c>_CameraOpaqueTexture</c> は使えない。** WebGL の品質段（Mobile_RPAsset）は
// Opaque Texture を切ってあるので、後ろの絵を読んで歪ませる手は取れない。
// 掛け算なら後ろの絵を読まずに済み、切ってあっても同じに動く。
//
// ---- 拭い跡 -----------------------------------------------------------------
//
// **状態を持たずに出す。** 羽根の角度は centre + amp * sin(位相) で、位相と振り幅と
// 速さはすべて Wipers が渡してくる。ある画素の角を羽根がいつ通ったかは sin を
// 逆に解けば出るので、前のフレームを憶えておく必要が無い。
// 通った直後は 0、_WipeSpan.z 秒かけて 1（溜まりきり）へ戻る。
//
// 羽根の届かない内側・外側・振り幅の外は拭われないまま残る。実際の風防もそうで、
// 隅に水が残っているほうが窓らしく見える。
//
// 渡す値は Wipers が Shader.SetGlobal で置く。マテリアルの側には持たせない。
// 置かれていなければ amp も速さも 0 で、全面が溜まりきったままになる
Shader "HalfAware/Screenwater"
{
    Properties
    {
        [HDR] _BaseColor ("水の色。線形。夜の空に近い暗さに取る", Color) = (0.186, 0.183, 0.176, 1)
        [HDR] _Glint ("筋の頭の色。灯りを集めるところ", Color) = (0.285, 0.275, 0.255, 1)
        _Veil ("薄膜の濃さ", Range(0, 1)) = 0.13
        _Sand ("砂目の濃さ", Range(0, 1)) = 0.32
        _Rill ("筋の濃さ", Range(0, 1)) = 0.85
        _Spray ("ちぎれて飛ぶ水の濃さ", Range(0, 1)) = 0.34
        _Smear ("後ろの灯りをにじませる量。掛け算で効く", Range(0, 3)) = 1.6
        [Toggle] _Wiped ("羽根に拭われる面か。脇の窓には羽根が無い", Float) = 1
        _Fall ("落ちる筋の量。uv の縦が真上でない面では減らす", Range(0, 1)) = 1
        _Grit ("1 m あたりの砂目の刻み", Float) = 150
        _Creep ("砂目が上へ流れる速さ。m/s", Float) = 0.38
        _Fan ("走行風が水を外へ開く量。上端での広がり", Float) = 0.22
        _Up ("後ろへ引かれる筋。列の間隔 / 長さ / 速さ / 半幅。m", Vector) = (0.165, 0.60, 1.70, 0.0030)
        _Down ("落ちる筋。列の間隔 / 長さ / 速さ / 半幅。m", Vector) = (0.270, 0.44, 0.38, 0.0052)
        _Dart ("ちぎれて飛ぶ水。列の間隔 / 長さ / 速さ / 半幅。m", Vector) = (0.058, 0.115, 2.40, 0.0026)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+10" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _Glint;
            half _Veil;
            half _Sand;
            half _Rill;
            half _Spray;
            half _Smear;
            half _Wiped;
            half _Fall;
            float _Grit;
            float _Creep;
            float _Fan;
            float4 _Up;
            float4 _Down;
            float4 _Dart;
        CBUFFER_END

        // ---- ワイパーから渡る値。Wipers.Set が置く -----------------------
        // 板の uv は板の中心を原点にしたメートルなので、軸の位置もメートルで来る

        /// 羽根の軸。(一枚目の x, 一枚目の y, 二枚目の x, 二枚目の y)
        float4 _WipePivot;
        /// (振りの中心, 振り幅, 位相の進む速さ, 今の位相)。ラジアン
        float4 _WipeAim;
        /// (羽根の内端, 羽根の外端, 溜まりきるまでの秒, 羽根の枚数)
        float4 _WipeSpan;
        /// 水の流れる時計。秒。**_Time.y は使わない。**
        /// エディタの _Time.y はエディタを開いてからの秒なので、半日開けたままだと
        /// 数万になり、升目を刻む frac が桁落ちして粒が階段状に飛ぶ。
        /// Wipers が 0 から数え直して 10 分で畳んだものを渡す。
        /// **筋が動いて見えるのはこの値が進んでいるからで、位相だけでは水は流れない**
        float _WipeWash;

        static const float Tau = 6.28318531;

        float2 Hash2(float2 p)
        {
            p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
            return frac(sin(p) * 43758.5453);
        }

        /// <summary>
        /// 滑らかな値雑音。升目の四隅を混ぜる。
        ///
        /// 砂目に円を描かないのは、狙う粒（2〜5 mm）が 1 画素（4.7 mm）に
        /// 満たないため。円は画素の網に掛かるかどうかで出たり消えたりするが、
        /// 滑らかな場を閾値で抜けば、粒より粗い網でも濃淡として残る
        /// </summary>
        float Value(float2 g)
        {
            float2 id = floor(g);
            float2 f = frac(g);
            f = f * f * (3.0 - 2.0 * f);
            float a = Hash2(id).x;
            float b = Hash2(id + float2(1.0, 0.0)).x;
            float c = Hash2(id + float2(0.0, 1.0)).x;
            float d = Hash2(id + float2(1.0, 1.0)).x;
            return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
        }

        /// <summary>
        /// 羽根が通ってからどれだけ溜まったか。0 が拭った直後、1 が溜まりきり。
        /// 羽根の届かないところは 1 のまま返す
        /// </summary>
        float Dried(float2 p, float2 pivot)
        {
            float centre = _WipeAim.x;
            float amp = _WipeAim.y;
            float rate = _WipeAim.z;
            if (amp <= 0.0 || rate <= 0.0) return 1.0;

            float2 d = p - pivot;
            float r = length(d);
            // 羽根の内端と外端。縁は少しぼかす。切り立てると拭い跡が板に見える
            float reach = smoothstep(_WipeSpan.x, _WipeSpan.x + 0.04, r)
                        * (1.0 - smoothstep(_WipeSpan.y - 0.06, _WipeSpan.y, r));
            if (reach <= 0.002) return 1.0;

            // 真上を 0、運転席の側（+x）へ回る向きを正に取る。Wipers.Aim と同じ取り方
            float a = atan2(d.x, d.y);
            float s = (a - centre) / amp;
            reach *= 1.0 - smoothstep(0.88, 1.0, abs(s));
            if (reach <= 0.002) return 1.0;

            // 角度 = centre + amp * sin(位相)。同じ角は一周に二度通る
            s = clamp(s, -1.0, 1.0);
            float first = asin(s);
            float second = 3.14159265 - first;
            float now = _WipeAim.w;
            float back = min(frac((now - first) / Tau), frac((now - second) / Tau)) * Tau;
            float wet = saturate((back / rate) / max(_WipeSpan.z, 0.01));
            return lerp(1.0, wet, reach);
        }

        /// <summary>
        /// 走行風が水をどう運ぶか。板の uv（中心が原点のメートル）を歪めて返す。
        ///
        /// 水は上へ引かれるだけでなく、上へ行くほど中心から外へ開く。
        /// 升目を刻む前に x を割っておくと、同じ列が上ほど外側に出るので、
        /// 時計が進むにつれて筋が上へ外へ流れて見える。
        /// 広げすぎると上端で筋が太って束になるので、上端で 1.35 倍までに留める
        /// </summary>
        float2 Blown(float2 uv)
        {
            float lift = max(uv.y + 0.32, 0.0);
            return float2(uv.x / (1.0 + _Fan * lift), uv.y);
        }

        /// <summary>
        /// 砂目。2〜5 mm の細かい水が面を覆っているところ。
        /// 溜まるにつれて閾値が下がり、まばらな粒から一面の砂目へ移る。
        ///
        /// **閾値を下げすぎない。** 場の半分を抜いたときは、砂目が白い粉雪のように
        /// 間口ぜんたいを覆い、道も対向車も読めなくなった。刻み（_Grit）を細かく取って
        /// 一粒を 1 画素より小さくしたうえで、抜くのは上の三分の一ほどに留める。
        /// wet が 0 のあいだは閾値が場の上限を越えるので、拭った直後は一粒も残らない
        /// </summary>
        float Sand(float2 p, float cells, float t, float wet)
        {
            float2 g = p * cells;
            g.y -= t * _Creep * cells;
            float n = Value(g) * 0.62 + Value(g * 2.1 + 19.0) * 0.38;
            return smoothstep(1.16 - 0.50 * wet, 1.40 - 0.50 * wet, n);
        }

        /// <summary>
        /// 流れる筋。x が総量、y が頭（灯りを集めるところ）。
        ///
        /// spec は (列の間隔, 一本の長さ, 流れる速さ, 半幅) でどれもメートル。
        /// dir が +1 なら走行風で後ろ（上）へ引かれ、-1 なら重さで落ちる。
        /// 尾は頭と反対側へ伸び、頭から離れるほど横へ逃げる。
        /// 真っ直ぐな線を等間隔に引かせると、ガラスの水ではなく外を落ちる雨に見える。
        ///
        /// **升目の三分の二に生やす。** 半分しか無かった頃は一本ずつが離れて浮かび、
        /// 流れているというより点滅して見えた。かといって 8 割まで詰めると、
        /// 太く白い線が間口を斜めに埋めて、ガラスの水ではなく外を落ちる雨になる。
        /// 列ごとに速さを違えてあるので、詰めても一枚の絵として動くことにはならない
        /// </summary>
        float2 Rill(float2 p, float t, float4 spec, float dir, float wet, float seed)
        {
            float pitch = spec.x;
            float span = spec.y;
            float speed = spec.z;
            float wide = spec.w;

            float gx = p.x / pitch + seed;
            float col = floor(gx);
            float2 h = Hash2(float2(col, 5.1 + seed));
            float lane = frac(gx) - 0.5;
            // 列ごとに速さを変える。揃えると板ぜんたいが一枚の絵として動いて見える
            float s = p.y / span - dir * (t * speed / span) * (0.6 + 0.8 * h.x) + h.y * 11.0;
            float row = floor(s);
            float f = frac(s);
            float2 k = Hash2(float2(col, row) + seed);
            if (k.x < 0.34) return float2(0.0, 0.0);
            // 拭った直後から生え始める。土砂降りのガラスは羽根の通った跡に
            // すぐ水が戻るので、ここを渋らせると雨が降っていないように見える
            float born = smoothstep(0.05, 0.30, wet);
            if (born <= 0.0) return float2(0.0, 0.0);

            // u は 1 が頭。後ろへ引かれる筋は上端が頭、落ちる筋は下端が頭
            float u = dir > 0.0 ? f : 1.0 - f;
            float cx = (lane + (h.y - 0.5) * 0.5 + (k.y - 0.5) * 0.9 * saturate(0.85 - u)) * pitch;
            float rad = wide * (0.7 + 0.7 * k.y);
            // 道筋を蛇行させる。真っ直ぐな線は、ガラスを伝う水ではなく
            // ガラスに入った引っ掻き傷に見える。振れ幅は太さの 1 本ぶんまで
            cx += sin(u * 8.5 + h.x * 6.283) * rad * 1.1;
            float body = smoothstep(rad, rad * 0.25, abs(cx));
            // 尾は升目のほとんどを埋める。切れ目を詰めると一本の筋が隣の升目へ続き、
            // 間口を縦に抜ける長さになる
            float tail = body * smoothstep(0.03, 0.66, u) * (1.0 - smoothstep(0.95, 1.0, u));
            // 頭。**大きく明るくしない。** 太い頭に長い尾が付くと、ガラスに乗った水ではなく
            // 目の前を落ちていく雨粒に見える
            float bulb = smoothstep(rad * 1.5, rad * 0.5,
                length(float2(cx, (u - 0.92) * span * 0.16)));
            return float2(born * max(tail * 0.85, bulb * 0.9), born * bulb * 0.7);
        }

        /// <summary>
        /// 走行風にちぎられて上へ飛ぶ細かい水。**流れを見せているのは主にここ。**
        ///
        /// 筋（<see cref="Rill"/>）より細く短く、そのぶん数を多く速く走らせる。
        /// 2.4 m/s は間口（0.63 m）を 0.26 秒で抜ける速さで、止めた絵では
        /// 短い線が散っているだけにしか見えないが、時計が進むと一斉に上へ抜けていく。
        /// 土砂降りの風防で最初に目に入るのはこの動きの方で、
        /// 一粒ずつの形ではない
        /// </summary>
        float Dart(float2 p, float t, float4 spec, float wet)
        {
            float pitch = spec.x;
            float span = spec.y;
            float speed = spec.z;
            float wide = spec.w;

            float gx = p.x / pitch + 7.3;
            float col = floor(gx);
            float2 h = Hash2(float2(col, 2.7));
            float lane = frac(gx) - 0.5;
            float s = p.y / span - (t * speed / span) * (0.75 + 0.5 * h.x) + h.y * 13.0;
            float row = floor(s);
            float f = frac(s);
            float2 k = Hash2(float2(col, row) + 31.0);
            if (k.x < 0.62) return 0.0;

            float cx = (lane + (h.y - 0.5) * 0.7) * pitch;
            float rad = wide * (0.6 + 1.0 * k.y);
            float body = smoothstep(rad, rad * 0.2, abs(cx));
            // 升目の下から len だけ伸びる。頭は上端で、尾へ向かって細く消える
            float len = 0.34 + 0.46 * k.y;
            float u = f / len;
            float along = smoothstep(0.0, 0.80, u) * (1.0 - smoothstep(0.90, 1.0, u));
            return body * along * smoothstep(0.02, 0.22, wet);
        }

        /// <summary>
        /// 水の寄り。濃いところと薄いところを作る。
        /// 升目から起こす砂目は放っておくと板ぜんたいに均して散るので、
        /// これを掛けないと水ではなく規則正しい模様に見える
        /// </summary>
        float Haze(float2 uv, float t)
        {
            float2 g = uv * 2.4;
            g.y -= t * 0.16;
            return 0.45 + 0.75 * Value(g);
        }

        /// <summary>
        /// この画素にどれだけ水が乗っているか（cover）と、
        /// そのうち筋の頭がどれだけ寄っているか（lit）。板を 2 回引くので共通にする
        /// </summary>
        void Water(float2 uv, out float cover, out float lit)
        {
            float t = _WipeWash;
            // **拭われる面かどうかは素材が決める。** 羽根の角も軸も Shader.SetGlobal で
            // 場面ぜんたいに置かれるので、風防と脇の窓が同じ値を読む。脇の窓には
            // 羽根が無いのに、そのまま読ませると風防の扇がそっくり脇の窓にも出る。
            // 拭われない面は溜まりきったまま（1）にして、水を減らさない
            float wet = 1.0;
            if (_Wiped > 0.5)
            {
                wet = Dried(uv, _WipePivot.xy);
                if (_WipeSpan.w > 1.5) wet = min(wet, Dried(uv, _WipePivot.zw));
            }

            // 拭い跡は羽根の軸から測るのでガラスの実寸で、
            // 水そのものは走行風に歪められた側の座標で起こす
            float2 p = Blown(uv);

            float blot = Haze(uv, t);
            float m = _Veil * blot * wet;
            m += _Sand * blot * Sand(p, _Grit, t, wet);

            float2 up = Rill(p, t, _Up, 1.0, wet, 0.0);
            // 落ちる筋は uv の縦を下へ流れる。脇の窓は縦を後ろへ寝かせて張ってあるので、
            // そのまま出すと水が前上がりに走る。面の側で減らせるようにしておく
            float2 down = Rill(p, t, _Down, -1.0, wet, 23.7) * _Fall;
            m += _Rill * max(up.x, down.x);
            m += _Spray * Dart(p, t, _Dart, wet);

            cover = saturate(m);
            lit = saturate(_Rill * max(up.y, down.y));
        }

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
        ENDHLSL

        // ---- 1 枚目。後ろの灯りを水がにじませる -----------------------------
        //
        // <c>Blend DstColor One</c> は 後ろの色 × (1 + ここで返す値) になる。
        // **自分では一切光らない。** 暗い空の上では掛ける相手が無いのでほとんど何も
        // 起きず、街灯や対向車の前照灯の上でだけ、水の乗った画素が明るく濁る。
        // 夜の雨がガラスに在ることを示すのはこの形。
        //
        // **水を重ねるより先に引く。** 後から掛けると、2 枚目が置いた水の色そのものを
        // 掛け算することになり、暗い空の上でも粒が白く飛ぶ。掛ける相手は後ろの景色でなければ
        // 意味が無い。LightMode を書かないパスは SRPDefaultUnlit として扱われ、
        // URP は SRPDefaultUnlit → UniversalForward の順に引くので、ここが先に来る。
        //
        // このパスが引かれなくても 2 枚目だけで水は見える。灯りのにじみが消えるだけで、
        // 黒い穴が開いたり水が消えたりはしない

        Pass
        {
            Name "ScreenwaterLit"
            Blend DstColor One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragLit

            half4 FragLit(Varyings i) : SV_Target
            {
                float cover, lit;
                Water(i.uv, cover, lit);
                // **薄膜のぶんは差し引く。** 間口ぜんたいに一様に乗っている薄膜まで
                // 掛けると、後ろの景色がただ一律に明るくなるだけで、にじみにはならない。
                // 効かせたいのは砂目と筋が厚く溜まっているところだけなので、
                // 薄膜の濃さぶんを底として抜いてから掛ける。
                // 係数を大きく取るのは、夜の帯は掛ける相手が 0.02 しかなく、
                // 控えめに置くと画面のどこにも 9/255 以上の差が出なかったため
                float gain = _Smear * (max(cover - 0.10, 0.0) * 1.30 + lit * 1.80);
                return half4(gain, gain, gain, 1.0);
            }
            ENDHLSL
        }

        // ---- 2 枚目。水そのものを淡く重ねる ---------------------------------

        Pass
        {
            Name "Screenwater"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            half4 Frag(Varyings i) : SV_Target
            {
                float cover, lit;
                Water(i.uv, cover, lit);
                return half4(lerp(_BaseColor.rgb, _Glint.rgb, lit), cover * _BaseColor.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
