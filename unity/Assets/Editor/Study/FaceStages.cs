using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HalfAware.EditorTools.Study
{
    /// <summary>
    /// 一つ目の道: 同じ模型（Quaternius の女性 W_Suit）に顔を描き込む段。
    /// 前の主人公を <see cref="OldProtagonist.Build"/> でそのまま組み立て、顔の面（FacePlate）の
    /// メッシュとマテリアルだけを段ごとの写しに差し替える。本番のアセットには触らない。
    ///
    /// - 段 0: 今のまま（FacePlate.asset と Face.mat）
    /// - 段 1: 今の面と今の解像度（256）のまま、絵を描き直す（<see cref="FacePaint"/>）
    /// - 段 2: 512 の絵と、頭へ沿わせた面（<see cref="FaceMesh"/>）。髪の形も見直す
    /// - 段 3: 段 2 に、目だけ別の小さな面を重ねて光を拾わせる
    ///
    /// 主人公はほくろが左目の下、片割れは右目の下（鏡像の双子）。
    /// 本人の左は模型の −x（肩の骨 Shoulder.L が −x にある）
    /// </summary>
    public static class FaceStages
    {
        public const string Dir = "Assets/Study/Face";

        /// <summary>段 0 の片割れ。今の Face.png はほくろが本人の右目の下にある</summary>
        public const string Face0 = "Assets/Textures/Face.png";

        public static string Tag(int stage, bool twin) { return "s" + stage + (twin ? "_twin" : "_self"); }

        /// <summary>段の頭を組み立てる。壊すのは呼んだ側（using）</summary>
        public static FaceSubject Build(int stage, bool twin)
        {
            var holder = new GameObject("FaceStudyHead");
            holder.hideFlags = HideFlags.HideAndDontSave;
            holder.transform.position = FaceStudy.Origin;
            var who = new FaceSubject { Root = holder };
            try
            {
                var her = OldProtagonist.Build(holder.transform);
                if (her == null) throw new InvalidOperationException("主人公を組み立てられない");
                FaceSubject.HideAll(holder);
                who.AdoptLooseMaterials();

                var plate = holder.transform.Find("Protagonist").GetComponentsInChildren<MeshRenderer>(true);
                MeshRenderer face = null;
                foreach (var r in plate) if (r.name == "FacePlate") face = r;
                if (face == null) throw new InvalidOperationException("FacePlate が無い");

                switch (stage)
                {
                    case 0: Stage0(who, face, twin); break;
                    case 1: Stage1(who, face, twin); break;
                    case 2: Stage2(who, her, face, twin, false); break;
                    case 3: Stage2(who, her, face, twin, true); break;
                    default: throw new ArgumentException("段は 0〜3");
                }

                // 顔の中心と目の高さ。面の入れ物（頭の骨の子、倍率 0.01）の中の値からワールドへ
                var a = face.transform;
                who.FaceCentreLocal = holder.transform.InverseTransformPoint(a.TransformPoint(new Vector3(0f, -0.012f, 0.2425f)));
                who.EyeLineLocal = holder.transform.InverseTransformPoint(a.TransformPoint(new Vector3(0f, 0.004f, 0.2425f)));
                who.FaceWidth = SkinWidth(her, who.EyeLine.y);
                who.Isolate();
                return who;
            }
            catch
            {
                who.Dispose();
                throw;
            }
        }

        static void Stage0(FaceSubject who, MeshRenderer face, bool twin)
        {
            who.MoleOff.Add(new KeyValuePair<MaskSlot, Material>(new MaskSlot(face, 0, null), Load<Material>(Dir + "/Face0_nomole.mat")));
            // 片割れは今の Face.mat そのまま。主人公はほくろを左目の下へ移した写し
            if (twin)
            {
                who.MaskSlots.Add(new MaskSlot(face, 0, Load<Texture2D>(Dir + "/Mask0_twin.png")));
                who.Note = "段 0: 今の FacePlate と Face.mat";
            }
            else
            {
                face.sharedMaterial = Load<Material>(Dir + "/Face0_self.mat");
                who.MaskSlots.Add(new MaskSlot(face, 0, Load<Texture2D>(Dir + "/Mask0_self.png")));
                who.Note = "段 0: 今の Face.png のほくろを左目の下へ移しただけ";
            }
        }

        static void Stage1(FaceSubject who, MeshRenderer face, bool twin)
        {
            var side = twin ? "twin" : "self";
            face.sharedMaterial = Load<Material>(Dir + "/Face1_" + side + ".mat");
            who.MaskSlots.Add(new MaskSlot(face, 0, Load<Texture2D>(Dir + "/Mask1_" + side + ".png")));
            who.MoleOff.Add(new KeyValuePair<MaskSlot, Material>(new MaskSlot(face, 0, null), Load<Material>(Dir + "/Face1_nomole.mat")));
            who.Note = "段 1: 今の面、256 の描き直し（α ブレンド）";
        }

        static void Stage2(FaceSubject who, GameObject her, MeshRenderer face, bool twin, bool eyes)
        {
            var side = twin ? "twin" : "self";
            var n = eyes ? "3" : "2";
            // 頭の写しは面の入れ物の位置だけで切る。面のメッシュを替える前でも後でもよい
            FaceMesh.Restyle(who, her, face.transform);
            face.GetComponent<MeshFilter>().sharedMesh = Load<Mesh>(Dir + "/FacePlate2.asset");
            face.sharedMaterial = Load<Material>(Dir + "/Face" + n + "_" + side + ".mat");
            who.MaskSlots.Add(new MaskSlot(face, 0, Load<Texture2D>(Dir + "/Mask" + n + "_" + side + ".png")));
            who.MoleOff.Add(new KeyValuePair<MaskSlot, Material>(new MaskSlot(face, 0, null), Load<Material>(Dir + "/Face" + n + "_nomole.mat")));
            who.Note = "段 2: 頭へ沿わせた面、512 の絵";
            if (!eyes) return;
            var ballMat = Load<Material>(Dir + "/EyeBall.mat");
            var ballMask = Load<Texture2D>(Dir + "/MaskBall.png");
            foreach (var s in new[] { "L", "R" })
            {
                var g = new GameObject("EyeBall" + s);
                g.hideFlags = HideFlags.HideAndDontSave;
                g.transform.SetParent(face.transform, false);
                g.AddComponent<MeshFilter>().sharedMesh = Load<Mesh>(Dir + "/EyeBall" + s + ".asset");
                var r = g.AddComponent<MeshRenderer>();
                r.sharedMaterial = ballMat;
                who.MaskSlots.Add(new MaskSlot(r, 0, ballMask));
            }
            who.Note = "段 3: 段 2 の面の目を抜き、奥に目の玉の面";
        }

        // ---- 段 1〜3 のアセットを作る ------------------------------------------

        /// <summary>段 1。今の面（UV は x −77.5〜77.5 mm、y −73〜73 mm へ真っ直ぐ）に 256 で描き直す</summary>
        public static string Prepare1()
        {
            var area = new Rect(-0.0775f, -0.073f, 0.155f, 0.146f);
            return FacePaint.Write("Face1", 256, area, FacePaint.Layout.Stage1(), Load<Material>(OldProtagonist.FaceMaterial), FacePaint.MakeTransparent);
        }

        /// <summary>段 2。頭へ沿わせた面を作って保存し、512 で描く</summary>
        public static string Prepare2()
        {
            EnsureDir();
            var l = FacePaint.Layout.Stage2();
            var mesh = WithHer((her, plate) => FaceMesh.BuildPlate2(her, plate, l));
            var saved = SaveMesh(mesh, Dir + "/FacePlate2.asset");
            var skin = SkinMaterial();
            var smooth = skin.GetFloat("_Smoothness");
            var r = FacePaint.Write("Face2", 512, FaceMesh.Area2, l, skin, m =>
            {
                m.SetColor("_BaseColor", Color.white);
                FacePaint.MakeOpaque(m, false, smooth);
            });
            return string.Format("段 2: 面 頂点 {0} 三角 {1} / {2}", saved.vertexCount, saved.triangles.Length / 3, r);
        }

        /// <summary>段 3。段 2 の絵の目の開きを抜いた絵と、目の玉の面・絵・マテリアル</summary>
        public static string Prepare3()
        {
            EnsureDir();
            var l = FacePaint.Layout.Stage2();
            l.eyeHoles = true;
            var skin = SkinMaterial();
            var smooth = skin.GetFloat("_Smoothness");
            var r = FacePaint.Write("Face3", 512, FaceMesh.Area2, l, skin, m =>
            {
                m.SetColor("_BaseColor", Color.white);
                FacePaint.MakeOpaque(m, true, smooth);
            });
            const int size = 128;
            var half = FaceMesh.BallHalfX;
            var ball = FacePaint.Ball(size, l, half);
            var tex = SavePng(ball.Pixels(), size, size, Dir + "/EyeBall.png", false);
            SavePng(ball.MaskPixels(), size, size, Dir + "/MaskBall.png", true);
            var bm = new Material(skin) { name = "EyeBall" };
            bm.SetColor("_BaseColor", Color.white);
            bm.SetTexture("_BaseMap", tex);
            FacePaint.MakeOpaque(bm, false, 0.88f);
            bm.SetFloat("_SpecularHighlights", 1f);
            bm.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
            SaveMaterial(bm, Dir + "/EyeBall.mat");
            var plate = Load<Mesh>(Dir + "/FacePlate2.asset");
            var tris = 0;
            foreach (var s in new[] { -1f, 1f })
            {
                var depth = FaceMesh.PlateDepth(plate, s * l.eyeX, l.eyeY, 0.022f);
                var front = depth.At(s * l.eyeX, l.eyeY) - FaceMesh.BallSink;
                var m = FaceMesh.Ball(s, front, l, half, depth);
                var saved = SaveMesh(m, Dir + "/EyeBall" + (s < 0 ? "L" : "R") + ".asset");
                tris += saved.triangles.Length / 3;
            }
            return string.Format("段 3: 目の玉 三角 {0}（二つで）絵 {1}×{1} / {2}", tris, size, r);
        }

        /// <summary>
        /// ほくろの大きさを変えて撮り比べる（段 2 の面と絵で、柔らかい光、正面と左 30 度、三つの近さ）。
        /// 絵はその場で作ってアセットにしない。tag は <c>sweep_r{半径 mm}</c>
        /// </summary>
        public static string MoleSweep(float[] radiiMm)
        {
            var sb = new System.Text.StringBuilder();
            var skin = SkinMaterial();
            var smooth = skin.GetFloat("_Smoothness");
            var made = new List<Object>();
            try
            {
                using (var rig = new FaceStudy.Rig(FaceStudy.Lighting.Soft))
                {
                    foreach (var mm in radiiMm)
                    {
                        var l = FacePaint.Layout.Stage2();
                        l.moleR = mm * 0.001f;
                        Func<Color32[], bool, Texture2D> tex = (px, lin) =>
                        {
                            var t = new Texture2D(512, 512, TextureFormat.RGBA32, true, false);
                            t.hideFlags = HideFlags.HideAndDontSave;
                            t.wrapMode = TextureWrapMode.Clamp;
                            t.filterMode = FilterMode.Bilinear;
                            t.SetPixels32(px);
                            t.Apply(true);
                            made.Add(t);
                            return t;
                        };
                        var with = FacePaint.Face(512, FaceMesh.Area2, l, false, true);
                        var bare = FacePaint.Face(512, FaceMesh.Area2, l, false, false);
                        Func<Texture2D, Material> mat = t =>
                        {
                            var m = new Material(skin);
                            m.hideFlags = HideFlags.HideAndDontSave;
                            m.SetColor("_BaseColor", Color.white);
                            m.SetTexture("_BaseMap", t);
                            FacePaint.MakeOpaque(m, false, smooth);
                            made.Add(m);
                            return m;
                        };
                        var withMat = mat(tex(with.Pixels(), false));
                        var bareMat = mat(tex(bare.Pixels(), false));
                        var mask = tex(with.MaskPixels(), false);
                        var tag = string.Format(System.Globalization.CultureInfo.InvariantCulture, "sweep_r{0:0.0}", mm);
                        using (var who = Build(2, false))
                        {
                            var face = who.MaskSlots[0].renderer;
                            face.sharedMaterial = withMat;
                            who.MaskSlots.Clear();
                            who.MaskSlots.Add(new MaskSlot(face, 0, mask));
                            who.MoleOff.Clear();
                            who.MoleOff.Add(new KeyValuePair<MaskSlot, Material>(new MaskSlot(face, 0, null), bareMat));
                            rig.Light(who);
                            foreach (var d in FaceStudy.Distances)
                                foreach (var yaw in new[] { -30f, 0f })
                                {
                                    var name = string.Format(System.Globalization.CultureInfo.InvariantCulture, "sweep/{0}_{1:0.0}m_{2}", tag, d, FaceStudy.YawName(yaw));
                                    var m = FaceStudy.ShootOne(rig, who, d, yaw, name, null);
                                    FaceStudy.Record(tag, "Soft", d, yaw, m);
                                    sb.AppendLine(name + " " + m.Short());
                                }
                        }
                    }
                }
            }
            finally
            {
                foreach (var o in made) if (o != null) Object.DestroyImmediate(o);
            }
            return sb.ToString();
        }

        /// <summary>頭の Skin マテリアル（素体の FBX の中）</summary>
        public static Material SkinMaterial()
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(OldProtagonist.SourceModel);
            foreach (var rr in src.GetComponentsInChildren<Renderer>(true))
                foreach (var m in rr.sharedMaterials)
                    if (m != null && m.name == "Skin") return m;
            throw new InvalidOperationException("Skin マテリアルが見つからない");
        }

        /// <summary>主人公を一体組み、頭と面の入れ物を渡して、終われば壊す</summary>
        static T WithHer<T>(Func<GameObject, Transform, T> f)
        {
            var holder = new GameObject("FaceStudyPrepare");
            holder.hideFlags = HideFlags.HideAndDontSave;
            holder.transform.position = FaceStudy.Origin;
            var loose = new List<Material>();
            try
            {
                var her = OldProtagonist.Build(holder.transform);
                FaceSubject.HideAll(holder);
                Transform plate = null;
                foreach (var t in her.GetComponentsInChildren<Transform>(true)) if (t.name == "FacePlate") plate = t;
                foreach (var rr in her.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in rr.sharedMaterials)
                        if (m != null && !EditorUtility.IsPersistent(m)) loose.Add(m);
                return f(her, plate);
            }
            finally
            {
                Object.DestroyImmediate(holder);
                foreach (var m in loose) if (m != null) Object.DestroyImmediate(m);
            }
        }

        /// <summary>メッシュをアセットへ。既にあれば中身だけ入れ替える（GUID を変えない）</summary>
        public static Mesh SaveMesh(Mesh m, string path)
        {
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old != null)
            {
                // CopySerialized だと CPU 側の値だけが替わり、描く側のバッファに古い形が残る。API で入れ直す
                old.Clear();
                old.indexFormat = m.indexFormat;
                old.SetVertices(m.vertices);
                old.SetNormals(m.normals);
                old.SetUVs(0, m.uv);
                old.subMeshCount = m.subMeshCount;
                for (var s = 0; s < m.subMeshCount; s++) old.SetTriangles(m.GetTriangles(s), s);
                old.RecalculateBounds();
                old.name = Path.GetFileNameWithoutExtension(path);
                EditorUtility.SetDirty(old);
                AssetDatabase.SaveAssetIfDirty(old);
                Object.DestroyImmediate(m);
                return old;
            }
            m.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(m, path);
            AssetDatabase.SaveAssetIfDirty(m);
            return m;
        }

        /// <summary>頭の形を測って文字で返す（<see cref="FaceMesh.Survey"/>）。段 0 を組んで壊す</summary>
        public static string Survey()
        {
            var holder = new GameObject("FaceStudySurvey");
            holder.hideFlags = HideFlags.HideAndDontSave;
            holder.transform.position = FaceStudy.Origin;
            try
            {
                var her = OldProtagonist.Build(holder.transform);
                FaceSubject.HideAll(holder);
                Transform plate = null;
                foreach (var t in her.GetComponentsInChildren<Transform>(true)) if (t.name == "FacePlate") plate = t;
                var loose = new List<Material>();
                foreach (var r in her.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                        if (m != null && !EditorUtility.IsPersistent(m)) loose.Add(m);
                var s = FaceMesh.Survey(her, plate);
                foreach (var m in loose) if (m != null) Object.DestroyImmediate(m);
                return s;
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        /// <summary>
        /// 目の高さの肌の左右の広がり。頭の肌（Suit_Head の 0 番）を今の姿勢で焼き、
        /// 目の高さ ±1.2 cm の頂点のうち顔の前半分にあるものの x の幅
        /// </summary>
        static float SkinWidth(GameObject her, float eyeY)
        {
            var head = her.transform.Find("Suit_Head");
            var smr = head != null ? head.GetComponent<SkinnedMeshRenderer>() : null;
            if (smr == null) return 0.15f;
            var baked = new Mesh();
            try
            {
                smr.BakeMesh(baked, true);
                var m = smr.transform.localToWorldMatrix;
                var verts = baked.vertices;
                var tris = baked.GetTriangles(0);
                float lo = float.MaxValue, hi = float.MinValue, zc = 0f; var n = 0;
                foreach (var i in tris) { zc += m.MultiplyPoint3x4(verts[i]).z; n++; }
                zc /= Mathf.Max(1, n);
                foreach (var i in tris)
                {
                    var p = m.MultiplyPoint3x4(verts[i]);
                    if (Mathf.Abs(p.y - eyeY) > 0.012f || p.z < zc) continue;
                    lo = Mathf.Min(lo, p.x);
                    hi = Mathf.Max(hi, p.x);
                }
                return hi > lo ? hi - lo : 0.15f;
            }
            finally
            {
                Object.DestroyImmediate(baked);
            }
        }

        public static T Load<T>(string path) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null) throw new FileNotFoundException("検証のアセットが無い（先に FaceStages.Prepare を呼ぶ）: " + path);
            return a;
        }

        // ---- 検証のアセットを作る -------------------------------------------

        public static void EnsureDir()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Study")) AssetDatabase.CreateFolder("Assets", "Study");
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Study", "Face");
        }

        /// <summary>
        /// 段 0 の写し。今の Face.png から印の絵を起こし、主人公用にほくろを左目の下へ移した絵とマテリアルを作る。
        /// ほくろは色（62, 44, 40）がそのまま一致する画素、虹彩は色相 20〜45 度・彩度 0.4 以上・明度 0.2 以上
        /// </summary>
        public static string Prepare0()
        {
            EnsureDir();
            var src = new Texture2D(2, 2);
            src.LoadImage(File.ReadAllBytes(Face0));
            try
            {
                int w = src.width, h = src.height;
                var px = src.GetPixels32();
                var moved = (Color32[])px.Clone();
                var mole = new List<int>();
                for (var i = 0; i < px.Length; i++)
                {
                    var c = px[i];
                    if (c.a > 200 && Mathf.Abs(c.r - 62) <= 3 && Mathf.Abs(c.g - 44) <= 3 && Mathf.Abs(c.b - 40) <= 3) mole.Add(i);
                }
                // 主人公: ほくろを消して（透明に）、左右を裏返した位置に同じ形で置く
                foreach (var i in mole) moved[i] = new Color32(0, 0, 0, 0);
                foreach (var i in mole)
                {
                    int x = i % w, y = i / w;
                    moved[y * w + (w - 1 - x)] = px[i];
                }
                var twinMask = MaskOf(px, w, h);
                var selfMask = MaskOf(moved, w, h);

                SavePng(twinMask, w, h, Dir + "/Mask0_twin.png", true);
                SavePng(selfMask, w, h, Dir + "/Mask0_self.png", true);
                var selfTex = SavePng(moved, w, h, Dir + "/Face0_self.png", false);
                var mat = new Material(Load<Material>(OldProtagonist.FaceMaterial));
                mat.name = "Face0_self";
                mat.SetTexture("_BaseMap", selfTex);
                mat.SetTexture("_MainTex", selfTex);
                SaveMaterial(mat, Dir + "/Face0_self.mat");

                // ほくろを消しただけの絵。ほくろ有りと無しを撮り比べるのに使う
                var bare = (Color32[])px.Clone();
                foreach (var i in mole) bare[i] = new Color32(0, 0, 0, 0);
                var bareTex = SavePng(bare, w, h, Dir + "/Face0_nomole.png", false);
                var bareMat = new Material(Load<Material>(OldProtagonist.FaceMaterial));
                bareMat.name = "Face0_nomole";
                bareMat.SetTexture("_BaseMap", bareTex);
                bareMat.SetTexture("_MainTex", bareTex);
                SaveMaterial(bareMat, Dir + "/Face0_nomole.mat");
                return string.Format("段 0: ほくろ {0} 画素（{1}×{2}）", mole.Count, w, h);
            }
            finally
            {
                Object.DestroyImmediate(src);
            }
        }

        static Color32[] MaskOf(Color32[] px, int w, int h)
        {
            var mask = new Color32[px.Length];
            for (var i = 0; i < px.Length; i++)
            {
                var c = px[i];
                if (c.a < 90) { mask[i] = FacePaint.MaskGround(c.a); continue; }
                float hh, ss, vv;
                Color.RGBToHSV(c, out hh, out ss, out vv);
                if (Mathf.Abs(c.r - 62) <= 3 && Mathf.Abs(c.g - 44) <= 3 && Mathf.Abs(c.b - 40) <= 3) mask[i] = FacePaint.MaskMole(c.a);
                else if (hh * 360f >= 20f && hh * 360f <= 45f && ss >= 0.4f && vv >= 0.2f) mask[i] = FacePaint.MaskIris(c.a);
                else mask[i] = FacePaint.MaskOther(c.a);
            }
            return mask;
        }

        /// <summary>
        /// 絵を PNG で書き、取り込みの設定を揃える。今の Face.png と同じく sRGB・ミップマップ・双線形・端で止める。
        /// 印の絵は圧縮しない（色で印を読むので）
        /// </summary>
        public static Texture2D SavePng(Color32[] px, int w, int h, string path, bool mask)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            try
            {
                t.SetPixels32(px);
                t.Apply();
                File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(Application.dataPath), path), t.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(t);
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = true;
            imp.alphaSource = TextureImporterAlphaSource.FromInput;
            imp.alphaIsTransparency = !mask;
            imp.mipmapEnabled = true;
            imp.filterMode = FilterMode.Bilinear;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.isReadable = false;
            imp.textureCompression = mask ? TextureImporterCompression.Uncompressed : TextureImporterCompression.Compressed;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static Material SaveMaterial(Material m, string path)
        {
            var old = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (old != null)
            {
                old.shader = m.shader;
                old.CopyPropertiesFromMaterial(m);
                old.shaderKeywords = m.shaderKeywords;
                old.renderQueue = m.renderQueue;
                EditorUtility.SetDirty(old);
                AssetDatabase.SaveAssetIfDirty(old);
                Object.DestroyImmediate(m);
                return old;
            }
            AssetDatabase.CreateAsset(m, path);
            AssetDatabase.SaveAssetIfDirty(m);
            return m;
        }
    }
}
