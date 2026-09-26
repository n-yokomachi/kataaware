using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 公衆電話の後ろの菜園（2026-09-27、オーナー「公衆電話の後ろにある小麦畑は、別の菜園にして。単一の野菜ではなくいろんな種類で」）。
    /// 路地の南、家 C と家 D の間の、農場の門の奥の一区画（x -37.5〜-19.5、z -4.3〜-17.5）。前は麦畑だった。
    ///
    /// **8 月のイングランドの村の、小さな貸し農園にする。** 二人の持ち主の区画を、門から奥の物置へまっすぐ通る芝の道で分ける。
    /// <list type="table">
    /// <item><term>西の区画</term><description>昔ながらの掘り起こした畝を路地に平行に並べる。手前から、レタス（緑と赤）、
    /// ビーツと玉ねぎ（葉が倒れて黄ばむ頃）、ジャガイモの土寄せの畝、キャベツとケール、奥にランナービーンの竹の合掌。
    /// 畝の間は細い芝の筋。西の奥の角に堆肥の枠、奥の生け垣の前にヒマワリ</description></item>
    /// <item><term>東の区画</term><description>板で囲った高い畝を八つ。フダンソウ、レタス、ズッキーニ、ケール、ビーツ、玉ねぎ、キャベツ。
    /// 奥にスイートピーとランナービーンの竹の三角錐、亜鉛引きの水槽とじょうろ、東の生け垣の前にヒマワリ</description></item>
    /// <item><term>奥</term><description>芝の道の突き当たりに板張りの物置と緑の雨水の樽</description></item>
    /// </list>
    /// 低い物（レタス・ビーツ）を路地の側に、背の高い物（豆の支柱・ヒマワリ・物置）を奥に置く。
    /// 路地の目の高さ（1.6 m）から、低く刈った前の生け垣（<see cref="AllotHedgeHigh"/>）越しに畝の段が重なって見える。
    ///
    /// **重さ。** 野菜の札は南の家並みの花と同じ入れ物（FloraLaneSouth）に、支柱や物置は村の入れ物に溜めて焼くので、描く回数は増えない。
    /// 札は花と葉のアトラスの下の半分に足した升（make-garden.py の 34〜44）
    /// </summary>
    public static partial class BuildVillage
    {
        // ---- 寸法 -----------------------------------------------------------------------

        /// <summary>菜園の内の縁。西と東は家 C・家 D との境の生け垣の内の面、北は路地の側の生け垣の奥の面、南は奥の生け垣の手前</summary>
        const float AllotWest = PlotCEast + 0.5f;
        const float AllotEast = PlotDWest - 0.5f;
        const float AllotNorth = -NorthEdge - 0.95f;
        const float AllotSouth = SouthHedge + 0.5f;
        /// <summary>門から奥の物置へまっすぐ通る芝の道。門（x -30.4〜-27.0）の真ん中に合わせる</summary>
        const float AllotPathWest = -29.3f;
        const float AllotPathEast = -28.1f;

        /// <summary>
        /// 菜園の前（路地の側）の生け垣の高さ。ほかの畑の縁の 1.4 m から下げた。
        /// 1.4 m では、路地の目の高さ（1.6 m）から畝がほとんど見えず、奥の豆の支柱とヒマワリの頭しか覗かなかった
        /// </summary>
        const float AllotHedgeHigh = 0.9f;

        /// <summary>西の区画の畝。(西, 東, 南, 北)。北（路地の側）から奥へ</summary>
        static readonly Vector4[] AllotWestBeds =
        {
            new Vector4(-37.1f, -29.8f, -6.65f, -5.45f),   // レタス
            new Vector4(-37.1f, -29.8f, -8.05f, -6.95f),   // ビーツと玉ねぎ
            new Vector4(-37.1f, -29.8f, -11.0f, -8.35f),   // ジャガイモ
            new Vector4(-37.1f, -29.8f, -12.95f, -11.3f),  // キャベツとケール
            new Vector4(-37.1f, -29.8f, -14.75f, -13.25f), // ランナービーンの合掌
        };
        /// <summary>ジャガイモの土寄せの畝の芯の z</summary>
        static readonly float[] PotatoRidges = { -8.75f, -9.65f, -10.55f };
        /// <summary>ランナービーンの合掌の芯の z と、西と東の端</summary>
        const float BeanRowZ = -14.0f;
        const float BeanRowWest = -36.6f;
        const float BeanRowEast = -31.4f;

        /// <summary>東の区画の高い畝の x の範囲（幅 1.2 m、間 0.6 m）と、手前と奥の二列の z の範囲</summary>
        static readonly Vector2[] RaisedX = { new Vector2(-27.6f, -26.4f), new Vector2(-25.8f, -24.6f), new Vector2(-24.0f, -22.8f), new Vector2(-22.2f, -21.0f) };
        static readonly Vector2[] RaisedZ = { new Vector2(-8.6f, -5.4f), new Vector2(-12.6f, -9.4f) };
        const float RaisedHigh = 0.26f;

        /// <summary>竹の三角錐（スイートピーとランナービーン）の足元の芯</summary>
        static readonly Vector3 PeaWigwamAt = new Vector3(-25.9f, 0f, -14.4f);
        static readonly Vector3 BeanWigwamAt = new Vector3(-23.6f, 0f, -14.5f);
        const float WigwamRadius = 0.42f;
        const float WigwamHigh = 2.0f;

        /// <summary>物置（芝の道の突き当たり）と、亜鉛引きの水槽</summary>
        const float AllotShedWest = -30.1f, AllotShedEast = -27.3f, AllotShedSouth = -17.3f, AllotShedNorth = -15.2f;
        static readonly Vector3 TankAt = new Vector3(-27.1f, 0f, -13.4f);

        // ---- 形 -------------------------------------------------------------------------

        /// <summary>畝の土、土寄せの畝、高い畝の枠、竹の支柱、物置、樽、水槽、堆肥の枠。奥の生け垣</summary>
        static void Allotment(Banks b)
        {
            // 奥の生け垣。家 C と家 D の裏の生け垣と繋げ、その先の麦畑と区切る
            FieldHedge(b, new Vector3(PlotCEast, 0f, SouthHedge), new Vector3(PlotDWest, 0f, SouthHedge), 1.7f, 0.9f, 59);

            // 西の区画の畝の土。畝の間は芝の筋を残す
            foreach (var r in AllotWestBeds) b.Soil.FaceY(0.012f, r.x, r.y, r.z, r.w, 1);
            // ジャガイモの土寄せ。三角の断面の畝を路地に平行に
            foreach (var z in PotatoRidges) EarthUp(b.Soil, AllotWestBeds[2].x + 0.15f, AllotWestBeds[2].y - 0.15f, z, 0.34f, 0.2f);

            // 東の区画の高い畝。板の枠に土
            foreach (var zr in RaisedZ)
                foreach (var xr in RaisedX)
                {
                    const float t = 0.05f;
                    b.Bark.Box(new Vector3(xr.x + t * 0.5f, RaisedHigh * 0.5f, (zr.x + zr.y) * 0.5f), new Vector3(t, RaisedHigh, zr.y - zr.x));
                    b.Bark.Box(new Vector3(xr.y - t * 0.5f, RaisedHigh * 0.5f, (zr.x + zr.y) * 0.5f), new Vector3(t, RaisedHigh, zr.y - zr.x));
                    b.Bark.Box(new Vector3((xr.x + xr.y) * 0.5f, RaisedHigh * 0.5f, zr.x + t * 0.5f), new Vector3(xr.y - xr.x - t * 2f, RaisedHigh, t));
                    b.Bark.Box(new Vector3((xr.x + xr.y) * 0.5f, RaisedHigh * 0.5f, zr.y - t * 0.5f), new Vector3(xr.y - xr.x - t * 2f, RaisedHigh, t));
                    b.Soil.FaceY(RaisedHigh - 0.04f, xr.x + t, xr.y - t, zr.x + t, zr.y - t, 1);
                }
            // 東の区画の奥の土（三角錐と水槽の足元）
            b.Soil.FaceY(0.012f, -26.8f, -22.6f, -15.3f, -13.4f, 1);

            // ランナービーンの合掌。0.52 m ごとに竹を二本、芯の上 1.9 m で交差させて先を少し越し、交差の股に横の竹を渡す
            const float lean = 0.36f;
            const float cross = 1.9f;
            for (var x = BeanRowWest; x <= BeanRowEast + 0.01f; x += 0.52f)
                foreach (var s in new[] { -1f, 1f })
                {
                    var foot = new Vector3(x, 0f, BeanRowZ + s * lean);
                    var top = new Vector3(x, cross, BeanRowZ);
                    Cane(b, foot, foot + (top - foot) * 1.18f);
                }
            Cane(b, new Vector3(BeanRowWest - 0.15f, cross + 0.02f, BeanRowZ), new Vector3(BeanRowEast + 0.15f, cross + 0.02f, BeanRowZ));

            // 竹の三角錐。六本を輪に立て、頭で束ねる
            foreach (var at in new[] { PeaWigwamAt, BeanWigwamAt })
                for (var k = 0; k < 6; k++)
                {
                    var a = (k * 60f + 15f) * Mathf.Deg2Rad;
                    var foot = at + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * WigwamRadius;
                    var apex = at + Vector3.up * WigwamHigh;
                    Cane(b, foot, foot + (apex - foot) * 1.1f);
                }

            // 物置。板張りの切妻に黒いフェルトの屋根。戸は路地の側（北）の妻に、くすんだ緑
            SimpleShed(b, AllotShedWest, AllotShedEast, AllotShedSouth, AllotShedNorth, 2.35f, SwBarrow);
            // 緑の雨水の樽。物置の東の角に、屋根の雨を受ける
            TintPrism(b.Swatch, new Vector3(AllotShedEast + 0.42f, 0f, AllotShedNorth - 0.45f), 0.3f, 0.92f, 10, Quaternion.identity, SwPlastic);
            // 亜鉛引きの水槽（汲み置きの水）と、脇のじょうろ
            Tank(b, TankAt, 1.1f, 0.62f, 0.6f);
            WateringCan(b.Swatch, TankAt + new Vector3(0.78f, 0f, 0.1f), 200f);

            // 堆肥の枠。西の奥の角に、板を隙間を空けて積んだ升を二つ
            Compost(b, -37.2f, -35.4f, -17.3f, -16.1f);
        }

        /// <summary>竹の支柱を一本。foot から top へ。淡い黄土の色（升の色）</summary>
        static void Cane(Banks b, Vector3 foot, Vector3 top)
        {
            var d = top - foot;
            var len = d.magnitude;
            if (len < 1e-3f) return;
            var up = Mathf.Abs(Vector3.Dot(d / len, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            Tint(b.Swatch, (foot + top) * 0.5f, new Vector3(0.022f, 0.022f, len), Quaternion.LookRotation(d / len, up), SwLogEnd);
        }

        /// <summary>土寄せの畝を一本。x に沿い、芯の z、裾の半幅、高さ。二つの斜面と両端の三角</summary>
        static void EarthUp(Bank b, float x0, float x1, float z, float half, float high)
        {
            const float y = 0.012f;
            var n0 = new Vector3(x0, y, z - half);
            var n1 = new Vector3(x1, y, z - half);
            var s0 = new Vector3(x0, y, z + half);
            var s1 = new Vector3(x1, y, z + half);
            var t0 = new Vector3(x0, y + high, z);
            var t1 = new Vector3(x1, y + high, z);
            Face(b, n0, n1, t1, t0, new Vector3(0f, half, -high));
            Face(b, s1, s0, t0, t1, new Vector3(0f, half, high));
            Face(b, n0, t0, s0, s0, Vector3.left);
            Face(b, s1, t1, n1, n1, Vector3.right);
        }

        /// <summary>亜鉛引きの長い水槽。四方の壁（升の色）と、縁の少し下の水の面</summary>
        static void Tank(Banks b, Vector3 at, float lenX, float wideZ, float high)
        {
            const float t = 0.03f;
            Tint(b.Swatch, at + new Vector3(0f, high * 0.5f, -wideZ * 0.5f), new Vector3(lenX, high, t), Quaternion.identity, SwGalv);
            Tint(b.Swatch, at + new Vector3(0f, high * 0.5f, wideZ * 0.5f), new Vector3(lenX, high, t), Quaternion.identity, SwGalv);
            Tint(b.Swatch, at + new Vector3(-lenX * 0.5f, high * 0.5f, 0f), new Vector3(t, high, wideZ), Quaternion.identity, SwGalv);
            Tint(b.Swatch, at + new Vector3(lenX * 0.5f, high * 0.5f, 0f), new Vector3(t, high, wideZ), Quaternion.identity, SwGalv);
            // 縁の巻き
            Tint(b.Swatch, at + new Vector3(0f, high, 0f), new Vector3(lenX + 0.04f, 0.03f, wideZ + 0.04f), Quaternion.identity, SwGalv);
            // 水の面は升の暗い青灰で塗る。水の入れ物（VillageWater）に足すと、庭の池と樽だけを囲んでいた箱が路地の南まで伸び、
            // 路地から南を見る所で描く回数が一つ増えた
            var y = at.y + high - 0.1f;
            var x0 = at.x - lenX * 0.5f + t;
            var x1 = at.x + lenX * 0.5f - t;
            var z0 = at.z - wideZ * 0.5f + t;
            var z1 = at.z + wideZ * 0.5f - t;
            TintFace(b.Swatch, new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0), new Vector3(x0, y, z0), Vector3.up, SwPane);
        }

        /// <summary>堆肥の枠。板を隙間を空けて積んだ升を二つ。前（北）は開けて中の堆肥を見せる</summary>
        static void Compost(Banks b, float x0, float x1, float z0, float z1)
        {
            var xm = (x0 + x1) * 0.5f;
            for (var i = 0; i < 5; i++)
            {
                var y = 0.08f + i * 0.15f;
                b.Bark.Box(new Vector3(xm, y, z0), new Vector3(x1 - x0, 0.1f, 0.03f));
                foreach (var x in new[] { x0, xm, x1 })
                    b.Bark.Box(new Vector3(x, y, (z0 + z1) * 0.5f), new Vector3(0.03f, 0.1f, z1 - z0));
            }
            b.Soil.Box(new Vector3((x0 + xm) * 0.5f, 0.28f, (z0 + z1) * 0.5f - 0.05f), new Vector3(xm - x0 - 0.08f, 0.56f, z1 - z0 - 0.2f));
            b.Soil.Box(new Vector3((xm + x1) * 0.5f, 0.17f, (z0 + z1) * 0.5f - 0.05f), new Vector3(x1 - xm - 0.08f, 0.34f, z1 - z0 - 0.2f));
        }

        // ---- 札 -------------------------------------------------------------------------

        /// <summary>
        /// 菜園の野菜と花の札。南の家並みの花の入れ物（FloraLaneSouth）へ溜める。
        /// 同じ物を三つ四つずつ続けて植え（蒔いた列ごとに揃う）、株ごとに大きさと向きを少し振る
        /// </summary>
        static void AllotmentPlants(Bank f)
        {
            var n = 0;
            // 西の区画。手前の二列のレタス。緑と赤を三株ずつの組で交互に
            foreach (var z in new[] { -5.78f, -6.32f })
            {
                var k = 0;
                for (var x = -36.85f + (z < -6f ? 0.23f : 0f); x < -30.0f; x += 0.46f, k++)
                    Clump(f, (k / 3 + (z < -6f ? 1 : 0)) % 2 == 0 ? Kind.Lettuce : Kind.LettuceRed, Jit(x, 0f, z, n), 0.95f + Hash(701, n) * 0.25f, Hash(703, n++) * 180f, Vector3.zero);
            }
            // ビーツと玉ねぎの列。列に沿う札と横切る札を一枚ずつ
            Row(f, Kind.Beetroot, -36.8f, -30.1f, -7.2f, 0.66f, ref n);
            Row(f, Kind.Onion, -36.8f, -30.1f, -7.82f, 0.66f, ref n);
            // ジャガイモ。土寄せの畝の上に
            foreach (var z in PotatoRidges)
                for (var x = -36.7f + Hash(705, n) * 0.3f; x < -30.1f; x += 0.82f)
                    Clump(f, Kind.Potato, Jit(x, 0.1f, z, n), 0.88f + Hash(707, n) * 0.26f, Hash(709, n++) * 40f - 20f, Vector3.zero);
            // キャベツとケール
            for (var x = -36.8f; x < -30.1f; x += 0.6f)
                Clump(f, Kind.Cabbage, Jit(x, 0f, -11.68f, n), 0.95f + Hash(711, n) * 0.2f, Hash(713, n++) * 180f, Vector3.zero);
            for (var x = -36.7f; x < -30.1f; x += 0.64f)
                Clump(f, Kind.Kale, Jit(x, 0f, -12.55f, n), 0.9f + Hash(715, n) * 0.25f, Hash(717, n++) * 180f, Vector3.zero);
            // ランナービーンの合掌の葉の壁。二つの斜面に、区切りごとに一枚
            const float lean = 0.36f;
            const int pieces = 5;
            var span = (BeanRowEast - BeanRowWest) / pieces;
            for (var s = -1; s <= 1; s += 2)
                for (var k = 0; k < pieces; k++)
                {
                    var x = BeanRowWest + span * (k + 0.5f);
                    var root = new Vector3(x, 0.02f, BeanRowZ + s * (lean + 0.03f));
                    // 斜面に沿わせ、頭は交差の少し上まで
                    var up = new Vector3(0f, 2.0f, -s * lean * 1.05f);
                    Flat(f, Kind.RunnerBean, root, new Vector3(span * 0.56f, 0f, 0f), up);
                }

            // 東の区画の高い畝。手前の列: フダンソウ・レタス・ズッキーニ・ケール。奥の列: ビーツ・玉ねぎ・キャベツ・ズッキーニ
            var y = RaisedHigh - 0.05f;
            Grid(f, Kind.Chard, RaisedX[0], RaisedZ[0], 2, 5, y, ref n);
            GridMixed(f, RaisedX[1], RaisedZ[0], 3, 7, y, ref n);
            Grid(f, Kind.Courgette, RaisedX[2], RaisedZ[0], 1, 2, y, ref n);
            Grid(f, Kind.Kale, RaisedX[3], RaisedZ[0], 2, 4, y, ref n);
            RowsZ(f, Kind.Beetroot, RaisedX[0], RaisedZ[1], 2, y, ref n);
            RowsZ(f, Kind.Onion, RaisedX[1], RaisedZ[1], 2, y, ref n);
            Grid(f, Kind.Cabbage, RaisedX[2], RaisedZ[1], 2, 4, y, ref n);
            Grid(f, Kind.Courgette, RaisedX[3], RaisedZ[1], 1, 2, y, ref n);

            // スイートピーの三角錐（オベリスクのスイートピーの札）と、ランナービーンの三角錐（葉の壁を四方の斜面に）
            Clump(f, Kind.ObeliskVine, PeaWigwamAt, 1.32f, 15f, Vector3.zero);
            for (var k = 0; k < 4; k++)
            {
                var a = (k * 90f + 45f) * Mathf.Deg2Rad;
                var out1 = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var side = Vector3.Cross(Vector3.up, out1);
                var root = BeanWigwamAt + out1 * (WigwamRadius + 0.04f) + Vector3.up * 0.02f;
                var apex = BeanWigwamAt + Vector3.up * (WigwamHigh + 0.1f);
                Flat(f, Kind.RunnerBean, root, side * 0.36f, apex - root);
            }

            // ヒマワリ。奥の生け垣の前と、東の生け垣の前に。顔を路地（北）へ向ける
            var suns = new[]
            {
                new Vector3(-34.9f, 0f, -16.7f), new Vector3(-33.8f, 0f, -16.5f), new Vector3(-32.7f, 0f, -16.8f), new Vector3(-31.6f, 0f, -16.6f),
                new Vector3(-20.2f, 0f, -13.6f), new Vector3(-20.3f, 0f, -14.7f), new Vector3(-20.1f, 0f, -15.9f), new Vector3(-20.3f, 0f, -17.0f),
            };
            for (var i = 0; i < suns.Length; i++)
                Clump(f, Kind.Sunflower, suns[i], 0.88f + Hash(721, i) * 0.3f, Hash(723, i) * 24f - 12f, Vector3.zero);
        }

        /// <summary>株の足元を少しずらす。列が定規で引いたように揃いすぎないように</summary>
        static Vector3 Jit(float x, float y, float z, int n)
        {
            return new Vector3(x + (Hash(731, n) - 0.5f) * 0.08f, y, z + (Hash(733, n) - 0.5f) * 0.08f);
        }

        /// <summary>x に沿った一列。札の一枚を列に沿わせ（路地から横に並んで見える）、もう一枚を横切らせる</summary>
        static void Row(Bank f, Kind k, float x0, float x1, float z, float step, ref int n)
        {
            for (var x = x0; x < x1; x += step)
            {
                Clump(f, k, Jit(x, 0f, z, n), 0.9f + Hash(741, n) * 0.22f, Hash(743, n) * 16f - 8f, Vector3.zero);
                n++;
            }
        }

        /// <summary>高い畝に升目に植える。cols は x の株の数、rows は z の株の数</summary>
        static void Grid(Bank f, Kind k, Vector2 xr, Vector2 zr, int cols, int rows, float y, ref int n)
        {
            for (var i = 0; i < cols; i++)
                for (var j = 0; j < rows; j++)
                {
                    var x = Mathf.Lerp(xr.x, xr.y, (i + 0.5f) / cols);
                    var z = Mathf.Lerp(zr.x, zr.y, (j + 0.5f) / rows);
                    Clump(f, k, Jit(x, y, z, n), 0.9f + Hash(751, n) * 0.22f, Hash(753, n) * 180f, Vector3.zero);
                    n++;
                }
        }

        /// <summary>レタスの緑と赤を列ごとに替えて植える</summary>
        static void GridMixed(Bank f, Vector2 xr, Vector2 zr, int cols, int rows, float y, ref int n)
        {
            for (var i = 0; i < cols; i++)
                for (var j = 0; j < rows; j++)
                {
                    var x = Mathf.Lerp(xr.x, xr.y, (i + 0.5f) / cols);
                    var z = Mathf.Lerp(zr.x, zr.y, (j + 0.5f) / rows);
                    Clump(f, i == 1 ? Kind.LettuceRed : Kind.Lettuce, Jit(x, y, z, n), 0.8f + Hash(755, n) * 0.2f, Hash(757, n) * 180f, Vector3.zero);
                    n++;
                }
        }

        /// <summary>高い畝に z に沿った列を並べる（列の札は z に沿わせる）</summary>
        static void RowsZ(Bank f, Kind k, Vector2 xr, Vector2 zr, int rows, float y, ref int n)
        {
            for (var i = 0; i < rows; i++)
            {
                var x = Mathf.Lerp(xr.x, xr.y, (i + 0.5f) / rows);
                for (var z = zr.x + 0.35f; z < zr.y - 0.2f; z += 0.62f)
                {
                    Clump(f, k, Jit(x, y, z, n), 0.85f + Hash(761, n) * 0.2f, 90f + Hash(763, n) * 16f - 8f, Vector3.zero);
                    n++;
                }
            }
        }
    }
}
