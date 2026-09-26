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
            Neighbours(parent);
            Controls(parent);
            SideDoor(parent);
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

            // 油溜まり。**濡れて灯りを映す面として置く。**
            //
            // 落とす場所は「床が空いているところ」ではなく「車から油が落ちるところ」。
            // 停まっている車の機関の下、空いた区画の真ん中、通路で何度も切り返す所。
            // 油は車の下に落ちるものなので、そこにしか無い。
            // 輪郭は <see cref="Stain"/>、濃さと薄膜の虹は絵、水気は艶が持つ
            // （<see cref="BuildDrive.Tone"/> の "OilStain"）
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
            // 隣の 2 番の車の鼻先。古い車なので、停めた先から垂れている
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
        /// 床の油溜まり 1 つ。
        ///
        /// **輪郭に角を立てない。** 16 の角それぞれに乱数の伸びを掛けていたので、
        /// 縁が折れ線のまま出て、床に貼った多角形にしか見えなかった。
        /// オーナーの言葉は「オイルだまりは曲線で作るように。角があるとわかりづらい」。
        ///
        /// 形は二段で作る。まず大小の丸をいくつか重ね、角度ごとにいちばん遠い丸の縁を
        /// 採って、重なった形の外側だけを輪郭にする。丸はどれも溜まりの中心を含ませるので、
        /// 輪郭は角度の一価の関数になり、中心からの扇でそのまま張れる。
        /// そのうえから低い波を二つ掛けて縁を崩す。角ごとの乱数と違って、
        /// 波は隣の角と繋がっているので折れ目が出ない。刻みは 64。
        ///
        /// **半透明の面を何枚も重ねて溜まりに見せる手は使わない。** 重なったところだけ
        /// 二度塗りになって濃く出る。重なりは輪郭として一枚の面が持つ。
        ///
        /// uv は縁がちょうど絵の外周へ来るように振る（<see cref="Bank.Patch"/> で
        /// 0〜1 をじかに渡す）。<see cref="Bank.FanY"/> の、世界の位置から振る uv では
        /// 溜まりごとに絵のどこが出るか分からず、芯も縁も揃わない。
        /// 絵の α は外周までに 0 へ落ちているので、面の縁そのものは画面に出ない。
        ///
        /// **逆に、縁より内側で α が残ると面の縁が直に出る。** 角が見えていたもう一つの
        /// 理由がこれで、角ごとに違う伸びを同じ物差しで uv へ写していたため、
        /// 伸びの短い角では絵がまだ濃いところで面が切れていた。角度によらず縁を
        /// 絵の外周へ写せば、どこで切れても縁は透けている。
        ///
        /// 判子を押したように揃わないよう、種ごとに uv を 90 度ずつ回して裏返す
        /// </summary>
        static void Stain(Bank bank, float x, float z, float span, int seed)
        {
            // 縁の刻み。64 なら 5.6 度ごとで、1 m の溜まりでも縁の折れが 3 cm に満たない
            const int n = 64;
            // 絵の外周を uv のどこへ置くか。0.5 より少し外にすると、面の縁では
            // α が完全に 0 になっている。内へ入れると絵の濃いところで面が切れる
            const float edge = 0.51f;
            var rnd = new System.Random(seed);
            // 溜まりを作る丸。芯が 1 つと、垂れて広がった 2〜3 つ。
            // **どれも溜まりの中心を含ませる**（離れ < 半径）。外すと、その丸の接する
            // 角度で輪郭が跳んで、無くしたはずの角が戻る
            var lobes = 3 + rnd.Next(2);
            var away = new Vector2[lobes];
            var wide = new float[lobes];
            away[0] = Vector2.zero;
            wide[0] = span * 0.70f;
            for (var i = 1; i < lobes; i++)
            {
                var a = Mathf.PI * 2f * (i + (float)rnd.NextDouble() - 0.5f) / lobes;
                var far = span * (0.20f + (float)rnd.NextDouble() * 0.18f);
                away[i] = new Vector2(Mathf.Cos(a) * far, Mathf.Sin(a) * far);
                wide[i] = far + span * (0.26f + (float)rnd.NextDouble() * 0.20f);
            }
            var turn = rnd.Next(4) * 90f * Mathf.Deg2Rad;
            var flip = rnd.Next(2) == 0 ? 1f : -1f;
            var wave = (float)rnd.NextDouble() * Mathf.PI * 2f;
            var ripple = (float)rnd.NextDouble() * Mathf.PI * 2f;
            var rim = new Vector3[n];
            var fold = new Vector2[n];
            for (var i = 0; i < n; i++)
            {
                var a = Mathf.PI * 2f * i / n;
                var ux = Mathf.Cos(a);
                var uz = Mathf.Sin(a);
                var r = 0f;
                for (var k = 0; k < lobes; k++)
                {
                    // 中心から引いた線が丸の縁を抜けるところ。遠い方を採る
                    var along = away[k].x * ux + away[k].y * uz;
                    var reach = wide[k] * wide[k] - (away[k].sqrMagnitude - along * along);
                    if (reach <= 0f) continue;
                    var hit = along + Mathf.Sqrt(reach);
                    if (hit > r) r = hit;
                }
                // 低い波で縁を崩す。3 周と 5 周を重ねると、丸のままにも見えず、
                // 角ごとの乱数のように折れもしない
                r *= 1f + 0.085f * Mathf.Sin(a * 3f + wave) + 0.050f * Mathf.Sin(a * 5f + ripple);
                // 溜まりは車の進む向きへ流れる。z へわずかに伸ばす
                rim[i] = new Vector3(x + ux * r, StainY, z + uz * r * 1.14f);
                var c = Mathf.Cos(turn);
                var s = Mathf.Sin(turn);
                fold[i] = new Vector2(0.5f + (ux * c - uz * s) * edge * flip,
                    0.5f + (ux * s + uz * c) * edge);
            }
            var mid = new Vector3(x, StainY, z);
            var uc = new Vector2(0.5f, 0.5f);
            // 扇に張る。Patch は a→b→c と a→c→d を出すので、縁を 2 つずつ渡せば
            // 面積の無い三角を作らずに扇になる（<see cref="Disc"/> と同じ手）。
            // **縁は逆回りに渡す。** FanY が (中心, 縁 i+1, 縁 i) の順で上を向かせているので、
            // 素直に i → i+2 の順で渡すと面が下を向いて、床から何も見えなくなる
            for (var i = 0; i < n; i += 2)
            {
                var b = (i + 2) % n;
                var c = (i + 1) % n;
                bank.Patch(mid, rim[b], rim[c], rim[i], uc, fold[b], fold[c], fold[i]);
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
            public bool near;               // すぐ隣に停まっている。CloseUp が細部を足す
        }

        /// <summary>
        /// 共用のガレージに停まっている、自分以外の車。
        ///
        /// 区画は 10 あって、3 番が自分の車。ここで組むのは 1・2・5・6・8・9 の 6 台で、
        /// 4・7・10 は空けておく。
        /// **空の区画が要る。** 全部埋めると駐車場に見えて、共同住宅の車庫に見えない。
        /// 4 番は歩く線が横切るので、そもそも埋められない。
        ///
        /// **2 番は前まで覆いを掛けた塊で済ませていた。** 布を掛けた車のつもりで
        /// 箱を四段重ねたものだったが、自分の車のすぐ隣にあって、乗り込むまでのあいだ
        /// ずっと横目に入る一台なので、ただの低い箱にしか見えなかった。
        /// ほかの隣と同じ <see cref="Park"/> で、外装のある車として組み直してある。
        ///
        /// 形は 5 種。背の高い商用車、長いエステート、古い箱型のセダン、小さな二箱の車、
        /// 荷台のある四輪駆動。どれも <see cref="Park"/> が同じ作りで組み、
        /// 寸法の違いだけで別の車に見せる。
        /// 遠くの一台は自分の車ほど作り込まない。近くに停まっている 2 番だけ
        /// <see cref="Motor.near"/> を立てて、継ぎ目・取っ手・格子の桟までを足す
        /// </summary>
        static readonly Motor[] Stalls =
        {
            // 1 番。背の高い商用車。屋根が高いので、いちばん左が壁のように立つ
            new Motor { bayX = -BayWide * 2f, rowZ = BayZ, face = 1f, paint = 3,
                half = 0.90f, nose = 2.24f, tail = -2.24f, sill = 0.48f, belt = 1.34f, roof = 2.18f,
                hood = 1.06f, cabBack = 0.30f, cabNose = 1.62f, axleF = 1.42f, axleR = -1.28f,
                tyre = 0.33f, box = true },
            // 2 番。自分の車の左隣。長いエステートで、客室が尻まで続く。
            // **塗りは Cream。** 左は Tan（1 番）、右は自分の車の緑で、どちらとも続かない。
            // 暗いガレージでいちばん明るい車体になるので、灯りの下で形がそのまま読める
            new Motor { bayX = CoveredBayX, rowZ = BayZ, face = 1f, paint = 0,
                half = 0.86f, nose = 2.16f, tail = -2.16f, sill = 0.36f, belt = 1.10f, roof = 1.54f,
                hood = 1.06f, cabBack = -0.62f, cabNose = 0.78f, axleF = 1.32f, axleR = -1.30f,
                tyre = 0.31f, box = true, near = true },
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

            // すぐ隣の一台だけ、近くで見るぶんの細部を足す
            if (m.near) CloseUp(slot, steel, dark, m, cz, hubY, outer);

            // 当たり。車体を包む箱ひとつ
            var stop = new GameObject("Block" + (which + 1));
            stop.transform.SetParent(group, false);
            var box2 = stop.AddComponent<BoxCollider>();
            box2.center = new Vector3(m.bayX, (GarageFloorY + m.roof) * 0.5f, cz + m.face * (m.tail + m.nose) * 0.5f);
            box2.size = new Vector3(m.half * 2f + 0.14f, m.roof - GarageFloorY, m.nose - m.tail + 0.26f);
        }

        /// <summary>
        /// すぐ隣に停まっている一台の仕上げ。窓の向こうの暗がり・扉の継ぎ目・取っ手・
        /// 窓の桟・車輪の止めねじ・格子・前照灯の縁・ワイパー・給油口・排気の先。
        ///
        /// **窓の暗がりがいちばん効く。** ガラスは外を向いた面 1 枚しか張っていないので、
        /// 遠くの一台では気づかないが、近くに寄ると窓が向こう側まで素通しの穴に見える。
        /// 客室の中へ暗い塊を一つ置けば、そこで初めてガラスとして読める。
        ///
        /// 置く場所はどれも <see cref="Motor"/> の寸法から出す。数を直に書かないのは、
        /// 近くに停める車が別の寸法へ変わってもそのまま付いてくるようにするため
        /// </summary>
        static void CloseUp(System.Action<Bank, float, float, float, float, float, float> slot,
            Bank steel, Bank dark, Motor m, float cz, float hubY, float outer)
        {
            var backTo = m.box ? m.tail : m.cabBack;

            // 窓の向こうの暗がり。ガラス（|x| = half - 0.014）より内に収める
            slot(dark, -outer + 0.07f, outer - 0.07f, m.belt - 0.04f, m.roof - 0.075f,
                backTo + 0.10f, m.cabNose - 0.09f);

            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                var inn = side * (m.half - 0.018f);
                var lip = side * (m.half + 0.006f);

                // 扉の継ぎ目。前の扉の前縁・扉と扉のあいだ・後ろの扉の後縁
                foreach (var z in new[] { m.cabNose - 0.03f, 0.02f, m.tail + 0.62f })
                    slot(dark, inn, lip, m.sill + 0.02f, m.belt + 0.05f, z - 0.014f, z + 0.014f);
                // 取っ手。扉ごとに一つ
                foreach (var z in new[] { 0.30f, -0.40f })
                    slot(steel, inn, side * (m.half + 0.028f), m.belt - 0.16f, m.belt - 0.10f,
                        z - 0.085f, z + 0.085f);
                // 鍵穴。前の扉の取っ手の下
                slot(steel, inn, side * (m.half + 0.014f), m.belt - 0.235f, m.belt - 0.195f, 0.335f, 0.375f);
                // 窓の桟。腰の線と屋根の縁に明るい一本ずつ。古い英国の車の顔付きはこれで決まる
                slot(steel, inn, lip, m.belt + 0.050f, m.belt + 0.068f, backTo + 0.04f, m.cabNose - 0.02f);
                slot(steel, side * (m.half - 0.030f), lip, m.roof - 0.020f, m.roof + 0.008f,
                    backTo, m.cabNose - 0.06f);

                // 車輪の止めねじ。五本。輪が回る物だと読めるのはこれがあるから
                foreach (var z in new[] { m.axleF, m.axleR })
                {
                    var at = new Vector3(m.bayX + side * (m.half - 0.022f), hubY, cz + m.face * z);
                    for (var i = 0; i < 5; i++)
                    {
                        var a = (i / 5f) * Mathf.PI * 2f;
                        steel.Box(at + new Vector3(0f, Mathf.Cos(a) * m.tyre * 0.34f,
                            Mathf.Sin(a) * m.tyre * 0.34f), new Vector3(0.030f, 0.032f, 0.032f));
                    }
                }
            }

            // ワイパー。風防（z は cabNose）の前、ボンネットの天板の上に二本寝かせる
            for (var s = 0; s < 2; s++)
            {
                var wx = (s == 0 ? -1f : 1f) * (outer - 0.30f);
                slot(steel, wx - 0.24f, wx + 0.24f, m.hood + 0.005f, m.hood + 0.022f,
                    m.cabNose + 0.020f, m.cabNose + 0.075f);
            }
            // 格子の桟。三本。前照灯のあいだにだけ渡す
            for (var k = 0; k < 3; k++)
                slot(steel, -0.44f, 0.44f, m.hood - 0.270f + k * 0.055f,
                    m.hood - 0.245f + k * 0.055f, m.nose + 0.032f, m.nose + 0.048f);
            // 前照灯の縁。淡い面の周りを金物で囲う。奥の暗がりより前、灯りの面より後ろ
            for (var s = 0; s < 2; s++)
            {
                var lx = (s == 0 ? -1f : 1f) * (outer - 0.16f);
                slot(steel, lx - 0.155f, lx + 0.155f, m.hood - 0.300f, m.hood - 0.090f,
                    m.nose + 0.030f, m.nose + 0.044f);
            }
            // 給油口。左の後ろの翼板に一つだけ
            slot(steel, -(m.half - 0.018f), -(m.half + 0.010f), m.sill + 0.40f, m.sill + 0.56f,
                m.axleR - 0.58f, m.axleR - 0.40f);
            // 排気の先。緩衝器（sill-0.10 から）の下をくぐらせる
            slot(steel, -0.62f, -0.48f, m.sill - 0.22f, m.sill - 0.12f, m.tail - 0.16f, m.tail + 0.24f);
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
        /// 三つが電線で繋がっていると「シャッターの仕掛け」として読める。
        ///
        /// **大きさを上げた。** 箱が 0.20 × 0.30、ボタンが 62 × 50 mm、光電管のレンズに
        /// 至っては 10 × 48 × 30 mm しかなく、歩いてきても壁の斑にしか見えなかった。
        /// 実物の押しボタン箱は 0.30 × 0.45 ほどあり、ボタンは掌で押す大きさをしている。
        /// 光電管も、車の通る口に据える物なので拳ほどの箱に載っている。
        ///
        /// **箱と光電管の筐体は CarSteel で塗る。** Metal（0.085）は壁（0.128）より
        /// 暗いので、暗がりでは壁と一体になって輪郭が出ない。塗った鉄（0.288）は
        /// この場面でいちばん明るい素材で、壁の前に置くと形がそのまま読める。
        /// 露わな配管と巻き取り機だけは Metal のまま残す。全部が明るいと、
        /// どれが触る物なのか分からなくなる
        /// </summary>
        static void Controls(Transform parent)
        {
            var metal = new Bank { Texel = 1.4f };
            var dark = new Bank { Texel = 1.0f };
            var knob = new Bank { Texel = 2.2f };
            var shell = new Bank { Texel = 1.4f };
            // 前の壁の内側の面。壁は厚み 0.25 の真ん中が front に来る
            var wall = GarageAt.z + GarageDeep * 0.5f;
            var jamb = ShutterWide * 0.5f;

            // 押しボタンの箱。シャッターの右の柱側。立って手の届く高さ
            var boxX = jamb + 0.45f;
            const float caseW = 0.34f;
            const float caseH = 0.54f;
            const float caseD = 0.13f;
            const float caseY = 1.24f;
            shell.Box(new Vector3(boxX, caseY, wall - caseD * 0.5f), new Vector3(caseW, caseH, caseD));
            // 庇。天井の灯りを受けて上の縁が光るので、箱の輪郭が壁から切り離される
            shell.Box(new Vector3(boxX, caseY + caseH * 0.5f + 0.022f, wall - 0.095f),
                new Vector3(caseW + 0.07f, 0.040f, 0.190f));
            // 座。ボタンの並ぶ暗い面
            var seat = wall - caseD - 0.006f;
            dark.Box(new Vector3(boxX, caseY - 0.02f, seat), new Vector3(caseW - 0.07f, caseH - 0.12f, 0.012f));
            // 上げる・止める・下げるの三つ。押しボタンなので座から大きく出る。
            // 掌で押す大きさにすると、三つ並んでいること自体が遠目にも読める
            for (var i = 0; i < 3; i++)
                knob.Box(new Vector3(boxX, 1.33f - i * 0.125f, seat - 0.031f),
                    new Vector3(0.105f, 0.085f, 0.050f));
            // 通電を示す灯り。**ガレージで唯一、自分で光っている物。**
            // 歩いてくる先で一点だけ緑が点いていて、そこに何か付いていると分かる。
            // 30 mm 角から 70 mm 角へ。遠くからでも点として消えない大きさが要る
            var pilot = Piece(Child(parent, "ShutterPilot"), "Lamp",
                Shape("PilotLamp", 1f, b => b.Box(Vector3.zero, new Vector3(0.070f, 0.070f, 0.016f))),
                Glow(new Color(0.42f, 0.95f, 0.50f), 2.2f));
            pilot.position = new Vector3(boxX, 1.44f, seat - 0.014f);
            // 灯りの座。緑が壁に直に浮かないよう、暗い面の上へ載せる
            dark.Box(new Vector3(boxX, 1.44f, seat - 0.004f), new Vector3(0.110f, 0.110f, 0.010f));
            // 箱の上の防滴灯。**ボタンを見つけさせているのはこれ。**
            //
            // 天井の灯りは壁から 1.7 m 離れた真上にあるので、壁に当たる光は斜めに薄まる。
            // 箱のところだけ 0.7 m の近さから照らす灯りを足すと、暗い壁の中で
            // そこだけ明るい溜まりになり、歩いてくる先で真っ先に目に入る。
            // 車庫の出入口の脇に灯りが一つ点いているのは、実際にそうなっている造作でもある
            var lampY = 2.20f;
            shell.Box(new Vector3(boxX, lampY + 0.135f, wall - 0.125f), new Vector3(0.320f, 0.045f, 0.250f));
            shell.Box(new Vector3(boxX, lampY, wall - 0.045f), new Vector3(0.280f, 0.230f, 0.090f));
            Piece(Child(parent, "ShutterLamp"), "Pane",
                Shape("DoorLamp", 1f, b => b.Box(Vector3.zero, new Vector3(0.230f, 0.155f, 0.030f))),
                Glow(GarageLamp, 1.6f)).position = new Vector3(boxX, lampY, wall - 0.105f);
            var bulb = new GameObject("Light");
            bulb.transform.SetParent(Child(parent, "ShutterLamp"), false);
            // **壁から 0.45 離す。** 灯りを壁の際に置くと、壁を向いた面（箱の座・ボタンの頭）
            // には光が斜めにしか当たらず、いちばん見せたいボタンが暗いまま残る。
            // 庇の付いた灯りは前へ投げるものなので、光の出どころを前へ出しても嘘にならない
            bulb.transform.localPosition = new Vector3(boxX, lampY - 0.15f, wall - 0.45f);
            var glowLight = bulb.AddComponent<Light>();
            // AddComponent<Light> だけでは URP の付属データが付かない（場面 4 の Lamp と同じ。7fffabc）
            UnityEngine.Rendering.Universal.LightExtensions.GetUniversalAdditionalLightData(glowLight);
            glowLight.type = LightType.Point;
            glowLight.color = GarageLamp;
            // 弱くて近い。強くすると箱の面が飛んで、ボタンの三つが一つの白い塊になる
            glowLight.intensity = 1.7f;
            glowLight.range = 5f;
            glowLight.shadows = LightShadows.None;

            // 電線の管。箱から天井へ立ち上げ、シャッターの上を横へ渡す。
            // 途中で防滴灯を貫くので、灯りへ電気が行っているようにも見える
            var top = caseY + caseH * 0.5f + 0.04f;
            metal.Box(new Vector3(boxX, (top + GarageHigh) * 0.5f, wall - 0.035f),
                new Vector3(0.045f, GarageHigh - top, 0.045f));
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
            // 一つでは「箱」だが、向かい合って二つあると光の線を渡しているのが読める。
            //
            // **床の上の設備として組み直した。** 筐体を拳ほどの箱にして、据え付けの
            // 台座に載せる。目の高さも 0.30 から 0.42 へ上げた。車の口に据える物なので、
            // 実物も膝より下には付いていない
            for (var s = 0; s < 2; s++)
            {
                var side = s == 0 ? -1f : 1f;
                var x = side * (jamb - 0.14f);
                var near = wall - 0.135f;
                // 台座・柱・筐体。下から順に細く、上でまた太くする
                shell.Box(new Vector3(x, GarageFloorY + 0.018f, near), new Vector3(0.240f, 0.036f, 0.210f));
                shell.Box(new Vector3(x, 0.20f, near), new Vector3(0.110f, 0.330f, 0.110f));
                shell.Box(new Vector3(x, 0.42f, near), new Vector3(0.190f, 0.260f, 0.170f));
                // 目。向かい合う側を向く。窪みを彫って、そこへ赤いレンズを嵌める
                dark.Box(new Vector3(x - side * 0.090f, 0.42f, near),
                    new Vector3(0.020f, 0.150f, 0.115f));
                var eye = Piece(Child(parent, "ShutterEye" + s), "Lens",
                    Shape("EyeLens", 1f, b => b.Box(Vector3.zero, new Vector3(0.016f, 0.125f, 0.092f))),
                    Glow(new Color(0.95f, 0.30f, 0.26f), 1.7f));
                eye.position = new Vector3(x - side * 0.099f, 0.42f, near);
                // 管。柱に沿って上へ逃がす
                metal.Box(new Vector3(x, 1.06f, wall - 0.035f), new Vector3(0.040f, 1.12f, 0.040f));
            }

            metal.Emit(parent, "ShutterGear", Mat("Metal"), false, Generated);
            dark.Emit(parent, "ShutterFace", Mat("CarGap"), false, Generated);
            shell.Emit(parent, "ShutterCases", Mat("CarSteel"), false, Generated);
            knob.Emit(parent, "ShutterButtons", Mat("CarSteel"), false, Generated);
        }

        // ---- 通用口 ----------------------------------------------------------

        /// <summary>
        /// 右の壁の、立ち位置のすぐ脇にある通用口。建物の中へ通じる扉。
        ///
        /// **調べられない。ただの見た目。** この場面で必須なのは garage.door と
        /// drive.window の 2 つだけで、必須でない対象を足しても、乗り込む前に
        /// 拾える物が増えるだけになる（<see cref="Controls"/> と同じ但し書き）。
        ///
        /// **立ち位置のすぐ脇に置く。** プレイヤーは <see cref="BuildDrive.StandAt"/>
        /// (4.2, 0, -7) に立って始まる。右の壁（x 7）までは 2.8 m しかないので、
        /// 振り向けば目の前に来る。前の壁（z 6）なら始まった向き
        /// （<see cref="BuildDrive.StandYaw"/> -28 度）の画面に入るが、13 m 先で、
        /// 「立ち位置の近くの壁」にはならない。
        ///
        /// **始まった向きの画面には入らない。** 右へ 90 度ほど向いたところに来る。
        /// z -7 は通路の側で、前の列の止め線（-2.5）と後ろの列の口（-8.4）のあいだ。
        /// 柱（z -8.4 と -2.5）にも、停めてある車にも掛からない。
        ///
        /// 枠・扉・框・取っ手・蝶番・敷居・扉の上の灯りまで作る。
        /// **枠と扉と鏡板の素材を分ける。** 枠は塗った鉄（0.216）、扉はシャッターと同じ塗り
        /// （0.155）、鏡板と開口の際は Metal（0.085）。壁（0.128）に対して明・中・暗の
        /// 三段になるので、扉が付いていることが形として読める。
        /// **鏡板を CarGap（0.020）で塗ると、近くで見たときに窪みではなく穴に見える。**
        /// 半分の明るさで足りる
        /// </summary>
        static void SideDoor(Transform parent)
        {
            // 右の壁の内側の面。壁は厚み 0.25 の真ん中が halfX + skin に来る
            var wall = GarageAt.x + GarageWide * 0.5f;
            const float z = -7f;
            const float wide = 0.98f;
            const float high = 2.10f;

            var cases = new Bank { Texel = 1.4f };
            var leaf = new Bank { Texel = 0.5f };
            var dark = new Bank { Texel = 1.0f };
            var mid = GarageFloorY + high * 0.5f;

            // 開口の暗がり。壁の面へ半分埋めて、縁が壁と並ばないようにする
            dark.Box(new Vector3(wall - 0.008f, mid, z), new Vector3(0.030f, high, wide));

            // 扉。開口より一回り小さく、継ぎ目のぶん内へ寄せる
            leaf.Box(new Vector3(wall - 0.048f, mid, z), new Vector3(0.055f, high - 0.03f, wide - 0.06f));
            // 框。竪框二本と、上・中・下の桟。板を継いだ扉に見せる
            for (var s = 0; s < 2; s++)
                leaf.Box(new Vector3(wall - 0.088f, mid, z + (s == 0 ? -0.375f : 0.375f)),
                    new Vector3(0.020f, high - 0.07f, 0.170f));
            leaf.Box(new Vector3(wall - 0.088f, GarageFloorY + 0.105f, z), new Vector3(0.020f, 0.185f, 0.920f));
            leaf.Box(new Vector3(wall - 0.088f, GarageFloorY + 1.150f, z), new Vector3(0.020f, 0.140f, 0.920f));
            leaf.Box(new Vector3(wall - 0.088f, GarageFloorY + 1.998f, z), new Vector3(0.020f, 0.165f, 0.920f));
            // 鏡板。框の窪みを暗い面で出す
            dark.Box(new Vector3(wall - 0.076f, GarageFloorY + 0.645f, z), new Vector3(0.012f, 0.860f, 0.600f));
            dark.Box(new Vector3(wall - 0.076f, GarageFloorY + 1.570f, z), new Vector3(0.012f, 0.680f, 0.600f));

            // 枠。前後の竪枠と上枠。壁から部屋の側へ 7 cm 出す
            for (var s = 0; s < 2; s++)
                cases.Box(new Vector3(wall - 0.035f, mid + 0.055f, z + (s == 0 ? -0.545f : 0.545f)),
                    new Vector3(0.070f, high + 0.150f, 0.110f));
            cases.Box(new Vector3(wall - 0.035f, GarageFloorY + high + 0.075f, z),
                new Vector3(0.070f, 0.110f, wide + 0.330f));
            // 敷居。床との境。ここで扉が止まっていることが読める
            cases.Box(new Vector3(wall - 0.060f, GarageFloorY + 0.010f, z), new Vector3(0.120f, 0.020f, 1.020f));

            // 取っ手。座と、握りの棒。蝶番と反対の側に付ける
            cases.Box(new Vector3(wall - 0.098f, 1.050f, z - 0.375f), new Vector3(0.026f, 0.100f, 0.100f));
            cases.Box(new Vector3(wall - 0.122f, 1.050f, z - 0.325f), new Vector3(0.035f, 0.035f, 0.165f));
            // 蝶番。奥の側に二枚
            foreach (var y in new[] { 0.44f, 1.78f })
                cases.Box(new Vector3(wall - 0.090f, y, z + 0.445f), new Vector3(0.030f, 0.130f, 0.055f));

            // 扉の上の灯り。防滴灯の笠と筐体。シャッターの脇のものと同じ作りで揃える
            const float lampY = 2.42f;
            cases.Box(new Vector3(wall - 0.115f, lampY + 0.085f, z), new Vector3(0.230f, 0.045f, 0.340f));
            cases.Box(new Vector3(wall - 0.048f, lampY, z), new Vector3(0.095f, 0.200f, 0.260f));
            // 電線の管。灯りから天井へ立ち上げる
            cases.Box(new Vector3(wall - 0.035f, (lampY + 0.11f + GarageHigh) * 0.5f, z),
                new Vector3(0.042f, GarageHigh - lampY - 0.11f, 0.042f));

            cases.Emit(parent, "SideDoorCase", Mat("CarSteel"), false, Generated);
            leaf.Emit(parent, "SideDoorLeaf", Mat("Shutter"), false, Generated);
            dark.Emit(parent, "SideDoorFace", Mat("Metal"), false, Generated);

            Piece(Child(parent, "SideDoorLamp"), "Pane",
                Shape("SideDoorPane", 1f, b => b.Box(Vector3.zero, new Vector3(0.030f, 0.135f, 0.210f))),
                Glow(GarageLamp, 1.6f)).position = new Vector3(wall - 0.100f, lampY, z);
            // **壁から 0.38 離す。** 壁の際に置くと、壁を向いた面（枠の小口・扉の框）へ
            // 光が斜めにしか当たらず、いちばん見せたい形が暗いまま残る。
            // 影は落とさせない。WebGL で影を持つ灯りを増やすと重くなる
            var bulb = new GameObject("Light");
            bulb.transform.SetParent(Child(parent, "SideDoorLamp"), false);
            bulb.transform.localPosition = new Vector3(wall - 0.38f, lampY - 0.18f, z);
            var lit = bulb.AddComponent<Light>();
            UnityEngine.Rendering.Universal.LightExtensions.GetUniversalAdditionalLightData(lit);
            lit.type = LightType.Point;
            lit.color = GarageLamp;
            // **押しボタンの箱の灯り（1.7）より弱い。** あちらは 8 m 先から見つけさせる
            // 灯りだが、こちらは立ち位置の 2.8 m 隣にある。同じ強さだと、
            // 始まった瞬間に画面の端が明るくなって、暗い車庫の暗さが崩れる
            lit.intensity = 0.9f;
            lit.range = 4f;
            lit.shadows = LightShadows.None;
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
        /// 天井の灯り。**区画の列に合わせて 2 列と、シャッターの手前にもう 1 列。**
        ///
        /// 1 列のときは (±3.0, 2.80, -2.00) に 2 本だけで、奥行き 16 m のうち
        /// 真ん中しか照らしていなかった。21 m に伸ばして列を 2 つにした以上、
        /// 灯りも 2 列に増やさないと後ろの列が丸ごと闇に沈む。
        ///
        /// **列の z は区画の真ん中ではなく、通路の両端に置く。** 区画の真ん中に吊ると、
        /// 灯りの真下に車の屋根が来て、光がそこで止まる。通路の縁に吊れば、
        /// 同じ灯りが車の前半分と通路の両方を照らす。実際の車庫もそう吊ってある。
        ///
        /// **三つめの列はシャッターの手前。** 二列のときは、いちばん前の灯りでも
        /// シャッターから 7.1 m 離れていた。灯りの range は 13 あっても減衰は距離の
        /// 二乗で効くので、7 m 先の壁に届くのは真下の 1/6 しかない。前の壁も、
        /// 押しボタンの箱も、足元の光電管も、そこにある物として読めないまま沈んでいた。
        /// <see cref="ShutterLampZ"/> に吊ると壁まで 1.7 m で、前の壁が
        /// ガレージでいちばん明るい面になる。歩いてくる先に明るい壁があること自体が、
        /// そこに何か付いていると気づかせる手掛かりになる。
        ///
        /// 管は x の向きに寝かせる。列が横一線に並んで、三つの列だと一目で分かる。
        /// 影を落とさせないのは、WebGL で影を持つ灯りを増やすと重くなるため
        /// （<see cref="Lamps"/> だけで 9 灯になる。描画は Forward+ なので、
        /// 1 つの mesh に載る灯りの数は頭打ちにならない）
        /// </summary>
        static void Lamps(Transform parent)
        {
            var tube = Shape("GarageTube", 0.5f, b => b.Box(Vector3.zero, new Vector3(2.40f, 0.08f, 0.18f)));
            var lit = Glow(GarageLamp, 1.6f);
            // 並びは変えない。前の二列が Lamp0*・Lamp1* のままになるよう、足すのは後ろへ
            var rows = new[] { AisleTo + 1.30f, AisleFrom - 1.30f, ShutterLampZ };
            // **シャッターの列だけ強い。** ほかの二列は床（真下 2.76 m）を照らすが、
            // この列は前の壁（斜め 2.2 m）を照らす。壁は立っているので光が斜めに当たり、
            // 同じ強さでは床ほど明るくならない。歩いてくる先の壁が読める強さに上げる
            var power = new[] { 4.6f, 4.6f, 6.2f };
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
                    UnityEngine.Rendering.Universal.LightExtensions.GetUniversalAdditionalLightData(l);
                    l.type = LightType.Point;
                    l.color = GarageLamp;
                    // **8.0 から下げた。** 2 本が 6 本になったので、同じ強さのままだと
                    // 重なったところで床が飛ぶ。灯りの真下で床が 52 前後に来るところは
                    // 変えずに、本数のぶんだけ 1 本を弱める
                    l.intensity = power[r];
                    l.range = 13f;
                    l.shadows = LightShadows.None;
                }
        }
    }
}
