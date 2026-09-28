using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 作り込んだ景色の帯 6・7（2026-09-28、親「帯 6・7 の作り込み」）。帯 1〜5 と同じ進め方で、実在の景色を下調べして
    /// 大きな形を三つと繰り返す物を一つ決めてから細部を足す（下調べの要点はシナリオ設計 13.3）。札は帯 3〜5 と同じ EndingField.png。
    ///
    /// - 帯 6 夕暮れのブナの並木（<see cref="Avenue"/>）: 北アイルランドのダーク・ヘッジズ（1775 年ごろに植えたブナの並木）。
    ///   うねる灰色の幹が道の両脇に並び、太い枝が道の上へ伸びて向かいの枝と絡み、トンネルになる。根は地面を這う。
    ///   低い夕日が前の左から幹のあいだを抜け、幹の長い影が道を横切る。並木の外は夕日を受けた畑
    /// - 帯 7 夜の湖（<see cref="Lakeshore"/>）: 湖水地方の湖（ダーウェントウォーターほか）の夜。月の光の道、岸のヨシとガマの群れ、
    ///   小さな木の桟橋と舟、湖の上の薄い靄、向こう岸の重なる丘の影と、ぽつぽつと灯る窓とその水の映り。左は粘板岩の石垣と暗い林
    /// </summary>
    public static partial class BuildEndingLand
    {
        // ---- 帯 6 夕暮れのブナの並木 ------------------------------------------------------

        /// <summary>並木の幹の、道の芯からの隔たり</summary>
        const float AvenueRow = 3.7f;
        /// <summary>幹の間隔。左右で半分ずらす</summary>
        const float AvenueStep = 6.6f;
        /// <summary>並木の外の畑の段</summary>
        static readonly float[] AvenueFields = { 8.4f, 12f, 18f, 26f, 36f, 50f, 70f, 95f, 130f, 170f };

        /// <summary>
        /// ブナの並木のトンネル。細い舗装の小道（線は引かない）の両脇に、うねる灰色の幹（繰り返す物）。
        /// 道の縁は落ち葉と苔、幹の根は地面を這う。並木の外は低い生け垣の向こうに、夕日を受けた刈った畑と牧草地
        /// </summary>
        static void Avenue(Slice s)
        {
            s.paint.Flat(-2.2f, 2.2f, 0f, TileLength, 0f, Swatch.Asphalt);
            for (var side = -1; side <= 1; side += 2)
            {
                var sd = side;
                System.Func<float, float, float> h = (d, z) => Hill(s, d, z, 9f, 6f, 60f, 1.2f);
                // 落ち葉の縁に、苔の斑を継ぎはぎに
                Strip(s, side, 2.15f, 4.4f, VergeY, Swatch.Litter);
                for (var z = s.Next() * 3f; z < TileLength; z += s.Range(2f, 5f))
                {
                    var len = s.Range(0.8f, 2.4f);
                    var a = s.Range(2.3f, 3.6f);
                    var b = a + s.Range(0.4f, 1.0f);
                    s.paint.Flat(Mathf.Min(side * a, side * b), Mathf.Max(side * a, side * b), z, Mathf.Min(TileLength, z + len), VergeY + 0.004f, Swatch.Moss);
                }
                Strip(s, side, 4.35f, 8.5f, VergeY, Swatch.Grass);
                // 落ち葉の上の草と、土手のワラビ
                for (var z = s.Next(); z < TileLength; z += s.Range(0.8f, 1.6f))
                {
                    var pick = s.Next();
                    var d = s.Range(2.4f, 4.2f);
                    if (pick < 0.45f) Plant(s.farm, F.Tuft, new Vector3(side * d, VergeY, z), s.Range(0.6f, 0.9f), s.Next() * 180f, Vector3.zero);
                    else if (pick < 0.7f) Plant(s.farm, F.Bracken, new Vector3(side * s.Range(4.6f, 7.8f), VergeY, z), s.Range(0.8f, 1.1f), s.Next() * 180f, Vector3.zero);
                    else if (pick < 0.85f) Plant(s.farm, F.Grass, new Vector3(side * s.Range(4.4f, 8f), VergeY, z), s.Range(0.8f, 1.0f), s.Next() * 180f, Vector3.zero);
                }
                // 並木の外の低い生け垣と、夕日を受けた畑
                Hedgerow(s, F.HedgeB, side * 8.6f, 0f, TileLength, 0.85f, 0.6f, z => VergeY);
                Ground(s, side, AvenueFields, 4, h, (d, z) =>
                {
                    var f = Hash01(Mathf.FloorToInt((d - 8f) / 45f), Mathf.FloorToInt(z / 80f), sd + 31);
                    return f < 0.4f ? Swatch.Stubble : f < 0.6f ? Swatch.WheatFar : f < 0.85f ? Swatch.Pasture : Swatch.Lush;
                });
                // 幹。左右で半分ずらし、ときどき一本欠ける（嵐で倒れた跡）
                for (var z = side < 0 ? 1.1f : 1.1f + AvenueStep * 0.5f; z < TileLength; z += AvenueStep)
                {
                    if (s.Next() < 0.08f) continue;
                    GnarledBeech(s, new Vector3(side * (AvenueRow + s.Range(-0.25f, 0.25f)), VergeY, z + s.Range(-0.5f, 0.5f)), side);
                }
                // 遠くの木立
                for (var z = s.Next() * 5f; z < TileLength; z += s.Range(5f, 9f))
                {
                    var d = s.Range(60f, 120f);
                    Plant(s.farm, s.Next() < 0.5f ? F.Oak : F.Ash, new Vector3(side * d, h(d, s.Offset + z) - 0.5f, z), s.Range(1.4f, 1.9f), s.Next() * 180f, Vector3.zero);
                }
            }
        }

        /// <summary>
        /// うねるブナの幹を一本。根元が張り、幹は道の側へ傾きながらうねって登り、3.5〜4 m で二股に分かれる。
        /// 太い枝の一本は道の上へ伸びて向かいの枝と絡み、もう一本は上へ。枝先にブナの樹冠を載せ、根は地面を這う
        /// </summary>
        static void GnarledBeech(Slice s, Vector3 foot, int side)
        {
            var inward = new Vector3(-side, 0f, 0f);
            s.paint.Prism(foot + Vector3.down * 0.15f, 0.66f, 0.46f, 1.15f, 9, s.Next() * 40f, Swatch.BeechBark, null);
            var p = foot + Vector3.up * 0.95f;
            var r = 0.46f;
            for (var k = 0; k < 3; k++)
            {
                var next = p + Vector3.up * s.Range(0.9f, 1.25f) + inward * s.Range(0.08f, 0.3f) + new Vector3(s.Range(-0.18f, 0.18f), 0f, s.Range(-0.25f, 0.25f));
                s.paint.Log(p, next, r, 8, Swatch.BeechBark, Swatch.BeechBark);
                p = next;
                r *= 0.9f;
            }
            // 二股。道の上へ伸びる枝と、上へ伸びる枝
            var over = p + inward * s.Range(3.0f, 3.8f) + Vector3.up * s.Range(2.8f, 3.6f) + Vector3.forward * s.Range(-1.4f, 1.4f);
            var upward = p - inward * s.Range(0.2f, 0.9f) + Vector3.up * s.Range(3.6f, 4.6f) + Vector3.forward * s.Range(-1.8f, 1.8f);
            s.paint.Log(p, over, r * 0.72f, 7, Swatch.BeechBark, Swatch.BeechBark);
            s.paint.Log(p, upward, r * 0.66f, 7, Swatch.BeechBark, Swatch.BeechBark);
            var twig0 = over + inward * s.Range(1.4f, 2.2f) + Vector3.up * s.Range(0.8f, 1.6f) + Vector3.forward * s.Range(-1.2f, 1.2f);
            var twig1 = upward + Vector3.up * s.Range(1.2f, 2.0f) + new Vector3(s.Range(-1.2f, 1.2f), 0f, s.Range(-1.6f, 1.6f));
            s.paint.Log(over, twig0, r * 0.34f, 5, Swatch.BeechBark, Swatch.BeechBark);
            s.paint.Log(upward, twig1, r * 0.3f, 5, Swatch.BeechBark, Swatch.BeechBark);
            // 樹冠。枝先に。道の上の樹冠は向かいの樹冠と重なる
            Plant(s.farm, F.Beech, over + Vector3.down * 3.2f, s.Range(0.85f, 1.05f), s.Next() * 180f, Vector3.zero);
            Plant(s.farm, F.Beech, twig0 + Vector3.down * 3.4f, s.Range(0.8f, 1.0f), s.Next() * 180f, Vector3.zero);
            Plant(s.farm, F.Beech, twig1 + Vector3.down * 3.8f, s.Range(0.9f, 1.15f), s.Next() * 180f, Vector3.zero);
            // 地面を這う根。上の面に苔
            var roots = 3 + s.rnd.Next(2);
            for (var k = 0; k < roots; k++)
            {
                var a = s.Range(0f, 360f) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var from = foot + Vector3.up * 0.18f + dir * 0.4f;
                var to = foot + dir * s.Range(1.2f, 2.2f) + Vector3.down * 0.06f;
                s.paint.Log(from, to, s.Range(0.1f, 0.16f), 5, Swatch.BeechBark, Swatch.Moss);
            }
        }

        // ---- 帯 7 夜の湖 ------------------------------------------------------------------

        /// <summary>
        /// 湖沿いの道。舗装の道を前照灯が照らす。右は草の縁から岸の小石の浜へ下り、浅い所にヨシとガマの群れ（繰り返す物）、
        /// 湖の上に薄い靄。左は粘板岩の乾いた石垣と、その奥の暗い林。区切りの一つに小さな木の桟橋と舟
        /// </summary>
        static void Lakeshore(Slice s)
        {
            PavedRoad(s, 2.2f);
            // 左。石垣と林
            Strip(s, -1, 2.15f, 3.45f, VergeY, Swatch.Grass);
            for (var z = 0f; z < TileLength - 0.01f; z += 1.25f)
            {
                var high = s.Range(0.95f, 1.12f);
                s.paint.Box(new Vector3(-3.7f, VergeY + high * 0.5f, z + 0.625f), new Vector3(0.6f, high, 1.26f), s.Range(-1.5f, 1.5f),
                    Swatch.Slate, s.Next() < 0.5f ? Swatch.Slate : Swatch.GraniteDark);
            }
            Strip(s, -1, 3.95f, 48f, FloorY, Swatch.Floor);
            for (var z = s.Next(); z < TileLength; z += s.Range(0.6f, 1.2f))
            {
                var pick = s.Next();
                var kind = pick < 0.5f ? F.Tuft : pick < 0.8f ? F.Grass : F.Knapweed;
                Plant(s.farm, kind, new Vector3(-s.Range(2.2f, 3.3f), VergeY, z), s.Range(0.7f, 1.0f), s.Next() * 180f, new Vector3(0.1f, 0f, 0f));
            }
            for (var i = 0; i < 9; i++)
            {
                var d = s.Range(4.5f, 26f);
                var z = s.Next() * TileLength;
                Plant(s.farm, F.Bracken, new Vector3(-d, FloorY, z), s.Range(1.0f, 1.4f), s.Next() * 180f, Vector3.zero);
            }
            for (var z = s.Next() * 5f; z < TileLength; z += s.Range(4.5f, 7.5f))
            {
                var d = s.Range(5.5f, 14f);
                var foot = new Vector3(-d, FloorY, z);
                s.paint.Prism(foot, s.Range(0.2f, 0.32f), 0.14f, 6f, 7, s.Next() * 50f, Swatch.Bark, null);
                Plant(s.farm, s.Next() < 0.6f ? F.Oak : F.Ash, foot + Vector3.up * 3.2f, s.Range(1.2f, 1.6f), s.Next() * 180f, Vector3.zero);
            }
            for (var z = s.Next() * 3f; z < TileLength; z += s.Range(3f, 5f))
                Plant(s.farm, s.Next() < 0.5f ? F.Oak : F.Beech, new Vector3(-s.Range(26f, 36f), FloorY - 0.5f, z), s.Range(1.6f, 2.2f), s.Next() * 180f, Vector3.zero);

            // 右。草の縁、小石の浜、水
            Strip(s, 1, 2.15f, 4.2f, VergeY, Swatch.Grass);
            s.paint.Quad(new Vector3(4.15f, VergeY, TileLength), new Vector3(Shore + 0.4f, LakeY - 0.1f, TileLength),
                new Vector3(Shore + 0.4f, LakeY - 0.1f, 0f), new Vector3(4.15f, VergeY, 0f), Swatch.Sand);
            s.water.Patch(new Vector3(Shore, LakeY, TileLength), new Vector3(900f, LakeY, TileLength), new Vector3(900f, LakeY, 0f), new Vector3(Shore, LakeY, 0f),
                new Vector2(Shore / 8f, 5f), new Vector2(900f / 8f, 5f), new Vector2(900f / 8f, 0f), new Vector2(Shore / 8f, 0f));
            for (var z = s.Next(); z < TileLength; z += s.Range(0.7f, 1.3f))
                Plant(s.farm, s.Next() < 0.6f ? F.Tuft : F.Grass, new Vector3(s.Range(2.3f, 4.0f), VergeY, z), s.Range(0.7f, 1.0f), s.Next() * 180f, new Vector3(-0.1f, 0f, 0f));
            // ヨシとガマの群れ。浅い所に、切れ目を空けて
            var bed = s.Next() * 3f;
            while (bed < TileLength)
            {
                var count = 3 + s.rnd.Next(6);
                var reedmace = s.Next() < 0.3f;
                for (var k = 0; k < count && bed < TileLength; k++)
                {
                    var at = new Vector3(s.Range(Shore - 0.3f, Shore + 2.6f), LakeY - 0.05f, bed);
                    Plant(s.farm, reedmace ? F.Reedmace : F.Reeds, at, s.Range(0.85f, 1.2f), s.Next() * 180f, new Vector3(s.Range(-0.08f, 0.08f), 0f, 0f));
                    bed += s.Range(0.35f, 0.8f);
                }
                bed += s.Range(1.5f, 4.5f);
            }
            for (var i = 0; i < 3; i++)
            {
                var r = s.Range(0.3f, 0.9f);
                s.paint.Prism(new Vector3(s.Range(Shore, Shore + 3f), LakeY - 0.3f, s.Next() * TileLength), r, r * 0.6f, r * 0.9f, 6, s.Next() * 60f,
                    Swatch.RockDark, Swatch.RockDark, s.rnd, 0.4f);
            }
            if (s.index == 5) Jetty(s, 9f);
            // 湖の上の薄い靄。岸と平行な、水の上の低い帯（加算）
            for (var i = 0; i < 4; i++)
            {
                var x = i == 0 ? s.Range(7f, 12f) : s.Range(12f, 70f);
                var z = s.Next() * TileLength;
                var wide = s.Range(10f, 24f) * (1f + x / 60f);
                var high = s.Range(1.4f, 2.6f) * (1f + x / 80f);
                var y0 = LakeY - 0.2f;
                s.mist.Patch(new Vector3(x, y0, z + wide * 0.5f), new Vector3(x, y0, z - wide * 0.5f), new Vector3(x, y0 + high, z - wide * 0.5f), new Vector3(x, y0 + high, z + wide * 0.5f),
                    new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f));
            }
        }

        /// <summary>小さな木の桟橋と、繋いだ手漕ぎの舟。z は区切りの中の位置</summary>
        static void Jetty(Slice s, float z)
        {
            var y = LakeY + 0.45f;
            var from = Shore - 0.4f;
            var to = Shore + 7.5f;
            s.paint.Box(new Vector3((from + to) * 0.5f, y, z), new Vector3(to - from, 0.12f, 1.3f), 0f, Swatch.Wood, Swatch.Wood);
            for (var x = from + 0.6f; x < to; x += 1.8f)
                foreach (var dz in new[] { -0.6f, 0.6f })
                    s.paint.Box(new Vector3(x, LakeY - 0.1f, z + dz), new Vector3(0.14f, 1.3f, 0.14f), 0f, Swatch.Wood, Swatch.Wood);
            // 舟。桟橋の脇に、鼻先を沖へ
            var boat = new Vector3(to - 2.2f, LakeY + 0.18f, z + 1.6f);
            s.paint.Box(boat, new Vector3(3.4f, 0.42f, 1.15f), 4f, Swatch.GraniteDark, Swatch.Wood);
            s.paint.Box(boat + Vector3.up * 0.2f, new Vector3(2.9f, 0.06f, 0.9f), 4f, Swatch.Floor, Swatch.Floor);
            s.paint.Box(boat + new Vector3(0.2f, 0.26f, 0f), new Vector3(0.25f, 0.08f, 1.0f), 4f, Swatch.Wood, Swatch.Wood);
        }

        // ---- 遠景 --------------------------------------------------------------------------

        /// <summary>
        /// 帯 6 の遠景。夕日を受けた畑の斜面と、前の左の低い所の沈みかけた日の円（幹のあいだから覗く）
        /// </summary>
        static void DuskBackdrop(Paint p, Paint glow, Bank trees, System.Random rnd)
        {
            var az = Fields(-100f, 100f, 9f, 0.6f, rnd);
            var us = Fields(0f, 1f, 0.18f, 0.02f, rnd);
            Hillside(p, az, us, 395f, (a, u, e) =>
            {
                var d = Mathf.Min(170f, e * Mathf.Abs(Mathf.Sin(a * Mathf.Deg2Rad)));
                var edge = 6f * (1f - Mathf.Exp(-Mathf.Pow(Mathf.Max(0f, d - 9f) / 60f, 2f)));
                return VergeY + edge + 22f * Mathf.SmoothStep(0f, 1f, u) + 2f * Mathf.Sin(a * 0.4f + u * 4f);
            }, (i, j) =>
            {
                if ((i & 1) == 1 || (j & 1) == 1) return Swatch.HedgeDark;
                var f = Hash01(i / 2, j / 2, 37);
                return f < 0.35f ? Swatch.Stubble : f < 0.55f ? Swatch.WheatFar : f < 0.85f ? Swatch.Pasture : Swatch.Lush;
            });
            for (var k = 0; k < 8; k++)
            {
                var a = Mathf.Lerp(-80f, 80f, (float)rnd.NextDouble()) * Mathf.Deg2Rad;
                var r = Mathf.Lerp(250f, 360f, (float)rnd.NextDouble());
                var at = new Vector3(Mathf.Sin(a) * r, 10f + 16f * Mathf.InverseLerp(200f, 395f, r), Mathf.Cos(a) * r);
                for (var q = 0; q < 3; q++)
                    Plant(trees, (q & 1) == 0 ? F.Oak : F.Beech, at + new Vector3((float)rnd.NextDouble() * 12f - 6f, -1f, (float)rnd.NextDouble() * 12f - 6f), 2.4f, (float)rnd.NextDouble() * 180f, Vector3.zero);
            }
            // 沈みかけた日。日の差す向き（DuskBeech の aim の逆）の、少し下
            Disc(glow, -30f, 6.5f, 300f, 7f, Swatch.Dusk);
        }

        /// <summary>半径 radius の円弧（方位 from〜to）に、高さ y0〜y1 の帯を立てる。原点を向く</summary>
        static void Glow(Paint glow, float radius, float from, float to, float y0, float y1, Swatch col)
        {
            const int n = 36;
            for (var i = 0; i < n; i++)
            {
                var a0 = Mathf.Lerp(from, to, i / (float)n) * Mathf.Deg2Rad;
                var a1 = Mathf.Lerp(from, to, (i + 1) / (float)n) * Mathf.Deg2Rad;
                var d0 = new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)) * radius;
                var d1 = new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1)) * radius;
                var mid = (d0 + d1) * 0.5f;
                Face4(glow, d0 + Vector3.up * y0, d1 + Vector3.up * y0, d1 + Vector3.up * y1, d0 + Vector3.up * y1, -mid, col);
            }
        }

        /// <summary>方位 deg・高さ elev（度）・隔たり dist の、原点を向く円（半径 radius）</summary>
        static void Disc(Paint glow, float deg, float elev, float dist, float radius, Swatch col)
        {
            var dir = Quaternion.Euler(-elev, deg, 0f) * Vector3.forward;
            var centre = dir * dist;
            var right = Vector3.Cross(Vector3.up, dir).normalized;
            var up = Vector3.Cross(dir, right).normalized;
            const int n = 20;
            for (var i = 0; i < n; i++)
            {
                var a0 = 2f * Mathf.PI * i / n;
                var a1 = 2f * Mathf.PI * (i + 1) / n;
                glow.Tri(centre, centre + (right * Mathf.Cos(a1) + up * Mathf.Sin(a1)) * radius, centre + (right * Mathf.Cos(a0) + up * Mathf.Sin(a0)) * radius, col);
            }
        }

        /// <summary>
        /// 帯 7 の遠景。向こう岸の丘の影を三重に重ね（近いほど暗く、遠いほど月明かりの靄で淡い）、近い丘の裾に家の窓の灯りと、
        /// 水に映った灯りの縦の筋。月と、湖に伸びる月の光の道、星。丘は月（前の右 22 度・11 度の高さ）より低く抑える
        /// </summary>
        static void LakeBackdrop(Paint p, Paint glow, System.Random rnd)
        {
            // 地平の明るみ。月明かりの靄で、地平の近くの空は天頂より明るい。丘の影はこの明るみに対して黒く立つ
            // （明るみが無いと、丘は霧の色に溶けて空と見分けられなかった）
            Glow(glow, 398f, -20f, 180f, -2f, 26f, Swatch.NightGlow);
            Glow(glow, 398f, -20f, 180f, 26f, 60f, Swatch.NightGlowHigh);
            Ridge(p, rnd, 395f, -10f, 175f, 30f, 70f, Swatch.HillNight3, -20f);
            Ridge(p, rnd, 330f, 0f, 170f, 20f, 52f, Swatch.HillNight2, -20f);
            Ridge(p, rnd, 262f, 5f, 165f, 10f, 36f, Swatch.HillNight, -20f);
            // 丘の影の手前の縁（湖の向こう岸）に沿った、水際の暗い林の帯
            Ridge(p, rnd, 250f, 5f, 165f, 3f, 9f, Swatch.HillNight, LakeY - 0.5f);
            for (var i = 0; i < 14; i++)
            {
                var deg = Mathf.Lerp(12f, 150f, (float)rnd.NextDouble());
                var a = deg * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                // 水際の林の帯（250 m）より手前に置く。後ろに置くと帯に隠れた
                var at = dir * 246f + Vector3.up * Mathf.Lerp(1.5f, 6f, (float)rnd.NextDouble());
                Face(glow, at, 0.9f, 0.7f, Swatch.Lamp);
                // 水に映った灯り。灯りの下から手前へ、揺れて切れる縦の筋
                for (var k = 0; k < 4; k++)
                {
                    var near = 242f - k * 7f - (float)rnd.NextDouble() * 3f;
                    var side = new Vector3(dir.z, 0f, -dir.x) * (0.35f + (float)rnd.NextDouble() * 0.3f);
                    var p0 = dir * near + Vector3.up * (LakeY + 0.05f);
                    var p1 = dir * (near - 4.5f) + Vector3.up * (LakeY + 0.05f);
                    Face4(glow, p0 - side, p0 + side, p1 + side, p1 - side, Vector3.up, Swatch.LampDim);
                }
            }
            Moon(glow, rnd);
            Stars(glow, rnd);
        }
    }
}
