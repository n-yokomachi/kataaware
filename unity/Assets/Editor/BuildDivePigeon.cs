using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HalfAware.EditorTools
{
    /// <summary>
    /// 公園の鳩（記憶 2・3・9・10）。形・色・群れの置き方。動かすのは <see cref="Pigeon"/>。
    ///
    /// **形は姿勢ごとに一枚ずつ焼く。** 立つ・首を引く・首を突き出す・ついばむ・首を傾げる・羽を震わせる・
    /// 羽ばたきの三枚（上げ・水平・下げ）の九枚。<see cref="Pigeon"/> がこまの頭ごとに差し替える。
    /// 丸い胴（六角の輪を六つ）・小さな頭とくちばし・たたんだ翼・尾羽・赤い脚で、三角形は一枚 140〜170。
    /// 面は一枚ずつ頂点を持たせて、PS1 のように面ごとに陰影が折れて見える作りにする。
    ///
    /// **色は升の絵（<see cref="PigeonAtlasPath"/>）で塗る。** 村の升（<c>VillageSwatch</c>）と同じ流儀で、
    /// 面の uv を升の真ん中に置く。横に部位（胴・首の緑と紫・翼・黒い帯・風切り・尾の先・目・脚…）、
    /// 縦に羽色の四つ（ふつうの灰・濃い灰・白の混じり・茶の混じり）を並べ、羽色はマテリアルの
    /// uv のずらしで段を選ぶ。形は四つの羽色で使い回す。
    ///
    /// **群れは記憶の時刻と出来事で置く。** 15:47（記憶 2・3）は池の手前に十羽、ベンチ A の前に二羽、
    /// 餌を撒いていたベンチ B の前に四羽。15:50（記憶 9・10）は池の手前に八羽、ベンチ B の前に三羽。
    /// 一か所に固めず、池の縁の舗装と芝の境に散らす
    /// </summary>
    public static partial class BuildDive
    {
        // ---- 色の升 -------------------------------------------------------------------

        /// <summary>部位。升の横の並び</summary>
        enum Plume { Body, Belly, Head, Green, Purple, Wing, Bar, Tips, Tail, TailTip, Rump, Beak, Eye, Legs, Under, Breast }

        const int PlumeCount = 16;
        const int PlumeRows = 4;
        const int PlumeCell = 8;
        const string PigeonAtlasPath = "Assets/Textures/Dive/DivePigeon.png";
        const string PigeonAtlasSign = "pigeon1";

        /// <summary>
        /// 羽色ごとの部位の色（sRGB）。0 ふつうの灰（灰の翼に黒い帯二本）、1 濃い灰、2 白の混じり、3 茶の混じり。
        /// 並びは <see cref="Plume"/> と同じ
        /// </summary>
        static readonly Color[][] PlumeRowsColors =
        {
            new[]
            {
                C(0.38f, 0.41f, 0.47f), C(0.42f, 0.45f, 0.50f), C(0.31f, 0.33f, 0.39f), C(0.22f, 0.44f, 0.32f),
                C(0.44f, 0.27f, 0.47f), C(0.48f, 0.51f, 0.56f), C(0.07f, 0.07f, 0.09f), C(0.22f, 0.23f, 0.26f),
                C(0.36f, 0.39f, 0.45f), C(0.08f, 0.08f, 0.10f), C(0.58f, 0.60f, 0.63f), C(0.15f, 0.14f, 0.14f),
                C(0.92f, 0.45f, 0.10f), C(0.80f, 0.30f, 0.30f), C(0.66f, 0.67f, 0.70f), C(0.50f, 0.42f, 0.50f),
            },
            new[]
            {
                C(0.24f, 0.25f, 0.28f), C(0.28f, 0.29f, 0.32f), C(0.17f, 0.18f, 0.21f), C(0.16f, 0.36f, 0.25f),
                C(0.34f, 0.19f, 0.38f), C(0.30f, 0.31f, 0.34f), C(0.05f, 0.05f, 0.06f), C(0.12f, 0.12f, 0.14f),
                C(0.22f, 0.23f, 0.26f), C(0.06f, 0.06f, 0.07f), C(0.28f, 0.29f, 0.32f), C(0.11f, 0.10f, 0.10f),
                C(0.88f, 0.40f, 0.10f), C(0.74f, 0.26f, 0.26f), C(0.46f, 0.47f, 0.50f), C(0.27f, 0.23f, 0.29f),
            },
            new[]
            {
                C(0.54f, 0.56f, 0.60f), C(0.76f, 0.76f, 0.75f), C(0.76f, 0.76f, 0.74f), C(0.26f, 0.46f, 0.34f),
                C(0.48f, 0.33f, 0.50f), C(0.74f, 0.74f, 0.73f), C(0.30f, 0.30f, 0.33f), C(0.76f, 0.76f, 0.74f),
                C(0.46f, 0.48f, 0.52f), C(0.12f, 0.12f, 0.14f), C(0.76f, 0.76f, 0.75f), C(0.60f, 0.53f, 0.50f),
                C(0.90f, 0.42f, 0.10f), C(0.82f, 0.34f, 0.34f), C(0.78f, 0.78f, 0.77f), C(0.70f, 0.70f, 0.70f),
            },
            new[]
            {
                C(0.48f, 0.43f, 0.41f), C(0.53f, 0.48f, 0.46f), C(0.42f, 0.36f, 0.35f), C(0.28f, 0.42f, 0.30f),
                C(0.46f, 0.29f, 0.40f), C(0.56f, 0.50f, 0.46f), C(0.44f, 0.22f, 0.14f), C(0.44f, 0.34f, 0.29f),
                C(0.46f, 0.41f, 0.39f), C(0.34f, 0.21f, 0.16f), C(0.60f, 0.56f, 0.53f), C(0.38f, 0.30f, 0.28f),
                C(0.92f, 0.48f, 0.12f), C(0.80f, 0.32f, 0.30f), C(0.78f, 0.75f, 0.73f), C(0.54f, 0.42f, 0.42f),
            },
        };

        static Color C(float r, float g, float b) { return new Color(r, g, b); }

        /// <summary>升の絵。部位を横に、羽色を縦に並べた 128×32。色が変わっていれば描き直す</summary>
        static Texture2D PigeonAtlas()
        {
            var sign = PigeonAtlasSign;
            foreach (var row in PlumeRowsColors) foreach (var c in row) sign += "|" + ColorUtility.ToHtmlStringRGB(c);
            var importer = AssetImporter.GetAtPath(PigeonAtlasPath) as TextureImporter;
            if (importer != null && importer.userData == sign && System.IO.File.Exists(PigeonAtlasPath))
                return AssetDatabase.LoadAssetAtPath<Texture2D>(PigeonAtlasPath);
            if (!AssetDatabase.IsValidFolder("Assets/Textures/Dive")) AssetDatabase.CreateFolder("Assets/Textures", "Dive");
            var w = PlumeCell * PlumeCount;
            var h = PlumeCell * PlumeRows;
            var px = new Color32[w * h];
            for (var r = 0; r < PlumeRows; r++)
                for (var i = 0; i < PlumeCount; i++)
                {
                    var c = (Color32)PlumeRowsColors[r][i];
                    for (var y = 0; y < PlumeCell; y++)
                        for (var x = 0; x < PlumeCell; x++)
                            px[(r * PlumeCell + y) * w + i * PlumeCell + x] = c;
                }
            var pic = new Texture2D(w, h, TextureFormat.RGB24, false, false);
            pic.SetPixels32(px);
            pic.Apply();
            System.IO.File.WriteAllBytes(PigeonAtlasPath, pic.EncodeToPNG());
            Object.DestroyImmediate(pic);
            AssetDatabase.ImportAsset(PigeonAtlasPath);
            importer = AssetImporter.GetAtPath(PigeonAtlasPath) as TextureImporter;
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
            return AssetDatabase.LoadAssetAtPath<Texture2D>(PigeonAtlasPath);
        }

        /// <summary>羽色ごとのマテリアル。升の絵の段を uv のずらしで選ぶ</summary>
        static Material PigeonMat(int row)
        {
            var name = "Pigeon" + row;
            var path = Materials + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.name = name;
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", PigeonAtlas());
            m.SetTextureScale("_BaseMap", new Vector2(1f, 1f / PlumeRows));
            m.SetTextureOffset("_BaseMap", new Vector2(0f, (float)row / PlumeRows));
            m.SetColor("_BaseColor", Color.white);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.12f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---- 形 ---------------------------------------------------------------------

        /// <summary>面を一枚ずつ溜めて、部位の升の uv を振る入れ物</summary>
        sealed class Plumage
        {
            readonly List<Vector3> verts = new List<Vector3>();
            readonly List<Vector2> uvs = new List<Vector2>();
            readonly List<int> tris = new List<int>();

            public int Count { get { return tris.Count / 3; } }

            /// <summary>
            /// 脚を詰めて胴を下げる量。m。足の指（地面）は動かさず、脚の付け根（0.075 m）から上をこれだけ下げる。
            /// 形を組む寸法は脚の長い鳩のまま書いてあり、ここで一度に詰める
            /// </summary>
            public const float Sink = 0.014f;

            static Vector3 Lower(Vector3 v)
            {
                v.y -= Sink * Mathf.Clamp01(v.y / 0.075f);
                return v;
            }

            /// <summary>三角を一枚。表は <paramref name="face"/> の向き</summary>
            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 face, Plume part)
            {
                a = Lower(a); b = Lower(b); c = Lower(c);
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), face) < 0f) { var t = b; b = c; c = t; }
                var uv = new Vector2(((int)part + 0.5f) / PlumeCount, 0.5f);
                var i = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                uvs.Add(uv); uvs.Add(uv); uvs.Add(uv);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            }

            /// <summary>四隅で一枚（a→b→c→d が縁を一周する）。表は <paramref name="face"/> の向き</summary>
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 face, Plume part)
            {
                Tri(a, b, c, face, part);
                Tri(a, c, d, face, part);
            }

            /// <summary>中心から外へ向けた四隅の面</summary>
            public void QuadOut(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 centre, Plume part)
            {
                Quad(a, b, c, d, (a + b + c + d) * 0.25f - centre, part);
            }

            public Mesh Bake(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(verts);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        /// <summary>姿勢の決め。頭の置き場と向き、翼の形、脚の有無、胴の膨らみ</summary>
        struct Stance
        {
            public Vector3 head;
            /// <summary>頭の向き。x が俯き（正が下）、y が首振り、z が傾げ</summary>
            public Vector3 turn;
            /// <summary>翼。0 たたむ、1 震わせる、2 上げ、3 水平、4 下げ</summary>
            public int wings;
            public bool legs;
            public float puff;
            public float tail;
        }

        static Stance PigeonStance(Pigeon.Shape s)
        {
            var st = new Stance { head = new Vector3(0f, 0.216f, 0.105f), wings = 0, legs = true, puff = 1f, tail = 1f };
            switch (s)
            {
                case Pigeon.Shape.StepBack: st.head = new Vector3(0f, 0.224f, 0.088f); st.turn = new Vector3(-8f, 0f, 0f); break;
                case Pigeon.Shape.StepFore: st.head = new Vector3(0f, 0.200f, 0.142f); st.turn = new Vector3(14f, 0f, 0f); break;
                case Pigeon.Shape.Peck: st.head = new Vector3(0f, 0.048f, 0.150f); st.turn = new Vector3(66f, 0f, 0f); break;
                case Pigeon.Shape.Cock: st.head = new Vector3(0.004f, 0.218f, 0.106f); st.turn = new Vector3(-12f, 28f, 32f); break;
                case Pigeon.Shape.Ruffle: st.head = new Vector3(0f, 0.204f, 0.106f); st.wings = 1; st.puff = 1.08f; st.tail = 1.3f; break;
                case Pigeon.Shape.FlapUp: st.head = new Vector3(0f, 0.186f, 0.140f); st.wings = 2; st.legs = false; st.tail = 1.5f; break;
                case Pigeon.Shape.FlapMid: st.head = new Vector3(0f, 0.186f, 0.140f); st.wings = 3; st.legs = false; st.tail = 1.5f; break;
                case Pigeon.Shape.FlapDown: st.head = new Vector3(0f, 0.186f, 0.140f); st.wings = 4; st.legs = false; st.tail = 1.5f; break;
            }
            return st;
        }

        /// <summary>
        /// 胴の輪。z、中心の高さ、横と縦の半径。首の付け根・胸の上・胸・胴の中・腰・尻の六つ。
        /// 胸の上の輪を挟まないと、首の付け根から胸へ段が付いて、横から見た胸が箱の壁のように切れる
        /// </summary>
        static readonly Vector4[] PigeonRings =
        {
            new Vector4(0.092f, 0.158f, 0.030f, 0.030f),
            new Vector4(0.078f, 0.134f, 0.047f, 0.050f),
            new Vector4(0.042f, 0.119f, 0.060f, 0.060f),
            new Vector4(-0.010f, 0.112f, 0.062f, 0.056f),
            new Vector4(-0.065f, 0.112f, 0.048f, 0.042f),
            new Vector4(-0.100f, 0.114f, 0.026f, 0.022f),
        };

        /// <summary>輪の六つの角。上から右回りに</summary>
        static readonly float[] PigeonSides = { 90f, 30f, -30f, -90f, -150f, 150f };

        /// <summary>たたんだ翼の断面。z と、背の側・中・脇の側の (x, y)。肩から翼の先へ</summary>
        static readonly float[][] PigeonFold =
        {
            new[] { 0.060f, 0.014f, 0.188f, 0.052f, 0.172f, 0.066f, 0.116f },
            new[] { -0.020f, 0.013f, 0.184f, 0.058f, 0.166f, 0.070f, 0.108f },
            new[] { -0.055f, 0.012f, 0.178f, 0.054f, 0.160f, 0.064f, 0.108f },
            new[] { -0.073f, 0.011f, 0.172f, 0.048f, 0.156f, 0.058f, 0.110f },
            new[] { -0.092f, 0.010f, 0.164f, 0.040f, 0.152f, 0.050f, 0.112f },
            new[] { -0.105f, 0.009f, 0.154f, 0.032f, 0.146f, 0.040f, 0.116f },
            new[] { -0.155f, 0.004f, 0.132f, 0.014f, 0.128f, 0.018f, 0.120f },
        };

        /// <summary>たたんだ翼の区切りごとの色。肩から、灰・灰・黒い帯・灰・黒い帯・風切り</summary>
        static readonly Plume[] PigeonFoldParts = { Plume.Wing, Plume.Wing, Plume.Bar, Plume.Wing, Plume.Bar, Plume.Tips };

        static Mesh PigeonShape(Pigeon.Shape s)
        {
            var st = PigeonStance(s);
            var b = new Plumage();
            PigeonBody(b, st);
            PigeonHead(b, st);
            if (st.wings <= 1) { PigeonFolded(b, st, 1f); PigeonFolded(b, st, -1f); }
            else { PigeonSpread(b, st, 1f); PigeonSpread(b, st, -1f); }
            PigeonTail(b, st);
            if (st.legs) { PigeonLeg(b, 1f); PigeonLeg(b, -1f); }
            return b.Bake("Pigeon" + s);
        }

        static Vector3 RingPoint(Vector4 r, int side, float puff)
        {
            var a = PigeonSides[side] * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a) * r.z * puff, r.y + Mathf.Sin(a) * r.w * puff, r.x);
        }

        /// <summary>胴。六角の輪を五つ繋ぐ。首の付け根は緑と紫の照り、腰は明るい灰、腹は薄い灰</summary>
        static void PigeonBody(Plumage b, Stance st)
        {
            for (var i = 0; i + 1 < PigeonRings.Length; i++)
            {
                var r0 = PigeonRings[i];
                var r1 = PigeonRings[i + 1];
                var centre = new Vector3(0f, (r0.y + r1.y) * 0.5f, (r0.x + r1.x) * 0.5f);
                for (var j = 0; j < 6; j++)
                {
                    var k = (j + 1) % 6;
                    var upper = j == 0 || j == 5;
                    var side = j == 1 || j == 4;
                    Plume part;
                    // 首の付け根と胸の上は緑と紫の照り、胸は紫がかった灰、腰は明るい灰
                    if (i == 0) part = upper ? Plume.Green : side ? Plume.Purple : Plume.Breast;
                    else if (i == 1) part = upper ? Plume.Body : side ? Plume.Green : Plume.Breast;
                    else if (i == 4) part = upper ? Plume.Rump : side ? Plume.Body : Plume.Belly;
                    else part = upper || side ? Plume.Body : (i == 2 ? Plume.Breast : Plume.Belly);
                    b.QuadOut(RingPoint(r0, j, st.puff), RingPoint(r0, k, st.puff), RingPoint(r1, k, st.puff), RingPoint(r1, j, st.puff), centre, part);
                }
            }
            // 尻の蓋
            var last = PigeonRings[PigeonRings.Length - 1];
            var mid = new Vector3(0f, last.y, last.x - 0.01f);
            for (var j = 0; j < 6; j++)
                b.Tri(mid, RingPoint(last, j, st.puff), RingPoint(last, (j + 1) % 6, st.puff), Vector3.back, Plume.Belly);
        }

        /// <summary>首と頭。首は胴の頭の輪から頭の後ろの輪へ。頭は細る箱、くちばし、橙の目</summary>
        static void PigeonHead(Plumage b, Stance st)
        {
            var turn = Quaternion.Euler(st.turn);
            System.Func<Vector3, Vector3> at = p => st.head + turn * p;
            // 首
            var neck = PigeonRings[0];
            var mid = (new Vector3(0f, neck.y, neck.x) + at(new Vector3(0f, -0.006f, -0.010f))) * 0.5f;
            for (var j = 0; j < 6; j++)
            {
                var k = (j + 1) % 6;
                var aj = PigeonSides[j] * Mathf.Deg2Rad;
                var ak = PigeonSides[k] * Mathf.Deg2Rad;
                // 首の上は太め。頭の幅とほぼ同じにして、細い棒の上に箱が載ったように見せない
                var hj = at(new Vector3(Mathf.Cos(aj) * 0.019f, -0.006f + Mathf.Sin(aj) * 0.019f, -0.010f));
                var hk = at(new Vector3(Mathf.Cos(ak) * 0.019f, -0.006f + Mathf.Sin(ak) * 0.019f, -0.010f));
                var lower = j == 2 || j == 3;
                b.QuadOut(RingPoint(neck, j, 1f), RingPoint(neck, k, 1f), hk, hj, mid, lower ? Plume.Purple : Plume.Green);
            }
            // 頭。後ろが太く前が細い箱
            var c = new Vector3[8];
            for (var i = 0; i < 8; i++)
            {
                var front = (i & 4) != 0;
                var hx = front ? 0.012f : 0.017f;
                var hy = front ? 0.013f : 0.018f;
                c[i] = at(new Vector3((i & 1) != 0 ? hx : -hx, 0.004f + ((i & 2) != 0 ? hy : -hy), front ? 0.022f : -0.020f));
            }
            var hc = at(new Vector3(0f, 0.004f, 0f));
            b.QuadOut(c[4], c[5], c[7], c[6], hc, Plume.Head); // 前
            b.QuadOut(c[0], c[1], c[3], c[2], hc, Plume.Head); // 後ろ
            b.QuadOut(c[2], c[3], c[7], c[6], hc, Plume.Head); // 上
            b.QuadOut(c[0], c[1], c[5], c[4], hc, Plume.Head); // 下
            b.QuadOut(c[1], c[3], c[7], c[5], hc, Plume.Head); // 右
            b.QuadOut(c[0], c[2], c[6], c[4], hc, Plume.Head); // 左
            // くちばし。前の面の下寄りから、少し下へ尖らせる
            var tip = at(new Vector3(0f, -0.008f, 0.047f));
            var q = new[]
            {
                at(new Vector3(-0.005f, 0.000f, 0.022f)), at(new Vector3(0.005f, 0.000f, 0.022f)),
                at(new Vector3(0.005f, -0.008f, 0.022f)), at(new Vector3(-0.005f, -0.008f, 0.022f)),
            };
            var bc = (q[0] + q[1] + q[2] + q[3]) * 0.25f;
            for (var i = 0; i < 4; i++)
            {
                var p0 = q[i];
                var p1 = q[(i + 1) % 4];
                b.Tri(p0, p1, tip, (p0 + p1 + tip) / 3f - bc, Plume.Beak);
            }
            // 目。頭の脇の少し前
            foreach (var sx in new[] { 1f, -1f })
            {
                var x = sx * 0.0148f;
                var e0 = at(new Vector3(x, 0.004f, 0.001f));
                var e1 = at(new Vector3(x, 0.004f, 0.011f));
                var e2 = at(new Vector3(x, 0.013f, 0.011f));
                var e3 = at(new Vector3(x, 0.013f, 0.001f));
                b.Quad(e0, e1, e2, e3, turn * new Vector3(sx, 0f, 0f), Plume.Eye);
            }
        }

        /// <summary>たたんだ翼。背から脇へ三つの点の断面を肩から翼の先へ並べ、二本の黒い帯と濃い風切りを入れる</summary>
        static void PigeonFolded(Plumage b, Stance st, float side)
        {
            // 震わせるときは、脇の側を開いて背から少し浮かせる
            var open = st.wings == 1 ? 1f : 0f;
            System.Func<float[], int, Vector3> p = (row, n) =>
            {
                var x = row[1 + n * 2];
                var y = row[2 + n * 2];
                x += open * (0.004f + n * 0.010f);
                y += open * (0.006f + n * 0.004f);
                return new Vector3(side * x, y, row[0]);
            };
            for (var i = 0; i + 1 < PigeonFold.Length; i++)
            {
                var a = PigeonFold[i];
                var c = PigeonFold[i + 1];
                var part = PigeonFoldParts[i];
                var outward = new Vector3(side, 1.2f, 0f);
                b.Quad(p(a, 0), p(c, 0), p(c, 1), p(a, 1), outward, part);
                b.Quad(p(a, 1), p(c, 1), p(c, 2), p(a, 2), new Vector3(side, 0.2f, 0f), part);
            }
        }

        /// <summary>
        /// 広げた翼。肩から手首まで（腕）と手首から先（手）の二枚を、肩を軸に上げ下げする。
        /// 上の面は灰に二本の黒い帯と濃い後ろの縁、手の先は濃い風切り。下の面は白っぽい
        /// </summary>
        static void PigeonSpread(Plumage b, Stance st, float side)
        {
            float lift, bend, sweep;
            switch (st.wings)
            {
                case 2: lift = 62f; bend = 22f; sweep = -0.01f; break;
                case 4: lift = -48f; bend = -18f; sweep = 0.035f; break;
                default: lift = 6f; bend = -4f; sweep = 0f; break;
            }
            var shoulder = new Vector3(side * 0.040f, 0.160f, 0f);
            var arm = Quaternion.AngleAxis(side * lift, Vector3.forward);
            var hand = Quaternion.AngleAxis(side * (lift + bend), Vector3.forward);
            // 手首
            var wrist = shoulder + arm * new Vector3(side * 0.13f, 0f, 0f);
            System.Func<float, float, Vector3> onArm = (u, z) => shoulder + arm * new Vector3(side * u, 0f, 0f) + new Vector3(0f, 0f, z + sweep * u / 0.13f);
            System.Func<float, float, Vector3> onHand = (u, z) => wrist + hand * new Vector3(side * u, 0f, 0f) + new Vector3(0f, 0f, z + sweep);
            var upArm = arm * Vector3.up;
            var upHand = hand * Vector3.up;

            // 腕。前の縁から後ろの縁へ、灰・黒い帯・灰・黒い帯・濃い縁
            float[] cut = { 0f, 0.5f, 0.62f, 0.76f, 0.88f, 1f };
            Plume[] parts = { Plume.Wing, Plume.Bar, Plume.Wing, Plume.Bar, Plume.Tips };
            System.Func<float, float, Vector3> armAt = (u, k) =>
            {
                var lead = Mathf.Lerp(0.050f, 0.045f, u / 0.13f);
                var trail = Mathf.Lerp(-0.070f, -0.085f, u / 0.13f);
                return onArm(u, Mathf.Lerp(lead, trail, k));
            };
            for (var i = 0; i + 1 < cut.Length; i++)
                b.Quad(armAt(0f, cut[i]), armAt(0.13f, cut[i]), armAt(0.13f, cut[i + 1]), armAt(0f, cut[i + 1]), upArm, parts[i]);
            b.Quad(armAt(0f, 0f), armAt(0.13f, 0f), armAt(0.13f, 1f), armAt(0f, 1f), -upArm, Plume.Under);

            // 手。手首から半ばまで灰、先は濃い風切り
            var w0 = onHand(0f, 0.045f);
            var w1 = onHand(0f, -0.085f);
            var m0 = onHand(0.07f, 0.030f);
            var m1 = onHand(0.07f, -0.090f);
            var t0 = onHand(0.17f, -0.015f);
            var t1 = onHand(0.16f, -0.070f);
            b.Quad(w0, m0, m1, w1, upHand, Plume.Wing);
            b.Quad(m0, t0, t1, m1, upHand, Plume.Tips);
            b.Quad(w0, t0, t1, w1, -upHand, Plume.Under);
        }

        /// <summary>尾羽。腰から後ろへ少し下がる板で、先に濃い帯。飛ぶときは扇に開く</summary>
        static void PigeonTail(Plumage b, Stance st)
        {
            var w0 = 0.022f;
            var w1 = 0.032f * st.tail;
            var a0 = new Vector3(-w0, 0.116f, -0.100f);
            var a1 = new Vector3(w0, 0.116f, -0.100f);
            var k = 0.72f;
            var m0 = new Vector3(-Mathf.Lerp(w0, w1, k), Mathf.Lerp(0.116f, 0.098f, k), Mathf.Lerp(-0.100f, -0.195f, k));
            var m1 = new Vector3(Mathf.Lerp(w0, w1, k), m0.y, m0.z);
            var e0 = new Vector3(-w1, 0.098f, -0.195f);
            var e1 = new Vector3(w1, 0.098f, -0.195f);
            b.Quad(a0, a1, m1, m0, Vector3.up, Plume.Tail);
            b.Quad(m0, m1, e1, e0, Vector3.up, Plume.TailTip);
            b.Quad(a0, a1, m1, m0, Vector3.down, Plume.Tail);
            b.Quad(m0, m1, e1, e0, Vector3.down, Plume.TailTip);
        }

        /// <summary>赤い脚。三角の柱と、地面に伏せた指</summary>
        static void PigeonLeg(Plumage b, float side)
        {
            var top = new Vector3(side * 0.018f, 0.075f, 0.010f);
            var foot = new Vector3(side * 0.022f, 0.004f, 0.018f);
            var ring = new Vector3[6];
            for (var i = 0; i < 3; i++)
            {
                var a = (i * 120f + 90f) * Mathf.Deg2Rad;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                ring[i] = top + d * 0.006f;
                ring[i + 3] = foot + d * 0.005f;
            }
            var mid = (top + foot) * 0.5f;
            for (var i = 0; i < 3; i++)
            {
                var j = (i + 1) % 3;
                b.QuadOut(ring[i], ring[j], ring[j + 3], ring[i + 3], mid, Plume.Legs);
            }
            b.Tri(new Vector3(foot.x - 0.014f, 0.003f, foot.z + 0.034f), new Vector3(foot.x + 0.014f, 0.003f, foot.z + 0.034f),
                new Vector3(foot.x, 0.003f, foot.z - 0.018f), Vector3.up, Plume.Legs);
        }

        /// <summary>九つの姿勢の形。組み立て一回に一度だけ焼いて mesh のアセットにする</summary>
        static Mesh[] PigeonPoses()
        {
            var names = System.Enum.GetNames(typeof(Pigeon.Shape));
            var all = new Mesh[names.Length];
            for (var i = 0; i < names.Length; i++)
            {
                var key = "Pigeon" + names[i];
                Mesh mesh;
                if (shapes.TryGetValue(key, out mesh) && mesh != null) { all[i] = mesh; continue; }
                var made = PigeonShape((Pigeon.Shape)i);
                mesh = ProcMesh.Save(made, Generated + key + ".asset");
                if (mesh != made) Object.DestroyImmediate(made);
                shapes[key] = mesh;
                all[i] = mesh;
            }
            return all;
        }

        // ---- 群れ -------------------------------------------------------------------

        /// <summary>鳩の足元の高さ。小径の舗装（地面から 2 cm）に指が沈まない所</summary>
        const float DoveFeet = 0.02f;

        /// <summary>池の手前の、入らない楕円。縁石と輪の柵（縁から 0.55 m）より外。中心の x・z、x と z の半径</summary>
        static Vector4 DovePool { get { return new Vector4(Pond.x, Pond.z, ParkPondNear + 0.55f + 0.25f, ParkPondSide + 0.55f + 0.25f); } }

        /// <summary>出ない四角。柵の内</summary>
        static Vector4 DoveYard { get { return new Vector4(ParkWest + 0.6f, ParkEast - 0.6f, ParkSouth + 0.6f, GateZ - 0.35f); } }

        /// <summary>
        /// 鳩を一羽。形と色の段を決め、<see cref="Pigeon"/> を付ける。
        /// 羽色は種で決める。ふつうの灰が半分、濃い灰・白の混じり・茶の混じりが残りを分ける
        /// </summary>
        static Transform Dove(Transform flock, string name, Vector3 home, float yaw, int seed, Pigeon.Leg leg)
        {
            var poses = PigeonPoses();
            var pick = Mathf.Repeat(seed * 0.618034f + 0.11f, 1f);
            var row = pick < 0.5f ? 0 : pick < 0.7f ? 1 : pick < 0.85f ? 2 : 3;
            var bird = Piece(flock, name, poses[0], PigeonMat(row));
            bird.localPosition = home;
            bird.localRotation = Quaternion.Euler(0f, yaw, 0f);
            // 一羽ずつ少し大きさを違える
            bird.localScale = Vector3.one * (0.92f + Mathf.Repeat(seed * 0.381966f, 1f) * 0.16f);
            var pigeon = bird.gameObject.AddComponent<Pigeon>();
            var so = new SerializedObject(pigeon);
            var list = so.FindProperty("poses");
            list.arraySize = poses.Length;
            for (var i = 0; i < poses.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = poses[i];
            so.FindProperty("leg").enumValueIndex = (int)leg;
            so.FindProperty("seed").intValue = seed;
            so.FindProperty("lag").floatValue = Mathf.Repeat(seed * 0.7548777f, 1f);
            so.FindProperty("yaw").floatValue = yaw;
            so.FindProperty("spot").vector3Value = home;
            so.FindProperty("roam").floatValue = 0.35f + Mathf.Repeat(seed * 0.5698403f, 1f) * 0.25f;
            so.FindProperty("pool").vector4Value = DovePool;
            so.FindProperty("yard").vector4Value = DoveYard;
            so.ApplyModifiedPropertiesWithoutUndo();
            return bird;
        }

        /// <summary>種から 0〜1。鳩ごとの向きや秒のずらしに使う</summary>
        static float DoveNoise(int seed, int salt)
        {
            return Mathf.Repeat(Mathf.Sin(seed * 12.9898f + salt * 78.233f) * 43758.547f, 1f);
        }

        /// <summary>
        /// 飛び立つ群れ。<paramref name="spots"/> の所でついばみ、<paramref name="when"/> 秒
        /// （<paramref name="cue"/> 行が出てから。-1 なら記憶の時計）で一斉に飛び立つ。
        /// 飛ぶ向きは、驚かせた者（<paramref name="threat"/>。場所のローカルの x・z）から離れる向きに、
        /// 記憶ごとの向き <paramref name="away"/>（度。+z が 0）を <paramref name="bias"/> だけ混ぜ、±25° 散らす。
        /// 一つの向きへ揃えると、主の向こう側の鳩が主の頭の上を越えて飛ぶ。斜めに 3〜4 m 上がってから空へ散る。
        /// 飛び立つ秒は一羽ずつ 0〜0.3 秒ずらす。同じこまに全部が上がると一枚の板が浮いたように見える
        /// </summary>
        static Transform Doves(Transform take, Vector2[] spots, float when, int cue, Vector2 threat, float away, float bias, int seed)
        {
            var flock = Child(take, "Doves");
            for (var i = 0; i < spots.Length; i++)
            {
                var s = seed + i;
                var home = new Vector3(spots[i].x, DoveFeet, spots[i].y);
                var yaw = DoveNoise(s, 1) * 360f;
                var bird = Dove(flock, "Dove" + i, home, yaw, s, Pigeon.Leg.Leaves);
                var from = new Vector3(spots[i].x - threat.x, 0f, spots[i].y - threat.y);
                var lean = Quaternion.Euler(0f, away, 0f) * Vector3.forward * bias;
                var dir = (from.normalized + lean).normalized;
                dir = Quaternion.Euler(0f, (DoveNoise(s, 2) * 2f - 1f) * 25f, 0f) * dir;
                var to = home + dir * (2.2f + DoveNoise(s, 3)) + Vector3.up * (3.2f + DoveNoise(s, 4) * 1.2f);
                Move(bird, home, to, when + DoveNoise(s, 5) * 0.3f, 1.4f + DoveNoise(s, 6) * 0.4f, false, false, cue);
            }
            return flock;
        }

        /// <summary>飛び立たない鳩。ベンチの前でこぼれた餌をついばむ。寄ってくる人からは離れる</summary>
        static Transform Loiter(Transform take, string name, Vector2[] spots, int seed)
        {
            var flock = Child(take, name);
            for (var i = 0; i < spots.Length; i++)
            {
                var s = seed + i;
                Dove(flock, "Dove" + i, new Vector3(spots[i].x, DoveFeet, spots[i].y), DoveNoise(s, 1) * 360f, s, Pigeon.Leg.Stays);
            }
            return flock;
        }

        // ---- 記憶ごとの群れ ------------------------------------------------------------

        /// <summary>
        /// 15:47 の池の手前の十羽（記憶 2・3）。池の縁の舗装と、その東の芝の境に散らす。
        /// 主が〔区切り〕で歩いてくる池の縁（-1.1, 4.3）から 0.6〜2 m。孫娘と祖父が歩いてくる所にも少しかかり、寄られた鳩は歩いて退く
        /// </summary>
        static readonly Vector2[] PondEarly =
        {
            new Vector2(-1.55f, 5.05f), new Vector2(-1.25f, 5.35f), new Vector2(-1.75f, 5.55f), new Vector2(-0.95f, 5.75f),
            new Vector2(-1.45f, 5.95f), new Vector2(-1.85f, 6.25f), new Vector2(-1.10f, 6.30f), new Vector2(-0.60f, 5.30f),
            new Vector2(-1.60f, 4.60f), new Vector2(-0.35f, 6.05f),
        };

        /// <summary>15:47 のベンチ A の前の二羽。ソフィアが白い石を拾っていた所の東</summary>
        static readonly Vector2[] BenchEarly = { new Vector2(0.35f, 1.35f), new Vector2(-0.15f, 1.75f) };

        /// <summary>15:47 のベンチ B の前の四羽。ローザが餌を撒いていた（「妻は餌の袋を畳み」）</summary>
        static readonly Vector2[] FeedEarly =
        {
            new Vector2(2.95f, 1.10f), new Vector2(2.40f, 1.45f), new Vector2(3.35f, 1.55f), new Vector2(2.75f, 1.95f),
        };

        /// <summary>
        /// 15:50 の池の手前の八羽（記憶 9・10）。三分前に飛んだ群れが、少し減って戻っている。
        /// 記憶 10 の主（ルーカス）が頭に立つ池の縁（-1.6, 4.7）から 0.85 m より離し、南と北に分ける
        /// </summary>
        static readonly Vector2[] PondLate =
        {
            new Vector2(-1.55f, 3.85f), new Vector2(-1.20f, 3.55f), new Vector2(-0.85f, 4.05f), new Vector2(-1.70f, 3.30f),
            new Vector2(-0.60f, 3.60f), new Vector2(-1.05f, 5.35f), new Vector2(-1.60f, 5.55f), new Vector2(-0.70f, 5.05f),
        };

        /// <summary>15:50 のベンチ B の前に残った三羽</summary>
        static readonly Vector2[] FeedLate = { new Vector2(2.85f, 1.25f), new Vector2(2.45f, 1.70f), new Vector2(3.25f, 1.60f) };

        /// <summary>15:47 の群れは門の方（+z）へ、門と通りの上を越えて散る。ソフィアは 10° の向きに空を見上げる</summary>
        const float AwayEarly = 0f;
        /// <summary>15:50 の群れは、駆け込んでくる孫から逃げて池の上（南西）へ散る</summary>
        const float AwayLate = 225f;
        /// <summary>
        /// 記憶 10 の頭の群れは、祖母と門の方（北東）へ散る。記憶の頭で目は呼ぶ祖母へ回るので、
        /// 飛び立つ 2 秒の所では祖母の方を向いている
        /// </summary>
        const float AwayCalled = 45f;

        /// <summary>
        /// 15:47 の公園の鳩（記憶 2・3）。池の手前の十羽が <paramref name="cue"/> 行が出てから <paramref name="when"/> 秒で飛び立ち、
        /// ベンチの前の六羽は残る。<paramref name="stop"/> は主が池の縁で立ち止まる所（〔区切り〕の行き先）で、鳩はそこから離れる向きに飛ぶ
        /// </summary>
        static void DovesEarly(Transform take, float when, int cue, Vector2 stop)
        {
            Doves(take, PondEarly, when, cue, stop, AwayEarly, 0.8f, 11);
            Loiter(take, "BenchDoves", BenchEarly, 31);
            Loiter(take, "FeedDoves", FeedEarly, 41);
        }

        /// <summary>15:50 の公園の鳩（記憶 9）。池の手前の八羽が、駆け込んでくる孫に追われて飛び立つ。ベンチ B の前の三羽は残る</summary>
        static void DovesLate(Transform take, float when, int cue)
        {
            // 飛び立つ瞬間に孫が駆けている所（門の手前から池の縁へ走る線の七割ほど）から離れる
            Doves(take, PondLate, when, cue, new Vector2(-1.0f, 5.15f), AwayLate, 0.8f, 61);
            Loiter(take, "FeedDoves", FeedLate, 81);
        }

        /// <summary>
        /// 15:50 の公園の鳩（記憶 10）。池の手前の八羽は <paramref name="when"/> 秒で飛び立ち、
        /// <paramref name="cue"/> 行（「うわっ！　全部こっち来た」）が出たら空から門の前の足元へ降りてくる。
        /// ベンチ B の前の三羽も同じ行で、短く飛んで足元の輪の外側へ来る（「全部」）
        /// </summary>
        static void DovesFed(Transform take, float when, Vector3 feet, int cue)
        {
            // 主（池の縁のルーカス）から離れる
            var flock = Doves(take, PondLate, when, -1, new Vector2(-1.6f, 4.7f), AwayCalled, 0.6f, 61);
            // 足元の輪は主が向く西南西（門の前で撒く所の鍵打ち 250°）の側へ寄せ、門の柱の手前の祖父の足元は避ける
            for (var i = 0; i < flock.childCount; i++)
                Then(flock.GetChild(i), FeedRing(feet, i, flock.childCount, 150f, 340f), 0.1f + i * 0.08f, 1.3f, cue);
            var late = Child(take, "FeedDoves");
            for (var i = 0; i < FeedLate.Length; i++)
            {
                var s = 81 + i;
                var home = new Vector3(FeedLate[i].x, DoveFeet, FeedLate[i].y);
                var bird = Dove(late, "Dove" + i, home, DoveNoise(s, 1) * 360f, s, Pigeon.Leg.Lands);
                var to = FeedRing(feet, i, FeedLate.Length, 175f, 315f);
                to += (to - new Vector3(feet.x, to.y, feet.z)).normalized * 0.35f;
                Move(bird, home, to, 0.6f + i * 0.25f, 1.6f + DoveNoise(s, 6) * 0.3f, false, false, cue);
            }
        }

        /// <summary>
        /// 撒いた餌へ寄ってくる輪の一点。足元のまわりの <paramref name="from"/>〜<paramref name="to"/> 度（+x が 0、+z が 90）に
        /// <paramref name="count"/> 羽を並べたときの i 羽目。0.6〜1.05 m の輪に散らす。
        /// 撒いた主のすぐ足元（<see cref="Pigeon.TooClose"/> の内）へ降りると、降りた途端に短く飛んで離れてしまう
        /// </summary>
        static Vector3 FeedRing(Vector3 feet, int i, int count, float from, float to)
        {
            var a = Mathf.Deg2Rad * Mathf.Lerp(from, to, count > 1 ? i / (float)(count - 1) : 0.5f);
            var ring = 0.6f + ((i * 5) % 7) / 6f * 0.45f;
            return new Vector3(feet.x + Mathf.Cos(a) * ring, DoveFeet, feet.z + Mathf.Sin(a) * ring);
        }
    }
}
