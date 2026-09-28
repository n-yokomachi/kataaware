using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 作り込んだ景色の帯 3〜5（2026-09-28、親「帯 3・4・5 を同じ進め方で作り込む」）。帯 1・2（<c>BuildEndingWild</c>）と同じ進め方で、
    /// 実在の 8 月の景色を下調べして、大きな形を三つと繰り返す物を一つ決めてから細部を足す（下調べの要点はシナリオ設計 13.3）。
    /// 植物の絵は <c>tools/make-ending.py</c> が描く二枚目のアトラス（<c>EndingField.png</c>）。石垣の面は村の石垣と同じ石灰岩の絵。
    ///
    /// - 帯 3 昼の荒野（<see cref="Moorland"/>）: ノース・ヨーク・ムーアズの尾根の道。一面の盛りのヒースが、焼いて若返らせた継ぎはぎの濃淡で
    ///   尾根の向こうへ重なり、右は谷（デール）へ下りて緑の牧草地と石の農家。道の脇に石の道標が並び、尾根の辻に背の高い石の十字架
    /// - 帯 4 昼過ぎの麦畑（<see cref="Harvest"/>）: 8 月の刈り入れ。左は立っている金色の麦と、縁のヒナゲシとヤグルマギク。
    ///   右は刈った後の淡い株の畑に、丸い藁の束が刈り跡の列に沿って並ぶ。畑を生け垣が区切り、生け垣の中にオークが立つ
    /// - 帯 5 夕方前の石垣と羊の丘（<see cref="Wolds"/>）: コッツウォルズの丘の上の小道。蜂蜜色の乾いた石垣（頭に縦の笠石）が道の両脇と
    ///   丘の起伏に沿って続き、白い顔の羊が散らばる。左の丘の頂にブナの木立、右は谷へ下りて、谷底に石の村と教会の塔
    ///
    /// 遠景（<see cref="FarmBackdrop"/>）は環の地面の端から続く斜面で、環の地面と同じ高さから始めて奥で持ち上げる。空には場面 8 と同じ千切れ雲
    /// </summary>
    public static partial class BuildEndingLand
    {
        // ---- アトラス（EndingField.png。make-ending.py の FIELD_CELLS と同じ並び） ----------------------

        enum F
        {
            Poppy, Cornflower, Thistle, Mayweed, Knapweed, Scabious, Ragwort, Grass,
            HedgeA, HedgeB, Bracken, Gorse, Ling, Bell, OldHeath, Bilberry,
            Oak, Beech, Ash, MoorGrass, Tuft, Coping, Reeds, Reedmace,
        }

        static readonly int[,] FieldCells =
        {
            { 0, 0, 1, 2 }, { 1, 0, 1, 2 }, { 2, 0, 1, 2 }, { 3, 0, 1, 2 }, { 4, 0, 1, 2 }, { 5, 0, 1, 2 }, { 6, 0, 1, 2 }, { 7, 0, 1, 2 },
            { 0, 2, 2, 1 }, { 2, 2, 2, 1 }, { 4, 2, 2, 1 }, { 6, 2, 2, 1 }, { 0, 3, 2, 1 }, { 2, 3, 2, 1 }, { 4, 3, 2, 1 }, { 6, 3, 2, 1 },
            { 0, 4, 2, 2 }, { 2, 4, 2, 2 }, { 4, 4, 2, 2 }, { 6, 4, 2, 1 }, { 6, 5, 2, 1 }, { 0, 6, 2, 1 },
            { 2, 6, 1, 2 }, { 3, 6, 1, 2 },
        };

        /// <summary>丈・幅（m）・札の枚数。畑の縁の花は実物の草丈（ヒナゲシ 0.6 m、アザミ 1.1 m）、生け垣は 1.3〜1.6 m</summary>
        static readonly Vector3[] FieldSizes =
        {
            new Vector3(0.65f, 0.33f, 2), new Vector3(0.70f, 0.35f, 2), new Vector3(1.10f, 0.55f, 2), new Vector3(0.50f, 0.25f, 2),
            new Vector3(0.75f, 0.48f, 2), new Vector3(0.85f, 0.50f, 2), new Vector3(0.85f, 0.50f, 2), new Vector3(0.95f, 0.55f, 2),
            new Vector3(1.30f, 2.60f, 1), new Vector3(1.60f, 3.20f, 1), new Vector3(0.90f, 1.80f, 3), new Vector3(0.85f, 1.60f, 3),
            new Vector3(0.40f, 0.80f, 3), new Vector3(0.36f, 0.72f, 3), new Vector3(0.50f, 0.90f, 3), new Vector3(0.34f, 0.68f, 3),
            new Vector3(6.0f, 7.0f, 3), new Vector3(8.0f, 9.0f, 3), new Vector3(6.5f, 7.0f, 3), new Vector3(0.55f, 1.00f, 3), new Vector3(0.45f, 0.90f, 3),
            new Vector3(1.00f, 2.00f, 1), new Vector3(2.0f, 1.0f, 2), new Vector3(1.8f, 0.9f, 2),
        };

        static void FieldUv(F k, out Vector2 min, out Vector2 max)
        {
            var i = (int)k;
            const float inset = 1.5f / 1024f;
            const float unit = 128f / 1024f;
            var x0 = FieldCells[i, 0] * unit;
            var x1 = (FieldCells[i, 0] + FieldCells[i, 2]) * unit;
            var y1 = 1f - FieldCells[i, 1] * unit;
            var y0 = 1f - (FieldCells[i, 1] + FieldCells[i, 3]) * unit;
            min = new Vector2(x0 + inset, y0 + inset);
            max = new Vector2(x1 - inset, y1 - inset);
        }

        /// <summary>株を一つ（畑と荒野のアトラス）。札を向きから等しい角度で回して交差させる</summary>
        static void Plant(Bank f, F k, Vector3 foot, float scale, float yaw, Vector3 lean)
        {
            var cards = Mathf.Max(1, (int)FieldSizes[(int)k].z);
            Plant(f, k, foot, scale, yaw, lean, cards, 180f / cards);
        }

        static void Plant(Bank f, F k, Vector3 foot, float scale, float yaw, Vector3 lean, int cards, float spread)
        {
            Vector2 min, max;
            FieldUv(k, out min, out max);
            var size = FieldSizes[(int)k];
            var high = size.x * scale;
            var wide = size.y * scale;
            f.RootY = foot.y;
            f.RootHigh = high;
            var up = (Vector3.up + lean).normalized * high;
            for (var c = 0; c < cards; c++)
            {
                var a = (yaw + spread * c) * Mathf.Deg2Rad;
                var across = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (wide * 0.5f);
                f.AtlasCard(foot + Vector3.down * 0.03f, across, up, min, max);
            }
        }

        /// <summary>
        /// 生け垣。x（道の芯からの向き付きの隔たり）に沿って、z0 から z1 まで札を並べる。厚み thick の表と裏の二枚にし、
        /// 裏を少し高くして頭に厚みを出す。札ごとに左右を反転し、背を揺らして、同じ絵の繰り返しを隠す。ground は z から足元の高さ
        /// </summary>
        static void Hedgerow(Slice s, F kind, float x, float z0, float z1, float scale, float thick, System.Func<float, float> ground)
        {
            Vector2 min, max;
            FieldUv(kind, out min, out max);
            var size = FieldSizes[(int)kind];
            var len = size.y * scale;
            for (var face = 0; face < 2; face++)
            {
                var off = (face == 0 ? -0.5f : 0.5f) * thick * Mathf.Sign(x);
                var z = z0 - s.Next() * len * 0.5f;
                while (z < z1)
                {
                    var zc = Mathf.Clamp(z + len * 0.5f, z0, z1);
                    var high = size.x * scale * s.Range(0.9f, 1.1f) * (face == 0 ? 1f : 1.08f);
                    var foot = new Vector3(x + off, ground(zc) - 0.05f, zc);
                    s.farm.RootY = foot.y;
                    s.farm.RootHigh = high;
                    var flip = s.Next() < 0.5f;
                    var a = flip ? new Vector2(max.x, min.y) : min;
                    var b = flip ? new Vector2(min.x, max.y) : max;
                    s.farm.AtlasCard(foot, new Vector3(0f, 0f, len * 0.5f), Vector3.up * high, a, b);
                    z += len * s.Range(0.72f, 0.85f);
                }
            }
        }

        // ---- 地面 ----------------------------------------------------------------------

        /// <summary>道の片側の地面を升目に張る。高さは h(隔たり, 環の中の z)、色は col(升の真ん中の隔たり, 環の中の z)</summary>
        static void Ground(Slice s, int side, float[] xs, int zSteps, System.Func<float, float, float> h, System.Func<float, float, Swatch> col)
        {
            var dz = TileLength / zSteps;
            for (var k = 0; k + 1 < xs.Length; k++)
                for (var j = 0; j < zSteps; j++)
                {
                    float a = xs[k], b = xs[k + 1], z0 = j * dz, z1 = z0 + dz;
                    System.Func<float, float, Vector3> P = (d, z) => new Vector3(side * d, h(d, s.Offset + z), z);
                    var c = col((a + b) * 0.5f, s.Offset + (z0 + z1) * 0.5f);
                    if (side > 0) s.paint.Quad(P(a, z1), P(b, z1), P(b, z0), P(a, z0), c);
                    else s.paint.Quad(P(b, z1), P(a, z1), P(a, z0), P(b, z0), c);
                }
        }

        /// <summary>0〜1 の決まった値。升や畑ごとに色を振るのに使う</summary>
        static float Hash01(int a, int b, int c)
        {
            unchecked
            {
                var h = (a * 73856093) ^ (b * 19349663) ^ (c * 83492791);
                h ^= h >> 13;
                h *= 1274126177;
                return ((h ^ (h >> 16)) & 0x7fffffff) % 1000 / 1000f;
            }
        }

        /// <summary>四隅で面を一枚。outward の側を表にする（巻きを確かめて、逆なら並びを返す）</summary>
        static void Face4(Paint p, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, Swatch col)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) >= 0f) p.Quad(a, b, c, d, col);
            else p.Quad(d, c, b, a, col);
        }

        static void Face3(Paint p, Vector3 a, Vector3 b, Vector3 c, Vector3 outward, Swatch col)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) >= 0f) p.Tri(a, b, c, col);
            else p.Tri(c, b, a, col);
        }

        /// <summary>
        /// 切妻の家。足元 foot、棟の向き yaw（度）、棟に沿った長さ len、幅 wide、軒の高さ eaves、棟の高さ ridge。
        /// 壁の箱の上に二枚の屋根と、妻の三角を載せる。屋根は 0.3 m 張り出す
        /// </summary>
        static void Gable(Paint p, Vector3 foot, float len, float wide, float eaves, float ridge, float yaw, Swatch wall, Swatch roof)
        {
            var r = Quaternion.Euler(0f, yaw, 0f);
            p.Box(foot + Vector3.up * (eaves * 0.5f - 0.5f), new Vector3(wide, eaves + 1f, len), yaw, wall, wall);
            System.Func<float, float, float, Vector3> P = (x, y, z) => foot + r * new Vector3(x, y, z);
            float hl = len * 0.5f + 0.3f, hw = wide * 0.5f + 0.3f;
            var e = eaves - 0.25f;
            Face4(p, P(hw, e, -hl), P(hw, e, hl), P(0f, ridge, hl), P(0f, ridge, -hl), r * new Vector3(1f, 1f, 0f), roof);
            Face4(p, P(-hw, e, -hl), P(-hw, e, hl), P(0f, ridge, hl), P(0f, ridge, -hl), r * new Vector3(-1f, 1f, 0f), roof);
            for (var end = -1; end <= 1; end += 2)
                Face3(p, P(-wide * 0.5f, eaves, end * len * 0.5f), P(wide * 0.5f, eaves, end * len * 0.5f), P(0f, ridge - 0.2f, end * len * 0.5f), r * new Vector3(0f, 0f, end), wall);
        }

        /// <summary>
        /// 五本の横木の木の門と、両脇の石の門柱。a と b は門柱の足元（道の脇の地面の高さ）
        /// </summary>
        static void FieldGate(Slice s, Vector3 a, Vector3 b, Swatch post)
        {
            var run = b - a;
            var len = run.magnitude;
            var yaw = Mathf.Atan2(run.x, run.z) * Mathf.Rad2Deg;
            s.paint.Box(a + Vector3.up * 0.65f, new Vector3(0.34f, 1.3f, 0.34f), yaw, post, post);
            s.paint.Box(b + Vector3.up * 0.65f, new Vector3(0.34f, 1.3f, 0.34f), yaw, post, post);
            var dir = run / len;
            var from = a + dir * 0.2f;
            var span = len - 0.4f;
            for (var k = 0; k < 5; k++)
                s.paint.Box(from + dir * (span * 0.5f) + Vector3.up * (0.28f + k * 0.2f), new Vector3(0.05f, 0.08f, span), yaw, Swatch.Gate, Swatch.Gate);
            s.paint.Box(from + dir * 0.06f + Vector3.up * 0.6f, new Vector3(0.07f, 1.1f, 0.1f), yaw, Swatch.Gate, Swatch.Gate);
            s.paint.Box(from + dir * (span - 0.06f) + Vector3.up * 0.6f, new Vector3(0.07f, 1.1f, 0.1f), yaw, Swatch.Gate, Swatch.Gate);
            // 筋交い（蝶番の側の下から、先の側の上へ）
            s.paint.Log(from + dir * 0.1f + Vector3.up * 0.3f, from + dir * (span * 0.55f) + Vector3.up * 1.05f, 0.035f, 4, Swatch.Gate, Swatch.Gate);
        }

        /// <summary>羊の群れ。face は顔と脚の色（荒野は黒い顔のスウェールデール、石垣の丘は白い顔のコッツウォルド）</summary>
        static void Flock(Slice s, int side, int count, float near, float far, System.Func<float, float, float> h, Swatch face)
        {
            var cz = s.Range(3f, 17f);
            var cd = s.Range(near, far);
            for (var i = 0; i < count; i++)
            {
                var d = Mathf.Clamp(cd + s.Range(-6f, 6f), near, far);
                var z = Mathf.Clamp(cz + s.Range(-6f, 6f), 0.5f, TileLength - 0.5f);
                Sheep(s, new Vector3(side * d, h(d, s.Offset + z), z), s.Next() * 360f, face);
            }
        }

        // ---- 帯 3 昼の荒野 --------------------------------------------------------------

        /// <summary>荒野の道の近くの地面の段（道の芯から）。縁の草の先は荒野の地面の絵</summary>
        static readonly float[] MoorNear = { 1.85f, 3.2f, 4.6f, 6.5f, 9f, 12f, 16f, 21f, 27f, 34f, 42f };
        /// <summary>その先の、継ぎはぎの色の段</summary>
        static readonly float[] MoorFar = { 42f, 51f, 62f, 75f, 90f, 106f, 124f, 146f, 170f };

        /// <summary>
        /// 道の近くの荒野の地面。縁の草（4.6 m まで）は色見本、その先 42 m までは荒野の地面の絵を 4 m で繰り返す。
        /// 絵の座標は環の中の z から取るので、区切りの継ぎ目でも環の継ぎ目（240 m は 4 m の倍）でも絵がつながる
        /// </summary>
        static void HeathGround(Slice s, int side, System.Func<float, float, float> h)
        {
            const int steps = 4;
            var dz = TileLength / steps;
            for (var k = 0; k + 1 < MoorNear.Length; k++)
                for (var j = 0; j < steps; j++)
                {
                    float a = MoorNear[k], b = MoorNear[k + 1], z0 = j * dz, z1 = z0 + dz;
                    System.Func<float, float, Vector3> P = (d, z) => new Vector3(side * d, h(d, s.Offset + z), z);
                    if (b <= 4.61f)
                    {
                        if (side > 0) s.paint.Quad(P(a, z1), P(b, z1), P(b, z0), P(a, z0), Swatch.Grass);
                        else s.paint.Quad(P(b, z1), P(a, z1), P(a, z0), P(b, z0), Swatch.Grass);
                        continue;
                    }
                    System.Func<float, float, Vector2> U = (d, z) => new Vector2(side * d * 0.25f, (s.Offset + z) * 0.25f);
                    if (side > 0) s.heath.Patch(P(a, z1), P(b, z1), P(b, z0), P(a, z0), U(a, z1), U(b, z1), U(b, z0), U(a, z0));
                    else s.heath.Patch(P(b, z1), P(a, z1), P(a, z0), P(b, z0), U(b, z1), U(a, z1), U(a, z0), U(b, z0));
                }
        }

        /// <summary>
        /// 荒野の高さ。道は尾根の上（お手本はブレイキー・リッジ）。左は浅く下がってから奥の尾根へ上がり、右は谷（デール）へ深く下りる。
        /// wave が false なら環の波を足さない（遠景の斜面の始まりを合わせる）
        /// </summary>
        static float MoorProfile(int side, float d)
        {
            if (d <= 4.6f) return VergeY;
            var t = d - 4.6f;
            var dip = side < 0 ? 8f : 18f;
            var rise = side < 0 ? 30f : 10f;
            var fall = -dip * (1f - Mathf.Exp(-(t / 45f) * (t / 45f)));
            var up = rise * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(70f, 170f, d));
            return VergeY + fall + up;
        }

        static float MoorHeight(Slice s, int side, float d, float zRing)
        {
            var w = 2f * Mathf.PI * zRing / s.Span;
            var t = Mathf.Max(0f, d - 4.6f);
            var wave = 2.0f * Mathf.Clamp01(t / 15f) * (Mathf.Sin(w * 2f + d * 0.04f + side) + 0.5f * Mathf.Sin(w * 5f - d * 0.07f + 1.3f));
            return MoorProfile(side, d) + wave;
        }

        /// <summary>
        /// 荒野の継ぎはぎ。ヒースは 10〜25 年ごとに幅 30 m ほどの帯で焼いて若返らせるので、盛りの紫・伸びた茶・焼いた後の緑・焼いた跡が
        /// 継ぎはぎに並ぶ。右の谷の斜面はワラビ、谷底は石垣で区切った牧草地
        /// </summary>
        static Swatch MoorPatch(int side, float d, float zRing)
        {
            if (d < 4.6f) return Swatch.Grass;
            if (side > 0 && d > 78f)
            {
                var f = Hash01(Mathf.FloorToInt((d - 78f) / 32f), Mathf.FloorToInt(zRing / 60f), 7);
                return f < 0.4f ? Swatch.Lush : f < 0.75f ? Swatch.Pasture : Swatch.Grass;
            }
            if (side > 0 && d > 46f) return Hash01(Mathf.FloorToInt(d / 12f), Mathf.FloorToInt(zRing / 20f), 5) < 0.75f ? Swatch.Bracken : Swatch.HeathOld;
            var h = Hash01(Mathf.FloorToInt((d + 7f) / 22f), Mathf.FloorToInt(zRing / 28f), side);
            if (h < 0.28f) return Swatch.HeathBloom;
            if (h < 0.48f) return Swatch.Heath;
            if (h < 0.68f) return Swatch.HeathOld;
            if (h < 0.82f) return Swatch.HeathYoung;
            if (h < 0.88f) return Swatch.Burnt;
            if (h < 0.96f) return Swatch.MoorGrass;
            return Swatch.Bracken;
        }

        static void Moorland(Slice s)
        {
            // 一車線の舗装の道。線は引かない
            s.paint.Flat(-1.85f, 1.85f, 0f, TileLength, 0f, Swatch.Asphalt);
            for (var side = -1; side <= 1; side += 2)
            {
                var sd = side;
                System.Func<float, float, float> h = (d, z) => MoorHeight(s, sd, d, z);
                HeathGround(s, side, h);
                Ground(s, side, MoorFar, 4, h, (d, z) => MoorPatch(sd, d, z));
                Heath(s, side, h);
                if (s.Next() < 0.6f) Flock(s, side, 1 + s.rnd.Next(3), 2.6f, 26f, h, Swatch.Face);
            }
            // 道標の石。40 m おきに、左右を替えて
            if (s.index % 2 == 0)
            {
                var side = (s.index / 2) % 2 == 0 ? -1 : 1;
                var z = s.Range(4f, 16f);
                var high = s.Range(0.8f, 1.2f);
                s.paint.Prism(new Vector3(side * 4.9f, VergeY - 0.1f, z), 0.2f, 0.16f, high, 4, s.Next() * 90f, Swatch.Gritstone, Swatch.Moss, s.rnd, 0.15f,
                    new Vector3(s.Range(-0.08f, 0.08f), 0f, s.Range(-0.08f, 0.08f)));
            }
            if (s.index == 7) MoorCross(s, new Vector3(-6.8f, VergeY, 11f));
            if (s.index == 4) GrouseButts(s);
            Dale(s);
        }

        /// <summary>
        /// ヒースの株。道の近くに密に、遠くへ疎らに大きく（遠い株ほど大きくして、升の色の上に粒として読ませる）。
        /// 株の種類は足元の継ぎはぎの色に合わせる
        /// </summary>
        static void Heath(Slice s, int side, System.Func<float, float, float> h)
        {
            for (var z = s.Next() * 0.8f; z < TileLength; z += s.Range(0.6f, 1.2f))
                Plant(s.farm, F.Tuft, new Vector3(side * s.Range(1.9f, 3.0f), VergeY, z), s.Range(0.55f, 0.85f), s.Next() * 180f, new Vector3(-side * 0.15f, 0f, 0f));
            for (var i = 0; i < 180; i++)
            {
                var d = 3.6f + Mathf.Pow(s.Next(), 1.9f) * 62f;
                var z = s.Next() * TileLength;
                var zr = s.Offset + z;
                var patch = MoorPatch(side, d, zr);
                F kind;
                // 株は大きめ（1〜1.5 m の座布団）。実物の丈どおりでは株のあいだの地面の色が広く見え、荒野が芝生に見えた
                var scale = s.Range(1.3f, 1.9f);
                switch (patch)
                {
                    case Swatch.HeathBloom: kind = s.Next() < 0.72f ? F.Ling : F.Bell; break;
                    case Swatch.Heath: kind = s.Next() < 0.5f ? F.Bell : F.Ling; break;
                    case Swatch.HeathOld: kind = F.OldHeath; break;
                    case Swatch.HeathYoung: kind = s.Next() < 0.6f ? F.Bilberry : F.MoorGrass; break;
                    case Swatch.Burnt: if (s.Next() < 0.7f) continue; kind = F.MoorGrass; scale *= 0.6f; break;
                    case Swatch.MoorGrass: kind = F.MoorGrass; break;
                    case Swatch.Bracken: kind = F.Bracken; scale *= 1.15f; break;
                    case Swatch.Grass: kind = F.Tuft; break;
                    default: continue;
                }
                var grow = 1f + Mathf.Max(0f, d - 6f) * 0.03f;
                Plant(s.farm, kind, new Vector3(side * d, h(d, zr), z), scale * grow, s.Next() * 180f, Vector3.zero);
            }
        }

        /// <summary>
        /// 尾根の辻の石の十字架（お手本はノース・ヨーク・ムーアズの「ヤング・ラルフ」。高さ 2.7 m ほどの細い柱の頭に小さな腕）。
        /// 段の台に立て、腕を道の方へ向ける
        /// </summary>
        static void MoorCross(Slice s, Vector3 at)
        {
            s.paint.Box(at + Vector3.up * 0.2f, new Vector3(1.3f, 0.5f, 1.3f), 8f, Swatch.Gritstone, Swatch.Gritstone);
            s.paint.Box(at + Vector3.up * 0.52f, new Vector3(0.8f, 0.2f, 0.8f), 8f, Swatch.Gritstone, Swatch.Gritstone);
            s.paint.Prism(at + Vector3.up * 0.6f, 0.2f, 0.16f, 2.1f, 4, 53f, Swatch.Gritstone, null);
            s.paint.Box(at + Vector3.up * 2.62f, new Vector3(0.72f, 0.2f, 0.18f), 8f, Swatch.Gritstone, Swatch.Gritstone);
            s.paint.Box(at + Vector3.up * 2.9f, new Vector3(0.2f, 0.42f, 0.18f), 8f, Swatch.Gritstone, Swatch.Gritstone);
        }

        /// <summary>斜面に一列に並んだライチョウ撃ちの隠れ場（石と芝の低い囲い）。左の斜面を登っていく</summary>
        static void GrouseButts(Slice s)
        {
            for (var i = 0; i < 5; i++)
            {
                var d = 42f + i * 26f;
                var z = 8f + i * 1.8f;
                var y = MoorHeight(s, -1, d, s.Offset + z);
                var at = new Vector3(-d, y, z);
                s.paint.Box(at + new Vector3(0f, 0.45f, 1.0f), new Vector3(2.4f, 1.1f, 0.6f), 0f, Swatch.Turf, Swatch.Gritstone);
                s.paint.Box(at + new Vector3(-1.1f, 0.45f, 0.1f), new Vector3(0.6f, 1.1f, 1.6f), 0f, Swatch.Turf, Swatch.Gritstone);
                s.paint.Box(at + new Vector3(1.1f, 0.45f, 0.1f), new Vector3(0.6f, 1.1f, 1.6f), 0f, Swatch.Turf, Swatch.Gritstone);
            }
        }

        /// <summary>右の谷底の、砂岩の石垣で区切った牧草地と、石の農家</summary>
        static void Dale(Slice s)
        {
            foreach (var d in new[] { 90f, 122f, 152f })
                for (var z = 1.25f; z < TileLength; z += 2.5f)
                {
                    var y = MoorHeight(s, 1, d, s.Offset + z);
                    s.paint.Box(new Vector3(d, y + 0.45f, z), new Vector3(0.6f, 1.1f, 2.55f), 0f, Swatch.Gritstone, Swatch.Gritstone);
                }
            if (s.index % 3 == 0)
                for (var d = 80f; d < 170f; d += 2.5f)
                {
                    var y = MoorHeight(s, 1, d, s.Offset + 6f);
                    s.paint.Box(new Vector3(d, y + 0.45f, 6f), new Vector3(2.55f, 1.1f, 0.6f), 0f, Swatch.Gritstone, Swatch.Gritstone);
                }
            if (s.index == 9)
            {
                var d = 106f;
                var y = MoorHeight(s, 1, d, s.Offset + 10f);
                Gable(s.paint, new Vector3(d, y, 10f), 11f, 6f, 5.2f, 8.4f, 6f, Swatch.Gritstone, Swatch.StoneSlate);
                Gable(s.paint, new Vector3(d + 3f, y, 20f), 14f, 7f, 4.6f, 7.6f, 96f, Swatch.Gritstone, Swatch.StoneSlate);
                for (var k = 0; k < 3; k++)
                {
                    var td = d - 10f + k * 7f;
                    Plant(s.farm, F.Ash, new Vector3(td, MoorHeight(s, 1, td, s.Offset + 2f) - 0.3f, 1f + k * 2.5f), s.Range(1.1f, 1.4f), s.Next() * 180f, Vector3.zero);
                }
            }
        }

        // ---- 帯 4 昼過ぎの麦畑 -----------------------------------------------------------

        /// <summary>麦畑の側（左）の高さ</summary>
        static float WheatHeight(Slice s, float d, float zRing) { return Hill(s, d, zRing, 5f, 13f, 55f, 1.6f); }
        /// <summary>刈った畑の側（右）の高さ</summary>
        static float StubbleHeight(Slice s, float d, float zRing) { return Hill(s, d, zRing, 4f, 9f, 55f, 1.6f); }

        /// <summary>刈った畑の段。0.8 m の細い段が刈り跡の列（藁を寄せた跡）で、藁の束はそこに並ぶ</summary>
        static readonly float[] StubbleRows = { 2.7f, 3.4f, 6.5f, 7.3f, 13f, 13.8f, 19.5f, 20.3f, 26f, 26.8f, 32.5f, 33.3f, 39f, 39.8f, 45.5f, 46.3f, 52f, 52.8f, 58f };
        static readonly float[] FarFields = { 58f, 70f, 84f, 97f, 99.5f, 120f, 137f, 139.5f, 170f };
        static readonly float[] WheatRows = { 2.5f, 4f, 6f, 9f, 12f, 16f, 21f, 27f, 34f, 42f };
        static readonly float[] BeyondWheat = { 42f, 52f, 64f, 81f, 83.5f, 100f, 121f, 123.5f, 145f, 170f };

        /// <summary>
        /// 生け垣の外の畑の継ぎはぎ。牧草を主に、麦と刈った畑を混ぜる（麦と刈った畑を多くすると、丘が砂の丘に見えた）。
        /// 畑の境（40 m ごと・環の 80 m ごと）は生け垣の暗い筋
        /// </summary>
        static Swatch Patchwork(int side, float d, float zRing, float from)
        {
            var fx = (d - from) / 40f;
            var fz = zRing / 80f;
            if (fx - Mathf.Floor(fx) < 0.06f || fz - Mathf.Floor(fz) < 0.04f) return Swatch.HedgeDark;
            var f = Hash01(Mathf.FloorToInt(fx), Mathf.FloorToInt(fz), side + 11);
            return f < 0.2f ? Swatch.WheatFar : f < 0.38f ? Swatch.Stubble : f < 0.72f ? Swatch.Pasture : Swatch.Lush;
        }

        static void Harvest(Slice s)
        {
            DirtRoad(s);
            WheatSide(s);
            StubbleSide(s);
        }

        /// <summary>
        /// 左の立っている麦。場面 8 と同じ麦の札と畑の地。道の縁の細い草と、畑の縁（ヘッドランド）のヒナゲシ・ヤグルマギク・シカギク。
        /// 麦の中にトラクターの轍の二本の筋（トラムライン）を空ける。畑の奥は生け垣で区切り、その外は継ぎはぎの畑と丘の上の木立
        /// </summary>
        static void WheatSide(Slice s)
        {
            const int side = -1;
            System.Func<float, float, float> h = (d, z) => WheatHeight(s, d, z);
            Strip(s, side, RoadHalf - 0.05f, 2.55f, VergeY, Swatch.Grass);
            const int steps = 4;
            var dz = TileLength / steps;
            for (var k = 0; k + 1 < WheatRows.Length; k++)
                for (var j = 0; j < steps; j++)
                {
                    float a = WheatRows[k], b = WheatRows[k + 1], z0 = j * dz, z1 = z0 + dz;
                    System.Func<float, float, Vector3> P = (d, z) => new Vector3(side * d, h(d, s.Offset + z) - 0.02f, z);
                    System.Func<float, float, Vector2> U = (d, z) => new Vector2(side * d * 0.15f, z * 0.15f);
                    s.field.Patch(P(b, z1), P(a, z1), P(a, z0), P(b, z0), U(b, z1), U(a, z1), U(a, z0), U(b, z0));
                }
            // 畑の縁の花
            for (var z = s.Next() * 0.4f; z < TileLength; z += s.Range(0.28f, 0.5f))
            {
                var d = s.Range(2.35f, 3.35f);
                var pick = s.Next();
                var kind = pick < 0.34f ? F.Poppy : pick < 0.5f ? F.Cornflower : pick < 0.68f ? F.Mayweed : pick < 0.84f ? F.Grass : F.Knapweed;
                Plant(s.farm, kind, new Vector3(side * d, h(d, s.Offset + z), z), s.Range(0.85f, 1.15f), s.Next() * 180f, new Vector3(0.12f, 0f, 0f));
            }
            // 麦の株（場面 8 と同じ十字の札）。トラムラインは空ける
            for (var d = 3.3f; d < 18f; d += 0.95f)
            {
                if ((d > 8.7f && d < 9.4f) || (d > 10.5f && d < 11.2f)) continue;
                for (var z = 0.2f; z < TileLength; z += 0.95f)
                {
                    var x = d + s.Range(-0.3f, 0.3f);
                    var zz = z + s.Range(-0.3f, 0.3f);
                    var root = new Vector3(side * x, h(x, s.Offset + zz), zz);
                    var high = WheatHigh * s.Range(0.9f, 1.12f);
                    s.wheat.RootY = root.y;
                    s.wheat.RootHigh = high;
                    var yaw = s.Next() * Mathf.PI;
                    for (var c = 0; c < 2; c++)
                    {
                        var a = yaw + c * Mathf.PI * 0.5f;
                        s.wheat.Card(root, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.36f, Vector3.up * high, s.Next() * 4f);
                    }
                }
            }
            // 奥の生け垣と、その外の畑
            Hedgerow(s, F.HedgeB, side * 42.5f, 0f, TileLength, 1.3f, 0.9f, z => h(42.5f, s.Offset + z));
            Ground(s, side, BeyondWheat, 4, h, (d, z) => Patchwork(side, d, z, 42f));
            if (s.index % 4 == 1) HedgeTree(s, new Vector3(side * 42.5f, h(42.5f, s.Offset + 9f), 9f), s.Next() < 0.5f ? F.Oak : F.Ash, 1.5f);
            // 丘の上の木立
            if (s.index == 3 || s.index == 9)
                for (var k = 0; k < 6; k++)
                {
                    var d = s.Range(112f, 150f);
                    var z = s.Range(2f, 18f);
                    Plant(s.farm, s.Next() < 0.6f ? F.Oak : F.Ash, new Vector3(side * d, h(d, s.Offset + z) - 0.5f, z), s.Range(1.5f, 2.0f), s.Next() * 180f, Vector3.zero);
                }
        }

        /// <summary>
        /// 右の刈った畑。道の脇の低く刈った生け垣（目の高さより低く、向こうの畑が見える）と、生け垣に立つオーク。
        /// 刈った跡の淡い株の畑に、藁を寄せた列の跡が道と平行に走り、丸い藁の束がその列に沿って並ぶ（繰り返す物）
        /// </summary>
        static void StubbleSide(Slice s)
        {
            const int side = 1;
            System.Func<float, float, float> h = (d, z) => StubbleHeight(s, d, z);
            Strip(s, side, RoadHalf - 0.05f, 2.75f, VergeY, Swatch.Grass);
            for (var z = s.Next() * 0.5f; z < TileLength; z += s.Range(0.5f, 0.9f))
            {
                var pick = s.Next();
                var kind = pick < 0.35f ? F.Grass : pick < 0.6f ? F.Tuft : pick < 0.8f ? F.Knapweed : F.Scabious;
                Plant(s.farm, kind, new Vector3(s.Range(2.05f, 2.6f), VergeY, z), s.Range(0.8f, 1.05f), s.Next() * 180f, new Vector3(-0.12f, 0f, 0f));
            }
            // 道の脇の生け垣。門の区切りでは口を空けて木の門
            System.Func<float, float> road = z => VergeY;
            if (s.index == 2)
            {
                Hedgerow(s, F.HedgeA, 2.95f, 0f, 7.6f, 0.92f, 0.55f, road);
                Hedgerow(s, F.HedgeA, 2.95f, 11.9f, TileLength, 0.92f, 0.55f, road);
                FieldGate(s, new Vector3(2.95f, VergeY, 7.9f), new Vector3(2.95f, VergeY, 11.6f), Swatch.Wood);
            }
            else Hedgerow(s, F.HedgeA, 2.95f, 0f, TileLength, 0.92f, 0.55f, road);
            if (s.index == 5 || s.index == 11) HedgeTree(s, new Vector3(3.1f, VergeY, 12f), F.Oak, 1.7f);
            // 刈った畑。刈り跡の列は暗い細い段
            Ground(s, side, StubbleRows, 4, h, (d, z) =>
            {
                if (d < 3.4f) return Swatch.Grass;
                for (var k = 2; k + 1 < StubbleRows.Length; k += 2)
                    if (d > StubbleRows[k] && d < StubbleRows[k + 1]) return Swatch.StubbleDark;
                return Swatch.Stubble;
            });
            // 丸い藁の束。刈り跡の列の真ん中に、10〜16 m おき。寝かせた物と立てた物
            for (var k = 2; k + 1 < StubbleRows.Length - 1; k += 2)
            {
                var d = (StubbleRows[k] + StubbleRows[k + 1]) * 0.5f;
                for (var z = s.Next() * 12f; z < TileLength; z += s.Range(10f, 16f))
                {
                    if (s.Next() < 0.2f) continue;
                    Bale(s, new Vector3(side * d, h(d, s.Offset + z), z), s.Next() < 0.72f);
                }
            }
            if (s.index == 6) BaleStack(s, new Vector3(side * 9f, h(9f, s.Offset + 4f), 4f));
            // 奥の生け垣と、その外の畑
            Hedgerow(s, F.HedgeA, side * 58.5f, 0f, TileLength, 1.35f, 0.9f, z => h(58.5f, s.Offset + z));
            Ground(s, side, FarFields, 4, h, (d, z) => Patchwork(side, d, z, 58f));
            if (s.index % 4 == 2) HedgeTree(s, new Vector3(side * 58.5f, h(58.5f, s.Offset + 5f), 5f), s.Next() < 0.5f ? F.Oak : F.Ash, 1.6f);
        }

        /// <summary>丸い藁の束（径 1.5 m・長さ 1.2 m）。lying なら寝かせて転がる向きをばらし、でなければ平らな端を下に立てる</summary>
        static void Bale(Slice s, Vector3 foot, bool lying)
        {
            const float r = 0.75f, len = 1.2f;
            if (lying)
            {
                var yaw = s.Range(0f, 180f) * Mathf.Deg2Rad;
                var axis = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)) * (len * 0.5f);
                var c = foot + Vector3.up * (r * 0.95f);
                s.paint.Log(c - axis, c + axis, r, 9, Swatch.Straw, Swatch.Straw, Swatch.StrawEnd);
            }
            else s.paint.Prism(foot + Vector3.down * 0.05f, r, r * 0.97f, len, 9, s.Next() * 40f, Swatch.Straw, Swatch.StrawEnd);
        }

        /// <summary>畑の角に積んだ藁の束（下に 4、上に 3、頂に 2）</summary>
        static void BaleStack(Slice s, Vector3 foot)
        {
            const float r = 0.75f, len = 1.2f;
            for (var tier = 0; tier < 3; tier++)
            {
                var n = 4 - tier;
                for (var k = 0; k < n; k++)
                {
                    var z = foot.z + (k - (n - 1) * 0.5f) * (len + 0.05f);
                    var c = new Vector3(foot.x, foot.y + r * 0.95f + tier * r * 1.7f, z);
                    var axis = new Vector3(0f, 0f, len * 0.5f - 0.02f);
                    s.paint.Log(c - axis, c + axis, r, 9, Swatch.Straw, Swatch.Straw, Swatch.StrawEnd);
                }
            }
        }

        /// <summary>生け垣の中に立つ木（オークかトネリコ）。幹と、少し高く上げた樹冠</summary>
        static void HedgeTree(Slice s, Vector3 foot, F kind, float crown)
        {
            var trunk = FieldSizes[(int)kind].x * crown * 0.42f;
            s.paint.Prism(foot + Vector3.down * 0.1f, 0.34f, 0.2f, trunk + 0.4f, 7, s.Next() * 50f, Swatch.Bark, null);
            Plant(s.farm, kind, foot + Vector3.up * (trunk * 0.55f), crown, s.Next() * 180f, Vector3.zero);
        }

        // ---- 帯 5 夕方前の石垣と羊の丘 -------------------------------------------------------

        /// <summary>石垣の丘の地面の段</summary>
        static readonly float[] WoldRows = { 1.9f, 3.1f, 4.5f, 6.5f, 9f, 12f, 16f, 21f, 27f, 34f, 42f, 52f, 64f, 78f, 95f, 115f, 140f, 170f };

        /// <summary>左は丘（ウォルド）の頂へ上がり、右は谷へ下りる。道は丘の肩を行く</summary>
        static float WoldProfile(int side, float d)
        {
            if (d <= 4.5f) return VergeY;
            var t = d - 4.5f;
            if (side < 0) return VergeY + 24f * (1f - Mathf.Exp(-(t / 60f) * (t / 60f)));
            // 右は道から 30〜60 m で急に落ちる（谷の肩）。緩く落とすと、谷の底の村が手前の斜面の肩に隠れた
            return VergeY - 16f * (1f - Mathf.Exp(-(t / 30f) * (t / 30f)));
        }

        static float WoldHeight(Slice s, int side, float d, float zRing)
        {
            var w = 2f * Mathf.PI * zRing / s.Span;
            var t = Mathf.Max(0f, d - 4.5f);
            var wave = 2.4f * Mathf.Clamp01(t / 12f) * (Mathf.Sin(w * 2f + d * 0.045f + side * 0.7f) + 0.5f * Mathf.Sin(w * 5f - d * 0.08f + 1.3f));
            return WoldProfile(side, d) + wave;
        }

        /// <summary>石垣で区切った畑の色。道と平行の石垣（45 m・100 m）と、丘を登る石垣で区切った升ごとに、牧草の濃淡と刈った干し草</summary>
        static Swatch WoldField(int side, float d, float zRing)
        {
            if (d < 3.1f) return Swatch.Grass;
            var band = d < 45f ? 0 : d < 100f ? 1 : 2;
            var f = Hash01(band, Mathf.FloorToInt(zRing / 80f), side + 21);
            return f < 0.45f ? Swatch.Pasture : f < 0.8f ? Swatch.Lush : f < 0.92f ? Swatch.Grass : Swatch.MoorGrass;
        }

        /// <summary>石垣の胴の高さ（0.84 m）と根元と頭の厚み、頭の縦の笠石の札の丈（絵の 40 画素ぶん。石は 0.12〜0.17 m で、上は透ける）</summary>
        const float WallHigh = 0.84f, WallBase = 0.62f, WallTop = 0.34f, CopingHigh = 0.31f;

        static void Wolds(Slice s)
        {
            // 細い舗装の小道。線は引かない
            s.paint.Flat(-1.95f, 1.95f, 0f, TileLength, 0f, Swatch.Asphalt);
            for (var side = -1; side <= 1; side += 2)
            {
                var sd = side;
                System.Func<float, float, float> h = (d, z) => WoldHeight(s, sd, d, z);
                Ground(s, side, WoldRows, 4, h, (d, z) => WoldField(sd, d, z));
                // 道の脇の石垣。左の門の区切りでは口を空けて木の門
                var x = side * 3.3f;
                System.Func<float, float> flat = z => VergeY;
                if (side < 0 && s.index == 3)
                {
                    DryWall(s, new Vector3(x, 0f, 0f), new Vector3(x, 0f, 8f), flat, true);
                    DryWall(s, new Vector3(x, 0f, 11.6f), new Vector3(x, 0f, TileLength), flat, true);
                    FieldGate(s, new Vector3(x, VergeY, 8.1f), new Vector3(x, VergeY, 11.4f), Swatch.Honey);
                }
                else DryWall(s, new Vector3(x, 0f, 0f), new Vector3(x, 0f, TileLength), flat, true);
                // 石垣の根元の草花（道の側）
                for (var z = s.Next() * 0.8f; z < TileLength; z += s.Range(0.7f, 1.4f))
                {
                    var pick = s.Next();
                    var kind = pick < 0.3f ? F.Grass : pick < 0.5f ? F.Knapweed : pick < 0.65f ? F.Scabious : pick < 0.78f ? F.Thistle : pick < 0.9f ? F.Tuft : F.Ragwort;
                    Plant(s.farm, kind, new Vector3(side * s.Range(2.0f, 2.95f), VergeY, z), s.Range(0.75f, 1.0f), s.Next() * 180f, new Vector3(-side * 0.1f, 0f, 0f));
                }
                // 道と平行の畑の石垣
                foreach (var d in new[] { 45f, 100f })
                {
                    var dd = d;
                    DryWall(s, new Vector3(side * d, 0f, 0f), new Vector3(side * d, 0f, TileLength), z => h(dd, s.Offset + z), d < 60f);
                }
                // 丘を登っていく石垣
                if (s.index % 4 == (side < 0 ? 1 : 3))
                {
                    var z = s.Range(6f, 14f);
                    var zz = z;
                    DryWall(s, new Vector3(side * 3.65f, 0f, z), new Vector3(side * 170f, 0f, z), p => h(Mathf.Abs(p), s.Offset + zz), false);
                }
                // 羊
                Flock(s, side, 3 + s.rnd.Next(5), 6f, 40f, h, Swatch.Wool);
                if (s.Next() < 0.5f) Flock(s, side, 2 + s.rnd.Next(4), 48f, 95f, h, Swatch.Wool);
                // 石垣の脇のトネリコ
                if (s.index % 5 == (side < 0 ? 0 : 2))
                    HedgeTree(s, new Vector3(side * 45.6f, h(45.6f, s.Offset + 13f), 13f), F.Ash, 1.4f);
            }
            // 左の丘の頂のブナの木立
            if (s.index % 4 == 2)
            {
                var cd = s.Range(80f, 115f);
                for (var k = 0; k < 6; k++)
                {
                    var d = cd + s.Range(-9f, 9f);
                    var z = s.Range(2f, 18f);
                    var y = WoldHeight(s, -1, d, s.Offset + z);
                    s.paint.Prism(new Vector3(-d, y - 0.2f, z), 0.4f, 0.26f, 7f, 7, s.Next() * 50f, Swatch.BeechBark, null);
                    Plant(s.farm, F.Beech, new Vector3(-d, y + 4.5f, z), s.Range(1.2f, 1.5f), s.Next() * 180f, Vector3.zero);
                }
            }
            // 右の斜面の石の納屋
            if (s.index == 8)
            {
                var d = 62f;
                var y = WoldHeight(s, 1, d, s.Offset + 10f);
                Gable(s.paint, new Vector3(d, y, 10f), 12f, 6.5f, 4.4f, 8f, 4f, Swatch.Honey, Swatch.StoneSlate);
            }
        }

        /// <summary>
        /// 乾いた石垣を一本。a と b は xz の端（y は見ない）、ground は a からの向き付きの位置の成分（道と平行なら z、登るなら x）から足元の高さ。
        /// 道と平行（x が同じ）なら ground に z を、そうでなければ x を渡す。
        /// 2 m ごとに、根元 0.62 m・頭 0.34 m の台形の胴（村の石垣と同じ石灰岩の絵）と天を張り、coping なら頭に縦の笠石の札を立てる
        /// </summary>
        static void DryWall(Slice s, Vector3 a, Vector3 b, System.Func<float, float> ground, bool coping)
        {
            var run = new Vector3(b.x - a.x, 0f, b.z - a.z);
            var len = run.magnitude;
            if (len < 0.1f) return;
            var dir = run / len;
            var alongZ = Mathf.Abs(dir.z) > Mathf.Abs(dir.x);
            var n1 = Vector3.Cross(Vector3.up, dir);
            var n = Mathf.Max(1, Mathf.CeilToInt(len / 2f));
            var step = len / n;
            Vector2 cmin = Vector2.zero, cmax = Vector2.zero;
            if (coping)
            {
                FieldUv(F.Coping, out cmin, out cmax);
                // 升の下の 40 画素だけ（笠石の絵）
                cmax.y = cmin.y + 40f / 1024f;
            }
            for (var i = 0; i < n; i++)
            {
                var p0 = new Vector3(a.x, 0f, a.z) + dir * (i * step);
                var p1 = p0 + dir * step;
                var g0 = ground(alongZ ? p0.z : p0.x);
                var g1 = ground(alongZ ? p1.z : p1.x);
                System.Func<Vector3, float, float, float, Vector3> P = (p, g, off, y) => new Vector3(p.x, g + y, p.z) + n1 * off;
                var u = i * step * 0.5f;
                // n1 の側と、その裏
                s.stone.Quad(P(p1, g1, WallBase * 0.5f, -0.15f), P(p0, g0, WallBase * 0.5f, -0.15f), P(p0, g0, WallTop * 0.5f, WallHigh), P(p1, g1, WallTop * 0.5f, WallHigh), u, 0f);
                s.stone.Quad(P(p0, g0, -WallBase * 0.5f, -0.15f), P(p1, g1, -WallBase * 0.5f, -0.15f), P(p1, g1, -WallTop * 0.5f, WallHigh), P(p0, g0, -WallTop * 0.5f, WallHigh), u, 0f);
                // 天（上から見て右回り）
                var t0 = P(p0, g0, -WallTop * 0.5f, WallHigh);
                var t1 = P(p1, g1, -WallTop * 0.5f, WallHigh);
                var t2 = P(p1, g1, WallTop * 0.5f, WallHigh);
                var t3 = P(p0, g0, WallTop * 0.5f, WallHigh);
                if (Vector3.Dot(Vector3.Cross(t1 - t0, t2 - t0), Vector3.up) >= 0f) s.stone.Quad(t0, t1, t2, t3, u, 0.3f);
                else s.stone.Quad(t3, t2, t1, t0, u, 0.3f);
                if (!coping) continue;
                var mid = (p0 + p1) * 0.5f;
                var gm = (g0 + g1) * 0.5f;
                s.farm.RootY = gm + WallHigh;
                s.farm.RootHigh = CopingHigh;
                var root = new Vector3(mid.x, gm + WallHigh - 0.03f, mid.z);
                var slope = (g1 - g0) * 0.5f;
                // 笠石の札は一枚（両面）。左右の反転で同じ並びの繰り返しを隠す
                var flip = (i + s.index) % 2 == 1;
                s.farm.AtlasCard(root, dir * (step * 0.5f + 0.02f) + Vector3.up * slope, Vector3.up * CopingHigh,
                    flip ? new Vector2(cmax.x, cmin.y) : cmin, flip ? new Vector2(cmin.x, cmax.y) : cmax);
            }
        }

        // ---- 遠景 ------------------------------------------------------------------------

        /// <summary>環の地面の外の端（道の芯からの隔たり 170 m、前へ 170 m、後ろへ 70 m の長方形）までの、方位 deg の向きの隔たり</summary>
        static float RingEdge(float deg)
        {
            var a = deg * Mathf.Deg2Rad;
            var sx = Mathf.Abs(Mathf.Sin(a));
            var cz = Mathf.Cos(a);
            var side = sx > 1e-4f ? 170f / sx : float.MaxValue;
            var ahead = cz > 1e-4f ? 170f / cz : cz < -1e-4f ? -Behind / -cz : float.MaxValue;
            return Mathf.Min(side, ahead) * 1.02f;
        }

        /// <summary>
        /// 遠景の斜面。方位 from〜to（度）を segs に割り、環の地面の外の端から半径 far までを rows に割って張る。
        /// 高さは height(方位, 端からの割合 0〜1, 端の隔たり)、色は col(方位の番号, 段の番号)。面は原点の側を向く
        /// </summary>
        static void Hillside(Paint p, float from, float to, float far, int segs, int rows,
            System.Func<float, float, float, float> height, System.Func<int, int, Swatch> col)
        {
            var az = new float[segs + 1];
            for (var i = 0; i <= segs; i++) az[i] = Mathf.Lerp(from, to, i / (float)segs);
            var us = new float[rows + 1];
            for (var j = 0; j <= rows; j++) us[j] = j / (float)rows;
            Hillside(p, az, us, far, height, col);
        }

        /// <summary>方位の境 az（度）と、端から奥への割合の境 us（0〜1）を列で渡す形。細い升を挟めば、畑の境の生け垣や石垣の筋になる</summary>
        static void Hillside(Paint p, float[] az, float[] us, float far,
            System.Func<float, float, float, float> height, System.Func<int, int, Swatch> col)
        {
            for (var i = 0; i + 1 < az.Length; i++)
            {
                float a0 = az[i], a1 = az[i + 1];
                float e0 = RingEdge(a0), e1 = RingEdge(a1);
                for (var j = 0; j + 1 < us.Length; j++)
                {
                    float u0 = us[j], u1 = us[j + 1];
                    System.Func<float, float, float, Vector3> P = (a, e, u) =>
                    {
                        var r = Mathf.Lerp(e, Mathf.Max(e + 5f, far), u);
                        var d = new Vector3(Mathf.Sin(a * Mathf.Deg2Rad), 0f, Mathf.Cos(a * Mathf.Deg2Rad));
                        return d * r + Vector3.up * height(a, u, e);
                    };
                    var q0 = P(a0, e0, u0);
                    var q1 = P(a1, e1, u0);
                    var q2 = P(a1, e1, u1);
                    var q3 = P(a0, e0, u1);
                    var mid = (q0 + q2) * 0.5f;
                    var inward = new Vector3(-mid.x, 0f, -mid.z).normalized * 0.3f + Vector3.up;
                    Face4(p, q0, q1, q2, q3, inward, col(i, j));
                    if (j > 0) continue;
                    // 裾。環の地面は波打つので、端の高さが斜面の始まりより低い所では隙間から空が覗いた。
                    // 環の端の少し内側へ、5 m 下まで垂らして塞ぐ
                    var k0 = new Vector3(q0.x * 0.975f, q0.y - 5f, q0.z * 0.975f);
                    var k1 = new Vector3(q1.x * 0.975f, q1.y - 5f, q1.z * 0.975f);
                    Face4(p, k0, k1, q1, q0, new Vector3(-mid.x, 0f, -mid.z).normalized, col(i, j));
                }
            }
        }

        /// <summary>
        /// 畑の継ぎはぎの境。from〜to を、幅 wide の畑と、幅 line の境の筋を交互に割る（揺らしを入れて、升目に見せない）。
        /// 境の筋は奇数の番号になる
        /// </summary>
        static float[] Fields(float from, float to, float wide, float line, System.Random rnd)
        {
            var list = new System.Collections.Generic.List<float> { from };
            var at = from;
            while (true)
            {
                at += wide * Mathf.Lerp(0.7f, 1.3f, (float)rnd.NextDouble());
                if (at + line >= to - wide * 0.3f) break;
                list.Add(at);
                at += line;
                list.Add(at);
            }
            list.Add(to);
            return list.ToArray();
        }

        /// <summary>石垣の丘の遠景の高さ。左は丘の頂へ、右は谷の肩から底（-16 m）へ下りて、向こうの丘へ上がる</summary>
        static float PastureFar(float a, float u, float e)
        {
            var side = a < 0f ? -1 : 1;
            var d = Mathf.Min(170f, e * Mathf.Abs(Mathf.Sin(a * Mathf.Deg2Rad)));
            var edge = WoldProfile(side, d);
            if (side < 0) return edge + 18f * Mathf.SmoothStep(0f, 1f, u) + 2f * Mathf.Sin(a * 0.3f + u * 3f);
            var valley = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2f, 12f, a));
            var floor = Mathf.Lerp(edge, -16f, valley * Mathf.SmoothStep(0f, 1f, u / 0.3f));
            var far = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.8f, u));
            return floor + (valley * 34f + (1f - valley) * 16f) * far;
        }

        /// <summary>石垣の丘の遠景の、方位 deg・隔たり r の地面の高さ</summary>
        static float PastureGround(float deg, float r)
        {
            var e = RingEdge(deg);
            return PastureFar(deg, Mathf.InverseLerp(e, Mathf.Max(e + 5f, 395f), r), e);
        }

        /// <summary>
        /// 帯 3〜5 の遠景。環の地面の端から同じ高さで始まる斜面を奥の丘へ持ち上げ、畑の継ぎはぎや荒野の濃淡と雲の影を塗る。
        /// 帯 5 は右の谷の底に石の村と教会の塔。どれも空に千切れ雲の層を二枚
        /// </summary>
        static void FarmBackdrop(Scene scene, Paint p, Bank trees, System.Random rnd)
        {
            var phase = (float)rnd.NextDouble() * 6f;
            switch (scene)
            {
                case Scene.Moor:
                    // 尾根の向こうに尾根が重なる。紫の荒野に、雲の影の暗い帯
                    Hillside(p, -100f, 100f, 395f, 64, 5, (a, u, e) =>
                    {
                        var side = a < 0f ? -1 : 1;
                        var d = Mathf.Min(170f, e * Mathf.Abs(Mathf.Sin(a * Mathf.Deg2Rad)));
                        var edge = MoorProfile(side, d);
                        var dale = side > 0 ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(10f, 30f, a)) : 0f;
                        var floor = Mathf.Lerp(edge, -9f, dale * Mathf.SmoothStep(0f, 1f, u / 0.35f));
                        var rise = (side < 0 ? 24f : 36f) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(side > 0 ? 0.35f : 0f, 1f, u));
                        return floor + rise * (0.8f + 0.2f * Mathf.Sin(a * 0.11f + phase)) + 3f * Mathf.Sin(a * 0.31f + u * 3f);
                    }, (i, j) =>
                    {
                        var a = Mathf.Lerp(-100f, 100f, (i + 0.5f) / 64f);
                        var shadow = Mathf.Sin(a * 0.09f + phase * 2f + j * 0.7f) > 0.55f;
                        if (a > 22f && j < 2) return Hash01(i, j, 3) < 0.5f ? Swatch.Lush : Swatch.Pasture;
                        if (a > 22f && j == 2) return Swatch.Bracken;
                        var h = Hash01(i / 2, j, 9);
                        var col = h < 0.5f ? Swatch.HeathBloom : h < 0.75f ? Swatch.Heath : h < 0.9f ? Swatch.HeathOld : Swatch.HeathYoung;
                        if (j >= 4) col = Swatch.HillMoor;
                        return shadow ? (col == Swatch.HeathBloom || col == Swatch.Heath ? Swatch.HeathDark : Swatch.HeathOld) : col;
                    });
                    break;
                case Scene.Wheat:
                {
                    // 丘の畑の継ぎはぎ。畑のあいだに生け垣の暗い筋を挟む。ところどころに木立
                    var az = Fields(-100f, 100f, 9f, 0.6f, rnd);
                    var us = Fields(0f, 1f, 0.17f, 0.018f, rnd);
                    Hillside(p, az, us, 395f, (a, u, e) =>
                    {
                        var d = Mathf.Min(170f, e * Mathf.Abs(Mathf.Sin(a * Mathf.Deg2Rad)));
                        var edge = a < 0f ? 13f * (1f - Mathf.Exp(-Mathf.Pow(Mathf.Max(0f, d - 5f) / 55f, 2f))) : 9f * (1f - Mathf.Exp(-Mathf.Pow(Mathf.Max(0f, d - 4f) / 55f, 2f)));
                        return VergeY + edge + 26f * Mathf.SmoothStep(0f, 1f, u) * (0.75f + 0.25f * Mathf.Sin(a * 0.07f + phase)) + 2f * Mathf.Sin(a * 0.4f + u * 4f);
                    }, (i, j) =>
                    {
                        if ((i & 1) == 1 || (j & 1) == 1) return Swatch.HedgeDark;
                        var f = Hash01(i / 2, j / 2, 17);
                        return f < 0.2f ? Swatch.WheatFar : f < 0.34f ? Swatch.Stubble : f < 0.7f ? Swatch.Pasture : Swatch.Lush;
                    });
                    for (var k = 0; k < 8; k++)
                    {
                        var a = Mathf.Lerp(-80f, 80f, (float)rnd.NextDouble()) * Mathf.Deg2Rad;
                        var r = Mathf.Lerp(250f, 360f, (float)rnd.NextDouble());
                        var at = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * r;
                        at.y = 14f + 18f * Mathf.InverseLerp(200f, 395f, r);
                        for (var q = 0; q < 4; q++)
                            Plant(trees, (q & 1) == 0 ? F.Oak : F.Ash, at + new Vector3((float)rnd.NextDouble() * 12f - 6f, -1f, (float)rnd.NextDouble() * 12f - 6f), 2.4f, (float)rnd.NextDouble() * 180f, Vector3.zero);
                    }
                    break;
                }
                case Scene.Pasture:
                {
                    // 左は丘の頂へ、右は谷の肩から底へ下りて向こうの丘へ。畑の境に遠い石垣の淡い筋。
                    // 村は前の右 6 度の向こうの丘の裾（地面がほぼ道の高さ）。谷の底に置くと、道の脇の石垣と谷の肩に隠れて見えなかった
                    var az = Fields(-100f, 100f, 10f, 0.5f, rnd);
                    var us = Fields(0f, 1f, 0.16f, 0.015f, rnd);
                    Hillside(p, az, us, 395f, PastureFar, (i, j) =>
                    {
                        if ((i & 1) == 1 || (j & 1) == 1) return Swatch.Stone;
                        var f = Hash01(i / 2, j / 2, 23);
                        return f < 0.45f ? Swatch.Pasture : f < 0.78f ? Swatch.Lush : f < 0.9f ? Swatch.Grass : Swatch.WheatFar;
                    });
                    ValleyVillage(p, trees, rnd, 6f, 280f);
                    for (var k = 0; k < 7; k++)
                    {
                        var deg = Mathf.Lerp(20f, 70f, (float)rnd.NextDouble());
                        var r = Mathf.Lerp(250f, 320f, (float)rnd.NextDouble());
                        var a = deg * Mathf.Deg2Rad;
                        var at = new Vector3(Mathf.Sin(a) * r, PastureGround(deg, r) - 0.5f, Mathf.Cos(a) * r);
                        Plant(trees, (k & 1) == 0 ? F.Ash : F.Oak, at, 2.0f, (float)rnd.NextDouble() * 180f, Vector3.zero);
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// 谷の向こうの丘の裾の村（片割れの村）。蜂蜜色の石の家と石版の屋根を、曲がった通りに沿って並べ、真ん中に羊毛で建った教会
        /// （身廊と、頂に四つの小尖塔を立てた四角い塔）。周りに木。方位 deg、隔たり dist。足元は遠景の斜面の高さ（<see cref="PastureGround"/>）
        /// </summary>
        static void ValleyVillage(Paint p, Bank trees, System.Random rnd, float deg, float dist)
        {
            var a = deg * Mathf.Deg2Rad;
            var toward = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            var across = new Vector3(toward.z, 0f, -toward.x);
            var centre = toward * dist;
            System.Func<Vector3, Vector3> Ground = q =>
            {
                var qd = Mathf.Atan2(q.x, q.z) * Mathf.Rad2Deg;
                return new Vector3(q.x, PastureGround(qd, new Vector2(q.x, q.z).magnitude) - 0.3f, q.z);
            };
            for (var i = 0; i < 16; i++)
            {
                var t = Mathf.Lerp(-1f, 1f, i / 15f);
                var at = Ground(centre + across * (t * 50f) + toward * (Mathf.Sin(t * 2.2f) * 12f + ((i & 1) == 0 ? -7f : 7f)));
                var yaw = deg + 90f + Mathf.Cos(t * 2.2f) * 18f + ((i & 1) == 0 ? 0f : 180f);
                var len = Mathf.Lerp(8f, 13f, (float)rnd.NextDouble());
                Gable(p, at, len, 5.8f, 5.2f, 9f, yaw, Swatch.Honey, Swatch.StoneSlate);
            }
            // 教会
            var church = Ground(centre + toward * 16f - across * 6f);
            Gable(p, church, 22f, 8f, 7.5f, 12.5f, deg, Swatch.Honey, Swatch.StoneSlate);
            var tower = Ground(church - toward * 13.5f);
            p.Box(tower + Vector3.up * 10.5f, new Vector3(5.6f, 23f, 5.6f), deg, Swatch.Honey, Swatch.Honey);
            var r = Quaternion.Euler(0f, deg, 0f);
            for (var cx = -1; cx <= 1; cx += 2)
                for (var cz = -1; cz <= 1; cz += 2)
                    p.Prism(tower + r * new Vector3(cx * 2.45f, 22f, cz * 2.45f), 0.45f, 0.06f, 3.2f, 4, deg + 45f, Swatch.Honey, null);
            for (var k = 0; k < 12; k++)
            {
                var t = Mathf.Lerp(-1.1f, 1.1f, (float)rnd.NextDouble());
                var at = Ground(centre + across * (t * 58f) + toward * Mathf.Lerp(-22f, 26f, (float)rnd.NextDouble()));
                Plant(trees, (k % 3) == 0 ? F.Oak : F.Ash, at + Vector3.down * 0.3f, Mathf.Lerp(1.3f, 1.8f, (float)rnd.NextDouble()), (float)rnd.NextDouble() * 180f, Vector3.zero);
            }
        }

        /// <summary>
        /// 空の千切れ雲の層（場面 8 の高い空と同じ絵とシェーダー。流れは <see cref="CloudDrift"/>）。低い層と高い層の二枚。
        /// 目の奥は 400 m なので、板の縁がそこに届く前の仰角で消し切る（低い層は 9 度、高い層は 16 度で消える）
        /// </summary>
        static void Clouds(Transform parent)
        {
            var m = Materials();
            Deck(parent, "CloudLow", m.cloudLow, 60f, 420f, 0.009f, new Vector2(0.0022f, 0.0008f));
            Deck(parent, "CloudHigh", m.cloudHigh, 110f, 420f, 0.0045f, new Vector2(0.0011f, 0.0004f));
        }

        static void Deck(Transform parent, string name, Material mat, float high, float half, float texel, Vector2 drift)
        {
            var bank = new Bank { Texel = texel };
            bank.FaceY(high, -half, half, -half, half, -1);
            var go = bank.Emit(parent, name, mat, false, BuildEnding.Generated + "FarSky_");
            if (go == null) return;
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var drifter = go.AddComponent<CloudDrift>();
            var so = new SerializedObject(drifter);
            so.FindProperty("speed").vector2Value = drift;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- マテリアル ------------------------------------------------------------------

        /// <summary>畑と荒野と石垣の丘の札のマテリアル（EndingField.png、HalfAware/Foliage）。EndingWild と同じ値</summary>
        static Material FarmMat()
        {
            const string path = BuildEnding.Materials + "EndingField.mat";
            var shader = Shader.Find("HalfAware/Foliage");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = "EndingField" };
                AssetDatabase.CreateAsset(m, path);
            }
            if (m.shader != shader) m.shader = shader;
            m.SetTexture("_BaseMap", Atlas(BuildEnding.Textures + "EndingField.png", true));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Cutoff", 0.5f);
            m.SetFloat("_Wrap", 0.5f);
            // 明るみは EndingWild より控える（昼の荒野のヒースが、日と明るみで綿菓子の桃色に浮いた）
            m.SetFloat("_Glow", 0.22f);
            m.SetFloat("_Shade", 0.35f);
            m.SetFloat("_SkyLift", 0.25f);
            m.SetFloat("_Sway", 0.03f);
            m.SetFloat("_SwayRate", 1.1f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>湖の上の薄い靄（加算。霧を色として混ぜない HalfAware/RoadGlow）。月明かりの青みを薄く</summary>
        static Material MistMat()
        {
            const string path = BuildEnding.Materials + "EndingMist.mat";
            var shader = Shader.Find("HalfAware/RoadGlow");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = "EndingMist" };
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetTexture("_BaseMap", Atlas(BuildEnding.Textures + "EndingMist.png", false));
            m.SetColor("_BaseColor", new Color(0.10f, 0.11f, 0.14f, 1f));
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>荒野の道の近くの地面（make-ending.py の EndingHeathGround.png）</summary>
        static Material HeathGroundMat()
        {
            const string path = BuildEnding.Textures + "EndingHeathGround.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.userData != "heath1")
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.maxTextureSize = 256;
                importer.userData = "heath1";
                importer.SaveAndReimport();
            }
            var m = Lit("EndingHeathGround", Color.white, 0.04f);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) Debug.LogWarning("荒野の地面の絵が無い。python tools/make-ending.py を走らせる: " + path);
            m.SetTexture("_BaseMap", tex);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>石垣の面。村の石垣と同じコッツウォルズの石灰岩の絵（VillageStone.png、2 m で一回り）</summary>
        static Material StoneMat()
        {
            var m = Lit("EndingStone", Color.white, 0.05f);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Village/VillageStone.png");
            if (tex == null) Debug.LogWarning("石垣の絵が無い: Assets/Textures/Village/VillageStone.png");
            m.SetTexture("_BaseMap", tex);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>雲の層のマテリアル（HalfAware/SkyCloud と場面 8 の千切れ雲の絵）。fade は消え切る仰角の sin、full は濃さのままになる仰角の sin</summary>
        static Material CloudMat(string name, Color tint, float fade, float full)
        {
            var path = BuildEnding.Materials + name + ".mat";
            var shader = Shader.Find("HalfAware/SkyCloud");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            var torn = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/CloudTorn.png");
            if (torn == null) Debug.LogWarning("雲の絵が無い: Assets/Textures/CloudTorn.png");
            m.SetTexture("_BaseMap", torn);
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_FadeAt", fade);
            m.SetFloat("_FullAt", full);
            m.renderQueue = 2950;
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
