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
// 読める大きさを持てるのは筋の方（<see cref="Rill"/>）で、こちらが主役になる。
// 走行風で後ろ（上）へ引かれる細い筋と、重さで落ちる太い筋の二つを重ねる。
// 長さは風防を縦に横切るほど取る。
//
// ---- 明るさ -----------------------------------------------------------------
//
// **水そのものは光らない。** 夜のガラスの水は、後ろの灯りを歪めて見せるもので、
// 自分で白く光るものではない。地の色は夜の空（線形 0.115）に近い暗い灰に取ってある。
// 空を背にすればほとんど出ず、街灯の溜まりや対向車の前照灯を背にすれば
// そこだけ濁って見える。それが水の在り処になる。
// 筋の頭だけは <c>_Glint</c> へ寄せて、集まった水が灯りを集めるところを作る。
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
        [HDR] _BaseColor ("水の色。線形。夜の空に近い暗さに取る", Color) = (0.175, 0.172, 0.166, 1)
        [HDR] _Glint ("筋の頭の色。灯りを集めるところ", Color) = (0.26, 0.25, 0.23, 1)
        _Veil ("薄膜の濃さ", Range(0, 1)) = 0.10
        _Sand ("砂目の濃さ", Range(0, 1)) = 0.30
        _Rill ("筋の濃さ", Range(0, 1)) = 0.85
        _Grit ("1 m あたりの砂目の刻み", Float) = 110
        _Creep ("砂目が後ろへ動く速さ。m/s", Float) = 0.05
        _Up ("後ろへ引かれる筋。列の間隔 / 長さ / 速さ / 半幅。m", Vector) = (0.19, 0.50, 1.10, 0.0045)
        _Down ("落ちる筋。列の間隔 / 長さ / 速さ / 半幅。m", Vector) = (0.30, 0.38, 0.22, 0.0075)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+10" }

        Pass
        {
            Name "Screenwater"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _Glint;
                half _Veil;
                half _Sand;
                half _Rill;
                float _Grit;
                float _Creep;
                float4 _Up;
                float4 _Down;
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
            /// Wipers が 0 から数え直して 10 分で畳んだものを渡す
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
            /// 砂目。2〜5 mm の細かい水が面を覆っているところ。
            /// 溜まるにつれて閾値が下がり、まばらな粒から一面の砂目へ移る
            /// </summary>
            float Sand(float2 uv, float cells, float t, float wet)
            {
                float2 g = uv * cells;
                g.y -= t * _Creep * cells;
                float n = Value(g) * 0.62 + Value(g * 2.1 + 19.0) * 0.38;
                // **閾値は高く取る。** 場の半分を抜いていた頃は、砂目が迷彩の斑に育って
                // 間口ぜんたいを覆い、道も対向車も読めなくなった。抜くのは上の四分の一だけ。
                // wet = 0 では閾値が 1 を越えるので、拭った直後は一粒も残らない
                return smoothstep(1.22 - 0.52 * wet, 1.44 - 0.52 * wet, n);
            }

            /// <summary>
            /// 流れる筋。x が総量、y が頭（灯りを集めるところ）。
            ///
            /// spec は (列の間隔, 一本の長さ, 流れる速さ, 半幅) でどれもメートル。
            /// dir が +1 なら走行風で後ろ（上）へ引かれ、-1 なら重さで落ちる。
            /// 尾は頭と反対側へ伸び、頭から離れるほど横へ逃げる。
            /// 真っ直ぐな線を等間隔に引かせると、ガラスの水ではなく外を落ちる雨に見える
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
                if (k.x < 0.45) return float2(0.0, 0.0);
                float born = smoothstep(0.18, 0.62, wet);
                if (born <= 0.0) return float2(0.0, 0.0);

                // u は 1 が頭。後ろへ引かれる筋は上端が頭、落ちる筋は下端が頭
                float u = dir > 0.0 ? f : 1.0 - f;
                float cx = (lane + (h.y - 0.5) * 0.5 + (k.y - 0.5) * 0.9 * saturate(0.85 - u)) * pitch;
                float rad = wide * (0.7 + 0.7 * k.y);
                float body = smoothstep(rad, rad * 0.25, abs(cx));
                float tail = body * smoothstep(0.03, 0.72, u) * (1.0 - smoothstep(0.93, 1.0, u));
                float bulb = smoothstep(rad * 1.8, rad * 0.4,
                    length(float2(cx, (u - 0.88) * span * 0.30)));
                return float2(born * max(tail * 0.8, bulb), born * bulb);
            }

            /// <summary>
            /// 水の寄り。濃いところと薄いところを作る。
            /// 升目から起こす砂目は放っておくと板ぜんたいに均して散るので、
            /// これを掛けないと水ではなく規則正しい模様に見える
            /// </summary>
            float Haze(float2 uv, float t)
            {
                float2 g = uv * 2.4;
                g.y -= t * 0.05;
                return 0.45 + 0.75 * Value(g);
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

            half4 Frag(Varyings i) : SV_Target
            {
                float t = _WipeWash;
                float wet = Dried(i.uv, _WipePivot.xy);
                if (_WipeSpan.w > 1.5) wet = min(wet, Dried(i.uv, _WipePivot.zw));

                float blot = Haze(i.uv, t);
                float m = _Veil * blot * wet;
                m += _Sand * blot * Sand(i.uv, _Grit, t, wet);

                float2 up = Rill(i.uv, t, _Up, 1.0, wet, 0.0);
                float2 down = Rill(i.uv, t, _Down, -1.0, wet, 23.7);
                m += _Rill * max(up.x, down.x);
                float lit = saturate(_Rill * max(up.y, down.y));

                return half4(lerp(_BaseColor.rgb, _Glint.rgb, lit), saturate(m) * _BaseColor.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
