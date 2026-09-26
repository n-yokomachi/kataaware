using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 周りの家の庭（設計書 2 節の 6）と、路地の家並みの花（前庭・花箱・吊り鉢・玄関のバラ）。
    ///
    /// **片割れの庭より簡素にし、暮らしぶりで分ける。**
    /// <list type="table">
    /// <item><term>家 A</term><description>子どものいる家。芝が広く、ブランコとトランポリンとサッカーの小さなゴール、
    /// 二本の柱に張った長い物干しに色の混じった洗濯物、物置。花は暖かい色（赤・橙・黄）</description></item>
    /// <item><term>家 B</term><description>庭いじりの長い年寄りの家。野菜の畝と豆の支柱、薪の山、冷床、ベンチ、
    /// リンゴの木。花は冷たい色（青・紫・白）。**路地の側の垣は低い**（片割れの裏庭の白いパラソルが覗く）</description></item>
    /// <item><term>家 C・D</term><description>前庭と、路地から見える庭の端（家の脇の板の塀と木戸、その上に覗く木）まで</description></item>
    /// </list>
    /// 植え込みは片割れの庭と同じ <see cref="Border"/> と <see cref="Palette"/> で、家ごとに Palette を替える。
    /// 描く回数を抑えるため、北の家並み・南の家並み・つると鉢・木の四つの入れ物にまとめて焼く
    /// </summary>
    public static partial class BuildVillage
    {
        // ---- 色の升（Swatch） ------------------------------------------------------------

        // 一色で塗る小物の色。一枚の絵に升を並べ、面の uv を升の真ん中に置く。
        // 戸・電話ボックス・郵便ポスト・道標・車・ベンチ・子どもの物を、マテリアル一つ（描く回数一回）で塗る
        const int SwOxblood = 0, SwNavy = 1, SwBlueGrey = 2, SwOchre = 3, SwRed = 4, SwBlack = 5, SwWhite = 6, SwYellow = 7;
        const int SwCarPaint = 8, SwCarGlass = 9, SwTyre = 10, SwChrome = 11, SwBench = 12, SwGalv = 13, SwPane = 14, SwGold = 15;
        const int SwToyBlue = 16, SwToyRed = 17, SwLog = 18, SwPlastic = 19, SwSheet = 20, SwShirt = 21, SwPink = 22, SwLogEnd = 23;

        /// <summary>升の色。sRGB。並びは上の番号と同じ</summary>
        static readonly Color[] Swatches =
        {
            new Color(0.40f, 0.10f, 0.10f), new Color(0.12f, 0.15f, 0.26f), new Color(0.38f, 0.46f, 0.52f), new Color(0.80f, 0.62f, 0.26f),
            new Color(0.66f, 0.07f, 0.05f), new Color(0.04f, 0.04f, 0.045f), new Color(0.90f, 0.89f, 0.86f), new Color(0.92f, 0.74f, 0.08f),
            new Color(0.34f, 0.40f, 0.42f), new Color(0.09f, 0.11f, 0.13f), new Color(0.05f, 0.05f, 0.05f), new Color(0.58f, 0.59f, 0.60f),
            new Color(0.44f, 0.33f, 0.22f), new Color(0.56f, 0.57f, 0.55f), new Color(0.30f, 0.36f, 0.38f), new Color(0.76f, 0.60f, 0.24f),
            new Color(0.12f, 0.32f, 0.70f), new Color(0.86f, 0.30f, 0.12f), new Color(0.40f, 0.28f, 0.18f), new Color(0.22f, 0.46f, 0.24f),
            new Color(0.93f, 0.93f, 0.90f), new Color(0.46f, 0.62f, 0.80f), new Color(0.92f, 0.62f, 0.70f), new Color(0.70f, 0.56f, 0.38f),
        };

        const int SwatchCell = 8;
        const int SwatchCount = 32;
        const string SwatchPath = Textures + "VillageSwatch.png";
        const string SwatchSign = "swatch1";

        static Vector2 SwatchUv(int i)
        {
            return new Vector2((i + 0.5f) / SwatchCount, 0.5f);
        }

        /// <summary>色の升の絵。升を並べた 256×8。組み立ての度に色が変わっていれば描き直す</summary>
        static Texture2D SwatchPicture()
        {
            var sign = SwatchSign;
            foreach (var c in Swatches) sign += "|" + ColorUtility.ToHtmlStringRGB(c);
            var importer = AssetImporter.GetAtPath(SwatchPath) as TextureImporter;
            if (importer != null && importer.userData == sign && System.IO.File.Exists(SwatchPath))
                return AssetDatabase.LoadAssetAtPath<Texture2D>(SwatchPath);
            var w = SwatchCell * SwatchCount;
            var h = SwatchCell;
            var px = new Color32[w * h];
            for (var i = 0; i < SwatchCount; i++)
            {
                var c = (Color32)(i < Swatches.Length ? Swatches[i] : Color.magenta);
                for (var y = 0; y < h; y++)
                    for (var x = 0; x < SwatchCell; x++)
                        px[y * w + i * SwatchCell + x] = c;
            }
            var pic = new Texture2D(w, h, TextureFormat.RGB24, false, false);
            pic.SetPixels32(px);
            pic.Apply();
            System.IO.File.WriteAllBytes(SwatchPath, pic.EncodeToPNG());
            Object.DestroyImmediate(pic);
            AssetDatabase.ImportAsset(SwatchPath);
            importer = AssetImporter.GetAtPath(SwatchPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                // 升の縁が滲まないよう、mipmap を作らず点で引く
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.userData = sign;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(SwatchPath);
        }

        static Material SwatchMat()
        {
            var m = Paint("VillageSwatch", Color.white, 0.28f);
            m.SetTexture("_BaseMap", SwatchPicture());
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>升の色で塗った向きを持つ箱。面の並びは <see cref="Bank.Box(Vector3, Vector3, Quaternion)"/> と同じ</summary>
        static void Tint(Bank b, Vector3 centre, Vector3 size, Quaternion rot, int swatch)
        {
            var uv = SwatchUv(swatch);
            var h = size * 0.5f;
            System.Func<float, float, float, Vector3> c = (sx, sy, sz) => centre + rot * new Vector3(h.x * sx, h.y * sy, h.z * sz);
            b.Patch(c(1, -1, 1), c(1, -1, -1), c(1, 1, -1), c(1, 1, 1), uv, uv, uv, uv);
            b.Patch(c(-1, -1, -1), c(-1, -1, 1), c(-1, 1, 1), c(-1, 1, -1), uv, uv, uv, uv);
            b.Patch(c(-1, 1, 1), c(1, 1, 1), c(1, 1, -1), c(-1, 1, -1), uv, uv, uv, uv);
            b.Patch(c(-1, -1, -1), c(1, -1, -1), c(1, -1, 1), c(-1, -1, 1), uv, uv, uv, uv);
            b.Patch(c(-1, -1, 1), c(1, -1, 1), c(1, 1, 1), c(-1, 1, 1), uv, uv, uv, uv);
            b.Patch(c(1, -1, -1), c(-1, -1, -1), c(-1, 1, -1), c(1, 1, -1), uv, uv, uv, uv);
        }

        /// <summary>升の色の面を一枚。outward の側を表にする</summary>
        static void TintFace(Bank b, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 outward, int swatch)
        {
            var uv = SwatchUv(swatch);
            if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), outward) >= 0f) b.Patch(p0, p1, p2, p3, uv, uv, uv, uv);
            else b.Patch(p3, p2, p1, p0, uv, uv, uv, uv);
        }

        // ---- 裏庭 -----------------------------------------------------------------------

        /// <summary>家 A と家 B の裏庭、家 C と家 D の脇</summary>
        static void Yards(Transform parent, Banks b)
        {
            YardVeg.Clear();
            YardA(b);
            YardB(b);
            YardSouth(b);
        }

        /// <summary>
        /// 家 A の裏庭。家の裏の敷石、広い芝、ブランコ、トランポリン、小さなゴール、長い物干し、物置。
        /// 西の境は野石の塀（前庭から続く）、東はフットパスの生け垣、奥は生け垣
        /// </summary>
        static void YardA(Banks b)
        {
            b.Flag.FaceY(0.03f, -42.2f, -30.8f, 15.2f, 17.4f, 1);
            // ブランコ。A の字の枠を二つと梁、座面二つ
            var sw = new Vector3(-44.2f, 0f, 24.5f);
            foreach (var dz in new[] { -1.1f, 1.1f })
            {
                Beam(b.Bark, sw + new Vector3(-0.8f, 0f, dz), sw + new Vector3(0f, 2.2f, dz), 0.08f, 0.08f);
                Beam(b.Bark, sw + new Vector3(0.8f, 0f, dz), sw + new Vector3(0f, 2.2f, dz), 0.08f, 0.08f);
            }
            Beam(b.Bark, sw + new Vector3(0f, 2.2f, -1.25f), sw + new Vector3(0f, 2.2f, 1.25f), 0.1f, 0.1f);
            foreach (var dz in new[] { -0.5f, 0.5f })
            {
                foreach (var dx in new[] { -0.2f, 0.2f })
                    b.Iron.Box(sw + new Vector3(dx, 1.35f, dz), new Vector3(0.015f, 1.7f, 0.015f));
                Tint(b.Swatch, sw + new Vector3(0f, 0.48f, dz), new Vector3(0.48f, 0.04f, 0.18f), Quaternion.identity, dz < 0f ? SwToyRed : SwToyBlue);
            }
            // トランポリン。黒い床と青い縁、脚
            var tr = new Vector3(-36.8f, 0f, 27.5f);
            Prism(b.Iron, tr + Vector3.up * 0.72f, 1.5f, 0.02f, 16);
            b.Iron.FanY(tr + Vector3.up * 0.75f, Ring(tr, 1.4f, 16));
            for (var i = 0; i < 16; i++)
            {
                var a0 = Mathf.PI * 2f * i / 16f;
                var a1 = Mathf.PI * 2f * (i + 1) / 16f;
                var p0 = tr + new Vector3(Mathf.Cos(a0) * 1.55f, 0.76f, Mathf.Sin(a0) * 1.55f);
                var p1 = tr + new Vector3(Mathf.Cos(a1) * 1.55f, 0.76f, Mathf.Sin(a1) * 1.55f);
                Tint(b.Swatch, (p0 + p1) * 0.5f, new Vector3(0.2f, 0.06f, (p1 - p0).magnitude), Quaternion.LookRotation(p1 - p0), SwToyBlue);
                if (i % 4 == 0) b.Iron.Box(tr + new Vector3(Mathf.Cos(a0) * 1.45f, 0.36f, Mathf.Sin(a0) * 1.45f), new Vector3(0.05f, 0.72f, 0.05f));
            }
            // 小さなサッカーのゴール。白い枠
            var goal = new Vector3(-30.2f, 0f, 31.5f);
            Tint(b.Swatch, goal + new Vector3(0f, 0.9f, -0.9f), new Vector3(0.06f, 1.8f, 0.06f), Quaternion.identity, SwWhite);
            Tint(b.Swatch, goal + new Vector3(0f, 0.9f, 0.9f), new Vector3(0.06f, 1.8f, 0.06f), Quaternion.identity, SwWhite);
            Tint(b.Swatch, goal + new Vector3(0f, 1.8f, 0f), new Vector3(0.06f, 0.06f, 1.86f), Quaternion.identity, SwWhite);
            Tint(b.Swatch, goal + new Vector3(0.45f, 0.03f, 0f), new Vector3(0.9f, 0.05f, 1.86f), Quaternion.identity, SwWhite);
            // 転がったボール
            Tint(b.Swatch, new Vector3(-33.1f, 0.11f, 29.2f), Vector3.one * 0.2f, Quaternion.Euler(20f, 30f, 10f), SwWhite);
            // 長い物干し。二本の柱に綱を張り、色の混じった洗濯物
            var l0 = new Vector3(-46.8f, 0f, 19.0f);
            var l1 = new Vector3(-46.8f, 0f, 31.0f);
            foreach (var p in new[] { l0, l1 })
                b.Dressed.Box(p + Vector3.up * 1.0f, new Vector3(0.06f, 2.0f, 0.06f));
            Beam(b.Paint, l0 + Vector3.up * 1.95f, l1 + Vector3.up * 1.95f, 0.01f, 0.01f);
            var cols = new[] { SwSheet, SwShirt, SwToyRed, SwSheet, SwPink, SwYellow, SwShirt };
            for (var i = 0; i < cols.Length; i++)
            {
                var z0 = l0.z + 0.6f + i * 1.55f;
                var drop = cols[i] == SwSheet ? 1.0f : 0.5f + Hash(81, i) * 0.2f;
                var wide = cols[i] == SwSheet ? 1.3f : 0.55f;
                TintFace(b.Swatch, new Vector3(l0.x, 1.95f - drop, z0), new Vector3(l0.x, 1.95f - drop, z0 + wide),
                    new Vector3(l0.x, 1.95f, z0 + wide), new Vector3(l0.x, 1.95f, z0), Vector3.right, cols[i]);
                TintFace(b.Swatch, new Vector3(l0.x, 1.95f - drop, z0), new Vector3(l0.x, 1.95f - drop, z0 + wide),
                    new Vector3(l0.x, 1.95f, z0 + wide), new Vector3(l0.x, 1.95f, z0), Vector3.left, cols[i]);
            }
            SimpleShed(b, -31.4f, -27.6f, 32.0f, 34.8f, 2.3f, SwPlastic);
        }

        /// <summary>
        /// 家 B の裏庭。野菜の畝二つと豆の支柱、薪の山、冷床、ベンチ、物置。
        /// 東の境は片割れの板の塀（片割れの側で立つ）。路地の側の東の脇は、低い生け垣の奥の芝
        /// </summary>
        static void YardB(Banks b)
        {
            b.Flag.FaceY(0.03f, -21.0f, -15.0f, 11.6f, 13.2f, 1);
            // 野菜の畝
            foreach (var r in new[] { new Rect(-23.4f, 20.0f, 2.2f, 5.5f), new Rect(-20.4f, 20.0f, 2.2f, 5.5f) })
            {
                const float h = 0.24f;
                b.Bark.Box(new Vector3(r.xMin + 0.03f, h * 0.5f, r.center.y), new Vector3(0.06f, h, r.height));
                b.Bark.Box(new Vector3(r.xMax - 0.03f, h * 0.5f, r.center.y), new Vector3(0.06f, h, r.height));
                b.Bark.Box(new Vector3(r.center.x, h * 0.5f, r.yMin + 0.03f), new Vector3(r.width, h, 0.06f));
                b.Bark.Box(new Vector3(r.center.x, h * 0.5f, r.yMax - 0.03f), new Vector3(r.width, h, 0.06f));
                b.Soil.FaceY(h - 0.04f, r.xMin + 0.06f, r.xMax - 0.06f, r.yMin + 0.06f, r.yMax - 0.06f, 1);
                YardVeg.Add(r);
            }
            // 豆の支柱。竹を合掌に組んだ列
            for (var i = 0; i <= 6; i++)
            {
                var z = 20.4f + i * 0.78f;
                Beam(b.Bark, new Vector3(-20.1f, 0.2f, z), new Vector3(-19.3f, 2.1f, z), 0.02f, 0.02f);
                Beam(b.Bark, new Vector3(-18.5f, 0.2f, z), new Vector3(-19.3f, 2.1f, z), 0.02f, 0.02f);
            }
            Beam(b.Bark, new Vector3(-19.3f, 2.05f, 20.2f), new Vector3(-19.3f, 2.05f, 25.3f), 0.025f, 0.025f);
            // 薪の山。物置の脇の庇の下に、丸太の小口を見せて積む
            SimpleShed(b, -23.8f, -20.6f, 31.4f, 34.8f, 2.2f, SwBlueGrey);
            var logs = new Vector3(-19.9f, 0f, 33.2f);
            Beam(b.Boards, logs + new Vector3(-0.2f, 1.6f, -1.3f), logs + new Vector3(-0.2f, 1.6f, 1.3f), 0.08f, 0.08f);
            for (var row = 0; row < 5; row++)
                for (var k = 0; k < 9; k++)
                {
                    var z = logs.z - 1.1f + k * 0.26f + (row % 2) * 0.13f;
                    if (z > logs.z + 1.15f) continue;
                    var c = new Vector3(logs.x + 0.2f, 0.13f + row * 0.24f, z);
                    Tint(b.Swatch, c, new Vector3(0.8f, 0.22f, 0.22f), Quaternion.identity, SwLog);
                    Tint(b.Swatch, c + new Vector3(0.405f, 0f, 0f), new Vector3(0.01f, 0.19f, 0.19f), Quaternion.identity, SwLogEnd);
                }
            // 冷床。煉瓦の低い枠にガラスの蓋
            var cf = new Vector3(-15.8f, 0f, 22.6f);
            b.Brick.Box(cf + new Vector3(0f, 0.18f, 0f), new Vector3(1.8f, 0.36f, 0.9f));
            Face(b.Glass, cf + new Vector3(-0.9f, 0.38f, -0.45f), cf + new Vector3(0.9f, 0.38f, -0.45f), cf + new Vector3(0.9f, 0.52f, 0.45f), cf + new Vector3(-0.9f, 0.52f, 0.45f), Vector3.up);
            // ベンチ。奥の生け垣を背に、家を向く
            var bench = new Vector3(-12.0f, 0f, 33.6f);
            b.Boards.Box(bench + new Vector3(0f, 0.45f, 0f), new Vector3(1.5f, 0.05f, 0.45f));
            b.Boards.Box(bench + new Vector3(0f, 0.75f, 0.22f), new Vector3(1.5f, 0.4f, 0.05f));
            foreach (var dx in new[] { -0.65f, 0.65f })
                b.Boards.Box(bench + new Vector3(dx, 0.23f, 0f), new Vector3(0.07f, 0.46f, 0.42f));
            // 鳥の餌台
            var feeder = new Vector3(-14.2f, 0f, 28.0f);
            b.Bark.Box(feeder + Vector3.up * 0.8f, new Vector3(0.07f, 1.6f, 0.07f));
            b.Boards.Box(feeder + Vector3.up * 1.6f, new Vector3(0.5f, 0.04f, 0.4f));
            b.Slate.Box(feeder + Vector3.up * 1.86f, new Vector3(0.55f, 0.05f, 0.45f), Quaternion.Euler(0f, 0f, 20f));
            // リンゴの木の幹。樹冠の札は LanePlants
            foreach (var at in YardTrees)
                if (at.z > 0f) Beam(b.Bark, at, at + new Vector3(0.05f, 1.35f, 0.02f), 0.18f, 0.16f);
        }

        /// <summary>家 B の裏庭の畝。レタスとキャベツの札を載せる</summary>
        static readonly List<Rect> YardVeg = new List<Rect>();
        /// <summary>周りの家の庭の木（リンゴ）の足元</summary>
        static readonly Vector3[] YardTrees =
        {
            new Vector3(-16.5f, 0f, 30.5f), new Vector3(-10.5f, 0f, 24.0f), new Vector3(-40.5f, 0f, 33.0f),
            new Vector3(-54.0f, 0f, -15.2f), new Vector3(-40.0f, 0f, -16.0f), new Vector3(-17.2f, 0f, -15.5f), new Vector3(-5.0f, 0f, -15.8f),
        };

        /// <summary>
        /// 家 C と家 D の脇。家の脇を刈り込んだ生け垣で閉じ、その奥は見せない。
        /// 垣の上に裏庭の木の頭と物置の屋根が覗く
        /// </summary>
        static void YardSouth(Banks b)
        {
            foreach (var span in new[] { new Vector2(PlotCWest + 0.45f, -52.6f), new Vector2(-42.4f, PlotCEast - 0.45f),
                new Vector2(PlotDWest + 0.45f, -16.6f), new Vector2(-5.6f, PlotDEast - 0.45f) })
            {
                var c = span.x < -40f;
                var z = c ? -9.4f : -8.6f;
                Clipped(b, null, null, c ? Shrub.Privet : Shrub.Beech, new Vector3(span.x, 0f, z), new Vector3(span.y, 0f, z), 1.8f, 0.6f, (int)(span.x * 5f));
            }
            // 裏の物置の屋根。塀の上に覗く
            SimpleShed(b, -55.2f, -52.8f, -17.2f, -14.6f, 2.2f, SwBlueGrey);
            SimpleShed(b, -5.4f, -3.4f, -17.3f, -14.8f, 2.2f, SwPlastic);
            foreach (var at in YardTrees)
                if (at.z < 0f) Beam(b.Bark, at, at + new Vector3(0.05f, 1.35f, 0.02f), 0.18f, 0.16f);
        }

        /// <summary>小さな物置。板張りの切妻に黒いフェルトの屋根、妻に戸。戸の色は升で</summary>
        static void SimpleShed(Banks b, float x0, float x1, float z0, float z1, float ridge, int door)
        {
            var eaves = ridge - 0.5f;
            var xm = (x0 + x1) * 0.5f;
            b.Boards.FaceX(x0, z0, z1, 0f, eaves, -1);
            b.Boards.FaceX(x1, z0, z1, 0f, eaves, 1);
            b.Boards.FaceZ(z0, x0, x1, 0f, eaves, -1);
            b.Boards.FaceZ(z1, x0, x1, 0f, eaves, 1);
            foreach (var z in new[] { z0, z1 })
                Face(b.Boards, new Vector3(x0, eaves, z), new Vector3(x1, eaves, z), new Vector3(xm, ridge, z), new Vector3(xm, ridge, z), Vector3.forward * (z == z0 ? -1f : 1f));
            foreach (var side in new[] { -1f, 1f })
            {
                var xe = side < 0f ? x0 - 0.15f : x1 + 0.15f;
                var ye = eaves - 0.15f * (ridge - eaves) / (xm - x0);
                Face(b.Iron, new Vector3(xe, ye, z0 - 0.15f), new Vector3(xe, ye, z1 + 0.15f), new Vector3(xm, ridge + 0.02f, z1 + 0.15f), new Vector3(xm, ridge + 0.02f, z0 - 0.15f), new Vector3(side, 2f, 0f));
                Face(b.Iron, new Vector3(xe, ye - 0.04f, z0 - 0.15f), new Vector3(xe, ye - 0.04f, z1 + 0.15f), new Vector3(xm, ridge - 0.02f, z1 + 0.15f), new Vector3(xm, ridge - 0.02f, z0 - 0.15f), new Vector3(-side, -2f, 0f));
            }
            // 戸は家の側（路地に近い側）の妻に
            var zd = Mathf.Abs(z0) < Mathf.Abs(z1) ? z0 : z1;
            var s = zd == z0 ? -1f : 1f;
            Tint(b.Swatch, new Vector3(xm, 0.9f, zd + s * 0.03f), new Vector3(0.75f, 1.75f, 0.04f), Quaternion.identity, door);
        }

        // ---- 花 -------------------------------------------------------------------------

        /// <summary>
        /// 家 A の裏庭。暖かい色（赤・橙・黄）。背の高い物は花の終わったジギタリスの葉の株と白いタチアオイを少し。
        /// **ピンクのタチアオイは使わない。** 家々の軒先に背の高いピンクが並ぶと、片割れの庭の主役を食う（設計書 7 節）
        /// </summary>
        static readonly Palette WarmPlan = new Palette
        {
            Tall = new[] { Kind.Foxglove, Kind.HollyWhite },
            TallW = new[] { 2f, 0.6f },
            Mid = new[] { Kind.Rudbeckia, Kind.DahliaRed, Kind.EchPink, Kind.Allium },
            MidW = new[] { 3f, 2f, 1.5f, 0.6f },
            Low = new[] { Kind.Mantle, Kind.Catmint, Kind.Filler },
            LowW = new[] { 2f, 1.5f, 1f },
        };

        /// <summary>家 B の裏庭。冷たい色。青・紫・白。背の高い物は花の終わったデルフィニウムを主に</summary>
        static readonly Palette CoolPlan = new Palette
        {
            Tall = new[] { Kind.Delph, Kind.HollyWhite },
            TallW = new[] { 2f, 0.6f },
            Mid = new[] { Kind.Aster, Kind.Hydrangea, Kind.EchWhite, Kind.Rosemary },
            MidW = new[] { 3f, 2f, 1.5f, 1f },
            Low = new[] { Kind.Lavender, Kind.Catmint, Kind.Geranium },
            LowW = new[] { 2.5f, 2f, 2f },
        };

        /// <summary>
        /// 家 A と家 B の前庭。路地から見えるので、背の高い物を置かず、中くらいを落ち着いた色で。
        /// 家 A は黄と白（ルドベキア・白いエキナセア）、家 B は青と白（アスター・アジサイ）
        /// </summary>
        static readonly Palette FrontWarmPlan = new Palette
        {
            Mid = new[] { Kind.Rudbeckia, Kind.EchWhite, Kind.Rosemary },
            MidW = new[] { 2f, 1.5f, 1f },
            Low = new[] { Kind.Mantle, Kind.Catmint, Kind.Lavender },
            LowW = new[] { 2f, 1.5f, 1f },
        };

        static readonly Palette FrontCoolPlan = new Palette
        {
            Mid = new[] { Kind.Aster, Kind.Hydrangea, Kind.EchWhite },
            MidW = new[] { 2.5f, 2f, 1f },
            Low = new[] { Kind.Lavender, Kind.Catmint, Kind.Geranium },
            LowW = new[] { 2.5f, 2f, 2f },
        };

        /// <summary>家 B の路地の側の脇の庭。低い物だけ。垣の上の見通しを塞がない</summary>
        static readonly Palette LowCoolPlan = new Palette
        {
            Low = new[] { Kind.Lavender, Kind.Catmint, Kind.Geranium, Kind.Sage },
            LowW = new[] { 2.5f, 2f, 2f, 1f },
        };

        /// <summary>家 C の前庭。白とアジサイの青。背の高い物は置かない</summary>
        static readonly Palette BrickPlan = new Palette
        {
            Mid = new[] { Kind.Hydrangea, Kind.EchWhite, Kind.Rosemary },
            MidW = new[] { 2.5f, 1.5f, 1f },
            Low = new[] { Kind.Lavender, Kind.Geranium, Kind.Mantle },
            LowW = new[] { 2f, 1.5f, 1.5f },
        };

        /// <summary>家 D の前庭。黄と青の混ぜ植え。背の高い物は置かない</summary>
        static readonly Palette MixPlan = new Palette
        {
            Mid = new[] { Kind.Rudbeckia, Kind.Aster, Kind.Sage },
            MidW = new[] { 2f, 2f, 1f },
            Low = new[] { Kind.Catmint, Kind.Mantle, Kind.Geranium, Kind.Lavender },
            LowW = new[] { 2f, 2f, 1.5f, 1f },
        };

        static Border AlongX(string name, float x0, float x1, float front, float back, Palette plan, int seed)
        {
            return new Border { Name = name, AlongZ = false, From = x0, To = x1, Front = x => front, Back = x => back, Plan = plan, Seed = seed };
        }

        static Border AlongZ(string name, float z0, float z1, float front, float back, Palette plan, int seed)
        {
            return new Border { Name = name, AlongZ = true, From = z0, To = z1, Front = z => front, Back = z => back, Plan = plan, Seed = seed };
        }

        /// <summary>北の家並み（家 A・家 B）の前庭と裏庭の花の縁</summary>
        static List<Border> NorthBorders()
        {
            return new List<Border>
            {
                AlongX("AFront", PlotAWest + 1.0f, -34.0f - 0.7f, NorthEdge + 0.85f, 6.35f, FrontWarmPlan, 401),
                AlongX("AWingFront", -34.0f + 0.7f, PlotAEast - 0.5f, NorthEdge + 0.85f, 8.15f, FrontWarmPlan, 403),
                AlongZ("AWest", 17.8f, 34.6f, PlotAWest + 1.6f, PlotAWest + 0.6f, WarmPlan, 405),
                AlongX("ABack", PlotAWest + 1.5f, -32.0f, 34.2f, BackHedge - 0.55f, WarmPlan, 407),
                AlongX("BFront0", PlotBWest + 0.5f, -17.6f - 0.7f, NorthEdge + 0.8f, 5.95f, FrontCoolPlan, 411),
                AlongX("BFront1", -17.6f + 0.7f, -13.4f, NorthEdge + 0.8f, 5.95f, FrontCoolPlan, 413),
                AlongX("BSide", -12.3f, PlotBEast - 0.3f, NorthEdge + 0.75f, NorthEdge + 1.55f, LowCoolPlan, 415),
                AlongZ("BEast", 13.6f, 34.4f, PlotBEast - 1.7f, PlotBEast - 0.75f, CoolPlan, 417),
                AlongX("BBack", -18.0f, PlotBEast - 1.4f, 34.3f, BackHedge - 0.55f, CoolPlan, 419),
            };
        }

        /// <summary>南の家並み（家 C・家 D）の前庭の花の縁</summary>
        static List<Border> SouthBorders()
        {
            return new List<Border>
            {
                AlongX("CFront0", PlotCWest + 1.0f, -47.5f - 0.85f, -NorthEdge - 0.85f, -6.95f, BrickPlan, 421),
                AlongX("CFront1", -47.5f + 0.85f, PlotCEast - 0.6f, -NorthEdge - 0.85f, -6.95f, BrickPlan, 423),
                AlongX("DFront0", PlotDWest + 0.6f, -10.6f - 0.75f, -NorthEdge - 0.9f, -6.35f, MixPlan, 425),
                AlongX("DFront1", -10.6f + 0.75f, PlotDEast - 0.5f, -NorthEdge - 0.9f, -6.35f, MixPlan, 427),
            };
        }

        /// <summary>
        /// 路地の家並みと周りの庭の花と葉。北・南・つると鉢・木の四つに焼く。
        /// 花の縁の下の土は Soil の面で敷く（地面は一枚の芝の色なので）
        /// </summary>
        static void LanePlants(Transform parent, Banks b)
        {
            var mat = FloraMat();
            foreach (var pair in new[] { new KeyValuePair<string, List<Border>>("North", NorthBorders()), new KeyValuePair<string, List<Border>>("South", SouthBorders()) })
            {
                var f = FloraBank();
                foreach (var border in pair.Value)
                {
                    Sow(f, border);
                    var x0 = border.AlongZ ? Mathf.Min(border.Front(border.From), border.Back(border.From)) : border.From;
                    var x1 = border.AlongZ ? Mathf.Max(border.Front(border.From), border.Back(border.From)) : border.To;
                    var z0 = border.AlongZ ? border.From : Mathf.Min(border.Front(border.From), border.Back(border.From));
                    var z1 = border.AlongZ ? border.To : Mathf.Max(border.Front(border.From), border.Back(border.From));
                    b.Soil.FaceY(0.008f, x0, x1, z0, z1, 1);
                }
                Emit(parent, "FloraLane" + pair.Key, f, mat, false);
            }

            // つると鉢。花箱と吊り鉢のペラルゴニウム、玄関のまわりのバラ、壁のアイビー、畝のレタス
            var g = FloraBank();
            g.CardLift = 0.8f;
            var i = 0;
            foreach (var at in WindowPlants)
                Clump(g, Kind.Pelargonium, at, 1.0f, 90f + Hash(501, i++) * 30f, Vector3.zero);
            foreach (var at in Baskets)
            {
                Clump(g, Kind.Pelargonium, at, 1.0f, 20f + i * 13f, Vector3.zero);
                Clump(g, Kind.Filler, at + Vector3.down * 0.25f, 0.55f, 70f + i * 7f, Vector3.zero);
                i++;
            }
            foreach (var r in DoorRoses)
                WallRose(g, new Vector3(r.x, 0f, r.y), Vector3.right, r.w, Kind.Roses);
            // 家 B の正面の壁のバラ（茅の軒の下）と、家 A の西の妻のアイビー、家 D の壁のアイビー
            for (var k = 0; k < 3; k++)
                Flat(g, Kind.Roses, new Vector3(-21.4f + k * 3.2f, 0.05f, 6.2f - 0.07f), new Vector3(0.4f, 0f, 0f), Vector3.up * 1.3f);
            for (var k = 0; k < 4; k++)
                Flat(g, Kind.Ivy, new Vector3(-42.07f, 0f, 7.6f + k * 1.9f), new Vector3(0f, 0f, 0.9f), Vector3.up * (2.2f + Hash(503, k) * 1.6f));
            Flat(g, Kind.Ivy, new Vector3(-16.67f, 0f, -8.0f), new Vector3(0f, 0f, 0.9f), Vector3.up * 2.4f);
            Flat(g, Kind.Clematis, new Vector3(-5.53f, 0.1f, -8.6f), new Vector3(0f, 0f, 0.6f), Vector3.up * 1.4f);
            // 野石の塀のアイビー。路地の側に点々と
            for (var k = 0; k < 10; k++)
            {
                var x = LaneWest + 6f + k * 7.3f + Hash(505, k) * 2f;
                if (x > PlotAWest - 1f && x < PlotCEast) continue;
                var z = k % 2 == 0 ? NorthEdge - 0.02f : -NorthEdge + 0.02f;
                Flat(g, Kind.Ivy, new Vector3(x, 0f, z), new Vector3(0.6f + Hash(507, k) * 0.5f, 0f, 0f), Vector3.up * (0.7f + Hash(509, k) * 0.4f));
            }
            foreach (var r in YardVeg)
                for (var row = 0; row < 3; row++)
                    for (var k = 0; k < 8; k++)
                        Clump(g, Kind.Filler, new Vector3(r.xMin + 0.4f + row * 0.7f, 0.2f, r.yMin + 0.35f + k * 0.65f), 0.45f, k * 37f + row * 11f, Vector3.zero);
            // 豆の支柱のスイートピー
            for (var k = 0; k < 3; k++)
                Flat(g, Kind.SweetPea, new Vector3(-19.3f, 0.22f, 21.0f + k * 1.7f), new Vector3(0f, 0f, 0.7f), Vector3.up * 1.8f);
            NoShadow(Emit(parent, "FloraLaneClimbers", g, mat, false));

            // 周りの庭の木
            var t = FloraBank();
            t.CardLift = 2.0f;
            foreach (var at in YardTrees)
            {
                var crown = at + new Vector3(0.05f, 1.3f, 0.02f);
                Clump(t, Kind.Apple, crown, 0.95f, at.x * 17f, Vector3.zero);
                Clump(t, Kind.Apple, crown + new Vector3(0.3f, 0.45f, -0.2f), 0.7f, at.x * 17f + 50f, Vector3.zero);
            }
            Emit(parent, "FloraLaneTrees", t, mat, false);
        }
    }
}
