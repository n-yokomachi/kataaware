// 風防に乗った水。**車の外に降る雨ではなく、ガラスの面に付いた水の方を描く。**
//
// 場面 8 は運転席からの一人称で、走行中の画面はほとんど風防で埋まっている。
// 外に粒を降らせても、目から 1 m 先を落ちる粒は 1 フレームで画面を横切るので
// 427 × 240 では線にすらならず、粒を増やすほど WebGL が重くなるだけだった。
// 雨が降っていると分かるのはガラスの側で、溜まった水と、それを拭う羽根の二つで足りる。
//
// 板は一枚。粒も筋も薄膜も、画素ごとに升目の乱数から起こす。絵も持たないし、
// 粒ひとつに mesh を立てることもしない。
//
// **拭った跡は状態を持たずに出す。** 羽根の角度は centre + amp * sin(位相) で、
// 位相と振り幅と速さはすべて Wipers が渡してくる。ある画素の角を羽根がいつ通ったかは
// sin を逆に解けば出るので、前のフレームを憶えておく必要が無い。
// 通った直後は 0、_WipeSpan.w 秒かけて 1（溜まりきり）へ戻る。
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
        [HDR] _BaseColor ("水の色", Color) = (0.62, 0.66, 0.72, 1)
        _Veil ("薄膜の濃さ", Range(0, 1)) = 0.11
        _Bead ("粒の濃さ", Range(0, 1)) = 0.62
        _Rill ("流れる筋の濃さ", Range(0, 1)) = 0.5
        _Grain ("1 m あたりの粒の升目", Float) = 13
        _Runs ("1 m あたりの筋の列", Float) = 9
        _Flow ("筋が上る速さ。m/s", Float) = 0.9
        _Creep ("粒が上る速さ。m/s", Float) = 0.06
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
                half _Veil;
                half _Bead;
                half _Rill;
                float _Grain;
                float _Runs;
                float _Flow;
                float _Creep;
            CBUFFER_END

            // ---- ワイパーから渡る値。Wipers.Push が置く -----------------------
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
            /// 丸い粒。溜まるにつれて升目ごとに順に現れ、少しずつ後ろへ上る。
            ///
            /// **升目の半分には立てない。** どの升目にも粒を置くと、大きさを振っても
            /// 427 × 240 では等間隔に並んだ白い点の壁にしかならず、雪が貼り付いて見えた。
            /// 疎らに、大きく、小さい粒ほど暗く。粒どうしの間が空いていないと
            /// ガラスの向こうの道が読めない
            /// </summary>
            float Beads(float2 uv, float cells, float t, float wet, float seed)
            {
                float2 g = uv * cells + seed;
                g.y -= t * _Creep * cells;
                float2 id = floor(g);
                float2 k = Hash2(id + seed);
                if (k.x < 0.52) return 0.0;
                // 一斉に出ると板が点滅する。升目ごとに出る頃合いをずらす
                float when = frac(k.x * 7.31);
                float born = smoothstep(when * 0.75, when * 0.75 + 0.25, wet);
                if (born <= 0.0) return 0.0;
                float2 off = (Hash2(id + 17.3 + seed) - 0.5) * 0.7;
                // **大きさは二乗で振る。** 一様に振ると、どの粒も同じくらいの
                // 中くらいの点になって、ガラスの水ではなく貼り付いた雪に見えた。
                // 二乗にすれば小さい粒が大半を占め、たまに大きいのが混じる
                float rad = (0.07 + 0.40 * k.y * k.y) * (0.5 + 0.5 * wet);
                // 走っているので粒は縦に伸びる。x を詰めて縦長にする
                float d = length((frac(g) - 0.5 - off) * float2(1.6, 1.0));
                return born * smoothstep(rad, rad * 0.25, d) * (0.35 + 0.65 * k.y);
            }

            /// <summary>
            /// 走っているあいだ後ろへ流れる筋。頭が丸く、下へ尾を引く。
            /// 列ごとに速さを変える。揃えると板ぜんたいが一枚の絵として動いて見える
            /// </summary>
            float Rills(float2 uv, float cells, float t, float wet, float seed)
            {
                float2 g = uv * cells + seed;
                float col = floor(g.x);
                float2 h = Hash2(float2(col, 3.7 + seed));
                float lane = frac(g.x) - 0.5;
                float y = g.y - t * _Flow * cells * (0.55 + 0.9 * h.x) - h.y * 13.0;
                float row = floor(y);
                float f = frac(y);
                float2 k = Hash2(float2(col, row));
                // **筋はまばらに、短く。** どの升目にも長い尾を引かせると、板ぜんたいが
                // 等間隔に並んだ縦線になり、ガラスに乗った水ではなく外を落ちる雨に見えた
                if (k.x < 0.62) return 0.0;
                float born = smoothstep(0.32, 0.78, wet);
                if (born <= 0.0) return 0.0;
                // 尾は真っ直ぐ下りない。頭から離れるほど横へ逃げる
                float cx = lane + (h.y - 0.5) * 0.30 + (k.y - 0.5) * 0.5 * saturate(0.78 - f);
                float head = smoothstep(0.22, 0.04, length(float2(cx * 2.2, (f - 0.80) * 1.3)));
                float tail = smoothstep(0.11, 0.02, abs(cx)) * smoothstep(0.38, 0.78, f);
                return born * max(head, tail * 0.40);
            }

            /// <summary>薄膜のむら。一様に掛けると窓ではなく曇りガラスになる</summary>
            float Haze(float2 uv, float t)
            {
                float2 g = uv * 2.6;
                g.y -= t * 0.05;
                float2 id = floor(g);
                float2 f = frac(g) - 0.5;
                float2 k = Hash2(id + 91.7);
                return 0.55 + 0.45 * smoothstep(0.55, 0.12, length(f - (k - 0.5) * 0.7));
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

                // むらは薄膜と粒の両方に掛ける。升目から起こす粒は放っておくと
                // 板ぜんたいに均して散るので、濃いところと薄いところを作らないと
                // 水ではなく規則正しい模様に見える
                float blot = Haze(i.uv, t);
                float m = _Veil * blot * wet;
                m += _Bead * blot * Beads(i.uv, _Grain, t, wet, 0.0);
                m += _Bead * 0.26 * Beads(i.uv, _Grain * 1.7, t, wet, 41.3);
                m += _Rill * Rills(i.uv, _Runs, t, wet, 0.0);
                return half4(_BaseColor.rgb, saturate(m) * _BaseColor.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
