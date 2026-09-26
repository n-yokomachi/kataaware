using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 片割れの庭の花と葉（設計書 3 節）。絵は <c>tools/make-garden.py</c> が描いた一枚のアトラス
    /// （<c>VillageFlora.png</c>）で、札（α で抜く板）を交差させて株にし、花の縁ごとに一枚の mesh へ焼く。
    /// シェーダーは <c>HalfAware/Foliage</c>（裏表を描き、法線を裏返さない）。
    ///
    /// **植え方は下調べ 2 節の決まり事に従う。**
    /// <list type="bullet">
    /// <item>密に植え、地面をあまり見せない（株の根元に地を埋める葉を混ぜる）</item>
    /// <item>小路へこぼれさせる（手前の低い株は縁の外へ出し、頭を小路の側へ傾ける）</item>
    /// <item>奥から手前へ、高い・中・低いの三段</item>
    /// <item>同じ物を 3 株か 5 株のまとまり（ドリフト）で植え、隣のまとまりと端を重ねて色を帯状ににじませる</item>
    /// </list>
    /// **色は白・淡いピンク・青紫を基調に、黄と濃い赤を差し色にする。** 差し色の物は重みを小さく取る。
    ///
    /// **8 月の庭にする。** タチアオイ・エキナセア・ダリア・アスター・キャットミント・ゼラニウム・ラベンダーが主役。
    /// デルフィニウム・ジギタリス・ルピナスは花の終わった穂と葉の株として奥に少し、アリウムは枯れた球
    /// </summary>
    public static partial class BuildVillage
    {
        // ---- アトラス -------------------------------------------------------------------

        /// <summary>アトラスの升の大きさ（画素）と、アトラスの一辺</summary>
        const int AtlasUnit = 128;
        const int AtlasSize = 1024;

        /// <summary>植物の種類。並びはアトラスの割り付け（make-garden.py の CELLS）と同じ</summary>
        enum Kind
        {
            HollyPink, HollyWhite, Delph, Foxglove, DahliaRed, DahliaPink, Rudbeckia, EchPink, EchWhite, Aster,
            Allium, Rosemary, SweetPea, Pelargonium, Catmint, Geranium, Lavender, Mantle, Sage, Hydrangea,
            Filler, Ivy, Roses, Clematis, Honeysuckle, Apple,
        }

        /// <summary>升の (x, y, 幅, 高さ)。make-garden.py の CELLS と同じ値。y は絵の上から数える</summary>
        static readonly int[,] Cells =
        {
            { 0, 0, 1, 3 }, { 1, 0, 1, 3 }, { 2, 0, 1, 3 }, { 3, 0, 1, 3 },
            { 4, 0, 1, 2 }, { 5, 0, 1, 2 }, { 6, 0, 1, 2 }, { 7, 0, 1, 2 },
            { 0, 3, 1, 2 }, { 1, 3, 1, 2 }, { 2, 3, 1, 2 }, { 3, 3, 1, 2 }, { 4, 3, 1, 2 }, { 5, 3, 1, 2 },
            { 4, 2, 2, 1 }, { 6, 2, 2, 1 }, { 6, 3, 2, 1 }, { 6, 4, 2, 1 }, { 0, 5, 2, 1 }, { 2, 5, 2, 1 },
            { 4, 5, 2, 1 }, { 6, 5, 2, 1 },
            { 0, 6, 2, 2 }, { 2, 6, 2, 2 }, { 4, 6, 2, 2 }, { 6, 6, 2, 2 },
        };

        /// <summary>
        /// 株の丈・幅・札の枚数。丈は下調べの表の草丈から、幅は絵の縦横の比に合わせる。
        /// 低い塊は札を三枚にして、上から見ても X の字にならないようにする
        /// </summary>
        static readonly Vector3[] Sizes =
        {
            new Vector3(2.00f, 0.72f, 2), new Vector3(1.90f, 0.70f, 2), new Vector3(1.45f, 0.55f, 2), new Vector3(1.10f, 0.52f, 2),
            new Vector3(1.15f, 0.62f, 2), new Vector3(1.25f, 0.66f, 2), new Vector3(0.85f, 0.50f, 2), new Vector3(0.95f, 0.50f, 2),
            new Vector3(0.90f, 0.48f, 2), new Vector3(0.95f, 0.55f, 2), new Vector3(0.85f, 0.42f, 2), new Vector3(0.95f, 0.52f, 2),
            new Vector3(1.80f, 0.90f, 1), new Vector3(0.52f, 0.46f, 3),
            new Vector3(0.48f, 0.95f, 3), new Vector3(0.42f, 0.85f, 3), new Vector3(0.55f, 0.95f, 3), new Vector3(0.36f, 0.75f, 3),
            new Vector3(0.42f, 0.80f, 3), new Vector3(1.05f, 1.60f, 3), new Vector3(0.45f, 0.90f, 3), new Vector3(0.60f, 1.20f, 1),
            new Vector3(1.00f, 1.00f, 1), new Vector3(1.00f, 1.00f, 1), new Vector3(1.00f, 1.00f, 1), new Vector3(3.00f, 3.20f, 3),
        };

        /// <summary>升の uv。左下と右上。縁を 1.5 画素内へ寄せて、隣の升の滲みを拾わない</summary>
        static void CellUv(Kind k, out Vector2 min, out Vector2 max)
        {
            var i = (int)k;
            const float inset = 1.5f / AtlasSize;
            const float u = (float)AtlasUnit / AtlasSize;
            var x0 = Cells[i, 0] * u;
            var x1 = (Cells[i, 0] + Cells[i, 2]) * u;
            var y1 = 1f - Cells[i, 1] * u;
            var y0 = 1f - (Cells[i, 1] + Cells[i, 3]) * u;
            min = new Vector2(x0 + inset, y0 + inset);
            max = new Vector2(x1 - inset, y1 - inset);
        }

        static Material FloraMat()
        {
            const string path = Materials + "VillageFlora.mat";
            var shader = Shader.Find("HalfAware/Foliage");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                m.name = "VillageFlora";
                AssetDatabase.CreateAsset(m, path);
            }
            if (m.shader != shader) m.shader = shader;
            m.SetTexture("_BaseMap", Picture("VillageFlora.png", true));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Cutoff", 0.5f);
            m.SetFloat("_Wrap", 0.5f);
            m.SetFloat("_Glow", 0.25f);
            m.SetFloat("_Shade", 0.35f);
            // 風の揺れ（設計書 7 節）。背 1 m の株の先が 7 cm ほど、4.8 秒ほどの周期で
            m.SetFloat("_Sway", 0.07f);
            m.SetFloat("_SwayRate", 1.3f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>札の入れ物。根からの高さを uv1 に持たせ（根元の陰り）、法線を上へ倒す</summary>
        static Bank FloraBank()
        {
            return new Bank { Texel = 1f, Rooted = true, CardLift = 1.4f };
        }

        /// <summary>
        /// 株を一つ。足元・大きさの倍率・向き（度）・頭の傾き（水平の向き。長さが傾きの強さ）。
        /// 札を向きから等しい角度で回して並べ、交差させる
        /// </summary>
        static void Clump(Bank f, Kind k, Vector3 foot, float scale, float yaw, Vector3 lean)
        {
            Vector2 min, max;
            CellUv(k, out min, out max);
            var size = Sizes[(int)k];
            var high = size.x * scale;
            var wide = size.y * scale;
            var cards = Mathf.Max(1, (int)size.z);
            f.RootY = foot.y;
            f.RootHigh = high;
            var up = (Vector3.up + lean).normalized * high;
            for (var c = 0; c < cards; c++)
            {
                var a = (yaw + 180f * c / cards) * Mathf.Deg2Rad;
                var across = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (wide * 0.5f);
                f.AtlasCard(foot + Vector3.down * 0.03f, across, up, min, max);
            }
        }

        /// <summary>
        /// 平らな札を一枚。壁や塀に這わせるつる、エスパリエの葉に使う。
        /// root は下辺の中、across は下辺の半分、up は丈。uv は升の中の縦の範囲（0〜1）を切り出せる
        /// </summary>
        static void Flat(Bank f, Kind k, Vector3 root, Vector3 across, Vector3 up, float v0 = 0f, float v1 = 1f)
        {
            Vector2 min, max;
            CellUv(k, out min, out max);
            var lo = Mathf.Lerp(min.y, max.y, v0);
            var hi = Mathf.Lerp(min.y, max.y, v1);
            f.RootY = root.y;
            f.RootHigh = Mathf.Max(0.3f, up.magnitude);
            f.AtlasCard(root, across, up, new Vector2(min.x, lo), new Vector2(max.x, hi));
        }

        // ---- 花の縁 ---------------------------------------------------------------------

        /// <summary>
        /// 花の縁。手前の縁と奥の縁を、z（<see cref="AlongZ"/>）か x に沿った関数で持つ。
        /// s は手前 0 から奥 1。<see cref="TwoSided"/> なら両縁が手前で、真ん中が高い
        /// </summary>
        sealed class Border
        {
            public string Name;
            public bool AlongZ;
            public float From;
            public float To;
            public System.Func<float, float> Front;
            public System.Func<float, float> Back;
            public bool TwoSided;
            public Palette Plan;
            public int Seed;

            public Vector2 At(float t, float s)
            {
                var c = Mathf.LerpUnclamped(Front(t), Back(t), s);
                return AlongZ ? new Vector2(c, t) : new Vector2(t, c);
            }

            public bool Contains(Vector2 p)
            {
                var t = AlongZ ? p.y : p.x;
                if (t < From || t > To) return false;
                var a = Front(t);
                var b = Back(t);
                var c = AlongZ ? p.x : p.y;
                return c >= Mathf.Min(a, b) && c <= Mathf.Max(a, b);
            }

            /// <summary>t での手前から奥への向き（水平）</summary>
            public Vector3 Inward(float t)
            {
                var d = Back(t) - Front(t);
                return AlongZ ? new Vector3(Mathf.Sign(d), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(d));
            }
        }

        /// <summary>段ごとの植える物と重み。重みが大きいほど多く出る</summary>
        sealed class Palette
        {
            public Kind[] Tall = new Kind[0];
            public float[] TallW = new float[0];
            public Kind[] Mid = new Kind[0];
            public float[] MidW = new float[0];
            public Kind[] Low = new Kind[0];
            public float[] LowW = new float[0];
        }

        /// <summary>片割れの裏庭の主な花の縁。白・淡いピンク・青紫に、黄（ルドベキア・レディースマントル）と濃い赤（ダリア）を少し</summary>
        static readonly Palette MainPlan = new Palette
        {
            Tall = new[] { Kind.HollyPink, Kind.HollyWhite, Kind.Delph, Kind.Foxglove },
            TallW = new[] { 3f, 3f, 1.8f, 1.2f },
            Mid = new[] { Kind.EchWhite, Kind.EchPink, Kind.DahliaPink, Kind.Aster, Kind.Allium, Kind.Rudbeckia, Kind.DahliaRed },
            MidW = new[] { 3f, 2.4f, 2f, 2.6f, 1.1f, 1.0f, 0.6f },
            Low = new[] { Kind.Catmint, Kind.Geranium, Kind.Lavender, Kind.Mantle, Kind.Sage, Kind.Filler },
            LowW = new[] { 3f, 3f, 1.2f, 1.5f, 0.5f, 0.6f },
        };

        /// <summary>小路と芝の間の細い縁。背の高い物は置かず、中くらいを真ん中に</summary>
        static readonly Palette NarrowPlan = new Palette
        {
            Mid = new[] { Kind.EchWhite, Kind.Aster, Kind.DahliaPink, Kind.Allium, Kind.EchPink, Kind.Rudbeckia },
            MidW = new[] { 2.5f, 2.5f, 1.5f, 1.2f, 1.5f, 0.6f },
            Low = new[] { Kind.Catmint, Kind.Geranium, Kind.Mantle, Kind.Lavender },
            LowW = new[] { 3f, 2.5f, 2f, 1f },
        };

        /// <summary>エスパリエの前。塀の葉を隠さないよう、背の高い物を置かない</summary>
        static readonly Palette LowPlan = new Palette
        {
            Mid = new[] { Kind.EchWhite, Kind.Aster, Kind.Allium },
            MidW = new[] { 2f, 2f, 1f },
            Low = new[] { Kind.Geranium, Kind.Mantle, Kind.Catmint, Kind.Filler },
            LowW = new[] { 3f, 2f, 2f, 1f },
        };

        /// <summary>前庭。路地から見せる庭なので、タチアオイを家の壁ぎわに、ラベンダーを手前に多く</summary>
        static readonly Palette FrontPlan = new Palette
        {
            Tall = new[] { Kind.HollyPink, Kind.HollyWhite, Kind.Delph },
            TallW = new[] { 2.5f, 2.5f, 1f },
            Mid = new[] { Kind.Rosemary, Kind.EchWhite, Kind.DahliaPink, Kind.Aster, Kind.Hydrangea },
            MidW = new[] { 1f, 1.5f, 1.2f, 1f, 1.4f },
            Low = new[] { Kind.Lavender, Kind.Catmint, Kind.Geranium, Kind.Mantle, Kind.Sage },
            LowW = new[] { 3f, 2f, 1.5f, 1.5f, 0.8f },
        };

        /// <summary>家の脇の日陰。アジサイとローズマリーの低木</summary>
        static readonly Palette ShadePlan = new Palette
        {
            Mid = new[] { Kind.Hydrangea, Kind.Rosemary },
            MidW = new[] { 3f, 1.2f },
            Low = new[] { Kind.Geranium, Kind.Sage, Kind.Filler },
            LowW = new[] { 2f, 1f, 1f },
        };

        /// <summary>奥の生け垣の根元。花の終わった穂と葉の株</summary>
        static readonly Palette HedgePlan = new Palette
        {
            Tall = new[] { Kind.Foxglove, Kind.Delph, Kind.HollyWhite },
            TallW = new[] { 2f, 1f, 1f },
            Low = new[] { Kind.Filler, Kind.Mantle, Kind.Geranium },
            LowW = new[] { 2f, 1f, 1f },
        };

        /// <summary>組み立ての度に作り直す花の縁の一覧。地面の絵・札・当たりが同じ線を読む</summary>
        static List<Border> Borders()
        {
            var all = new List<Border>();
            // 西の縁。板の塀と小路の間。格子戸の脇はエスパリエの前なので低く
            all.Add(new Border
            {
                Name = "WestLow", AlongZ = true, From = GateZ + 0.35f, To = 18.0f,
                Front = z => PathX(z) - PathWide * 0.5f - 0.05f, Back = z => PlotWest + 0.45f, Plan = LowPlan, Seed = 101,
            });
            all.Add(new Border
            {
                Name = "West", AlongZ = true, From = 18.0f, To = 30.8f,
                Front = z => PathX(z) - PathWide * 0.5f - 0.05f, Back = z => PlotWest + 0.12f, Plan = MainPlan, Seed = 103,
            });
            // 小路と芝の間。両側から見る
            all.Add(new Border
            {
                Name = "Middle", AlongZ = true, From = 19.0f, To = LawnNorth,
                Front = z => PathX(z) + PathWide * 0.5f + 0.05f, Back = z => LawnWest, TwoSided = true, Plan = NarrowPlan, Seed = 107,
            });
            // 芝の東。奥は菜園
            all.Add(new Border
            {
                Name = "East", AlongZ = true, From = LawnSouth, To = 27.2f,
                Front = z => LawnEast, Back = z => EastBorderBack, Plan = MainPlan, Seed = 109,
            });
            // 奥の生け垣の根元。リンゴの入り込みの奥
            all.Add(new Border
            {
                Name = "Back", AlongZ = false, From = -3.7f, To = AlcoveEast,
                Front = x => 33.9f, Back = x => PlotNorth - 0.05f, Plan = HedgePlan, Seed = 113,
            });
            // 前庭。玄関の小路の西と東、脇の小路の西、家の西の脇
            all.Add(new Border
            {
                Name = "FrontWest", AlongZ = false, From = HouseWest - 0.05f, To = FrontDoorX - PathWide * 0.5f - 0.08f,
                Front = x => NorthEdge + FrontWallThick + 0.1f, Back = x => HouseFront - 0.25f, Plan = FrontPlan, Seed = 127,
            });
            all.Add(new Border
            {
                Name = "FrontEast", AlongZ = false, From = FrontDoorX + PathWide * 0.5f + 0.08f, To = PlotEast - 0.4f,
                Front = x => NorthEdge + FrontWallThick + 0.1f, Back = x => HouseFront - 0.25f, Plan = FrontPlan, Seed = 131,
            });
            all.Add(new Border
            {
                Name = "Side", AlongZ = true, From = NorthEdge + FrontWallThick + 0.1f, To = GateZ - 0.3f,
                Front = z => SidePathX - PathWide * 0.5f - 0.05f, Back = z => PlotWest + 0.4f, Plan = FrontPlan, Seed = 137,
            });
            all.Add(new Border
            {
                // 家の西の脇。脇の小路と家の西の壁の間を、前庭の石垣から格子戸の塀まで。
                // 背の高い物を置かず、格子戸へ向かう目線を塞がない
                Name = "Shade", AlongZ = true, From = NorthEdge + FrontWallThick + 0.1f, To = GateZ - 0.3f,
                Front = z => SidePathX + PathWide * 0.5f + 0.05f, Back = z => HouseWest - 0.12f, Plan = ShadePlan, Seed = 139,
            });
            // テラスの縁のラベンダー。芝へ下りる段の所は開ける
            all.Add(new Border
            {
                Name = "LavenderW", AlongZ = false, From = TerraceWest + 0.1f, To = StepWest - 0.05f,
                Front = x => LawnSouth - 0.05f, Back = x => TerraceNorth + 0.05f, TwoSided = true, Plan = LavenderPlan, Seed = 149,
            });
            all.Add(new Border
            {
                Name = "LavenderE", AlongZ = false, From = StepEast + 0.05f, To = LawnEast + 0.9f,
                Front = x => LawnSouth - 0.05f, Back = x => TerraceNorth + 0.05f, TwoSided = true, Plan = LavenderPlan, Seed = 151,
            });
            return all;
        }

        static readonly Palette LavenderPlan = new Palette
        {
            Low = new[] { Kind.Lavender },
            LowW = new[] { 1f },
        };

        /// <summary>花の縁をまとまりごとに植える。花の縁一つを一枚の mesh に焼く</summary>
        static void Plant(Transform parent, Border border, Material mat)
        {
            var f = FloraBank();
            Sow(f, border);
            Emit(parent, "Flora" + border.Name, f, mat, false);
        }

        /// <summary>
        /// 花の縁一つ分の株を入れ物へ溜める。周りの家の庭は、家ごとに幾つもの縁を一つの入れ物へ溜めて一枚に焼く
        /// （描く回数を抑えるため。<c>BuildVillageYards.cs</c>）
        /// </summary>
        static void Sow(Bank f, Border border)
        {
            var seed = border.Seed;
            var n = 0;
            // 段ごとに、縁に沿ってまとまりを並べる
            if (border.TwoSided)
            {
                Band(f, border, border.Plan.Low, border.Plan.LowW, -0.14f, 0.28f, 0.95f, 1.6f, ref seed, ref n, true);
                Band(f, border, border.Plan.Low, border.Plan.LowW, 0.72f, 1.14f, 0.95f, 1.6f, ref seed, ref n, true);
                Band(f, border, border.Plan.Mid, border.Plan.MidW, 0.28f, 0.72f, 1.0f, 1.6f, ref seed, ref n, false);
            }
            else
            {
                var hasTall = border.Plan.Tall.Length > 0;
                var hasMid = border.Plan.Mid.Length > 0;
                var lowTo = hasMid || hasTall ? 0.32f : 1.0f;
                Band(f, border, border.Plan.Low, border.Plan.LowW, -0.16f, lowTo, 0.9f, 1.6f, ref seed, ref n, true);
                if (hasMid) Band(f, border, border.Plan.Mid, border.Plan.MidW, lowTo, hasTall ? 0.64f : 1.0f, 1.0f, 1.7f, ref seed, ref n, false);
                if (hasTall) Band(f, border, border.Plan.Tall, border.Plan.TallW, hasMid ? 0.64f : lowTo, 0.98f, 0.9f, 1.5f, ref seed, ref n, false);
            }
            // 株の根元を埋める葉。地の土が株の隙間から見えすぎないように
            var len = border.To - border.From;
            var fill = Mathf.RoundToInt(len * 2.2f);
            for (var i = 0; i < fill; i++)
            {
                var t = border.From + len * (i + Hash(seed, i)) / fill;
                var s = 0.3f + Hash(seed + 1, i) * 0.6f;
                var p = border.At(t, s);
                Clump(f, Kind.Filler, new Vector3(p.x, 0f, p.y), 0.8f + Hash(seed + 2, i) * 0.4f, Hash(seed + 3, i) * 180f, Vector3.zero);
                n++;
            }
        }

        /// <summary>
        /// 一つの段を、縁に沿ってまとまり（ドリフト）で埋める。
        /// まとまりは縁に沿って長く（帯状に）、隣のまとまりとは端を重ねる。同じ物は続けない。
        /// 株は 3 か 5（長いまとまりは 7）。spill なら頭を手前へ傾けて小路へこぼす
        /// </summary>
        static void Band(Bank f, Border border, Kind[] kinds, float[] weights, float s0, float s1, float lenMin, float lenMax,
            ref int seed, ref int count, bool spill)
        {
            if (kinds.Length == 0) return;
            var t = border.From;
            var prev = -1;
            var i = 0;
            while (t < border.To - 0.2f)
            {
                var len = Mathf.Lerp(lenMin, lenMax, Hash(seed, i * 7 + 1));
                var pick = Pick(weights, Hash(seed, i * 7 + 2));
                if (pick == prev && kinds.Length > 1) pick = (pick + 1) % kinds.Length;
                prev = pick;
                var kind = kinds[pick];
                var size = Sizes[(int)kind];
                var width = Mathf.Abs(border.Back(t) - border.Front(t)) * (s1 - s0);
                var area = Mathf.Max(0.3f, len * Mathf.Max(0.35f, width));
                var per = size.y * size.y * 0.55f;
                var m = Mathf.Clamp(Mathf.RoundToInt(area / per), 3, 7);
                if (m % 2 == 0) m += 1;
                for (var k = 0; k < m; k++)
                {
                    var u = Hash(seed, i * 31 + k * 3);
                    var v = Hash(seed, i * 31 + k * 3 + 1);
                    // 端を 0.25 m ずつ外へ出して、隣のまとまりに食い込ませる
                    var tt = Mathf.Clamp(t - 0.25f + (len + 0.5f) * (k + u) / m, border.From, border.To);
                    var s = Mathf.Lerp(s0, s1, v);
                    // 根は縁から 0.15 m 以上内に置く。こぼれるのは株の広がりと頭の傾きの分だけにして、
                    // 小路の真ん中まで塞がないようにする
                    var depth = Mathf.Max(0.3f, Mathf.Abs(border.Back(tt) - border.Front(tt)));
                    var edge = 0.15f / depth;
                    s = border.TwoSided ? Mathf.Clamp(s, edge, 1f - edge) : Mathf.Max(s, edge);
                    var p = border.At(tt, s);
                    var lean = Vector3.zero;
                    if (spill && s < 0.3f) lean = -border.Inward(tt) * 0.28f;
                    if (spill && border.TwoSided && s > 0.7f) lean = border.Inward(tt) * 0.28f;
                    var scale = 0.82f + Hash(seed, i * 31 + k * 3 + 2) * 0.36f;
                    Clump(f, kind, new Vector3(p.x, 0f, p.y), scale, Hash(seed + 5, i * 31 + k) * 180f, lean);
                    count++;
                }
                t += len;
                i++;
            }
            seed += 17;
        }

        static int Pick(float[] weights, float r)
        {
            var total = 0f;
            foreach (var w in weights) total += w;
            var x = r * total;
            for (var i = 0; i < weights.Length; i++)
            {
                if (x < weights[i]) return i;
                x -= weights[i];
            }
            return weights.Length - 1;
        }

        // ---- 組み立て -------------------------------------------------------------------

        /// <summary>花の縁・つる・鉢・菜園・木。花の縁ごとと、つる・鉢・木ごとに一枚ずつ焼く</summary>
        static void Plants(Transform parent)
        {
            var mat = FloraMat();
            var borders = Borders();
            foreach (var border in borders) Plant(parent, border, mat);
            Climbers(parent, mat);
            Pots(parent, mat);
            Trees(parent, mat);
            BorderBounds(Child(parent, "Bounds"), borders);
        }

        /// <summary>
        /// つる。アーチのバラとクレマチス、家の壁のバラ（玄関と裏口のまわり）とクレマチス、
        /// 東屋のハニーサックル、石垣と塀のアイビー、菜園の支柱のスイートピー
        /// </summary>
        static void Climbers(Transform parent, Material mat)
        {
            var f = FloraBank();
            f.CardLift = 0.8f;
            // アーチ。両脇の柱に三段、頭の弧に沿って
            Vector3 centre, across, ahead;
            ArchPose(out centre, out across, out ahead);
            // 柱に沿って五段。横の格子の面に一枚、柱を巻くように交差の札をもう二枚。
            // 骨の白が見えすぎると、花のアーチではなく園芸店の売り物に見える
            for (var s = -1; s <= 1; s += 2)
            {
                for (var k = 0; k < 5; k++)
                {
                    var root = centre + across * (0.78f * s) + Vector3.up * (k * 0.45f);
                    var kind = (k + (s > 0 ? 1 : 0)) % 3 == 1 ? Kind.Clematis : Kind.Roses;
                    Flat(f, kind, root + across * (0.04f * s) - ahead * 0.05f, ahead * 0.48f, Vector3.up * 0.8f);
                    Flat(f, kind, root + ahead * 0.22f, across * 0.26f, Vector3.up * 0.75f);
                    Flat(f, kind, root - ahead * 0.22f, across * 0.26f, Vector3.up * 0.75f);
                }
            }
            for (var k = 0; k < 9; k++)
            {
                var a = Mathf.PI * (k + 0.5f) / 9f;
                var p = centre + across * (Mathf.Cos(a) * 0.78f) + Vector3.up * (2.1f + Mathf.Sin(a) * 0.7f);
                var tangent = (across * -Mathf.Sin(a) + Vector3.up * Mathf.Cos(a) * 0.9f).normalized;
                var outward = (across * Mathf.Cos(a) + Vector3.up * Mathf.Sin(a)).normalized;
                Flat(f, k % 3 == 1 ? Kind.Clematis : Kind.Roses, p - outward * 0.25f, ahead * 0.55f, outward * 0.6f);
                Flat(f, Kind.Roses, p - tangent * 0.28f, ahead * 0.5f, tangent * 0.56f);
            }

            // 玄関のまわりのバラ。戸の両脇から庇の上へ
            WallRose(f, new Vector3(FrontDoorX, 0f, HouseFront - 0.07f), Vector3.right, 1f, Kind.Roses);
            // 裏口のまわりのバラ
            WallRose(f, new Vector3(BackDoorX, 0f, HouseRear + 0.07f), Vector3.left, 1f, Kind.Roses);
            // 西の妻のクレマチスと、裏の壁の西寄りのハニーサックル
            for (var i = 0; i < 3; i++)
                Flat(f, Kind.Clematis, new Vector3(HouseWest - 0.07f, 0.1f + i * 0.9f, 14.0f - i * 0.25f), new Vector3(0f, 0f, 0.55f), Vector3.up * 1.0f);
            for (var i = 0; i < 3; i++)
                Flat(f, Kind.Honeysuckle, new Vector3(0.9f + i * 0.2f, TerraceTop + i * 0.85f, HouseRear + 0.07f), new Vector3(0.5f, 0f, 0f), Vector3.up * 0.95f);

            // 東屋の南の二本の柱と軒にハニーサックルとバラ
            var c = GazeboAt;
            var h = GazeboHalf * 0.96f;
            foreach (var sx in new[] { -1f, 1f })
            {
                var post = c + new Vector3(sx * h, 0f, -h);
                var kind = sx < 0f ? Kind.Honeysuckle : Kind.Roses;
                for (var k = 0; k < 3; k++)
                {
                    Flat(f, kind, post + new Vector3(0f, 0.1f + k * 0.75f, -0.08f), new Vector3(0.32f, 0f, 0f), Vector3.up * 0.85f);
                    Flat(f, kind, post + new Vector3(-sx * 0.08f, 0.1f + k * 0.75f, 0f), new Vector3(0f, 0f, 0.32f), Vector3.up * 0.85f);
                }
                Flat(f, kind, post + new Vector3(-sx * 0.45f, 2.1f, -0.1f), new Vector3(0.6f, 0f, 0f), Vector3.up * 0.6f);
            }

            // アイビー。東の野石の塀の内の面に、塊を点々と。前庭の塀と格子戸の塀にも
            for (var i = 0; i < 9; i++)
            {
                var z = GateZ + 1.0f + i * 2.5f + Hash(211, i) * 1.2f;
                if (z > PlotNorth - 0.6f) break;
                var wide = 0.7f + Hash(213, i) * 0.7f;
                Flat(f, Kind.Ivy, new Vector3(PlotEast - 0.02f, 0f, z), new Vector3(0f, 0f, wide), Vector3.up * (0.9f + Hash(215, i) * 0.8f));
            }
            Flat(f, Kind.Ivy, new Vector3(-6.2f, 0f, GateZ - 0.2f), new Vector3(0.7f, 0f, 0f), Vector3.up * 1.6f);
            Flat(f, Kind.Ivy, new Vector3(-2.3f, 0f, GateZ + 0.2f), new Vector3(0.6f, 0f, 0f), Vector3.up * 1.5f);

            // スイートピー。奥の畝の支柱の列に
            var far = VegBeds[1];
            for (var i = 0; i < 3; i++)
            {
                var z = Mathf.Lerp(far.yMin + 0.7f, far.yMax - 0.7f, i / 2f);
                Flat(f, Kind.SweetPea, new Vector3(far.xMax - 0.22f, 0.22f, z), new Vector3(0f, 0f, 0.62f), Vector3.up * 1.72f);
            }
            Emit(parent, "FloraClimbers", f, mat, false);
        }

        /// <summary>戸のまわりに這わせるバラ。戸の両脇に二段ずつと、上に一枚。along は壁に沿った横の向き</summary>
        static void WallRose(Bank f, Vector3 door, Vector3 along, float scale, Kind kind)
        {
            foreach (var s in new[] { -1f, 1f })
                for (var k = 0; k < 2; k++)
                    Flat(f, kind, door + along * (s * 0.82f) + Vector3.up * (0.05f + k * 1.0f), along * 0.30f, Vector3.up * 1.05f * scale);
            Flat(f, kind, door + Vector3.up * 2.05f, along * 0.95f, Vector3.up * 0.7f);
        }

        /// <summary>アーチの芯と、小路を横切る向きと、進む向き（BuildVillageGarden.ArchFrame と同じ値）</summary>
        static void ArchPose(out Vector3 centre, out Vector3 across, out Vector3 ahead)
        {
            centre = new Vector3(PathX(ArchZ), 0f, ArchZ);
            ahead = (new Vector3(PathX(ArchZ + 0.3f), 0f, ArchZ + 0.3f) - new Vector3(PathX(ArchZ - 0.3f), 0f, ArchZ - 0.3f)).normalized;
            across = Vector3.Cross(Vector3.up, ahead).normalized;
        }

        /// <summary>鉢物。テラスの鉢と窓の花箱と吊り鉢のペラルゴニウム、温室のトマト、菜園のレタス</summary>
        static void Pots(Transform parent, Material mat)
        {
            var f = FloraBank();
            var i = 0;
            foreach (var at in TerracePots)
            {
                Clump(f, Kind.Pelargonium, at, 1.1f, Hash(301, i) * 180f, Vector3.zero);
                Clump(f, Kind.Pelargonium, at + new Vector3(0.05f, 0f, -0.04f), 0.9f, 45f + Hash(303, i) * 180f, Vector3.zero);
                i++;
            }
            // 窓の花箱。外へ向けた札を並べ、交差の札を足す
            foreach (var w in FrontWindows)
            {
                if (w.z > 1.5f) continue;
                for (var k = 0; k < 3; k++)
                {
                    var x = w.x - w.y * 0.35f + k * w.y * 0.35f;
                    Clump(f, Kind.Pelargonium, new Vector3(x, w.z + 0.17f, HouseFront - 0.2f), 1.05f, 90f + Hash(305, k) * 30f, Vector3.zero);
                }
            }
            // 吊り鉢
            Clump(f, Kind.Pelargonium, HangingBasketAt, 1.0f, 20f, Vector3.zero);
            Clump(f, Kind.Filler, HangingBasketAt + Vector3.down * 0.25f, 0.55f, 70f, Vector3.zero);
            // 温室のトマト。緑に赤い実の絵を小さく
            foreach (var at in GreenhousePots)
                Clump(f, Kind.Apple, at, 0.22f, Hash(307, (int)(at.z * 10f)) * 180f, Vector3.zero);
            // 奥の畝のレタスの列
            var far = VegBeds[1];
            for (var r = 0; r < 2; r++)
                for (var k = 0; k < 6; k++)
                    Clump(f, Kind.Filler, new Vector3(far.xMin + 0.35f + r * 0.45f, 0.24f, far.yMin + 0.3f + k * 0.5f), 0.42f, k * 37f, Vector3.zero);
            NoShadow(Emit(parent, "FloraPots", f, mat, false));
        }

        /// <summary>
        /// 木。リンゴの樹冠（立てた札を三枚と、寝かせた札を一枚）と、塀のエスパリエの葉と実
        /// </summary>
        static void Trees(Transform parent, Material mat)
        {
            var f = FloraBank();
            f.CardLift = 2.0f;
            var crown = AppleAt + new Vector3(0.05f, 1.35f, 0.02f);
            Clump(f, Kind.Apple, crown, 1.0f, 15f, Vector3.zero);
            Clump(f, Kind.Apple, crown + new Vector3(0.3f, 0.5f, -0.2f), 0.7f, 50f, Vector3.zero);
            // 寝かせた札。上から見ても丸い樹冠に見えるように
            Vector2 min, max;
            CellUv(Kind.Apple, out min, out max);
            f.RootY = crown.y;
            f.RootHigh = 3f;
            f.AtlasCard(crown + new Vector3(0f, 1.6f, -1.5f), new Vector3(1.5f, 0f, 0f), new Vector3(0f, 0f, 3.0f), min, max);

            // エスパリエ。段ごとに、塀に沿った平らな札を四枚
            var x = PlotWest + 0.2f;
            foreach (var y in EspalierTiers)
                for (var k = 0; k < 4; k++)
                {
                    var z = EspalierZ - EspalierHalf + (k + 0.5f) * EspalierHalf * 0.5f;
                    Flat(f, Kind.Apple, new Vector3(x, y - 0.2f, z), new Vector3(0f, 0f, EspalierHalf * 0.27f), Vector3.up * 0.44f, 0.30f + 0.08f * (k % 3), 0.58f + 0.08f * (k % 3));
                }
            Emit(parent, "FloraTrees", f, mat, false);
        }

        /// <summary>
        /// 花の縁の中へ入れない見えない当たり。人の歩ける側（小路・芝・テラス）に面した縁に沿って立てる
        /// </summary>
        static void BorderBounds(Transform parent, List<Border> borders)
        {
            foreach (var b in borders)
            {
                var line = new List<Vector2>();
                var n = Mathf.Max(1, Mathf.CeilToInt((b.To - b.From) / 0.8f));
                for (var i = 0; i <= n; i++) line.Add(b.At(Mathf.Lerp(b.From, b.To, i / (float)n), 0.1f));
                Edge(parent, b.Name + "Front", line, 1.1f);
                if (b.TwoSided)
                {
                    var back = new List<Vector2>();
                    for (var i = 0; i <= n; i++) back.Add(b.At(Mathf.Lerp(b.From, b.To, i / (float)n), 0.9f));
                    Edge(parent, b.Name + "Back", back, 1.1f);
                }
                // 両端
                Wall(parent, b.Name + "End0", ToV3(b.At(b.From, 0.1f)), ToV3(b.At(b.From, b.TwoSided ? 0.9f : 1f)), 1.1f, 0.2f);
                Wall(parent, b.Name + "End1", ToV3(b.At(b.To, 0.1f)), ToV3(b.At(b.To, b.TwoSided ? 0.9f : 1f)), 1.1f, 0.2f);
            }
        }

        static Vector3 ToV3(Vector2 p) { return new Vector3(p.x, 0f, p.y); }

        // ---- 地面の絵 -------------------------------------------------------------------

        /// <summary>地面の絵が覆う広がり。敷地の石垣から奥の生け垣の下まで</summary>
        const float PicWest = PlotWest - 0.4f;
        const float PicEast = PlotEast + 0.4f;
        const float PicSouth = NorthEdge;
        const float PicNorth = PlotNorth + 1.0f;
        const string GroundPath = Textures + "VillageGround.png";
        /// <summary>描き方の版。花の縁や芝の寸法を変えたら上げる。上げないと前の絵のまま貼られる</summary>
        const string GroundSign = "ground3|512x1024";

        static Material GroundMat()
        {
            var m = Paint("VillageGround", Color.white, 0.04f);
            m.SetTexture("_BaseMap", GroundPicture());
            EditorUtility.SetDirty(m);
            return m;
        }

        // 地面の色。sRGB
        static readonly Color LawnDark = new Color(0.25f, 0.34f, 0.13f);
        static readonly Color LawnLight = new Color(0.33f, 0.42f, 0.17f);
        static readonly Color LawnDry = new Color(0.44f, 0.43f, 0.22f);
        static readonly Color RoughGrass = new Color(0.27f, 0.33f, 0.15f);
        static readonly Color SoilCol = new Color(0.20f, 0.15f, 0.10f);
        static readonly Color WornCol = new Color(0.34f, 0.29f, 0.20f);

        /// <summary>
        /// 敷地の地面の絵。芝は刈り込みの縞（イギリスの芝の一番早い目印）を入れ、8 月の乾きで斑に黄ばませる。
        /// 花の縁と菜園の下は土。物置と堆肥箱の前は踏まれて禿げる。
        /// 描いた絵は取り込みの userData に版を持ち、版が同じなら描き直さない（公園の地面の絵と同じ作り）
        /// </summary>
        static Texture2D GroundPicture()
        {
            var importer = AssetImporter.GetAtPath(GroundPath) as TextureImporter;
            if (importer != null && importer.userData == GroundSign && System.IO.File.Exists(GroundPath))
                return AssetDatabase.LoadAssetAtPath<Texture2D>(GroundPath);
            const int w = 512;
            const int h = 1024;
            var borders = Borders();
            var px = new Color32[w * h];
            for (var j = 0; j < h; j++)
            {
                var z = Mathf.Lerp(PicSouth, PicNorth, (j + 0.5f) / h);
                for (var i = 0; i < w; i++)
                {
                    var x = Mathf.Lerp(PicWest, PicEast, (i + 0.5f) / w);
                    var c = GroundAt(new Vector2(x, z), borders);
                    px[j * w + i] = new Color32((byte)Mathf.Clamp(c.r * 255f, 0, 255), (byte)Mathf.Clamp(c.g * 255f, 0, 255), (byte)Mathf.Clamp(c.b * 255f, 0, 255), 255);
                }
            }
            var pic = new Texture2D(w, h, TextureFormat.RGB24, false, false);
            pic.SetPixels32(px);
            pic.Apply();
            System.IO.File.WriteAllBytes(GroundPath, pic.EncodeToPNG());
            Object.DestroyImmediate(pic);
            AssetDatabase.ImportAsset(GroundPath);
            importer = AssetImporter.GetAtPath(GroundPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = 1024;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.userData = GroundSign;
                importer.SaveAndReimport();
            }
            Debug.Log("村の敷地の地面の絵を描いた: " + GroundPath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(GroundPath);
        }

        static Color GroundAt(Vector2 p, List<Border> borders)
        {
            var big = SkyPaint.Noise(new Vector3(p.x * 0.35f, 0.3f, p.y * 0.35f), 3);
            var fine = SkyPaint.Noise(new Vector3(p.x * 2.6f, 1.7f, p.y * 2.6f), 5);
            var grain = SkyPaint.Noise(new Vector3(p.x * 9f, 4.1f, p.y * 9f), 9);
            // 花の縁と菜園は土。縁は細かな斑で揺らす
            foreach (var b in borders)
                if (b.Contains(p))
                    return Color.Lerp(SoilCol, RoughGrass, Mathf.Clamp01(fine * 0.6f - 0.2f)) * (0.9f + grain * 0.2f);
            foreach (var r in VegBeds)
                if (r.Contains(p)) return SoilCol * (0.9f + grain * 0.2f);
            var lawn = InLawn(p);
            Color c;
            if (lawn)
            {
                // 刈り込みの縞。0.55 m 幅で南北に
                var stripe = Mathf.Repeat(p.x - LawnWest, 1.1f) < 0.55f ? 1f : 0f;
                c = Color.Lerp(LawnDark, LawnLight, 0.35f + stripe * 0.4f + (big - 0.5f) * 0.3f);
                var dry = Mathf.Clamp01((big * 0.8f + fine * 0.4f - 0.72f) * 3f);
                c = Color.Lerp(c, LawnDry, dry * 0.6f);
            }
            else
            {
                c = Color.Lerp(RoughGrass * 0.9f, RoughGrass * 1.1f, big);
            }
            c *= 0.94f + grain * 0.12f;
            // 物置と堆肥箱と温室の前は踏まれて禿げる
            var worn = 0f;
            worn = Mathf.Max(worn, 1f - SkyPaint.Smooth(0.2f, 0.9f + fine * 0.3f, Vector2.Distance(p, new Vector2((ShedWest + ShedEast) * 0.5f, ShedSouth - 0.5f))));
            worn = Mathf.Max(worn, 1f - SkyPaint.Smooth(0.2f, 0.8f + fine * 0.3f, Vector2.Distance(p, new Vector2(6.1f, 27.4f))));
            worn = Mathf.Max(worn, 1f - SkyPaint.Smooth(0.2f, 0.7f + fine * 0.3f, Vector2.Distance(p, new Vector2((GlassWest + GlassEast) * 0.5f, GlassSouth - 0.4f))));
            // 物干しの下と、テラスの段の下
            worn = Mathf.Max(worn, (1f - SkyPaint.Smooth(0.1f, 0.5f, Vector2.Distance(p, new Vector2(AirerAt.x, AirerAt.z)))) * 0.8f);
            worn = Mathf.Max(worn, (1f - SkyPaint.Smooth(0.1f, 0.7f, Mathf.Abs(p.y - (TerraceNorth + 0.6f)) + Mathf.Max(0f, Mathf.Abs(p.x - 1f) - 0.6f))) * 0.5f);
            c = Color.Lerp(c, WornCol, Mathf.Clamp01(worn) * (0.6f + fine * 0.4f));
            // 塀と生け垣の根元は暗い
            var edge = Mathf.Min(Mathf.Min(p.x - PlotWest, PlotEast - p.x), PlotNorth - p.y);
            c *= Mathf.Lerp(0.75f, 1f, SkyPaint.Smooth(0f, 0.6f, edge));
            c.a = 1f;
            return c;
        }

        /// <summary>芝の上か。角を 0.6 m 丸めた四角と、奥のリンゴの入り込み</summary>
        static bool InLawn(Vector2 p)
        {
            return InRounded(p, LawnWest, LawnEast, LawnSouth, LawnNorth + 0.3f, 0.6f)
                || InRounded(p, LawnWest, AlcoveEast, LawnNorth - 0.5f, 33.9f, 0.5f);
        }

        static bool InRounded(Vector2 p, float x0, float x1, float z0, float z1, float r)
        {
            if (p.x < x0 || p.x > x1 || p.y < z0 || p.y > z1) return false;
            var cx = Mathf.Clamp(p.x, x0 + r, x1 - r);
            var cz = Mathf.Clamp(p.y, z0 + r, z1 - r);
            return Vector2.Distance(p, new Vector2(cx, cz)) <= r;
        }
    }
}
