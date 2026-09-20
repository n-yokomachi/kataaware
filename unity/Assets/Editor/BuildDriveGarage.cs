using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 場面 8 のガレージ。区画・隣の車・シャッターの操作盤・奥の壁際を作る。
    /// 寸法の定数と組み立ての入口は <see cref="BuildDrive"/> 本体にある
    /// </summary>
    public static partial class BuildDrive
    {
        // ---- ガレージ --------------------------------------------------------

        /// <summary>
        /// 車を入れてある共用のガレージ。プレイヤーは隅に立ち、歩いて運転席のドアまで来る。
        /// 乗り込んだ時点で DriveDirector が丸ごと伏せるので、入れ物はひとつにまとめる。
        ///
        /// **壁は省けない。** 道のタイルは y 0 のまま z -30 から 150 まで敷いてあり、
        /// DriveWorld には道を伏せる手立てが無い（Dress(-1) が消すのは沿道だけ）。
        /// 壁が無いと、ガレージに立っている間ずっと道が左右へ突き抜けて見える。
        ///
        /// 面はどれも Box で作る。板 1 枚で立てると内側から見た面が裏になり、
        /// 絵が出ないうえに MeshCollider の光線も素通りする
        /// </summary>
        static void Garage(Transform root)
        {
            var parent = Child(root, "Garage");
            Clear(parent);
            parent.gameObject.SetActive(true);
            parent.localPosition = Vector3.zero;

            var halfX = GarageWide * 0.5f;
            var halfZ = GarageDeep * 0.5f;
            var skin = GarageThick * 0.5f;
            // 壁は床の面から天井の面まで。中心と高さをここで一度だけ出す
            var midY = (GarageFloorY + GarageHigh) * 0.5f;
            var tall = GarageHigh - GarageFloorY;
            var front = GarageAt.z + halfZ + skin;

            var floor = new Bank { Texel = 0.5f };
            floor.Box(new Vector3(GarageAt.x, GarageFloorY - skin, GarageAt.z),
                new Vector3(GarageWide, GarageThick, GarageDeep));

            var walls = new Bank { Texel = 0.5f };
            // 長辺の壁。四隅を覆うので、z 方向は厚みのぶん伸ばす
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                walls.Box(new Vector3(GarageAt.x + side * (halfX + skin), midY, GarageAt.z),
                    new Vector3(GarageThick, tall, GarageDeep + GarageThick * 2f));
            }
            walls.Box(new Vector3(GarageAt.x, midY, GarageAt.z - halfZ - skin),
                new Vector3(GarageWide, tall, GarageThick));
            // 前の壁はシャッターの口を空けて、左右とまぐさだけ立てる
            var jamb = halfX - ShutterWide * 0.5f;
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                walls.Box(new Vector3(GarageAt.x + side * (ShutterWide * 0.5f + jamb * 0.5f), midY, front),
                    new Vector3(jamb, tall, GarageThick));
            }
            walls.Box(new Vector3(GarageAt.x, (ShutterHigh + GarageHigh) * 0.5f, front),
                new Vector3(ShutterWide, GarageHigh - ShutterHigh, GarageThick));

            var roof = new Bank { Texel = 0.5f };
            roof.Box(new Vector3(GarageAt.x, GarageHigh + skin, GarageAt.z),
                new Vector3(GarageWide, GarageThick, GarageDeep));

            // 柱は四隅と、区画の列の境目。壁から部屋の側へ出して、面の中に埋もれないようにする。
            // 通路の両端に立てると、二つの列の切れ目が柱で読めるようになる
            var posts = new Bank { Texel = 0.5f };
            var inset = halfX - PillarSide * 0.5f;
            var rows = new[]
            {
                GarageAt.z - halfZ + PillarSide * 0.5f, AisleFrom, AisleTo,
                GarageAt.z + halfZ - PillarSide * 0.5f,
            };
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                for (var i = 0; i < rows.Length; i++)
                    posts.Box(new Vector3(GarageAt.x + side * inset, midY, rows[i]),
                        new Vector3(PillarSide, tall, PillarSide));
            }

            // シャッターは壁の厚みの中へ収める。壁と同じ面に置くと、縁でちらつく
            var door = new Bank { Texel = 0.5f };
            door.Box(new Vector3(GarageAt.x, (GarageFloorY + ShutterHigh) * 0.5f, front),
                new Vector3(ShutterWide, ShutterHigh - GarageFloorY, GarageThick * 0.5f));
            Slats(door, front - GarageThick * 0.25f);

            // 当たりは壁と床に要る。歩いて出られては困るし、床が無いと落ちる
            floor.Emit(parent, "Floor", Mat("GarageFloor"), true, Generated);
            walls.Emit(parent, "Walls", Mat("GarageWall"), true, Generated);
            roof.Emit(parent, "Ceiling", Mat("Concrete"), true, Generated);
            posts.Emit(parent, "Pillars", Mat("Concrete"), true, Generated);
            door.Emit(parent, "Shutter", Mat("Shutter"), true, Generated);

            Blocker(parent);
            Bays(parent);
            Covered(parent);
            Neighbours(parent);
            Controls(parent);
            Racks(parent);
            Lamps(parent);
        }

        /// <summary>
        /// シャッターの桟。歩く線の正面にあるのに、今までただの平らな面だった。
        /// inner は壁の厚みの中にある内側の面の z。ここから部屋の側へ出す。
        /// 巻き上げ式なので横桟だけ。真ん中に一本だけ太いのが持ち手にあたる
        /// </summary>
        static void Slats(Bank bank, float inner)
        {
            const float pitch = 0.235f;
            const float depth = 0.035f;
            var z = inner - depth * 0.5f;
            for (var y = GarageFloorY + 0.14f; y < ShutterHigh - 0.05f; y += pitch)
            {
                var grip = Mathf.Abs(y - 1.15f) < pitch * 0.5f;
                bank.Box(new Vector3(GarageAt.x, y, z),
                    new Vector3(ShutterWide - 0.10f, grip ? 0.105f : 0.060f, depth));
            }
            // 下端のレール。床との境に影が落ちて、閉まっていることが読める
            bank.Box(new Vector3(GarageAt.x, GarageFloorY + 0.045f, z),
                new Vector3(ShutterWide - 0.04f, 0.090f, depth * 1.4f));
        }

        /// <summary>
        /// 床の飾り。歩いているあいだ画面の下半分を埋めるのは床なので、
        /// ここが空だと 8 m のあいだ見るものが何も無い。
        /// 区画の線・番号・油染み・排水口の 4 つだけ置く。
        ///
        /// どれも当たりは持たせない。歩く線の真上を通るので、少しでも高さを持つと
        /// 足が引っかかったように見える。区画が 5 つ並ぶこと自体が、
        /// ここが自分ひとりの車庫ではなく共用だという説明になっている
        /// </summary>
        static void Bays(Transform parent)
        {
            var paint = new Bank { Texel = 0.5f };
            // 前の列（1〜5）と後ろの列（6〜10）。どちらも通路から入るので、
            // 止め線は奥（前の列は +z、後ろの列は -z）、番号は通路に面した口に描く
            Row(paint, BayZ, 1, 1f);
            Row(paint, BackBayZ, 6, -1f);
            paint.Emit(parent, "BayPaint", Mat("BayPaint"), false, Generated);

            // 油染み。**「水たまりに見える」と言われた所。**
            //
            // 落とす場所を「床が空いているところ」から「車から油が落ちるところ」へ
            // 変えてある。停まっている車の機関の下、空いた区画の真ん中、通路で
            // 何度も切り返す所。油は車の下に落ちるものなので、そこにしか無い。
            // 絵と形は Stain が持つ
            var oil = new Bank { Texel = 1f };
            // 空いている 4 番の区画の真ん中。**歩く線が横切る。**
            // 車の居ない区画に油だけが残っているのが、共用の車庫のいちばんの説明になる
            Stain(oil, BayWide, BayZ + 0.35f, 0.62f, 7311);
            // 自分の車の右前輪の脇。ドアへ寄る途中で足元に来る
            Stain(oil, 1.38f, AxleFrontZ + 0.10f, 0.34f, 4127);
            // 通路。後ろの列の車が切り返す所
            Stain(oil, -2.55f, AisleFrom - 1.05f, 0.50f, 9043);
            // 空いている 10 番の区画
            Stain(oil, BayWide * 2f - 0.15f, BackBayZ - 0.20f, 0.56f, 5181);
            // 覆いを掛けた車の手前。布の下からいまも垂れている
            Stain(oil, CoveredBayX + 0.24f, BayZ + 2.55f, 0.38f, 2609);
            oil.Emit(parent, "OilStains", Mat("OilStain"), false, Generated);

            // 排水口。歩く線がちょうど踏む場所に置く。足の下を過ぎていくのが分かる
            var pan = new Bank { Texel = 0.5f };
            var grate = new Bank { Texel = 1.0f };
            Drain(pan, grate, 3.45f, -5.20f);
            pan.Emit(parent, "DrainPan", Mat("Drain"), false, Generated);
            grate.Emit(parent, "DrainGrate", Mat("Metal"), false, Generated);
        }

        /// <summary>
        /// 区画の列 1 つ。仕切りの線・突き当たりの止め線・口に描いた番号。
        /// deep が +1 なら鼻を +z へ向けて停める列で、止め線は +z 側、番号は -z 側に来る
        /// </summary>
        static void Row(Bank paint, float z, int first, float deep)
        {
            // 仕切り。内側の 4 本だけ引く。外側の 2 区画は壁が境になる
            foreach (var k in new[] { -1.5f, -0.5f, 0.5f, 1.5f })
                Stripe(paint, BayPaintY, k * BayWide, z, BayLine, BayDeep);
            // 突き当たりの止め線。ここまで入れて停める
            Stripe(paint, BayPaintY, GarageAt.x, z + deep * BayDeep * 0.5f, GarageWide - 0.4f, BayLine);
            // 番号。通路に面した口に、通路から読める向きで描く
            for (var i = 0; i < 5; i++)
                Number(paint, (i - 2) * BayWide, z - deep * (BayDeep * 0.5f + 0.75f), first + i, BayPaintY, deep);
        }

        /// <summary>床に貼る帯。上を向いた面 1 枚だけ。裏は誰も見ないので作らない</summary>
        static void Stripe(Bank bank, float y, float x, float z, float wide, float deep)
        {
            bank.FaceY(y, x - wide * 0.5f, x + wide * 0.5f, z - deep * 0.5f, z + deep * 0.5f, 1);
        }

        /// <summary>数字の 7 本の棒の組み合わせ。0 から 9 まで。上から順に a b c d e f g の位</summary>
        static readonly int[] Segments = { 63, 6, 91, 79, 102, 109, 125, 7, 127, 111 };

        /// <summary>
        /// 区画の番号。二桁（10 番）まで。桁を横に並べる。
        /// up が +1 なら字の上が +z を向き、-1 なら裏返る。
        /// 通路の両側で読む向きが逆になるので、列ごとに渡し分ける
        /// </summary>
        static void Number(Bank bank, float x, float z, int n, float y, float up)
        {
            if (n < 10) { Numeral(bank, x, z, n, y, up); return; }
            const float pitch = 0.30f;
            // 左右は字の向きに合わせて入れ替わる。裏返したまま並べると 10 が 01 になる
            Numeral(bank, x - up * pitch, z, n / 10, y, up);
            Numeral(bank, x + up * pitch, z, n % 10, y, up);
        }

        /// <summary>
        /// 床に描く区画の番号 1 桁。字の形（フォント）を 3D に持ち込まずに済むよう、
        /// 7 本の棒で組む。型で抜いた塗りなので、角の丸みが無くても嘘にならない。
        /// up が +1 なら読む向きの上は +z
        /// </summary>
        static void Numeral(Bank bank, float x, float z, int n, float y, float up)
        {
            const float wide = 0.44f;
            const float high = 0.72f;
            const float thick = 0.085f;
            var bits = Segments[Mathf.Clamp(n, 0, 9)];
            if ((bits & 1) != 0) Stripe(bank, y, x, z + up * high * 0.5f, wide, thick);
            if ((bits & 64) != 0) Stripe(bank, y, x, z, wide, thick);
            if ((bits & 8) != 0) Stripe(bank, y, x, z - up * high * 0.5f, wide, thick);
            if ((bits & 32) != 0) Stripe(bank, y, x - up * wide * 0.5f, z + up * high * 0.25f, thick, high * 0.5f);
            if ((bits & 2) != 0) Stripe(bank, y, x + up * wide * 0.5f, z + up * high * 0.25f, thick, high * 0.5f);
            if ((bits & 16) != 0) Stripe(bank, y, x - up * wide * 0.5f, z - up * high * 0.25f, thick, high * 0.5f);
            if ((bits & 4) != 0) Stripe(bank, y, x + up * wide * 0.5f, z - up * high * 0.25f, thick, high * 0.5f);
        }

        /// <summary>
        /// 油染み 1 つ。
        ///
        /// **これまでは輪郭を崩した扇に、ほぼ真っ黒（0.030）を一様に塗っていた。**
        /// 実画面では床に開いた穴か、でなければ水たまりにしか見えない。
        /// オーナーの第一声が「あの黒いのは水たまり？」で、そのとおりだった。
        /// 濃さが均一で、縁が切り立っていて、色が無い。水の見え方そのものになっている。
        ///
        /// 直したのは絵の方（`tools/make-drive.py` の `oilstain`）で、薄膜の虹・
        /// 染みてぼけた縁・焦茶の芯の三つを持たせてある。ここで変えたのは uv の振り方。
        ///
        /// <see cref="Bank.FanY"/> は uv を世界の位置から振るので、絵を貼ると
        /// 染みごとに絵のどこが出るか分からず、芯も縁も揃わない。
        /// <see cref="Bank.Patch"/> で 0〜1 の uv をじかに渡し、絵 1 枚をそのまま 1 つの
        /// 染みに写す。大きさを変えても芯と輪と虹の割合は変わらない。
        ///
        /// 判子を押したように揃わないよう、種ごとに uv を 90 度ずつ回して裏返す。
        /// 縁の崩れも同じ種から引くので、形と絵の崩れがそろって動く
        /// </summary>
        static void Stain(Bank bank, float x, float z, float span, int seed)
        {
            const int n = 16;
            var rnd = new System.Random(seed);
            // 縁の最大の伸び。uv を畳む物差しになるので、実際に使う伸びより少し広く取る
            const float reach = 1.32f;
            var rim = new Vector2[n];
            for (var i = 0; i < n; i++)
            {
                var a = Mathf.PI * 2f * i / n;
                var r = span * (0.72f + (float)rnd.NextDouble() * 0.52f);
                rim[i] = new Vector2(x + Mathf.Cos(a) * r, z + Mathf.Sin(a) * r * 1.18f);
            }
            var turn = rnd.Next(4) * 90f * Mathf.Deg2Rad;
            var flip = rnd.Next(2) == 0 ? 1f : -1f;
            System.Func<Vector2, Vector2> fold = at =>
            {
                var dx = (at.x - x) / (span * reach);
                var dz = (at.y - z) / (span * reach * 1.18f);
                var c = Mathf.Cos(turn);
                var s = Mathf.Sin(turn);
                return new Vector2(0.5f + (dx * c - dz * s) * 0.5f * flip,
                    0.5f + (dx * s + dz * c) * 0.5f);
            };
            var mid = new Vector3(x, StainY, z);
            var uc = new Vector2(0.5f, 0.5f);
            // 扇に張る。Patch は a→b→c と a→c→d を出すので、縁を 2 つずつ渡せば
            // 面積の無い三角を作らずに扇になる（<see cref="Disc"/> と同じ手）。
            // **縁は逆回りに渡す。** FanY が (中心, 縁 i+1, 縁 i) の順で上を向かせているので、
            // 素直に i → i+2 の順で渡すと面が下を向いて、床から何も見えなくなる
            for (var i = 0; i < n; i += 2)
            {
                var b = rim[i];
                var c = rim[(i + 1) % n];
                var d = rim[(i + 2) % n];
                bank.Patch(mid, new Vector3(d.x, StainY, d.y), new Vector3(c.x, StainY, c.y),
                    new Vector3(b.x, StainY, b.y), uc, fold(d), fold(c), fold(b));
            }
        }

        /// <summary>
        /// 排水口。床を抜く代わりに、暗い受けの上へ格子を並べて開いているように見せる。
        /// 抜いてしまうと下に何も無いので、歩いて覗き込まれたときに床の裏が見える
        /// </summary>
        static void Drain(Bank pan, Bank grate, float x, float z)
        {
            const float side = 0.52f;
            Stripe(pan, DrainY, x, z, side, side);
            // 枠。受けの縁を隠す
            for (var s = 0; s < 2; s++)
            {
                var away = s == 0 ? -1f : 1f;
                grate.Box(new Vector3(x + away * side * 0.5f, GrateY, z), new Vector3(0.06f, 0.012f, side));
                grate.Box(new Vector3(x, GrateY, z + away * side * 0.5f), new Vector3(side, 0.012f, 0.06f));
            }
            for (var i = -2; i <= 2; i++)
                grate.Box(new Vector3(x, GrateY, z + i * 0.093f), new Vector3(side - 0.10f, 0.012f, 0.035f));
        }

        /// <summary>
        /// 隣の区画の、覆いを掛けたままの車。共用のガレージだと一目で分かるものがこれ。
        /// 形は覆いの下の塊だけで、車そのものは作らない。
        /// 歩く線は x 1.3〜4.2 を通るので、左隣の区画に置けば道を塞がない
        /// </summary>
        static void Covered(Transform parent)
        {
            var tarp = new Bank { Texel = 0.8f };
            var x = CoveredBayX;
            var z = BayZ;
            // 下から順に細くしていく。角の立った箱を重ねると、布を掛けた丸みに近づく
            tarp.Box(new Vector3(x, 0.26f, z), new Vector3(1.80f, 0.44f, 4.10f));
            tarp.Box(new Vector3(x, 0.60f, z - 0.05f), new Vector3(1.70f, 0.30f, 3.90f));
            tarp.Box(new Vector3(x, 0.86f, z - 0.30f), new Vector3(1.48f, 0.26f, 2.30f));
            tarp.Box(new Vector3(x, 1.02f, z - 0.35f), new Vector3(1.20f, 0.12f, 1.80f));
            // 掛けた布の皺。上を横切る紐のあたりが盛り上がる
            foreach (var at in new[] { -1.35f, 0.20f, 1.55f })
                tarp.Box(new Vector3(x, 0.52f, z + at), new Vector3(1.86f, 0.52f, 0.09f),
                    Quaternion.Euler(0f, 0f, 2.5f));
            // 当たりを入れる。壁と同じで、すり抜けられては困る。
            // Garage の下にあるので、乗り込んだ瞬間にこれも一緒に消える
            tarp.Emit(parent, "CoveredCar", Mat("Tarp"), true, Generated);
        }

        // ---- 隣の車 ----------------------------------------------------------

        /// <summary>
        /// 隣に停めてある車 1 台の寸法。長さはすべて車の中心から測る。
        /// 高さだけは床からの絶対値で、区画をまたいでも地面の高さは変わらない
        /// </summary>
        struct Motor
        {
            public float bayX;              // 区画の中心の x
            public float rowZ;              // 列の中心の z
            public float face;              // +1 なら鼻が +z。前の列が +1、後ろの列が -1
            public int paint;               // 塗りの番号
            public float half;              // 全幅の半分
            public float nose, tail;        // 鼻先と尻
            public float sill, belt, roof;  // 車体の下端・腰の線・屋根
            public float hood;              // ボンネットの天板
            public float cabBack, cabNose;  // 客室の後ろと前
            public float axleF, axleR;      // 車軸
            public float tyre;              // 車輪の外径の半分
            public bool box;                // 尻が箱（ワゴン・バン）。false なら段（セダン）
        }

        /// <summary>
        /// 共用のガレージに停まっている、自分以外の車。
        ///
        /// 区画は 10 あって、3 番が自分の車、2 番が覆いを掛けた車。
        /// ここで組むのは 1・5・6・8・9 の 5 台で、4・7・10 は空けておく。
        /// **空の区画が要る。** 全部埋めると駐車場に見えて、共同住宅の車庫に見えない。
        /// 4 番は歩く線が横切るので、そもそも埋められない。
        ///
        /// 形は 4 種。背の高い商用車、古い箱型のセダン、小さな二箱の車、荷台のある四輪駆動。
        /// どれも <see cref="Park"/> が同じ作りで組み、寸法の違いだけで別の車に見せる。
        /// 自分の車ほど作り込まない。隣の車は遠目にしか見ないので、
        /// 車輪の止めねじも蝶番も取っ手も持たせていない
        /// </summary>
        static readonly Motor[] Stalls =
        {
            // 1 番。背の高い商用車。屋根が高いので、いちばん左が壁のように立つ
            new Motor { bayX = -BayWide * 2f, rowZ = BayZ, face = 1f, paint = 3,
                half = 0.90f, nose = 2.24f, tail = -2.24f, sill = 0.48f, belt = 1.34f, roof = 2.18f,
                hood = 1.06f, cabBack = 0.30f, cabNose = 1.62f, axleF = 1.42f, axleR = -1.28f,
                tyre = 0.33f, box = true },
            // 5 番。古い箱型のセダン。歩く線の右手にあたる
            new Motor { bayX = BayWide * 2f, rowZ = BayZ, face = 1f, paint = 1,
                half = 0.84f, nose = 2.20f, tail = -2.20f, sill = 0.34f, belt = 1.06f, roof = 1.46f,
                hood = 1.02f, cabBack = -0.44f, cabNose = 0.72f, axleF = 1.34f, axleR = -1.30f,
                tyre = 0.30f, box = false },
            // 6 番。小さな二箱の車。後ろの列のいちばん左
            new Motor { bayX = -BayWide * 2f, rowZ = BackBayZ, face = -1f, paint = 0,
                half = 0.79f, nose = 1.86f, tail = -1.86f, sill = 0.32f, belt = 1.02f, roof = 1.48f,
                hood = 0.96f, cabBack = -1.12f, cabNose = 0.50f, axleF = 1.18f, axleR = -1.14f,
                tyre = 0.28f, box = true },
            // 8 番。もう一台のセダン。真後ろに来るので、立ち位置から真っ先に目に入る
            new Motor { bayX = 0f, rowZ = BackBayZ, face = -1f, paint = 2,
                half = 0.86f, nose = 2.26f, tail = -2.26f, sill = 0.36f, belt = 1.08f, roof = 1.50f,
                hood = 1.04f, cabBack = -0.52f, cabNose = 0.76f, axleF = 1.38f, axleR = -1.34f,
                tyre = 0.31f, box = false },
            // 9 番。荷台のある四輪駆動。自分の車と同じ背丈なので、並べて比べられる
            new Motor { bayX = BayWide, rowZ = BackBayZ, face = -1f, paint = 1,
                half = 0.92f, nose = 2.30f, tail = -2.30f, sill = 0.58f, belt = 1.30f, roof = 1.88f,
                hood = 1.22f, cabBack = -0.30f, cabNose = 0.88f, axleF = 1.50f, axleR = -1.12f,
                tyre = 0.36f, box = false },
        };

        static void Neighbours(Transform parent)
        {
            var group = Child(parent, "Neighbours");
            var paints = new Bank[PaintNames.Length];
            for (var i = 0; i < paints.Length; i++) paints[i] = new Bank { Texel = 1.1f };
            var steel = new Bank { Texel = 2.2f };
            var dark = new Bank { Texel = 1.0f };
            var glass = new Bank { Texel = 0.8f };
            var tyre = new Bank { Texel = 3.0f };
            var lens = new Bank { Texel = 1.0f };
            var tail = new Bank { Texel = 1.0f };

            for (var i = 0; i < Stalls.Length; i++)
                Park(group, paints, steel, dark, glass, tyre, lens, tail, Stalls[i], i);

            // 塗りは 1 枚の絵を色で塗り分ける。車ごとに絵を焼くと絵だけで 5 枚増える
            for (var i = 0; i < paints.Length; i++)
                paints[i].Emit(group, "Paint" + PaintNames[i], Paint(i), false, Generated);
            steel.Emit(group, "NeighbourSteel", Mat("CarSteel"), false, Generated);
            dark.Emit(group, "NeighbourGap", Mat("CarGap"), false, Generated);
            glass.Emit(group, "NeighbourGlass", Mat("CarGlass"), false, Generated);
            tyre.Emit(group, "NeighbourTyre", Mat("CarTyre"), false, Generated);
            lens.Emit(group, "NeighbourLamp", Mat("CarLamp"), false, Generated);
            tail.Emit(group, "NeighbourTail", Mat("CarTail"), false, Generated);
        }

        /// <summary>
        /// 車 1 台。自分の車と同じ語彙で組む。平らな板と角の立った箱だけで、曲面は使わない。
        ///
        /// **当たりは mesh ではなく箱で持たせる。** 塗りの入れ物は何台ぶんも一つの mesh に
        /// まとまっているので、MeshCollider を貼ると台ごとに切れない。車体を包む箱を
        /// 1 台に 1 つ置けば足りるし、非凸の MeshCollider より軽い。
        /// 箱は Garage の下にあるので、乗り込んだ瞬間に当たりごと消える
        /// </summary>
        static void Park(Transform group, Bank[] paints, Bank steel, Bank dark, Bank glass,
            Bank tyre, Bank lens, Bank tail, Motor m, int which)
        {
            var body = paints[m.paint];
            const float skin = 0.045f;
            // 鼻先を止め線の 0.2 手前に置く。区画の奥行きは車より長いので、尻に余りが出る
            var cz = m.rowZ + m.face * (BayDeep * 0.5f - 0.20f) - m.face * m.nose;
            var hubY = GarageFloorY + m.tyre;
            var arch = m.tyre + 0.10f;
            var outer = m.half - skin * 0.5f;

            // 局所の座標を世界へ。z は鼻の向きで裏返る
            System.Action<Bank, float, float, float, float, float, float> slot =
                (bank, ax, bx, y0, y1, z0, z1) =>
                {
                    if (y1 <= y0) return;
                    bank.Box(new Vector3(m.bayX + (ax + bx) * 0.5f, (y0 + y1) * 0.5f,
                            cz + m.face * (z0 + z1) * 0.5f),
                        new Vector3(Mathf.Abs(bx - ax), y1 - y0, Mathf.Abs(z1 - z0)));
                };

            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                var a = side * (outer - skin * 0.5f);
                var b = side * (outer + skin * 0.5f);
                // 側面。泥除けの抜きを避けて三つに割り、抜きの上だけ低く始める
                slot(body, a, b, m.sill, m.belt, m.tail, m.axleR - arch);
                slot(body, a, b, m.sill, m.belt, m.axleR + arch, m.axleF - arch);
                slot(body, a, b, m.sill, m.belt, m.axleF + arch, m.nose);
                slot(body, a, b, hubY + arch, m.belt, m.axleR - arch, m.axleR + arch);
                slot(body, a, b, hubY + arch, m.belt, m.axleF - arch, m.axleF + arch);
                // 窓の枠。下枠と屋根の縁、前後の柱。あいだを抜いてガラスを張る
                var backTo = m.box ? m.tail : m.cabBack;
                slot(body, a, b, m.belt, m.belt + 0.055f, backTo, m.cabNose);
                slot(body, a, b, m.roof - 0.065f, m.roof, backTo, m.cabNose);
                slot(body, a, b, m.belt, m.roof, m.cabNose - 0.085f, m.cabNose);
                slot(body, a, b, m.belt, m.roof, backTo, backTo + 0.085f);
                if (m.box) slot(body, a, b, m.belt, m.roof, m.cabBack, m.cabBack + 0.070f);
                // ガラス。外を向いた面 1 枚ずつ。車内は誰も見ない
                glass.FaceX(m.bayX + side * (m.half - 0.014f),
                    Mathf.Min(cz + m.face * (backTo + 0.085f), cz + m.face * (m.cabNose - 0.085f)),
                    Mathf.Max(cz + m.face * (backTo + 0.085f), cz + m.face * (m.cabNose - 0.085f)),
                    m.belt + 0.055f, m.roof - 0.065f, s == 0 ? -1 : 1);
                // 鏡
                body.Box(new Vector3(m.bayX + side * (m.half + 0.055f), m.belt + 0.10f,
                        cz + m.face * (m.cabNose - 0.10f)),
                    new Vector3(0.090f, 0.110f, 0.040f));

                // 車輪。止めねじは省く。遠目にしか見ないので、皿と窪みだけで足りる
                foreach (var z in new[] { m.axleF, m.axleR })
                {
                    var at = new Vector3(m.bayX + side * (m.half - 0.10f), hubY, cz + m.face * z);
                    Hoop(tyre, at, Vector3.right, m.tyre - 0.050f, 0.215f, 0.100f, 10, 0f, 360f);
                    var out4 = at + new Vector3(side * 0.070f, 0f, 0f);
                    Disc(steel, out4, Vector3.right * side, m.tyre * 0.62f, 8);
                    Disc(dark, out4 + new Vector3(side * 0.004f, 0f, 0f), Vector3.right * side, m.tyre * 0.22f, 6);
                    // 抜きの奥の暗がり
                    Hoop(dark, at - new Vector3(side * 0.030f, 0f, 0f), Vector3.right,
                        arch - 0.02f, 0.130f, 0.040f, 6, -82f, 164f);
                }
            }

            // 屋根と、ボンネットと、尻
            var roofBack = m.box ? m.tail : m.cabBack;
            slot(body, -outer, outer, m.roof - 0.055f, m.roof, roofBack, m.cabNose);
            slot(body, -outer, outer, m.hood - 0.055f, m.hood, m.cabNose, m.nose);
            if (m.box)
            {
                // 尻は立てた一枚。荷室の窓と扉
                slot(body, -outer, outer, m.sill, m.belt, m.tail, m.tail + 0.055f);
                slot(body, -outer, outer, m.roof - 0.065f, m.roof, m.tail, m.tail + 0.055f);
                glass.FaceZ(cz + m.face * (m.tail - 0.008f), m.bayX - outer + 0.09f, m.bayX + outer - 0.09f,
                    m.belt + 0.055f, m.roof - 0.065f, m.face > 0f ? -1 : 1);
            }
            else
            {
                // 段。荷室の蓋と、立てた尻の板
                slot(body, -outer, outer, m.hood - 0.055f, m.hood, m.tail, m.cabBack);
                slot(body, -outer, outer, m.sill, m.hood, m.tail, m.tail + 0.055f);
                // 後ろのガラス。客室の後ろを寝かせて塞ぐ
                glass.FaceZ(cz + m.face * (m.cabBack + 0.012f), m.bayX - outer + 0.10f, m.bayX + outer - 0.10f,
                    m.belt + 0.055f, m.roof - 0.065f, m.face > 0f ? -1 : 1);
            }
            // 風防
            glass.FaceZ(cz + m.face * (m.cabNose - 0.012f), m.bayX - outer + 0.09f, m.bayX + outer - 0.09f,
                m.belt + 0.055f, m.roof - 0.065f, m.face > 0f ? 1 : -1);
            // 前の顔。格子と灯り
            slot(dark, -outer + 0.14f, outer - 0.14f, m.hood - 0.30f, m.hood - 0.09f, m.nose, m.nose + 0.030f);
            slot(body, -outer, outer, m.sill, m.hood - 0.30f, m.nose, m.nose + 0.040f);
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                var lx = side * (outer - 0.16f);
                slot(lens, lx - 0.13f, lx + 0.13f, m.hood - 0.28f, m.hood - 0.11f, m.nose + 0.020f, m.nose + 0.050f);
                slot(tail, lx - 0.12f, lx + 0.12f, m.sill + 0.24f, m.sill + 0.44f, m.tail - 0.030f, m.tail);
            }
            // 番号板。前後に一枚ずつ
            slot(lens, -0.20f, 0.20f, m.sill + 0.10f, m.sill + 0.21f, m.nose + 0.030f, m.nose + 0.050f);
            slot(lens, -0.20f, 0.20f, m.sill + 0.08f, m.sill + 0.19f, m.tail - 0.050f, m.tail - 0.030f);
            // 緩衝器。前後
            slot(steel, -m.half, m.half, m.sill - 0.10f, m.sill + 0.04f, m.nose + 0.020f, m.nose + 0.130f);
            slot(steel, -m.half, m.half, m.sill - 0.10f, m.sill + 0.04f, m.tail - 0.130f, m.tail - 0.020f);
            // 車台の暗がり。無いと脇から車の下が見通せて、車体が浮く
            slot(dark, -outer + 0.20f, outer - 0.20f, m.sill - 0.22f, m.sill + 0.01f, m.tail + 0.30f, m.nose - 0.30f);

            // 当たり。車体を包む箱ひとつ
            var stop = new GameObject("Block" + (which + 1));
            stop.transform.SetParent(group, false);
            var box2 = stop.AddComponent<BoxCollider>();
            box2.center = new Vector3(m.bayX, (GarageFloorY + m.roof) * 0.5f, cz + m.face * (m.tail + m.nose) * 0.5f);
            box2.size = new Vector3(m.half * 2f + 0.14f, m.roof - GarageFloorY, m.nose - m.tail + 0.26f);
        }

        // ---- シャッターの操作盤と感知器 ----------------------------------------

        /// <summary>
        /// シャッターの押しボタンと感知器。
        ///
        /// **どちらも調べる対象にはしない。** この場面で必須なのは garage.door と
        /// drive.window の 2 つだけで、必須をもう 1 つ足すと最後の帯で場面が閉じられない
        /// （<see cref="Items"/> の但し書き）。必須でない対象を足しても、乗り込む前に
        /// 拾える物が増えて、ドアを調べる前に別の物へ手が伸びる。
        /// ここで置くのは、共用の車庫にあるべき物として立っているだけの造作。
        ///
        /// 三つで一組にする。押しボタンの箱・上げ下げの巻き取り機・足元の光電管。
        /// どれか一つだけだと壁に付いた謎の箱にしか見えないが、
        /// 三つが電線で繋がっていると「シャッターの仕掛け」として読める
        /// </summary>
        static void Controls(Transform parent)
        {
            var metal = new Bank { Texel = 1.4f };
            var dark = new Bank { Texel = 1.0f };
            var knob = new Bank { Texel = 2.2f };
            // 前の壁の内側の面。壁は厚み 0.25 の真ん中が front に来る
            var wall = GarageAt.z + GarageDeep * 0.5f;
            var jamb = ShutterWide * 0.5f;

            // 押しボタンの箱。シャッターの右の柱側。立って手の届く高さ
            var boxX = jamb + 0.45f;
            metal.Box(new Vector3(boxX, 1.24f, wall - 0.055f), new Vector3(0.20f, 0.30f, 0.110f));
            dark.Box(new Vector3(boxX, 1.24f, wall - 0.113f), new Vector3(0.155f, 0.245f, 0.010f));
            // 上げる・止める・下げるの三つ。押しボタンなので座から少し出る
            for (var i = 0; i < 3; i++)
                knob.Box(new Vector3(boxX, 1.325f - i * 0.085f, wall - 0.126f),
                    new Vector3(0.062f, 0.050f, 0.028f));
            // 通電を示す小さな灯り。**ガレージで唯一、自分で光っている物。**
            // 天井の灯りが届かないシャッターの際に、一点だけ緑が点いている
            var pilot = Piece(Child(parent, "ShutterPilot"), "Lamp",
                Shape("PilotLamp", 1f, b => b.Box(Vector3.zero, new Vector3(0.030f, 0.030f, 0.014f))),
                Glow(new Color(0.42f, 0.95f, 0.50f), 1.9f));
            pilot.position = new Vector3(boxX, 1.398f, wall - 0.122f);
            // 電線の管。箱から天井へ立ち上げ、シャッターの上を横へ渡す
            metal.Box(new Vector3(boxX, (1.39f + GarageHigh) * 0.5f, wall - 0.035f),
                new Vector3(0.045f, GarageHigh - 1.39f, 0.045f));
            metal.Box(new Vector3((boxX + 0f) * 0.5f, GarageHigh - 0.075f, wall - 0.035f),
                new Vector3(boxX + 0.045f, 0.045f, 0.045f));

            // 巻き取り機。シャッターの上のまぐさに渡す。これが無いと、
            // 板が勝手に上がる仕掛けになる
            metal.Box(new Vector3(0f, GarageHigh - 0.28f, wall - 0.175f),
                new Vector3(ShutterWide - 0.30f, 0.240f, 0.240f));
            metal.Box(new Vector3(-ShutterWide * 0.5f + 0.42f, GarageHigh - 0.28f, wall - 0.255f),
                new Vector3(0.300f, 0.300f, 0.180f));
            for (var s = 0; s < 2; s++)
                metal.Box(new Vector3((s == 0 ? -1f : 1f) * (ShutterWide * 0.5f - 0.06f), GarageHigh - 0.28f, wall - 0.175f),
                    new Vector3(0.100f, 0.320f, 0.320f));

            // 足元の光電管。シャッターの両脇に向かい合わせで一対。
            // 一つでは「箱」だが、向かい合って二つあると光の線を渡しているのが読める
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                var x = side * (jamb - 0.10f);
                metal.Box(new Vector3(x, 0.30f, wall - 0.110f), new Vector3(0.090f, 0.170f, 0.100f));
                metal.Box(new Vector3(x, 0.14f, wall - 0.110f), new Vector3(0.055f, 0.160f, 0.055f));
                // 目。向かい合う側を向く。赤い小さな面
                dark.Box(new Vector3(x - side * 0.048f, 0.30f, wall - 0.110f),
                    new Vector3(0.010f, 0.100f, 0.060f));
                var eye = Piece(Child(parent, "ShutterEye" + s), "Lens",
                    Shape("EyeLens", 1f, b => b.Box(Vector3.zero, new Vector3(0.010f, 0.048f, 0.030f))),
                    Glow(new Color(0.95f, 0.30f, 0.26f), 1.4f));
                eye.position = new Vector3(x - side * 0.055f, 0.30f, wall - 0.110f);
                // 管。柱に沿って上へ逃がす
                metal.Box(new Vector3(x, 1.00f, wall - 0.035f), new Vector3(0.040f, 1.24f, 0.040f));
            }

            metal.Emit(parent, "ShutterGear", Mat("Metal"), false, Generated);
            dark.Emit(parent, "ShutterFace", Mat("CarGap"), false, Generated);
            knob.Emit(parent, "ShutterButtons", Mat("CarSteel"), false, Generated);
        }

        // ---- 奥の壁際 --------------------------------------------------------

        /// <summary>
        /// 後ろの列の先、奥の壁までの 1.4 m。
        ///
        /// 区画を 2 列にして奥行きを 21 m へ伸ばしたぶん、奥の壁がまるごと空いた。
        /// 何も置かないと、灯りの届かない奥にただの灰色の板が立っているだけになる。
        /// 共用の車庫の奥に置いてある物を並べる。棚と、油の缶と、積んだ古い車輪
        /// </summary>
        static void Racks(Transform parent)
        {
            var metal = new Bank { Texel = 1.4f };
            var dark = new Bank { Texel = 1.0f };
            var tyre = new Bank { Texel = 3.0f };
            var wall = GarageAt.z - GarageDeep * 0.5f;

            // 棚。支柱 4 本と棚板 3 枚
            var from = -4.6f;
            var to = -0.9f;
            for (var i = 0; i < 4; i++)
            {
                var x = Mathf.Lerp(from, to, i / 3f);
                metal.Box(new Vector3(x, 1.02f, wall + 0.28f), new Vector3(0.055f, 1.96f, 0.055f));
                metal.Box(new Vector3(x, 1.02f, wall + 0.74f), new Vector3(0.055f, 1.96f, 0.055f));
            }
            foreach (var y in new[] { 0.42f, 1.06f, 1.70f })
                metal.Box(new Vector3((from + to) * 0.5f, y, wall + 0.51f),
                    new Vector3(to - from + 0.10f, 0.035f, 0.500f));
            // 棚に載っている物。箱を無造作に。大きさをばらけさせるだけで散らかって見える
            var rnd = new System.Random(4421);
            for (var i = 0; i < 11; i++)
            {
                var x = Mathf.Lerp(from + 0.2f, to - 0.2f, (float)rnd.NextDouble());
                var shelf = new[] { 0.42f, 1.06f, 1.70f }[rnd.Next(3)];
                var w = 0.18f + (float)rnd.NextDouble() * 0.22f;
                var h = 0.14f + (float)rnd.NextDouble() * 0.20f;
                dark.Box(new Vector3(x, shelf + 0.018f + h * 0.5f, wall + 0.51f + (float)(rnd.NextDouble() - 0.5) * 0.12f),
                    new Vector3(w, h, 0.26f + (float)rnd.NextDouble() * 0.12f),
                    Quaternion.Euler(0f, (float)(rnd.NextDouble() - 0.5) * 22f, 0f));
            }

            // 油の缶。二つ並べて立てる
            for (var i = 0; i < 2; i++)
            {
                var at = new Vector3(2.9f + i * 0.72f, 0.0f, wall + 0.44f + i * 0.10f);
                Hoop(metal, at + new Vector3(0f, GarageFloorY + 0.44f, 0f), Vector3.up,
                    0.285f, 0.880f, 0.040f, 10, 0f, 360f);
                Disc(metal, at + new Vector3(0f, GarageFloorY + 0.880f, 0f), Vector3.up, 0.300f, 10);
                // 蓋の縁。二本の輪が缶らしさを作る
                foreach (var y in new[] { 0.26f, 0.62f })
                    Hoop(metal, at + new Vector3(0f, GarageFloorY + y, 0f), Vector3.up,
                        0.300f, 0.050f, 0.030f, 10, 0f, 360f);
            }

            // 積んだ古い車輪。四つ重ねる
            for (var i = 0; i < 4; i++)
            {
                var at = new Vector3(5.3f, GarageFloorY + 0.10f + i * 0.20f, wall + 0.62f);
                Hoop(tyre, at, Vector3.up, 0.305f, 0.195f, 0.115f, 10, i * 9f, 360f);
                if (i == 3) Disc(dark, at + new Vector3(0f, 0.098f, 0f), Vector3.up, 0.245f, 8);
            }

            metal.Emit(parent, "Racks", Mat("Metal"), true, Generated);
            dark.Emit(parent, "RackBoxes", Mat("CarGap"), true, Generated);
            tyre.Emit(parent, "SpareTyres", Mat("CarTyre"), true, Generated);
        }

        /// <summary>
        /// 車体を塞ぐ箱。見えないので絵は持たせない。
        ///
        /// 車ではなくガレージの下に置く。<see cref="DriveDirector"/> は乗り込んだ瞬間に
        /// Garage を丸ごと伏せるので、この当たりもそこで消える。車の下に置くと、
        /// 座ったあとの CharacterController が生きた当たりの中に座ることになる。
        /// 場面 1 の SceneFlow.chairBlocker と同じ考え方
        /// </summary>
        static void Blocker(Transform parent)
        {
            var go = new GameObject("Blocker");
            go.transform.SetParent(parent, false);
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, (GarageFloorY + BlockTop) * 0.5f, (BlockBack + BlockFront) * 0.5f);
            box.size = new Vector3(BlockHalfX * 2f, BlockTop - GarageFloorY, BlockFront - BlockBack);
        }

        /// <summary>
        /// 天井の灯り。**区画の列に合わせて 2 列。**
        ///
        /// 1 列のときは (±3.0, 2.80, -2.00) に 2 本だけで、奥行き 16 m のうち
        /// 真ん中しか照らしていなかった。21 m に伸ばして列を 2 つにした以上、
        /// 灯りも 2 列に増やさないと後ろの列が丸ごと闇に沈む。
        ///
        /// **列の z は区画の真ん中ではなく、通路の両端に置く。** 区画の真ん中に吊ると、
        /// 灯りの真下に車の屋根が来て、光がそこで止まる。通路の縁に吊れば、
        /// 同じ灯りが車の前半分と通路の両方を照らす。実際の車庫もそう吊ってある。
        ///
        /// 管は x の向きに寝かせる。列が横一線に並んで、二つの列だと一目で分かる。
        /// 影を落とさせないのは、WebGL で影を持つ灯りを増やすと重くなるため
        /// </summary>
        static void Lamps(Transform parent)
        {
            var tube = Shape("GarageTube", 0.5f, b => b.Box(Vector3.zero, new Vector3(2.40f, 0.08f, 0.18f)));
            var lit = Glow(GarageLamp, 1.6f);
            var rows = new[] { AisleTo + 1.30f, AisleFrom - 1.30f };
            var across = new[] { -4.6f, 0f, 4.6f };
            for (var r = 0; r < rows.Length; r++)
                for (var k = 0; k < across.Length; k++)
                {
                    var lamp = Child(parent, "Lamp" + r + k);
                    lamp.localPosition = new Vector3(across[k], 2.8f, rows[r]);
                    Piece(lamp, "Tube", tube, lit);
                    // 吊り具。天井から 10 cm 下げてあるので、繋がっていないと浮いて見える
                    for (var i = 0; i < 2; i++)
                        Piece(lamp, "Hanger" + i,
                            Shape("GarageHanger", 0.5f, b => b.Box(Vector3.zero, new Vector3(0.05f, 0.14f, 0.05f))),
                            Mat("Metal")).localPosition = new Vector3(i == 0 ? -0.9f : 0.9f, 0.10f, 0f);
                    var bulb = new GameObject("Light");
                    bulb.transform.SetParent(lamp, false);
                    var l = bulb.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.color = GarageLamp;
                    // **8.0 から下げた。** 2 本が 6 本になったので、同じ強さのままだと
                    // 重なったところで床が飛ぶ。灯りの真下で床が 52 前後に来るところは
                    // 変えずに、本数のぶんだけ 1 本を弱める
                    l.intensity = 4.6f;
                    l.range = 13f;
                    l.shadows = LightShadows.None;
                }
        }
    }
}
