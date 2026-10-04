using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HalfAware.EditorTools.Rocketbox
{
    /// <summary>
    /// 同じ顔の三人（主人公・片割れ・場面 6 の過去の主人公。<see cref="RocketboxPerson.AnimeFace"/>）の顔を、リアル系のアニメ寄りにする
    /// （オーナー、2026-10-05「見た目はもうちょっと美人というかアニメ寄りというかリアル系アニメ寄りにできないものかと思っている」。
    /// 検証（Assets/Editor/Study/FaceAnime.cs）の後、チークを薄くして採用。瞳は琥珀）。三つを組み合わせる。
    ///
    /// 1. 顔のテクスチャ: tools/make-anime-face.py が、その人の Painted/Head_self.png（手入れ・黒子・髪の色を済ませた絵）の上に描き重ね、
    ///    Painted/ に *_anime.png（色）と *_anime_mask.png（部位。R 肌（顔は 1、首から下と体は 0.5。体の肌は陰を深く当てる）・G 髪・B 艶）を書く。体・胸元・膝から下の絵は肌の色だけ変える
    /// 2. 顔の比率: <see cref="Shape"/> の表（目を少し大きく、鼻と顎を小さく細く）を、頭のメッシュ（*_nose_mesh.asset）へ焼く
    ///    （<see cref="Bake"/>。<see cref="BuildRocketboxProtagonist"/> の鼻の手入れが続けて呼ぶ）。ボーンは動かさないので、場面のファイルは変わらない
    /// 3. 影の付け方: マテリアル（Painted/ の Head_self・Hair・Body・Chest・Legs）を HalfAware/AnimeSkin にする（<see cref="ApplyMaterials"/>）。
    ///    アセットをその場で書き換えるので、場面のファイルは変わらない
    ///
    /// 描き直しの手順（Look の値や元の絵を変えたとき）:
    ///   1. HalfAware/Rocketbox/Paint the protagonist など（元の Head_self.png を描く。いつもどおり）
    ///   2. HalfAware/Rocketbox/Anime face: export maps（描くための地図を unity/Temp/AnimeFace/ に書く）
    ///   3. py -3.12 tools/make-anime-face.py（numpy・scipy・PIL）
    ///   4. HalfAware/Rocketbox/Anime face: apply（マテリアルと頭のメッシュ）
    /// 組み立て（<see cref="BuildRocketboxProtagonist.Build"/>）と描き直し（<see cref="BuildRocketboxProtagonist.Paint"/>）は、
    /// Painted/ に *_anime.png があればそれを使うので、Python を毎回は呼ばない
    /// </summary>
    public static class RocketboxAnimeFace
    {
        public const string ShaderName = "HalfAware/AnimeSkin";

        /// <summary>tools/make-anime-face.py が読む地図の置き場（人ごとの下の置き場）。Assets の外</summary>
        public static string WorkDir(RocketboxPerson who)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "AnimeFace", who.Name));
        }

        /// <summary>顔の三人</summary>
        public static IEnumerable<RocketboxPerson> People
        {
            get
            {
                foreach (var p in RocketboxPerson.All)
                    if (p.AnimeFace) yield return p;
            }
        }

        // ---- 顔の比率の表 ------------------------------------------------------

        /// <summary>
        /// 顔のボーンを一つ動かす・縮める。{S} は L と R の両方（左右対称）。
        /// move は模型の根の向き（m）で、x は顔の真ん中から外へ向けて正（左右で向きを返す）、y は上、z は前。
        /// scale は模型の根の向きの倍率（x 幅・y 高さ・z 奥行き）で、ボーンの軸のうち一番近い軸へ掛ける
        /// </summary>
        public sealed class Tweak
        {
            public string bone;
            public Vector3 move;
            public Vector3 scale = Vector3.one;
            public string why;
        }

        /// <summary>
        /// 顔の比率の表（2026-10-05 にオーナーが採用）。今の手入れ（顎のボーンの幅 0.95・顎を 2.5 度閉じる・鼻の高さと小鼻のメッシュの手入れ）の上に重ねる。
        /// 束ねた姿勢で頭のメッシュへ焼く（<see cref="Bake"/>）。顎のボーンは場面 6 で口を動かすのに回すが、焼いた後もそのまま回る
        /// </summary>
        public static readonly Tweak[] Shape =
        {
            new Tweak { bone = "Bip01 {S}Eye", scale = new Vector3(1.10f, 1.10f, 1.04f), why = "目の玉と目の縁を 1 割大きく（奥行きは控えめ）" },
            new Tweak { bone = "Bip01 {S}EyeBlinkTop", move = new Vector3(0.0005f, 0.0008f, 0.0003f), why = "上瞼を 0.8 mm 上げて目を縦に開く（虹彩の上の端に瞼が少し掛かる所まで）" },
            new Tweak { bone = "Bip01 {S}EyeBlinkBottom", move = new Vector3(0.0005f, -0.0007f, 0f), why = "下瞼を 0.7 mm 下げる" },
            new Tweak { bone = "Bip01 MNose", move = new Vector3(0f, 0.0004f, -0.0020f), scale = new Vector3(0.88f, 1f, 1f), why = "鼻先を 0.4 mm 上げ 2 mm 引き、小鼻を 12 % 細く（1.2 mm 上げると鼻の穴が正面から見えすぎた）" },
            new Tweak { bone = "Bip01 MJaw", scale = new Vector3(0.92f, 0.95f, 1f), why = "顎を 5 % 短く、8 % 細く（今の 0.95 の幅に重ねる。前後は縮めず、顎の先を引っ込めない）" },
            new Tweak { bone = "Bip01 {S}Masseter", move = new Vector3(-0.0040f, 0f, 0f), why = "えらを 4 mm 内へ" },
            new Tweak { bone = "Bip01 {S}Cheek", move = new Vector3(-0.0015f, 0.0008f, 0f), why = "頬を 1.5 mm 内へ、0.8 mm 上へ" },
            new Tweak { bone = "Bip01 {S}MouthCorner", move = new Vector3(-0.0010f, 0.0005f, 0f), why = "口の端を 1 mm 内へ（小さな口）、0.5 mm 上へ" },
        };

        /// <summary>
        /// 表のとおりにボーンを動かした形を、束ねた姿勢のメッシュの頂点と法線へ焼く。ボーンの重みのまま、
        /// 束ねた姿勢のボーンを動かしたときの頂点の位置を、新しい束ねた姿勢の頂点にする（子のボーンは親に付いて動く）。
        /// smr はその人の模型の写し（ボーンの名前と親子を引く）。模型の根を裏返していても（片割れ）、元の模型の向きで焼く。
        /// 動いた頂点の数を返す
        /// </summary>
        public static int Bake(Mesh mesh, SkinnedMeshRenderer smr, RocketboxPerson who, Tweak[] table = null)
        {
            table = table ?? Shape;
            var bones = smr.bones;
            var bind = mesh.bindposes;
            var n = Mathf.Min(bones.Length, bind.Length);
            // 模型の根の向きからメッシュの中の向きへ（元の模型のアセットで測る。裏返しを含めない）
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(who.Model);
            var srcSmr = src.GetComponentInChildren<SkinnedMeshRenderer>();
            var modelToMesh = srcSmr.transform.worldToLocalMatrix * src.transform.localToWorldMatrix;
            var index = new Dictionary<string, int>();
            for (var i = 0; i < n; i++) if (bones[i] != null) index[bones[i].name] = i;
            var parent = new int[n];
            for (var i = 0; i < n; i++)
            {
                parent[i] = -1;
                if (bones[i] == null || bones[i].parent == null) continue;
                int p;
                if (index.TryGetValue(bones[i].parent.name, out p)) parent[i] = p;
            }
            // 束ねた姿勢のボーン（メッシュの中）
            var w0 = new Matrix4x4[n];
            for (var i = 0; i < n; i++) w0[i] = bind[i].inverse;
            var w = (Matrix4x4[])w0.Clone();
            foreach (var tw in table)
            {
                var sides = tw.bone.Contains("{S}") ? new[] { "L", "R" } : new[] { "" };
                foreach (var side in sides)
                {
                    int b;
                    if (!index.TryGetValue(tw.bone.Replace("{S}", side), out b)) throw new InvalidOperationException("顔のボーンが無い: " + tw.bone.Replace("{S}", side));
                    // 本人の左は模型の −x
                    var outward = side == "L" ? -1f : 1f;
                    var mModel = new Vector3(tw.move.x * (side == "" ? 1f : outward), tw.move.y, tw.move.z);
                    var mMesh = modelToMesh.MultiplyVector(mModel);
                    var scale = Vector3.one;
                    var axes = new[] { Vector3.right, Vector3.up, Vector3.forward };
                    for (var a = 0; a < 3; a++)
                    {
                        if (Mathf.Approximately(tw.scale[a], 1f)) continue;
                        var dir = modelToMesh.MultiplyVector(axes[a]).normalized;
                        var best = 0;
                        var bestDot = -1f;
                        for (var k = 0; k < 3; k++)
                        {
                            var col = ((Vector3)w[b].GetColumn(k)).normalized;
                            var d = Mathf.Abs(Vector3.Dot(col, dir));
                            if (d > bestDot) { bestDot = d; best = k; }
                        }
                        scale[best] *= tw.scale[a];
                    }
                    var before = w[b];
                    var after = Matrix4x4.Translate(mMesh) * before * Matrix4x4.Scale(scale);
                    var delta = after * before.inverse;
                    w[b] = after;
                    // 子のボーンは親に付いて動く
                    for (var j = 0; j < n; j++)
                        if (j != b && Below(parent, j, b)) w[j] = delta * w[j];
                }
            }
            var move = new Matrix4x4[n];
            var normal = new Matrix4x4[n];
            var touched = new bool[n];
            for (var i = 0; i < n; i++)
            {
                move[i] = w[i] * bind[i];
                normal[i] = move[i].inverse.transpose;
                touched[i] = !Same(move[i], Matrix4x4.identity);
            }
            var v = mesh.vertices;
            var nr = mesh.normals;
            var bw = mesh.boneWeights;
            var count = 0;
            for (var i = 0; i < v.Length; i++)
            {
                var q = bw[i];
                if (!touched[q.boneIndex0] && !(q.weight1 > 0f && touched[q.boneIndex1]) && !(q.weight2 > 0f && touched[q.boneIndex2]) && !(q.weight3 > 0f && touched[q.boneIndex3])) continue;
                var p = Vector3.zero;
                var nn = Vector3.zero;
                Action<int, float> add = (bi, wt) =>
                {
                    if (wt <= 0f) return;
                    p += move[bi].MultiplyPoint3x4(v[i]) * wt;
                    if (nr.Length == v.Length) nn += normal[bi].MultiplyVector(nr[i]) * wt;
                };
                add(q.boneIndex0, q.weight0);
                add(q.boneIndex1, q.weight1);
                add(q.boneIndex2, q.weight2);
                add(q.boneIndex3, q.weight3);
                var total = q.weight0 + q.weight1 + q.weight2 + q.weight3;
                if (total <= 0f) continue;
                v[i] = p / total;
                if (nr.Length == v.Length && nn.sqrMagnitude > 1e-12f) nr[i] = nn.normalized;
                count++;
            }
            mesh.vertices = v;
            if (nr.Length == v.Length) mesh.normals = nr;
            mesh.RecalculateBounds();
            return count;
        }

        static bool Below(int[] parent, int j, int b)
        {
            for (var p = parent[j]; p >= 0; p = parent[p])
                if (p == b) return true;
            return false;
        }

        static bool Same(Matrix4x4 a, Matrix4x4 b)
        {
            for (var i = 0; i < 16; i++)
                if (Mathf.Abs(a[i] - b[i]) > 1e-7f) return false;
            return true;
        }

        // ---- マテリアル --------------------------------------------------------

        /// <summary>髪の色ごとの、髪の影・地の色・光の帯・縁の照り</summary>
        public sealed class HairLook
        {
            public Color shade, tint, ring, sheen;
            public float ringStrength;
        }

        /// <summary>黒い髪（主人公と過去の主人公）。地の色を青みへ、帯は青みの銀</summary>
        public static readonly HairLook Black = new HairLook
        {
            shade = new Color(0.42f, 0.42f, 0.50f),
            tint = new Color(0.80f, 0.86f, 1.0f),
            ring = new Color(0.55f, 0.62f, 0.82f),
            sheen = new Color(0.24f, 0.28f, 0.40f),
            ringStrength = 0.32f,
        };

        /// <summary>茶色の髪（片割れ）。青みを抜いて少し暖かく、帯と縁の照りも暖かく。茶色は地の色が明るいので、縁の照りと帯は黒い髪より弱く（元の茶色より明るく見えないように）</summary>
        public static readonly HairLook Brown = new HairLook
        {
            shade = new Color(0.45f, 0.38f, 0.36f),
            tint = new Color(0.97f, 0.94f, 0.90f),
            ring = new Color(0.80f, 0.64f, 0.48f),
            sheen = new Color(0.18f, 0.15f, 0.12f),
            ringStrength = 0.22f,
        };

        public static HairLook HairOf(RocketboxPerson who)
        {
            return who.Look().naturalHair ? Brown : Black;
        }

        /// <summary>
        /// その人の Painted/ のマテリアル（Head_self・Hair・Body と、あれば Chest・Legs）を、*_anime.png を使う HalfAware/AnimeSkin にする。
        /// アセットはその場で書き換える（GUID と名前を保つので、場面のファイルは変わらない）。絵が無ければ何もしない
        /// </summary>
        public static string ApplyMaterials(RocketboxPerson who)
        {
            var sb = new StringBuilder();
            var shader = Shader.Find(ShaderName);
            if (shader == null) throw new InvalidOperationException("シェーダーが無い: " + ShaderName);
            var dir = who.Painted;
            var hair = HairOf(who);
            // マテリアルの名前・色の絵・部位の絵・α で抜くか
            var parts = new[]
            {
                new[] { "Head_self", "Head_anime", "Head_anime_mask", "" },
                new[] { "Hair", "Hair_anime", "Hair_anime_mask", "clip" },
                new[] { "Body", "Body_anime", "Body_anime_mask", "" },
                new[] { "Chest", "Chest_anime", "Chest_anime_mask", "" },
                new[] { "Legs", "Legs_anime", "Legs_anime_mask", "" },
            };
            foreach (var part in parts)
            {
                var matPath = dir + part[0] + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (m == null) continue;
                var clip = part[3] == "clip";
                var tex = Import(dir + part[1] + ".png", true, clip);
                var mask = Import(dir + part[2] + ".png", false, false);
                if (tex == null || mask == null)
                {
                    sb.AppendLine("アニメ寄りの絵が無いので、今のまま（tools/make-anime-face.py を回す）: " + dir + part[1] + ".png");
                    continue;
                }
                var fresh = new Material(shader);
                m.shader = shader;
                // 値はシェーダーの既定から始める
                m.CopyPropertiesFromMaterial(fresh);
                Object.DestroyImmediate(fresh);
                m.shaderKeywords = new string[0];
                m.SetTexture("_BaseMap", tex);
                m.SetTexture("_MaskMap", mask);
                m.SetColor("_BaseColor", Color.white);
                m.SetFloat("_AlphaClip", clip ? 1f : 0f);
                if (clip) m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Cutoff", 0.45f);
                m.SetFloat("_Cull", (float)CullMode.Back);
                m.SetColor("_HairShade", hair.shade);
                m.SetColor("_HairTint", hair.tint);
                m.SetColor("_RingColor", hair.ring);
                m.SetColor("_HairSheen", hair.sheen);
                m.SetFloat("_RingStrength", hair.ringStrength);
                m.SetOverrideTag("RenderType", clip ? "TransparentCutout" : "Opaque");
                m.renderQueue = clip ? (int)RenderQueue.AlphaTest : -1;
                m.doubleSidedGI = false;
                EditorUtility.SetDirty(m);
                sb.AppendLine("マテリアル: " + matPath + "（" + part[1] + "）");
            }
            AssetDatabase.SaveAssets();
            return sb.ToString();
        }

        /// <summary>描いた絵の取り込みの設定を揃える（512 まで、端は伸ばす。部位の絵は線形で、α を持たない）</summary>
        static Texture2D Import(string path, bool srgb, bool alpha)
        {
            if (!File.Exists(path)) return null;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti == null) return null;
            var dirty = false;
            if (ti.sRGBTexture != srgb) { ti.sRGBTexture = srgb; dirty = true; }
            if (ti.maxTextureSize != 512) { ti.maxTextureSize = 512; dirty = true; }
            if (ti.wrapMode != TextureWrapMode.Clamp) { ti.wrapMode = TextureWrapMode.Clamp; dirty = true; }
            var source = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            if (ti.alphaSource != source) { ti.alphaSource = source; dirty = true; }
            if (ti.alphaIsTransparency != alpha) { ti.alphaIsTransparency = alpha; dirty = true; }
            if (dirty) ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---- 描くための地図 ----------------------------------------------------

        /// <summary>
        /// tools/make-anime-face.py が読む地図を <see cref="WorkDir"/> に書く（人ごと）。
        /// - head_map_1024.bin: 頭のテクスチャの画素ごとの、模型の根の中の位置（束ねた姿勢、m）と面の上か（float32 ×4、UV の v が 0 の行から）
        /// - head_hair_512.bin: その人の頭の絵で髪と見なした重み（float32、512×512、v が 0 の行から）
        /// - anchors.txt: 顔のボーンの位置（模型の根の中）と黒子の位置と UV
        /// </summary>
        public static string ExportMaps(RocketboxPerson who, int n = 1024)
        {
            var dir = WorkDir(who);
            Directory.CreateDirectory(dir);
            var maps = BuildRocketboxProtagonist.Maps.Get(who, n);
            var s = maps.Head;
            using (var w = new BinaryWriter(File.Create(Path.Combine(dir, "head_map_" + n + ".bin"))))
                for (var i = 0; i < s.P.Length; i++)
                {
                    w.Write(s.P[i].x);
                    w.Write(s.P[i].y);
                    w.Write(s.P[i].z);
                    w.Write(s.On[i] ? 1f : 0f);
                }
            var maps512 = BuildRocketboxProtagonist.Maps.Get(who, 512);
            int size;
            var head = BuildRocketboxProtagonist.ToColors(RocketboxTextures.ReadPng(who.HeadSrc, out size, out size));
            var info = RocketboxPaint.Head(head, maps512.Head, maps512.Anchors, who.Look(), false, who.IrisUv, who.IrisRadius);
            using (var w = new BinaryWriter(File.Create(Path.Combine(dir, "head_hair_512.bin"))))
                foreach (var h in info.Hair) w.Write(h);
            var a = maps.Anchors;
            var sb = new StringBuilder();
            Action<string, Vector3> put = (k, v) => sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0} {1:R} {2:R} {3:R}", k, v.x, v.y, v.z));
            put("eyeL", a.eyeL); put("eyeR", a.eyeR);
            put("blinkTopL", a.blinkTopL); put("blinkTopR", a.blinkTopR);
            put("browInL", a.browInL); put("browInR", a.browInR);
            put("browOutL", a.browOutL); put("browOutR", a.browOutR);
            put("upperLip", a.upperLip); put("lowerLip", a.lowerLip);
            put("mouthL", a.mouthL); put("mouthR", a.mouthR);
            put("nose", a.nose); put("cheekL", a.cheekL); put("cheekR", a.cheekR); put("head", a.head);
            put("mole", info.Mole);
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "moleUv {0:R} {1:R} 0", info.MoleUv.x, info.MoleUv.y));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "irisUv {0:R} {1:R} {2:R}", who.IrisUv.x, who.IrisUv.y, who.IrisRadius));
            File.WriteAllText(Path.Combine(dir, "anchors.txt"), sb.ToString());
            return "書いた: " + dir;
        }

        // ---- メニュー ----------------------------------------------------------

        [MenuItem("HalfAware/Rocketbox/Anime face: export maps")]
        public static void ExportMenu()
        {
            var sb = new StringBuilder();
            foreach (var p in People) sb.AppendLine(p + ": " + ExportMaps(p));
            sb.AppendLine("続けて py -3.12 tools/make-anime-face.py を回し、HalfAware/Rocketbox/Anime face: apply");
            Debug.Log(sb);
        }

        [MenuItem("HalfAware/Rocketbox/Anime face: apply")]
        public static void ApplyMenu()
        {
            Debug.Log(Apply());
        }

        /// <summary>顔の三人のマテリアルを AnimeSkin にし、頭のメッシュを組み直す（鼻の手入れと顔の比率を焼く）。場面のファイルと差込口の mesh には触らない</summary>
        public static string Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("再生中は組み直さない");
            var sb = new StringBuilder();
            foreach (var p in People)
            {
                sb.AppendLine(p.ToString());
                sb.Append(ApplyMaterials(p));
                sb.AppendLine(BuildRocketboxProtagonist.RebuildHeadMesh(p));
            }
            AssetDatabase.SaveAssets();
            return sb.ToString();
        }
    }
}
