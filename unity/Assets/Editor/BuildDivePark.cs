using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 4 の公園。
    ///
    /// **一目でどこか分かることを条件に組む。** 記憶は他人の頭から抜いてきた絵なので
    /// 隅々まで見えている必要はないが、床と壁だけの箱では何の場所か読めない。
    /// 等間隔に並ぶものを一つ入れると、その場所らしさがいちばん早く伝わる。
    ///
    /// ここで読ませたい形は三つ。**ベンチの列・街灯・明るい小径の帯**。
    /// ほかの物（植え込み・ゴミ箱・水飲み場・池の柵）は、この三つが立ってから
    /// 隙間を埋めるために置いてある。
    ///
    /// 寸法は場所のローカル。鍵打ち（<c>BuildDiveTakes.cs</c>）が同じ数を見るので、
    /// 目印になる点は const にして両方から見る
    /// </summary>
    public static partial class BuildDive
    {
        // ---- 公園 ------------------------------------------------------------

        /// <summary>左のベンチ。記憶 2 のアルベルトが掛けている</summary>
        public static readonly Vector3 BenchA = new Vector3(0f, 0f, 0f);
        /// <summary>右のベンチ。記憶 9 のローザが餌の袋を畳んでいる</summary>
        public static readonly Vector3 BenchB = new Vector3(2.6f, 0f, 0f);
        /// <summary>
        /// 三脚目。人は掛けない。
        /// 二脚では「たまたま二つ置いてある」にしか見えず、列として読めないので同じ間隔で伸ばす
        /// </summary>
        public static readonly Vector3 BenchC = new Vector3(5.2f, 0f, 0f);
        /// <summary>門。ベンチから真っ直ぐ +z</summary>
        public const float GateZ = 9.4f;
        const float GateHalf = 1.3f;
        /// <summary>池の縁の中心と半径。鳩はこの手前に集まる</summary>
        public static readonly Vector3 Pond = new Vector3(-4.2f, 0f, 4.6f);
        const float PondR = 2.8f;
        /// <summary>鳩の群れの真ん中</summary>
        public static readonly Vector3 Flock = new Vector3(0.6f, 0f, 3.6f);

        /// <summary>小径の半分の幅。門の柱の間（±<see cref="GateHalf"/>）を通り抜けられる幅に留める</summary>
        const float ParkWalkHalf = 1.15f;
        /// <summary>
        /// 池の手前側の半径。<see cref="PondR"/> より狭い。
        /// 真円のままだと水際が主の歩く線（記憶 2 の -1.3、記憶 10 の -1.4）に重なって、
        /// 池の縁に立ったはずの人が水の上に立つ
        /// </summary>
        const float ParkPondNear = 1.6f;
        /// <summary>池の南北の半径</summary>
        const float ParkPondSide = 2.6f;
        /// <summary>街灯の間隔。等間隔の繰り返しが、ここが公園だといちばん早く伝える</summary>
        const float ParkLampStep = 8f;
        /// <summary>街灯の列の x。小径の西の肩で、池の手前を横切らない位置</summary>
        const float ParkLampX = -2.35f;
        /// <summary>池の柵を並べる角度の幅。小径から見える手前側だけ</summary>
        const float ParkHoopArc = 60f;


        // ---- 公園 --------------------------------------------------------------

        /// <summary>
        /// 記憶 2・3・9・10 の舞台。ベンチ三脚から門まで真っ直ぐ歩けるようにしてある。
        /// 池は縁石と水の面だけで、深さは持たない。遠くの木は板で済ませる
        /// </summary>
        static void Park(Transform place)
        {
            var rim = ParkRim(0f);
            var pave = ParkMat("ParkPave", new Color(0.395f, 0.390f, 0.368f), 0.10f);
            var leaf = ParkMat("ParkLeaf", new Color(0.108f, 0.140f, 0.082f), 0.03f);

            // **一色の平面ではどこを歩く場所なのか読めない。** 芝を地にして、踏まれるところへ土、
            // 通る筋へ舗装を薄く重ねる。重ねる高さを 1 cm ほどずつ違えてあるのは、
            // 同じ高さだと面どうしが奪い合って斑になるため
            var grass = new Bank { Texel = 0.22f };
            grass.FaceY(0f, -9f, 9f, -9f, 13f, 1);
            ParkEmit(place, "ParkGround", grass,
                ParkMat("ParkGrass", new Color(0.186f, 0.232f, 0.132f), 0.05f), true);

            var soil = new Bank { Texel = 0.3f };
            // 短冊に分けて縁を振る。一枚の四角だと、芝の上に茶色い絨毯を敷いたように見える
            for (var i = 0; i < 12; i++)
            {
                var wave = Mathf.Repeat(i * 0.618f, 1f);
                var x0 = -2.6f + i * 0.825f;
                soil.FaceY(0.012f, x0, x0 + 0.825f, -2.3f - wave * 0.6f, 0.9f + wave * 0.5f, 1);
            }
            ParkShore(soil, rim, ParkRim(0.9f), 0.012f);
            ParkEmit(place, "ParkSoil", soil,
                ParkMat("ParkSoil", new Color(0.262f, 0.208f, 0.148f), 0.04f), false);

            // 小径。門からベンチの前を通って池の岸まで、切れ目なく明るい帯で繋ぐ。
            // 街灯と水飲み場を同じ素材で持たせてあるのは、どれもコンクリートで、
            // 別の素材にすると公園だけマテリアルが二枚増えるため
            var walk = new Bank { Texel = 0.35f };
            walk.FaceY(0.02f, -ParkWalkHalf, ParkWalkHalf, 0.6f, 10.6f, 1);
            walk.FaceY(0.02f, -2.0f, 6.7f, -0.9f, 0.6f, 1);
            walk.FaceY(0.02f, -1.9f, -ParkWalkHalf, 2.2f, 7.2f, 1);
            walk.FaceY(0.02f, -2.0f, 2.0f, 10.6f, 11.7f, 1);
            for (var i = -1; i <= 1; i++)
                ParkLamp(walk, new Vector3(ParkLampX, 0f, 1f + i * ParkLampStep));
            ParkFount(walk, new Vector3(2.15f, 0f, 2.6f));
            ParkEmit(place, "ParkPath", walk, pave, false);

            var stone = new Bank { Texel = 0.4f };
            ParkCoping(stone, rim);
            stone.Box(new Vector3(-GateHalf, 1.2f, GateZ), new Vector3(0.35f, 2.4f, 0.35f));
            stone.Box(new Vector3(GateHalf, 1.2f, GateZ), new Vector3(0.35f, 2.4f, 0.35f));
            // 柱の頭。逆光で柱が黒い棒になっても、頭の段だけは影の形が変わって門だと読める
            stone.Box(new Vector3(-GateHalf, 2.48f, GateZ), new Vector3(0.52f, 0.16f, 0.52f));
            stone.Box(new Vector3(GateHalf, 2.48f, GateZ), new Vector3(0.52f, 0.16f, 0.52f));
            ParkEmit(place, "ParkStone", stone, pave, true);

            var pool = new Bank { Texel = 0.2f };
            pool.FanY(new Vector3(Pond.x, 0.06f, Pond.z), rim);
            Emit(place, "ParkWater", pool, "Water");

            var wood = new Bank { Texel = 0.5f };
            Bench(wood, BenchA);
            Bench(wood, BenchB);
            Bench(wood, BenchC);
            Emit(place, "ParkBench", wood, "Timber");

            var iron = new Bank { Texel = 0.3f };
            ParkHoops(iron);
            ParkBin(iron, new Vector3(1.55f, 0f, 8.7f));
            Emit(place, "ParkIron", iron, "Wall");

            var hedge = new Bank { Texel = 0.3f };
            ParkHedge(hedge, new Vector3(1.72f, 0f, 4.3f), new Vector3(1.72f, 0f, 8.7f));
            ParkHedge(hedge, new Vector3(-1.72f, 0f, 7.4f), new Vector3(-1.72f, 0f, 8.7f));
            ParkHedge(hedge, new Vector3(-1.9f, 0f, -1.5f), new Vector3(6.7f, 0f, -1.5f));
            ParkEmit(place, "ParkHedge", hedge, leaf, false);

            // 遠くの木。板を並べるだけで、近づけないところに置く。
            // 四方を回してあるのは、抜けているところから場所の外の虚空が覗くため
            var far = new Bank { Texel = 0.2f };
            for (var i = 0; i < 11; i++) ParkTree(far, new Vector3(-9f + i * 1.9f, 0f, -7.5f), 0f, i);
            for (var i = 0; i < 6; i++)
            {
                ParkTree(far, new Vector3(-8.4f, 0f, -5.5f + i * 3.4f), 90f, i + 3);
                ParkTree(far, new Vector3(8.4f, 0f, -5.5f + i * 3.4f), 90f, i + 7);
            }
            // 門の正面だけは空けておく。塞ぐと、どこから入ってきた場所なのかが読めなくなる
            for (var i = 0; i < 8; i++)
            {
                var x = -8.6f + i * 2.4f;
                if (Mathf.Abs(x) < 2.8f) continue;
                ParkTree(far, new Vector3(x, 0f, 12.4f), 0f, i + 5);
            }
            NoShadow(Emit(place, "ParkTrees", far, "Leaf"));

            // 鳩は場所ではなく記憶の側に置く（BuildDiveTakes.Doves）。
            // 飛び立つ秒が記憶ごとに違うので、場所に置くと四つの記憶で同じ瞬間に飛ぶことになる

            Ring(place, "ParkFence", new Vector2(-9f, 9f), new Vector2(-9f, 13f), 3.5f);

            // 午後の低い日。**門の側（+z）から差す。** 門へ歩く人はこれで逆光になり、
            // 遠景の板の影も場所の外へ伸びる。逆向きに当てていたときは、板の影が
            // 十数メートルの帯になってベンチから池までを丸ごと夜にしていた
            var sun = Lamp(place, "Afternoon", LightType.Directional, new Vector3(0f, 12f, 0f),
                new Vector3(26f, 188f, 0f), new Color(1f, 0.93f, 0.78f), 3.1f, 10f);
            sun.shadows = LightShadows.Soft;

            // 逆光の一灯だけでは、こちらを向いた面がどれも黒く潰れて輪郭が拾えない。
            // 環境光はシーンぜんたいのもので、公園のためだけには上げられないので、
            // 影を落とさない灯りを二つ、場所の中へ入れて底を持ち上げる。
            // 二つを東西へ振り分けてあるのは、街灯の柱も植え込みも縦の面ばかりで、
            // 上からだけ当てるとどの側面も影のまま残るため
            Lamp(place, "ParkFillFront", LightType.Directional, new Vector3(0f, 12f, 0f),
                new Vector3(18f, 40f, 0f), new Color(0.74f, 0.79f, 0.94f), 0.90f, 10f);
            Lamp(place, "ParkFillSky", LightType.Directional, new Vector3(0f, 12f, 0f),
                new Vector3(55f, -40f, 0f), new Color(0.66f, 0.72f, 0.88f), 0.65f, 10f);
        }

        /// <summary>
        /// ベンチ 1 脚。
        ///
        /// 座面と背もたれを桟に分けてあるのは、板一枚のままだと木の色をした箱にしか見えず、
        /// 三脚並べても列として読めないため。桟の隙間に日が抜けて初めてベンチになる
        /// </summary>
        static void Bench(Bank b, Vector3 at)
        {
            for (var i = 0; i < 3; i++)
                b.Box(at + new Vector3(0f, 0.45f, -0.16f + i * 0.16f), new Vector3(1.80f, 0.06f, 0.13f));
            for (var i = 0; i < 2; i++)
                b.Box(at + new Vector3(0f, 0.66f + i * 0.20f, -0.26f), new Vector3(1.80f, 0.15f, 0.05f));
            for (var i = 0; i < 2; i++)
            {
                var x = (i == 0 ? -1f : 1f) * 0.80f;
                b.Box(at + new Vector3(x, 0.68f, -0.26f), new Vector3(0.07f, 0.48f, 0.07f));
                b.Box(at + new Vector3(x, 0.22f, -0.20f), new Vector3(0.07f, 0.44f, 0.07f));
                b.Box(at + new Vector3(x, 0.22f, 0.16f), new Vector3(0.07f, 0.44f, 0.07f));
                b.Box(at + new Vector3(x, 0.41f, -0.02f), new Vector3(0.09f, 0.08f, 0.50f));
            }
        }

        /// <summary>
        /// 遠くの木 1 本。幹と葉の板だけで済ませる。
        ///
        /// 丈と幅を <paramref name="seed"/> で振ってあるのは、同じ丈の板を並べると
        /// 木立ではなく緑に塗った壁に見えるため。
        /// 幹の高さぶん下を空けておくと、根元から日が抜けて奥行きが出る
        /// </summary>
        static void ParkTree(Bank b, Vector3 at, float turn, int seed)
        {
            // 黄金比のずらし。並びに周期が出ない
            var wave = Mathf.Repeat(seed * 0.618f, 1f);
            var high = 4.4f + wave * 2.6f;
            var wide = 1.7f + wave * 1.2f;
            var lift = 1.0f + wave * 0.6f;
            var rot = Quaternion.Euler(0f, turn, 0f);
            var span = high - lift;
            b.Box(at + new Vector3(0f, lift * 0.5f, 0f), new Vector3(0.26f, lift, 0.12f), rot);
            // 葉は三段。四角一枚のままだと、並べたときに木立ではなく街の遠景に見える
            b.Box(at + new Vector3(0f, lift + span * 0.16f, 0f),
                new Vector3(wide * 0.72f, span * 0.32f, 0.12f), rot);
            b.Box(at + new Vector3(0f, lift + span * 0.48f, 0f),
                new Vector3(wide, span * 0.38f, 0.12f), rot);
            b.Box(at + new Vector3(0f, lift + span * 0.83f, 0f),
                new Vector3(wide * 0.52f, span * 0.34f, 0.12f), rot);
        }

        /// <summary>
        /// 街灯 1 本。
        ///
        /// **夕方なので灯は入れない。** 笠と柱の形だけで読ませる。灯りを点けると、
        /// 記憶ごとに色味を寄せる Volume の前で光る点が三つ増えて、絵の明るさがそちらに引かれる
        /// </summary>
        static void ParkLamp(Bank b, Vector3 at)
        {
            b.Box(at + new Vector3(0f, 0.10f, 0f), new Vector3(0.42f, 0.20f, 0.42f));
            b.Box(at + new Vector3(0f, 1.85f, 0f), new Vector3(0.15f, 3.50f, 0.15f));
            b.Box(at + new Vector3(0f, 3.66f, 0f), new Vector3(0.26f, 0.12f, 0.26f));
            b.Box(at + new Vector3(0f, 3.90f, 0f), new Vector3(0.34f, 0.36f, 0.34f));
            b.Box(at + new Vector3(0f, 4.12f, 0f), new Vector3(0.58f, 0.08f, 0.58f));
            b.Box(at + new Vector3(0f, 4.20f, 0f), new Vector3(0.40f, 0.08f, 0.40f));
            b.Box(at + new Vector3(0f, 4.30f, 0f), new Vector3(0.14f, 0.12f, 0.14f));
        }

        /// <summary>
        /// 水飲み場。公園と学校の庭にしか無い物なので、一つあるだけで場所が絞れる
        /// </summary>
        static void ParkFount(Bank b, Vector3 at)
        {
            b.Box(at + new Vector3(0f, 0.04f, 0f), new Vector3(0.72f, 0.08f, 0.72f));
            b.Box(at + new Vector3(0f, 0.45f, 0f), new Vector3(0.34f, 0.82f, 0.34f));
            b.Box(at + new Vector3(0f, 0.90f, 0f), new Vector3(0.56f, 0.14f, 0.48f));
            b.Box(at + new Vector3(0f, 1.00f, -0.10f), new Vector3(0.06f, 0.14f, 0.06f));
        }

        /// <summary>ゴミ箱。門の内側に置いて、入ってすぐ目に入るようにする</summary>
        static void ParkBin(Bank b, Vector3 at)
        {
            b.Box(at + new Vector3(0f, 0.05f, 0f), new Vector3(0.46f, 0.10f, 0.46f));
            b.Box(at + new Vector3(0f, 0.40f, 0f), new Vector3(0.42f, 0.60f, 0.42f));
            b.Box(at + new Vector3(0f, 0.72f, 0f), new Vector3(0.50f, 0.06f, 0.50f));
            b.Box(at + new Vector3(0f, 0.80f, 0f), new Vector3(0.38f, 0.10f, 0.38f));
        }

        /// <summary>
        /// 低い植え込みの列。
        ///
        /// 株を 0.8 m 間隔で並べて小径の縁をなぞる。等間隔の繰り返しが、
        /// 舗装の帯だけのときより強く「歩く筋」を示す。
        /// またげる高さなので当たりは入れない。引っ掛かる隅を増やすほうが困る
        /// </summary>
        static void ParkHedge(Bank b, Vector3 from, Vector3 to)
        {
            var run = to - from;
            var span = run.magnitude;
            if (span < 0.01f) return;
            var dir = run / span;
            var turn = Quaternion.LookRotation(dir, Vector3.up);
            var n = Mathf.Max(1, Mathf.RoundToInt(span / 0.8f));
            for (var i = 0; i <= n; i++)
            {
                var at = from + dir * (span * i / n);
                // 株ごとに背を振る。同じ高さで揃えると生垣ではなく低い塀に見える
                var tall = 0.44f + Mathf.Repeat(i * 0.618f, 1f) * 0.16f;
                b.Box(at + new Vector3(0f, tall * 0.5f, 0f), new Vector3(0.62f, tall, 0.78f), turn);
                b.Box(at + new Vector3(0f, tall + 0.08f, 0f), new Vector3(0.48f, 0.20f, 0.62f), turn);
            }
        }

        /// <summary>
        /// 池の縁の点。上から見て左回り。
        ///
        /// 手前（+x）側だけ半径を狭めてあるのと、三つ山の揺らぎを重ねてあるのは、
        /// 真円の水たまりが人の掘った池に見えないため。<paramref name="grow"/> は外へ広げる量で、
        /// 岸の土と柵を同じ形から取るのに使う
        /// </summary>
        static Vector2[] ParkRim(float grow)
        {
            const int n = 24;
            var rim = new Vector2[n];
            for (var i = 0; i < n; i++) rim[i] = ParkRimAt(Mathf.PI * 2f * i / n, grow);
            return rim;
        }

        static Vector2 ParkRimAt(float a, float grow)
        {
            var cos = Mathf.Cos(a);
            var wave = 1f + 0.06f * Mathf.Sin(a * 3f);
            var rx = (cos >= 0f ? ParkPondNear : PondR) * wave + grow;
            var rz = ParkPondSide * wave + grow;
            return new Vector2(Pond.x + cos * rx, Pond.z + Mathf.Sin(a) * rz);
        }

        /// <summary>
        /// 池の縁石。
        ///
        /// 水の面だけでは地面に置いた板にしか見えないので、縁を一段上げて窪みに見せる。
        /// 一周させてあるのは、対岸の縁が切れていると水が地面へ流れ出して見えるため
        /// </summary>
        static void ParkCoping(Bank b, Vector2[] rim)
        {
            var mid = new Vector2(Pond.x, Pond.z);
            for (var i = 0; i < rim.Length; i++)
            {
                var p = rim[i];
                var q = rim[(i + 1) % rim.Length];
                var at = (p + q) * 0.5f;
                var run = q - p;
                var away = (at - mid).normalized * 0.17f;
                b.Box(new Vector3(at.x + away.x, 0.15f, at.y + away.y),
                    new Vector3(0.34f, 0.30f, run.magnitude + 0.06f),
                    Quaternion.LookRotation(new Vector3(run.x, 0f, run.y).normalized, Vector3.up));
            }
        }

        /// <summary>池の岸。縁石の外へ土を回して、芝がそのまま水際で切れないようにする</summary>
        static void ParkShore(Bank b, Vector2[] inner, Vector2[] outer, float y)
        {
            for (var i = 0; i < inner.Length; i++)
            {
                var j = (i + 1) % inner.Length;
                b.Quad(new Vector3(inner[i].x, y, inner[i].y), new Vector3(inner[j].x, y, inner[j].y),
                    new Vector3(outer[j].x, y, outer[j].y), new Vector3(outer[i].x, y, outer[i].y));
            }
        }

        /// <summary>
        /// 池の低い柵。輪を伏せて並べたもの。
        ///
        /// 小径の側にだけ並べるのは、対岸の分は水面越しに縁石と重なって潰れるため。
        /// またげる高さなので当たりは入れない
        /// </summary>
        static void ParkHoops(Bank b)
        {
            var from = -ParkHoopArc * Mathf.Deg2Rad;
            var span = ParkHoopArc * 2f * Mathf.Deg2Rad;
            var last = ParkRimAt(from, 0.55f);
            var run = 0f;
            for (var i = 1; i <= 120; i++)
            {
                var p = ParkRimAt(from + span * i / 120f, 0.55f);
                run += Vector2.Distance(last, p);
                // 0.62 m は輪の差し渡し。隣どうしが触れる間隔にすると柵の線が切れない
                if (run >= 0.62f)
                {
                    var tan = (p - last).normalized;
                    ParkHoop(b, new Vector3(p.x, 0f, p.y), new Vector3(tan.x, 0f, tan.y));
                    run = 0f;
                }
                last = p;
            }
        }

        static void ParkHoop(Bank b, Vector3 at, Vector3 along)
        {
            const float r = 0.30f;
            const int n = 7;
            // 輪の面の法線。真上を向く辺があるので、ここを上向きに取ると回しようが無くなる
            var side = Vector3.Cross(Vector3.up, along).normalized;
            for (var i = 0; i < n; i++)
            {
                var a0 = Mathf.PI * i / n;
                var a1 = Mathf.PI * (i + 1) / n;
                var p0 = at + along * (Mathf.Cos(a0) * r) + Vector3.up * (Mathf.Sin(a0) * r);
                var p1 = at + along * (Mathf.Cos(a1) * r) + Vector3.up * (Mathf.Sin(a1) * r);
                var run = p1 - p0;
                b.Box((p0 + p1) * 0.5f, new Vector3(0.05f, 0.05f, run.magnitude + 0.02f),
                    Quaternion.LookRotation(run.normalized, side));
            }
        }

        /// <summary>
        /// 公園にだけ要る色。
        ///
        /// <see cref="Tone"/> の表はどの場所からも見るので、そこを明るくすると五つの場所が
        /// 揃って明るくなる。芝・土・舗装のように公園でしか使わない色はここに持つ
        /// </summary>
        static Material ParkMat(string name, Color col, float smooth)
        {
            var path = Materials + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.name = name;
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", null);
            m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>素材の名前ではなくマテリアルそのものを渡して焼く。公園の色は表に無いので</summary>
        static Transform ParkEmit(Transform parent, string name, Bank bank, Material mat, bool collide)
        {
            var made = bank.Emit(parent, name, mat, collide, Generated);
            return made != null ? made.transform : null;
        }
    }
}
